using System;
using Gibraltar.Serialization;




namespace Gibraltar.Monitor.Internal
{
    internal class MetricDefinitionPacketFactory: IPacketFactory
    {
        private readonly Session m_Session;
        private readonly string m_MetricDefinitionPacketType;
        private readonly string m_SampledMetricDefinitionPacketType;
        private readonly string m_EventMetricDefinitionPacketType;
        private readonly string m_EventMetricValueDefinitionPacketType;
        private readonly string m_CustomSampledMetricDefinitionPacketType;

        public MetricDefinitionPacketFactory(Session session)
        {
            m_Session = session;

            //resolve the names of all the types we want to be able to get packets for
            //this lets us do a faster switch in CreatePacket
            m_MetricDefinitionPacketType = typeof(MetricDefinitionPacket).Name;
            m_SampledMetricDefinitionPacketType = typeof(SampledMetricDefinitionPacket).Name;
            m_EventMetricDefinitionPacketType = typeof(EventMetricDefinitionPacket).Name;
            m_EventMetricValueDefinitionPacketType = typeof(EventMetricValueDefinitionPacket).Name;
            m_CustomSampledMetricDefinitionPacketType = typeof(CustomSampledMetricDefinitionPacket).Name;
        }

        /// <summary>
        /// Register the packet factory with the packet reader for all packet types it supports
        /// </summary>
        /// <param name="packetReader"></param>
        public void Register(IPacketReader packetReader)
        {
            packetReader.RegisterFactory(m_MetricDefinitionPacketType, this);
            packetReader.RegisterFactory(m_SampledMetricDefinitionPacketType, this);
            packetReader.RegisterFactory(m_EventMetricDefinitionPacketType, this);
            packetReader.RegisterFactory(m_EventMetricValueDefinitionPacketType, this);
            packetReader.RegisterFactory(m_CustomSampledMetricDefinitionPacketType, this);
        }

        public IPacket CreatePacket(PacketDefinition definition, IFieldReader reader)
        {
            IPacket packet;

            if (definition.TypeName == m_SampledMetricDefinitionPacketType)
            {
                //sampled metrics can't be created directly - they're an abstract class.
                throw new ArgumentOutOfRangeException(nameof(definition), definition.TypeName, "Sampled Metric objects can't be created, only derived classes can.");
            }

            //what we create varies by what specific definition they're looking for
            if (definition.TypeName == m_MetricDefinitionPacketType)
            {
                packet = new MetricDefinitionPacket(m_Session);
            }
            else if (definition.TypeName == m_EventMetricDefinitionPacketType)
            {
                packet = new EventMetricDefinitionPacket(m_Session);
            }
            else if (definition.TypeName == m_EventMetricValueDefinitionPacketType)
            {
                packet = new EventMetricValueDefinitionPacket(m_Session);
            }
            else if (definition.TypeName == m_CustomSampledMetricDefinitionPacketType)
            {
                packet = new CustomSampledMetricDefinitionPacket(m_Session);
            }
            else
            {
                //crap, we don't know what to do here.
                throw new ArgumentOutOfRangeException(nameof(definition), definition.TypeName, "This packet factory doesn't undersatnd how to create packets for the provided type.");
            }

            //this feels a little crazy, but you have to do your own read call here - we aren't just creating the packet
            //object, we actually have to make the standard call to have it read data... 
            definition.ReadFields(packet, reader);

            return packet;
        }
    }
}
