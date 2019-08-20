
using System;
using System.Diagnostics;
using System.Reflection;
using Loupe.Core.Data;
using Loupe.Core.IO.Serialization;
using Loupe.Core.Metrics;
using Loupe.Core.Monitor;
using Loupe.Core.Serialization;

#pragma warning disable 1591
namespace Loupe.Agent.PerformanceCounters.Serialization
{
    /// <summary>
    /// A serializable performance counter metric definition.  Provides metadata for metrics based on Windows performance counters.
    /// </summary>
    [Serializable]
    internal class PerfCounterMetricPacket : SampledMetricPacket, IPacketObjectFactory<Metric, MetricDefinition>, IComparable<PerfCounterMetricPacket>, IEquatable<PerfCounterMetricPacket>
    {
        /// <summary>
        /// Creates an object from the provided metric definition packet and windows performance counter.
        /// </summary>
        /// <remarks>The windows performance counter must match the metric definition provided.</remarks>
        /// <param name="metricDefinitionPacket">The metric definition for the provided performance counter</param>
        /// <param name="counter">The windows performance counter instance</param>
        public PerfCounterMetricPacket(PerfCounterMetricDefinitionPacket metricDefinitionPacket, PerformanceCounter counter)
            : base(metricDefinitionPacket, counter.InstanceName)
        {
            //NOTE:  I don't think this code is reachable; you'll get an explosion on the base constructor first when 
            //it tries to walk a null pointer.
            if (counter == null)
            {
                throw new ArgumentNullException(nameof(counter), "No performance counter object was provided and one is required.");
            }
        }

        public PerfCounterMetricPacket(Session session) 
            : base(session)
        {            
        }

        #region Public Properties and Methods

        /// <summary>
        /// Compare this object to another.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PerfCounterMetricPacket other)
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
            return Equals(other as PerfCounterMetricPacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(PerfCounterMetricPacket other)
        {
            //we let our base object do the compare, we're realy just casting things
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

        #region IPacketObjectFactory<Metric, MetricDefinition> Members

        Metric IPacketObjectFactory<Metric, MetricDefinition>.GetDataObject(MetricDefinition optionalParent)
        {
            //this is just here for us to be able to create our derived type for the generic infrastructure
            return new PerfCounterMetric((PerfCounterMetricDefinition)optionalParent, this);
        }

        #endregion
    }
}
