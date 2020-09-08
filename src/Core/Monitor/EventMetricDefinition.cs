
using System;
using System.Reflection;
using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility.Data;



namespace Gibraltar.Monitor
{
    /// <summary>
    /// The definition of an event metric, necessary before any specific metric can be created.
    /// </summary>
    /// <remarks>
    /// A sampled metric always has a value for any timestamp between its start and end timestamps.
    /// It presumes any interim value by looking at the best fit sampling of the real world value
    /// and assuming it covers the timestamp in question.  It is therefore said to be contiguous for 
    /// the range of start and end.  Event metrics are only defined at the instant they are timestamped, 
    /// and imply nothing for other timestamps.  
    /// For sampled metrics, use the SampledMetric base class.</remarks>
    public class EventMetricDefinition : MetricDefinition, IEventMetricDefinition
    {
        private readonly EventMetricValueDefinitionCollection m_MetricValues;
        private bool m_Bound;
        private Type m_BoundType;
        private bool m_NameBound;
        private string m_NameMemberName;
        private MemberTypes m_NameMemberType;

        /// <summary>
        /// Create a new event metric definition for the active log.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will <b>not</b> be automatically added to the provided collection.</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        public EventMetricDefinition(string metricTypeName, string categoryName, string counterName)
            : base(Log.Metrics, new EventMetricDefinitionPacket(metricTypeName, categoryName, counterName))
        {
            m_MetricValues = new EventMetricValueDefinitionCollection(this);
            
            //and we need to set that to our packet, all part of our bogus reach-around to make persistence work
            Packet.MetricValues = m_MetricValues;
        }
            
        /// <summary>
        /// Create a new event metric definition.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will <b>not</b> be automatically added to the provided collection.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        public EventMetricDefinition(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName)
            : base(definitions, new EventMetricDefinitionPacket(metricTypeName, categoryName, counterName))
        {
            m_MetricValues = new EventMetricValueDefinitionCollection(this);

            //and we need to set that to our packet, all part of our bogus reach-around to make persistence work
            Packet.MetricValues = m_MetricValues;
        }


        /// <summary>
        /// Create a new event metric object from the provided raw data packet
        /// </summary>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="packet">The packet to create a definition from.</param>
        internal EventMetricDefinition(MetricDefinitionCollection definitions, MetricDefinitionPacket packet)
            : base(definitions, packet)
        {
            m_MetricValues = new EventMetricValueDefinitionCollection(this);

            //and we need to set that to our packet, all part of our bogus reach-around to make persistence work
            Packet.MetricValues = m_MetricValues;
        }

        #region Public Properties and Methods

        /// <summary>Creates a new metric definition from the provided information, or returns an existing matching definition if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Custom Sampled Metric (or a derived class) an exception will be thrown.
        /// Definitions are looked up and added to the provided definitions dictionary.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        public static EventMetricDefinition AddOrGet(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName)
        {
            //we must have a definitions collection, or we have a problem
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            //we need to find the definition, adding it if necessary
            string definitionKey = GetKey(metricTypeName, categoryName, counterName);
            IMetricDefinition definition;

            //We need to grab a lock so our try get & the create are done as one lock.
            lock (definitions.Lock)
            {
                if (definitions.TryGetValue(definitionKey, out definition))
                {
                    //if the metric definition exists, but is of the wrong type we have a problem.
                    if ((definition is CustomSampledMetricDefinition) == false)
                    {
                        throw new ArgumentException("A metric already exists with the provided type, category, and counter name but it is not compatible with being an event metric.  Please use a different counter name.", nameof(counterName));
                    }
                }
                else
                {
                    //we didn't find one, make a new one
                    definition = new EventMetricDefinition(definitions, metricTypeName, categoryName, counterName);
                    definitions.Add(definition); // Add it to the collection, no longer done in the constructor.
                    // ToDo: Reconsider this implementation; putting incomplete event metric definitions in the collection is not ideal.
                }
            }
            return (EventMetricDefinition)definition;
        }

        /// <summary>Creates a new metric definition from the provided information, or returns an existing matching definition if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.
        /// If the metric definition does exist, but is not an Event Metric an exception will be thrown.
        /// Definitions are looked up and added to the active logging metrics collection (Log.Metrics)</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        public static EventMetricDefinition AddOrGet(string metricTypeName, string categoryName, string counterName)
        {
            //just forward into our call that requires the definition to be specified
            return AddOrGet(Log.Metrics, metricTypeName, categoryName, counterName);
        }

