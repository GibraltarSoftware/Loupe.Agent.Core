using System;
using System.Diagnostics;
using Loupe.Core.IO.Serialization;
using Loupe.Core.Monitor;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Metrics
{
    /// <summary>
    /// A single raw sample of a metric.  
    /// </summary>
    /// <remarks>
    /// Individual samples represent a single data point and may not be directly useful without
    /// manipulation.  For example, if the underlying metric is intended to be the rate of an event, the individual raw samples 
    /// will need to be used to calculate the rate instead of being used directly.
    /// </remarks>
    [DebuggerDisplay("{Sequence}: {Timestamp} Value={Value}")]
    public abstract class MetricSample : IMetricSample
    {
        private readonly Metric m_Metric;
        private readonly MetricSamplePacket m_MetricSamplePacket;

        /// <summary>
        /// Create a new metric sample for the provided metric and raw sample packet
        /// </summary>
        /// <remarks>The metric sample is automatically added to the samples collection of the provided metric object.</remarks>
        /// <param name="metric">The specific metric this sample relates to</param>
        /// <param name="samplePacket">The raw sample packet to wrap.</param>
        internal MetricSample(Metric metric, MetricSamplePacket samplePacket)
        {
            //if we didn't get a metric or metric sample, we're toast.
            if (metric == null)
            {
                throw new ArgumentNullException(nameof(metric));
            }

            if (samplePacket == null)
            {
                throw new ArgumentNullException(nameof(samplePacket));
            }

            m_Metric = metric;
            m_MetricSamplePacket = samplePacket;

            //we may need to correct the sample packet - this is due to the order objects are rehydrated in.
            if (m_MetricSamplePacket.MetricPacket == null)
            {
                m_MetricSamplePacket.MetricPacket = m_Metric.Packet;
            }

            //now add ourself to the metric's sample collection, if we aren't in collection mode
            if (((MetricDefinition)metric.Definition).IsLive == false)
            {
                metric.Samples.Add(this);
            }
        }

        #region Public Properties and Methods

        /// <summary>
        /// The unique id of this sample
        /// </summary>
        public virtual Guid Id { get { return m_MetricSamplePacket.ID; } }

        /// <summary>
        /// The metric this sample relates to.
        /// </summary>
        public virtual IMetric Metric { get { return m_Metric; } }

        /// <summary>
        /// The increasing sequence number of all sample packets for this metric to be used as an absolute order sort.
        /// </summary>
        public virtual long Sequence { get { return m_MetricSamplePacket.Sequence; } }

        /// <summary>
        /// The exact date and time the metric was captured.
        /// </summary>
        public virtual DateTimeOffset Timestamp { get { return m_MetricSamplePacket.Timestamp; } }

        /// <summary>
        /// The raw value of the metric.
        /// </summary>
        public abstract double Value { get; }

        /// <summary>
        /// Write this sample to the current process log if it hasn't been written already
        /// </summary>
        /// <remarks>If the sample has not been written to the log yet, it will be written.  
        /// If it has been written, subsequent calls to this method are ignored.</remarks>
        public void Write()
        {
            if (m_MetricSamplePacket.Persisted == false )
            {
                Log.Write(this);
            }
        }

        /// <summary>
        /// Compare this metric sample with another to determine if they are the same or how they should be sorted relative to each other.
        /// </summary>
        /// <remarks>MetricSample instances are sorted by their Sequence number property.</remarks>
        /// <param name="other"></param>
        /// <returns>0 for an exact match, otherwise the relationship between the two for sorting.</returns>
        public int CompareTo(IMetricSample other)
        {
            //for performance reasons we've duped the key check here.
            return m_MetricSamplePacket.Sequence.CompareTo(other.Sequence);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(IMetricSample other)
        {
            // Careful, it could be null; check it without recursion
            if (object.ReferenceEquals(other, null))
            {
                return false; // Since we're a live object we can't be equal to a null instance.
            }

            //look at the metric packets to let them make the call
            return m_MetricSamplePacket.Equals(((MetricSample)other).Packet);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a MetricSample and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            MetricSample otherMetricSample = obj as MetricSample;

            return Equals(otherMetricSample); // Just have type-specific Equals do the check (it even handles null)
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
        /// An int representing the hash code calculated for the contents of this object.
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = m_MetricSamplePacket.GetHashCode(); // Equals just defers to the MetricSamplePacket

            return myHash;
        }

        #endregion

        #region Static Public Methods and Operators

        /// <summary>
        /// Compares two MetricSample instances for equality.
        /// </summary>
        /// <param name="left">The MetricSample to the left of the operator</param>
        /// <param name="right">The MetricSample to the right of the operator</param>
        /// <returns>True if the two MetricSamples are equal.</returns>
        public static bool operator ==(MetricSample left, MetricSample right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (object.ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return object.ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two MetricSample instances for inequality.
        /// </summary>
        /// <param name="left">The MetricSample to the left of the operator</param>
        /// <param name="right">The MetricSample to the right of the operator</param>
        /// <returns>True if the two MetricSamples are not equal.</returns>
        public static bool operator !=(MetricSample left, MetricSample right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (object.ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ! object.ReferenceEquals(right, null);
            }
            return ! left.Equals(right);
        }

        /// <summary>
        /// Compares if one MetricSample instance should sort less than another.
        /// </summary>
        /// <param name="left">The MetricSample to the left of the operator</param>
        /// <param name="right">The MetricSample to the right of the operator</param>
        /// <returns>True if the MetricSample to the left should sort less than the MetricSample to the right.</returns>
        public static bool operator <(MetricSample left, MetricSample right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one MetricSample instance should sort greater than another.
        /// </summary>
        /// <param name="left">The MetricSample to the left of the operator</param>
        /// <param name="right">The MetricSample to the right of the operator</param>
        /// <returns>True if the MetricSample to the left should sort greater than the MetricSample to the right.</returns>
        public static bool operator >(MetricSample left, MetricSample right)
        {
            return (left.CompareTo(right) > 0);
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The raw metric sample packet
        /// </summary>
        public virtual MetricSamplePacket Packet { get { return m_MetricSamplePacket; } }

        #endregion

    }
}
