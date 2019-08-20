using System;
using Loupe.Messaging;
using Loupe.Serialization;
#pragma warning disable 1591

namespace Loupe.Monitor.Serialization
{
    public abstract class GibraltarPacket : IMessengerPacket, IEquatable<GibraltarPacket>
    {
        private long m_Sequence;
        private DateTimeOffset m_TimeStamp;

        #region IMessengerPacket Members

        /// <summary>
        /// The increasing sequence number of all packets for this session to be used as an absolute order sort.
        /// </summary>
        public long Sequence { get { return m_Sequence; } set { m_Sequence = value; } }

        public DateTimeOffset Timestamp { get { return m_TimeStamp; } set { m_TimeStamp = value; } }

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
            return Equals(other as GibraltarPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(GibraltarPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((Sequence == other.Sequence)
                && (Timestamp == other.Timestamp));
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

            return myHash;
        }

        /// <summary>
        /// Lock in data, optimizing it for storage and memory representation.
        /// </summary>
        /// <remarks>Should be called after the client has configured the data in the packet and on a background thread
        /// to avoid blocking the client.  This does not set the packet as read only, it can still be changed.</remarks>
        public virtual void FixData()
        {
            //by default we do nothing.
        }

        #endregion

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
            const string typeName = nameof(GenericPacket);
            var definition = new PacketDefinition(typeName, Version, false);

            definition.Fields.Add("Sequence", m_Sequence.GetType());
            definition.Fields.Add("TimeStamp", m_TimeStamp.GetType());
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("Sequence", m_Sequence);
            packet.SetField("TimeStamp", m_TimeStamp);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("Sequence", out m_Sequence);
                    packet.GetField("TimeStamp", out m_TimeStamp);
                    break;
            }
        }


        protected void ReadFieldsFast(IFieldReader reader)
        {
            m_Sequence  = reader.ReadInt64();
            m_TimeStamp = reader.ReadDateTimeOffset();
        }
        #endregion
    }
}
