#if(NETCORE3)
using System;
using System.Collections.Generic;
using Gibraltar.Agent;
using Loupe.Agent.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Loupe.Agent.AspNetCore.Metrics
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// Listener for ASP.NET Core diagnostics.
    /// </summary>
    /// <seealso cref="ILoupeDiagnosticListener" />
    public class ActionDiagnosticListener : ILoupeDiagnosticListener, IObserver<KeyValuePair<string, object>>
    {
        private readonly ActionMetricFactory _actionMetricFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionDiagnosticListener"/> class.
        /// </summary>
        /// <param name="agent">The Loupe agent.</param>
        public ActionDiagnosticListener(LoupeAgent agent)
        {
            _actionMetricFactory = new ActionMetricFactory();
        }

        /// <summary>
        /// Returns the name of the <see cref="T:System.Diagnostics.DiagnosticSource" /> this implementation targets.
        /// </summary>
        public string Name => "Microsoft.AspNetCore";

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            Log.Error(error, LogWriteMode.Queued, "AspNetCoreDiagnosticListener", error.Message, "LoupeDiagnosticListener");
        }

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
                        beforeActionEventData.HttpContext.Features.Set(new RequestMetric(beforeActionEventData.HttpContext, beforeActionEventData.ActionDescriptor));
                        break;
                    case AfterActionEventData afterActionEventData:
                        afterActionEventData.HttpContext.Features.Get<RequestMetric>()?.Record();
                        break;
                    case BeforeAuthorizationFilterOnAuthorizationEventData beforeAuthorization:
                        beforeAuthorization.AuthorizationContext.HttpContext.Features
                            .Get<ActionMetric>()?.StartRequestAuthorization();
                        break;
                    case AfterAuthorizationFilterOnAuthorizationEventData afterAuthorization:
                        afterAuthorization.AuthorizationContext.HttpContext.Features
                            .Get<ActionMetric>()?.StopRequestAuthorization();
                        break;
                    case BeforeActionFilterOnActionExecutingEventData beforeActionExecuting:
                        beforeActionExecuting.ActionExecutingContext.HttpContext.Features
                            .Get<ActionMetric>()?.StartRequestExecution(beforeActionExecuting.ActionDescriptor.DisplayName);
                        break;
                    case AfterActionFilterOnActionExecutedEventData afterActionExecuted:
                        afterActionExecuted.ActionExecutedContext.HttpContext.Features
                            .Get<ActionMetric>()?.StopRequestExecution();
                        break;
                    case BeforeExceptionFilterOnException onException:
                        onException.ExceptionContext.HttpContext.Features.Get<ActionMetric>()?.SetException(onException.ExceptionContext.Exception);
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
                    httpContext.Features.Set(_actionMetricFactory.Start(httpContext));
                    break;
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                    httpContext.Features.Get<ActionMetric>()?.Stop();
                    break;
            }
        }
    }
}
#endif