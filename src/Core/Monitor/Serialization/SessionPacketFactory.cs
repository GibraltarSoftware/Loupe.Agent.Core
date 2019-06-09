using System;
using Gibraltar.Serialization;
#pragma warning disable 1591

namespace Gibraltar.Monitor.Serialization
{
    public class SessionPacketFactory : IPacketFactory
    {
        private readonly string m_SessionStartInfoPacketType;
        private readonly string m_SessionEndInfoPacketType;
        private readonly string m_SessionFilePacketType;
        private readonly string m_ThreadInfoPacketType;

        public SessionPacketFactory()
        {
            //resolve the names of all the types we want to be able to get packets for
            //this lets us do a faster switch in CreatePacket
            m_SessionStartInfoPacketType = typeof(SessionSummaryPacket).Name;
            m_SessionEndInfoPacketType = typeof(SessionClosePacket).Name;
            m_SessionFilePacketType = typeof(SessionFragmentPacket).Name;
            m_ThreadInfoPacketType = typeof(ThreadInfoPacket).Name;
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
            if (definition.TypeName == m_ThreadInfoPacketType)
            {
                packet = new ThreadInfoPacket();
            }
            else if (definition.TypeName == m_SessionStartInfoPacketType)
            {
                packet = new SessionSummaryPacket();
            }
            else if (definition.TypeName == m_SessionEndInfoPacketType)
            {
                packet = new SessionClosePacket();
            }
            else if (definition.TypeName == m_SessionFilePacketType)
            {
                packet = new SessionFragmentPacket();
            }
            else
            {
                //crap, we don't know what to do here.
                throw new ArgumentOutOfRangeException(nameof(definition), definition.TypeName, "This packet factory doesn't understand how to create packets for the provided type.");
            }

            //this feels a little crazy, but you have to do your own read call here - we aren't just creating the packet
            //object, we actually have to make the standard call to have it read data... 
            definition.ReadFields(packet, reader);

            return packet;
        }

        /// <summary>
        /// Register the packet factory with the packet reader for all packet types it supports
        /// </summary>
        /// <param name="packetReader"></param>
        public void Register(IPacketReader packetReader)
        {
            packetReader.RegisterFactory(m_SessionStartInfoPacketType, this);
            packetReader.RegisterFactory(m_SessionEndInfoPacketType, this);
            packetReader.RegisterFactory(m_SessionFilePacketType, this);
            packetReader.RegisterFactory(m_ThreadInfoPacketType, this);
        }
    }
}
