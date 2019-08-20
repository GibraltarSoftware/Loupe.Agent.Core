using System;
using Loupe.Core.Data;
using Loupe.Core.Metrics;
using Loupe.Core.Serialization;
using Loupe.Metrics;

namespace Loupe.Core.IO.Serialization
{
    /// <summary>
    /// A serializable event value definition.  Provides metadata for one value associated with an event
    /// </summary>
    public class EventMetricValueDefinitionPacket : GibraltarCachedPacket, ICachedPacket, IEquatable<EventMetricValueDefinitionPacket>, IDisplayable
    {
        private string m_Name;
        private string m_Caption;
        private string m_Description;
        private string m_UnitCaption;
        private SummaryFunction m_DefaultTrend = SummaryFunction.Average;
        private Guid m_EventDefinitionPacketId;

        /// <summary>
        /// Creates an event metric definition packet for the provided event metric information
        /// </summary>
        /// <param name="definition">The event metric definition for this value.</param>
        /// <param name="name">The unique name of this event value within the definition.</param>
        /// <param name="type">The simple type of the data being stored in this value.</param>
        public EventMetricValueDefinitionPacket(EventMetricDefinitionPacket definition, string name, Type type)
            : base(false)
        {
            m_EventDefinitionPacketId = definition.ID;
            m_Name = name;
            SetType(type);
            m_Caption = name;

            //TODO: see if we can get a caption & description from the type by reflection
        }

        /// <summary>
        /// Creates an event metric definition packet for the provided event metric information
        /// </summary>
        /// <param name="definition">The event metric definition for this value.</param>
        /// <param name="name">The unique name of this event value within the definition.</param>
        /// <param name="type">The simple type of the data being stored in this value.</param>
        /// <param name="caption">The end-user display caption for this value</param>
        /// <param name="description">The end-user description for this value.</param>
        public EventMetricValueDefinitionPacket(EventMetricDefinitionPacket definition, string name, Type type, string caption, string description)
            : base(false)
        {
            m_EventDefinitionPacketId = definition.ID;
            ID = Guid.NewGuid();
            m_Name = name;
            SetType(type);
            m_Caption = caption;
            m_Description = description;
        }

        //used for rehydration only
        internal EventMetricValueDefinitionPacket(Session session)
            : base(false)
        {
            
        }

        #region Public Properties and Methods

        /// <summary>
        /// The default way that individual samples will be aggregated to create a graphable trend.
        /// </summary>
        public SummaryFunction DefaultTrend
        {
            get { return m_DefaultTrend; }
            set { m_DefaultTrend = value; }
        }

        /// <summary>
        /// The unique name for this value within the event definition.
        /// </summary>
        public string Name { get { return m_Name; } }

        /// <summary>
        /// The end-user display caption for this value.
        /// </summary>
        public string Caption
        {
            get { return m_Caption; }
            set 
            {
                m_Caption = value == null ? Name : value.Trim();
            }
        }

        /// <summary>
        /// The end-user description for this value.
        /// </summary>
        public string Description
        {
            get { return m_Description; }
            set 
            {
                m_Description = value == null ? null : value.Trim();
            }
        }

        /// <summary>
        /// The original type of all data recorded for this value.
        /// </summary>
        public System.Type Type { get; private set; }

        /// <summary>
        /// The simple type of all data recorded for this value.
        /// </summary>
        public System.Type SerializedType { get; private set; }

        /// <summary>
        /// The units of measure for the data captured with this value (if numeric)
        /// </summary>
        public string UnitCaption
        {
            get { return m_UnitCaption; }
            set 
            {
                m_UnitCaption = value == null ? null : value.Trim();
            }
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
            return Equals(other as EventMetricValueDefinitionPacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetricValueDefinitionPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((m_Name == other.m_Name)
                && (Type == other.Type)
                && (m_Caption == other.m_Caption)
                && (m_Description == other.m_Description)
                && (m_DefaultTrend == other.m_DefaultTrend)
                && (m_EventDefinitionPacketId == other.m_EventDefinitionPacketId)
                && (m_UnitCaption == other.m_UnitCaption)
                && base.Equals(other));
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
            int myHash = base.GetHashCode(); // Fold in hash code for inherited base type

            myHash ^= m_EventDefinitionPacketId.GetHashCode(); // Fold in hash code for GUID
            if (m_Name != null) myHash ^= m_Name.GetHashCode(); // Fold in hash code for string Name
            if (m_Caption != null) myHash ^= m_Caption.GetHashCode(); // Fold in hash code for string Caption
            if (m_Description != null) myHash ^= m_Description.GetHashCode(); // Fold in hash code for string Description
            if (m_UnitCaption != null) myHash ^= m_UnitCaption.GetHashCode(); // Fold in hash code for string UnitCaption

            if (Type != null) myHash ^= Type.GetHashCode(); // Fold in hash code for Type member

            // Not bothering with ...Trend member?

            return myHash;
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The unique Id of the definition of this event value.
        /// </summary>
        internal Guid DefinitionId { get { return m_EventDefinitionPacketId; } }

        #endregion

        #region IPacket implementation

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
            const string typeName = nameof(EventMetricValueDefinitionPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("name", FieldType.String);
            definition.Fields.Add("valueType", FieldType.String);
            definition.Fields.Add("caption", FieldType.String);
            definition.Fields.Add("description", FieldType.String);
            definition.Fields.Add("defaultTrend", FieldType.Int32);
            definition.Fields.Add("eventDefinitionPacketId", FieldType.Guid);
            definition.Fields.Add("unitCaption", FieldType.String);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("name", m_Name);
            packet.SetField("valueType", SerializedType.FullName);
            packet.SetField("caption", m_Caption);
            packet.SetField("description", m_Description);
            packet.SetField("defaultTrend", (int)m_DefaultTrend);
            packet.SetField("eventDefinitionPacketId", m_EventDefinitionPacketId);
            packet.SetField("unitCaption", m_UnitCaption);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("name", out m_Name);

                    packet.GetField("valueType", out string typeName);
                    SetType(Type.GetType(typeName));

                    packet.GetField("caption", out m_Caption);
                    packet.GetField("description", out m_Description);

                    packet.GetField("defaultTrend", out int rawDefaultTrend);
                    m_DefaultTrend = (SummaryFunction)rawDefaultTrend;

                    packet.GetField("eventDefinitionPacketId", out m_EventDefinitionPacketId);
                    packet.GetField("unitCaption", out m_UnitCaption);

                    break;
                default:
                    throw new LoupePacketVersionException(definition.Version);
            }
        }

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// Translate provided type to the effective serializable type
        /// </summary>
        /// <param name="originalType"></param>
        private void SetType(Type originalType)
        {
            Type = originalType;
            SerializedType = EventMetricDefinition.IsTrendableValueType(originalType) ? originalType : typeof(string);
        }

        #endregion
    }
}
