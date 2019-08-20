using System;
using System.Globalization;
using Loupe.Core.Data;
using Loupe.Core.Metrics;
using Loupe.Core.Serialization;
using Loupe.Extensibility.Data;

#pragma warning disable 1591
namespace Loupe.Core.IO.Serialization
{
    /// <summary>
    /// Defines a metric that has been captured.  Specific metrics extend this class.
    /// Each time a metric is captured, a MetricSample is recorded.
    /// </summary>
    public class MetricDefinitionPacket : GibraltarCachedPacket, IPacket, IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>, IComparable<MetricDefinitionPacket>, IEquatable<MetricDefinitionPacket>, IDisplayable
    {
        /// <summary>
        /// A global default sampling interval for display if no-one attempts to override it
        /// </summary>
        private const MetricSampleInterval DefaultInterval = MetricSampleInterval.Minute;

        //our metric definition data (this gets written out)

        private string m_Name;
        private MetricSampleInterval m_Interval;
        private string m_MetricTypeName;
        private string m_CategoryName;
        private string m_CounterName;
        private bool m_Persisted;
        private bool m_IsLive;
        private SampleType m_SampleType;
        private string m_Caption;
        private string m_Description;

        //internal tracking information (this does NOT get written out)
        private Session m_Session;

        private bool m_ReadOnly;

        /// <summary>
        /// Create a new metric definition packet.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="sampleType">The type of data sampling done for this metric.</param>
        /// <param name="description">A description of what is tracked by this metric, suitable for end-user display.</param>
        public MetricDefinitionPacket(string metricTypeName, string categoryName, string counterName, SampleType sampleType, string description)
            : this(metricTypeName, categoryName, counterName, sampleType)
        {
            Description = description;
        }

        /// <summary>
        /// Create a new metric definition packet.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="sampleType">The type of data sampling done for this metric.</param>
        public MetricDefinitionPacket(string metricTypeName, string categoryName, string counterName, SampleType sampleType)
            : base(false)
        {
            //verify our input
            if ((string.IsNullOrEmpty(metricTypeName)) || (string.IsNullOrEmpty(metricTypeName.Trim())))
            {
                throw new ArgumentNullException(nameof(metricTypeName));
            }
            if ((string.IsNullOrEmpty(categoryName)) || (string.IsNullOrEmpty(categoryName.Trim())))
            {
                throw new ArgumentNullException(nameof(categoryName));
            }
            if ((string.IsNullOrEmpty(counterName)) || (string.IsNullOrEmpty(counterName.Trim())))
            {
                throw new ArgumentNullException(nameof(counterName));
            }

            //we require a type, category, and counter name, which is checked by GetKey.
            MetricTypeName = metricTypeName.Trim();
            CategoryName = categoryName.Trim();
            CounterName = counterName.Trim();
            SampleType = sampleType;

            Name = MetricDefinition.GetKey(metricTypeName, categoryName, counterName); //generate the name
            m_Caption = string.Format(CultureInfo.CurrentCulture, "{0} - {1}", categoryName, counterName); //make an attempt to generate a plausible caption

            Interval = DefaultInterval;

            Persisted = false; // we haven't been written to the log yet.

            IsLive = true;  //and we're live - if we were from another source, this constructor wouldn't have been called.
        }

        /// <summary>
        /// Create an event metric definition packet for rehydration
        /// </summary>
        /// <param name="session"></param>
        public MetricDefinitionPacket(Session session)
            : base(false)
        {  
            //verify our input
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            m_Session = session;
        }


        #region Public Properties and Methods


        /// <summary>
        /// The name of the metric definition being captured.  
        /// </summary>
        /// <remarks>The name is for comparing the same definition in different sessions. They will have the same name but 
        /// not the same Id.</remarks>
        public string Name { get { return m_Name; } private set { m_Name = value; } }

        /// <summary>
        /// A short display string for this metric definition, suitable for end-user display.
        /// </summary>
        public string Caption
        {
            get
            {
                //if we're null, we're going to use the Name property instead
                if (string.IsNullOrEmpty(m_Caption))
                {
                    //we call our own set here.
                    Caption = Name;
                }
                return m_Caption;
            }
            set
            {
                //We want to get rid of any leading/trailing white space, but make sure they aren't setting us to a null object
                if (string.IsNullOrEmpty(value))
                {
                    m_Caption = value;
                }
                else
                {
                    m_Caption = value.Trim();
                }
            }
        }

        /// <summary>
        /// A description of what is tracked by this metric, suitable for end-user display.
        /// </summary>
        public string Description
        {
            get
            {
                return m_Description;
            }
            set
            {
                //We want to get rid of any leading/trailing white space, but make sure they aren't setting us to a null object
                if (string.IsNullOrEmpty(value))
                {
                    m_Description = value;
                }
                else
                {
                    m_Description = value.Trim();
                }
            }
        }


        /// <summary>
        /// The recommended default display interval for graphing. 
        /// </summary>
        public MetricSampleInterval Interval { get { return m_Interval; } set { m_Interval = value; } }


        /// <summary>
        /// The internal metric type of this metric definition
        /// </summary>
        /// <remarks>Metric types distinguish different metric capture libraries from each other, ensuring
        /// that we can correctly correlate the same metric between sessions and not require category names 
        /// to be globally unique.  If you are creating a new metric, pick your own metric type that will
        /// uniquely idenify your library or namespace.</remarks>
        public string MetricTypeName { get { return m_MetricTypeName; } private set { m_MetricTypeName = value; } }