        /// <summary>
        /// Register this instance as a completed definition and return the valid usable definition for this event metric.
        /// </summary>
        /// <remarks>This call is necessary to complete a new event metric definition (created by calls to AddValue) before
        /// it can be used, and it signifies that all desired value columns have been added to the definition.  Only the
        /// first registration of a metric definition with a given Key (metrics system, category name, and counter name)
        /// will be effective and return the same definition object; subsequent calls (perhaps by another thread) will
        /// instead return the existing definition already registered.  If a definition already registered with that Key
        /// can not be an event metric (e.g. a sampled metric is defined with that Key) or if this instance defined value
        /// columns not present as compatible value columns in the existing registered definition with that Key, then an
        /// ArgumentException will be thrown to signal your programming mistake.</remarks>
        /// <returns>The actual usable definition with the same metrics system, category name, and counter name as this instance.</returns>
        public EventMetricDefinition Register()
        {
            EventMetricDefinition officialDefinition;
            MetricDefinitionCollection definitionCollection = (MetricDefinitionCollection)Definitions;

            // We need to lock the collection while we check for an existing definition and maybe add this one to it.
            lock (definitionCollection.Lock)
            {
                IMetricDefinition rawDefinition;
                if (definitionCollection.TryGetValue(MetricTypeName, CategoryName, CounterName, out rawDefinition) == false)
                {
                    // There isn't already one by that Key.  Great!  Register ourselves.
                    SetReadOnly(); // Mark this definition as completed.
                    officialDefinition = this;
                    definitionCollection.Add(this);
                }
                else
                {
                    // Oooh, we found one already registered.  We'll want to do some checking on this, but outside the lock.
                    officialDefinition = rawDefinition as EventMetricDefinition;
                }
            } // End of collection lock

            if (officialDefinition == null)
            {
                throw new ArgumentException(
                    string.Format(
                        "There is already a metric definition for the same metrics system ({0}), category name ({1}), and counter name ({2}), but it is not an event metric.",
                        MetricTypeName, CategoryName, CounterName));
            }
            else if (this != officialDefinition)
            {
                // There was one other than us, make sure it's compatible with us.
                IEventMetricValueDefinitionCollection officialValues = officialDefinition.Values;
                foreach (EventMetricValueDefinition ourValue in Values)
                {
                    IEventMetricValueDefinition officialValue;
                    if (officialValues.TryGetValue(ourValue.Name, out officialValue) == false)
                    {
                        // It doesn't have one of our value columns!
                        throw new ArgumentException(
                            string.Format(
                                "There is already an event metric definition for the same metrics system ({0}), category name ({1}), and counter name ({2}), but it is not compatible; it does not define value column \"{3}\".",
                                MetricTypeName, CategoryName, CounterName, ourValue.Name));
                    }
                    else if (ourValue.SerializedType != ((EventMetricValueDefinition)officialValue).SerializedType)
                    {
                        throw new ArgumentException(
                            string.Format(
                                "There is already an event metric definition for the same metrics system ({0}), category name ({1}), and counter name ({2}), but it is not compatible; " +
                                "it defines value column \"{3}\" with type {4} rather than type {5}.",
                                MetricTypeName, CategoryName, CounterName, ourValue.Name, officialValue.Type.Name, ourValue.Type.Name));
                    }
                }

                // We got through all the values defined in this instance?  Then we're okay to return the official one.
            }
            // Otherwise, it's just us, so we're all good.

            return officialDefinition;
        }

        /// <summary>
        /// Indicates if this definition is configured to retrieve its information directly from an object.
        /// </summary>
        /// <remarks>When true, metric instances and samples can be defined from a live object of the same type that was used 
        /// to generate the data binding.  It isn't necessary that the same object be used, just that it be a compatible
        /// type to the original type used to establish the binding.</remarks>
        public bool IsBound
        {
            get { return m_Bound; }
            set { m_Bound = value; }
        }

        /// <summary>
        /// When bound, indicates the exact interface or object type that was bound.
        /// </summary>
        /// <remarks>When creating new metrics or metric samples, this data type must be provided in bound mode.</remarks>
        public Type BoundType
        {
            get { return m_BoundType; }
            set { m_BoundType = value; }
        }


