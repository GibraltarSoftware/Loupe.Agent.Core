using System;
using System.Diagnostics;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;

namespace Loupe.Agent.EntityFramework.Internal
{
    /// <summary>
    /// The metric data object that implements our database metric.
    /// </summary>
    [EventMetric("Gibraltar", "Database", "Query", Caption = "Database Query Performance",
      Description = "Performance data for every database query")]
    internal class DatabaseMetric
    {
        private readonly Stopwatch _stopWatch;

        /// <summary>
        /// Create a new metric instance for the specified query
        /// </summary>
        public DatabaseMetric(string shortenedQuery, string query)
        {
            ShortenedQuery = shortenedQuery;
            Query = query;
            _stopWatch = Stopwatch.StartNew();
            //by default assume we're going to succeed - that way we don't have to explicitly add this
            //to every place we record a metric.
            Result = "Success";
        }

        /// <summary>
        /// The shortened down version we used for recording the caption on the starting side.
        /// </summary>
        public string? ShortenedQuery { get; private set; }

        /// <summary>
        /// The name of the stored procedure or query that was executed
        /// </summary>
        [EventMetricValue("queryName", SummaryFunction.Count, null, Caption = "Query Name",
            Description = "The name of the stored procedure or query that was executed")]
        public string? Query { get; private set; }


        /// <summary>
        /// The parameters that were provided as input to the query
        /// </summary>
        [EventMetricValue("parameters", SummaryFunction.Count, null, Caption = "Parameters",
            Description = "The parameters that were provided as input to the query")]
        public string? Parameters { get; set; }

        /// <summary>
        /// The server the operation was run against
        /// </summary>
        [EventMetricValue("server", SummaryFunction.Count, null, Caption = "Server",
            Description = "The server the operation was run against")]
        public string? Server { get; set; }

        /// <summary>
        /// The database the operation was run against
        /// </summary>
        [EventMetricValue("database", SummaryFunction.Count, null, Caption = "Database",
            Description = "The database the operation was run against")]
        public string? Database { get; set; }

        /// <summary>
        /// The number of rows returned by the query
        /// </summary>
        [EventMetricValue("rowCount", SummaryFunction.Average, null, Caption = "Rows",
            Description = "The number of rows returned by the query")]
        public int Rows { get; set; }

        /// <summary>
        /// Duration of the query execution
        /// </summary>
        [EventMetricValue("duration", SummaryFunction.Average, "ms", Caption = "Duration",
            Description = "Duration of the query execution", IsDefaultValue = true)]
        public TimeSpan Duration { get; private set; }

        /// <summary>
        /// The result of the query; Success or an error message.
        /// </summary>
        [EventMetricValue("result", SummaryFunction.Count, null, Caption = "Result",
            Description = "The result of the query; Success or an error message.")]
        public string? Result { get; set; }

        /// <summary>
        /// The message source provider for this call.
        /// </summary>
        internal IMessageSourceProvider? MessageSourceProvider { get; set; }

        /// <summary>
        /// Stops the timer but doesn't record the metric yet.
        /// </summary>
        public void Stop()
        {
            if (_stopWatch.IsRunning)
            _stopWatch.Stop();

            Duration = _stopWatch.Elapsed;

            //fix for issue with .Net < 4 where short durations can be negative on some computers.
            if (Duration.Ticks < 0)
                Duration = new TimeSpan(0);
        }

        /// <summary>
        /// Stops the timer and records the metric
        /// </summary>
        public void Record()
        {
            Stop();

            EventMetric.Write(this);
        }
    }
}
