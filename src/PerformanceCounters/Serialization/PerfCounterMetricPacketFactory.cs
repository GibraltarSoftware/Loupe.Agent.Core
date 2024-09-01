using System;
using Gibraltar.Monitor;
using Gibraltar.Serialization;

namespace Loupe.Agent.PerformanceCounters.Serialization
{
    /// <summary>
    /// Registers our packet types with the serialization logic
    /// </summary>
    internal class PerfCounterMetricPacketFactory : IPacketFactory
    {
        private readonly Session m_Session;
        private readonly string m_PerfCounterMetricDefinitionPacketType;
        private readonly string m_PerfCounterMetricPacketType;
        private readonly string m_PerfCounterMetricSamplePacketType;

        public PerfCounterMetricPacketFactory(Session session)
        {
            m_Session = session;

            //resolve the names of all the types we want to be able to get packets for
            //this lets us do a faster switch in CreatePacket
            m_PerfCounterMetricDefinitionPacketType = nameof(PerfCounterMetricDefinitionPacket);
            m_PerfCounterMetricPacketType = nameof(PerfCounterMetricPacket);
            m_PerfCounterMetricSamplePacketType = nameof(PerfCounterMetricSamplePacket);
        }

        /// <summary>
        /// Register the packet factory with the packet reader for all packet types it supports
        /// </summary>
        /// <param name="packetReader"></param>
        public void Register(IPacketReader packetReader)
        {
            packetReader.RegisterFactory(m_PerfCounterMetricDefinitionPacketType, this);
            packetReader.RegisterFactory(m_PerfCounterMetricPacketType, this);
            packetReader.RegisterFactory(m_PerfCounterMetricSamplePacketType, this);
        }

        public IPacket CreatePacket(PacketDefinition definition, IFieldReader reader)
        {
            IPacket packet;

            //what we create varies by what specific definition they're looking for
            if (definition.TypeName == m_PerfCounterMetricDefinitionPacketType)
            {
                packet = new PerfCounterMetricDefinitionPacket(m_Session);
            }
            else if (definition.TypeName == m_PerfCounterMetricPacketType)
            {
                packet = new PerfCounterMetricPacket(m_Session);
            }
            else if (definition.TypeName == m_PerfCounterMetricSamplePacketType)
            {
                packet = new PerfCounterMetricSamplePacket(m_Session);
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
    }
}
