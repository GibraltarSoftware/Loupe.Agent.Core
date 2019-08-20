namespace Loupe.Serialization
{
    /// <summary>
    /// This is the key interface objects implement to be serializable by Loupe.
    /// </summary>
    /// <remarks>
    /// To properly implement IPacket the class should also provide a default constructor.
    /// This is needed to be compatible with the SimplePacketFactory implementation of IPacketFactory.
    /// </remarks>
    public interface IPacket
    {
        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] GetRequiredPackets();

        /// <summary>
        /// Get a new, populated definition for this packet.
        /// </summary>
        /// <returns>A new Packet Definition object</returns>
        /// <remarks>Once a definition is cached by the packet writer it won't be requested again.
        /// Packet Definitions must be invariant for an entire data stream.</remarks>
        PacketDefinition GetPacketDefinition();

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void WriteFields(PacketDefinition definition, SerializedPacket packet);

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void ReadFields(PacketDefinition definition, SerializedPacket packet);
    }
}