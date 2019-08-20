using System;
using System.IO;
using Loupe.Core.Data;



#pragma warning disable 1591

namespace Loupe.Core.Messaging.Net
{
    /// <summary>
    /// Indicates that the identified session has been closed.
    /// </summary>
    public class SessionClosedMessage : NetworkMessage
    {
        private Guid m_SessionId;

        internal SessionClosedMessage()
        {
            TypeCode = NetworkMessageTypeCode.SessionClosed;
            Version = new Version(1, 0);
        }

        /// <summary>
        /// Create a new session closed message for the specified session id
        /// </summary>
        /// <param name="sessionId"></param>
        public SessionClosedMessage(Guid sessionId)
            : this()
        {
            SessionId = sessionId;
        }

        public Guid SessionId { get { return m_SessionId; } private set { m_SessionId = value; } }

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected override void OnWrite(Stream stream)
        {
            BinarySerializer.SerializeValue(stream, m_SessionId);
        }

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected override void OnRead(Stream stream)
        {
            BinarySerializer.DeserializeValue(stream, out m_SessionId);
        }
    }
}
