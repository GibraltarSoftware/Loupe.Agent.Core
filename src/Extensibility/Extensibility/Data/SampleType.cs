namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The method of data collection done by the metric
    /// </summary>
    /// <remarks>
    /// Metrics are either sampled or event oriented.  Sampled metrics are well defined for all
    /// time between the start and end of the metric, meaning they have a value.  This value's accuracy compared to
    /// reality is dependent on the frequency of sampling (the Sample Interval).  For example, all Windows performance
    /// counter metrics are sampled metrics.
    /// Event metrics are undefined between occurrences.  For example, an IIS server log represents a set of event
    /// metric values - each one is an event that has additional information worth tracking, but the value is undefined
    /// between events.
    /// </remarks>
    public enum SampleType
    {
        /// <summary>
        /// Metric values are contiguous samples of the measured value
        /// </summary>
        Sampled = 0,

        /// <summary>
        /// Metric values are isolated events with additional information.
        /// </summary>
        Event = 1
    }
}
