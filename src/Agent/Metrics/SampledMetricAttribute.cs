using System;



namespace Gibraltar.Agent.Metrics
{
    /// <summary>
    /// Describes shared parameters for sampled metrics defined on the current object.
    /// </summary>
    /// <remarks><para>An object (class, struct, or interface) can be decorated with this attribute to describe how to define
    /// a group of sampled metrics among its direct members.  Use the SampledMetricValue attribute on individual properties
    /// (etc) to complete the definition of various sampled metrics to be collected on this object.  Any number of sampled
    /// metrics may be defined among the direct members (properties, fields, and zero-argument methods) of this object,
    /// but they will all share the metrics system and category name declared in this attribute.  If sampling is performed
    /// for the entire group in one call, they will also share the same instance name (see SampledMetricInstanceName
    /// attribute and the instanceName argument of relevant API methods).  The sampled metrics can also be sampled
    /// separately (with independent instance names) through their individual definitions instead of as a group on a
    /// specific class, struct, or interface type.</para>
    /// <para>This attribute may be used on other classes, structs, and interfaces even with the same metric namespace
    /// and category name, but each use of the SampledMetricValue attribute to define individual sampled metrics must
    /// use a counter name unique within the particular namespace and category name across all uses of the SampledMetric
    /// attribute (or other sampled metric definitions through API calls).  Also, sampled metrics and event metrics may not
    /// overlap with the same counter name within a given namespace and category name.  When sampling a particular object's
    /// sampled metrics as a group, only those sampled metrics defined with a SampledMetricValue attribute within the
    /// specific enclosing type (marked with a SampledMetric attribute) will be seen as part of the group to sample, not
    /// sampled metrics defined under different enclosing types even with the same namespace and category name.</para>
    /// <para>As with event metrics, multiple sampled metric groups (allowing different category names and instance names)
    /// can be combined on an object by defining them on separate interfaces.  Such advanced tricks may require selection
    /// of a specific sampled metric group by type (e.g. by typeof a particular interface) in order to sample each possible
    /// sampled metric group as desired for that object.  Selection of a definition by a specific type may also be required
    /// when sampling an inheritor object, to ensure that all desired sampled metrics are found and sampled.</para></remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class SampledMetricAttribute : Attribute
    {
        private readonly string m_Namespace;
        private readonly string m_CategoryName;

        /// <summary>
        /// Designate the target object as containing a group of sampled metrics sharing the specified namespace and category name.
        /// </summary>
        /// <param name="metricsSystem">The metrics capture system label the user has selected to distinguish all metrics they define, to avoid colliding with usage by other libraries.</param>
        /// <param name="metricCategoryName">A dot-delimited categorization for the metric under the metrics system.</param>
        public SampledMetricAttribute(string metricsSystem, string metricCategoryName)
        {
            m_Namespace = string.IsNullOrEmpty(metricsSystem) ? metricsSystem : metricsSystem.Trim();
            m_CategoryName = string.IsNullOrEmpty(metricCategoryName) ? metricCategoryName : metricCategoryName.Trim();
        }

        /// <summary>
        /// The metrics capture system label the user has selected to distinguish all metrics they define, to avoid colliding with usage by other libraries.
        /// </summary>
        public string MetricsSystem
        {
            get { return m_Namespace; }
        }

        /// <summary>
        /// A dot-delimited categorization for the metric under the particular metrics capture system.
        /// </summary>
        public string MetricCategoryName
        {
            get { return m_CategoryName; }
        }
    }

    /// <summary>
    /// Indicates which field, property, or method should be used to determine the category name for the sampled metric.
    /// </summary>
    /// <remarks>The current object must also have the SampledMetric attribute defined.  Only one field, property, or method in an object
    /// can have this attribute defined.  Whatever value is returned will be converted to a string to be the category name to the metric.
    /// If no item on an object has this attribute defined, the attribute value from the SampledMetric attribute will be used.</remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class SampledMetricCategoryNameAttribute : Attribute
    {
    }

    // Note: The above attribute is currently internal to prevent client use because it is not presently supported.
    // But we might consider adding this capability in the future, if we can figure out how to sensibly support it.
    // For now, it seems too problematic, and the hypothetical benefits seem to be achievable with clever usage of the
    // instance name (e.g. using their own dot-delimited hierarchy in the instance name), which we could more easily
    // support analysis across multiple metric instances because they would all share a single definition.

    /// <summary>
    /// Indicates which field, property, or method should be used to provide the instance name for the sampled metric.
    /// </summary>
    /// <remarks>The current object must also have the SampledMetric attribute defined.  Only one field, property, or method in an object
    /// can have this attribute defined.  Whatever value is returned will be converted to a string to uniquely identify the
    /// metric, or a null value will select the default instance.  If no item on an object has this attribute defined, the
    /// default instance will be used unless an instance name is specified when sampling.</remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class SampledMetricInstanceNameAttribute : Attribute
    {
    }

}
