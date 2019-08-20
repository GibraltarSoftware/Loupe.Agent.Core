using System;
using System.Collections;
using System.Collections.Generic;
using Loupe.Core;
using Loupe.Core.Metrics;

namespace Loupe.Agent.Metrics.Internal
{
    /// <summary>
    /// A collection of metric definitions, keyed by their unique Id and Key.
    /// </summary>
    /// <remarks>
    /// A metric definition has a unique ID to identify a particular instance of the definition (associated with one session) 
    /// and a Key which is unique within a session but is designed for comparison of the same definition between sessions.
    /// </remarks>
    internal sealed class MetricDefinitionCollection : IList<IMetricDefinition>
    {
        private Core.Metrics.MetricDefinitionCollection m_WrappedCollection;
        private readonly Dictionary<Loupe.Extensibility.Data.IMetricDefinition, IMetricDefinition> m_Externalized = new Dictionary<Loupe.Extensibility.Data.IMetricDefinition, IMetricDefinition>();

        /// <summary>
        /// Create a new collection of API metric definitions wrapping a given internal collection.
        /// </summary>
        /// <param name="definitionCollection">An internal metric definition collection to wrap.</param>
        internal MetricDefinitionCollection(Core.Metrics.MetricDefinitionCollection definitionCollection)
        {
            m_WrappedCollection = definitionCollection;
        }

        /*
        /// <summary>
        /// Create a new collection of metric definitions.
        /// </summary>
        public MetricDefinitionCollection() // Should this be internal?
        {
            m_WrappedCollection = new Monitor.MetricDefinitionCollection();
        }
        */

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        internal event EventHandler<CollectionChangedEventArgs<MetricDefinitionCollection, IMetricDefinition>> CollectionChanged;

        #region Private Properties and Methods

