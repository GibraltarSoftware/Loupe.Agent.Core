namespace Loupe.Serialization.Internal
{
    /// <summary>
    /// Helper class used by PacketFactory to wrapper the creation of GenericPacket
    /// </summary>
    internal class GenericPacketFactory : IPacketFactory
    {
        public IPacket CreatePacket(PacketDefinition definition, IFieldReader reader)
        {
            GenericPacket packet = new GenericPacket(definition, reader);
            return packet;
        }
    }
}