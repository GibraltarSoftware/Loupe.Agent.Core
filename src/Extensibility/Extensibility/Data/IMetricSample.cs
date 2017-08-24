using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single raw sample of a metric.  
    /// </summary>
    /// <remarks>
    /// Individual samples represent a single data point and may not be directly useful without
    /// manipulation.  For example, if the underlying metric is intended to be the rate of an event, the individual raw samples 
    /// will need to be used to calculate the rate instead of being used directly.
    /// </remarks>
    public interface IMetricSample : IComparable<IMetricSample>, IEquatable<IMetricSample>
    {
        /// <summary>
        /// The unique id of this sample
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The metric this sample relates to.
        /// </summary>
        IMetric Metric { get; }

        /// <summary>
        /// The increasing sequence number of all sample packets for this metric to be used as an absolute order sort.
        /// </summary>
        long Sequence { get; }

        /// <summary>
        /// The exact date and time the metric was captured.
        /// </summary>
        DateTimeOffset Timestamp { get; }

        /// <summary>
        /// The raw value of the metric.
        /// </summary>
        double Value { get; }
    }
}
