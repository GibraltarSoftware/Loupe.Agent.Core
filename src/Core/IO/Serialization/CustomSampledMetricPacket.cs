using System;
using Loupe.Core.Data;
using Loupe.Core.Metrics;

#pragma warning disable 1591
namespace Loupe.Core.IO.Serialization
{
    /// <summary>
    /// The serializeable representation of a custom sampled metric
    /// </summary>
    public class CustomSampledMetricPacket : SampledMetricPacket, IPacketObjectFactory<Metric, MetricDefinition>, IComparable<CustomSampledMetricPacket>, IEquatable<CustomSampledMetricPacket>
    {
        /// <summary>
        /// Create a new custom sampled metric packet for the provided metric definition and a specific instance.
        /// </summary>
        /// <param name="metricDefinitionPacket">The metric definition packet that defines this metric</param>
        /// <param name="instanceName">The unique instance name of this metric or null for the default instance.</param>
        public CustomSampledMetricPacket(CustomSampledMetricDefinitionPacket metricDefinitionPacket, string instanceName)
            : base(metricDefinitionPacket, instanceName)
        {
        }

        public CustomSampledMetricPacket(Session session) 
            : base(session)
        {
        }

        #region Public Properties and Methods

        public int CompareTo(CustomSampledMetricPacket other)
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
            return Equals(other as CustomSampledMetricPacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(CustomSampledMetricPacket other)
        {
            //We're really just a type cast, refer to our base object
            return ((Object) this).Equals(other);
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


        #region IPacketObjectFactory<Metric, MetricDefinition> Members

        Metric IPacketObjectFactory<Metric, MetricDefinition>.GetDataObject(MetricDefinition optionalParent)
        {
            //this is just here for us to be able to create our derived type for the generic infrastructure
            return new CustomSampledMetric((CustomSampledMetricDefinition)optionalParent, this);
        }

        #endregion
    }
}
