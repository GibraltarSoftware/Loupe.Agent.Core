using Gibraltar.Agent.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loupe.Agent.AspNetCore.Metrics.EntityFrameworkCore
{
    internal class ConnectionMetric
    {
        private readonly EventMetric _metric;
        private readonly ConnectionEventData _eventData;
        private readonly string _countProperty;

        public ConnectionMetric(EventMetric metric, ConnectionEventData eventData, string countProperty)
        {
            _metric = metric;
            _eventData = eventData;
            _countProperty = countProperty;
        }

        public void Stop(ConnectionEndEventData eventData)
        {
            var sample = _metric.CreateSample();
            sample.SetValue(_countProperty, $"{_eventData.Connection.DataSource}.{_eventData.Connection.Database}");
            sample.SetValue("duration", eventData.Duration);
            sample.Write();
        }

        public void Stop(ConnectionErrorEventData eventData)
        {
            var sample = _metric.CreateSample();
            sample.SetValue(_countProperty, $"{_eventData.Connection.DataSource}.{_eventData.Connection.Database}");
            sample.SetValue("duration", eventData.Duration);
            sample.SetValue("error", eventData.Exception?.GetType().Name);
            sample.Write();
        }
    }
}