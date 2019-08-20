using System;
using System.Collections;
using System.Collections.Generic;



namespace Loupe.Agent.Metrics
{
    /// <summary>
    /// The collection of event metrics for a given event metric definition.
    /// </summary>
    internal class EventMetricCollection
    {
        private readonly Core.Monitor.EventMetricCollection m_WrappedCollection;
        private readonly Dictionary<Core.Monitor.EventMetric, EventMetric> m_Externalized = new Dictionary<Core.Monitor.EventMetric, EventMetric>();
        private readonly EventMetricDefinition m_MetricDefinition;
        private readonly object m_Lock = new object();

        /// <summary>
        /// Create a new event metric dictionary for the provided definition.
        /// </summary>
        /// <remarks>This dictionary is created automatically by the Custom Sampled Metric Definition during its initialization.</remarks>
        /// <param name="metricDefinition">The definition of the event metric to create a metric dictionary for</param>
        public EventMetricCollection(EventMetricDefinition metricDefinition)
        {
            m_MetricDefinition = metricDefinition;
            m_WrappedCollection = metricDefinition.WrappedDefinition.Metrics;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(Guid key)
        {
            //gateway to our inner dictionary 
            return m_WrappedCollection.ContainsKey(key);
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            //gateway to our alternate inner dictionary
            return m_WrappedCollection.ContainsKey(key);
        }

        /// <summary>
        /// Create a new metric object with the provided instance name and add it to the collection
        /// </summary>
        /// <param name="instanceName">The instance name to use, or blank or null for the default metric.</param>
        /// <returns>The new metric object that was added to the collection</returns>
        public EventMetric Add(string instanceName)
        {
            lock (m_Lock)
            {
                //Create a new metric object with the provided instance name (it will get added to us automatically)
                EventMetric newMetric = new EventMetric(m_MetricDefinition, instanceName);

                //finally, return the newly created metric object to our caller
                return newMetric;
            }
        }

        /*
        /// <summary>Creates a new metric instance or returns an existing one by inspecting the provided object for EventMetricDefinition attributes.</summary>
        /// <remarks>If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not an Event Metric (or a derived class) an exception will be thrown.
        /// If the metric definition isn't bound to an object type, the default metric will be returned.
        /// The provided object must not be null and must be of the type the metric definition owning this dictionary is bound to.</remarks>
        /// <param name="userDataObject">The object to create a metric from.</param>
        /// <returns>The event metric object for the specified event metric instance.</returns>
        public EventMetric AddOrGet(object userDataObject)
        {
            EventMetric newMetric;

            //we need a live object, not a null object or we'll fail
            if (userDataObject == null)
            {
                throw new ArgumentNullException("userDataObject");
            }

            //great.  We now know a lot - namely that it has to have the right attributes, etc. to define a metric so we can 
            //now go and find all of the information we need to create a new metric.
            string instanceName = null;
            if (Definition.NameBound)
            {
                //we don't even need to get it - we just care that it's defined.
                try
                {
                    //To be righteous, we need to only invoke the member we're looking at
                    BindingFlags methodBinding;
                    switch (Definition.NameMemberType)
                    {
                        case MemberTypes.Field:
                            methodBinding = BindingFlags.GetField;
                            break;
                        case MemberTypes.Method:
                            methodBinding = BindingFlags.InvokeMethod;
                            break;
                        case MemberTypes.Property:
                            methodBinding = BindingFlags.GetProperty;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    //invoke the bound instance name from the type our definition is associated with.  This way if the object provided
                    //has multiple implementations that are metric-enabled, we use the correct one.
                    object rawValue = Definition.BoundType.InvokeMember(Definition.NameMemberName, methodBinding, null, userDataObject,
                                                                        null, CultureInfo.InvariantCulture);

                    //and the raw value is either null or something we're going to convert to a string
                    if (rawValue == null)
                    {
                        instanceName = null;
                    }
                    else
                    {
                        instanceName = rawValue.ToString();
                    }
                }
                catch (Exception ex)
                {
                    //just trace log this - we can continue, they'll just get the default instance until they fix their code.
                    Trace.TraceWarning("Unable to retrieve the instance name to create a specific {0} metric because an exception occurred while accessing the member {1}: {2}",
                        Definition.Key, Definition.NameMemberName, ex.ToString());
                }
            }

            //now that we have our instance name, we go ahead and see if there is already an instance with the right name or just add it
            lock (this.Lock) //make sure the try & add are atomic
            {
                if (TryGetValue(instanceName, out newMetric) == false)
                {
                    //there isn't one with the right name, we need to create it.
                    newMetric = Add(instanceName);
                }
            }

            //return what we got - we have an object one way or another, or we threw an exception.
            return newMetric;
        }
        */

        /// <summary>
        /// The definition of all of the metrics in this collection.
        /// </summary>
        public EventMetricDefinition Definition { get { return m_MetricDefinition; } }


        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out EventMetric value)
        {
            lock (m_Lock)
            {
                //We are playing a few games to get native typing here.
                //Because it's an out value, we have to swap types around ourselves so we can cast.

                //gateway to our internal collection try get value
                bool result = m_WrappedCollection.TryGetValue(key, out var innerValue);

                value = result ? Externalize(innerValue) : null;

                return result;
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out EventMetric value)
        {
            lock (m_Lock)
            {
                //We are playing a few games to get native typing here.
                //Because it's an out value, we have to swap types around ourselves so we can cast.

                //gateway to our internal collection try get value
                bool result = m_WrappedCollection.TryGetValue(key, out var innerValue);

                value = result ? Externalize(innerValue) : null;

                return result;
            }
        }


        /// <summary>
        /// Retrieve the event metric by its zero-based index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">An attempt was made to set an object by index which is not supported.</exception>
        public EventMetric this[int index]
        {
            get
            {
                lock (m_Lock)
                {
                    return Externalize(m_WrappedCollection[index]);
                }
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException("Setting an object by index is not supported");
            }
        }

        /// <summary>
        /// Retrieve event metric object by its Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public EventMetric this[Guid Id]
        {
            get
            {
                lock (m_Lock)
                {
                    return Externalize(m_WrappedCollection[Id]);
                }
            }
        }

        /// <summary>
        /// Retrieve event metric object by its name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public EventMetric this[string key]
        {
            get
            {
                lock (m_Lock)
                {
                    return Externalize(m_WrappedCollection[key]);
                }
            }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Object Change Locking object.
        /// </summary>
        internal object Lock { get { return m_Lock; } }

        internal Core.Monitor.EventMetricCollection WrappedCollection
        {
            get { return m_WrappedCollection; }
        }

        internal EventMetric Externalize(Core.Monitor.EventMetric eventMetric)
        {
            if (eventMetric == null)
                return null;

            lock (m_Lock)
            {
                if (m_Externalized.TryGetValue(eventMetric, out var externalDefinition) == false)
                {
                    externalDefinition = new EventMetric(m_MetricDefinition, eventMetric);
                    m_Externalized[eventMetric] = externalDefinition;
                }

                return externalDefinition;
            }
        }

        internal void Internalize(EventMetric metric)
        {
            lock (m_Lock)
            {
                Core.Monitor.EventMetric internalMetric = metric.WrappedMetric;

                m_Externalized[internalMetric] = metric;
            }
        }

        #endregion

        #region Private helper class

        private class Enumerator : IEnumerator<EventMetric>
        {
            private readonly IEnumerator<Core.Monitor.EventMetric> m_Enumerator;
            private readonly EventMetricCollection m_Collection;

            public Enumerator(EventMetricCollection collection, IEnumerator<Core.Monitor.EventMetric> enumerator)
            {
                m_Collection = collection;
                m_Enumerator = enumerator;
            }

            public void Dispose()
            {
                m_Enumerator.Dispose();
            }

            public bool MoveNext()
            {
                return m_Enumerator.MoveNext();
            }

            public void Reset()
            {
                m_Enumerator.Reset();
            }

            public EventMetric Current
            {
                get { return m_Collection.Externalize(m_Enumerator.Current); }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }
        }

        #endregion
    }
}
