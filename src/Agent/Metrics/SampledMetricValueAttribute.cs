using System;
using Loupe.Metrics;


namespace Gibraltar.Agent.Metrics
{
    /// <summary>
    /// Define a specific sampled metric counter mapped from this field, property, or zero-argument method.
    /// </summary>
    /// <remarks>By decorating a field, property, or method with this attribute you can define how it should be recorded
    /// as a sampled metric.  The enclosing object must also have the SampledMetric attribute defined.  More than one
    /// SampledMetricValue attribute may be applied to any field, property, or zero-argument method, provided that each
    /// specifies a counter name unique within the metric namespace and category name specified in the SampledMetric
    /// attribute on the enclosing object.  If the counter name is not specified (or null), the name of the member is used
    /// as the counter name by default.  For sampling types requiring a divisor, use the SampledMetricDivisor
    /// attribute (specifying the same counter name as this sampled metric) to designate another field, property, or method
    /// to provide it.</remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SampledMetricValueAttribute : Attribute
    {
        private string m_CounterName;
        private readonly SamplingType m_SamplingType;
        private readonly string m_UnitCaption;

        private string m_Caption;
        private string m_Description;

        /// <summary>
        /// Map the current field, property, or method to a sampled metric as its own name, with a specified sampling type and unit caption.
        /// </summary>
        /// <param name="counterName">The name of the metric counter to be defined under the metric category name.</param>
        /// <param name="samplingType">The sampling type of the sampled metric.</param>
        /// <param name="unitCaption">A displayable caption for the units this value represents, or null for unit-less values.</param>
        public SampledMetricValueAttribute(string counterName, SamplingType samplingType, string unitCaption)
        {
            m_CounterName = string.IsNullOrEmpty(counterName) ? counterName : counterName.Trim();
            m_SamplingType = samplingType;
            m_UnitCaption = string.IsNullOrEmpty(unitCaption) ? unitCaption : unitCaption.Trim();
        }

        /// <summary>
        /// The name of the metric counter to be defined under the metric category name.
        /// </summary>
        public string CounterName
        {
            get { return m_CounterName; }
        }

        /// <summary>
        /// Used internally to set the CounterName property after construction (e.g. to correct a null).
        /// </summary>
        /// <param name="counterName">The new value for the CounterName property.</param>
        internal void SetCounterName(string counterName)
        {
            m_CounterName = string.IsNullOrEmpty(counterName) ? counterName : counterName.Trim();
        }

        /// <summary>
        /// The sampling type of the metric.
        /// </summary>
        public SamplingType SamplingType
        {
            get { return m_SamplingType; }
        }

        /// <summary>
        /// A displayable caption for the units this value represents, or null for unit-less values.
        /// </summary>
        public string UnitCaption
        {
            get { return m_UnitCaption; }
        }

        /// <summary>
        /// A displayable caption for this sampled metric counter.
        /// </summary>
        public string Caption
        {
            get { return m_Caption; }
            set { m_Caption = string.IsNullOrEmpty(value) ? value : value.Trim(); }
        }

        /// <summary>
        /// An extended end-user description of this sampled metric counter.
        /// </summary>
        public string Description
        {
            get { return m_Description; }
            set { m_Description = string.IsNullOrEmpty(value) ? value : value.Trim(); }
        }
    }

    /// <summary>
    /// Designate a field, property, or method to provide the divisor for a specified sampled metric counter.
    /// </summary>
    /// <remarks>The current object must also have the SampledMetric attribute defined, and must have a SampledMetricValue
    /// attribute defined for the same counter name as this attribute and defining a sampling type requiring a divisor,
    /// or this attribute is not meaningful.  This attribute may be defined multiple times (even on the same member), but
    /// only one SampledMetricDivisor attribute may be defined for the same sampled metric counter in the object.</remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SampledMetricDivisorAttribute : Attribute
    {
        private readonly string m_CounterName;

        /// <summary>
        /// Map the current field, property, or zero-argument method to provide the divisor for the sampled metric with the specified counter name.
        /// </summary>
        /// <param name="counterName">The name of a sampled metric with a sampling type requiring a divisor.</param>
        public SampledMetricDivisorAttribute(string counterName)
        {
            m_CounterName = string.IsNullOrEmpty(counterName) ? counterName : counterName.Trim();
        }

        /// <summary>
        /// The name of a sampled metric counter with a sampling type requiring a divisor to be provided by the target of this attribute.
        /// </summary>
        public string CounterName
        {
            get { return m_CounterName; }
        }
    }
}
