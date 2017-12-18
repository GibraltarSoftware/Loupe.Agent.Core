using System;
using System.Diagnostics;
using Gibraltar.Agent.Metrics;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public class ActionMetric
    {
        private static readonly double TickResolution = Stopwatch.Frequency / 1000d;
        private readonly EventMetric _metric;
        private readonly IProxyHttpContext _context;
        private long _requestAuthorization;
        private long _requestExecution;

        public ActionMetric(EventMetric metric, IProxyHttpContext context)
        {
            _metric = metric;
            _context = context;
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
            }
        }

        public void StartRequestExecution()
        {
            _requestExecution = Stopwatch.GetTimestamp();
        }

        public void StopRequestExecution()
        {
            if (_requestExecution > 0)
            {
                _requestExecution = Stopwatch.GetTimestamp() - _requestExecution;
            }
        }

        public void Stop(Activity activity)
        {
            var sample = _metric.CreateSample();

            sample.SetValue(MetricValue.TotalDuration, activity.Duration.TotalMilliseconds);
            if (_requestAuthorization > 0)
            {
                sample.SetValue(MetricValue.AuthorizeRequestDuration, _requestAuthorization / TickResolution);
            }
            if (_requestExecution > 0)
            {
                sample.SetValue(MetricValue.RequestHandlerExecuteDuration, _requestExecution / TickResolution);
            }
            string path = _context.Request.Path;
            string page;
            if (path == "/" || string.IsNullOrEmpty(path))
            {
                page = "";
            }
            else if (path.Length == 1)
            {
                page = path;
            }
            else
            {
                int lastDelimiter = path.LastIndexOf('/', path.Length - 2);
                page = lastDelimiter >= 0 ? path.Substring(lastDelimiter + 1) : path;
            }
            sample.SetValue(MetricValue.AbsolutePath, _context.Request.Path);
            sample.SetValue(MetricValue.PageName, page);
            sample.SetValue(MetricValue.QueryString, _context.Request.QueryString);
            sample.Write();
        }

        public Exception Exception { get; set; }
    }
}