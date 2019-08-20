
using System;
using Loupe.Metrics;


namespace Loupe.Agent.Metrics
{
    /// <summary>
    /// Define a value column mapped from this field, property, or zero-argument method as part of the event metric definition.
    /// </summary>
    /// <remarks>By decorating a field, property, or zero-argument method with this attribute you can describe how it 
    /// should be recorded as a value column of an event metric.  The declaring type must also have the EventMetric
    /// attribute defined.  More than one EventMetricValue attribute may be applied to any field, property, or zero-argument
    /// method provided that each specifies a name unique within this event metric definition.  If not specified (or null),
    /// the name of the value column will be the name of the field, property, or method, and the default caption will be
    /// taken from the name of the value column.  The type will be the member's type (if a supported numeric type) or string
    /// for all other types (using ToString()).  A unit caption must be specified for each value column (or null for unit-less
    /// values), and a default summary function must be designated for the value column to describe how best to aggregate
    /// that column for graphing.  Non-numeric value types (all converted to strings) should use SummaryFunction.Count and
    /// use null for unit caption.  (Timespan, DateTime, and DateTimeOffset are also considered numeric types.)</remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class EventMetricValueAttribute : Attribute
    {
        private string m_Name;
        private readonly string m_UnitCaption;
        private SummaryFunction m_SummaryFunction;

        private string m_Caption;
        private string m_Description;
        private bool m_DefaultValue;

        /// <summary>
        /// Add the current field, property, or method as a value column in the event metric using its declared Type and the specified name, unit caption and summary function.
        /// </summary>
        /// <param name="name">The unique name of this value column within the event.</param>
        /// <param name="summaryFunction">An aggregation summary to best interpret this value column for graphing. (use SummaryFunction.Count for non-math types)</param>
        /// <param name="unitCaption">A displayable caption for the units this value represents, or null for unit-less values.</param>
        public EventMetricValueAttribute(string name, SummaryFunction summaryFunction, string unitCaption)
        {
            m_Name = string.IsNullOrEmpty(name) ? name : name.Trim();
            m_UnitCaption = string.IsNullOrEmpty(unitCaption) ? unitCaption : unitCaption.Trim();
            m_SummaryFunction = summaryFunction;
        }

        /// <summary>
        /// The unique name of this value column within the defined event metric.
        /// </summary>
        public string Name
        {
            get { return m_Name; }
        }

        /// <summary>
        /// Used internally to set the Name property after construction.
        /// </summary>
        /// <param name="name">The new value for the Name property.</param>
        internal void SetName(string name)
        {
            m_Name = string.IsNullOrEmpty(name) ? name : name.Trim();
        }

        /// <summary>
        /// An end-user display caption for the units of this value column of the event metric (or null for unit-less).
        /// </summary>
        public string UnitCaption
        {
            get { return m_UnitCaption; }
        }

        /// <summary>
        /// The default way that individual samples will be aggregated to create a graphable summary.
        /// </summary>
        public SummaryFunction SummaryFunction
        {
            get { return m_SummaryFunction; }
        }

        /// <summary>
        /// Set the default way that individual samples will be aggregated to create a graphable summary.
        /// </summary>
        /// <param name="summaryFunction">The new value for the SummaryFunction property.</param>
        internal void SetSummaryFunction(SummaryFunction summaryFunction)
        {
            m_SummaryFunction = summaryFunction;
        }

        /// <summary>
        /// An end-user display caption for this value column of the event metric.
        /// </summary>
        public string Caption
        {
            get { return m_Caption; }
            set { m_Caption = string.IsNullOrEmpty(value) ? value : value.Trim(); }
        }

        /// <summary>
        /// An extended end-user description of this value column of the event metric.
        /// </summary>
        public string Description
        {
            get { return m_Description; }
            set { m_Description = string.IsNullOrEmpty(value) ? value : value.Trim(); }
        }

        /// <summary>
        /// Indicates this field, property, or method is the default value to graph for the event metric.
        /// </summary>
        /// <remarks>The current object must also have the EventMetric attribute defined.  Only one field, property, or
        /// method in an object can have this value set true (it's false by default).  If no item on an object is marked 
        /// as the default, the number of events will be used as a default summary.</remarks>
        public bool IsDefaultValue
        {
            get { return m_DefaultValue; }
            set { m_DefaultValue = value; }
        }
    }
}
