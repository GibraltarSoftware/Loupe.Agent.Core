#if  !(NETCOREAPP2_1)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Gibraltar.Agent;
using Loupe.Agent.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Loupe.Agent.AspNetCore.Metrics
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// Listener for ASP.NET Core diagnostics.
    /// </summary>
    /// <seealso cref="ILoupeDiagnosticListener" />
    public class ActionDiagnosticListener : ILoupeDiagnosticListener, IObserver<KeyValuePair<string, object>>
    {
        private readonly RequestMetricFactory _requestMetricFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionDiagnosticListener"/> class.
        /// </summary>
        /// <param name="agent">The Loupe agent.</param>
        public ActionDiagnosticListener(LoupeAgent agent)
        {
            _requestMetricFactory = new RequestMetricFactory();
        }

        /// <inheritdoc />
        public string Name => "Microsoft.AspNetCore";

        /// <inheritdoc />
        public void OnCompleted()
        {
        }

        /// <inheritdoc />
        public void OnError(Exception error)
        {
            Log.Error(error, LogWriteMode.Queued, "AspNetCoreDiagnosticListener", error.Message, "LoupeDiagnosticListener");
        }

        /// <inheritdoc />
        public void OnNext(KeyValuePair<string, object> value)
        {
            try
            {
                switch (value.Value)
                {
                    case HttpContext httpContext:
                        HandleHttpContextDiagnostic(httpContext, value.Key);
                        break;
                    case BeforeActionEventData beforeActionEventData:
                        var requestMetric = beforeActionEventData.HttpContext.Features.Get<RequestMetric>();
                        if (requestMetric == null)
                            break;

                        ActionMetricBase actionMetric = null;

                        //create the right action metric for our and associate it with our request metric.
                        switch (beforeActionEventData.ActionDescriptor)
                        {
                            case ControllerActionDescriptor controllerActionDescriptor:
                                actionMetric = new ControllerMetric(beforeActionEventData.HttpContext, controllerActionDescriptor);
                                break;
                            case PageActionDescriptor pageActionDescriptor:
                                actionMetric = new PageMetric(beforeActionEventData.HttpContext, pageActionDescriptor);
                                break;
                        }

                        if (actionMetric != null)
                        {
                            requestMetric.ActionMetric = actionMetric;
                        }

                        break;
                    case AfterActionEventData afterActionEventData:
                        var metric = afterActionEventData.HttpContext.Features.Get<RequestMetric>();

                        if (metric?.ActionMetric == null) break;

                        metric.ActionMetric.Stop(Activity.Current);
                        break;
                    case BeforeAuthorizationFilterOnAuthorizationEventData beforeAuthorization:
                        beforeAuthorization.AuthorizationContext.HttpContext.Features
                            .Get<RequestMetric>()?.StartRequestAuthorization();
                        break;
                    case AfterAuthorizationFilterOnAuthorizationEventData afterAuthorization:
                        afterAuthorization.AuthorizationContext.HttpContext.Features
                            .Get<RequestMetric>()?.StopRequestAuthorization();
                        break;
                    case BeforeActionFilterOnActionExecutingEventData beforeActionExecuting:
                        beforeActionExecuting.ActionExecutingContext.HttpContext.Features
                            .Get<RequestMetric>()?.StartRequestExecution(beforeActionExecuting.ActionDescriptor.DisplayName
                            ?? string.Empty);
                        break;
                    case AfterActionFilterOnActionExecutedEventData afterActionExecuted:
                        afterActionExecuted.ActionExecutedContext.HttpContext.Features
                            .Get<RequestMetric>()?.StopRequestExecution();
                        break;
                    case BeforeExceptionFilterOnException onException:
                        onException.ExceptionContext.HttpContext.Features.Get<RequestMetric>()
                            ?.SetException(onException.ExceptionContext.Exception);
                        break;
                }
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        private void HandleHttpContextDiagnostic(HttpContext httpContext, string key)
        {
            switch (key)
            {
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start":
                    httpContext.Features.Set(_requestMetricFactory.Start(httpContext));
                    break;
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                    httpContext.Features.Get<RequestMetric>()?.Stop();
                    break;
            }
        }
    }
}
#endif