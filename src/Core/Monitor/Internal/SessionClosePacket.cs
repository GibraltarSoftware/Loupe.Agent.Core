
using System;
using System.Diagnostics;
using System.Globalization;
using Gibraltar.Serialization;
using Loupe.Extensibility.Data;



namespace Gibraltar.Monitor.Internal
{
    /// <summary>
    /// Marks the ending status of a session.
    /// </summary>
    internal class SessionClosePacket : GibraltarPacket, IPacket, IComparable<SessionClosePacket>, IEquatable<SessionClosePacket>
    {
        private readonly Session m_Session; //used for rehydration

        private Guid m_Id;
        private SessionStatus m_EndingStatus;

        public SessionClosePacket()
        {
            //we aren't a cacheable packet so we have our own GUID
            Id = Guid.NewGuid();
            EndingStatus = SessionStatus.Normal;
        }

        public SessionClosePacket(SessionStatus endingStatus)
        {
            //we aren't a cacheable packet so we have our own GUID
            Id = Guid.NewGuid();
            EndingStatus = endingStatus;
        }

        public SessionClosePacket(Session session)
        {
            m_Session = session;
            EndingStatus = SessionStatus.Normal;
        }

        #region Public Properties and methods

        public Guid Id { get { return m_Id; } private set { m_Id = value; } }

        public SessionStatus EndingStatus { get { return m_EndingStatus; } set { m_EndingStatus = value; } }

        public override string ToString()
        {
            string text = string.Format(CultureInfo.CurrentCulture, "Session Close: Status is {0}", m_EndingStatus);
            return text;
        }

        public int CompareTo(SessionClosePacket other)
        {
            //First do a quick match on Guid.  this is the only case we want to return zero (an exact match)
            if (Id == other.Id)
                return 0;

            //now we want to sort by our nice increasing sequence #
            int compareResult = Sequence.CompareTo(other.Sequence);

            Debug.Assert(compareResult != 0); //no way we should ever get an equal at this point.

            return compareResult;
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
            return Equals(other as SessionClosePacket);
        }


        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(SessionClosePacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((Id == other.Id)
                 && (EndingStatus == other.EndingStatus)
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

            myHash ^= Id.GetHashCode(); // Fold in hash code for GUID
            myHash ^= EndingStatus.GetHashCode(); // Fold in hash code for EndingStatus enum

            // Session member is not used in Equals, so we can't use it in hash calculation!

            return myHash;
        }

        #endregion

        #region IPacket Members

        /// <summary>
        /// The current serialization version
        /// </summary>
        /// <remarks>
        /// <para>Version 1: Added Id and EndingStatus field to previously empty packet.</para>
        /// </remarks>
        private const int SerializationVersion = 1;

        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            // We don't depend on any packets.
            return null;
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(SessionClosePacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("Id", FieldType.Guid);
            definition.Fields.Add("Status", FieldType.Int32);

            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("Id", m_Id);
            packet.SetField("Status", (int)m_EndingStatus);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("Id", out m_Id);

                    // Hmmm, it's tricky to handle the enum with an out parameter; use a temporary int and cast it.
                    int status;
                    packet.GetField("Status", out status);
                    m_EndingStatus = (SessionStatus)status;
                    break;
            }
        }

        #endregion
    }
}
