using System;
using Loupe.Core.Serialization;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Monitor.Serialization
{
    /// <summary>
    /// A serializable sampled metric definition.  Provides metadata for metrics based on sampled values.
    /// </summary>
    public abstract class SampledMetricDefinitionPacket : MetricDefinitionPacket, IPacket, IComparable<SampledMetricDefinitionPacket>, IEquatable<SampledMetricDefinitionPacket>
    {
        private string m_UnitCaption;

        /// <summary>
        /// Base implementation for creating a sampled metric definition packet
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        protected SampledMetricDefinitionPacket(string metricTypeName, string categoryName, string counterName)
            : base(metricTypeName, categoryName, counterName, SampleType.Sampled)
        {
        }

        /// <summary>
        /// Base implementation for creating a sampled metric definition packet
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="unitCaption">The display caption for the calculated values captured under this metric.</param>
        /// <param name="description">A description of what is tracked by this metric, suitable for end-user display.</param>
        protected SampledMetricDefinitionPacket(string metricTypeName, string categoryName, string counterName, string unitCaption, string description)
            : base(metricTypeName, categoryName, counterName, SampleType.Sampled, description)
        {
            UnitCaption = unitCaption;
        }

        /// <summary>
        /// Create a sampled metric definition packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        protected SampledMetricDefinitionPacket(Session session)
            : base(session)
        {            
        }


        #region Public Properties and Methods

        /// <inheritdoc />
        public int CompareTo(SampledMetricDefinitionPacket other)
        {
            //we just gateway to our base object.
            return base.CompareTo(other);
        }

        /// <summary>
        /// The display caption for the calculated values captured under this metric.
        /// </summary>
        public string UnitCaption
        {
            get
            {
                if(string.IsNullOrEmpty(m_UnitCaption))
                {
                    //A little odd; we're actually going to route this to our setter..
                    UnitCaption = OnUnitCaptionGenerate();
                }

                return m_UnitCaption;
            }
            set
            {
                //We want to get rid of any leading/trailing white space, but make sure they aren't setting us to a null object
                if (string.IsNullOrEmpty(value))
                {
                    m_UnitCaption = value;
                }
                else
                {
                    m_UnitCaption = value.Trim();
                }
            }
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
            return Equals(other as SampledMetricDefinitionPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(SampledMetricDefinitionPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((UnitCaption == other.UnitCaption) && (base.Equals(other)));
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

            if (m_UnitCaption != null) myHash ^= m_UnitCaption.GetHashCode(); // Fold in hash code for string UnitCaption

            return myHash;
        }

        #endregion

        #region Protected Methods and Properties


        /// <summary>
        /// Inheritors will need to implement this to calculate a unit caption when requested.
        /// </summary>
        /// <returns>The caption to display for the units of value.</returns>
        protected abstract string OnUnitCaptionGenerate();

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
            const string typeName = nameof(SampledMetricDefinitionPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("unitCaption", FieldType.String);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("unitCaption", m_UnitCaption);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("unitCaption", out m_UnitCaption);
                    break;
                default:
                    throw new LoupePacketVersionException(definition.Version);
            }
        }
        #endregion
    }
}
