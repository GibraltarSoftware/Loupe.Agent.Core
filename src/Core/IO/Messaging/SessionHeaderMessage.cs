using System;
using System.IO;
using Loupe.Core.Data;

namespace Loupe.Core.IO.Messaging
{
    /// <summary>
    /// Contains the session summary information for a remote session
    /// </summary>
    public class SessionHeaderMessage : NetworkMessage
    {
        private SessionHeader m_SessionHeader;
        private DateTimeOffset m_Timestamp;
        private TimeSpan m_ClockDrift;

        internal SessionHeaderMessage()
        {
            TypeCode = NetworkMessageTypeCode.SessionHeader;
            Version = new Version(1, 0);
        }

        /// <summary>
        /// Create a new session header message without any clock drift
        /// </summary>
        /// <param name="sessionHeader"></param>
        public SessionHeaderMessage(SessionHeader sessionHeader)
            :this(sessionHeader, TimeSpan.Zero)
        {            
        }

        /// <summary>
        /// Create a new session header message with the specified clock drift
        /// </summary>
        /// <param name="sessionHeader"></param>
        /// <param name="clockDrift"></param>
        public SessionHeaderMessage(SessionHeader sessionHeader,  TimeSpan clockDrift)
            :this()
        {
            m_SessionHeader = sessionHeader;
            m_ClockDrift = clockDrift;
            m_Timestamp = DateTimeOffset.Now;
        }

        /// <summary>
        /// The current session header
        /// </summary>
        public SessionHeader SessionHeader { get { return m_SessionHeader; } }

        /// <summary>
        /// The timestamp of when the message was generated
        /// </summary>
        public DateTimeOffset Timestamp { get { return m_Timestamp; } }

        /// <summary>
        /// The total amount of 
        /// </summary>
        public TimeSpan ClockDrift { get { return m_ClockDrift; } }

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected override void OnRead(Stream stream)
        {
            //we need to calculate the length of the header or this constructor fails.
            long initialPosition = stream.Position;
            BinarySerializer.DeserializeValue(stream, out m_Timestamp);
            BinarySerializer.DeserializeValue(stream, out m_ClockDrift);

            int length = Length - BaseLength - (int)(stream.Position - initialPosition); //we have to offset for the number of bytes we just read.
            m_SessionHeader = new SessionHeader(stream, length);
        }

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected override void OnWrite(Stream stream)
        {
            //this will automatically figure out its length...
            BinarySerializer.SerializeValue(stream, m_Timestamp);
            BinarySerializer.SerializeValue(stream, m_ClockDrift);
            byte[] rawData = m_SessionHeader.RawData();
            stream.Write(rawData, 0, rawData.Length);
        }
    }
}
