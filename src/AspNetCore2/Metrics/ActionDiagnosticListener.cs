using Loupe.Agent.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DiagnosticAdapter;

namespace Loupe.Agent.AspNetCore.Metrics
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ActionDiagnosticListener : ILoupeDiagnosticListener
    {
        private readonly ActionMetricFactory _actionMetricFactory;

        public ActionDiagnosticListener(LoupeAgent agent)
        {
            _actionMetricFactory = new ActionMetricFactory(agent.ApplicationName);
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.BeginRequest")]
        public virtual void BeginRequest(HttpContext httpContext)
        {
            httpContext?.Features.Set(_actionMetricFactory.Start(httpContext));
        }
        
        [DiagnosticName("Microsoft.AspNetCore.Hosting.EndRequest")]
        public virtual void EndRequest(IProxyHttpContext httpContext)
        {
            httpContext?.Features.Get<ActionMetric>()?.Stop();
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnActionExecution")]
        public virtual void BeforeOnActionExecution(ActionExecutingContext actionExecutingContext)
        {
            actionExecutingContext?.HttpContext?.Features.Set(new RequestMetric(actionExecutingContext));
        }
        
        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnActionExecution")]
        public virtual void AfterOnActionExecution(ActionExecutedContext actionExecutedContext)
        {
            actionExecutedContext?.HttpContext?.Features.Get<RequestMetric>()?.Record(actionExecutedContext);
        }
        
        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnResourceExecuting")]
        public virtual void BeforeOnResourceExecuting(IProxyActionContext resourceExecutingContext, IProxyActionDescriptor actionDescriptor)
        {
            resourceExecutingContext?.HttpContext?.Features.Get<ActionMetric>()?.StartRequestExecution(actionDescriptor);
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnResourceExecuted")]
        public virtual void AfterOnResourceExecuted(IProxyActionContext resourceExecutedContext, IProxyActionDescriptor actionDescriptor)
        {
            resourceExecutedContext?.HttpContext?.Features.Get<ActionMetric>()?.StopRequestExecution();
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnResourceExecution")]
        public virtual void BeforeOnResourceExecution(IProxyActionContext resourceExecutingContext, IProxyActionDescriptor actionDescriptor)
        {
            resourceExecutingContext?.HttpContext?.Features.Get<ActionMetric>()?.StartRequestExecution(actionDescriptor);
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnResourceExecution")]
        public virtual void AfterOnResourceExecution(IProxyActionContext resourceExecutedContext, IProxyActionDescriptor actionDescriptor)
        {
            resourceExecutedContext?.HttpContext?.Features.Get<ActionMetric>()?.StopRequestExecution();
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnAuthorization")]
        public virtual void BeforeOnAuthorization(IProxyActionContext actionContext, IProxyActionDescriptor actionDescriptor)
        {
            actionContext?.HttpContext?.Features.Get<ActionMetric>()?.StartRequestAuthorization();
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnAuthorization")]
        public virtual void AfterOnAuthorization(IProxyActionContext actionContext, IProxyActionDescriptor actionDescriptor)
        {
            actionContext?.HttpContext?.Features.Get<ActionMetric>()?.StopRequestAuthorization();
        }

        public string Name => "Microsoft.AspNetCore";
    }
}