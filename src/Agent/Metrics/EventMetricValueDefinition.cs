
using System;
using System.Reflection;
using Loupe.Metrics;


namespace Loupe.Agent.Metrics
{
    /// <summary>
    /// Defines one value that can be associated with an event metric, created via eventMetricDefinition.AddValue(...);
    /// </summary>
    public sealed class EventMetricValueDefinition : IEquatable<EventMetricValueDefinition>
    {
        private readonly EventMetricDefinition m_Definition;
        private readonly Monitor.EventMetricValueDefinition m_WrappedValueDefinition;

        /// <summary>
        /// Create a new API value definition from a provided API event metric definition and internal value definition.
        /// </summary>
        /// <param name="definition">The API event metric definition that owns this value definition</param>
        /// <param name="valueDefinition">The internal value definition to wrap.</param>
        internal EventMetricValueDefinition(EventMetricDefinition definition, Monitor.EventMetricValueDefinition valueDefinition)
        {
            m_Definition = definition;
            m_WrappedValueDefinition = valueDefinition;
        }

        #region Public Properties and Methods

        /// <summary>
        /// The default way that individual samples will be aggregated to create a graphable summary.
        /// </summary>
        public SummaryFunction SummaryFunction
        {
            get { return (SummaryFunction)m_WrappedValueDefinition.DefaultTrend; }
            internal set { m_WrappedValueDefinition.DefaultTrend = value; }
        }

        /// <summary>
        /// The metric definition this value is associated with.
        /// </summary>
        public EventMetricDefinition Definition { get { return m_Definition; } }

        /// <summary>
        /// The unique name for this value within the event definition.
        /// </summary>
        public string Name { get { return m_WrappedValueDefinition.Name; } }

        /// <summary>
        /// The end-user display caption for this value.
        /// </summary>
        public string Caption
        {
            get { return m_WrappedValueDefinition.Caption; }
            internal set { m_WrappedValueDefinition.Caption = value; }
        }

        /// <summary>
        /// The end-user description for this value.
        /// </summary>
        public string Description
        {
            get { return m_WrappedValueDefinition.Description; }
            internal set { m_WrappedValueDefinition.Description = value; }
        }

        /// <summary>
        /// The simple type of all data recorded for this value.
        /// </summary>
        public Type Type { get { return m_WrappedValueDefinition.Type; } }

        /// <summary>
        /// The simple type that all data recorded for this value will be serialized as.
        /// </summary>
        public Type SerializedType { get { return m_WrappedValueDefinition.SerializedType; } }

        /// <summary>
        /// Indicates whether this metric value column can be graphed by a mathematical summary (true), or only by count (false).
        /// </summary>
        public bool IsNumeric { get { return m_WrappedValueDefinition.IsTrendable; } }

        /// <summary>
        /// The display caption for the units this value column represents (if numeric), or null for unit-less values (or non-numeric).
        /// </summary>
        public string UnitCaption
        {
            get { return m_WrappedValueDefinition.UnitCaption; }
            internal set { m_WrappedValueDefinition.UnitCaption = value; }
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetricValueDefinition other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            //We're really just a type cast, refer to our base object
            return WrappedValueDefinition.Equals(other.WrappedValueDefinition);
        }


        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Indicates whether the value is configured for automatic collection through binding
        /// </summary>
        /// <remarks>If true, the other binding-related properties are available.</remarks>
        internal bool Bound
        {
            get { return m_WrappedValueDefinition.Bound; }
            set { m_WrappedValueDefinition.Bound = value; }
        }

        /// <summary>
        /// The type of member that this value is bound to (field, property or method)
        /// </summary>
        /// <remarks>This property is only valid if Bound is true.</remarks>
        internal MemberTypes MemberType
        {
            get { return m_WrappedValueDefinition.MemberType; }
            set { m_WrappedValueDefinition.MemberType = value; }
        }

        /// <summary>
        /// The name of the member that this value is bound to.
        /// </summary>
        /// <remarks>This property is only valid if Bound is true.</remarks>
        internal string MemberName
        {
            get { return m_WrappedValueDefinition.MemberName; }
            set { m_WrappedValueDefinition.MemberName = value; }
        }

        /// <summary>
        /// Conversion to the inner packet object
        /// </summary>
        /// <returns></returns>
        internal Monitor.EventMetricValueDefinition WrappedValueDefinition { get { return m_WrappedValueDefinition; } }

        #endregion
    }
}
