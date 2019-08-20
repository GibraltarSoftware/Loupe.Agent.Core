using System;
using Loupe.Core.Monitor.Serialization;
using Loupe.Extensibility.Data;
using Loupe.Metrics;

namespace Loupe.Core.Monitor
{
    /// <summary>
    /// A user defined sampled metric.
    /// </summary>
    /// <remarks>Custom Sampled Metrics are the simplest form of Sampled Metrics, not requiring the developer
    /// to derive their own classes to encapsulate a sampled metric.  Review if this class can serve your needs before
    /// you create your own custom set of classes derived from SampledMetric (or derive from this class)</remarks>
    public sealed class CustomSampledMetric : SampledMetric, IComparable<CustomSampledMetric>, IEquatable<CustomSampledMetric>
    {
        private readonly CustomSampledMetricSampleCollection m_Samples;
        private readonly CustomSampledMetricDefinition m_MetricDefinition;
        private readonly CustomSampledMetricPacket m_Packet;

        /// <summary>Creates a new custom sampled metric object from the metric definition looked up with the provided key information.</summary>
        /// <remarks>The metric definition must already exist or an exception will be raised.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        public CustomSampledMetric(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName, string instanceName)
            : this((CustomSampledMetricDefinition)definitions[MetricDefinition.GetKey(metricTypeName, categoryName, counterName)], instanceName)
        {
        }

        /// <summary>
        /// Create a new custom sampled metric object from the provided metric definition
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The metric definition for the metric instance</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        public CustomSampledMetric(CustomSampledMetricDefinition definition, string instanceName)
            : this(definition, new CustomSampledMetricPacket(definition.Packet, instanceName))
        {
        }

        /// <summary>
        /// Create a new custom sampled metric object from the provided raw data packet
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The metric definition for the metric instance</param>
        /// <param name="packet">The raw data packet</param>
        internal CustomSampledMetric(CustomSampledMetricDefinition definition, CustomSampledMetricPacket packet)
            : base(definition, packet)
        {
            // We created a CustomSampledMetricSampleCollection when base constructor called our OnSampleCollectionCreate().
            m_Samples = (CustomSampledMetricSampleCollection)base.Samples;
            m_MetricDefinition = definition;
            m_Packet = packet;
        }

        #region Public Properties and Methods

        /// <summary>Creates a new metric instance from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Custom Sampled Metric (or a derived class) an exception will be thrown.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The type of data captured for each metric under this definition.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        public static CustomSampledMetric AddOrGet(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType, string instanceName)
        {
            //we must have a definitions collection, or we have a problem
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            //we need to find the definition, adding it if necessary
            string definitionKey = MetricDefinition.GetKey(metricTypeName, categoryName, counterName);
            IMetricDefinition definition;

            //Establish a lock on the definitions collection so our lookup & create are atomic.
            lock (definitions.Lock)
            {
                if (definitions.TryGetValue(definitionKey, out definition))
                {
                    //if the metric definition exists, but is of the wrong type we have a problem.
                    if ((definition is CustomSampledMetricDefinition) == false)
                    {
                        throw new ArgumentException("A metric already exists with the provided type, category, and counter name but it is not compatible with being a custom sampled metric.  Please use a different counter name.", nameof(counterName));
                    }
                }
                else
                {
                    //we didn't find one, make a new one
                    definition =
                        new CustomSampledMetricDefinition(definitions, metricTypeName, categoryName, counterName,
                                                          metricSampleType);
                }
            }

            //now we have our definition, proceed to create a new metric if it doesn't exist
            string metricKey = MetricDefinition.GetKey(metricTypeName, categoryName, counterName, instanceName);
            IMetric metric;

            //see if we can get the metric already.  If not, we'll create it
            lock (((MetricCollection)definition.Metrics).Lock)
            {
                if (definition.Metrics.TryGetValue(metricKey, out metric) == false)
                {
                    metric = new CustomSampledMetric((CustomSampledMetricDefinition)definition, instanceName);
                }
            }

            return (CustomSampledMetric)metric;
        }

        /// <summary>Creates a new metric instance from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Custom Sampled Metric (or a derived class) an exception will be thrown.
        /// Definitions are looked up and added to the active logging metrics collection (Log.Metrics)</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The type of data captured for each metric under this definition.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        public static CustomSampledMetric AddOrGet(string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType, string instanceName)
        {
            //just forward into our call that requires the definition to be specified
            return AddOrGet(Log.Metrics, metricTypeName, categoryName, counterName, metricSampleType, instanceName);
        }

        /// <summary>
        /// Create a complete metric sample from the provided data.  The caller must write this sample for it to be recorded.
        /// </summary>
        /// <remarks>To write this sample out to the log, use Log.Write.  If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.</remarks>
        /// <para>Custom metrics using a sample type of AverageFraction and DeltaFraction should not use this method because
        /// they require a base value as well as a raw value.</para>
        /// <param name="rawValue">The raw data value</param>
        public CustomSampledMetricSample CreateSample(double rawValue)
        {
            return new CustomSampledMetricSample(this, new CustomSampledMetricSamplePacket(this, rawValue));
        }

