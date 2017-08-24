using System;
using System.Collections.Generic;
using Loupe.Extensibility.Data;



namespace Gibraltar.Monitor
{
    /// <summary>
    /// A collection of metrics, keyed by their unique ID and name
    /// </summary>
    /// <remarks>A metric has a unique ID to identify a particular instance of the metric (associated with one session) 
    /// and a name that is unique within a session but is designed for comparison of the same metric between sessions.</remarks>
    public class MetricCollection : IMetricCollection
    {
        private readonly Dictionary<string, IMetric> m_DictionaryByName = new Dictionary<string, IMetric>();
        private readonly Dictionary<Guid, IMetric> m_Dictionary = new Dictionary<Guid, IMetric>();
        private readonly List<IMetric> m_List = new List<IMetric>();
        private readonly object m_Lock = new object();
        private readonly MetricDefinition m_MetricDefinition;

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs<IMetricCollection, IMetric>> CollectionChanged;

        /// <summary>
        /// Create a new metric dictionary for the provided definition.
        /// </summary>
        /// <remarks>This dictionary is created automatically by the Metric Definition during its initialization.</remarks>
        /// <param name="metricDefinition"></param>
        internal MetricCollection(MetricDefinition metricDefinition)
        {
            if (metricDefinition == null)
            {
                throw new ArgumentNullException(nameof(metricDefinition));
            }
            m_MetricDefinition = metricDefinition;
        }

        #region Private Properties and Methods

        /// <summary>
        /// Raises an event whenever our collection is changed to notify objects that want to know when we change.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnCollectionChanged(CollectionChangedEventArgs<IMetricCollection, IMetric> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<IMetricCollection, IMetric>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Internal Properties and Methods
        
        /// <summary>
        /// Object Change Locking object.
        /// </summary>
        internal object Lock { get { return m_Lock; } }

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(Guid key)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary 
                return m_Dictionary.ContainsKey(key);
            }
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            //we do a few cute tricks to normalize the key before checking it to be tolerant of what users do
            string trueKey = MetricDefinition.NormalizeKey(m_MetricDefinition, key);

