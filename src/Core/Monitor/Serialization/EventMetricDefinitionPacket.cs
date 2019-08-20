using System;
using Loupe.Core.Serialization;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Monitor.Serialization
{
    /// <summary>
    /// A serializable event metric definition.  Provides metadata for metrics based on events.
    /// </summary>
    public sealed class EventMetricDefinitionPacket : MetricDefinitionPacket, ICachedPacket, IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>, IComparable<EventMetricDefinitionPacket>, IEquatable<EventMetricDefinitionPacket>
    {
        private string m_DefaultValueName;

        /// <summary>
        /// Creates an event metric definition packet for the provided event metric information
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        public EventMetricDefinitionPacket(string metricTypeName, string categoryName, string counterName)
            : base(metricTypeName, categoryName, counterName, SampleType.Event)
        {
        }

        /// <summary>
        /// Create an event metric definition packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        public EventMetricDefinitionPacket(Session session)
            : base(session)
        {            
        }

        #region Public Properties and Methods


        /// <summary>
        /// The default value to display for this event metric.  Typically this should be a trendable value.
        /// </summary>
        public string DefaultValueName { get { return m_DefaultValueName; } set { m_DefaultValueName = value; } }


        /// <summary>
        /// Compare this event metric definition packet with another.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(EventMetricDefinitionPacket other)
        {
            //we just gateway to our base object.
            return base.CompareTo(other);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public override bool Equals(object other)
        {
            //use our type-specific override
            return Equals(other as EventMetricDefinitionPacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetricDefinitionPacket other)
        {
            //We're really just a type cast, refer to our base object
            return base.Equals(other);
        }

        /// <summary>
        /// Provides a representative hash code for objects of this type to spread out distribution
        /// in hash tables.
        /// </summary>
        /// <remarks>Objects which consider themselves to be Equal (a.Equals(b) returns true) are
        /// expected to have the same hash code.  Objects which are not Equal may have the same
        /// hash code, but minimizing such overlaps helps with efficient operation of hash tables.
        /// </remarks>
        /// <returns>
        /// an int representing the hash code calculated for the contents of this object
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = base.GetHashCode(); // Equals defers to base, so just use hash code for inherited base type

            return myHash;
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The collection of value definitions for this event metric
        /// </summary>
        /// <remarks>This is really a hack to allow the packet writer deep in the bowels of the system
        /// to find the metric value definitions to write out.  We really need to refactor the model to get 
        /// rid of this much coupling.</remarks>
        internal EventMetricValueDefinitionCollection MetricValues { get; set; }

        #endregion

        #region IPacket Members

        private const int SerializationVersion = 1;

        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            //the majority of packets have no dependencies
            return null;
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(EventMetricDefinitionPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("DefaultValueName", FieldType.String);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("DefaultValueName", m_DefaultValueName);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("DefaultValueName", out m_DefaultValueName);
                    break;
            }
        }

        #endregion


        #region IPacketObjectFactory<MetricDefinition, object> Members

        MetricDefinition IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>.GetDataObject(MetricDefinitionCollection optionalParent)
        {
            //this is just here for us to be able to create our derived type for the generic infrastructure
            EventMetricDefinition newDefinitionObject = new EventMetricDefinition(optionalParent, this);
            optionalParent.Add(newDefinitionObject); // We have to add it to the collection, no longer done in constructor.
            return newDefinitionObject;
        }

        #endregion
    }
}
