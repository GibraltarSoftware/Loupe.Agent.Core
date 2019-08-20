using System;
using Loupe.Serialization;
using Loupe.Extensibility.Data;
using Loupe.Metrics;

namespace Loupe.Monitor.Serialization
{
    /// <summary>
    /// The serializeable representation of a custom sampled metric
    /// </summary>
    public class CustomSampledMetricDefinitionPacket : SampledMetricDefinitionPacket, IPacket, IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>, IComparable<CustomSampledMetricDefinitionPacket>, IEquatable<CustomSampledMetricDefinitionPacket>
    {
        private SamplingType m_MetricSampleType;

        /// <summary>
        /// Create a new custom sampled metric definition packet from the provided information
        /// </summary>
        /// <remarks>Definition packets are the lightweight internals used for persistence.</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The specific unit representation of the data being captured for this metric</param>
        public CustomSampledMetricDefinitionPacket(string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType)
            : base(metricTypeName, categoryName, counterName)
        {
            m_MetricSampleType = metricSampleType;
        }

        /// <summary>
        /// Create a new custom sampled metric definition packet from the provided information
        /// </summary>
        /// <remarks>Definition packets are the lightweight internals used for persistence.</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="metricSampleType">The specific unit representation of the data being captured for this metric</param>
        /// <param name="unitCaption">The display caption for the calculated values captured under this metric.</param>
        /// <param name="description">A description of what is tracked by this metric, suitable for end-user display.</param>
        public CustomSampledMetricDefinitionPacket(string metricTypeName, string categoryName, string counterName, SamplingType metricSampleType, string unitCaption, string description)
            : base(metricTypeName, categoryName, counterName, unitCaption, description)
        {
            m_MetricSampleType = metricSampleType;
        }

        /// <summary>
        /// Create a new custom sampled metric definition packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        public CustomSampledMetricDefinitionPacket(Session session)
            : base(session)
        {            
        }

        #region Public Properties and Methods

        /// <summary>
        /// Compares this object to the provided comparison object
        /// </summary>
        /// <param name="other"></param>
        /// <returns>Zero if objects are the same object, -1 or 1 to indicate relative order (see CompareTo for more information)</returns>
        public int CompareTo(CustomSampledMetricDefinitionPacket other)
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
            return Equals(other as CustomSampledMetricDefinitionPacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(CustomSampledMetricDefinitionPacket other)
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
        /// The intended method of interpreting the sampled counter value.
        /// </summary>
        /// <remarks>The counter type determines what math needs to be run
        /// to determine the correct value when comparing two samples.</remarks>
        public SamplingType MetricSampleType
        {
            get { return m_MetricSampleType; }
            protected set { m_MetricSampleType = value; }
        }

        /// <summary>
        /// Generate a display caption for the supplied sample metric type
        /// </summary>
        /// <param name="metricSampleType">The sample metric type to make a caption for</param>
        /// <returns>An end-user display caption</returns>
        public static string SampledMetricTypeCaption(SamplingType metricSampleType)
        {
            string returnVal;

            switch(metricSampleType)
            {
                case SamplingType.TotalCount:
                    returnVal = "Count of Items";
                    break;
                case SamplingType.TotalFraction:
                    returnVal = "Percentage";
                    break;
                case SamplingType.IncrementalCount:
                    returnVal = "Count of Items";
                    break;
                case SamplingType.IncrementalFraction:
                    returnVal = "Percentage";
                    break;
                case SamplingType.RawCount:
                    returnVal = "Count of Items";
                    break;
                case SamplingType.RawFraction:
                    returnVal = "Percentage";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(metricSampleType));
            }

            return returnVal;
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Calculates the unit caption as required by the base object.
        /// </summary>
        /// <returns>The caption to display for the units of value.</returns>
        protected override string OnUnitCaptionGenerate()
        {
            //make a string description of our counter type
            return SampledMetricTypeCaption(MetricSampleType);
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
            const string typeName = nameof(CustomSampledMetricDefinitionPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("metricSampleType", FieldType.Int32);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("metricSampleType", (int)m_MetricSampleType);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("metricSampleType", out int rawSampleType);
                    m_MetricSampleType = (SamplingType)rawSampleType;
                    break;
                default:
                    throw new LoupePacketVersionException(definition.Version);
            }
        }
        #endregion

        #region IPacketObjectFactory<MetricDefinition, object> Members

        MetricDefinition IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>.GetDataObject(MetricDefinitionCollection optionalParent)
        {
            //this is just here for us to be able to create our derived type for the generic infrastructure
            return new CustomSampledMetricDefinition(optionalParent, this);
        }

        #endregion
    }
}
