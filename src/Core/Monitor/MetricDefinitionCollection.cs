using System;
using System.Collections.Generic;
using Gibraltar.Monitor.Internal;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// A collection of metric definitions, keyed by their unique ID and name
    /// </summary>
    /// <remarks>
    /// <para>A metric definition has a unique ID to identify a particular instance of the definition(associated with one session) 
    /// and a name that is unique within a session but is designed for comparison of the same definition between sessions.</para>
    /// <para>This class is sealed because it is owned by either the single static active Log class (for metric collection in the current process)
    /// or during replay is automatically created as part of base objects and there is no way to inject an alternative implementation.</para>
    /// </remarks>
    public sealed class MetricDefinitionCollection : IMetricDefinitionCollection
    {
        private readonly Dictionary<Guid, IMetric> m_MetricById = new Dictionary<Guid, IMetric>();
        private readonly Dictionary<string, IMetricDefinition> m_DictionaryByName = new Dictionary<string, IMetricDefinition>();
        private readonly Dictionary<Guid, IMetricDefinition> m_Dictionary = new Dictionary<Guid, IMetricDefinition>();
        private readonly List<IMetricDefinition> m_List = new List<IMetricDefinition>();
        private readonly object m_Lock = new object();

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs<IMetricDefinitionCollection, IMetricDefinition>> CollectionChanged;

        #region Private Properties and Methods

        /// <summary>
        /// This method is called every time a collection change event occurs to allow inheritors to override the change event.
        /// </summary>
        /// <remarks>If overridden, it is important to call this base implementation to actually fire the event.</remarks>
        /// <param name="e"></param>
        private void OnCollectionChanged(CollectionChangedEventArgs<IMetricDefinitionCollection, IMetricDefinition> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<IMetricDefinitionCollection, IMetricDefinition>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Add a metric to the definition metric cache.  Used by the MetricCollection base class to flatten the hierarchy.
        /// </summary>
        /// <param name="newMetric">The metric object to add to the cache.</param>
        internal void AddMetric(IMetric newMetric)
        {
            lock (m_Lock)
            {
                m_MetricById.Add(newMetric.Id, newMetric);
            }
        }

        /// <summary>
        /// Remove a metric to the definition metric cache.  Used by the MetricCollection base class to flatten the hierarchy.
        /// </summary>
        /// <param name="victimMetric">The metric object to remove from the cache.</param>
        internal void RemoveMetric(IMetric victimMetric)
        {
            lock (m_Lock)
            {
                m_MetricById.Remove(victimMetric.Id);
            }
        }

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// Object Change Locking object.
        /// </summary>
        public object Lock { get { return m_Lock; } }

        /// <summary>
        /// Add an existing metric packet object to our collection for the supplied packet.
        /// </summary>
        /// <param name="newDefinitionPacket">The new metric packet object to add.</param>
        internal MetricDefinition Add(MetricDefinitionPacket newDefinitionPacket)
        {
            //just do the null check - the rest of the checks (like dupe checks) are done in the normal add routine
            if (newDefinitionPacket == null)
            {
                throw new ArgumentNullException(nameof(newDefinitionPacket), "A metric packet object must be provided to add it to the collection.");
            }

            //create a new metric object to wrap the supplied metric packet
            MetricDefinition newDefinition = new MetricDefinition(this, newDefinitionPacket);

            //and call our mainstream add method which does the rest of our checks.
            Add(newDefinition);

            //return the newly added object so our caller doesn't have to go digging for it
            return newDefinition;
        }

        /// <summary>
        /// Retrieve a metric given its unique Id.
        /// </summary>
        /// <param name="metricId">The unique Id of the metric to retrieve</param>
        /// <returns></returns>
        public IMetric Metric(Guid metricId)
        {
            lock (m_Lock)
            {
                return m_MetricById[metricId];
            }
        }

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
        /// <param name="name">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string name)
        {
            //protect ourself from a null before we do the trim (or we'll get an odd user the error won't understand)
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            lock (m_Lock)
            {
                //gateway to our alternate inner dictionary
                return m_DictionaryByName.ContainsKey(name.Trim());
            }
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string metricTypeName, string categoryName, string counterName)
        {
            //get the key for the provided values
            string key = MetricDefinition.GetKey(metricTypeName, categoryName, counterName);

            lock (m_Lock)
            {
                //gateway to our alternate inner dictionary
                return m_DictionaryByName.ContainsKey(key);
            }
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsMetricKey(Guid key)
        {
            lock (m_Lock)
            {
                //gateway to our alternate inner dictionary
                return m_MetricById.ContainsKey(key);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out IMetricDefinition value)
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
        /// <param name="name">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string name, out IMetricDefinition value)
        {
            //protect ourself from a null before we do the trim (or we'll get an odd user the error won't understand)
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_DictionaryByName.TryGetValue(name.Trim(), out value);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">The provided metricsSystem, categoryName, or counterName was null.</exception>
        public bool TryGetValue(string metricTypeName, string categoryName, string counterName, out IMetricDefinition value)
        {
            //get the key for the provided values
            string key = MetricDefinition.GetKey( metricTypeName, categoryName, counterName );

            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_DictionaryByName.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetMetricValue(Guid key, out IMetric value)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_MetricById.TryGetValue(key, out value);
            }
        }

        #endregion

        #region IEnumerable<IMetricDefinition> Members

        IEnumerator<IMetricDefinition> IEnumerable<IMetricDefinition>.GetEnumerator()
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

        #region IList<IMetricDefinition> Members

        /// <summary>
        /// Searches for the specified value and returns the zero-based index of the first occurrence
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(IMetricDefinition item)
        {
            lock (m_Lock)
            {
                return m_List.IndexOf(item);
            }
        }

        ///<summary>Inserting objects by index is not supported because the collection is sorted.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        public void Insert(int index, IMetricDefinition item)
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
        public IMetricDefinition this[int index]
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
        /// <param name="ID"></param>
        /// <returns></returns>
        public IMetricDefinition this[Guid ID]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Dictionary[ID];
                }
            }
        }

        /// <summary>
        /// Retrieve metric object by its name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IMetricDefinition this[string name]
        {
            get
            {
                //protect ourself from a null before we do the trim (or we'll get an odd user the error won't understand)
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentNullException(nameof(name));
                }

                lock (m_Lock)
                {
                    return m_DictionaryByName[name.Trim()];
                }
            }
        }

        /// <summary>
        /// Retrieve metric object by its type, category, and counter names.
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <returns></returns>
        public IMetricDefinition this[string metricTypeName, string categoryName, string counterName]
        {
            get
            {
                //create the key from the parts we got
                string key = MetricDefinition.GetKey( metricTypeName, categoryName, counterName );
                lock (m_Lock)
                {
                    return m_DictionaryByName[key];
                }
            }
        }

        #endregion

        #region ICollection<IMetricDefinition> Members

        /// <summary>
        /// Add an existing MetricDefinition item to this collection.
        /// </summary>
        /// <remarks>If the supplied MetricDefinitin item is already in the collection, an exception will be thrown.</remarks>
        /// <param name="item">The new MetricDefinition item to add.</param>
        public void Add(IMetricDefinition item)
        {
            //we really don't want to support this method, but we have to for ICollection<T> compatibility.  So we're going to ruthlessly
            //verify that the metric object was created correctly.

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            //we're about to modify the collection, get a lock.  We don't want the lock to cover the changed event since
            //we really don't know how long that will take, and it could be deadlock prone.
            lock (m_Lock)
            {
                //make sure we don't already have it
                if (m_Dictionary.ContainsKey(item.Id))
                {
                    throw new ArgumentException("The specified metric definition item is already in the collection.", nameof(item));
                }

                if (m_DictionaryByName.ContainsKey(item.Name))
                {
                    throw new ArgumentException("A metric definition item for the same metric is already in the collection.", nameof(item));
                }

                //add it to both lookup collections
                m_Dictionary.Add(item.Id, item);
                m_DictionaryByName.Add(item.Name, item);
                m_List.Add(item);
            }

            //and fire our event
            OnCollectionChanged(new CollectionChangedEventArgs<IMetricDefinitionCollection, IMetricDefinition>(this, item, CollectionAction.Added));
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
            lock (m_Lock)
            {
                //here we are relying on the fact that the comment object implements IComparable sufficiently to guarantee uniqueness
                return m_List.Contains(item);
            }
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
            lock (m_Lock)
            {
                m_List.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Copy the entire collection of metric definitions into a new array.
        /// </summary>
        /// <returns>A new array containing all of the metric definitions in this collection.</returns>
        public IMetricDefinition[] ToArray()
        {
            lock (m_Lock)
            {
                return m_List.ToArray();
            }
        }

        /// <summary>
        /// The number of items currently in the collection
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
        /// <param name="item">The MetricDefinition item to remove.</param>
        public bool Remove(IMetricDefinition item)
        {
            throw new NotSupportedException();
        }


        #endregion
    }
}
