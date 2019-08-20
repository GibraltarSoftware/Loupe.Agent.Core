namespace Loupe.Serialization
{
    /// <summary>
    /// Defines the interface necessary for a packet factory to be
    /// registered with IPacketReader.
    /// </summary>
    public interface IPacketFactory
    {
        /// <summary>
        /// This is the method that is invoked on an IPacketFactory to create an IPacket
        /// from the data in an IFieldReader given a specified PacketDefinition.
        /// </summary>
        /// <param name="definition">Definition of the fields expected in the next packet</param>
        /// <param name="reader">Data stream to be read</param>
        /// <returns>An IPacket corresponding to the PacketDefinition and the stream data</returns>
        IPacket CreatePacket(PacketDefinition definition, IFieldReader reader);
    }
}