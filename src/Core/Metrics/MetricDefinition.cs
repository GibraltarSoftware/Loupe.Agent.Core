using System;
using System.Diagnostics;
using System.Globalization;
using Loupe.Core.IO.Serialization;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Metrics
{
    /// <summary>
    /// The definition of a single metric that has been captured.  
    /// </summary>
    /// <remarks>
    /// Individual metrics capture a stream of values for a metric definition which can then be displayed and manipulated.
    /// </remarks>
    [DebuggerDisplay("Name: {Name}, Id: {Id}, Caption: {Caption}")]
    public class MetricDefinition : IMetricDefinition, IDisplayable
    {
        private readonly MetricDefinitionCollection m_Definitions;
        private readonly MetricDefinitionPacket m_Packet;
        private readonly MetricCollection m_Metrics;

        private readonly object m_Lock = new object();

        private string[] m_CategoryNames;   //the parsed array of the category name hierarchy, period delimited

        /// <summary>
        /// Create a new metric definition.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will <b>not</b> be automatically added to the provided collection.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricType">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="sampleType">The type of data sampling done for this metric.</param>
        public MetricDefinition(MetricDefinitionCollection definitions, string metricType, string categoryName, string counterName, SampleType sampleType)
            : this(definitions, new MetricDefinitionPacket(metricType, categoryName, counterName, sampleType))
        {
        }

        /// <summary>
        /// Create a new metric definition from the provided metric definition packet.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will <b>not</b> be automatically added to the provided collection.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="packet">The packet to create a definition from.</param>
        internal MetricDefinition(MetricDefinitionCollection definitions, MetricDefinitionPacket packet)
        {
            //make sure our definitions dictionary isn't null
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            //make sure our packet isn't null
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            m_Definitions = definitions;
            m_Packet = packet;

            //and create our metric dictionary
            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            m_Metrics = OnMetricDictionaryCreate();
            // ReSharper restore DoNotCallOverridableMethodsInConstructor

            //finally, auto-add ourself to the definition
            //m_Definitions.Add(this); // Commented out for new EventMetricDefinition protocol with Register().
        }

        #region Public Properties and Methods

        /// <summary>
        /// The unique Id of this metric definition packet.  This can reliably be used as a key to refer to this item.
        /// </summary>
        /// <remarks>The key can be used to compare the same definition across different instances (e.g. sessions).
        /// This Id is always unique to a particular instance.</remarks>
        public Guid Id { get { return m_Packet.ID; } }

        /// <summary>
        /// The name of the metric definition being captured.  
        /// </summary>
        /// <remarks>The name is for comparing the same definition in different sessions. They will have the same name but 
        /// not the same Id.</remarks>
        public string Name { get { return m_Packet.Name; } }

        /// <summary>
        /// A short display string for this metric definition, suitable for end-user display.
        /// </summary>
        public string Caption
        {
            get { return m_Packet.Caption; }
            set { m_Packet.Caption = value; }
        }

        /// <summary>
        /// A description of what is tracked by this metric, suitable for end-user display.
        /// </summary>
        public string Description
        {
            get { return m_Packet.Description; }
            set { m_Packet.Description = value; }
        }

        /// <summary>
        /// The recommended default display interval for graphing. 
        /// </summary>
        public MetricSampleInterval Interval { get { return m_Packet.Interval; } protected set { m_Packet.Interval = value; } }

        /// <summary>
        /// The internal metric type of this metric definition
        /// </summary>
        /// <remarks>Metric types distinguish different metric capture libraries from each other, ensuring
        /// that we can correctly correlate the same metric between sessions and not require category names 
        /// to be globally unique.  If you are creating a new metric, pick your own metric type that will
        /// uniquely identify your library or namespace.</remarks>
        public string MetricTypeName { get { return m_Packet.MetricTypeName; } }

        /// <summary>
        /// The definitions collection that contains this definition.
        /// </summary>
        /// <remarks>This parent pointer should be used when walking from an object back to its parent instead of taking
        /// advantage of the static metrics definition collection to ensure your application works as expected when handling
        /// data that has been loaded from a database or data file.  The static metrics collection is for the metrics being
        /// actively captured in the current process, not for metrics that are being read or manipulated.</remarks>
        public IMetricDefinitionCollection Definitions { get { return m_Definitions; } }

        /// <summary>
        /// The set of metrics that use this definition.
        /// </summary>
        /// <remarks>All metrics with the same definition are of the same object type.</remarks>
        public IMetricCollection Metrics { get { return m_Metrics; } }

        /// <summary>
        /// The category of this metric for display purposes. This can be a period delimited string to represent a variable height hierarchy
        /// </summary>
        public string CategoryName
        {
            get { return m_Packet.CategoryName; }
        }

        /// <summary>
        /// An array of the individual category names within the specified category name which is period delimited.
        /// </summary>
        public string[] CategoryNames
        {
            get
            {
                //have we parsed it yet?  We don't want to do this every time, it ain't cheap.
                if (m_CategoryNames == null)
                {
                    //no.
                    m_CategoryNames = TextParse.CategoryName(CategoryName);
                }

                return m_CategoryNames;
            }
        }

        /// <summary>
        /// The display name of this metric (unique within the category name).
        /// </summary>
        public string CounterName
        {
            get { return m_Packet.CounterName; }
        }

        /// <summary>
        /// The sample type of the metric.  Indicates whether the metric represents discrete events or a continuous value.
        /// </summary>
        public SampleType SampleType
        {
            get { return m_Packet.SampleType; }
        }

        #region IComparable and IEquatable methods

        /// <summary>
        /// Compares this MetricDefinition to another MetricDefinition to determine sort order
        /// </summary>
        /// <remarks>MetricDefinition instances are sorted by their Name property.</remarks>
        /// <param name="other">The MetricDefinition to compare this MetricDefinition against</param>
        /// <returns>An int which is less than zero, equal to zero, or greater than zero to reflect whether
        /// this MetricDefinition should sort as being less-than, equal to, or greater-than the other
        /// MetricDefintion, respectively.</returns>
        public int CompareTo(IMetricDefinition other)
        {
            //our packet knows what to do.
            return m_Packet.CompareTo(((MetricDefinition)other).Packet);
        }

        /// <summary>
        /// Determines if the provided MetricDefinition object is identical to this object.
        /// </summary>
        /// <param name="other">The MetricDefinition object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(IMetricDefinition other)
        {
            // Careful, it could be null; check it without recursion
            if (ReferenceEquals(other, null))
            {
                return false; // Since we're a live object we can't be equal to a null instance.
            }

            //they are the same if their GUID's match
            return (Id == other.Id);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a MetricDefinition and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            MetricDefinition otherMetricDefinition = obj as MetricDefinition;

            return Equals(otherMetricDefinition); // Just have type-specific Equals do the check (it even handles null)
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
        /// An int representing the hash code calculated for the contents of this object.
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = Id.GetHashCode(); // The ID is all that Equals checks!

            return myHash;
        }

        /// <summary>
        /// Compares two MetricDefinition instances for equality.
        /// </summary>
        /// <param name="left">The MetricDefinition to the left of the operator</param>
        /// <param name="right">The MetricDefinition to the right of the operator</param>
        /// <returns>True if the two MetricDefinitions are equal.</returns>
        public static bool operator ==(MetricDefinition left, MetricDefinition right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two MetricDefinition instances for inequality.
        /// </summary>
        /// <param name="left">The MetricDefinition to the left of the operator</param>
        /// <param name="right">The MetricDefinition to the right of the operator</param>
        /// <returns>True if the two MetricDefinitions are not equal.</returns>
        public static bool operator !=(MetricDefinition left, MetricDefinition right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ! ReferenceEquals(right, null);
            }
            return ! left.Equals(right);
        }

        /// <summary>
        /// Compares if one MetricDefinition instance should sort less than another.
        /// </summary>
        /// <param name="left">The MetricDefinition to the left of the operator</param>
        /// <param name="right">The MetricDefinition to the right of the operator</param>
        /// <returns>True if the MetricDefinition to the left should sort less than the MetricDefinition to the right.</returns>
        public static bool operator <(MetricDefinition left, MetricDefinition right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one MetricDefinition instance should sort greater than another.
        /// </summary>
        /// <param name="left">The MetricDefinition to the left of the operator</param>
        /// <param name="right">The MetricDefinition to the right of the operator</param>
        /// <returns>True if the MetricDefinition to the left should sort greater than the MetricDefinition to the right.</returns>
        public static bool operator >(MetricDefinition left, MetricDefinition right)
        {
            return (left.CompareTo(right) > 0);
        }

        #endregion

        /// <summary>
        /// Indicates if the definition is part of the current live metric definition collection
        /// </summary>
        /// <remarks>The same process can be recording metrics and reading metrics from a data source such as a file.  This flag indicates
        /// whether this metric definition is for playback purposes (it represents previously recorded data) or is part of the active
        /// metric capture capability of the current process.</remarks>
        public bool IsLive
        {
            get { return m_Packet.IsLive; }
        }

        /// <summary>
        /// Indicates if the definition can be changed.
        /// </summary>
        /// <remarks>If a metric definition is read-only, that means the definition can't be changed in a way that would invalidate
        /// metrics or metric samples recorded with it.  Display-only values (such as captions and descriptions) can always be changed,
        /// and new metrics can always be added to a metric definition.</remarks>
        public bool IsReadOnly
        {
            get { return m_Packet.IsReadOnly; }
            internal set { m_Packet.IsReadOnly = value; }
        }

        /// <summary>
        /// Set this metric definition to be read-only and lock out further changes, allowing it to be instantiated and sampled.
        /// </summary>
        public virtual void SetReadOnly()
        {
            IsReadOnly = true;
        }

        /// <summary>
        /// Object Change Locking object.
        /// </summary>
        public object Lock { get { return m_Lock; } }        

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Invoked by the base class to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for the metric dictionary in your derived metric, use this
        /// method to create and return your derived object. 
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <returns>The MetricCollection-compatible object.</returns>
        protected virtual MetricCollection OnMetricDictionaryCreate()
        {
            return new MetricCollection(this);
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Calculate the string key for a metric definition.
        /// </summary>
        /// <param name="metric">The existing metric object to generate a string key for</param>
        /// <returns>The unique string key for this item</returns>
        public static string GetKey(IMetric metric)
        {
            //make sure the metric object isn't null
            if (metric == null)
            {
                throw new ArgumentNullException(nameof(metric));
            }

            //We are explicitly NOT passing the instance name here -  we want the key of the DEFINITION.
            return GetKey(metric.MetricTypeName, metric.CategoryName, metric.CounterName);
        }

        /// <summary>
        /// Calculate the string key for a metric definition.
        /// </summary>
        /// <param name="metricDefinition">The existing metric definition object to generate a string key for</param>
        /// <returns>The unique string key for this item</returns>
        public static string GetKey(MetricDefinition metricDefinition)
        {
            //make sure the metric definition object isn't null
            if (metricDefinition == null)
            {
                throw new ArgumentNullException(nameof(metricDefinition));
            }

            return GetKey(metricDefinition.MetricTypeName, metricDefinition.CategoryName, metricDefinition.CounterName);
        }

        /// <summary>
        /// Calculate the string key for a metric.
        /// </summary>
        /// <param name="metricDefinition">The existing metric definition object to generate a string key for</param>
        /// <param name="instanceName">The name of the performance counter category instance, or an empty string (""), if the category contains a single instance.</param>
        /// <returns>The unique string key for this item</returns>
        public static string GetKey(MetricDefinition metricDefinition, string instanceName)
        {
            //make sure the metric definition object isn't null
            if (metricDefinition == null)
            {
                throw new ArgumentNullException(nameof(metricDefinition));
            }

            return GetKey(metricDefinition.MetricTypeName, metricDefinition.CategoryName, metricDefinition.CounterName, instanceName);
        }

        /// <summary>
        /// Calculate the string key for a metric definition.
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <returns>The unique string key for this item</returns>
        /// <exception cref="ArgumentNullException">The provided metricsSystem, categoryName, or counterName was null.</exception>
        public static string GetKey(string metricTypeName, string categoryName, string counterName)
        {
            return GetKey(metricTypeName, categoryName, counterName, null);
        }

        /// <summary>
        /// Calculate the string key for a metric.
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="instanceName">The name of the performance counter category instance, or an empty string (""), if the category contains a single instance.</param>
        /// <returns>The unique string key for this item</returns>
        /// <exception cref="ArgumentNullException">The provided metricsSystem, categoryName, or counterName was null.</exception>
        public static string GetKey(string metricTypeName, string categoryName, string counterName, string instanceName)
        {
            string key;

            if (string.IsNullOrEmpty(metricTypeName))
            {
                throw new ArgumentNullException(nameof(metricTypeName));
            }

            if (string.IsNullOrEmpty(categoryName))
            {
                throw new ArgumentNullException(nameof(categoryName));
            }

            if (string.IsNullOrEmpty(counterName))
            {
                throw new ArgumentNullException(nameof(counterName));
            }

            //we assemble the key by appending the parts of the name of the counter together.  We have to guard for a NULL or EMPTY instance name
            if ((string.IsNullOrEmpty(instanceName)) || (string.IsNullOrEmpty(instanceName.Trim())))
            {
                //there is no instance name - just the first two parts
                key = string.Format(CultureInfo.InvariantCulture, "{0}~{1}~{2}", metricTypeName.Trim(), categoryName.Trim(), counterName.Trim());
            }
            else
            {
                key = string.Format(CultureInfo.InvariantCulture, "{0}~{1}~{2}~{3}", metricTypeName.Trim(), categoryName.Trim(), counterName.Trim(), instanceName.Trim());
            }

            return key;
        }

        /// <summary>
        /// Takes an instance name or complete metric name and normalizes it to a metric name so it can be used to look up a metric
        /// </summary>
        /// <param name="metricDefinition">The metric definition to look for metrics within</param>
        /// <param name="metricKey">The instance name or complete metric name</param>
        /// <returns></returns>
        internal static string NormalizeKey(MetricDefinition metricDefinition, string metricKey)
        {
            string returnVal;
            string trueMetricKey;

            bool prependDefinitionName = false;

            //Did we get a null?  If we got a null, we know we need to pre-pend the definition (and it isn't safe to do any more testing)
            if (metricKey == null)
            {
                prependDefinitionName = true;
                trueMetricKey = null;
            }
            else
            {
                //trim the input for subsequent testing to see what we get
                trueMetricKey = metricKey.Trim();

                if (string.IsNullOrEmpty(trueMetricKey))
                {
                    //we know we need to pre-pend the definition name
                    prependDefinitionName = true;
                }
                else
                {
                    //OK, a true key is a full name, so see if the key we got STARTS with our definition name
                    if (trueMetricKey.Length < metricDefinition.Name.Length)
                    {
                        //the key we got is shorter than the length of the metric definition name, so it can't include the metric definition name.
                        prependDefinitionName = true;
                    }
                    else
                    {
                        //now check the start of the string to see what we get
                        if (trueMetricKey.StartsWith(metricDefinition.Name, StringComparison.Ordinal) == false)
                        {
                            //they aren't the same at least as long as the metric definition name is, so we assume we need to pre-pend.
                            prependDefinitionName = true;
                        }
                    }
                }
            }

            //If the value we got was just the instance name, we need to put the metric definition's key in front of it.
            if (prependDefinitionName)
            {
                returnVal = GetKey(metricDefinition, trueMetricKey);
            }
            else
            {
                returnVal = trueMetricKey;
            }

            return returnVal;
        }

        /// <summary>
        /// The underlying packet 
        /// </summary>
        public MetricDefinitionPacket Packet { get { return m_Packet; } }

        #endregion
    }
}
