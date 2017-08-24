namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The requested interval between value samples.
    /// </summary>
    public enum MetricSampleInterval
    {
        /// <summary>
        /// Use the interval as the data was recorded.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Use the interval as the data was recorded.
        /// </summary>
        Shortest = 1,

        /// <summary>
        /// Use a sampling interval set in milliseconds
        /// </summary>
        Millisecond = 2,

        /// <summary>
        /// Use a sampling interval set in seconds.
        /// </summary>
        Second = 3,

        /// <summary>
        /// Use a sampling interval set in minutes.
        /// </summary>
        Minute = 4,

        /// <summary>
        /// Use a sampling interval set in hours.
        /// </summary>
        Hour = 5,

        /// <summary>
        /// Use a sampling interval set in days.
        /// </summary>
        Day = 6,

        /// <summary>
        /// Use a sampling interval set in weeks.
        /// </summary>
        Week = 7,

        /// <summary>
        /// Use a sampling interval set in months.
        /// </summary>
        Month = 8
    }
}
