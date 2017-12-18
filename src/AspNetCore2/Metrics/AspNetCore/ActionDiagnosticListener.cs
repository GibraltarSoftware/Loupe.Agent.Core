using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DiagnosticAdapter;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public class ActionDiagnosticListener : ILoupeDiagnosticListener
    {
        private readonly ConcurrentDictionary<string, ActionMetric> _actions = new ConcurrentDictionary<string, ActionMetric>();
        private readonly ActionMetricFactory _actionMetricFactory;

        public ActionDiagnosticListener(LoupeAgent agent)
        {
            _actionMetricFactory = new ActionMetricFactory(agent.ApplicationName);
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start")]
        public virtual void HttpRequestInStart(IProxyHttpContext httpContext)
        {
            if (httpContext == null) return;
            _actions[httpContext.TraceIdentifier] = _actionMetricFactory.Start(httpContext);
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop")]
        public virtual void AfterHttpRequestIn(IProxyHttpContext httpContext)
        {
            if (httpContext == null) return;
            var activity = Activity.Current;
            if (_actions.TryRemove(httpContext.TraceIdentifier, out var metric))
            {
                metric.Stop(activity);
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnResourceExecuting")]
        public virtual void BeforeOnResourceExecuting(IProxyActionContext resourceExecutingContext, IProxyActionDescriptor actionDescriptor)
        {
            if (TryGetMetric(resourceExecutingContext?.HttpContext?.TraceIdentifier, out var metric))
            {
                metric.StartRequestExecution();
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnResourceExecuted")]
        public virtual void AfterOnResourceExecuted(IProxyActionContext resourceExecutedContext, IProxyActionDescriptor actionDescriptor)
        {
            if (TryGetMetric(resourceExecutedContext?.HttpContext?.TraceIdentifier, out var metric))
            {
                metric.StopRequestExecution();
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnResourceExecution")]
        public virtual void BeforeOnResourceExecution(IProxyActionContext resourceExecutingContext, IProxyActionDescriptor actionDescriptor)
        {
            if (TryGetMetric(resourceExecutingContext?.HttpContext?.TraceIdentifier, out var metric))
            {
                metric.StartRequestExecution();
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnResourceExecution")]
        public virtual void AfterOnResourceExecution(IProxyActionContext resourceExecutedContext, IProxyActionDescriptor actionDescriptor)
        {
            if (TryGetMetric(resourceExecutedContext?.HttpContext?.TraceIdentifier, out var metric))
            {
                metric.StopRequestExecution();
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnAuthorization")]
        public virtual void BeforeOnAuthorization(IProxyActionContext actionContext, IProxyActionDescriptor actionDescriptor)
        {
            if (TryGetMetric(actionContext?.HttpContext?.TraceIdentifier, out var metric))
            {
                metric.StartRequestAuthorization();
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnAuthorization")]
        public virtual void AfterOnAuthorization(IProxyActionContext actionContext, IProxyActionDescriptor actionDescriptor)
        {
            if (TryGetMetric(actionContext?.HttpContext?.TraceIdentifier, out var metric))
            {
                metric.StopRequestAuthorization();
            }
        }

        private bool TryGetMetric(string identifier, out ActionMetric metric)
        {
            if (identifier == null)
            {
                metric = null;
                return false;
            }
            return _actions.TryGetValue(identifier, out metric);
        }

        public string Name => "Microsoft.AspNetCore";
    }
}