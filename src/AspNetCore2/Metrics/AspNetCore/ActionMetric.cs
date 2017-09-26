using System;
using Gibraltar.Agent.Metrics;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public class ActionMetric
    {
        private readonly EventMetric _metric;
        private readonly long _start;
        private readonly string _action;

        public ActionMetric(EventMetric metric, long start, string action)
        {
            _metric = metric;
            _start = start;
            _action = action;
        }

        public void Stop(long tickCount)
        {
            var sample = _metric.CreateSample();
            sample.SetValue("action", _action);
            sample.SetValue("duration", TimeSpan.FromTicks(tickCount - _start));
            if (Exception != null)
            {
                sample.SetValue("error", Exception.GetType().Name);
            }
            sample.Write();
        }

        public Exception Exception { get; set; }
    }
}