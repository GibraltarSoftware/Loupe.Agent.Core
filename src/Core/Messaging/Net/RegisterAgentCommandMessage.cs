using System;
using System.IO;
using Gibraltar.Data;



namespace Gibraltar.Messaging.Net
{
    /// <summary>
    /// Sent by an agent to register itself with the remote server or desktop
    /// </summary>
    public class RegisterAgentCommandMessage : NetworkMessage
    {
        private Guid m_SessionId;

        internal RegisterAgentCommandMessage()
        {
            TypeCode = NetworkMessageTypeCode.RegisterAgentCommand;
            Version = new Version(1, 0);
        }

        /// <summary>
        /// Create a new agent registration message for the specified session Id
        /// </summary>
        /// <param name="sessionId"></param>
        public RegisterAgentCommandMessage(Guid sessionId)
            : this()
        {
            m_SessionId = sessionId;
        }

        /// <summary>
        /// The session Id identifying the agent
        /// </summary>
        public Guid SessionId { get { return m_SessionId; } }

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
