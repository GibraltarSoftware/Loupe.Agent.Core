using System;
using Loupe.Core.Serialization;
using Loupe.Extensibility.Data;
#pragma warning disable 1591

namespace Loupe.Core.Monitor.Serialization
{
    public class ExceptionInfoPacket : GibraltarPacket, IPacket, IEquatable<ExceptionInfoPacket>, IExceptionInfo
    {
        private string m_TypeName;
        private string m_Message;
        private string m_Source;
        private string m_StackTrace;
        private IExceptionInfo m_InnerException; //not serialized as such.

        public ExceptionInfoPacket()
        { }

        public ExceptionInfoPacket(Exception exception)
        {
            m_TypeName = exception.GetType().FullName;
            m_Message = exception.Message;
            m_Source = exception.Source;
            m_StackTrace = exception.StackTrace;
        }

        public string TypeName { get { return m_TypeName; } set { m_TypeName = value; } }

        public string Message { get { return m_Message; } set { m_Message = value; } }

        public string Source { get { return m_Source; } set { m_Source = value; } }

        public string StackTrace { get { return m_StackTrace; } set { m_StackTrace = value; } }

        public IExceptionInfo InnerException { get { return m_InnerException; } set { m_InnerException = value; } }

        /// <summary>
        /// Lock in data, optimizing it for storage and memory representation.
        /// </summary>
        /// <remarks>Should be called after the client has configured the data in the packet and on a background thread
        /// to avoid blocking the client.  This does not set the packet as read only, it can still be changed.</remarks>
        public override void FixData()
        {
            base.FixData();

            // Swap all strings for their StringReference equivalent
            StringReference.SwapReference(ref m_TypeName);
            StringReference.SwapReference(ref m_Message);
            StringReference.SwapReference(ref m_Source);
            StringReference.SwapReference(ref m_StackTrace);
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
            return Equals(other as ExceptionInfoPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ExceptionInfoPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((TypeName == other.TypeName)
                && (Message == other.Message)
                && (Source == other.Source)
                && (StackTrace == other.StackTrace)
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

            if (m_TypeName != null) myHash ^= m_TypeName.GetHashCode(); // Fold in hash code for string TypeName
            if (m_Message != null) myHash ^= m_Message.GetHashCode(); // Fold in hash code for string Message
            if (m_Source != null) myHash ^= m_Source.GetHashCode(); // Fold in hash code for string Source
            if (m_StackTrace != null) myHash ^= m_StackTrace.GetHashCode(); // Fold in hash code for string StackTrace

            return myHash;
        }

        #region IPacket Members

        private const int SerializationVersion = 1;

        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(ExceptionInfoPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("TypeName", FieldType.String);
            definition.Fields.Add("Message", FieldType.String);
            definition.Fields.Add("Source", FieldType.String);
            definition.Fields.Add("StackTrace", FieldType.String);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("TypeName", TypeName);
            packet.SetField("Message", Message);
            packet.SetField("Source", Source);
            packet.SetField("StackTrace", StackTrace);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("TypeName", out m_TypeName);
                    packet.GetField("Message", out m_Message);
                    packet.GetField("Source", out m_Source);
                    packet.GetField("StackTrace", out m_StackTrace);
                    break;
            }
        }

        IPacket[] IPacket.GetRequiredPackets()
        {
            // we depend on nothing
            return null;
        }

        #endregion

    }
}
