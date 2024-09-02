
using System;
using System.Diagnostics;
#if !NETSTANDARD2_0
using System.Diagnostics.PerformanceData;
#endif
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
                computedValue = ComputeCounterValue(baselinePerfCounterSample, Packet.CounterSample);
            }
            else
            {
                computedValue = ComputeCounterValue(Packet.CounterSample);
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
        public static implicit operator SerializedCounterSample(PerfCounterMetricSample sample)
        {
            return sample.CounterSample;
        }

        /// <summary>
        /// Provide users a handy default conversion since we ultimately just wrapper a counter sample object anyway.
        /// </summary>
        /// <returns>The counter sample object wrappered by this object</returns>
        public SerializedCounterSample ToCounterSample() { return CounterSample; }

        /// <summary>
        /// The underlying counter sample for this object
        /// </summary>
        public SerializedCounterSample CounterSample => Packet;

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

        internal new PerfCounterMetricSamplePacket Packet => (PerfCounterMetricSamplePacket)base.Packet;

#if NETSTANDARD2_0
        private double ComputeCounterValue(SerializedCounterSample sample)
        {
            throw new PlatformNotSupportedException("Calculating performance counter values is not supported in .NET Standard 2.0");
        }

        private double ComputeCounterValue(SerializedCounterSample baselineSample, SerializedCounterSample currentSample)
        {
            throw new PlatformNotSupportedException("Calculating performance counter values is not supported in .NET Standard 2.0");
        }
#else
        private double ComputeCounterValue(SerializedCounterSample sample)
        {
            double computedValue = 0;
            switch (sample.CounterType)
            {
                case PerformanceCounterType.ElapsedTime:
                    computedValue = (double)(sample.CounterTimeStamp - sample.RawValue) / sample.SystemFrequency;
                    break;
                case PerformanceCounterType.NumberOfItemsHEX32:
                case PerformanceCounterType.NumberOfItemsHEX64:
                case PerformanceCounterType.NumberOfItems32:
                case PerformanceCounterType.NumberOfItems64:
                    computedValue = sample.RawValue;
                    break;
                case PerformanceCounterType.RawFraction:
                    computedValue = sample.BaseValue == 0 ? 0 : (double)sample.RawValue / sample.BaseValue * 100;
                    break;
                default:
                    switch ((CounterType)sample.CounterType) //upgrade it to the newer type which seems to match the values we get
                    {
                        case CounterType.RawFraction32:
                        case CounterType.RawFraction64:
                            computedValue = sample.BaseValue == 0 ? 0 : (double)sample.RawValue / sample.BaseValue * 100;
                            break;

                        default:
                            throw new NotImplementedException($"Calculations for the performance counter type {sample.CounterType} aren't implemented");
                    }
                    break;
            }

            return computedValue;
        }

        private double ComputeCounterValue(SerializedCounterSample baselineSample, SerializedCounterSample currentSample)
        {
            double rawDelta = currentSample.RawValue - baselineSample.RawValue;
            double baseDelta = currentSample.BaseValue - baselineSample.BaseValue;
            double ticksDelta = currentSample.TimeStamp - baselineSample.TimeStamp;
            double ticks100NsDelta = currentSample.TimeStamp100nSec - baselineSample.TimeStamp100nSec;
            double computedValue = 0;

            switch (currentSample.CounterType)
            {
                case PerformanceCounterType.RateOfCountsPerSecond32:
                case PerformanceCounterType.RateOfCountsPerSecond64:
                    computedValue = ticksDelta == 0 ? 0 : rawDelta / (ticksDelta / currentSample.SystemFrequency);
                    break;
                case PerformanceCounterType.SampleCounter:
                    computedValue = ticksDelta == 0 ? 0 : rawDelta / (ticksDelta / currentSample.SystemFrequency);
                    break;
                case PerformanceCounterType.SampleFraction:
                    computedValue = ticksDelta == 0 ? 0 : (rawDelta / ticksDelta) * 100;
                    break;
                case PerformanceCounterType.Timer100Ns:
                    computedValue = ticks100NsDelta == 0 ? 0 : (rawDelta / ticks100NsDelta) * 100;
                    break;
                case PerformanceCounterType.Timer100NsInverse:
                    computedValue = ticks100NsDelta == 0 ? 0 : (1 - (rawDelta / ticks100NsDelta)) * 100;
                    break;
                default:
                    //Second pass: switch to CounterType enum since we get some of them too.  I don't know why.
                    switch ((CounterType)currentSample.CounterType)
                    {
                        case CounterType.AverageCount64:
                            computedValue = baseDelta == 0 ? 0 : rawDelta / baseDelta;
                            break;
                        case CounterType.AverageTimer32:
                            computedValue = baseDelta == 0 ? 0 : (rawDelta / currentSample.SystemFrequency) / baseDelta;
                            break;
                        case CounterType.Delta32:
                        case CounterType.Delta64:
                            computedValue = rawDelta;
                            break;
                        case CounterType.PrecisionTimer100Ns:
                            computedValue = ticks100NsDelta == 0 ? 0 : (rawDelta / ticks100NsDelta) * 100;
                            break;
                        case CounterType.QueueLength:
                            computedValue = ticksDelta == 0 ? 0 : rawDelta / ticksDelta;
                            break;
                        case CounterType.QueueLength100Ns:
                            computedValue = ticks100NsDelta == 0 ? 0 : rawDelta / ticks100NsDelta;
                            break;
                        default:
                            throw new NotImplementedException($"Calculations for the performance counter type {currentSample.CounterType} aren't implemented");
                    }
                    break;
            }

            return computedValue;
        }
#endif

    }
}
