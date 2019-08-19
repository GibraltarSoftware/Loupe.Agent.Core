using System;
using Gibraltar.Agent.Metrics;
using Loupe.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loupe.Agent.EntityFrameworkCore
{
    internal class ConnectionMetricFactory
    {
        private readonly EventMetric _open;
        private readonly EventMetric _close;

        public ConnectionMetricFactory(string applicationName)
        {
            _open = CreateOpenDefinition(applicationName);
            _close = CreateCloseDefinition(applicationName);
        }

        public ConnectionMetric Opening(ConnectionEventData eventData)
        {
            return new ConnectionMetric(_open, eventData, "open");
        }

        public ConnectionMetric Closing(ConnectionEventData eventData)
        {
            return new ConnectionMetric(_close, eventData, "close");
        }

        private static EventMetric CreateOpenDefinition(string applicationName)
        {
            var openDefinition = new EventMetricDefinition(applicationName, "EntityFrameworkCore", "Connection.Open");
            openDefinition.AddValue("open", typeof(string), SummaryFunction.Count, "Open", "Open",
                "The connection that was opened.");
            openDefinition.AddValue("duration", typeof(TimeSpan), SummaryFunction.Average, "Duration", "Duration",
                "Time taken to open connection.");
            openDefinition.AddValue("error", typeof(string), SummaryFunction.Count, "Error", "Error",
                "Connection errors.");
            EventMetricDefinition.Register(ref openDefinition);
            return EventMetric.Register(openDefinition, null);
        }

        private static EventMetric CreateCloseDefinition(string applicationName)
        {
            var closeDefinition = new EventMetricDefinition(applicationName, "EntityFrameworkCore", "Connection.Close");
            closeDefinition.AddValue("close", typeof(string), SummaryFunction.Count, "Close", "Close",
                "The connection that was closed.");
            closeDefinition.AddValue("duration", typeof(TimeSpan), SummaryFunction.Average, "Duration", "Duration",
                "Time taken to close connection.");
            closeDefinition.AddValue("error", typeof(string), SummaryFunction.Count, "Error", "Error",
                "Connection errors.");
            EventMetricDefinition.Register(ref closeDefinition);
            return EventMetric.Register(closeDefinition, null);
        }
    }
}