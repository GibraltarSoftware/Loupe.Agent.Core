using Loupe.Agent.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loupe.Agent.EntityFrameworkCore
{
    internal class CommandMetric
    {
        private readonly EventMetric _metric;
        private readonly CommandEventData _eventData;

        public CommandMetric(EventMetric metric, CommandEventData eventData)
        {
            _metric = metric;
            _eventData = eventData;
        }

        public void Stop(CommandExecutedEventData eventData)
        {
            var sample = _metric.CreateSample();
            sample.SetValue("query", _eventData.Command.CommandText);
            sample.SetValue("duration", eventData.Duration);
            sample.Write();
        }

        public void Stop(DataReaderDisposingEventData eventData)
        {
            var sample = _metric.CreateSample();
            sample.SetValue("query", _eventData.Command.CommandText);
            sample.SetValue("duration", eventData.Duration);
            sample.SetValue("rows", eventData.ReadCount);
            sample.Write();
        }

        public void Stop(CommandErrorEventData eventData)
        {
            var sample = _metric.CreateSample();
            sample.SetValue("query", _eventData.Command.CommandText);
            sample.SetValue("duration", eventData.Duration);
            sample.SetValue("error", eventData.Exception?.GetType().Name);
            sample.Write();
        }

        public void Stop(CommandEndEventData eventData)
        {
            var sample = _metric.CreateSample();
            sample.SetValue("query", _eventData.Command.CommandText);
            sample.SetValue("duration", eventData.Duration);
            sample.Write();
        }
    }
}