using System;
using Gibraltar.Monitor.Internal;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// The collection of metric samples for a custom sampled metric.
    /// </summary>
    public sealed class CustomSampledMetricSampleCollection : MetricSampleCollection
    {
        //we are caching our type-safe handle for convenience
        private readonly CustomSampledMetric m_CustomSampledMetric;

        /// <summary>
        /// Create a new sample collection for the specified metric object
        /// </summary>
        /// <param name="metric"></param>
        internal CustomSampledMetricSampleCollection(CustomSampledMetric metric)
            : base(metric)
        {
            m_CustomSampledMetric = metric;
        }

        /// <summary>
        /// Add the specified custom sampled metric sample object to the collection
        /// </summary>
        /// <param name="newMetricSample">The new custom sampled metric object to add</param>
        public void Add(CustomSampledMetricSample newMetricSample)
        {
            if (newMetricSample == null)
            {
                throw new ArgumentNullException(nameof(newMetricSample), "A new custom sampled metric sample object must be provided to add it to the collection.");
            }
            
            //our base object does all the work
            base.Add(newMetricSample);
        }

        /// <summary>
        /// Add a new custom sampled metric sample from the specified sample packet
        /// </summary>
        /// <param name="newMetricSamplePacket">The sample packet to create a new metric sample object from</param>
        internal CustomSampledMetricSample Add(CustomSampledMetricSamplePacket newMetricSamplePacket)
        {
            if (newMetricSamplePacket == null)
            {
                throw new ArgumentNullException(nameof(newMetricSamplePacket), "A new custom sampled metric sample packet object must be provided to add it to the collection.");
            }

            //now we have to create a new sample object to wrap the provided packet
            CustomSampledMetricSample newMetricSample = new CustomSampledMetricSample(m_CustomSampledMetric, newMetricSamplePacket);

            //and forward to our normal add routine
            Add(newMetricSample);

            //returning our new wrapped object
            return newMetricSample;
        }

        /// <summary>
        /// Select a metric sample by its numerical index
        /// </summary>
        /// <remarks>Setting a metric sample to a particular index is not supported and will result in an exception being thrown.</remarks>
        /// <param name="index"></param>
        /// <returns></returns>
        public new CustomSampledMetricSample this[int index]
        {
            get
            {
                return (CustomSampledMetricSample)base[index];
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// The metric this sample is for.
        /// </summary>
        public new CustomSampledMetric Metric { get { return m_CustomSampledMetric; } }

        /// <summary>
        /// The first object in the collection
        /// </summary>
        public new CustomSampledMetricSample First { get { return (CustomSampledMetricSample)base.First; } }

        /// <summary>
        /// The last object in the collection
        /// </summary>
        public new CustomSampledMetricSample Last { get { return (CustomSampledMetricSample)base.Last; } }

    }
}
