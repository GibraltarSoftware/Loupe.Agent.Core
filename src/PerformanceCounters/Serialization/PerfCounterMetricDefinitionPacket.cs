
using System;
using System.Diagnostics;
using System.Reflection;
using Loupe.Monitor;
using Loupe.Monitor.Serialization;
using Loupe.Serialization;

namespace Loupe.Agent.PerformanceCounters.Serialization
{
    /// <summary>
    /// A serializable performance counter metric definition.  Provides metadata for metrics based on Windows performance counters.
    /// </summary>
    internal class PerfCounterMetricDefinitionPacket : SampledMetricDefinitionPacket, IPacket, IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>, IComparable<PerfCounterMetricDefinitionPacket>, IEquatable<PerfCounterMetricDefinitionPacket>
    {
        /// <summary>
        /// Create a new metric definition for the provided perfomance counter object
        /// </summary>
        /// <param name="counter">The windows performance counter object to create a metric definition for</param>
        public PerfCounterMetricDefinitionPacket(PerformanceCounter counter)
            : base(PerfCounterMetricDefinition.PerfCounterMetricType, counter.CategoryName, counter.CounterName)
        {
            //NOTE:  I don't think this code is reachable; you'll get an explosion on the base constructor first when 
            //it tries to walk a null pointer.
            if (counter == null)
            {
                throw new ArgumentNullException(nameof(counter), "No performance counter object was provided and one is required.");
            }

            //don't have to check counter type for null because it isn't a nullable base type
            CounterType = counter.CounterType;

            //we override our caption & description from the counter's configuration.
            Caption = counter.CounterName;
            Description = counter.CounterHelp;
        }
        
        /// <summary>
        /// Create a performance counter metric definition packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        public PerfCounterMetricDefinitionPacket(Session session)
            : base(session)
        {            
        }

        #region Public Properties and Methods

        /// <summary>
        /// The intended method of interpreting the sampled counter value.
        /// </summary>
        /// <remarks>Uses the Windows Performance Counter type which appeared very comprehensive to the 
        /// various reasons you'd use a sampled metric. The counter type determines what math needs to be run
        /// to determine the correct value when comparing two samples.</remarks>
        public PerformanceCounterType CounterType { get; protected set; }


        /// <summary>
        /// Compare this object to another.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PerfCounterMetricDefinitionPacket other)
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
            return Equals(other as PerfCounterMetricDefinitionPacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(PerfCounterMetricDefinitionPacket other)
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

        #region Protected Properties and Methods


        protected override string OnUnitCaptionGenerate()
        {
            //we are going to guess this from the counter name.  We'll continue to enhance this list as time goes on.

            string unitCaption = string.Empty;
            string counterName = CounterName;

            //lets see what the counter name contains to make a good guess.
            if (counterName.Contains("%"))
            {
                //whatever percentage it is, that's our unit
                unitCaption = "%";
            }
            else if (counterName.Contains("Bytes"))
            {
                unitCaption = "Bytes";
            }
            else if (counterName.Contains("KBytes"))
            {
                unitCaption = "KBytes";
            }
            else if (counterName.Contains("MBytes"))
            {
                unitCaption = "MBytes";
            }
            else if (counterName.Contains("Time"))
            {
                unitCaption = "Time";
            }
            //Now lets go after things we know we can match to just count.
            else if (counterName.Contains("Packets"))
            {
                unitCaption = "Count";
            }
            else if (counterName.Contains("Queue"))
            {
                unitCaption = "Count";
            }
            else if (counterName.Contains("Page"))
            {
                unitCaption = "Count";
            }
            else if (counterName.Contains("Thread"))
            {
                unitCaption = "Count";
            }
            else if (counterName.Contains("Connection"))
            {
                unitCaption = "Count";
            }

            //additionally, if it ends in "/sec" then it's a rate and we need to represent that.
            if (counterName.EndsWith("/sec", StringComparison.OrdinalIgnoreCase)) // allow with arbitrary case?
            {
                unitCaption += "/sec";
            }

            return unitCaption;
        }
        
        #endregion


        #region IPacket Members

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
            definition.Fields.Add("counterType", FieldType.Int32);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("counterType", (int)CounterType);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("counterType", out int rawCounterType);
                    CounterType = (PerformanceCounterType)rawCounterType;
                    break;
            }
        }

        #endregion

        #region IPacketObjectFactory<Metric, MetricDefinitionCollection> Members

        MetricDefinition IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>.GetDataObject(MetricDefinitionCollection optionalParent)
        {
            //this is just here for us to be able to create our derived type for the generic infrastructure
            return new PerfCounterMetricDefinition(optionalParent, this);
        }

        #endregion
    }
}
