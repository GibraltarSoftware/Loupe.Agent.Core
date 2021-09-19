#if  !(NETCOREAPP2_1)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Gibraltar.Agent;
using Loupe.Agent.Core.Services;
using Loupe.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.ObjectPool;

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
        private readonly AspNetConfiguration _options;
        private static readonly ObjectPool<StringBuilder> StringBuilderPool = new DefaultObjectPool<StringBuilder>(new StringBuilderPooledObjectPolicy());

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionDiagnosticListener"/> class.
        /// </summary>
        /// <param name="agent">The Loupe agent.</param>
        public ActionDiagnosticListener(LoupeAgent agent)
        {
            _requestMetricFactory = new RequestMetricFactory();

            _options = agent.Configuration.AspNet ?? new AspNetConfiguration();
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
                var processed = ProcessEventsByValueType(value); //these do type comparisons and are fast & frequent
                if (processed == false)
                {
                    ProcessEventsByKey(value); //these do string comparisons so are slower.
                }
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        /// <summary>
        /// Process events where we need to switch based on the key, which is slower.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ProcessEventsByKey(KeyValuePair<string, object> value)
        {
            switch (value.Key)
            {
                case "Microsoft.AspNetCore.Hosting.UnhandledException": //used when no exception handler is registered in the request pipeline
                case "Microsoft.AspNetCore.Diagnostics.UnhandledException": //used when an exception handler is registered 
                case "Microsoft.AspNetCore.Diagnostics.HandledException": //used when an exception handler is registered 
                {
                    //This can be made faster, but if we're in an exception flow performance is already not fabulous.
                    var httpContext = value.Value.GetProperty<HttpContext>("httpContext");
                    var exception = value.Value.GetProperty<Exception>("exception");

                    if (exception != null)
                        httpContext?.Features.Get<RequestMetric>()?.SetException(exception);
                    break;
                }
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start":
                {
                    var httpContext = value.Value as HttpContext;
                    httpContext?.Features.Set(_requestMetricFactory.Start(httpContext));
                    break;
                }
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                {
                    var httpContext = value.Value as HttpContext;
                    httpContext?.Features.Get<RequestMetric>()?.Stop();
                    break;
                }
                default:
                    return false; //we didn't handle it.
            }

            return true;
        }

        /// <summary>
        /// Process events where we can switch by type.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ProcessEventsByValueType(KeyValuePair<string, object> value)
        {
            switch (value.Value)
            {
                case BeforeActionEventData beforeActionEventData:
                    RequestMetric? requestMetric = beforeActionEventData.HttpContext.Features.Get<RequestMetric>();
                    if (requestMetric == null)
                        break;

                    if (requestMetric.ActionMetric != null)
                    {
                        //this is a secondary action - like an error page being displayed. Ignore it for now.
                        break;
                    }

                    ActionMetricBase actionMetric = null;

                    //create the right action metric for our and associate it with our request metric.
                    switch (beforeActionEventData.ActionDescriptor)
                    {
                        case ControllerActionDescriptor controllerActionDescriptor:
                            actionMetric = new ControllerMetric(_options, StringBuilderPool, beforeActionEventData.HttpContext,
                                controllerActionDescriptor);
                            break;
                        case PageActionDescriptor pageActionDescriptor:
                            actionMetric = new PageMetric(_options, StringBuilderPool, beforeActionEventData.HttpContext,
                                pageActionDescriptor);
                            break;
                    }

                    if (actionMetric != null)
                    {
                        requestMetric.ActionMetric = actionMetric;
                    }

                    break;
                case AfterActionEventData afterActionEventData:
                {
                    var metric = afterActionEventData.HttpContext.Features.Get<RequestMetric>();

                    if (metric?.ActionMetric == null) break;

                    metric.ActionMetric.Stop(Activity.Current);
                    break;
                }
                case BeforeAuthorizationFilterOnAuthorizationEventData beforeAuthorization:
                    beforeAuthorization.AuthorizationContext.HttpContext.Features
                        .Get<RequestMetric>()?.StartRequestAuthorization();
                    break;
                case AfterAuthorizationFilterOnAuthorizationEventData afterAuthorization:
                    afterAuthorization.AuthorizationContext.HttpContext.Features
                        .Get<RequestMetric>()?.StopRequestAuthorization();
                    break;
                case BeforeControllerActionMethodEventData beforeActionExecuting:
                {
                    var metric = beforeActionExecuting.ActionContext.HttpContext.Features
                        .Get<RequestMetric>();

                    if (metric == null) break;

                    if (metric.ActionMetric != null && _options.LogRequests && _options.LogRequestParameters)
                    {
                        metric.ActionMetric.SetParameterDetails(beforeActionExecuting.ActionArguments);
                    }

                    //since we're about to execute, record the request.
                    metric.ActionMetric?.RecordRequest();

                    metric.StartRequestExecution(beforeActionExecuting.ActionContext.ActionDescriptor.DisplayName
                                                 ?? string.Empty);
                    break;
                }
                case AfterControllerActionMethodEventData afterActionExecuted:
                    afterActionExecuted.ActionContext.HttpContext.Features
                        .Get<RequestMetric>()?.StopRequestExecution();
                    break;
                case BeforeExceptionFilterOnException onException:
                    onException.ExceptionContext.HttpContext.Features.Get<RequestMetric>()
                        ?.SetException(onException.ExceptionContext.Exception);
                    break;
                default:
                    return false; //we didn't handle it.
            }

            return true;
        }
    }
}
#endif