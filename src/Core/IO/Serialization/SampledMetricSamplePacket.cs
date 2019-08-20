using System;
using Loupe.Core.Data;
using Loupe.Core.Metrics;
using Loupe.Core.Serialization;

namespace Loupe.Core.IO.Serialization
{
    /// <summary>
    /// The base class for a single sampled metric data sample.
    /// </summary>
    /// <remarks>A sampled metric sample packet must be explicitly logged to be recorded, although when it is logged 
    /// does not affect any of its data (timestamps and other information are captured during construction).  
    /// This is a base class and can not be used directly.  Instead, one of its inheritors will be used depending on
    /// the particular metric type being logged.</remarks>
    public abstract class SampledMetricSamplePacket : MetricSamplePacket, IPacket, IComparable<SampledMetricSamplePacket>, IEquatable<SampledMetricSamplePacket>
    {
        private DateTimeOffset m_RawTimestamp;
        private double m_RawValue;

        /// <summary>
        /// Create an incomplete sampled metric with just the metric packet
        /// </summary>
        /// <remarks>Before the sampled metric packet is valid, a raw value, counter time stamp, and counter type will need to be supplied.</remarks>
        /// <param name="metric">The metric this sample is for</param>
        protected SampledMetricSamplePacket(SampledMetric metric)
            : base(metric)
        {
        }

        /// <summary>
        /// Create a complete sampled metric packet
        /// </summary>
        /// <para>Metrics using a sample type of AverageFraction and DeltaFraction should not use this method because
        /// they require a base value as well as a raw value.</para>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="metric">The metric this sample is for</param>
        protected SampledMetricSamplePacket(SampledMetric metric, double rawValue)
            : base(metric)
        {
            RawValue = rawValue;

            //we will fill in the other items if they are missing, so we don't check them for null.
            RawTimestamp = DateTimeOffset.Now; //we convert to UTC during serialization, we want local time.
        }

        /// <summary>
        /// Create a complete sampled metric packet
        /// </summary>
        /// <para>Metrics using a sample type of AverageFraction and DeltaFraction should not use this method because
        /// they require a base value as well as a raw value.</para>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimeStamp">The exact date and time the raw value was determined</param>
        /// <param name="metric">The metric this sample is for</param>
        protected SampledMetricSamplePacket(SampledMetric metric, double rawValue, DateTimeOffset rawTimeStamp)
            : base(metric)
        {
            RawValue = rawValue;

            //we will fill in the other items if they are missing, so we don't check them for null.
            RawTimestamp = rawTimeStamp;
        }

        /// <summary>
        /// Create a new sampled metric sample packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        protected SampledMetricSamplePacket(Session session)
            : base (session)
        {
            
        }

        #region Public Properties and Methods

        /// <summary>
        /// Compares this sampled metric packet with another.  See general CompareTo documentation for specifics.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(SampledMetricSamplePacket other)
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
            return Equals(other as SampledMetricSamplePacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(SampledMetricSamplePacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((RawTimestamp == other.RawTimestamp) && (RawValue == other.RawValue) && base.Equals(other));
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
            int myHash = base.GetHashCode(); // Fold in hash code for inherited base type

            myHash ^= m_RawValue.GetHashCode(); // Fold in hash code for double member RawValue
            myHash ^= m_RawTimestamp.GetHashCode(); // Fold in hash code for DateTimeOffset member RawTimeStamp

            return myHash;
        }

        /// <summary>
        /// The exact date and time the raw value was determined.
        /// </summary>
        /// <remarks>When doing some calculations it is essential to know when the raw value became
        /// the new value so a difference between it and a subsequent value is given the proper duration.  
        /// For example, if you want to know bytes per second you need to know exactly when the underlying
        /// bytes metric was determined, which may not be when it was recorded to the log file.</remarks>
        public DateTimeOffset RawTimestamp { get { return m_RawTimestamp; } protected set { m_RawTimestamp = value; } }


        /// <summary>
        /// The raw value as it was sampled
        /// </summary>
        /// <remarks>The raw value generally can't be used directly but instead must be processed by comparing
        /// the raw values of two different samples and their time difference to determine the effective sampled metric value.</remarks>
        public double RawValue { get { return m_RawValue; } protected set { m_RawValue = value; } }

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
            const string typeName = nameof(SampledMetricSamplePacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("rawTimeStamp", FieldType.DateTimeOffset);
            definition.Fields.Add("rawValue", FieldType.Double);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("rawTimeStamp", m_RawTimestamp);
            packet.SetField("rawValue", m_RawValue);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("rawTimeStamp", out m_RawTimestamp);
                    packet.GetField("rawValue", out m_RawValue);
                    break;
                default:
                    throw new LoupePacketVersionException(definition.Version);
            }
        }
        #endregion

    }
}
