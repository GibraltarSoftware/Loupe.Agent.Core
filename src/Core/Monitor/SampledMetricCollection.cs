namespace Loupe.Core.Monitor
{
    /// <summary>
    /// A collection of sampled metrics, keyed by their unique ID and name
    /// </summary>
    /// <remarks>A metric has a unique ID to identify a particular instance of the metric (associated with one session) 
    /// and a name that is unique within a session but is designed for comparison of the same metric between sessions.</remarks>
    public class SampledMetricCollection : MetricCollection
    {
        /// <summary>
        /// Create a new sampled metric dictionary for the provided definition.
        /// </summary>
        /// <remarks>This dictionary is created automatically by the Metric Definition during its initialization.</remarks>
        /// <param name="metricDefinition">The definition of the sampled metric to create a metric dictionary for</param>
        internal SampledMetricCollection(SampledMetricDefinition metricDefinition)
            : base(metricDefinition)
        {

        }
    }
}
