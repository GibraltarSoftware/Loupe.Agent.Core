using System;
using Loupe.Agent.Metrics;
using Loupe.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loupe.Agent.EntityFrameworkCore.Internal
{
    [SampledMetric("Gibraltar", "Database")]
    [EventMetric("Gibraltar", "Database", "Connection", Caption = "Database Query Performance",
        Description = "Performance data for every database query")]
    internal class ConnectionMetric
    {
        public ConnectionMetric(ConnectionEventData eventData)
        {
            InstanceName = eventData.Connection == null
                ? null
                : $"{eventData.Connection.DataSource}: {eventData.Connection.Database}";

            ConnectionId = eventData.ConnectionId;
            Server = eventData.Connection?.DataSource;
            Database = eventData.Connection?.Database;
        }

        public ConnectionMetric(ConnectionEventData eventData, string instanceName = null)
        {
            InstanceName = instanceName; 

            ConnectionId = eventData.ConnectionId;
            Server = eventData.Connection?.DataSource;
            Database = eventData.Connection?.Database;
        }

        /// <summary>
        /// The action performed on the connection (open, closed)
        /// </summary>
        [EventMetricValue("action", SummaryFunction.Count, null, Caption = "Action",
            Description = "The action performed on the connection (open, closed)", IsDefaultValue = true)]
        public string Action { get; set; }

        /// <summary>
        /// The unique Id of the connection
        /// </summary>
        [EventMetricValue("connectionId", SummaryFunction.Count, null, Caption = "Id",
            Description = "The unique Id of the connection")]
        public Guid ConnectionId { get; set; }

        /// <summary>
        /// Duration of the query execution
        /// </summary>
        [EventMetricValue("duration", SummaryFunction.Average, "ms", Caption = "Duration",
            Description = "Duration of the connection action")]
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// The server the operation was run against
        /// </summary>
        [EventMetricValue("server", SummaryFunction.Count, null, Caption = "Server",
            Description = "The server the operation was run against")]
        public string Server { get; set; }

        /// <summary>
        /// The database the operation was run against
        /// </summary>
        [EventMetricValue("database", SummaryFunction.Count, null, Caption = "Database",
            Description = "The database the operation was run against")]
        public string Database { get; set; }

        [EventMetricInstanceName]
        [SampledMetricInstanceName]
        public string InstanceName { get; }

        [SampledMetricValue("Connections", SamplingType.IncrementalCount, "count", 
            Caption = "Connections", Description = "Number of open connections")]
        public int ConnectionDelta { get; set; }
    }
}