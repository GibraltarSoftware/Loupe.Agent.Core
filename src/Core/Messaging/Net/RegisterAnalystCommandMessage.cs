using System;
using System.IO;
using Loupe.Data;



namespace Loupe.Messaging.Net
{
    /// <summary>
    /// Sent by a Desktop to register itself with the remote server
    /// </summary>
    public class RegisterAnalystCommandMessage : NetworkMessage
    {
        private string m_UserName;
        private Guid m_RepositoryId;

        internal RegisterAnalystCommandMessage()
        {
            TypeCode = NetworkMessageTypeCode.RegisterAnalystCommand;
            Version = new Version(1, 0);
        }

        /// <summary>
        /// Create a new registration for the specified client repository id and user name
        /// </summary>
        /// <param name="repositoryId"></param>
        /// <param name="userName"></param>
        public RegisterAnalystCommandMessage(Guid repositoryId, string userName)
            : this()
        {
            m_RepositoryId = repositoryId;
            m_UserName = userName;
        }

        /// <summary>
        /// The user running Analyst
        /// </summary>
        public string UserName { get { return m_UserName; } }

        /// <summary>
        /// The unique client repository id of the Analyst
        /// </summary>
        public Guid RepositoryId { get { return m_RepositoryId; } }

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected override void OnWrite(Stream stream)
        {
            BinarySerializer.SerializeValue(stream, m_RepositoryId);
            BinarySerializer.SerializeValue(stream, m_UserName);
        }

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected override void OnRead(Stream stream)
        {
            BinarySerializer.DeserializeValue(stream, out m_RepositoryId);
            BinarySerializer.DeserializeValue(stream, out m_UserName);
        }
    }
}
