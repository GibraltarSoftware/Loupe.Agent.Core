using System;
using System.IO;

namespace Loupe.Core.IO.Messaging
{
    /// <summary>
    /// Indicates the live view session for the specified session Id be terminated
    /// </summary>
    public class LiveViewStopCommandMessage : NetworkMessage
    {
        private Guid m_ChannelId;
        private Guid m_SessionId;

        internal LiveViewStopCommandMessage()
        {
            TypeCode = NetworkMessageTypeCode.LiveViewStopCommand;
            Version = new Version(1, 0);
        }

        /// <summary>
        /// Create a command to stop the specified live view channel
        /// </summary>
        public LiveViewStopCommandMessage(Guid channelId, Guid sessionId)
            : this()
        {
            ChannelId = channelId;
            SessionId = sessionId;
        }

        /// <summary>
        /// The channel Id of the viewer
        /// </summary>
        public Guid ChannelId { get { return m_ChannelId; } set { m_ChannelId = value; } }

        /// <summary>
        /// The session Id that is being viewed
        /// </summary>
        public Guid SessionId { get { return m_SessionId; } set { m_SessionId = value; } }

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected override void OnWrite(Stream stream)
        {
            BinarySerializer.SerializeValue(stream, m_ChannelId);
            BinarySerializer.SerializeValue(stream, m_SessionId);
        }

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected override void OnRead(Stream stream)
        {
            BinarySerializer.DeserializeValue(stream, out m_ChannelId);
            BinarySerializer.DeserializeValue(stream, out m_SessionId);
        }
    }
}
