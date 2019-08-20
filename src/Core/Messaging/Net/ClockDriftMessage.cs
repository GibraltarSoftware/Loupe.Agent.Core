using System;
using System.IO;
using Loupe.Core.Data;

namespace Loupe.Core.Messaging.Net
{
    /// <summary>
    /// Used to determine the latency and relative clock drift of a connection
    /// </summary>
    public class ClockDriftMessage : NetworkMessage
    {
        private Guid m_Id;
        private DateTimeOffset? m_OriginatorTimestamp;
        private DateTimeOffset? m_DestinationTimestamp;
        private DateTimeOffset m_DeserializationTimestamp;

        private TimeSpan m_ClockDrift;
        private TimeSpan m_Latency;

        internal ClockDriftMessage()
        {
            TypeCode = NetworkMessageTypeCode.ClockDrift;
            Version = new Version(1, 0);
        }

        /// <summary>
        /// Create a new clock drift message for the specified agent
        /// </summary>
        /// <param name="id"></param>
        public ClockDriftMessage(Guid id)
            :this()
        {
            m_Id = id;
        }

        /// <summary>
        /// The session Id of the endpoint we're identifying clock drift for
        /// </summary>
        public Guid Id { get { return m_Id; } set { m_Id = value; } }

        /// <summary>
        /// The timestamp the original request was created on the source end
        /// </summary>
        public DateTimeOffset? OriginatorTimestamp { get { return m_OriginatorTimestamp; } set { m_OriginatorTimestamp = value; } }

        /// <summary>
        /// The timestamp of the destination when it received the message
        /// </summary>
        public DateTimeOffset? DestinationTimestamp { get { return m_DestinationTimestamp; } set { m_DestinationTimestamp = value; } }

        /// <summary>
        /// The clock drift between the agent and the server, discounting latency
        /// </summary>
        public TimeSpan ClockDrift { get { return m_ClockDrift; } }

        /// <summary>
        /// The estimated latency in the connection (used to calculate true clock drift)
        /// </summary>
        public TimeSpan Latency { get { return m_Latency; } }

        /// <summary>
        /// Locks in the latency and drift calculations when called by the originator after a round trip.
        /// </summary>
        public void CalculateValues()
        {
            long latencyTicks = (m_DeserializationTimestamp - m_OriginatorTimestamp.Value).Ticks;
            if (latencyTicks < 0)
            {
                latencyTicks = 0;
            }

            m_Latency = new TimeSpan(latencyTicks / 2);

            if (m_DestinationTimestamp.HasValue)
                m_ClockDrift = (m_OriginatorTimestamp.Value - m_DestinationTimestamp.Value) - m_Latency - m_Latency; //account for coming & going latency
        }

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected override void OnWrite(Stream stream)
        {
            if (OriginatorTimestamp == null)
                OriginatorTimestamp = DateTimeOffset.Now;

            BinarySerializer.SerializeValue(stream, Id);
            BinarySerializer.SerializeValue(stream, OriginatorTimestamp.Value);
            BinarySerializer.SerializeValue(stream, DestinationTimestamp ?? DateTimeOffset.MinValue);
        }

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected override void OnRead(Stream stream)
        {
            m_DeserializationTimestamp = DateTimeOffset.Now;

            BinarySerializer.DeserializeValue(stream, out m_Id);

            BinarySerializer.DeserializeValue(stream, out DateTimeOffset rawValue);
            if (rawValue != DateTimeOffset.MinValue)
                m_OriginatorTimestamp = rawValue;

            BinarySerializer.DeserializeValue(stream, out rawValue);
            if (rawValue != DateTimeOffset.MinValue)
                m_DestinationTimestamp = rawValue;

        }
    }
}
