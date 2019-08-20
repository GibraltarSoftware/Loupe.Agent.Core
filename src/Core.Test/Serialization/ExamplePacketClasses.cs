using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Loupe;
using Loupe.Serialization;

namespace Loupe.Core.Serialization.UnitTests
{
    // The concept of a DynamicPacket is that some instance objects may have a 
    // different (i.e. dynamic) number of fields from other instances.
    // This may be needed for EventMetrics.  To accommodate this, we need a way
    // to associate a packet definition with each variation of the number of fields
    // and each packet instance must somehow be associated with the corresponding
    // packet definition.  We accomplish this by implementing the IDynamicPacket
    // interface.  The DynamicTypeName is then used to allow for these mappings.
    internal class DynoPacket : IDynamicPacket, IEquatable<DynoPacket>
    {
        private string[] m_Strings;
        private int[] m_Ints;
        private string m_DynamicTypeName;

        /// <summary>
        /// A default constructor is necessary to properly implement IPacket
        /// </summary>
        internal DynoPacket()
        {
        }

        public DynoPacket(int stringCount, int intCount)
        {
            m_Strings = new string[stringCount];
            m_Ints = new int[intCount];
            for (int i = 0; i < Math.Max(stringCount, intCount); i++)
            {
                if (i < stringCount)
                    m_Strings[i] = "String " + (i + 1);
                if (i < intCount)
                    m_Ints[i] = i + 1;
            }
        }

        #region IDynamicPacket Members

        public string DynamicTypeName { get { return m_DynamicTypeName; } set { m_DynamicTypeName = value; } }

        #endregion

        #region IPacket Members

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
            string typeName = nameof(DynoPacket);
            PacketDefinition definition = new PacketDefinition(typeName, 1, true);

            for (int i = 0; i < m_Strings.Length; i++)
                definition.Fields.Add("String " + (i + 1), FieldType.String);
            for (int i = 0; i < m_Ints.Length; i++)
                definition.Fields.Add("Int " + (i + 1), FieldType.Int32);
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            for (int i = 0; i < m_Strings.Length; i++)
                packet.SetField("String " + (i + 1), m_Strings[i]);
            for (int i = 0; i < m_Ints.Length; i++)
                packet.SetField("Int " + (i + 1), m_Ints[i]);
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            // In order for the NMock tests to work right, the number and type
            // of fields read must match the PacketDefinition exactly
            int stringCount = 0;
            foreach (FieldDefinition fieldDefinition in definition)
            {
                if (fieldDefinition.FieldType != FieldType.String)
                    break;
                stringCount++;
            }
            int intCount = definition.Fields.Count - stringCount;
            m_Strings = new string[stringCount];
            m_Ints = new int[intCount];
            for (int i = 0; i < m_Strings.Length; i++)
            {
                string newString;
                packet.GetField("String " + (i + 1), out newString);
                m_Strings[i] = newString;
            }

            for (int i = 0; i < m_Ints.Length; i++)
            {
                int newInteger;
                packet.GetField("Int " + (i + 1), out newInteger);
                m_Ints[i] = newInteger;
            }
        }

        #endregion

        #region IEquatable<DynoPacket> Members

        public override bool Equals(object obj)
        {
            return Equals(obj as DynoPacket);
        }

        ///<summary>
        ///Indicates whether the current object is equal to another object of the same type.
        ///</summary>
        ///
        ///<returns>
        ///true if the current object is equal to the other parameter; otherwise, false.
        ///</returns>
        ///
        ///<param name="other">An object to compare with this object.</param>
        public bool Equals(DynoPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (m_Strings.Length != other.m_Strings.Length)
                return false;

            for (int i = 0; i < m_Strings.Length; i++)
            {
                if (m_Strings[i] != other.m_Strings[i])
                    return false;
            }

            if (m_Ints.Length != other.m_Ints.Length)
                return false;

            for (int i = 0; i < m_Ints.Length; i++)
            {
                if (m_Ints[i] != other.m_Ints[i])
                    return false;
            }

            return true;
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
            int myHash = m_Strings.Length ^ m_Ints.Length; // Different array lengths make them not Equal, fold them in
                         

            for (int i = 0; i < m_Strings.Length; i++)
            {
                if (m_Strings[i] != null) myHash ^= m_Strings[i].GetHashCode(); // Fold in hash code for each string in the array
            }

            for (int i=0; i < m_Ints.Length; i++)
            {
                myHash ^= m_Ints[i]; // Fold in each int (as a hash code for itself) in the array
            }

            return myHash;
        }

