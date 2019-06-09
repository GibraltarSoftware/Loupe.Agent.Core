
using System;
using System.Diagnostics;
using Gibraltar.Monitor;
using Loupe.Agent.PerformanceCounters.Serialization;

namespace Loupe.Agent.PerformanceCounters
{
    /// <summary>
    /// A single performance counter raw sample.
    /// </summary>
    /// <remarks>
    /// Depending on the sample, the raw value may not be directly usable.  If the sample is a rate over time, 
    /// you will need to compare two samples to get a true value.  Additionally, depending on the resolution of
    /// the data you need, you can pick a smaller set of samples to compare.  For example, if you only want one hour
    /// resolution for a graph, pick just the first sample for each hour and calculate between them.  
    /// To calculate a usable value, either directly use the CounterSampleCalculator class or use the Calculate method
    /// on the CounterSample property 
    /// </remarks>
    public sealed class PerfCounterMetricSample : SampledMetricSample, IComparable<PerfCounterMetricSample>, IEquatable<PerfCounterMetricSample>
    {
        /// <summary>
        /// Create a new performance counter metric sample object for the provided metric and raw sample packet.
        /// </summary>
        /// <remarks>The metric sample is automatically added to the samples collection of the provided metric object.</remarks>
        /// <param name="metric">The metric object this sample applies to.</param>
        /// <param name="metricSamplePacket">The raw sample data packet.</param>
        internal PerfCounterMetricSample(PerfCounterMetric metric, PerfCounterMetricSamplePacket metricSamplePacket)
            : base(metric, metricSamplePacket, ((PerfCounterMetricDefinition) metric.Definition).RequiresMultipleSamples)
        {
        }

        #region Public Properties and Methods

        /// <summary>
        /// Compute the counter value for this sample compared with the provided baseline sample
        /// </summary>
        /// <remarks>
        /// The baseline sample must be for a date and time prior to this sample for correct results.
        /// </remarks>
        /// <param name="baselineSample">The previous baseline sample to calculate a difference for</param>
        /// <returns>The calculated counter value</returns>
        public override double ComputeValue(SampledMetricSample baselineSample)
        {
            double computedValue;

            //we have to verify a few things depending on whether we use a baseline
            if (RequiresMultipleSamples)
            {
                if (baselineSample == null)
                {
                    throw new ArgumentNullException(nameof(baselineSample),
                                                    "A baseline metric sample is required and none was provided.");
                }

                if (baselineSample.Timestamp > Timestamp)
                {
                    throw new ArgumentOutOfRangeException(nameof(baselineSample), baselineSample.Timestamp,
                                                          "The baseline sample must be for a date & time before this sample to be valid for comparison.");
                }

                //gateway to the counter sample calculator
                PerfCounterMetricSample baselinePerfCounterSample = (PerfCounterMetricSample) baselineSample;
                computedValue = CounterSampleCalculator.ComputeCounterValue(baselinePerfCounterSample,
                                                                   Packet.CounterSample);
            }
            else
            {
                computedValue = CounterSampleCalculator.ComputeCounterValue(Packet.CounterSample);
            }

            //is this a percentage value?  Perf counters "upscale" at the calculation stage which we want to undo.
            if (Metric.Definition.IsPercentage)
            {
                computedValue = computedValue / 100;
            }

            return computedValue;
        }

        /// <summary>
        /// Implicitly convert our metric sample to a windows counter sample.
        /// </summary>
        /// <param name="sample"></param>
        /// <returns></returns>
        public static implicit operator CounterSample(PerfCounterMetricSample sample)
        {
            return sample.CounterSample;
        }

        /// <summary>
        /// Provide users a handy default conversion since we ultimately just wrapper a counter sample object anyway.
        /// </summary>
        /// <returns>The counter sample object wrappered by this object</returns>
        public CounterSample ToCounterSample() { return CounterSample; }
        
        /// <summary>
        /// The underlying counter sample for this object
        /// </summary>
        public CounterSample CounterSample => Packet;

        /// <summary>
        /// The performance counter metric this sample is for.
        /// </summary>
        public new PerfCounterMetric Metric => (PerfCounterMetric)base.Metric;

        /// <summary>
        /// The raw value of this metric.  Depending on the metric definition, this may be meaningless and instead a 
        /// calculation may need to be performed.
        /// </summary>
        public override double Value => Packet.CounterSample.RawValue;

        /// <summary>
        /// Compare this object to another.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PerfCounterMetricSample other)
        {
            //we just gateway to our base object.
            return base.CompareTo(other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(PerfCounterMetricSample other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.Equals(other);
        }

        #endregion

        #region Internal Properties and Methods

        internal new PerfCounterMetricSamplePacket Packet => (PerfCounterMetricSamplePacket)base.Packet;

        #endregion
    }
}
