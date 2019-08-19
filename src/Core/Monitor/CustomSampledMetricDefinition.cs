using System;
using System.Reflection;
using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility.Data;
using Loupe.Metrics;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// The definition of a user-defined sampled metric
    /// </summary>
    /// <remarks>Custom Sampled Metrics are the simplest form of Sampled Metrics, not requiring the developer
    /// to derive their own classes to encapsulate a sampled metric.  Review if this class can serve your needs before
    /// you create your own custom set of classes derived from SampledMetric (or derive from this class)</remarks>
    public sealed class CustomSampledMetricDefinition : SampledMetricDefinition, IComparable<CustomSampledMetricDefinition>, IEquatable<CustomSampledMetricDefinition>
    {
        //This value is calculated during object creation and is not persisted.
        private readonly bool m_RequiresMultipleSamples;
        private bool m_Bound;
        private Type m_BoundType;
        private bool m_NameBound;
        private string m_NameMemberName;
        private MemberTypes m_NameMemberType;

        /// <summary>
        /// Create a new metric definition for the active log.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The type of data captured for each metric under this definition.</param>
        public CustomSampledMetricDefinition(string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType)
            : base(Log.Metrics, new CustomSampledMetricDefinitionPacket(metricTypeName, categoryName, counterName, metricSampleType))
        {
            m_RequiresMultipleSamples = SampledMetricTypeRequiresMultipleSamples(metricSampleType);
        }

        /// <summary>
        /// Create a new metric definition for the active log.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The type of data captured for each metric under this definition.</param>
        /// <param name="unitCaption">The display caption for the calculated values captured under this metric.</param>
        /// <param name="description">A description of what is tracked by this metric, suitable for end-user display.</param>
        public CustomSampledMetricDefinition(string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType, string unitCaption, string description)
            : base(Log.Metrics, new CustomSampledMetricDefinitionPacket(metricTypeName, categoryName, counterName, metricSampleType, unitCaption, description))
        {
            m_RequiresMultipleSamples = SampledMetricTypeRequiresMultipleSamples(metricSampleType);
        }
            
        /// <summary>
        /// Create a new metric definition.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The type of data captured for each metric under this definition.</param>
        public CustomSampledMetricDefinition(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType)
            : base(definitions, new CustomSampledMetricDefinitionPacket(metricTypeName, categoryName, counterName, metricSampleType))
        {
            m_RequiresMultipleSamples = SampledMetricTypeRequiresMultipleSamples(metricSampleType);
        }

        /// <summary>
        /// Create a new metric definition.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The type of data captured for each metric under this definition.</param>
        /// <param name="unitCaption">The display caption for the calculated values captured under this metric.</param>
        /// <param name="description">A description of what is tracked by this metric, suitable for end-user display.</param>
        public CustomSampledMetricDefinition(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType, string unitCaption, string description)
            : base(definitions, new CustomSampledMetricDefinitionPacket(metricTypeName, categoryName, counterName, metricSampleType, unitCaption, description))
        {
            m_RequiresMultipleSamples = SampledMetricTypeRequiresMultipleSamples(metricSampleType);
        }
            
        /// <summary>
        /// Create a new custom sampled metric object from the provided raw data packet
        /// </summary>
        /// <remarks>The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="packet">The raw data packet</param>
        internal CustomSampledMetricDefinition(MetricDefinitionCollection definitions, CustomSampledMetricDefinitionPacket packet)
            : base(definitions, packet)
        {
            m_RequiresMultipleSamples = SampledMetricTypeRequiresMultipleSamples(packet.MetricSampleType);
        }

        #region Public Properties and Methods


        /// <summary>Creates a new metric definition from the provided information, or returns an existing matching definition if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Custom Sampled Metric (or a derived class) an exception will be thrown.
        /// Definitions are looked up and added to the provided definitions dictionary.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The type of data captured for each metric under this definition.</param>
        /// <param name="unitCaption">The display caption for the calculated values captured under this metric.</param>
        /// <param name="description">A description of what is tracked by this metric, suitable for end-user display.</param>
        public static CustomSampledMetricDefinition AddOrGet(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType, string unitCaption, string description)
        {
            //we must have a definitions collection, or we have a problem
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            //we need to find the definition, adding it if necessary
            string definitionKey = GetKey(metricTypeName, categoryName, counterName);
            IMetricDefinition definition;

            //we need the try and create to be atomic in a multi-threaded environment
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
                    definition = new CustomSampledMetricDefinition(definitions, metricTypeName, categoryName, counterName,
                                                                   metricSampleType, unitCaption, description);
                }
            }

            return (CustomSampledMetricDefinition)definition;
        }

        /// <summary>Creates a new metric definition from the provided information, or returns an existing matching definition if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Custom Sampled Metric (or a derived class) an exception will be thrown.
        /// Definitions are looked up and added to the active logging metrics collection (Log.Metrics)</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The type of data captured for each metric under this definition.</param>
        /// <param name="unitCaption">The display caption for the calculated values captured under this metric.</param>
        /// <param name="description">A description of what is tracked by this metric, suitable for end-user display.</param>
        public static CustomSampledMetricDefinition AddOrGet(string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType, string unitCaption, string description)
        {
            //just forward into our call that requires the definition to be specified
            return AddOrGet(Log.Metrics, metricTypeName, categoryName, counterName, metricSampleType, unitCaption, description);
        }

        /// <summary>
        /// The intended method of interpreting the sampled counter value.
        /// </summary>
        public SamplingType MetricSampleType
        {
            get { return ((CustomSampledMetricDefinitionPacket)base.Packet).MetricSampleType; }
        }

        /// <summary>
        /// Indicates whether a final value can be determined from just one sample or if two comparable samples are required.
        /// </summary>
        public bool RequiresMultipleSamples { get { return m_RequiresMultipleSamples; } }

        /// <summary>
        /// Indicates if this definition is configured to retrieve its information directly from an object.
        /// </summary>
        /// <remarks>When true, metric instances and samples can be defined from a live object of the same type that was used 
        /// to generate the data binding.  It isn't necessary that the same object be used, just that it be a compatible
        /// type to the original type used to establish the binding.</remarks>
        public bool IsBound
        {
            get { return m_Bound; }
            set { m_Bound = value; }
        }

        /// <summary>
        /// When bound, indicates the exact interface or object type that was bound.
        /// </summary>
        /// <remarks>When creating new metrics or metric samples, this data type must be provided in bound mode.</remarks>
        public Type BoundType
        {
            get { return m_BoundType; }
            set { m_BoundType = value; }
        }

        /// <summary>
        /// Compare this custom sampled metric definition with another to determine if they are identical.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(CustomSampledMetricDefinition other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.CompareTo(other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(CustomSampledMetricDefinition other)
        {
            //We're really just a type cast, refer to our base object
            return base.Equals(other);
        }


        /// <summary>
        /// The set of metrics that use this definition.
        /// </summary>
        /// <remarks>All metrics with the same definition are of the same object type.</remarks>
        public new CustomSampledMetricDictionary Metrics
        {
            get { return (CustomSampledMetricDictionary) base.Metrics; }
        }

        /// <summary>
        /// Write a metric sample to the specified metric instance with the provided data immediately, creating the metric if it doesn't exist.
        /// </summary>
        /// <remarks>
        /// <para>The sample is immediately written to the log. If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.  To do this, you can use CreateMetricSample.</para>
        /// <para>Custom metrics using a sample type of AverageFraction and DeltaFraction should not use this method because
        /// they require a base value as well as a raw value.</para>
        /// </remarks>
        /// <param name="instanceName">The instance name to use, or blank or null for the default metric.</param>
        /// <param name="rawValue">The raw data value</param>
        public void WriteSample(string instanceName, double rawValue)
        {
            //Find the right metric sample instance, creating it if we have to.
            CustomSampledMetric ourMetric = EnsureMetricExists(instanceName);

            //now that we have the right metric object, its time to go ahead and create the sample.
            ourMetric.WriteSample(rawValue);
        }

        /// <summary>
        /// Write a metric sample to the specified metric instance with the provided data immediately, creating the metric if it doesn't exist.
        /// </summary>
        /// <remarks>
        /// <para>The sample is immediately written to the log. If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.  To do this, you can use CreateMetricSample.</para>
        /// <para>Custom metrics using a sample type of AverageFraction and DeltaFraction should not use this method because
        /// they require a base value as well as a raw value.</para>
        /// </remarks>
        /// <param name="instanceName">The instance name to use, or blank or null for the default metric.</param>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        public void WriteSample(string instanceName, double rawValue, DateTimeOffset rawTimeStamp)
        {
            //Find the right metric sample instance, creating it if we have to.
            CustomSampledMetric ourMetric = EnsureMetricExists(instanceName);

            //now that we have the right metric object, its time to go ahead and create the sample.
            ourMetric.WriteSample(rawValue, rawTimeStamp);
        }

        /// <summary>
        /// Write a metric sample to the specified metric instance with the provided data immediately, creating the metric if it doesn't exist.
        /// </summary>
        /// <remarks>The sample is immediately written to the log. If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.  To do this, you can use CreateMetricSample</remarks>
        /// <param name="instanceName">The instance name to use, or blank or null for the default metric.</param>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="baseValue">The reference value to compare against for come counter types</param>
        public void WriteSample(string instanceName, double rawValue, double baseValue)
        {
            //Find the right metric sample instance, creating it if we have to.
            CustomSampledMetric ourMetric = EnsureMetricExists(instanceName);

            //now that we have the right metric object, its time to go ahead and create the sample.
            ourMetric.WriteSample(rawValue, baseValue);
        }

        /// <summary>
        /// Write a metric sample to the specified metric instance with the provided data immediately, creating the metric if it doesn't exist.
        /// </summary>
        /// <remarks>The sample is immediately written to the log. If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.  To do this, you can use CreateMetricSample</remarks>
        /// <param name="instanceName">The instance name to use, or blank or null for the default metric.</param>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        /// <param name="baseValue">The reference value to compare against for come counter types</param>
        public void WriteSample(string instanceName, double rawValue, double baseValue, DateTimeOffset rawTimeStamp)
        {
            //Find the right metric sample instance, creating it if we have to.
            CustomSampledMetric ourMetric = EnsureMetricExists(instanceName);

            //now that we have the right metric object, its time to go ahead and create the sample.
            ourMetric.WriteSample(rawValue, baseValue, rawTimeStamp);
        }

        /// <summary>
        /// Indicates if there is a binding for metric instance name.
        /// </summary>
        /// <remarks>When true, the Name Member Name and Name Member Type properties are available.</remarks>
        public bool NameBound
        {
            get { return m_NameBound; }
            set { m_NameBound = value; }
        }

        /// <summary>
        /// The name of the member to invoke to determine the metric instance name.
        /// </summary>
        /// <remarks>This property is only valid when NameBound is true.</remarks>
        public string NameMemberName
        {
            get { return m_NameMemberName; }
            set { m_NameMemberName = value; }
        }

        /// <summary>
        /// The type of the member to be invoked to determine the metric instance name (field, method, or property)
        /// </summary>
        /// <remarks>This property is only valid when NameBound is true.</remarks>
        public MemberTypes NameMemberType
        {
            get { return m_NameMemberType; }
            set { m_NameMemberType = value; }
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Invoked by the base class to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for the metric dictionary in your derived metric, use this
        /// method to create and return your derived object. 
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <returns>The MetricCollection-compatible object.</returns>
        protected override MetricCollection OnMetricDictionaryCreate()
        {
            return new CustomSampledMetricDictionary(this);
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Indicates whether two samples are required to calculate a metric value or not. 
        /// </summary>
        /// <remarks>
        /// Many sample types require multiple samples to determine an output value because they work with 
        /// the change between two points.
        /// </remarks>
        internal static bool SampledMetricTypeRequiresMultipleSamples(SamplingType metricSampleType)
        {
            bool multipleRequired;

            //based purely on the counter type, according to Microsoft documentation
            switch (metricSampleType)
            {
                case SamplingType.RawFraction:
                case SamplingType.RawCount:
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
        internal new CustomSampledMetricDefinitionPacket Packet { get { return (CustomSampledMetricDefinitionPacket) base.Packet; } }
        
        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Retrieves the specified metric instance, or creates it if it doesn't exist
        /// </summary>
        /// <param name="instanceName"></param>
        /// <returns>The custom sampled metric object.</returns>
        private CustomSampledMetric EnsureMetricExists(string instanceName)
        {
            //Find the right metric sample instance, creating it if we have to.
            string metricKey = MetricDefinition.GetKey(this, instanceName);
            IMetric ourMetric;

            //This must be protected in a multi-threaded environment
            lock (Metrics.Lock)
            {
                if (Metrics.TryGetValue(metricKey, out ourMetric) == false)
                {
                    //it doesn't exist - go ahead and add it
                    ourMetric = new CustomSampledMetric(this, instanceName);
                }
            }

            //and return the metric
            return (CustomSampledMetric)ourMetric;
        }

        #endregion
    }
}
