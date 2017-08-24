using System;
using Gibraltar.Serialization;

namespace Gibraltar.Monitor.Internal
{
    /// <summary>
    /// One raw data sample of a custom sampled metric
    /// </summary>
    /// <remarks>A metric sample packet must be explicitly logged to be recorded, although when it is logged 
    /// does not affect any of its data (timestamps and other information are captured during construction).</remarks>
    internal class CustomSampledMetricSamplePacket : SampledMetricSamplePacket, IPacket, IPacketObjectFactory<MetricSample, Metric>, IComparable<CustomSampledMetricSamplePacket>, IEquatable<CustomSampledMetricSamplePacket>
    {
        private double m_BaseValue; //the (optional) base value to compare the raw value to (used in some metric types)

        /// <summary>
        /// Create a complete custom sampled metric packet
        /// </summary>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="metric">The metric this sample is for</param>
        public CustomSampledMetricSamplePacket(CustomSampledMetric metric, double rawValue)
            : base(metric, rawValue)
        {
            m_BaseValue = 0;
        }

        /// <summary>
        /// Create a complete custom sampled metric packet
        /// </summary>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        /// <param name="metric">The metric this sample is for</param>
        public CustomSampledMetricSamplePacket(CustomSampledMetric metric, double rawValue, DateTimeOffset rawTimeStamp)
            : base(metric, rawValue, rawTimeStamp)
        {
            m_BaseValue = 0;
        }

        /// <summary>
        /// Create a complete custom sampled metric packet
        /// </summary>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="baseValue">The reference value to compare against for come counter types</param>
        /// <param name="metric">The metric this sample is for</param>
        public CustomSampledMetricSamplePacket(CustomSampledMetric metric, double rawValue, double baseValue)
            : base(metric, rawValue)
        {
            m_BaseValue = baseValue;
        }

        /// <summary>
        /// Create a complete custom sampled metric packet
        /// </summary>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        /// <param name="baseValue">The reference value to compare against for come counter types</param>
        /// <param name="metric">The metric this sample is for</param>
        public CustomSampledMetricSamplePacket(CustomSampledMetric metric, double rawValue, double baseValue, DateTimeOffset rawTimeStamp)
            : base(metric, rawValue, rawTimeStamp)
        {
            m_BaseValue = baseValue;
        }

        /// <summary>
        /// Create a custom sampled metric sample packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        public CustomSampledMetricSamplePacket(Session session)
            : base(session)
        {            
        }


        #region Public Properties and Methods

        /// <summary>
        /// The base value as it was sampled
        /// </summary>
        /// <remarks>The base value is used with the raw value for certain counter types.  For example, if you want to determine
        /// the percentage utilization, you need to know both how much capacity was used and how much was available.  The base 
        /// represents how much was available and the raw value how much was used in that scenario.</remarks>
        public double BaseValue
        {
            get { return m_BaseValue; }
            protected set { m_BaseValue = value; }
        }

        /// <summary>
        /// Compare this custom sampled metric sample packet with another to determine if they are the same sample packet.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(CustomSampledMetricSamplePacket other)
        {
            //we really are just forwarding to the default comparitor; we are just casting types
            return base.CompareTo(other);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public override bool Equals(object other)
        {
            //use our type-specific override
            return Equals(other as CustomSampledMetricSamplePacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(CustomSampledMetricSamplePacket other)
        {
            //We're really just a type cast, refer to our base object
            return base.Equals(other);
        }

        /// <summary>
        /// Provides a representative hash code for objects of this type to spread out distribution
        /// in hash tables.
        /// </summary>
        /// <remarks>Objects which consider themselves to be Equal (a.Equals(b) returns true) are
        /// expected to have the same hash code.  Objects which are not Equal may have the same
        /// hash code, but minimizing such overlaps helps with efficient operation of hash tables.
        /// </remarks>
        /// <returns>
        /// an int representing the hash code calculated for the contents of this object
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = base.GetHashCode(); // Equals defers to base, so just use hash code for inherited base type

            return myHash;
        }

        
        #endregion

        #region IPacket implementation

        private const int SerializationVersion = 1;

        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            //the majority of packets have no dependencies
            return null;
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            var typeName = typeof(CustomSampledMetricSamplePacket).Name;
            PacketDefinition definition = new PacketDefinition(typeName, SerializationVersion, false);
            definition.Fields.Add("baseValue", FieldType.Double);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("baseValue", m_BaseValue);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("baseValue", out m_BaseValue);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }
        }

        #endregion

        #region IPacketObjectFactory<MetricSample,Metric> Members

        MetricSample IPacketObjectFactory<MetricSample, Metric>.GetDataObject(Metric optionalParent)
        {
            return new CustomSampledMetricSample((CustomSampledMetric)optionalParent, this);
        }

        #endregion
    }
}
