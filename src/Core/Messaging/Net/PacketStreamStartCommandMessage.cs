using System;
using System.IO;



namespace Loupe.Messaging.Net
{
    /// <summary>
    /// Informs the receiver to start a new packet serializer for the subsequent data.
    /// </summary>
    public class PacketStreamStartCommandMessage : NetworkMessage
    {
        /// <summary>
        /// Create a new packet stream start message
        /// </summary>
        public PacketStreamStartCommandMessage()
        {
            TypeCode = NetworkMessageTypeCode.PacketStreamStartCommand;
            Version = new Version(1, 0);
        }

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected override void OnWrite(Stream stream)
        {
        }

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected override void OnRead(Stream stream)
        {
        }
    }
}
