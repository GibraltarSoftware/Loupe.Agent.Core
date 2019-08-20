using System;



namespace Loupe.Core.Serialization
{
    /// <summary>Implemented on invariant packets that can be cached</summary>
    /// <remarks>
    /// This interface extends IPacket to handle packets that are referenced
    /// by multiple packets and should only be serialized once.
    /// </remarks>
    public interface ICachedPacket : IPacket
    {
        /// <summary>
        /// The unique Id of the packet
        /// </summary>
        Guid ID { get; }
    }
}