namespace Loupe.Serialization
{
    /// <summary>
    /// Implemented to support writing packets
    /// </summary>
    /// <remarks>Having everything use an interface allows us to support NMOCK</remarks>
    public interface IPacketWriter
    {
        /// <summary>
        /// Returns the current position within the stream.
        /// </summary>
        long Position { get; }

        /// <summary>
        /// Returns the length of the stream.
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Write the data needed to serialize the state of the packet
        /// </summary>
        /// <param name="packet">Object to be serialized, must implement IPacket</param>
        void Write(IPacket packet);
    }
}