        /// <summary>
        /// Indicates the relative sort order of this object to another of the same type.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(IEventMetricDefinition other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.CompareTo(other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(IEventMetricDefinition other)
        {
            //We're really just a type cast, refer to our base object
            return base.Equals(other);
        }

        
        /// <summary>
        /// The set of metrics that use this definition.
        /// </summary>
        /// <remarks>All metrics with the same definition are of the same object type.</remarks>
        public new EventMetricCollection Metrics { get { return (EventMetricCollection)base.Metrics; } }

        /// <summary>
        /// The set of values defined for this metric definition
        /// </summary>
        /// <remarks>Any number of different values can be recorded along with each event to provide additional trends and filtering ability
        /// for later client analysis.</remarks>
        public IEventMetricValueDefinitionCollection Values { get { return m_MetricValues; } }

        /// <summary>
        /// Indicates whether the provided object can be graphed as a trend.
        /// </summary>
        /// <param name="type">The type to be verified</param>
        /// <returns>True if the supplied type is trendable, false otherwise.</returns>
        public static bool IsTrendableValueType(Type type)
        {
            bool trendable = false;

            //we're using Is so we can check for compatible types, not just base types.
            if ((type == typeof(short)) || (type == typeof(ushort)) || (type == typeof(int)) || (type == typeof(uint)) || (type == typeof(long)) || (type == typeof(ulong)) ||
                (type == typeof(decimal)) || (type == typeof(double)) || (type == typeof(float)))
            {
                trendable = true;
            }
            //Now check object types
            else if ((type == typeof(DateTimeOffset)) || (type == typeof(TimeSpan)))
            {
                trendable = true;
            }

            return trendable;
        }

        /// <summary>
        /// Indicates whether the provided type can be stored as a value or not.
        /// </summary>
        /// <remarks>Most types can be stored, with the value being the string representation of the type.
        /// Collections, arrays, and other such sets can't be stored as a single value.</remarks>
        /// <param name="type">The type to be verified</param>
        /// <returns>True if the supplied type is supported, false otherwise.</returns>
        public static bool IsSupportedValueType(Type type)
        {
            //We can support any true type by ToString if nothing else, so really we're trying to get rid of
            //things that aren't single objects
            bool supportedType = true;

            //verify if the type is an array or an interface 
            if ((type.IsArray) || (type.GetTypeInfo().IsInterface))
            {
                //no go!
                supportedType = false;
            }

            return supportedType;
        }

        /// <summary>
        /// The default value to display for this event metric.  Typically this should be a trendable value.
        /// </summary>
        public IEventMetricValueDefinition DefaultValue
        {
            get
            {
                return ((string.IsNullOrEmpty(Packet.DefaultValueName)) ? null : Values[Packet.DefaultValueName]);
            }
            //We do set in a round-the-world fashion to guarantee that the provided default value's name is in our collection.
            set
            {
                Packet.DefaultValueName = ((value == null) ? null : Values[value.Name].Name);
            }
        }

        /// <summary>
        /// Indicates if there is a binding for metric instance name.
        /// </summary>
        /// <remarks>When true, the Name Member Name and Name Member Type properties are available.</remarks>
        public bool NameBound
        {
            get { return m_NameBound; }
            set { m_NameBound = value; }
        }

        /// <summary>
        /// The name of the member to invoke to determine the metric instance name.
        /// </summary>
        /// <remarks>This property is only valid when NameBound is true.</remarks>
        public string NameMemberName
        {
            get { return m_NameMemberName; }
            set { m_NameMemberName = value; }
        }

        /// <summary>
        /// The type of the member to be invoked to deterine the metric instance name (field, method, or property)
        /// </summary>
        /// <remarks>This property is only valid when NameBound is true.</remarks>
        public MemberTypes NameMemberType
        {
            get { return m_NameMemberType; }
            set { m_NameMemberType = value; }
        }

        /// <summary>
        /// Set this metric definition to be read-only and lock out further changes, allowing it to be instantiated and sampled.
        /// </summary>
        public override void SetReadOnly()
        {
            lock (Lock)
            {
                base.SetReadOnly();
                m_MetricValues.SetAllIndex();
            }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The internal event metric definition packet
        /// </summary>
        internal new EventMetricDefinitionPacket Packet { get { return (EventMetricDefinitionPacket)base.Packet; } }

        #endregion

        #region Base Object Overrides

        /// <summary>
        /// Create a metric dictionary in our derived type for highest type fidelity.
        /// </summary>
        /// <returns>An event metric dictionary object for this event metric definition.</returns>
        protected override MetricCollection OnMetricDictionaryCreate()
        {
            return new EventMetricCollection(this);
        }

        #endregion
    }
}
