namespace Gibraltar.Agent.Metrics
{
    /// <summary>
    /// Indicates the default way to interpret multiple values for display purposes
    /// </summary>
    public enum SummaryFunction
    {
        /// <summary>
        /// Average all of the values within each sample range to determine the displayed value.
        /// </summary>
        Average = Loupe.Extensibility.Data.EventMetricValueTrend.Average,

        /// <summary>
        /// Add all of the values within each sample range to determine the displayed value.
        /// </summary>
        Sum = Loupe.Extensibility.Data.EventMetricValueTrend.Sum,

        /// <summary>
        /// An average of all values up through the end of the sample range.
        /// </summary>
        RunningAverage = Loupe.Extensibility.Data.EventMetricValueTrend.RunningAverage,

        /// <summary>
        /// The sum of all values up through the end of the sample range.
        /// </summary>
        RunningSum = Loupe.Extensibility.Data.EventMetricValueTrend.RunningSum,

        /// <summary>
        /// The number of values within each sample range.
        /// </summary>
        Count = Loupe.Extensibility.Data.EventMetricValueTrend.Count
    }
}
