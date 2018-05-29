using System;
using Gibraltar.Agent.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loupe.Agent.EntityFrameworkCore
{
    internal class CommandMetricFactory
    {
        private readonly EventMetric _metric;
        public CommandMetricFactory(string applicationName)
        {
            var definition = new EventMetricDefinition(applicationName, "EntityFrameworkCore", "Command.Execute");
            definition.AddValue("query", typeof(string), SummaryFunction.Count, "Query", "Query",
                "The query that was executed.");
            definition.AddValue("duration", typeof(TimeSpan), SummaryFunction.Average, "Duration", "Duration",
                "Time taken to execute the query");
            definition.AddValue("rows", typeof(int), SummaryFunction.Average, "Rows", "Rows",
                "Rows returned by query");
            definition.AddValue("error", typeof(string), SummaryFunction.Count, "Errors", "Errors",
                "Errors executing query.");
            EventMetricDefinition.Register(ref definition);
            _metric = EventMetric.Register(definition, null);
        }

        public CommandMetric Start(CommandEventData eventData)
        {
            return new CommandMetric(_metric, eventData);
        }
    }
}