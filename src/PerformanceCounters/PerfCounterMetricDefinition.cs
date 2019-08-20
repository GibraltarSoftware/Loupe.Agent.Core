using System;
using System.Diagnostics;
using Loupe.Core.Monitor;
using Loupe.Agent.PerformanceCounters.Serialization;
using Loupe.Core.Metrics;

namespace Loupe.Agent.PerformanceCounters
{
    /// <summary>
    /// A single tracked metric based on a Windows Performance Counter.
    /// </summary>
    /// <remarks>
    /// Each performance counter that is tracked for a session has its own performance counter metric associated with it.
    /// Use the Calculate method to generate display-ready value sets from the samples recorded for this metric.
    /// Metrics with the same category and counter can be directly compared.
    /// </remarks>
    public sealed class PerfCounterMetricDefinition : SampledMetricDefinition, IComparable<PerfCounterMetricDefinition>, IEquatable<PerfCounterMetricDefinition>
    {
        /// <summary>
        /// The metric type for all performance counters.
        /// </summary>
        public static string PerfCounterMetricType = "PerfCounter";

        //This value is calculated during object creation and is not persisted.
        private readonly bool m_RequiresMultipleSamples = false;
        private bool m_IsPercentage;

        /// <summary>
        /// Create a new performance counter metric definition from the provided performance counter.
        /// </summary>
        /// <remarks>The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="definitions">The definitions collection to add this definition to.</param>
        /// <param name="newPerformanceCounter">The windows performance counter to add a definition for</param>
        public PerfCounterMetricDefinition(MetricDefinitionCollection definitions, PerformanceCounter newPerformanceCounter)
            : base(definitions, new PerfCounterMetricDefinitionPacket(newPerformanceCounter))
        {
            m_RequiresMultipleSamples = CounterRequiresMultipleSamples(newPerformanceCounter.CounterType);

            CalculateIsPercentage();
        }

        /// <summary>
        /// Create a new performance counter metric object from the provided raw data packet
        /// </summary>
        /// <remarks>The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="packet">The raw data packet</param>
        internal PerfCounterMetricDefinition(MetricDefinitionCollection definitions, PerfCounterMetricDefinitionPacket packet)
            : base(definitions, packet)
        {
            m_RequiresMultipleSamples = CounterRequiresMultipleSamples(packet.CounterType);

            CalculateIsPercentage();
        }


        #region Public Properties and Methods

        /// <summary>
        /// Compare this object to another to determine sort order
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PerfCounterMetricDefinition other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.CompareTo(other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(PerfCounterMetricDefinition other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.Equals(other);
        }

        /// <summary>
        /// The set of metrics that use this definition.
        /// </summary>
        /// <remarks>All metrics with the same definition are of the same object type.</remarks>
        public new PerfCounterMetricCollection Metrics => (PerfCounterMetricCollection)base.Metrics;


        /// <summary>
        /// The intended method of interpreting the sampled counter value.
        /// </summary>
        /// <remarks>Uses the Windows Performance Counter type. The counter type determines what math needs to be run
        /// to determine the correct value when comparing two samples.</remarks>
        public PerformanceCounterType CounterType => ((PerfCounterMetricDefinitionPacket)base.Packet).CounterType;

        /// <summary>
        /// Indicates whether a final value can be determined from just one sample or if two comparable samples are required.
        /// </summary>
        public bool RequiresMultipleSamples => m_RequiresMultipleSamples;

        /// <summary>
        /// Indicates if the performance counter is a percentage and will require downscaling (division by zero) to align with other metrics.
        /// </summary>
        public bool IsPercentage => m_IsPercentage;

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Generates a definition key for a the provided performance counter.  
        /// This is not instance-specific and should not be used for a metric.
        /// </summary>
        /// <param name="newPerfCounter"></param>
        /// <returns></returns>
        internal static string GetKey(PerformanceCounter newPerfCounter)
        {
            //generate a key using the central method
            return MetricDefinition.GetKey(PerfCounterMetricType, newPerfCounter.CategoryName, newPerfCounter.CounterName);
        }

        /// <summary>
        /// Indicates whether two samples are required to calculate a metric value or not. 
        /// </summary>
        /// <remarks>
        /// Only a few counter types - notably the NumberOfItems types - are in their final form
        /// from a single sample.  All others require two samples to compare.
        /// </remarks>
        internal static bool CounterRequiresMultipleSamples(PerformanceCounterType counterType)
        {
            bool multipleRequired;

            //based purely on the counter type, according to Microsoft documentation
            switch (counterType)
            {
                case PerformanceCounterType.NumberOfItems32:
                case PerformanceCounterType.NumberOfItems64:
                case PerformanceCounterType.NumberOfItemsHEX32:
                case PerformanceCounterType.NumberOfItemsHEX64:
                case PerformanceCounterType.RawFraction:
                    //these just require one sample
                    multipleRequired = false;
                    break;
                default:
                    //everything else requires more than one sample
                    multipleRequired = true;
                    break;
            }

            return multipleRequired;
        }

        /// <summary>
        /// The underlying packet 
        /// </summary>
        internal new PerfCounterMetricDefinitionPacket Packet => (PerfCounterMetricDefinitionPacket)base.Packet;

        #endregion

        #region Base Object Overrides

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override MetricCollection OnMetricDictionaryCreate()
        {
            return new PerfCounterMetricCollection(this);
        }

        #endregion

        #region Private Properties and methods

        private void CalculateIsPercentage()
        {
            //is this a percentage value?  Perf counters "upscale" at the calculation stage which we want to undo.
            switch (CounterType)
            {
                case PerformanceCounterType.RawFraction:
                case PerformanceCounterType.SampleFraction:
                case PerformanceCounterType.CounterTimer:
                case PerformanceCounterType.CounterTimerInverse:
                case PerformanceCounterType.Timer100Ns:
                case PerformanceCounterType.Timer100NsInverse:
                case PerformanceCounterType.CounterMultiTimer:
                case PerformanceCounterType.CounterMultiTimerInverse:
                case PerformanceCounterType.CounterMultiTimer100Ns:
                case PerformanceCounterType.CounterMultiTimer100NsInverse:
                case (PerformanceCounterType)542573824: //I don't know if there is an enumeration for this value or not, but it's coming up in the real world.
                    m_IsPercentage = true;
                    break;
                default:
                    m_IsPercentage = false;
                    break;
            }
            
        }

        #endregion
    }
}
