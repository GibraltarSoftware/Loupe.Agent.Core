using System;
using Gibraltar.Serialization;

namespace Gibraltar.Messaging
{
    /// <summary>
    /// This interface is required to be a publishable packet
    /// </summary>
    internal interface IMessengerPacket : IPacket
    {
        long Sequence { get; set; }
        DateTimeOffset Timestamp { get; set; }
    }
}