        /// <summary>
        /// This method is called every time a collection change event occurs to allow inheritors to override the change event.
        /// </summary>
        /// <remarks>If overridden, it is important to call this base implementation to actually fire the event.</remarks>
        /// <param name="e"></param>
        private void OnCollectionChanged(CollectionChangedEventArgs<MetricDefinitionCollection, IMetricDefinition> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<MetricDefinitionCollection, IMetricDefinition>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Internal Properties and Methods

        internal Core.Metrics.MetricDefinitionCollection WrappedCollection { get { return m_WrappedCollection; } }

        /// <summary>
        /// Ensures that the provided object is used as the wrapped object.
        /// </summary>
        /// <param name="summary"></param>
        internal void SyncWrappedObject(Core.Metrics.MetricDefinitionCollection summary)
        {
            if (ReferenceEquals(summary, m_WrappedCollection) == false)
            {
                m_WrappedCollection = summary;
            }
        }

        internal IMetricDefinition Externalize(Loupe.Extensibility.Data.IMetricDefinition metricDefinition)
        {
            if (metricDefinition == null)
                return null;

            lock (Lock)
            {
                if (m_Externalized.TryGetValue(metricDefinition, out var externalDefinition) == false)
                {
                    var eventDefinition = metricDefinition as Core.Metrics.EventMetricDefinition;
                    var customDefinition = metricDefinition as CustomSampledMetricDefinition;

                    if (eventDefinition != null)
                    {
                        externalDefinition = new EventMetricDefinition(eventDefinition);
                    }
                    else if (customDefinition != null)
                    {
                        externalDefinition = new SampledMetricDefinition(customDefinition);
                    }
                    else
                    {
                        throw new NotSupportedException(string.Format("Unable to wrap metric definition type: {0}", metricDefinition.GetType().Name));
                    }

                    m_Externalized[metricDefinition] = externalDefinition;
                }

                return externalDefinition;
            }
        }

        internal void Internalize(IMetricDefinition metricDefinition)
        {
            lock (Lock)
            {
                MetricDefinition internaldefinition = metricDefinition.WrappedDefinition;

                m_Externalized[internaldefinition] = metricDefinition;
            }
        }

        /// <summary>
        /// Object Change Locking object.
        /// </summary>
        internal object Lock { get { return m_WrappedCollection.Lock; } }

        #endregion

        #region Public Properties and Methods

        /*
        /// <summary>
        /// Retrieve a metric given its unique Id.
        /// </summary>
        /// <param name="metricId">The unique Id of the metric to retrieve</param>
        /// <returns></returns>
        public EventMetric EventMetric(Guid metricId)
        {
            // Have the internal definition collection look up the metric instance.
            Monitor.Metric metric = m_WrappedCollection.Metric(metricId);

            // Get its internal definition, and get its wrapper, which we track locally.
            IMetricDefinition definition = Externalize(metric.Definition);
            EventMetricDefinition eventDefinition = definition as EventMetricDefinition;
            if (eventDefinition == null)
                return null; // Wrong kind of metric definition!

            Monitor.EventMetric eventMetric = metric as Monitor.EventMetric;

            // Then ask the definition's metrics collection for the metric wrapper, which it tracks.
            return eventDefinition.Metrics.Externalize(eventMetric);
        }

        /// <summary>
        /// Retrieve a metric given its unique Id.
        /// </summary>
        /// <param name="metricId">The unique Id of the metric to retrieve</param>
        /// <returns></returns>
        public SampledMetric SampledMetric(Guid metricId)
        {
            // Have the internal definition collection look up the metric instance.
            Monitor.Metric metric = m_WrappedCollection.Metric(metricId);

            // Get its internal definition, and get its wrapper, which we track locally.
            IMetricDefinition definition = Externalize(metric.Definition);
            SampledMetricDefinition sampledDefinition = definition as SampledMetricDefinition;
            if (sampledDefinition == null)
                return null; // Wrong kind of metric definition!

            Monitor.CustomSampledMetric sampledMetric = metric as Monitor.CustomSampledMetric;

            // Then ask the definition's metrics collection for the metric wrapper, which it tracks.
            return sampledDefinition.Metrics.Externalize(sampledMetric);
        }
        */

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="id">The metric definition Id to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(Guid id)
        {
            //gateway to our inner dictionary 
            return m_WrappedCollection.ContainsKey(id);
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="name">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">The provided name was null.</exception>
        public bool ContainsKey(string name)
        {
            //protect ourself from a null before we do the trim (or we'll get an odd user the error won't understand)
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            //gateway to our alternate inner dictionary
            return m_WrappedCollection.ContainsKey(name.Trim());
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string metricsSystem, string categoryName, string counterName)
        {
            //gateway to our alternate inner dictionary
            return m_WrappedCollection.ContainsKey(metricsSystem, categoryName, counterName);
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="id">The metric instance Id to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsMetricKey(Guid id)
        {
            //gateway to our alternate inner dictionary
            return m_WrappedCollection.ContainsMetricKey(id);
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="id">The metric definition Id of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid id, out IMetricDefinition value)
        {
            lock (Lock)
            {
                //gateway to our inner dictionary try get value
                bool foundValue = m_WrappedCollection.TryGetValue(id, out var definition);
                value = foundValue ? Externalize(definition) : null;
                return foundValue;
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="name">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">The provided name was null.</exception>
        public bool TryGetValue(string name, out IMetricDefinition value)
        {
            //protect ourself from a null before we do the trim (or we'll get an odd user the error won't understand)
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            lock (Lock)
            {
                //gateway to our inner dictionary try get value
                bool foundValue = m_WrappedCollection.TryGetValue(name.Trim(), out var definition);
                value = foundValue ? Externalize(definition) : null;
                return foundValue;
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">The provided metricsSystem, categoryName, or counterName was null.</exception>
        public bool TryGetValue(string metricsSystem, string categoryName, string counterName, out IMetricDefinition value)
        {
            lock (Lock)
            {
                //gateway to our inner dictionary try get value
                bool foundValue = m_WrappedCollection.TryGetValue(metricsSystem, categoryName, counterName, out var definition);
                value = foundValue ? Externalize(definition) : null;
                return foundValue;
            }
        }

        #endregion

        #region IEnumerable<MetricPacket> Members

        IEnumerator<IMetricDefinition> IEnumerable<IMetricDefinition>.GetEnumerator()
        {
            IEnumerable<Loupe.Extensibility.Data.IMetricDefinition> enumerable = m_WrappedCollection;
            return new Enumerator(this, enumerable.GetEnumerator());
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            IEnumerable<Loupe.Extensibility.Data.IMetricDefinition> enumerable = m_WrappedCollection;
            return new Enumerator(this, enumerable.GetEnumerator());
        }

        #endregion

        #region IList<MetricPacket> Members

        /// <summary>
        /// Searches for the specified value and returns the zero-based index of the first occurrence
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(IMetricDefinition item)
        {
            return m_WrappedCollection.IndexOf(item?.WrappedDefinition);
        }

        ///<summary>Inserting objects by index is not supported because the collection is sorted.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        /// <exception cref="NotSupportedException">An attempt was made to set an object by index which is not supported.</exception>
        public void Insert(int index, IMetricDefinition item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException("Setting an object by index is not supported");
        }

        ///<summary>Removing objects by index is not supported because the collection is always read only.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        /// <exception cref="NotSupportedException">An attempt was made to remove an object by index which is not supported.</exception>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException("Removing an object by index is not supported");
        }

        /// <summary>
        /// Retrieve metric packet by numeric index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">An attempt was made to set an object by index which is not supported.</exception>
        public IMetricDefinition this[int index]
        {
            get
            {
                lock (Lock)
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
        /// Retrieve metric definition object by its Id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IMetricDefinition this[Guid id]
        {
            get
            {
                lock (Lock)
                {
                    return Externalize(m_WrappedCollection[id]);
                }
            }
        }

        /// <summary>
        /// Retrieve metric definition object by its Key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IMetricDefinition this[string key]
        {
            get
            {
                lock (Lock)
                {
                    return Externalize(m_WrappedCollection[key.Trim()]);
                }
            }
        }

        /// <summary>
        /// Retrieve metric definition object by its metrics system, category, and counter names.
        /// </summary>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <returns></returns>
        public IMetricDefinition this[string metricsSystem, string categoryName, string counterName]
        {
            get
            {
                lock (Lock)
                {
                    return Externalize(m_WrappedCollection[metricsSystem, categoryName, counterName]);
                }
            }
        }

        #endregion

        #region ICollection<MetricPacket> Members

        /// <summary>
        /// Add an existing IMetricDefinition item to this collection.
        /// </summary>
        /// <remarks>If the supplied MetricDefinition item is already in the collection, an exception will be thrown.</remarks>
        /// <param name="item">The new IMetricDefinition item to add.</param>
        /// <exception cref="ArgumentNullException">The provided item was null</exception>
        public void Add(IMetricDefinition item)
        {
            //we really don't want to support this method, but we have to for ICollection<T> compatibility.  So we're going to ruthlessly
            //verify that the metric object was created correctly.

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            lock (Lock)
            {
                // Set up our mapping of internal to API definition objects for the one we're about to add...
                MetricDefinition internalDefinition = item.WrappedDefinition;
                m_Externalized[internalDefinition] = item;

                m_WrappedCollection.Add(internalDefinition); // ...Then add it to the internal collection.
            }

            //and fire our event, outside the lock
            // Note: This is not done by subscribing to the event from the internal collection, which happens inside the lock!
            OnCollectionChanged(new CollectionChangedEventArgs<MetricDefinitionCollection, IMetricDefinition>(this, item, CollectionAction.Added));
        }

        /// <summary>
        /// Clearing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        public void Clear()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Determines whether an element is in the collection.
        /// </summary>
        /// <remarks>This method determines equality using the default equality comparer for the type of values in the list.  It performs
        /// a linear search and therefore is an O(n) operation.</remarks>
        /// <param name="item">The object to locate in the collection.</param>
        /// <returns>true if the item is found in the collection; otherwise false.</returns>
        public bool Contains(IMetricDefinition item)
        {
            if (item == null) return false;

            //here we are relying on the fact that the comment object implements IComparable sufficiently to guarantee uniqueness
            return m_WrappedCollection.Contains(item.WrappedDefinition);
        }

        /// <summary>
        /// Copies the entire collection to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <remarks>Elements are copied to the array in the same order in which the enumerator iterates them from the collection.  The provided array 
        /// must be large enough to contain the entire contents of this collection starting at the specified index.  This method is an O(n) operation.</remarks>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the collection.  The Array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(IMetricDefinition[] array, int arrayIndex)
        {
            int length = array.Length - arrayIndex;
            if (length <= 0)
                return;

            Loupe.Extensibility.Data.IMetricDefinition[] internalArray = m_WrappedCollection.ToArray();
            if (length > internalArray.Length)
                length = internalArray.Length;

            for (int i = 0; i < length; i++)
            {
                array[arrayIndex + i] = Externalize(internalArray[i]);
            }
        }

        /// <summary>
        /// Copy the entire collection of metric definitions into a new array.
        /// </summary>
        /// <returns>A new array containing all of the metric definitions in this collection.</returns>
        public IMetricDefinition[] ToArray()
        {
            Loupe.Extensibility.Data.IMetricDefinition[] internalArray = m_WrappedCollection.ToArray();
            int length = internalArray.Length;
            IMetricDefinition[] array = new IMetricDefinition[length];

            for (int i=0; i<length; i++)
            {
                array[i] = Externalize(internalArray[i]);
            }

            return array;
        }

        /// <summary>
        /// The number of items currently in the collection
        /// </summary>
        public int Count
        {
            get { return m_WrappedCollection.Count; }
        }

        /// <summary>
        /// Indicates if the collection is read only and therefore can't have items added or removed.
        /// </summary>
        /// <remarks>This collection is never read-only, however removing items is not supported.
        /// This property is required for ICollection compatibility</remarks>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Removing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        /// <param name="item">The IMetricDefinition item to remove.</param>
        public bool Remove(IMetricDefinition item)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region Private helper class

        private class Enumerator : IEnumerator<IMetricDefinition>
        {
            private readonly IEnumerator<Loupe.Extensibility.Data.IMetricDefinition> m_Enumerator;
            private readonly MetricDefinitionCollection m_Collection;

            public Enumerator(MetricDefinitionCollection collection, IEnumerator<Loupe.Extensibility.Data.IMetricDefinition> enumerator)
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

            public IMetricDefinition Current
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