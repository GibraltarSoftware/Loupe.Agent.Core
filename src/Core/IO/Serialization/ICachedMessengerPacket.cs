
using Loupe.Core.Serialization;

namespace Loupe.Core.IO.Serialization
{
    /// <summary>
    /// This interface is required to be a publishable cached packet
    /// </summary>
    public interface ICachedMessengerPacket : ICachedPacket, IMessengerPacket
    {
        //we're primarily here really as a way of associating a messenger packet with ICachedPacket in one blow.

        /// <summary>
        /// Indicates if this packet is part of the session header and should be presented with other header packets.
        /// </summary>
        bool IsHeader { get; }
    }
}