using System;
using Loupe.Core.Serialization;
using Loupe.Extensibility.Data;

#pragma warning disable 1591

namespace Loupe.Core.IO.Serialization
{
    public class ThreadInfoPacket : GibraltarCachedPacket, IPacket, IEquatable<ThreadInfoPacket>, IThreadInfo
    {
        private int m_ThreadIndex;
        private int m_ThreadId;
        private string m_ThreadName;
        private int m_DomainId;
        private string m_DomainName;
        private bool m_IsBackground;
        private bool m_IsThreadPoolThread;

        public ThreadInfoPacket() 
            : base(false)
        {            
        }

        #region Public Properties and Methods

        public int ThreadIndex { get { return m_ThreadIndex; } set { m_ThreadIndex = value; } }

        public int ThreadId { get { return m_ThreadId; } set { m_ThreadId = value; } }

        public string ThreadName { get { return m_ThreadName; } set { m_ThreadName = value; } }

        public int DomainId { get { return m_DomainId; } set { m_DomainId = value; } }

        public string DomainName { get { return m_DomainName; } set { m_DomainName = value; } }

        public bool IsBackground { get { return m_IsBackground; } set { m_IsBackground = value; } }

        public bool IsThreadPoolThread { get { return m_IsThreadPoolThread; } set { m_IsThreadPoolThread = value; } }

        ISession IThreadInfo.Session { get { return null; } } 

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
            return Equals(other as ThreadInfoPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ThreadInfoPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((ThreadIndex == other.ThreadIndex)
                && (ThreadId == other.ThreadId)
                && (ThreadName == other.ThreadName)
                && (DomainId == other.DomainId)
                && (DomainName == other.DomainName)
                && (IsBackground == other.IsBackground)
                && (IsThreadPoolThread == other.IsThreadPoolThread)
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

            myHash ^= m_ThreadId ^ m_DomainId; // Fold in ThreadId and DomainId as hash code for themselves
            myHash ^= m_ThreadIndex << 16; // Fold in the ThreadIndex (in a different position).
            if (m_ThreadName != null) myHash ^= m_ThreadName.GetHashCode(); // Fold in hash code for string ThreadName
            if (m_DomainName != null) myHash ^= m_DomainName.GetHashCode(); // Fold in hash code for string DomainName

            // Not bothering with bool members

            return myHash;
        }

        #endregion

        #region IPacket Implementation

        private const int SerializationVersion = 1;

        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            //we have no dependencies.  Things depend on US!
            return null;
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(ThreadInfoPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("ThreadIndex", FieldType.Int32); 
            definition.Fields.Add("ThreadId", FieldType.Int32);
            definition.Fields.Add("ThreadName", FieldType.String);
            definition.Fields.Add("DomainId", FieldType.Int32);
            definition.Fields.Add("DomainName", FieldType.String);
            definition.Fields.Add("IsBackground", FieldType.Bool);
            definition.Fields.Add("IsThreadPoolThread", FieldType.Bool);
            return definition;
        }


        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("ThreadIndex", m_ThreadIndex); 
            packet.SetField("ThreadId", m_ThreadId);
            packet.SetField("ThreadName", m_ThreadName);
            packet.SetField("DomainId", m_DomainId);
            packet.SetField("DomainName", m_DomainName);
            packet.SetField("IsBackground", m_IsBackground);
            packet.SetField("IsThreadPoolThread", m_IsThreadPoolThread);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version )
            {
                case 1:
                    packet.GetField("ThreadId", out m_ThreadId);
                    if (definition.Fields.ContainsKey("ThreadIndex"))
                    {
                        packet.GetField("ThreadIndex", out m_ThreadIndex);
                        if (m_ThreadIndex == 0)
                            m_ThreadIndex = m_ThreadId; // Zero isn't legal, so it must not have had it.  Fall back to ThreadId.
                    }
                    else
                    {
                        m_ThreadIndex = m_ThreadId; // Use the "unique" ThreadId from older Agent code.
                    }
                    packet.GetField("ThreadName", out m_ThreadName);
                    packet.GetField("DomainId", out m_DomainId);
                    packet.GetField("DomainName", out m_DomainName);
                    packet.GetField("IsBackground", out m_IsBackground);
                    packet.GetField("IsThreadPoolThread", out m_IsThreadPoolThread);
                    break;
            }
        }

        #endregion

    }
}
