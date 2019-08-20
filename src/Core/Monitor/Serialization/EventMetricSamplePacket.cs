using System;
using Loupe.Core.Serialization;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Monitor.Serialization
{
    /// <summary>
    /// One raw data sample of an event metric
    /// </summary>
    /// <remarks>A metric sample packet must be explicitly logged to be recorded, although when it is logged 
    /// does not affect any of its data (timestamps and other information are captured during construction).</remarks>
    public class EventMetricSamplePacket : MetricSamplePacket, IDynamicPacket, IPacketObjectFactory<MetricSample, Metric>, IComparable<EventMetricSamplePacket>, IEquatable<EventMetricSamplePacket>
    {
        private EventMetricValueDefinitionCollection m_ValueDefinitions;

        /// <summary>
        /// Create an event metric sample packet for live data collection
        /// </summary>
        /// <param name="metric">The metric this sample is for</param>
        public EventMetricSamplePacket(EventMetric metric)
            : base(metric)
        {
            //create a new sample values collection the correct size of our metric's values collection
            Values = new object[metric.Definition.Values.Count];
            m_ValueDefinitions = (EventMetricValueDefinitionCollection)metric.Definition.Values;

            //and set our default dynamic type name based on our metric definition.  It isn't clear to me
            //that there's really a contract that it won't be changed by the serializer, so we allow it to be
            DynamicTypeName = metric.Definition.Name;
        }

        /// <summary>
        /// Create an event metric sample packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        public EventMetricSamplePacket(Session session) 
            : base(session)
        {            
        }

        #region Public Properties and Methods

        /// <summary>
        /// Compare this object to another to determine sort order
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(EventMetricSamplePacket other)
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
            return Equals(other as EventMetricSamplePacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetricSamplePacket other)
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


        /// <summary>
        /// The values related to this event metric sample.
        /// </summary>
        public object[] Values { get; private set; }

        #endregion
        
        #region Explicit IDynamicPacket Implementation
        //We need to explicitly implement this interface because we don't want to override the IPacket implementation,
        //we want to have our own distinct implementation because the packet serialization methods know to recurse object
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
            const string typeName = nameof(EventMetricSamplePacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            //now we need to write out the definition of each of our values to our type.
            //we're a dynamic packet so we can have a different definition for our declaring type each time.
            foreach (EventMetricValueDefinition valueDefinition in m_ValueDefinitions)
            {
                valueDefinition.AddField(definition);
            }

            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            //iterate our array, writing out each value. 
            for (int valueIndex = 0; valueIndex < definition.Fields.Count; valueIndex++)
            {
                FieldDefinition fieldDefinition = definition.Fields[valueIndex];
                packet.SetField(fieldDefinition.Name, Values[valueIndex]);
            }
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    //read the values by our definition
                    Values = new object[definition.Fields.Count];

                    for (int valueIndex = 0; valueIndex < definition.Fields.Count; valueIndex++)
                    {
                        FieldDefinition fieldDefinition = definition.Fields[valueIndex];
                        packet.GetField(fieldDefinition.Name, out object fieldValue);
                        Values[valueIndex] = fieldValue;
                    }

                    break;
                default:
                    throw new LoupePacketVersionException(definition.Version);
            }

            //Now we need to go off and find our value definition so we can be serialized out gain
            if (Session.MetricDefinitions.TryGetMetricValue(MetricId, out var ourMetric) == false)
            {
                //BIG problems- no metric for our metric ID?
                throw new ArgumentException("Unable to read event metric sample because the associated metric couldn't be found.");
            }
            m_ValueDefinitions = (EventMetricValueDefinitionCollection)((EventMetric)ourMetric).Definition.Values;
        }

        /// <summary>
        /// The type name to use for the dynamic packet storing this sample.
        /// </summary>
        public string DynamicTypeName { get; set; }

        #endregion

        #region IPacketObjectFactory<MetricSample,Metric> Members

        MetricSample IPacketObjectFactory<MetricSample, Metric>.GetDataObject(Metric optionalParent)
        {
            return new EventMetricSample((EventMetric)optionalParent, this);
        }

        #endregion
    }
}
