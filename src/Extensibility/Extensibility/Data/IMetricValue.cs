using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single display-ready metric value.  
    /// </summary>
    /// <remarks>
    /// This is the complementary object to a Metric Sample.  A Sample is a raw value that may require multiple
    /// samples to determine a display ready value.
    /// </remarks>
    public interface IMetricValue : IComparable<IMetricValue>, IEquatable<IMetricValue>
    {
        /// <summary>
        /// The metric value set this value is part of.
        /// </summary>
        IMetricValueCollection ValueCollection { get; }

        /// <summary>
        /// The exact date and time the metric was captured.
        /// </summary>
        DateTimeOffset Timestamp { get; }

        /// <summary>
        /// The date and time the metric was captured in the effective time zone.
        /// </summary>
        DateTime LocalTimestamp { get; }

        /// <summary>
        /// The value of the metric.
        /// </summary>
        double Value { get; }

        /// <summary>
        /// The value of the metric, multiplied by 100.
        /// </summary>
        /// <remarks>Since percentage values span from 0 to 1 it may be more convenient to retrieve them scaled from 0 to 100.</remarks>
        double PercentageValue { get; }

        /// <summary>
        /// The increasing sequence number of all sample packets for this metric to be used as an absolute order sort.
        /// </summary>
        long Sequence { get; }
    }
}
