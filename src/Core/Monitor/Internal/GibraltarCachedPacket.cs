using System;
using System.Diagnostics;
using Gibraltar.Messaging;
using Gibraltar.Serialization;




namespace Gibraltar.Monitor.Internal
{
    internal abstract class GibraltarCachedPacket : ICachedMessengerPacket, IEquatable<GibraltarCachedPacket>
    {
        private long m_Sequence;
        private DateTimeOffset m_TimeStamp;
        private Guid m_ID;
        private bool m_IsHeader;

        protected GibraltarCachedPacket(Guid packetID, bool isHeader)
        {
            ID = packetID;
            m_IsHeader = isHeader;
        }

        protected GibraltarCachedPacket(bool isHeader)
        {
            ID = Guid.NewGuid();
            m_IsHeader = isHeader;
        }

        /// <summary>
        /// The increasing sequence number of all packets for this session to be used as an absolute order sort.
        /// </summary>
        public long Sequence { get { return m_Sequence; } set { m_Sequence = value; } }

        public DateTimeOffset Timestamp { get { return m_TimeStamp; } set { m_TimeStamp = value; } }

        public Guid ID { get { return m_ID; } set { m_ID = value; } }

        public bool IsHeader { get { return m_IsHeader; } }

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
            return Equals(other as GibraltarCachedPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(GibraltarCachedPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((Sequence == other.Sequence)
                && (Timestamp == other.Timestamp)
                && (ID == other.ID));
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
            int myHash = (int) (m_Sequence >> 32) ^ (int) (m_Sequence); // Fold long Sequence down to 32-bit hash code

            myHash ^= m_TimeStamp.GetHashCode(); // Fold in hash code for DateTimeOffset member TimeStamp
            myHash ^= m_ID.GetHashCode(); // Fold in hash code for GUID

            return myHash;
        }

        #region IPacket Members

        private const int Version = 1;

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
            const string typeName = nameof(GibraltarCachedPacket);
            var definition = new PacketDefinition(typeName, Version, false);

            definition.Fields.Add("Sequence", FieldType.Int64);
            definition.Fields.Add("TimeStamp", FieldType.DateTimeOffset);
            definition.Fields.Add("ID", FieldType.Guid);
            definition.Fields.Add("IsHeader", FieldType.Bool);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
#if DEBUG
            if (m_TimeStamp == DateTimeOffset.MinValue && Debugger.IsAttached)
                Debugger.Break(); // Stop the debugger before the Assert
            Debug.Assert(m_TimeStamp.Year > 1 ); //watch for timestamp being some variation of zero
#endif

            packet.SetField("Sequence", m_Sequence);
            packet.SetField("TimeStamp", m_TimeStamp);
            packet.SetField("ID", m_ID);
            packet.SetField("IsHeader", m_IsHeader);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("Sequence", out m_Sequence);
                    packet.GetField("TimeStamp", out m_TimeStamp);
                    packet.GetField("ID", out m_ID);
                    packet.GetField("IsHeader", out m_IsHeader);
                    break;
            }
        }

        #endregion

    }
}
