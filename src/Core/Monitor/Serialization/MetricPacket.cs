using System;
using System.Diagnostics;
using System.Globalization;
using Loupe.Serialization;

namespace Loupe.Monitor.Serialization
{
    /// <summary>
    /// Defines a metric that has been captured.  Specific metrics extend this class.
    /// Each time a metric is captured, a MetricSample is recorded.
    /// </summary>
    public abstract class MetricPacket : GibraltarCachedPacket, IPacket, IPacketObjectFactory<Metric, MetricDefinition>, IComparable<MetricPacket>, IEquatable<MetricPacket>, IDisplayable
    {
        //our metric definition data (this gets written out)
        private Guid m_DefinitionId;
        private string m_InstanceName;

        //internal tracking information (this does NOT get written out)
        private readonly Session m_Session; //only used when we're rehydrating
        private MetricDefinitionPacket m_MetricDefinitionPacket;   //we just persist the ID when we get around to this.
        private string m_Name;
        private bool m_Persisted;


        /// <summary>
        /// Create a new metric packet with the specified unique name.
        /// </summary>
        /// <remarks>At any one time there should only be one metric with a given name.  
        /// This name is used to correlate metrics between sessions.</remarks>
        /// <param name="metricDefinitionPacket">The metric definition to create a metric instance for.</param>
        /// <param name="instanceName">The name of the metric instance, or an empty string ("") to create a default instance.</param>
        protected MetricPacket(MetricDefinitionPacket metricDefinitionPacket, string instanceName)
            : base(false)
        {
            //verify our input.  instance name can be null or an empty string; we'll coalesce all those cases to null
            if (metricDefinitionPacket == null)
            {
                throw new ArgumentNullException(nameof(metricDefinitionPacket));
            }

            DefinitionId = metricDefinitionPacket.ID; //it's really important we set this and not rely on people just picking up the metric packet for some of our other code
            
            if (string.IsNullOrEmpty(instanceName) == false) 
                InstanceName = instanceName.Trim();

            //force empty strings to null.
            if (string.IsNullOrEmpty(InstanceName)) InstanceName = null;

            //process setting our definition through the common routine.  This has to be AFTER we set our definition ID and instance name above.
            DefinitionPacket = metricDefinitionPacket;
            
            Persisted = false; // we haven't been written to the log yet.
        }

        /// <summary>
        /// Create a new metric packet to be rehydrated by packet reader
        /// </summary>
        /// <param name="session">The session to look up the metric definition in</param>
        protected MetricPacket(Session session)
            : base(false)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            m_Session = session;
        }

        #region Public Properties and Methods


        /// <summary>
        /// The unique Id of the metric definition.
        /// </summary>
        public Guid DefinitionId { get { return m_DefinitionId; } private set { m_DefinitionId = value; } }


        /// <summary>
        /// The name of the metric being captured.  
        /// </summary>
        /// <remarks>The name is for comparing the same metric in different sessions. They will have the same name but 
        /// not the same Id.</remarks>
        public string Name { get { return m_Name; } private set { m_Name = value; } }

        /// <summary>
        /// A short display string for this metric packet.
        /// </summary>
        public virtual string Caption 
        {
            get
            {
                //if our caller didn't override us, we're going to do a best effort caption generation.
                string caption;

                //If there is no instance name, just use the name of the definition (this is common - default instances won't have a name)
                if (string.IsNullOrEmpty(InstanceName))
                {
                    caption = m_MetricDefinitionPacket.Caption;
                }
                else
                {
                    //If there is an instance name, prepend the caption definition if available.
                    if (string.IsNullOrEmpty(m_MetricDefinitionPacket.Caption))
                    {
                        caption = InstanceName;
                    }
                    else
                    {
                        caption = string.Format(CultureInfo.CurrentCulture, "{0} - {1}", m_MetricDefinitionPacket.Caption, InstanceName);
                    }
                }

                return caption;
            }
        }

        /// <summary>
        /// The metric definition's description.
        /// </summary>
        public virtual string Description 
        { 
            get
            {
                return m_MetricDefinitionPacket.Description;
            } 
        }


        /// <summary>
        /// The metric instance name (unique within the counter name).
        /// May be null or empty if no instance name is required.
        /// </summary>
        public string InstanceName { get { return m_InstanceName; } private set { m_InstanceName = value; } }


        /// <summary>
        /// Indicates whether the metric packet has been written to the log stream yet.
        /// </summary>
        public Boolean Persisted { get { return m_Persisted; } private set { m_Persisted = value; } }


        /// <summary>
        /// Compare this object to another to determine sort order
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(MetricPacket other)
        {
            //quick identity comparison based on guid
            if (ID == other.ID)
            {
                return 0;
            }

            //Now we try to stort by name.  We already guard against uniqueness
            int compareResult = string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);

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
            return Equals(other as MetricPacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(MetricPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((InstanceName == other.InstanceName)
                 && (DefinitionId == other.DefinitionId)
                 && (base.Equals(other)));
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

            if (m_InstanceName != null) myHash ^= m_InstanceName.GetHashCode(); // Fold in hash code for InstanceName
            myHash ^= m_DefinitionId.GetHashCode(); // Fold in hash code for DefinitionID

            return myHash;
        }


        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The current metric definition packet.  Setting to null is not allowed.
        /// </summary>
        internal MetricDefinitionPacket DefinitionPacket
        {
            get
            {
                return m_MetricDefinitionPacket;
            }
            private set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                //we have to already have a definition ID, and it better match
                if (value.ID != DefinitionId)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "The definition packet object provided is not the same as the definition of this metric packet based on comparing Id's");
                }

                m_MetricDefinitionPacket = value;
                Name = MetricDefinition.GetKey(m_MetricDefinitionPacket.MetricTypeName, m_MetricDefinitionPacket.CategoryName, m_MetricDefinitionPacket.CounterName, InstanceName); //generate the name
            }
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
            //a metric depends on its metric definition
#if DEBUG
            Debug.Assert(m_MetricDefinitionPacket != null);
#endif
            return new IPacket[] {m_MetricDefinitionPacket};
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(MetricPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, true);

            definition.Fields.Add("instanceName", FieldType.String);
            definition.Fields.Add("definitionId", FieldType.Guid);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("instanceName", m_InstanceName);
            packet.SetField("definitionId", m_DefinitionId);

            //and now we HAVE persisted
            Persisted = true;
        
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("instanceName", out m_InstanceName);
                    packet.GetField("definitionId", out m_DefinitionId);
                    break;
                default:
                    throw new GibraltarPacketVersionException(definition.Version);
            }

            //find our definition from the definitions on the session
            DefinitionPacket = ((MetricDefinition)m_Session.MetricDefinitions[DefinitionId]).Packet;
        }

        #endregion

        #region IPacketObjectFactory<Metric, MetricDefinition> Members

        Metric IPacketObjectFactory<Metric, MetricDefinition>.GetDataObject(MetricDefinition optionalParent)
        {
            //we don't implement this; our derived class always should.
            throw new NotSupportedException();
        }

        #endregion
    }
}
