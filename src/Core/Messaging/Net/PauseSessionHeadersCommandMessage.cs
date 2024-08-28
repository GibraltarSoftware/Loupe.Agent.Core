using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gibraltar.Messaging.Net
{
    /// <summary>
    /// Suspend sending session headers to the client until told to resume
    /// </summary>
    public class PauseSessionHeadersCommandMessage : NetworkMessage
    {
        /// <summary>
        /// create a new session headers command message
        /// </summary>
        public PauseSessionHeadersCommandMessage()
        {
            TypeCode = NetworkMessageTypeCode.PauseSessionHeaders;
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
