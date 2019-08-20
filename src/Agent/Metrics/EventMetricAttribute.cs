
using System;



namespace Loupe.Agent.Metrics
{
    /// <summary>
    /// Define an event metric with value columns from the members of the current object.
    /// </summary>
    /// <remarks><para>An object (class, struct, or interface) can be decorated with this attribute to define an event metric
    /// for it.  Use the EventMetricValue attribute to designate which direct members (properties, fields, or zero-argument
    /// methods) should be stored as value columns each time the event metric is sampled.  The EventMetricInstanceName
    /// attribute can optionally be used on a member (typically not one also chosen as a value column, but it is allowed
    /// to be) to designate that member to automatically provide the instance name when sampling the object for this
    /// defined event metric.</para>
    /// <para>Only one event metric (containing any number of value columns) can be defined on a specific class, struct,
    /// or interface.  However, using interfaces to define event metrics can allow a single object to support multiple
    /// event metric types through those separate interfaces.  Such advanced tricks may require selection of a specific
    /// event metric definition by type (e.g. by typeof a particular interface) in order to sample each possible event
    /// metric as desired for that object.  Selection of a definition by a specific type may also be required when sampling
    /// an inheritor object, to ensure the desired event metric is identified and sampled as appropriate, because multiple
    /// event metrics defined on a complex object can not be assumed to all be appropriate to sample every time.</para></remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class EventMetricAttribute : Attribute
    {
        private readonly string m_Namespace;
        private readonly string m_CategoryName;
        private readonly string m_CounterName;

        private string m_Caption;
        private string m_Description;

        /// <summary>
        /// Define an event metric with value columns to be selected from the direct members of the current object.
        /// </summary>
        /// <param name="metricsSystem">The metrics capture system label the user has selected to distinguish all metrics they define, to avoid colliding with usage by other libraries.</param>
        /// <param name="metricCategoryName">A dot-delimited categorization for the metric under the metrics system.</param>
        /// <param name="counterName">The name of the metric to be defined under the metric category name.</param>
        public EventMetricAttribute(string metricsSystem, string metricCategoryName, string counterName)
        {
            m_Namespace = string.IsNullOrEmpty(metricsSystem) ? metricsSystem : metricsSystem.Trim();
            m_CategoryName = string.IsNullOrEmpty(metricCategoryName) ? metricCategoryName : metricCategoryName.Trim();
            m_CounterName = string.IsNullOrEmpty(counterName) ? counterName : counterName.Trim();
        }

        /// <summary>
        /// The metrics capture system label the user has selected to distinguish all metrics they define, to avoid colliding with usage by other libraries.
        /// </summary>
        public string MetricsSystem
        {
            get { return m_Namespace; }
        }

        /// <summary>
        /// A dot-delimited categorization for the metric under the metrics system.
        /// </summary>
        public string MetricCategoryName
        {
            get { return m_CategoryName; }
        }

        /// <summary>
        /// The name of the metric to be defined under the metric category name.
        /// </summary>
        public string CounterName
        {
            get { return m_CounterName; }
        }

        /// <summary>
        /// A displayable caption for this event metric definition.
        /// </summary>
        public string Caption
        {
            get { return m_Caption; }
            set { m_Caption = string.IsNullOrEmpty(value) ? value : value.Trim(); }
        }

        /// <summary>
        /// An end-user description of this metric definition.
        /// </summary>
        public string Description
        {
            get { return m_Description; }
            set { m_Description = string.IsNullOrEmpty(value) ? value : value.Trim(); }
        }
    }

    /// <summary>
    /// Indicates which field, property, or method should be used to determine the instance name for the event metric.
    /// </summary>
    /// <remarks>The current object must also have the EventMetric attribute defined.  Only one field, property, or method in an object
    /// can have this attribute defined.  Whatever value is returned will be converted to a string to uniquely identify the metric, or
    /// a null value will select the default instance.  If no item on an object has this attribute defined, the default event metric will
    /// be used unless an instance name is specified when sampling.</remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EventMetricInstanceNameAttribute : Attribute
    {
    }

}
