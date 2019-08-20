using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using Loupe.Core.Messaging;
using Loupe.Core.Serialization;
#pragma warning disable 1591

namespace Loupe.Core.Monitor.Serialization
{
    /// <summary>
    /// Base object for all metric sample packets
    /// </summary>
    /// <remarks>A metric sample packet is the persistable form of a single metric sample.
    /// This is the base class; inherit from either SampledMetricSamplePacket for a sampled metric or EventMetricSamplePacket for an event metric, or
    /// a further downstream object as appropriate.</remarks>
    public abstract class MetricSamplePacket : GibraltarPacket, IUserPacket, IPacket, IPacketObjectFactory<MetricSample, Metric>, IComparable<MetricSamplePacket>, IEquatable<MetricSamplePacket>, IDisplayable
    {
        private MetricPacket m_MetricPacket;
        private Guid m_ID;
        private Guid m_MetricId;

        //internal tracking information (this does NOT get written out)
        private readonly Session m_Session;

        /// <summary>
        /// Create a new metric sample for the provided metric.
        /// </summary>
        /// <param name="metric">The metric this sample applies to</param>
        protected MetricSamplePacket(Metric metric)
        {
            if (metric == null)
            {
                throw new ArgumentNullException(nameof(metric));
            }

            ID = Guid.NewGuid();
            m_MetricPacket = metric.Packet;

            Persisted = false;
        }

        protected MetricSamplePacket(Session session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            m_Session = session;
        }

        #region Public Properties and Methods


        /// <summary>
        /// The globally unique Id if this metric sample packet.
        /// </summary>
        public Guid ID { get { return m_ID; } private set { m_ID = value; } }


        /// <summary>
        /// The display caption of the metric this sample is for.
        /// </summary>
        public virtual string Caption
        {
            get { return m_MetricPacket.Caption; }
        }

        /// <summary>
        /// The description of the metric this sample is for.
        /// </summary>
        public virtual string Description
        {
            get { return m_MetricPacket.Description; }
        }

        /// <summary>
        /// Optional.  Extended user information related to this message
        /// </summary>
        public ApplicationUserPacket UserPacket { get; set; }

        /// <summary>
        /// Optional.  The raw user principal, used for deferred user lookup
        /// </summary>
        public IPrincipal Principal { get; set; }

        /// <summary>
        /// The unique Id of the metric we are associated with.
        /// </summary>
        internal Guid MetricId { get { return m_MetricId; } private set { m_MetricId = value; } }

        /// <summary>
        /// The performance counter metric packet this sample is for.
        /// </summary>
        public MetricPacket MetricPacket
        {
            get { return m_MetricPacket; }
            internal set
            {
                //make sure the packet has the same Guid as our current GUID so the user isn't pulling a funny one
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if ((MetricId != Guid.Empty) && (value.ID != MetricId))
                {
                    throw new ArgumentException("The provided metric packet doesn't have the same ID as the metric packet ID already stored. This indicates the data would be inconsistent.");
                }

                m_MetricPacket = value;

                if (MetricId == Guid.Empty)
                {
                    //we are getting the packet set and our ID is empty (shouldn't actually happen, but we've guarded for that, so make it work)
                    MetricId = m_MetricPacket.ID;
                }
            }
        }

        /// <summary>
        /// Indicates whether the metric packet has been written to the log stream yet.
        /// </summary>
        public Boolean Persisted { get; private set; }
        
        public override string ToString()
        {
            string text = string.Format(CultureInfo.CurrentCulture, "{0:d} {0:t}: {1}", Timestamp, Caption);
            return text;
        }

        public int CompareTo(MetricSamplePacket other)
        {
            //First do a quick match on Guid.  this is the only case we want to return zero (an exact match)
            if (ID == other.ID)
            {
                return 0;
            }

            //now we want to sort by our nice increasing sequence #
            int compareResult = Sequence.CompareTo(other.Sequence);

#if DEBUG
            Debug.Assert(compareResult != 0); //no way we should ever get an equal at this point.
#endif

            return compareResult;
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
            return Equals(other as MetricSamplePacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(MetricSamplePacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((ID == other.ID)
                 && (((m_MetricPacket == null) && (other.MetricPacket == null)) 
                    || (((m_MetricPacket != null) && (other.MetricPacket != null)) && (m_MetricPacket.ID == other.MetricPacket.ID)))
                 && (base.Equals(other)));
            // Bug: (?) Should Equals also be comparing on MetricID field?
            // Note: I wonder if we should be digging into MetricPacket.ID fields directly like this
            // or if it would be better to invoke m_MetricPacket.Equals(other.MetricPacket) (but less efficient?)
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

            myHash ^= m_ID.GetHashCode(); // Fold in hash code for GUID field ID
            if (m_MetricPacket != null) myHash ^= m_MetricPacket.GetHashCode(); // Fold in hash code for the MetricPacket member

            // Other fields aren't used in Equals, so we must not use them in hash code calculation

            return myHash;
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// The current session, only available for rehydrated packets
        /// </summary>
        protected Session Session
        {
            get
            {
                if (m_Session == null)
                {
                    throw new InvalidOperationException("There is no session object available, it is only valid during rehydration.");
                }

                return m_Session;
            }
        }

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
            //we depend on our metric
#if DEBUG
            Debug.Assert(m_MetricPacket != null);
#endif
            return new IPacket[] {m_MetricPacket};
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(MetricSamplePacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, true);

            definition.Fields.Add("Id", FieldType.Guid);
            definition.Fields.Add("metricPacketId", FieldType.Guid);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("Id", m_ID);
            packet.SetField("metricPacketId", m_MetricPacket.ID);

            //and now we HAVE persisted
            Persisted = true;

        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("Id",out  m_ID);
                    packet.GetField("metricPacketId", out m_MetricId);
                    break;
                default:
                    throw new LoupePacketVersionException(definition.Version);
            }
        }

        #endregion

        #region IPacketObjectFactory<MetricSample,Metric> Members

        MetricSample IPacketObjectFactory<MetricSample, Metric>.GetDataObject(Metric optionalParent)
        {
            //This is actually correct- we never want our version to be used, this interface should always be implemented by our inheritors.
            throw new NotSupportedException();
        }

        #endregion
    }
}
