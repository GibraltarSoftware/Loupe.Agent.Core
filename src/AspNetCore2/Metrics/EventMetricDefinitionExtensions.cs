using Gibraltar.Agent.Metrics;
using Loupe.Metrics;

namespace Loupe.Agent.AspNetCore.Metrics
{
    internal static class EventMetricDefinitionExtensions
    {
        public static EventMetricDefinition AddCount(this EventMetricDefinition definition, string name, string caption, string description)
        {
            definition.AddValue(name, typeof(string), SummaryFunction.Count, null, caption, description);
            return definition;
        }

        public static EventMetricDefinition AddHitCount(this EventMetricDefinition definition, string name, string caption, string description)
        {
            definition.AddValue(name, typeof(string), SummaryFunction.Count, "Hits", caption, description);
            return definition;
        }

        public static EventMetricDefinition AddDuration(this EventMetricDefinition definition, string name, string caption, string description)
        {
            definition.AddValue(name, typeof(double), SummaryFunction.Average, "Milliseconds", caption, description);
            return definition;
        }

        public static EventMetricDefinition AddFlag(this EventMetricDefinition definition, string name, string caption,
            string description)
        {
            definition.AddValue(name, typeof(bool), SummaryFunction.Average, "Hits", caption, description);
            return definition;
        }
    }
}