using System;
using Gibraltar.Agent.Metrics;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public class ActionMetricFactory
    {

        private readonly EventMetric _metric;
        public ActionMetricFactory(string applicationName)
        {
            var definition = new EventMetricDefinition(applicationName, "AspNetCore", "Action");
            definition.AddValue("action", typeof(string), SummaryFunction.Count, "Action", "Action",
                "The Action that was executed.");
            definition.AddValue("duration", typeof(TimeSpan), SummaryFunction.Average, "Duration", "Duration",
                "Time taken to execute the query");
            definition.AddValue("error", typeof(string), SummaryFunction.Count, "Errors", "Errors",
                "Errors executing query.");
            EventMetricDefinition.Register(ref definition);
            _metric = EventMetric.Register(definition, null);
        }

        public ActionMetric Start(long tickCount, string action)
        {
            return new ActionMetric(_metric, tickCount, action);
        }
    }
}