        /// <summary>
        /// Create a complete metric sample from the provided data.  The caller must write this sample for it to be recorded.
        /// </summary>
        /// <remarks>To write this sample out to the log, use Log.Write.  If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.</remarks>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        public CustomSampledMetricSample CreateSample(double rawValue, DateTimeOffset rawTimeStamp)
        {
            return new CustomSampledMetricSample(this, new CustomSampledMetricSamplePacket(this, rawValue, rawTimeStamp));
        }

        /// <summary>
        /// Create a complete metric sample from the provided data.  The caller must write this sample for it to be recorded.
        /// </summary>
        /// <remarks>To write this sample out to the log, use Log.Write.  If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.</remarks>
        /// <para>Custom metrics using a sample type of AverageFraction and DeltaFraction should not use this method because
        /// they require a base value as well as a raw value.</para>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="baseValue">The reference value to compare against for come counter types</param>
        public CustomSampledMetricSample CreateSample(double rawValue, double baseValue)
        {
            return new CustomSampledMetricSample(this, new CustomSampledMetricSamplePacket(this, rawValue, baseValue));
        }


        /// <summary>
        /// Create a complete metric sample from the provided data.  The caller must write this sample for it to be recorded.
        /// </summary>
        /// <remarks>To write this sample out to the log, use Log.Write.  If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.</remarks>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="baseValue">The reference value to compare against for come counter types</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        public CustomSampledMetricSample CreateSample(double rawValue, double baseValue, DateTimeOffset rawTimeStamp)
        {
            return new CustomSampledMetricSample(this, new CustomSampledMetricSamplePacket(this, rawValue, baseValue, rawTimeStamp));
        }

        /// <summary>
        /// Write a metric sample with the provided data immediately.
        /// </summary>
        /// <remarks>
        /// <para>The sample is immediately written to the log. If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.  To do this, you can use CreateMetricSample.</para>
        /// <para>Custom metrics using a sample type of AverageFraction and DeltaFraction should not use this method because
        /// they require a base value as well as a raw value.</para>
        /// </remarks>
        /// <param name="rawValue">The raw data value</param>
        public void WriteSample(double rawValue)
        {
            //Create a new custom sampled metric and write it out to the log
            CreateSample(rawValue).Write();
        }

        /// <summary>
        /// Write a metric sample with the provided data immediately.
        /// </summary>
        /// <remarks>
        /// <para>The sample is immediately written to the log. If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.  To do this, you can use CreateMetricSample.</para>
        /// <para>Custom metrics using a sample type of AverageFraction and DeltaFraction should not use this method because
        /// they require a base value as well as a raw value.</para>
        /// </remarks>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        public void WriteSample(double rawValue, DateTimeOffset rawTimeStamp)
        {
            //Create a new custom sampled metric and write it out to the log
            CreateSample(rawValue, rawTimeStamp).Write();
        }

        /// <summary>
        /// Write a metric sample with the provided data immediately.
        /// </summary>
        /// <remarks>The sample is immediately written to the log. If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.  To do this, you can use CreateMetricSample</remarks>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="baseValue">The reference value to compare against for come counter types</param>
        public void WriteSample(double rawValue, double baseValue)
        {
            //Create a new custom sampled metric and write it out to the log
            CreateSample(rawValue, baseValue).Write();
        }

        /// <summary>
        /// Write a metric sample with the provided data immediately.
        /// </summary>
        /// <remarks>The sample is immediately written to the log. If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.  To do this, you can use CreateMetricSample</remarks>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        /// <param name="baseValue">The reference value to compare against for come counter types</param>
        public void WriteSample(double rawValue, double baseValue, DateTimeOffset rawTimeStamp)
        {
            //Create a new custom sampled metric and write it out to the log
            CreateSample(rawValue, baseValue, rawTimeStamp).Write();
        }

        /// <summary>
        /// Compare this custom sampled metric to another custom sampled metric.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(CustomSampledMetric other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.CompareTo(other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(CustomSampledMetric other)
        {
            //We're really just a type cast, refer to our base object
            return base.Equals(other);
        }

        /// <summary>
        /// The definition of this metric object.
        /// </summary>
        public new CustomSampledMetricDefinition Definition { get { return m_MetricDefinition; } }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The underlying packet 
        /// </summary>
        internal new CustomSampledMetricPacket Packet { get { return m_Packet; } }

        /// <summary>
        /// The set of raw samples for this metric
        /// </summary>
        public new CustomSampledMetricSampleCollection Samples { get { return m_Samples; } } 

        #endregion

        #region Base Object Overrides

        /// <summary>
        /// Invoked when deserializing a metric sample to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for metric samples in your derived metric, use this
        /// method to create and return your derived object to support the deserialization process.
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <param name="packet">The metric sample packet being deserialized</param>
        /// <returns>The metric sample-compatible object.</returns>
        protected override MetricSample OnMetricSampleRead(MetricSamplePacket packet)
        {
            //create a custom sampled metric sample object
            return new CustomSampledMetricSample(this, (CustomSampledMetricSamplePacket)packet);
        }

        /// <summary>
        /// Invoked by the base class to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for metric sample collection in your derived metric, use this
        /// method to create and return your derived object. 
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <returns>The MetricSampleCollection-compatible object.</returns>
        protected override MetricSampleCollection OnSampleCollectionCreate()
        {
            return new CustomSampledMetricSampleCollection(this);

        }

        #endregion    
    }
}
