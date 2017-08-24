using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A collection of metric samples for a metric.
    /// </summary>
    public interface IMetricSampleCollection : IList<IMetricSample>
    {
        /// <summary>
        /// The metric this collection of samples is related to
        /// </summary>
        IMetric Metric { get; }

        /// <summary>
        /// The first object in the collection
        /// </summary>
        IMetricSample First { get; }

        /// <summary>
        /// The last object in the collection
        /// </summary>
        IMetricSample Last { get; }
    }
}
