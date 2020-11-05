#if(!NETCORE3)
using Loupe.Agent.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DiagnosticAdapter;

namespace Loupe.Agent.AspNetCore.Metrics
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// Listener for ASP.NET Core diagnostics.
    /// </summary>
    /// <seealso cref="ILoupeDiagnosticListener" />
    public class ActionDiagnosticListener : ILoupeDiagnosticListener
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
        /// Called when a request begins.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        [DiagnosticName("Microsoft.AspNetCore.Hosting.BeginRequest")]
        public virtual void BeginRequest(HttpContext httpContext)
        {
            httpContext?.Features.Set(_actionMetricFactory.Start(httpContext));
        }

        /// <summary>
        /// Called when a request ends.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        [DiagnosticName("Microsoft.AspNetCore.Hosting.EndRequest")]
        public virtual void EndRequest(IProxyHttpContext httpContext)
        {
            httpContext?.Features.Get<ActionMetric>()?.Stop();
        }

        /// <summary>
        /// Called before an Action is executed.
        /// </summary>
        /// <param name="actionExecutingContext">The action executing context.</param>
        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnActionExecution")]
        public virtual void BeforeOnActionExecution(ActionExecutingContext actionExecutingContext)
        {
            actionExecutingContext?.HttpContext?.Features.Set(new RequestMetric(actionExecutingContext));
        }

        /// <summary>
        /// Called after an Action has been executed.
        /// </summary>
        /// <param name="actionExecutedContext">The action executed context.</param>
        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnActionExecution")]
        public virtual void AfterOnActionExecution(ActionExecutedContext actionExecutedContext)
        {
            actionExecutedContext?.HttpContext?.Features.Get<RequestMetric>()?.Record(actionExecutedContext);
        }

        /// <summary>
        /// Called before a Resource is executed.
        /// </summary>
        /// <param name="resourceExecutingContext">The resource executing context.</param>
        /// <param name="actionDescriptor">The action descriptor.</param>
        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnResourceExecuting")]
        public virtual void BeforeOnResourceExecuting(IProxyActionContext resourceExecutingContext, IProxyActionDescriptor actionDescriptor)
        {
            resourceExecutingContext?.HttpContext?.Features.Get<ActionMetric>()?.StartRequestExecution(actionDescriptor);
        }

        /// <summary>
        /// Called after a Resource has been executed.
        /// </summary>
        /// <param name="resourceExecutedContext">The resource executed context.</param>
        /// <param name="actionDescriptor">The action descriptor.</param>
        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnResourceExecuted")]
        public virtual void AfterOnResourceExecuted(IProxyActionContext resourceExecutedContext, IProxyActionDescriptor actionDescriptor)
        {
            resourceExecutedContext?.HttpContext?.Features.Get<ActionMetric>()?.StopRequestExecution();
        }

        /// <summary>
        /// Called before a Resource is executed.
        /// </summary>
        /// <param name="resourceExecutingContext">The resource executing context.</param>
        /// <param name="actionDescriptor">The action descriptor.</param>
        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnResourceExecution")]
        public virtual void BeforeOnResourceExecution(IProxyActionContext resourceExecutingContext, IProxyActionDescriptor actionDescriptor)
        {
            resourceExecutingContext?.HttpContext?.Features.Get<ActionMetric>()?.StartRequestExecution(actionDescriptor);
        }

        /// <summary>
        /// Called after a Resource has been executed.
        /// </summary>
        /// <param name="resourceExecutedContext">The resource executed context.</param>
        /// <param name="actionDescriptor">The action descriptor.</param>
        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnResourceExecution")]
        public virtual void AfterOnResourceExecution(IProxyActionContext resourceExecutedContext, IProxyActionDescriptor actionDescriptor)
        {
            resourceExecutedContext?.HttpContext?.Features.Get<ActionMetric>()?.StopRequestExecution();
        }

        /// <summary>
        /// Called before a request is authorized.
        /// </summary>
        /// <param name="actionContext">The action context.</param>
        /// <param name="actionDescriptor">The action descriptor.</param>
        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnAuthorization")]
        public virtual void BeforeOnAuthorization(IProxyActionContext actionContext, IProxyActionDescriptor actionDescriptor)
        {
            actionContext?.HttpContext?.Features.Get<ActionMetric>()?.StartRequestAuthorization();
        }

        /// <summary>
        /// Called after request authorization is completed.
        /// </summary>
        /// <param name="actionContext">The action context.</param>
        /// <param name="actionDescriptor">The action descriptor.</param>
        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnAuthorization")]
        public virtual void AfterOnAuthorization(IProxyActionContext actionContext, IProxyActionDescriptor actionDescriptor)
        {
            actionContext?.HttpContext?.Features.Get<ActionMetric>()?.StopRequestAuthorization();
        }

        /// <summary>
        /// Returns the name of the <see cref="T:System.Diagnostics.DiagnosticSource" /> this implementation targets.
        /// </summary>
        public string Name => "Microsoft.AspNetCore";
    }
}
#endif