using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single event metric instance object, representing one instance of an event metric definition.
    /// </summary>
    public interface IEventMetric : IMetric
    {
        /// <summary>
        /// Compute displayable values based on the full information captured for this metric
        /// </summary>
        /// <remarks>
        /// The raw values may not be suitable for display depending on the unit the values are captured in, and
        /// depending on how the data was sampled it may not display well because of uneven sampling if processed
        /// directly.
        /// </remarks>
        /// <param name="interval">The requested data sample size</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="startDateTime">The earliest date to retrieve data for</param>
        /// <param name="endDateTime">The last date to retrieve data for</param>
        /// <param name="trendValue">The specific event metric value to trend</param>
        /// <returns>A metric value set suitable for display</returns>
        IMetricValueCollection CalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset? startDateTime, DateTimeOffset? endDateTime, IEventMetricValueDefinition trendValue);
    }
}