        #endregion
    }

    internal class ThreadInfo : ICachedPacket, IEquatable<ThreadInfo>
    {
        private static readonly Dictionary<int, ThreadInfo> m_Threads = new Dictionary<int, ThreadInfo>();

        public static ThreadInfo AddOrGet(int threadId)
        {
            ThreadInfo threadInfo;
            if (m_Threads.TryGetValue(threadId, out threadInfo))
                return threadInfo;
            else
            {
                threadInfo = new ThreadInfo(threadId);
                m_Threads.Add(threadId, threadInfo);
            }

            return threadInfo;
        }

        private Guid m_ID;
        private int m_ThreadId;
        private string m_Caption;

        internal ThreadInfo()
        {
        }

        internal ThreadInfo(int threadId)
        {
            m_ID = Guid.NewGuid();
            m_ThreadId = threadId;
            m_Caption = "Thread " + threadId;
        }

        public int ThreadId { get { return m_ThreadId; } }
        public string Caption { get { return m_Caption; } }

        #region ICachedPacket Members

        public Guid ID { get { return m_ID; } }

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
            string typeName = nameof(ThreadInfo);
            PacketDefinition definition = new PacketDefinition(typeName, 1, true);
            definition.Fields.Add("ID", FieldType.Guid);
            definition.Fields.Add("threadId", FieldType.Int32);
            definition.Fields.Add("caption", FieldType.String);
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("ID", m_ID);
            packet.SetField("threadId", m_ThreadId);
            packet.SetField("caption", m_Caption);
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("ID", out m_ID);
                    packet.GetField("threadId", out m_ThreadId);
                    packet.GetField("caption", out m_Caption);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        #endregion

        #region IEquatable<ThreadInfo> Members

        public override bool Equals(object obj)
        {
            return Equals(obj as ThreadInfo);
        }

        public bool Equals(ThreadInfo other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (m_ID != other.m_ID)
                return false;
            if (m_ThreadId != other.m_ThreadId)
                return false;
            if (m_Caption != other.m_Caption)
                return false;
            return true;
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
            int myHash = m_ThreadId; // Fold in thread ID as a hash code for itself

            myHash ^= m_ID.GetHashCode(); // Fold in hash code for GUID
            if (m_Caption != null) myHash ^= m_Caption.GetHashCode(); // Fold in hash code for string caption

            return myHash;
        }

        #endregion
    }

    internal class LogPacket : IPacket, IEquatable<LogPacket>
    {
        public static void Write(string caption, int threadId, IPacketWriter writer)
        {
            writer.Write(ThreadInfo.AddOrGet(threadId));
            writer.Write(new LogPacket(caption, threadId));
        }

        private DateTime m_TimeStamp;
        private int m_ThreadId;
        private string m_Caption;

        public LogPacket(string caption, int threadId)
        {
            m_TimeStamp = DateTime.Now; //we convert to UTC during serialization, we want local time.
            m_ThreadId = threadId;
            m_Caption = caption;
        }

        internal LogPacket()
        {
        }

        public DateTime TimeStamp { get { return m_TimeStamp; } }
        public int ThreadId { get { return m_ThreadId; } }
        public string Caption { get { return m_Caption; } }

        #region IPacket Members

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
            string typeName = nameof(LogPacket);
            PacketDefinition definition = new PacketDefinition(typeName, 1, true);
            definition.Fields.Add("timestamp", FieldType.DateTime);
            definition.Fields.Add("threadId", FieldType.Int32);
            definition.Fields.Add("caption", FieldType.String);
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("timestamp", m_TimeStamp);
            packet.SetField("threadId", m_ThreadId);
            packet.SetField("caption", m_Caption);
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("timestamp", out m_TimeStamp);
                    packet.GetField("threadId", out m_ThreadId);
                    packet.GetField("caption", out m_Caption);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        #endregion

        #region IEquatable<LogPacket> Members

        public override bool Equals(object obj)
        {
            return Equals(obj as LogPacket);
        }

        public bool Equals(LogPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (m_TimeStamp != other.m_TimeStamp)
                return false;
            if (m_ThreadId != other.m_ThreadId)
                return false;
            if (m_Caption != other.m_Caption)
                return false;
            return true;
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
            int myHash = m_ThreadId; // Fold in thread ID as a hash code for itself

            myHash ^= m_TimeStamp.GetHashCode(); // Fold in hash code for DateTime timestamp
            if (m_Caption != null) myHash ^= m_Caption.GetHashCode(); // Fold in hash code for string caption

            return myHash;
        }

        #endregion
    }

    internal class BaseObject
    {
        private int id;

        public BaseObject()
        {
        }

        public BaseObject(int id)
        {
            this.id = id;
        }

        public int Id { get { return id; } set { id = value; } }

        public override bool Equals(object obj)
        {
            BaseObject other = obj as BaseObject;
            if (other == null)
                return false;

            return other.id == id;
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
            int myHash = id; // Just use the only member variable, an int, as our hash code

            return myHash;
        }
    }

    internal class BasePacket : BaseObject, IPacket
    {
        private string text;

        public BasePacket()
        {
        }

        public BasePacket(string text, int id)
            : base(id)
        {
            this.text = text;
        }

        public string Text { get { return text; } set { text = value; } }

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
            string typeName = nameof(BasePacket);
            PacketDefinition definition = new PacketDefinition(typeName, 1, true);
            definition.Fields.Add("ID", FieldType.Int32);
            definition.Fields.Add("text", FieldType.String);
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("ID", Id);
            packet.SetField("text", text);
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    int rawId;
                    packet.GetField("ID", out rawId);
                    Id = rawId;

                    packet.GetField("text", out text);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        public override bool Equals(object obj)
        {
            BasePacket other = obj as BasePacket;
            if (other == null)
                return false;

            if (!base.Equals(obj))
                return false;

            return other.text == text;
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
            int myHash = base.GetHashCode(); // Fold in hash code for inherited BaseObject

            if (text != null) myHash ^= text.GetHashCode(); // Fold in hash code for string text member

            return myHash;
        }
    }

    internal class IntermediatePacket : BasePacket
    {
        public IntermediatePacket()
        {
        }

        public IntermediatePacket(string text, int id)
            : base(text, id)
        {
        }

        public override bool Equals(object obj)
        {
            IntermediatePacket other = obj as IntermediatePacket;
            if (other == null)
                return false;

            return base.Equals(obj);
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
            int myHash = base.GetHashCode(); // Fold in hash code for inherited BasePacket

            // No member variables at this level, so just use the base hash code

            return myHash;
        }
    }

    internal class DerivedPacket : IntermediatePacket, IPacket
    {
        private string text;

        public DerivedPacket()
        {
        }

        public DerivedPacket(string text, int id)
            : base("(" + text + ")", id)
        {
            this.text = text;
        }

        public string Text2 { get { return text; } set { text = value; } }

        public override bool Equals(object obj)
        {
            DerivedPacket other = obj as DerivedPacket;
            if (other == null)
                return false;

            if (!base.Equals(obj))
                return false;

            return other.text == text;
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
            int myHash = base.GetHashCode(); // Fold in hash code for inherited IntermediatePacket

            if (text != null) myHash ^= text.GetHashCode(); // Fold in hash code for our string text member at this level

            return myHash;
        }

        #region IPacket Members

        IPacket[] IPacket.GetRequiredPackets()
        {
            return null;
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            string typeName = nameof(DerivedPacket);
            PacketDefinition definition = new PacketDefinition(typeName, 2, true);
            definition.Fields.Add("text", FieldType.String);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("text", text);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 2:
                    packet.GetField("text", out text);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        #endregion
    }

    internal class WrapperPacket : DerivedPacket
    {
        public WrapperPacket()
        {
        }

        public WrapperPacket(string text, int id)
            : base(text, id)
        {
        }

        public override bool Equals(object obj)
        {
            WrapperPacket other = obj as WrapperPacket;
            if (other == null)
                return false;

            return base.Equals(obj);
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
            int myHash = base.GetHashCode(); // Fold in hash code for inherited DerivedPacket

            // No member variables at this level, so just use the base hash code

            return myHash;
        }
    }

    // This is modeled on BasePacket (may thus be redundant, but used for clarity)
    internal class SubPacket : BaseObject, IPacket, IEquatable<SubPacket>
    {
        private string text;

        public SubPacket()
        {
        }

        public SubPacket(string text, int id)
            : base(id)
        {
            this.text = text;
        }

        public string Text { get { return text; } set { text = value; } }

        #region IPacket Members

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
            string typeName = nameof(SubPacket);
            PacketDefinition definition = new PacketDefinition(typeName, 1, true);
            definition.Fields.Add("text", FieldType.String);
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("text", text);
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("text", out text);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        #endregion

        public override bool Equals(object obj)
        {
            SubPacket other = obj as SubPacket;
            if (other == null)
                return false;

            if (!base.Equals(obj))
                return false;

            return other.text == text;
        }

        public bool Equals(SubPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (!base.Equals(other))
                return false;
            if (text != other.text)
                return false;
            return true;
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
            int myHash = base.GetHashCode(); // Fold in hash code for inherited BaseObject

            if (text != null) myHash ^= text.GetHashCode(); // Fold in hash code for string text member

            return myHash;
        }
    }

    // This is modeled on LogPacket, but modified to contain a subpacket of SubPacket
    internal class RootPacket : IPacket, IEquatable<RootPacket>
    {
        private DateTime m_TimeStamp;
        private int m_ThreadId;
        private string m_Caption;
        private SubPacket m_SubPacket;

        public RootPacket(string caption)
        {
            m_TimeStamp = DateTime.Now; //we convert to UTC during serialization, we want local time.
            m_ThreadId = Thread.CurrentThread.ManagedThreadId;
            m_Caption = caption;
            m_SubPacket = new SubPacket(caption, m_ThreadId);
        }

        public RootPacket()
        {
        }

        public DateTime TimeStamp { get { return m_TimeStamp; } }
        public int ThreadId { get { return m_ThreadId; } }
        public string Caption { get { return m_Caption; } }
        public SubPacket SubPacket { get { return m_SubPacket; } }

        #region IPacket Members

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
            string typeName = nameof(RootPacket);
            PacketDefinition definition = new PacketDefinition(typeName, 1, true);
            definition.Fields.Add("timestamp", FieldType.DateTime);
            definition.Fields.Add("threadId", FieldType.Int32);
            definition.Fields.Add("caption", FieldType.String);
            definition.SubPackets.Add(((IPacket)m_SubPacket).GetPacketDefinition());
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("timestamp", m_TimeStamp);
            packet.SetField("threadId", m_ThreadId);
            packet.SetField("caption", m_Caption);
            ((IPacket)m_SubPacket).WriteFields(definition, packet);
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("timestamp", out m_TimeStamp);
                    packet.GetField("threadId", out m_ThreadId);
                    packet.GetField("caption", out m_Caption);
                    m_SubPacket = new SubPacket(); // Need a valid but empty SubPacket to read into
                    ((IPacket)m_SubPacket).ReadFields(definition.SubPackets[0], packet);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        #endregion

        #region IEquatable<RootPacket> Members

        public override bool Equals(object obj)
        {
            return Equals(obj as RootPacket);
        }

        public bool Equals(RootPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (m_TimeStamp != other.m_TimeStamp)
                return false;
            if (m_ThreadId != other.m_ThreadId)
                return false;
            if (m_Caption != other.m_Caption)
                return false;
            if (!m_SubPacket.Equals(other.m_SubPacket))
                return false;
            return true;
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
            int myHash = m_ThreadId; // Fold in thread ID as a hash code for itself

            myHash ^= m_TimeStamp.GetHashCode(); // Fold in hash code for DateTime timestamp
            if (m_Caption != null) myHash ^= m_Caption.GetHashCode(); // Fold in hash code for string caption
            if (m_SubPacket != null) myHash ^= m_SubPacket.GetHashCode(); // Fold in hash code for subpacket member

            return myHash;
        }

        #endregion
    }

    // This is modeled on RootPacket (thus may be redundant but used for clarity),
    // intended to be used as a subpacket containing a SubPacket of its own.
    internal class InnerPacket : IPacket, IEquatable<InnerPacket>
    {
        private DateTime m_TimeStamp;
        private int m_ThreadId;
        private string m_Caption;
        private SubPacket m_SubPacket;

        public InnerPacket(string caption)
        {
            m_TimeStamp = DateTime.Now; //we convert to UTC during serialization, we want local time.
            m_ThreadId = Thread.CurrentThread.ManagedThreadId;
            m_Caption = caption;
            m_SubPacket = new SubPacket(caption, m_ThreadId); // use same caption for both by default
        }

        public InnerPacket(string innerCaption, string subCaption)
        {
            m_TimeStamp = DateTime.Now; //we convert to UTC during serialization, we want local time.
            m_ThreadId = Thread.CurrentThread.ManagedThreadId;
            m_Caption = innerCaption; // set InnerPacket caption and...
            m_SubPacket = new SubPacket(subCaption, m_ThreadId); // ...SubPacket caption separately
        }

        public InnerPacket()
        {
        }

        public DateTime TimeStamp { get { return m_TimeStamp; } }
        public int ThreadId { get { return m_ThreadId; } }
        public string Caption { get { return m_Caption; } }
        public SubPacket SubPacket { get { return m_SubPacket; } }

        #region IPacket Members

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
            string typeName = nameof(InnerPacket);
            PacketDefinition definition = new PacketDefinition(typeName, 1, true);
            definition.Fields.Add("timestamp", FieldType.DateTime);
            definition.Fields.Add("threadId", FieldType.Int32);
            definition.Fields.Add("innerCaption", FieldType.String);
            definition.SubPackets.Add(((IPacket)m_SubPacket).GetPacketDefinition());
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("timestamp", m_TimeStamp);
            packet.SetField("threadId", m_ThreadId);
            packet.SetField("innerCaption", m_Caption);
            ((IPacket)m_SubPacket).WriteFields(definition, packet);
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("timestamp", out m_TimeStamp);
                    packet.GetField("threadId", out m_ThreadId);
                    packet.GetField("innerCaption", out m_Caption);
                    m_SubPacket = new SubPacket(); // Need a valid but empty SubPacket to read into
                    ((IPacket)m_SubPacket).ReadFields(definition.SubPackets[0], packet);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        #endregion

        #region IEquatable<InnerPacket> Members

        public override bool Equals(object obj)
        {
            return Equals(obj as InnerPacket);
        }

        public bool Equals(InnerPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (m_TimeStamp != other.m_TimeStamp)
                return false;
            if (m_ThreadId != other.m_ThreadId)
                return false;
            if (m_Caption != other.m_Caption)
                return false;
            if (!m_SubPacket.Equals(other.m_SubPacket))
                return false;
            return true;
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
            int myHash = m_ThreadId; // Fold in thread ID as a hash code for itself

            myHash ^= m_TimeStamp.GetHashCode(); // Fold in hash code for DateTime timestamp
            if (m_Caption != null) myHash ^= m_Caption.GetHashCode(); // Fold in hash code for string caption
            if (m_SubPacket != null) myHash ^= m_SubPacket.GetHashCode(); // Fold in hash code for subpacket member

            return myHash;
        }

        #endregion
    }

    // This is modeled on RootPacket, but modified to contain multiple subpackets
    // of both SubPacket and InnerPacket (which then also contains its own SubPacket)
    internal class OuterPacket : IPacket, IEquatable<OuterPacket>
    {
        private DateTime m_TimeStamp;
        private int m_ThreadId;
        private string m_Caption;
        private SubPacket m_SubPacket;
        private InnerPacket m_InnerPacket;

        public OuterPacket(string caption)
        {
            m_TimeStamp = DateTime.Now; //we convert to UTC during serialization, we want local time.
            m_ThreadId = Thread.CurrentThread.ManagedThreadId;
            m_Caption = caption;
            m_SubPacket = new SubPacket(caption, m_ThreadId);
            m_InnerPacket = new InnerPacket(caption); // use same caption everywhere
        }

        public OuterPacket(string outerCaption, string mySubCaption, string innerCaption, string subCaption)
        {
            m_TimeStamp = DateTime.Now; //we convert to UTC during serialization, we want local time.
            m_ThreadId = Thread.CurrentThread.ManagedThreadId;
            m_Caption = outerCaption; // Set each caption separately....
            m_SubPacket = new SubPacket(mySubCaption, m_ThreadId);
            m_InnerPacket = new InnerPacket(innerCaption, subCaption);
        }

        public OuterPacket()
        {
        }

        public DateTime TimeStamp { get { return m_TimeStamp; } }
        public int ThreadId { get { return m_ThreadId; } }
        public string Caption { get { return m_Caption; } }
        public SubPacket SubPacket { get { return m_SubPacket; } }
        public InnerPacket InnerPacket { get { return m_InnerPacket; } }

        #region IPacket Members

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
            string typeName = nameof(OuterPacket);
            PacketDefinition definition = new PacketDefinition(typeName, 1, true);
            definition.Fields.Add("timestamp", FieldType.DateTime);
            definition.Fields.Add("threadId", FieldType.Int32);
            definition.Fields.Add("outerCaption", FieldType.String);
            definition.SubPackets.Add(((IPacket)m_SubPacket).GetPacketDefinition());
            definition.SubPackets.Add(((IPacket)m_InnerPacket).GetPacketDefinition());
            return definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("timestamp", m_TimeStamp);
            packet.SetField("threadId", m_ThreadId);
            packet.SetField("outerCaption", m_Caption);
            ((IPacket)m_SubPacket).WriteFields(definition, packet);
            ((IPacket)m_InnerPacket).WriteFields(definition, packet); // recursively writes its own SubPacket as well
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("timestamp", out m_TimeStamp);
                    packet.GetField("threadId", out m_ThreadId);
                    packet.GetField("outerCaption", out m_Caption);
                    m_SubPacket = new SubPacket(); // Need a valid but empty SubPacket to read into
                    ((IPacket)m_SubPacket).ReadFields(definition.SubPackets[0], packet);
                    m_InnerPacket = new InnerPacket(); // Need a valid but empty InnerPacket to read into
                    ((IPacket)m_InnerPacket).ReadFields(definition.SubPackets[1], packet); // recursively reads its own SubPacket
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        #endregion

        #region IEquatable<OuterPacket> Members

        public override bool Equals(object obj)
        {
            return Equals(obj as OuterPacket);
        }

        public bool Equals(OuterPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (m_TimeStamp != other.m_TimeStamp)
                return false;
            if (m_ThreadId != other.m_ThreadId)
                return false;
            if (m_Caption != other.m_Caption)
                return false;
            if (!m_SubPacket.Equals(other.m_SubPacket))
                return false;
            if (!m_InnerPacket.Equals(other.m_InnerPacket))
                return false;
            return true;
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
            int myHash = m_ThreadId; // Fold in thread ID as a hash code for itself

            myHash ^= m_TimeStamp.GetHashCode(); // Fold in hash code for DateTime timestamp
            if (m_Caption != null) myHash ^= m_Caption.GetHashCode(); // Fold in hash code for string caption
            if (m_SubPacket != null) myHash ^= m_SubPacket.GetHashCode(); // Fold in hash code for subpacket member SubPacket
            if (m_InnerPacket != null) myHash ^= m_InnerPacket.GetHashCode(); // Fold in hash code for subpacket member InnerPacket

            return myHash;
        }

        #endregion
    }

}