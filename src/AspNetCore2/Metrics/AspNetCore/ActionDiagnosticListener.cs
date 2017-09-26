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
        public virtual void BeforeAction(IProxyHttpContext httpContext,
            IProxyActionDescriptor actionDescriptor)
        {
            _actions[httpContext.TraceIdentifier] = _actionMetricFactory.Start(Environment.TickCount, actionDescriptor.DisplayName);
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterAction")]
        public virtual void AfterAction(IProxyHttpContext httpContext, IProxyActionDescriptor actionDescriptor)
        {
            long ticks = Environment.TickCount;
            if (_actions.TryRemove(httpContext.TraceIdentifier, out var metric))
            {
                metric.Stop(ticks);
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnActionExecuted")]
        public virtual void AfterOnActionExecuted(IProxyActionDescriptor actionDescriptor,
            IProxyActionExecutedContext actionExecutedContext)
        {
            if (actionExecutedContext.Exception == null) return;

            if (_actions.TryGetValue(actionExecutedContext.HttpContext.TraceIdentifier, out var metric))
            {
                metric.Exception = actionExecutedContext.Exception;
            }
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeOnException")]
        public virtual void BeforeOnException(IProxyActionDescriptor actionDescriptor,
            IProxyExceptionContext exceptionContext, IProxyExceptionFilter filter)
        {
            Debug.WriteLine("Microsoft.AspNetCore.Mvc.BeforeOnException");
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterOnException")]
        public virtual void AfterOnException(IProxyActionDescriptor actionDescriptor,
            IProxyExceptionContext exceptionContext, IProxyExceptionFilter filter)
        {
            Debug.WriteLine("Microsoft.AspNetCore.Mvc.AfterOnException");
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.BeforeActionResult")]
        public virtual void BeforeActionResult(IProxyActionContext actionContext, IProxyActionResult actionResult)
        {
            Debug.WriteLine("Microsoft.AspNetCore.Mvc.BeforeActionResult");
        }

        [DiagnosticName("Microsoft.AspNetCore.Mvc.AfterActionResult")]
        public virtual void AfterActionResult(IProxyActionContext actionContext, IProxyActionResult actionResult)
        {
            Debug.WriteLine("Microsoft.AspNetCore.Mvc.AfterActionResult");
        }

        public string Name => "Microsoft.AspNetCore";
    }

    public interface IProxyExceptionContext
    {
    }

    public interface IProxyExceptionFilter
    {
    }

    public interface IProxyActionExecutedContext
    {
        Exception Exception { get; }
        HttpContext HttpContext { get; }
    }

    public interface IProxyAsyncActionFilter
    {
    }

    public interface IProxyActionExecutingContext
    {
    }

    public interface IProxyHttpContext
    {
        string TraceIdentifier { get; }
    }

    public interface IProxyRouteData
    {
    }

    public interface IProxyActionDescriptor
    {
        string DisplayName { get; }
    }

    public interface IProxyActionContext
    {
        IProxyHttpContext HttpContext { get; }
        IProxyActionDescriptor ActionDescriptor { get; }
    }

    public interface IProxyActionResult
    {
        
    }
}