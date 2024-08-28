using System;
using System.IO;

namespace Gibraltar.Messaging.Net
{
    /// <summary>
    /// Resume sending session headers to the client
    /// </summary>
    public class ResumeSessionHeadersCommandMessage : NetworkMessage
    {
        /// <summary>
        /// create a new session headers command message
        /// </summary>
        public ResumeSessionHeadersCommandMessage()
        {
            TypeCode = NetworkMessageTypeCode.ResumeSessionHeaders;
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
