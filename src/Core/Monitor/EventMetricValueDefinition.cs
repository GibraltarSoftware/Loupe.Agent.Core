
using System;
using System.Diagnostics;
using System.Reflection;
using Gibraltar.Monitor.Internal;
using Gibraltar.Serialization;
using Loupe.Extensibility.Data;



namespace Gibraltar.Monitor
{
    /// <summary>
    /// Defines one value that can be associated with an event metric.
    /// </summary>
    [DebuggerDisplay("Name: {Name}, Index: {Index}")]
    public class EventMetricValueDefinition: IEventMetricValueDefinition
    {
        private readonly EventMetricDefinition m_Definition;
        private readonly EventMetricValueDefinitionPacket m_Packet;
        private readonly bool m_Trendable;

        /// <summary>
        /// Create a new value definition from the provided information.
        /// </summary>
        /// <param name="definition">The metric definition that owns this value definition</param>
        /// <param name="packet">The prepopulated value definition packet.</param>
        internal EventMetricValueDefinition(EventMetricDefinition definition, EventMetricValueDefinitionPacket packet)
        {
            m_Definition = definition;
            m_Packet = packet;

            //and determine if it is trendable or not
            m_Trendable = EventMetricDefinition.IsTrendableValueType(m_Packet.Type);
            MyIndex = -1; // Mark it as unknown.
        }

        #region Public Properties and Methods

        /// <summary>
        /// The default way that individual samples will be aggregated to create a graphable trend.
        /// </summary>
        public EventMetricValueTrend DefaultTrend
        {
            get { return m_Packet.DefaultTrend; }
            set { m_Packet.DefaultTrend = value; }
        }

        /// <summary>
        /// The metric definition this value is associated with.
        /// </summary>
        public IEventMetricDefinition Definition { get { return m_Definition; } }

        /// <summary>
        /// The unique name for this value within the event definition.
        /// </summary>
        public string Name { get { return m_Packet.Name; } }

        /// <summary>
        /// The end-user display caption for this value.
        /// </summary>
        public string Caption
        {
            get { return m_Packet.Caption; }
            set { m_Packet.Caption = value; }
        }

        /// <summary>
        /// The end-user description for this value.
        /// </summary>
        public string Description
        {
            get { return m_Packet.Description; }
            set { m_Packet.Description = value; }
        }

        /// <summary>
        /// The simple type of all data recorded for this value.
        /// </summary>
        public Type Type { get { return m_Packet.Type; } }

        /// <summary>
        /// The simple type that all data recorded for this value will be serialized as.
        /// </summary>
        public Type SerializedType { get { return m_Packet.SerializedType; } }

        /// <summary>
        /// Indicates whether the metric value can be graphed as a trend.
        /// </summary>
        public bool IsTrendable { get { return m_Trendable; } }

        /// <summary>
        /// The units of measure for the data captured with this value (if numeric)
        /// </summary>
        public string UnitCaption
        {
            get { return m_Packet.UnitCaption; }
            set { m_Packet.UnitCaption = value; }
        }

        /// <summary>
        /// The index of this value definition (and related values) within the values collection.
        /// </summary>
        /// <remarks>Since sample values are provided as an object array it is useful to cache the 
        /// index of an individual value to rapidly retrieve specific values from each sample.</remarks>
        public int Index
        {
            get { return MyIndex; }
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(IEventMetricValueDefinition other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (ReferenceEquals(this, other) || ReferenceEquals(m_Packet, ((EventMetricValueDefinition)other).Packet))
                return true; // If the objects or the underlying packet are the same object, they're the same.

            //We're really just a type cast, refer to our base object
            return m_Packet.Name.Equals(other.Name);
        }

        /// <summary>
        /// Indicates whether the value is configured for automatic collection through binding
        /// </summary>
        /// <remarks>If true, the other binding-related properties are available.</remarks>
        public bool Bound { get; set; }

        /// <summary>
        /// The type of member that this value is bound to (field, property or method)
        /// </summary>
        /// <remarks>This property is only valid if Bound is true.</remarks>
        public MemberTypes MemberType { get; set; }

        /// <summary>
        /// The name of the member that this value is bound to.
        /// </summary>
        /// <remarks>This property is only valid if Bound is true.</remarks>
        public string MemberName { get; set; }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Conversion to the inner packet object
        /// </summary>
        /// <returns></returns>
        internal EventMetricValueDefinitionPacket Packet { get { return m_Packet; } }

        /// <summary>
        /// The index of this value in the arrays, once the definition is read-only.
        /// </summary>
        internal int MyIndex { get; set; }

        /// <summary>
        /// Add a definition for this value to the packet definition
        /// </summary>
        /// <param name="definition">The packet definition to add our value definition to</param>
        internal void AddField(PacketDefinition definition)
        {
            definition.Fields.Add(Name, m_Packet.SerializedType);
        }

        #endregion
    }
}
