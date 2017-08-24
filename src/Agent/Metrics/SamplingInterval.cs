namespace Gibraltar.Agent.Metrics
{
    /// <summary>
    /// A suggested interval between value samples.
    /// </summary>
    public enum SamplingInterval
    {
        /// <summary>
        /// Use the interval as the data was recorded.
        /// </summary>
        Default = Loupe.Extensibility.Data.MetricSampleInterval.Default,

        /// <summary>
        /// Use the interval as the data was recorded.
        /// </summary>
        Shortest = Loupe.Extensibility.Data.MetricSampleInterval.Shortest,

        /// <summary>
        /// Use a sampling interval set in milliseconds
        /// </summary>
        Millisecond = Loupe.Extensibility.Data.MetricSampleInterval.Millisecond,

        /// <summary>
        /// Use a sampling interval set in seconds.
        /// </summary>
        Second = Loupe.Extensibility.Data.MetricSampleInterval.Second,

        /// <summary>
        /// Use a sampling interval set in minutes.
        /// </summary>
        Minute = Loupe.Extensibility.Data.MetricSampleInterval.Minute,

        /// <summary>
        /// Use a sampling interval set in hours.
        /// </summary>
        Hour = Loupe.Extensibility.Data.MetricSampleInterval.Hour,

        /// <summary>
        /// Use a sampling interval set in days.
        /// </summary>
        Day = Loupe.Extensibility.Data.MetricSampleInterval.Day,

        /// <summary>
        /// Use a sampling interval set in weeks.
        /// </summary>
        Week = Loupe.Extensibility.Data.MetricSampleInterval.Week,

        /// <summary>
        /// Use a sampling interval set in months.
        /// </summary>
        Month = Loupe.Extensibility.Data.MetricSampleInterval.Month,
    }
}
