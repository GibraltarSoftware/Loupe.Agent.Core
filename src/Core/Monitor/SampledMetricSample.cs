using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility.Data;




namespace Gibraltar.Monitor
{
    /// <summary>
    /// One sample of a sampled metric
    /// </summary>
    /// <remarks>Specific sampled metrics will have a derived implementation of this class, however
    /// clients should work with this interface when feasible to ensure compatibility with any sampled
    /// metric implementation.</remarks>
    public abstract class SampledMetricSample : MetricSample, ISampledMetricSample
    {
        private readonly bool m_RequiresMultipleSamples = false;

        /// <summary>
        /// Create a new sampled metric sample object for the provided metric and raw sample packet.
        /// </summary>
        /// <remarks>The metric sample is automatically added to the samples collection of the provided metric object.</remarks>
        /// <param name="metric">The metric object this sample applies to.</param>
        /// <param name="metricSamplePacket">The raw sample data packet.</param>
        /// <param name="requiresMultipleSamples">Indicates whether more than one sample is required to calculate an effective metric value.</param>
        public SampledMetricSample(SampledMetric metric, SampledMetricSamplePacket metricSamplePacket, bool requiresMultipleSamples)
            : base(metric, metricSamplePacket)
        {
            m_RequiresMultipleSamples = requiresMultipleSamples;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Compute the resultant value for this sample.
        /// </summary>
        /// <remarks>
        /// This override is only useful if RequiresMultipleSamples is false.  In all other cases, you need to first
        /// identify a baseline sample to compare this sample with to determine the final value.
        /// </remarks>
        /// <returns>The calculated counter value</returns>
        public double ComputeValue()
        {
            //pass control to the real method
            return ComputeValue(null);
        }

        /// <summary>
        /// Compute the resultant value for this sample compared with the provided baseline sample
        /// </summary>
        /// <remarks>
        /// The baseline sample must be for a date and time prior to this sample for correct results.
        /// </remarks>
        /// <param name="baselineSample">The previous baseline sample to calculate a difference for</param>
        /// <returns>The calculated counter value</returns>
        public double ComputeValue(ISampledMetricSample baselineSample)
        {
            return ComputeValue((SampledMetricSample)baselineSample);
        }

        /// <summary>
        /// Compute the resultant value for this sample compared with the provided baseline sample
        /// </summary>
        /// <remarks>
        /// The baseline sample must be for a date and time prior to this sample for correct results.
        /// </remarks>
        /// <param name="baselineSample">The previous baseline sample to calculate a difference for</param>
        /// <returns>The calculated counter value</returns>
        public abstract double ComputeValue(SampledMetricSample baselineSample);

        /// <summary>
        /// Indicates whether two samples are required to calculate a metric value or not. 
        /// </summary>
        /// <remarks>
        /// Only a few counter types - notably the NumberOfItems types - are in their final form
        /// from a single sample.  All others require two samples to compare.
        /// </remarks>
        public bool RequiresMultipleSamples { get { return m_RequiresMultipleSamples; } }

        /// <summary>
        /// The sampled metric this sample is for.
        /// </summary>
        public new SampledMetric Metric { get { return (SampledMetric)base.Metric; } }

        /// <summary>
        /// The raw value of this metric.  Depending on the metric definition, this may be meaningless and instead a 
        /// calculation may need to be performed.
        /// </summary>
        public override double Value
        {
            get
            {
                //return our raw value.  
                return ((SampledMetricSamplePacket)Packet).RawValue;
            }
        }

        /// <summary>
        /// Compares this sampled metric object to another.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(SampledMetricSample other)
        {
            //gateway to our base object
            return base.CompareTo(other);
        }

        #endregion
    }
}
