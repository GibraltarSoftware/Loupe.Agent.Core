using System;
using System.IO;
using Gibraltar.Data;
using Loupe.Extensibility.Data;


namespace Gibraltar.Messaging.Net
{
    /// <summary>
    /// A command to have the agent send sessions to the server immediately
    /// </summary>
    public class SendSessionCommandMessage : NetworkMessage
    {
        private Guid m_SessionId;
        private SessionCriteria m_Criteria;

        internal SendSessionCommandMessage()
        {
            TypeCode = NetworkMessageTypeCode.SendSession;
            Version = new Version(1, 0);
        }

        /// <summary>
        /// Create a new send session command for the specified session id and criteria
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="criteria"></param>
        public SendSessionCommandMessage(Guid sessionId, SessionCriteria criteria)
            : this()
        {
            SessionId = sessionId;
            Criteria = criteria;
        }

        /// <summary>
        /// The session Id to send
        /// </summary>
        public Guid SessionId { get { return m_SessionId; } set { m_SessionId = value; } }

        /// <summary>
        /// The criteria to use to send the session
        /// </summary>
        public SessionCriteria Criteria { get { return m_Criteria; } set { m_Criteria = value; } }

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected override void OnWrite(Stream stream)
        {
            BinarySerializer.SerializeValue(stream, m_SessionId);
            BinarySerializer.SerializeValue(stream, (int)m_Criteria);
        }

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected override void OnRead(Stream stream)
        {
            BinarySerializer.DeserializeValue(stream, out m_SessionId);

            BinarySerializer.DeserializeValue(stream, out int rawCriteria);
            m_Criteria = (SessionCriteria)rawCriteria;
        }
    }
}
