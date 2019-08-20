using System;
using Loupe.Serialization;
#pragma warning disable 1591

namespace Loupe.Monitor.Serialization
{
    public class LogMessagePacketFactory : IPacketFactory
    {
        private readonly ISessionPacketCache m_SessionPacketCache;
        private readonly string m_LogMessagePacketType;
        private readonly string m_ExceptionInfoPacketType;
        private readonly string m_ApplicationUserPacketType;
        private PacketDefinition m_CachedLogMessagePacketDefinition;
        private bool m_UseFastDeserialization;

        public LogMessagePacketFactory(ISessionPacketCache session)
        {
            m_SessionPacketCache = session;

            //resolve the names of all the types we want to be able to get packets for
            //this lets us do a faster switch in CreatePacket
            m_LogMessagePacketType = typeof(LogMessagePacket).Name;
            m_ExceptionInfoPacketType = typeof(ExceptionInfoPacket).Name;
            m_ApplicationUserPacketType = typeof(ApplicationUserPacket).Name;
        }

        /// <summary>
        /// This is the method that is invoked on an IPacketFactory to create an IPacket
        /// from the data in an IFieldReader given a specified PacketDefinition.
        /// </summary>
        /// <param name="definition">Definition of the fields expected in the next packet</param>
        /// <param name="reader">Data stream to be read</param>
        /// <returns>An IPacket corresponding to the PacketDefinition and the stream data</returns>
        public IPacket CreatePacket(PacketDefinition definition, IFieldReader reader)
        {
            IPacket packet;

            //what we create varies by what specific definition they're looking for
            if (definition.TypeName == m_LogMessagePacketType)
            {
                var logPacket = new LogMessagePacket(m_SessionPacketCache);
                packet = logPacket;
                
                if (!ReferenceEquals(definition, m_CachedLogMessagePacketDefinition))
                {
                    var currentDefinition = PacketDefinition.CreatePacketDefinition(packet);
                    m_UseFastDeserialization = definition.Equals(currentDefinition);
                    m_CachedLogMessagePacketDefinition = definition;
                }

                if (m_UseFastDeserialization)
                    logPacket.ReadFieldsFast(reader);
                else
                    definition.ReadFields(packet, reader);
            }
            else if (definition.TypeName == m_ExceptionInfoPacketType)
            {
                packet = new ExceptionInfoPacket();
                definition.ReadFields(packet, reader);
            }
            else if (definition.TypeName == m_ApplicationUserPacketType)
            {
                packet = new ApplicationUserPacket();
                definition.ReadFields(packet, reader);
            }
            else
            {
                //crap, we don't know what to do here.
                throw new ArgumentOutOfRangeException(nameof(definition), definition.TypeName, "This packet factory doesn't understand how to create packets for the provided type.");
            }

            return packet;
        }

        /// <summary>
        /// Register the packet factory with the packet reader for all packet types it supports
        /// </summary>
        /// <param name="packetReader"></param>
        public void Register(IPacketReader packetReader)
        {
            packetReader.RegisterFactory(m_LogMessagePacketType, this);
            packetReader.RegisterFactory(m_ExceptionInfoPacketType, this);
            packetReader.RegisterFactory(m_ApplicationUserPacketType, this);
        }
    }
}
