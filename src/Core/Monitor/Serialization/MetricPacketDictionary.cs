using System;
using System.Collections.Generic;
#pragma warning disable 1591

namespace Gibraltar.Monitor.Serialization
{
    /// <summary>
    /// A collection of performance counter metric packets, keyed by their unique ID.  This is the persistable form of a performance counter metric.
    /// </summary>
    public class MetricPacketDictionary : IList<MetricPacket>
    {
        private readonly Dictionary<string, MetricPacket> m_DictionaryByName = new Dictionary<string, MetricPacket>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, MetricPacket> m_Dictionary = new Dictionary<Guid, MetricPacket>();
        private readonly List<MetricPacket> m_List = new List<MetricPacket>();
        private readonly object m_Lock = new object();

        public event EventHandler<CollectionChangedEventArgs<MetricPacketDictionary, MetricPacket>> CollectionChanged;

        #region Private Properties and Methods

        protected virtual void OnCollectionChanged(CollectionChangedEventArgs<MetricPacketDictionary, MetricPacket> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<MetricPacketDictionary, MetricPacket>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// Add an existing metric packet object to our collection.  It must be for the same analysis as this collection.
        /// </summary>
        /// <param name="newMetricPacket">The new metric object to add.</param>
        public void Add(MetricPacket newMetricPacket)
        {
            //we really don't want to support this method, but we have to for ICollection<T> compatibility.  So we're going to ruthelessly
            //verify that the metric packet object was created correctly.

            if (newMetricPacket == null)
            {
                throw new ArgumentNullException(nameof(newMetricPacket), "A metric packet object must be provided to add it to the collection.");
            }

            //we're about to modify the collection, get a lock.  We don't want the lock to cover the changed event since
            //we really don't know how long that will take, and it could be deadlock prone.
            lock (m_Lock)
            {
                //make sure we don't already have it
                if (m_Dictionary.ContainsKey(newMetricPacket.ID))
                {
                    throw new ArgumentException("There already exists a metric packet object in the collection for the specified Id.", nameof(newMetricPacket));
                }

                //make sure we don't already have it by its name
                if (m_DictionaryByName.ContainsKey(newMetricPacket.Name))
                {
                    throw new ArgumentException("There already exists a metric packet object in the collection with the specified name", nameof(newMetricPacket));
                }

                //add it to both lookup collections
                m_Dictionary.Add(newMetricPacket.ID, newMetricPacket);
                m_DictionaryByName.Add(newMetricPacket.Name, newMetricPacket);
                m_List.Add(newMetricPacket);
            }

            //and fire our event
            OnCollectionChanged(new CollectionChangedEventArgs<MetricPacketDictionary, MetricPacket>(this, newMetricPacket, CollectionAction.Added));
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(Guid key)
        {
            lock (m_Lock) // Apparently Dictionaries are not internally threadsafe.
            {
                //gateway to our inner dictionary 
                return m_Dictionary.ContainsKey(key);
            }
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The performance counter key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            lock (m_Lock) // Apparently Dictionaries are not internally threadsafe.
            {
                //gateway to our alternate inner dictionary
                return m_DictionaryByName.ContainsKey(key);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out MetricPacket value)
        {
            lock (m_Lock) // Apparently Dictionaries are not internally threadsafe.
            {
                //gateway to our inner dictionary try get value
                return m_Dictionary.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The performance counter key to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out MetricPacket value)
        {
            lock (m_Lock) // Apparently Dictionaries are not internally threadsafe.
            {
                //gateway to our inner dictionary try get value
                return m_DictionaryByName.TryGetValue(key, out value);
            }
        }
        #endregion

        #region IEnumerable<MetricPacket> Members

        IEnumerator<MetricPacket> IEnumerable<MetricPacket>.GetEnumerator()
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

        public int IndexOf(MetricPacket item)
        {
            lock (m_Lock) // Apparently Lists are not internally threadsafe.
            {
                return m_List.IndexOf(item);
            }
        }

        public void Insert(int index, MetricPacket item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            //find the item at the requested location
            MetricPacket victim;
            lock (m_Lock) // Apparently Lists are not internally threadsafe.
            {
                victim = m_List[index];
            }

            //and pass that to our normal remove method.  Don't lock around this, it needs to send an event outside the lock.
            Remove(victim);
        }

        /// <summary>
        /// Retrieve performance counter metric packet object by numeric index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public MetricPacket this[int index]
        {
            get
            {
                lock (m_Lock) // Apparently Lists are not internally threadsafe.
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
        /// Retrieve metric packet object by its name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public MetricPacket this[string key]
        {
            get
            {
                lock (m_Lock) // Apparently Dictionaries are not internally threadsafe.
                {
                    return m_DictionaryByName[key];
                }
            }
        }

        /// <summary>
        /// Retrieve performance counter metric packet object by its Id
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public MetricPacket this[Guid ID]
        {
            get
            {
                lock (m_Lock) // Apparently Dictionaries are not internally threadsafe.
                {
                    return m_Dictionary[ID];
                }
            }
        }

        #endregion

        #region ICollection<MetricPacket> Members


        public void Clear()
        {
            //Only do this if we HAVE something, since events are fired.
            int count;
            lock (m_Lock) // We need the lock for checking the count; apparently Lists are not internally threadsafe.
            {
                count = m_List.Count; // Save this to check outside the lock, too.
                if (count > 0)
                {
                    //The collection isn't already clear, so clear it inside the lock.
                    m_List.Clear();
                    m_Dictionary.Clear();
                    m_DictionaryByName.Clear();
                }
            }

            //We don't want the lock to cover the changed event since we really don't know how long that will take,
            //and it could be deadlock prone.
            if (count > 0)
            {
                //and raise the event so our caller knows we're cleared
                OnCollectionChanged(new CollectionChangedEventArgs<MetricPacketDictionary, MetricPacket>(this, null, CollectionAction.Cleared));
            }
        }

        public bool Contains(MetricPacket item)
        {
            //here we are relying on the fact that the comment object implements IComparable sufficiently to guarantee uniqueness
            lock (m_Lock) // Apparently Lists are not internally threadsafe.
            {
                return m_List.Contains(item);
            }
        }

        public void CopyTo(MetricPacket[] array, int arrayIndex)
        {
            lock (m_Lock) // Apparently Lists are not internally threadsafe.
            {
                m_List.CopyTo(array, arrayIndex);
            }
        }

        public int Count
        {
            get
            {
                lock (m_Lock) // Apparently Lists are not internally threadsafe.
                {
                    return m_List.Count;
                }
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Remove the specified victim comment.  If the comment isn't in the collection, no exception is thrown.
        /// </summary>
        /// <param name="victim">The object to remove.</param>
        public bool Remove(MetricPacket victim)
        {
            bool result = false;

            if (victim == null)
            {
                throw new ArgumentNullException(nameof(victim), "A victim object must be provided to remove it from the collection.");
            }

            //we're about to modify the collection, get a lock.  We don't want the lock to cover the changed event since
            //we really don't know how long that will take, and it could be deadlock prone.
            lock (m_Lock)
            {
                //we have to remove it from both collections, and we better not raise an error if not there.
                if (m_Dictionary.ContainsKey(victim.ID))
                {
                    m_Dictionary.Remove(victim.ID);
                    result = true;  // we did remove something
                }

                if (m_DictionaryByName.ContainsKey(victim.Name))
                {
                    m_DictionaryByName.Remove(victim.Name);
                    result = true;  // we did remove something
                }

                if (m_List.Contains(victim))
                {
                    m_List.Remove(victim);
                    result = true;  // we did remove something
                }
            }

            //and fire our event
            OnCollectionChanged(new CollectionChangedEventArgs<MetricPacketDictionary, MetricPacket>(this, victim, CollectionAction.Removed));

            return result;
        }


        #endregion
    }
}
