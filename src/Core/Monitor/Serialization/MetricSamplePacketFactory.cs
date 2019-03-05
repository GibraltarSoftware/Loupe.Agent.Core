using System;
using Gibraltar.Serialization;
#pragma warning disable 1591

namespace Gibraltar.Monitor.Serialization
{
    public class MetricSamplePacketFactory : IPacketFactory
    {
        private readonly Session m_Session;
        private readonly string m_MetricSamplePacketType;
        private readonly string m_SampledMetricSamplePacketType;
        private readonly string m_CustomSampledMetricSamplePacketType;
        private readonly string m_EventMetricSamplePacketType;

        public MetricSamplePacketFactory(Session session)
        {
            m_Session = session;

            //resolve the names of all the types we want to be able to get packets for
            //this lets us do a faster switch in CreatePacket
            m_MetricSamplePacketType = typeof(MetricSamplePacket).Name;
            m_SampledMetricSamplePacketType = typeof(SampledMetricSamplePacket).Name;
            m_EventMetricSamplePacketType = typeof(EventMetricSamplePacket).Name;
            m_CustomSampledMetricSamplePacketType = typeof(CustomSampledMetricSamplePacket).Name;
        }

        /// <summary>
        /// Register the packet factory with the packet reader for all packet types it supports
        /// </summary>
        /// <param name="packetReader"></param>
        public void Register(IPacketReader packetReader)
        {
            packetReader.RegisterFactory(m_MetricSamplePacketType, this);
            packetReader.RegisterFactory(m_SampledMetricSamplePacketType, this);
            packetReader.RegisterFactory(m_EventMetricSamplePacketType, this);
            packetReader.RegisterFactory(m_CustomSampledMetricSamplePacketType, this);
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
            if (definition.TypeName == m_MetricSamplePacketType)
            {
                //metrics can't be created directly - they're an abstract class.
                throw new ArgumentOutOfRangeException(nameof(definition), definition.TypeName, "Metric objects can't be created, only derived classes can.");
            }

            if (definition.TypeName == m_SampledMetricSamplePacketType)
            {
                //sampled metrics can't be created directly - they're an abstract class.
                throw new ArgumentOutOfRangeException(nameof(definition), definition.TypeName, "Sampled Metric objects can't be created, only derived classes can.");
            }

            if (definition.TypeName == m_EventMetricSamplePacketType)
            {
                packet = new EventMetricSamplePacket(m_Session);
            }
            else if (definition.TypeName == m_CustomSampledMetricSamplePacketType)
            {
                packet = new CustomSampledMetricSamplePacket(m_Session);
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
