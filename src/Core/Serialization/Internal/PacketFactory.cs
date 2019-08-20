using System;
using System.Collections.Generic;
using System.Reflection;

namespace Loupe.Serialization.Internal
{
    /// <summary>
    /// This helper class is used by PacketReader to manage the list of IPacketFactory
    /// classes used to deserialize a stream of packets.
    /// </summary>
    internal class PacketFactory
    {
        private readonly Dictionary<string, IPacketFactory> m_PacketFactories;
        private readonly GenericPacketFactory m_GenericFactory;

        /// <summary>
        /// Creates an empty list of IPacketFactory objects.
        /// </summary>
        public PacketFactory()
        {
            m_PacketFactories = new Dictionary<string, IPacketFactory>();
            m_GenericFactory = new GenericPacketFactory();
        }

        /// <summary>
        /// Registers a SimplePacketFactory wrappering the specified type.
        /// </summary>
        /// <param name="type">Type must implement IPacket.</param>
        public void RegisterType(Type type)
        {
            var factory = new SimplePacketFactory(type);
            if ( factory.IsValid )
                RegisterFactory(type.Name, factory);
        }

        /// <summary>
        /// Associates the specified IPacketFactory with a type name
        /// </summary>
        /// <param name="typeName">Should refer to a type that implements IPacket</param>
        /// <param name="factory">IPacketFactory class used to </param>
        public void RegisterFactory(string typeName, IPacketFactory factory)
        {
            m_PacketFactories[typeName] = factory;
        }

        /// <summary>
        /// Registers a SimplePacketFactory for each IPacket in an assembly
        /// </summary>
        /// <param name="assembly">Assembly to be searched for IPacket implementations</param>
        public void RegisterAssembly(Assembly assembly)
        {
            Type[] types = assembly.GetTypes();
            Type targetType = typeof(IPacket);
            foreach (Type type in types)
            {
                if (targetType.IsAssignableFrom(type))
                    RegisterType(type);
            }
        }

        public IPacketFactory GetPacketFactory(string typeName)
        {
            if (m_PacketFactories.TryGetValue(typeName, out var factory))
                return factory;

            return m_GenericFactory;
        }
    }
}