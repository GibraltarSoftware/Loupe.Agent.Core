
using System;
using System.Diagnostics;
using System.Reflection;
using Loupe;
using Loupe.Monitor;
using Loupe.Monitor.Serialization;
using Loupe.Serialization;

namespace Loupe.Agent.PerformanceCounters.Serialization
{
    /// <summary>
    /// A single windows performance counter data sample.
    /// </summary>
    internal class PerfCounterMetricSamplePacket : SampledMetricSamplePacket, IPacket, IPacketObjectFactory<MetricSample, Metric>, IComparable<PerfCounterMetricSamplePacket>, IEquatable<PerfCounterMetricSamplePacket>
    {
        private CounterSample m_Sample;

        /// <summary>
        /// Create a new performance counter metric by sampling the provided performance counter object.
        /// </summary>
        /// <remarks>
        /// The counter packet provided must match the performance counter object.  The counter packet is used
        /// to provide clients with all of the information necessary to process performance metrics without using 
        /// the performance counter infrastructure.
        /// </remarks>
        /// <param name="counter">The windows performance counter to sample</param>
        /// <param name="metric">The corresponding performance counter metric</param>
        public PerfCounterMetricSamplePacket(PerformanceCounter counter, PerfCounterMetric metric)
            : base(metric)
        {
            //we ask the perf counter for a value right now
            m_Sample = counter.NextSample();
            
            //and we have to stuff a few things into our base object so it knows what we're talking about
            RawValue = m_Sample.RawValue;
        }


        /// <summary>
        /// Create a new performance counter metric sampled packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        public PerfCounterMetricSamplePacket(Session session) 
            : base(session)
        {            
        }

        #region Public Properties and Methods

        public static implicit operator CounterSample(PerfCounterMetricSamplePacket r)
        {
            return r.CounterSample;
        }

        /// <summary>
        /// Compare this object to another.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PerfCounterMetricSamplePacket other)
        {
            //we just gateway to our base object.
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
            return Equals(other as PerfCounterMetricSamplePacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(PerfCounterMetricSamplePacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            //we let our base object do the compare, we're realy just casting things
            return ((m_Sample == other.m_Sample) 
                    && base.Equals(other));
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

            myHash ^= m_Sample.GetHashCode(); // Fold in hash code for CounterSample member Sample

            return myHash;
        }


        /// <summary>
        /// The Windows Counter Sample.
        /// </summary>
        /// <returns></returns>
        public CounterSample CounterSample => m_Sample;

        #endregion


        #region Explicit IPacket Implementation
        //We need to explicitly implement this interface because we don't want to override the IPacket implementation,
        //we want to have our own distinct implementatino because the packet serialization methods know to recurse object
        //structures looking for the interface.

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
            string typeName = MethodBase.GetCurrentMethod().DeclaringType.Name;
            PacketDefinition definition = new PacketDefinition(typeName, SerializationVersion, false);
            definition.Fields.Add("baseValue", FieldType.Int64);
            definition.Fields.Add("counterTimeStamp", FieldType.Int64);
            definition.Fields.Add("counterFrequency", FieldType.Int64);
            definition.Fields.Add("systemFrequency", FieldType.Int64);
            definition.Fields.Add("timeStamp", FieldType.Int64);
            definition.Fields.Add("timeStamp100nSec", FieldType.Int64);
            definition.Fields.Add("counterType", FieldType.Int32);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("baseValue", m_Sample.BaseValue);
            packet.SetField("counterTimeStamp", m_Sample.CounterTimeStamp);
            packet.SetField("counterFrequency", m_Sample.CounterFrequency);
            packet.SetField("systemFrequency", m_Sample.SystemFrequency);
            packet.SetField("timeStamp", m_Sample.TimeStamp);
            packet.SetField("timeStamp100nSec", m_Sample.TimeStamp100nSec);

            //conceptually we shouldn't persist this - it's always the same and it's always on our metric, however
            //we need it here for deserialization purposes because our metric packet object isn't available during
            //the deserialization process.
            packet.SetField("counterType", (int)m_Sample.CounterType);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:

                    packet.GetField("baseValue", out long baseValue);
                    packet.GetField("counterTimeStamp", out long counterTimeStamp);
                    packet.GetField("counterFrequency", out long counterFrequency);
                    packet.GetField("systemFrequency", out long systemFrequency);
                    packet.GetField("timeStamp", out long timeStamp);
                    packet.GetField("timeStamp100nSec", out long timeStamp100nSec);

                    //conceptually we shouldn't persist this - it's always the same and it's always on our metric, however
                    //we need it here for deserialization purposes because our metric packet object isn't available during
                    //the deserialization process.
                    packet.GetField("counterType", out int rawCounterType);
                    PerformanceCounterType counterType = (PerformanceCounterType)rawCounterType;

                    //Now, create our sample object from this data
                    m_Sample = new CounterSample((long)base.RawValue, baseValue, counterFrequency, systemFrequency, timeStamp, timeStamp100nSec, counterType, counterTimeStamp);
                    break;
                default:
                    throw new LoupePacketVersionException(definition.Version);
            }
        }

        #endregion

        #region IPacketObjectFactory<MetricSample,Metric> Members

        MetricSample IPacketObjectFactory<MetricSample, Metric>.GetDataObject(Metric optionalParent)
        {
            return new PerfCounterMetricSample((PerfCounterMetric)optionalParent, this);
        }

        #endregion
    }
}
