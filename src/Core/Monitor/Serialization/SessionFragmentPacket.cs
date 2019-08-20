using System;
using Loupe.Core.Serialization;
#pragma warning disable 1591

namespace Loupe.Core.Monitor.Serialization
{
    public class SessionFragmentPacket : GibraltarCachedPacket, IPacket, IEquatable<SessionFragmentPacket>
    {
        private DateTimeOffset m_FileStartDateTime;
        private DateTimeOffset m_FileEndDateTime;
        private bool m_IsLastFile;

        public DateTimeOffset FileStartDateTime
        {
            get { return m_FileStartDateTime; }
            set
            {
                m_FileStartDateTime = value;
                if (Timestamp == DateTimeOffset.MinValue)
                    Timestamp = value;
            }
        }

        public DateTimeOffset FileEndDateTime { get { return m_FileEndDateTime; } set { m_FileEndDateTime = value; } }

        public bool IsLastFile { get { return m_IsLastFile; } set { m_IsLastFile = value; } }

        /// <summary>
        /// Create a new session file packet for the provided FileID.
        /// </summary>
        /// <param name="m_FileID"></param>
        public SessionFragmentPacket(Guid m_FileID)
            : base(m_FileID, true)
        {            
        }

        /// <summary>
        /// Used during rehydration
        /// </summary>
        internal SessionFragmentPacket()
            : base(false)
        {
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
            return Equals(other as SessionSummaryPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(SessionFragmentPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((FileStartDateTime == other.FileStartDateTime)
                 && (FileEndDateTime == other.FileEndDateTime)
                 && (IsLastFile == other.IsLastFile)
                 && (base.Equals(other)));
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

            myHash ^= m_FileStartDateTime.GetHashCode(); // Fold in hash code for DateTimeOffset member start time
            myHash ^= m_FileEndDateTime.GetHashCode(); // Fold in hash code for DateTimeOffset member end time

            // Not bothering with bool member IsLastFile

            return myHash;
        }


        #region IPacket Members

        private const int SerializationVersion = 1;

        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            return null;
        }

        /// <summary>
        /// Get a new, populated definition for this packet.
        /// </summary>
        /// <returns>A new Packet Definition object</returns>
        /// <remarks>Once a definition is cached by the packet writer it won't be requested again.
        /// Packet Definitions must be invariant for an entire data stream.</remarks>
        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(SessionFragmentPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("FileStartDateTime", FieldType.DateTimeOffset);
            definition.Fields.Add("FileEndDateTime", FieldType.DateTimeOffset);
            definition.Fields.Add("IsLastFile", FieldType.Bool);
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to perisist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("FileStartDateTime", m_FileStartDateTime);
            packet.SetField("FileEndDateTime", m_FileEndDateTime);
            packet.SetField("IsLastFile", m_IsLastFile);
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to perisist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("FileStartDateTime", out m_FileStartDateTime);
                    packet.GetField("FileEndDateTime", out m_FileEndDateTime);
                    packet.GetField("IsLastFile", out m_IsLastFile);
                    break;
            }
        }

        #endregion
    }
}
