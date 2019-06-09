using System;
using Gibraltar.Serialization;

namespace Gibraltar.Messaging
{
    /// <summary>
    /// This interface is required to be a publishable packet
    /// </summary>
    public interface IMessengerPacket : IPacket
    {
        /// <summary>
        /// The unique sequence number of this packet in the session
        /// </summary>
        long Sequence { get; set; }

        /// <summary>
        /// The timestamp of this packet in the session
        /// </summary>
        DateTimeOffset Timestamp { get; set; }
    }
}