using System;

#pragma warning disable 1591
namespace Gibraltar.Monitor.Serialization
{
    /// <summary>
    /// A serializable sampled metric definition.  Provides metadata for metrics based on sampled values.
    /// </summary>
    public abstract class SampledMetricPacket : MetricPacket, IComparable<SampledMetricPacket>, IEquatable<SampledMetricPacket>
    {
        protected SampledMetricPacket(SampledMetricDefinitionPacket metricDefinitionPacket, string instanceName)
            : base(metricDefinitionPacket, instanceName)
        {   
        }

        protected SampledMetricPacket(Session session)
            : base(session)
        {            
        }

        #region Public Properties and Methods

        public int CompareTo(SampledMetricPacket other)
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
            return Equals(other as SampledMetricPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(SampledMetricPacket other)
        {
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
    }
}