        /// <summary>
        /// The category of this metric for display purposes.  Category is the top displayed hierarchy.
        /// </summary>
        public string CategoryName { get { return m_CategoryName; } private set { m_CategoryName = value; } }


        /// <summary>
        /// The display name of this metric (unique within the category name).
        /// </summary>
        public string CounterName { get { return m_CounterName; } private set { m_CounterName = value; } }


        /// <summary>
        /// Indicates whether the metric packet has been written to the log stream yet.
        /// </summary>
        public Boolean Persisted { get { return m_Persisted; } private set { m_Persisted = value; } }

        /// <summary>
        /// Indicates if the definition (and all metrics associated with it) are read-only or can be read/write.
        /// </summary>
        /// <remarks>If a metric definition is read-only, all metrics associated with it are read-only, however it's possible for some child
        /// objects to be read-only even if a definition is not.  When read only, no new metrics can be added however display values can be changed.</remarks>
        public bool IsReadOnly
        {
            get { return m_ReadOnly; }
            internal set
            {
                //this is really a latch
                if (value)
                {
                    m_ReadOnly = true;
                }
            }
        }


        /// <summary>
        /// Indicates if the definition is part of the current live metric definitino collection
        /// </summary>
        /// <remarks>The same process can be recording metrics and reading metrics from a data source such as a file.  This flag indiciates
        /// whether this metric definition is for playback purposes (it represents previously recorded data) or is part of the active
        /// metric capture capability of the current process.</remarks>
        public bool IsLive { get { return m_IsLive; } private set { m_IsLive = value; } }


        /// <summary>
        /// The sample type of the metric.  Indicates whether the metric represents discrete events or a continuous value.
        /// </summary>
        public SampleType SampleType { get { return m_SampleType; } private set { m_SampleType = value; } }

        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <param name="other">The object to compare this object with.</param>
        /// <returns>Zero if the objects are equal, less than zero if this object is less than the other, more than zero if this object is more than the other.</returns>
        public int CompareTo(MetricDefinitionPacket other)
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
            return Equals(other as MetricDefinitionPacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(MetricDefinitionPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((MetricTypeName == other.MetricTypeName)
                 && (CategoryName == other.CategoryName)
                 && (CounterName == other.CounterName)
                 && (SampleType == other.SampleType)
                 && (Caption == other.Caption)
                 && (Description == other.Description)
                 && (Interval == other.Interval)
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

            if (m_MetricTypeName != null) myHash ^= m_MetricTypeName.GetHashCode(); // Fold in hash code for string MetricTypeName
            if (m_CategoryName != null) myHash ^= m_CategoryName.GetHashCode(); // Fold in hash code for string CategoryName
            if (m_CounterName != null) myHash ^= m_CounterName.GetHashCode(); // Fold in hash code for string CounterName
            if (m_Caption != null) myHash ^= m_Caption.GetHashCode(); // Fold in hash code for string Caption
            if (m_Description != null) myHash ^= m_Description.GetHashCode(); // Fold in hash code for string Description

            // Note: Name is not checked in Equals, so it can't be in hash, but Name is constructed from other fields anyway
            // Not bothering with SampleType and ...Interval members

            return myHash;
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
            const string typeName = nameof(MetricDefinitionPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("MetricTypeName", FieldType.String);
            definition.Fields.Add("CategoryName", FieldType.String);
            definition.Fields.Add("CounterName", FieldType.String);
            definition.Fields.Add("SampleType", FieldType.Int32);
            definition.Fields.Add("Caption", FieldType.String);
            definition.Fields.Add("Description", FieldType.String);
            definition.Fields.Add("Interval", FieldType.Int32);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("MetricTypeName", m_MetricTypeName);
            packet.SetField("CategoryName", m_CategoryName);
            packet.SetField("CounterName", m_CounterName);
            packet.SetField("SampleType", (int)m_SampleType);
            packet.SetField("Caption", m_Caption);
            packet.SetField("Description", m_Description);
            packet.SetField("Interval", (int)m_Interval);

            //and now we HAVE persisted
            Persisted = true;
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("MetricTypeName", out m_MetricTypeName);
                    packet.GetField("CategoryName", out m_CategoryName);
                    packet.GetField("CounterName", out m_CounterName);

                    packet.GetField("SampleType", out int rawSampleType);
                    m_SampleType = (SampleType)rawSampleType;

                    packet.GetField("Caption", out m_Caption);
                    packet.GetField("Description", out m_Description);

                    packet.GetField("Interval", out int rawInterval);
                    m_Interval = (MetricSampleInterval)rawInterval;

                    //and our stuff that we have to calculate
                    Name = MetricDefinition.GetKey(MetricTypeName, CategoryName, CounterName); //generate the name

                    m_ReadOnly = true;  //if we got read out of a file, we're read only.
                    break;
                default:
                    throw new LoupePacketVersionException(definition.Version);
            }

            //we are NOT live - we came from a serialization reader
            IsLive = false;
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

        #region IPacketObjectFactory<Metric, object> Members

        MetricDefinition IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>.GetDataObject(MetricDefinitionCollection optionalParent)
        {
            //we don't implement this; our derived class always should.
            throw new NotSupportedException();
        }

        #endregion
    }
}
