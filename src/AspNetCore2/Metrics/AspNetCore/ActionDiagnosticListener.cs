using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeAction")]
        public virtual void BeforeAction(IProxyHttpContext httpContext, IProxyActionDescriptor actionDescriptor)
        {
            _actions[httpContext.TraceIdentifier] = _actionMetricFactory.Start(Environment.TickCount, actionDescriptor.DisplayName);
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterAction")]
        public virtual void AfterAction(IProxyHttpContext httpContext)
        {
            long ticks = Environment.TickCount;
            if (_actions.TryRemove(httpContext.TraceIdentifier, out var metric))
            {
                metric.Stop(ticks);
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnActionExecuted")]
        public virtual void AfterOnActionExecuted(IProxyActionExecutedContext actionExecutedContext)
        {
            if (actionExecutedContext.Exception == null) return;

            if (_actions.TryGetValue(actionExecutedContext.HttpContext.TraceIdentifier, out var metric))
            {
                metric.Exception = actionExecutedContext.Exception;
            }
        }

        public string Name => "Microsoft.AspNetCore";
    }

    public interface IProxyActionExecutedContext
    {
        Exception Exception { get; }
        HttpContext HttpContext { get; }
    }
    public interface IProxyHttpContext
    {
        string TraceIdentifier { get; }
    }

    public interface IProxyActionDescriptor
    {
        string DisplayName { get; }
    }
}