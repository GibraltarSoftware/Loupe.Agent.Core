using System;
using System.Diagnostics;
using System.Linq;
using Gibraltar.Agent.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
#pragma warning disable 1591

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Metric for all requests to ASP.NET
    /// </summary>
    public class RequestMetric
    {
        private static readonly double TickResolution = Stopwatch.Frequency / 1000d;
        private readonly EventMetric _metric;
        private readonly HttpContext _context;
        private long _request;
        private long _requestAuthorization;
        private long _requestExecution;
        private string? _pageName;

        public RequestMetric(EventMetric metric, HttpContext context)
        {
            _metric = metric;
            _context = context;
            _request = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Optional.  An action metric associated with this request.
        /// </summary>
        public ActionMetricBase? CurrentActionMetric { get; private set; }

        /// <summary>
        /// Optional.  An action metric associated with this request.
        /// </summary>
        public ActionMetricBase? RootActionMetric { get; private set; }

        /// <summary>
        /// set the current action metric
        /// </summary>
        /// <param name="metric"></param>
        public void SetActionMetric(ActionMetricBase metric)
        {
            if (RootActionMetric == null)
            {
                RootActionMetric = metric;
            }

            CurrentActionMetric = metric;
        }

        public void StartRequestAuthorization()
        {
            _requestAuthorization = Stopwatch.GetTimestamp();
        }

        public void StopRequestAuthorization()
        {
            if (_requestAuthorization > 0)
            {
                _requestAuthorization = Stopwatch.GetTimestamp() - _requestAuthorization;

                if (RootActionMetric != null)
                {
                    RootActionMetric.AuthorizeRequestDuration = new TimeSpan(_requestAuthorization);
                }
            }
        }

        public void StartRequestExecution(IProxyActionDescriptor? actionDescriptor)
        {
            _requestExecution = Stopwatch.GetTimestamp();
            if (actionDescriptor != null)
            {
                SetPageName($"{actionDescriptor.ControllerName}.{actionDescriptor.ActionName}");
            }
        }

        public void StartRequestExecution(string actionName)
        {
            _requestExecution = Stopwatch.GetTimestamp();
            SetPageName($"{actionName}");
        }
        
        public void StopRequestExecution()
        {
            if (_requestExecution > 0)
            {
                _requestExecution = Stopwatch.GetTimestamp() - _requestExecution;
            }
        }

        public void SetPageName(string pageName)
        {
            _pageName = pageName;
        }

        public void Stop()
        {
            _request = Stopwatch.GetTimestamp() - _request;

            if (RootActionMetric != null)
            {
                RootActionMetric.Duration = new TimeSpan(_request);
                RootActionMetric.Record(_context);
                return; //if it's an action we don't bother recording anything else.
            }

            var sample = _metric.CreateSample();

            sample.SetValue(MetricValue.TotalDuration, _request / TickResolution);

            if (_requestAuthorization > 0)
            {
                sample.SetValue(MetricValue.AuthorizeRequestDuration, _requestAuthorization / TickResolution);
            }

            if (_requestExecution > 0)
            {
                sample.SetValue(MetricValue.RequestHandlerExecuteDuration, _requestExecution / TickResolution);
            }

            string path = _context.Request.Path;
            string page = _pageName ?? path;
            sample.SetValue(MetricValue.AbsolutePath, path);
            sample.SetValue(MetricValue.PageName, page);
            sample.SetValue(MetricValue.QueryString, _context.Request.QueryString);
            sample.Write();
        }

        public Exception? Exception { get; set; }

        public void SetException(Exception exception)
        {
            Exception = exception;

            if (RootActionMetric != null)
                RootActionMetric.Exception = exception;
        }
    }
}