            lock (m_Lock)
            {
                //gateway to our alternate inner dictionary
                return m_DictionaryByName.ContainsKey(trueKey);
            }
        }

        /// <summary>
        /// The metric definition that owns this dictionary, meaning every metric is a specific instance of this metric definition.
        /// </summary>
        public IMetricDefinition Definition
        {
            get { return m_MetricDefinition; }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out IMetric value)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_Dictionary.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out IMetric value)
        {
            //we do a few cute tricks to normalize the key before checking it to be tolerant of what users do
            string trueKey = MetricDefinition.NormalizeKey(m_MetricDefinition, key);

            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_DictionaryByName.TryGetValue(trueKey, out value);
            }
        }

        #endregion

        #region IEnumerable<MetricPacket> Members

        IEnumerator<IMetric> IEnumerable<IMetric>.GetEnumerator()
        {
            //we use the list for enumeration
            return m_List.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            //we use the list for enumeration
            return m_List.GetEnumerator();
        }

        #endregion

        #region IList<MetricPacket> Members

        /// <summary>
        /// Returns the zero-based index of the specified metric within the dictionary.
        /// </summary>
        /// <param name="item">A metric object to find the index of</param>
        /// <returns>The zero-based index of an item if found, a negative number if not found.</returns>
        public int IndexOf(IMetric item)
        {
            lock (m_Lock)
            {
                return m_List.IndexOf(item);
            }
        }

        ///<summary>Inserting objects by index is not supported because the collection is sorted.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        public void Insert(int index, IMetric item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        ///<summary>Removing objects by index is not supported because the collection is always read only.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Retrieve metric packet by numeric index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IMetric this[int index]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_List[index];
                }
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Retrieve metric object by its Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public IMetric this[Guid Id]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Dictionary[Id];
                }
            }
        }

        /// <summary>
        /// Retrieve metric object by its name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IMetric this[string key]
        {
            get
            {
                //we do a few cute tricks to normalize the key before checking it to be tolerant of what users do
                string trueKey = MetricDefinition.NormalizeKey(m_MetricDefinition, key);

                lock (m_Lock)
                {
                    return m_DictionaryByName[trueKey];
                }
            }
        }

        #endregion

        #region ICollection<IMetric> Members

        /// <summary>
        /// Add the supplied Metric item to this collection.
        /// </summary>
        /// <remarks>Metrics automatically add themselves when they are created, so it isn't necessary (and will produce errors) to manually add them.</remarks>
        /// <param name="item">The new Metric item to add to this collection</param>
        public void Add(IMetric item)
        {
            //we really don't want to support this method, but we have to for ICollection<T> compatibility.  So we're going to ruthlessly
            //verify that the metric object was created correctly.

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A metric item must be provided to add it to the collection.");
            }

            //make sure the metric is for the right definition, namely our definition.
            if (MetricDefinition.GetKey(item) != m_MetricDefinition.Name)
            {
                throw new ArgumentOutOfRangeException(nameof(item), "The provided metric item is not related to the metric definition that owns this metrics collection.");
            }

            //we're about to modify the collection, get a lock.  We don't want the lock to cover the changed event since
            //we really don't know how long that will take, and it could be deadlock prone.
            lock (m_Lock)
            {
                //make sure we don't already have it
                if (m_Dictionary.ContainsKey(item.Id))
                {
                    throw new ArgumentException("The specified metric item is already in the collection.", nameof(item));
                }

                if (m_DictionaryByName.ContainsKey(item.Name))
                {
                    throw new ArgumentException("A metric item for the same metric is already in the collection.", nameof(item));
                }

                //add it to all of our collections, and to the definition metric cache.
                m_Dictionary.Add(item.Id, item);
                m_DictionaryByName.Add(item.Name, item);
                m_List.Add(item);
                ((MetricDefinitionCollection)m_MetricDefinition.Definitions).AddMetric(item);
            }

            //and fire our event
            OnCollectionChanged(new CollectionChangedEventArgs<IMetricCollection, IMetric>(this, item, CollectionAction.Added));
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
        /// Indicates whether the specified metric object is contained in this dictionary.
        /// </summary>
        /// <param name="item">The non-null object to look for.</param>
        /// <returns></returns>
        public bool Contains(IMetric item)
        {
            lock (m_Lock)
            {
                //here we are relying on the fact that the comment object implements IComparable sufficiently to guarantee uniqueness
                return m_List.Contains(item);
            }
        }

        /// <summary>
        /// Copies the entire contents of the dictionary into the provided array starting at the specified index.
        /// </summary>
        /// <remarks>The provided array must be large enough to contain the entire contents of this dictionary starting with the specified index.</remarks>
        /// <param name="array">The existing array to copy the dictionary into</param>
        /// <param name="arrayIndex">The zero-based index to start copying from.</param>
        public void CopyTo(IMetric[] array, int arrayIndex)
        {
            lock (m_Lock)
            {
                m_List.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Copy the entire collection of metric instances into a new array.
        /// </summary>
        /// <returns>A new array containing all of the metric instances in this collection.</returns>
        public IMetric[] ToArray()
        {
            lock (m_Lock)
            {
                return m_List.ToArray();
            }
        }

        /// <summary>
        /// The number of items in the dictionary.
        /// </summary>
        public int Count
        {
            get
            {
                lock (m_Lock)
                {
                    return m_List.Count;
                }
            }
        }

        /// <summary>
        /// Indicates whether the dictionary is read-only (meaning no new metrics can be added) or not.
        /// </summary>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>
        /// Removing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        /// <param name="item">The Metric item to remove.</param>
        public bool Remove(IMetric item)
        {
            throw new NotSupportedException();
        }


        #endregion
    }
}
