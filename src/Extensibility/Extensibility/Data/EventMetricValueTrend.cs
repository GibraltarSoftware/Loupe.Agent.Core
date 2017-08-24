namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// Indicates the default way to interpret multiple values for display purposes
    /// </summary>
    public enum EventMetricValueTrend
    {
        /// <summary>
        /// Average all of the values within each sample range to determine the displayed value.
        /// </summary>
        Average = 0,

        /// <summary>
        /// Add all of the values within each sample range to determine the displayed value.
        /// </summary>
        Sum = 1,

        /// <summary>
        /// An average of all values up through the end of the sample range.
        /// </summary>
        RunningAverage = 2,

        /// <summary>
        /// The sum of all values up through the end of the sample range.
        /// </summary>
        RunningSum = 3,

        /// <summary>
        /// The number of values within each sample range.
        /// </summary>
        Count = 4
    }
}
