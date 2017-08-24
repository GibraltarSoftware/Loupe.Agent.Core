using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A set of display-ready values for a metric. 
    /// </summary>
    /// <remarks>
    /// These are after any necessary calculation or interpolation.
    /// To get a value set, use the Calculate method on a metric.  For best performance, specify the least accurate
    /// interval you need to graph.
    /// </remarks>
    public interface IMetricValueCollection : IList<IMetricValue>
    {
        /// <summary>
        /// A display caption for this metric set.
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// A description of this metric set.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The unit of intervals between samples.  If not set to default or shortest, see the Intervals property for how many intervals between samples.
        /// </summary>
        /// <remarks>To get a higher resolution data set in the case when the sample interval is not set to shortest, use the CalculateValues method on the metric.</remarks>
        MetricSampleInterval Interval { get; }

        /// <summary>
        /// The number of intervals between samples in the interval set.
        /// </summary>
        /// <remarks>This property is only meaningful if the sample interval is set to default or shortest.</remarks>
        int Intervals { get; }

        /// <summary>
        /// The start date and time of this value set interval.  This may not represent all of the data available in the metric.
        /// </summary>
        DateTimeOffset StartDateTime { get; }

        /// <summary>
        /// The end date and time of this value set interval.  This may not represent all of the data available in the metric.
        /// </summary>
        DateTimeOffset EndDateTime { get; }

        /// <summary>
        /// The smallest value in the value set, useful for setting ranges for display.  The minimum value may be negative.
        /// </summary>
        double MinValue { get; }

        /// <summary>
        /// The metric object with the smallest value in the value set, useful for setting ranges for display.  The minimum value may be negative.
        /// </summary>
        IMetricValue MinValueMetricValue { get; }

        /// <summary>
        /// The largest value in the value set, useful for setting ranges for display.  The maximum value may be negative.
        /// </summary>
        double MaxValue { get; }

        /// <summary>
        /// The metric object with the largest value in the value set, useful for setting ranges for display.  The maximum value may be negative.
        /// </summary>
        IMetricValue MaxValueMetricValue { get; }

        /// <summary>
        /// The average value in the value set, useful for setting ranges for display.  The average value may be negative.
        /// </summary>
        double AverageValue { get; }

        /// <summary>
        /// The 95th percentile value in the value set.  The percentile value may be negative.
        /// </summary>
        double PercentileValue { get; }

        /// <summary>
        /// The display caption for the values in this set
        /// </summary>
        string UnitCaption { get; }
    }
}
