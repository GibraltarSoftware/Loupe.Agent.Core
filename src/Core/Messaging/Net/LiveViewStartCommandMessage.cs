using System;
using System.Diagnostics;
using System.IO;
using Gibraltar.Data;



namespace Gibraltar.Messaging.Net
{
    /// <summary>
    /// Requests a new live view stream
    /// </summary>
    [DebuggerDisplay("Session: {SessionId}, Offset: {SequenceOffset}")]
    public class LiveViewStartCommandMessage : NetworkMessage
    {
        private readonly object m_Lock = new object();

        private Guid m_RepositoryId;
        private long m_SequenceOffset;
        private Guid m_SessionId;
        private Guid m_ChannelId;

        internal LiveViewStartCommandMessage()
        {
            lock(m_Lock)
            {
                TypeCode = NetworkMessageTypeCode.LiveViewStartCommand;
                Version = new Version(1, 0);
            }
        }

        /// <summary>
        /// Create a new message with the specified session id and optionally sequence offset
        /// </summary>
        /// <param name="repositoryId">The unique Id of the client for all related activities</param>
        /// <param name="channelId">A unique id for this request to identify a conversation pair</param>
        /// <param name="sessionId">The session that is being requested to live view</param>
        /// <param name="sequenceOffset">The packet index to start at</param>
        public LiveViewStartCommandMessage(Guid repositoryId, Guid sessionId, Guid channelId, long sequenceOffset = 0)
            : this()
        {
            lock(m_Lock)
            {
                m_RepositoryId = repositoryId;
                m_SequenceOffset = sequenceOffset;
                m_SessionId = sessionId;
                m_ChannelId = channelId;
                Validate();
            }
        }

        /// <summary>
        /// A unique id for this request to identify a conversation pair
        /// </summary>
        public Guid ChannelId
        {
            get
            {
                lock(m_Lock)
                {
                    return m_ChannelId;
                }
            }
        }

        /// <summary>
        /// The last sequence number that was received previously to enable restart at the right point in the stream
        /// </summary>
        public long SequenceOffset
        {
            get
            {
                lock(m_Lock)
                {
                    return m_SequenceOffset;
                }
            }
        }

        /// <summary>
        /// The Id of the session to be viewed
        /// </summary>
        public Guid SessionId
        {
            get
            {
                lock(m_Lock)
                {
                    return m_SessionId;
                }
            }
        }

        /// <summary>
        /// The unique Id of the client for all related activities
        /// </summary>
        public Guid RepositoryId
        {
            get
            {
                lock(m_Lock)
                {
                    return m_RepositoryId;
                }
            }
        }

        /// <summary>
        /// Verify the command is fully populated and 
        /// </summary>
        public void Validate()
        {
            lock(m_Lock)
            {
                if (ChannelId == Guid.Empty)
                    throw new InvalidOperationException("There is no channel Id specified");
                if (SessionId == Guid.Empty)
                    throw new InvalidOperationException("There is no session Id specified");
            }

            //Repository Id is optional
        }

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected override void OnWrite(Stream stream)
        {
            lock(m_Lock)
            {
                BinarySerializer.SerializeValue(stream, m_RepositoryId);
                BinarySerializer.SerializeValue(stream, m_SessionId);
                BinarySerializer.SerializeValue(stream, m_ChannelId);
                BinarySerializer.SerializeValue(stream, m_SequenceOffset);
            }
        }

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected override void OnRead(Stream stream)
        {
            lock(m_Lock)
            {
                BinarySerializer.DeserializeValue(stream, out m_RepositoryId);
                BinarySerializer.DeserializeValue(stream, out m_SessionId);
                BinarySerializer.DeserializeValue(stream, out m_ChannelId);
                BinarySerializer.DeserializeValue(stream, out m_SequenceOffset);
            }
        }
    }
}
