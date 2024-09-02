using System;
using System.Collections.Generic;
using System.Globalization;
using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility.Data;



namespace Gibraltar.Monitor
{
    /// <summary>
    /// A collection of event values for the parent metric definition.
    /// </summary>
    /// <remarks>This object is automatically created by the Event Metric Definition and is accessible through the Values property.</remarks>
    public class EventMetricValueDefinitionCollection : IEventMetricValueDefinitionCollection
    {
        private readonly Dictionary<string , IEventMetricValueDefinition> m_Dictionary = new Dictionary<string, IEventMetricValueDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IEventMetricValueDefinition> m_List = new List<IEventMetricValueDefinition>();
        private readonly EventMetricDefinition m_Definition;

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs<IEventMetricValueDefinitionCollection, IEventMetricValueDefinition>> CollectionChanged;

        /// <summary>
        /// Create a new values dictionary for the specified metric definition
        /// </summary>
        /// <param name="definition">The parent metric definition object that will own this dictionary.</param>
        internal EventMetricValueDefinitionCollection(EventMetricDefinition definition)
        {
            m_Definition = definition;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Create a new value definition with the supplied name and type.  The name must be unique in this collection
        /// </summary>
        /// <remarks>Internally, only simple type are supported.  Any non-numeric, non-DateTimeOffset type will be converted to a string
        /// using the default ToString capability when it is recorded.</remarks>
        /// <param name="name">The unique name for this value definition</param>
        /// <param name="type">The simple type of this value</param>
        /// <returns>The newly created value definition</returns>
        public EventMetricValueDefinition Add(string name, Type type)
        {
            //forward the call to our larger add method
            return Add(name, type, null, null);
        }

        /// <summary>
        /// Create a new value definition with the supplied name and type.  The name must be unique in this collection
        /// </summary>
        /// <remarks>Internally, only simple type are supported.  Any non-numeric, non-DateTimeOffset type will be converted to a string
        /// using the default ToString capability when it is recorded.</remarks>
        /// <param name="name">The unique name for this value definition</param>
        /// <param name="type">The simple type of this value</param>
        /// <param name="caption">The end-user display caption for this value</param>
        /// <param name="description">The end-user description for this value.</param>
        /// <returns>The newly created value definition</returns>
        public EventMetricValueDefinition Add(string name, Type type, string caption, string description)
        {
            lock (m_Definition.Lock) // Is this really needed?  Can't hurt....
            {
                //if we are read-only, you can't add a new value
                if (IsReadOnly)
                {
                    throw new NotSupportedException("The collection is read-only");
                }

                //make sure we got everything we require
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentNullException(nameof(name));
                }

                if (type == null)
                {
                    throw new ArgumentNullException(nameof(type));
                }

                //make sure the name is unique
                if (m_Dictionary.ContainsKey(name))
                {
                    throw new ArgumentException("There is already a value definition with the provided name.", nameof(name));
                }

                //create a new value definition
                EventMetricValueDefinitionPacket newPacket =
                    new EventMetricValueDefinitionPacket(m_Definition.Packet, name, type, caption, description);
                EventMetricValueDefinition newDefinition = new EventMetricValueDefinition(m_Definition, newPacket);

                //forward the call to our one true add method
                Add(newDefinition);

                //and return the new object to our caller so the have the object we created from their input.
                return newDefinition;
            }
        }


        /// <summary>
        /// The metric definition this value is associated with.
        /// </summary>
        public IEventMetricDefinition Definition { get { return m_Definition; } }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="name">The value name to locate in the collection</param>
        /// <returns>True if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string name)
        {
            if (IsReadOnly) // Don't need the lock once we're read-only.
            {
                //gateway to our alternate inner dictionary
                return m_Dictionary.ContainsKey(name);
            }

            lock (m_Definition.Lock) // But we do need the lock when it may still be changing.
            {
                //gateway to our alternate inner dictionary
                return m_Dictionary.ContainsKey(name);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="name">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string name, out IEventMetricValueDefinition value)
        {
            if (IsReadOnly) // Don't need the lock once we're read-only.
            {
                //gateway to our inner dictionary try get value
                return m_Dictionary.TryGetValue(name, out value);
            }

            lock (m_Definition.Lock) // But we do need the lock when it may still be changing.
            {
                //gateway to our inner dictionary try get value
                return m_Dictionary.TryGetValue(name, out value);
            }
        }

        /// <summary>
        /// Retrieve the index of a metric value definition by its name
        /// </summary>
        /// <param name="name">The value name to locate in the collection</param>
        /// <remarks>Items are identified using ordinal, case insensitive string comparisons.  If no value exists with the provided name an exception will be thrown.</remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if no value exists with the provided name.</exception>
        public int IndexOf(string name)
        {
            IEventMetricValueDefinition value = this[name];
            return IndexOf(value);
        }

        #endregion

        #region IEnumerable<MetricPacket> Members

        IEnumerator<IEventMetricValueDefinition> IEnumerable<IEventMetricValueDefinition>.GetEnumerator()
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
        /// Searches for the specified object and returns the zero-based index of it within the dictionary if it is found.
        /// </summary>
        /// <param name="item">The object to search for</param>
        /// <returns>The zero based index of the object within the dictionary or -1 if not found.</returns>
        public int IndexOf(IEventMetricValueDefinition item)
        {
            if (m_Definition.IsReadOnly) // && ReferenceEquals(item.Definition, Definition))
            {
                int index = ((EventMetricValueDefinition)item).MyIndex;
                if (index < 0)
                {
                    // Wasn't found, do a scan the hard way.
                    index = m_List.IndexOf(item);
                    if (index >= 0)
                        ((EventMetricValueDefinition)item).MyIndex = index; // And if we found it, remember for next time.
                }
                return index;
            }

            // Otherwise, we aren't necessarily final (could be removes?), so punt it the hard way.  We need the lock.
            lock (m_Definition.Lock)
            {
                return m_List.IndexOf(item);
            }
        }

        ///<summary>Inserting objects by index is not supported because the collection is sorted.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        public void Insert(int index, IEventMetricValueDefinition item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        ///<summary>Removing objects by index is not supported.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Retrieve the metric definition by numeric index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IEventMetricValueDefinition this[int index]
        {
            get
            {
                if (IsReadOnly) // Don't need the lock once we're read-only.
                {
                    return m_List[index];
                }

                lock (m_Definition.Lock) // But we do need the lock when it may still be changing.
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
        /// Retrieve metric object by its name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IEventMetricValueDefinition this[string name]
        {
            get
            {
                if (IsReadOnly) // Don't need the lock once we're read-only.
                {
                    return m_Dictionary[name];
                }

                lock (m_Definition.Lock) // But we do need the lock when it may still be changing.
                {
                    return m_Dictionary[name];
                }
            }
        }

        #endregion

        #region ICollection<EventMetricValueDefinition> Members


        /// <summary>
        /// Add an existing value definition item to this collection
        /// </summary>
        /// <param name="item">An existing value definition item associated with our metric definition</param>
        public void Add(IEventMetricValueDefinition item)
        {
            //we can't do the read-only check here because we use this method to add existing objects during rehydration

            //make sure the input isn't null 
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            //make sure it's associated with our definition
            if ((EventMetricDefinition)item.Definition != m_Definition)
            {
                throw new ArgumentException("The provided value definition item is not associated with our metric definition", nameof(item));
            }

            lock (m_Definition.Lock)
            {
                //and make sure it isn't a duplicate key
                if (m_Dictionary.ContainsKey(item.Name))
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.CurrentCulture, "There is already a value definition item with the name {0}", item.Name), nameof(item));
                }

                //and finally what the hell, go ahead and add it.
                m_List.Add(item);
                m_Dictionary.Add(item.Name, item);
            }

            //and fire our event, outside the lock
            OnCollectionChanged(new CollectionChangedEventArgs<IEventMetricValueDefinitionCollection, IEventMetricValueDefinition>(this, item, CollectionAction.Added));
        }

        /// <summary>
        /// Clearing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        public void Clear()
        {
            lock (m_Definition.Lock)
            {
                if (IsReadOnly)
                {
                    throw new NotSupportedException("The collection is read-only");
                }

                m_Dictionary.Clear();
                m_List.Clear();
            }

            //and raise the event so our caller knows we're cleared
            OnCollectionChanged(new CollectionChangedEventArgs<IEventMetricValueDefinitionCollection, IEventMetricValueDefinition>(this, null, CollectionAction.Cleared));
        }

        /// <summary>
        /// Indicates whether the collection already contains the specified definition object
        /// </summary>
        /// <remarks>Even if the object doesn't exist in the collection, if another object with the same key exists then 
        /// an exception will be thrown if the supplied object is added to the collection.  See Add for more information.</remarks>
        /// <param name="item">The event metric value definition object to look for</param>
        /// <returns>True if the object already exists in the collection, false otherwise</returns>
        public bool Contains(IEventMetricValueDefinition item)
        {
            //here we are relying on the fact that the comment object implements IComparable sufficiently to guarantee uniqueness
            if (IsReadOnly) // Don't need the lock once we're read-only.
            {
                return m_List.Contains(item);
            }

            lock (m_Definition.Lock) // But we do need the lock when it may still be changing.
            {
                return m_List.Contains(item);
            }
        }

        /// <summary>
        /// Copy the current list of event metric value definitions into the provided array starting at the specified index.
        /// </summary>
        /// <remarks>The array must be large enough to handle the entire contents of this dictionary starting at the provided array index.</remarks>
        /// <param name="array">The array to copy into</param>
        /// <param name="arrayIndex">The index to start inserting at</param>
        public void CopyTo(IEventMetricValueDefinition[] array, int arrayIndex)
        {
            if (IsReadOnly) // Don't need the lock once we're read-only.
            {
                m_List.CopyTo(array, arrayIndex);
            }
            else
            {
                lock (m_Definition.Lock) // But we do need the lock when it may still be changing.
                {
                    m_List.CopyTo(array, arrayIndex);
                }
            }
        }

        /// <summary>
        /// Copy the current list of event metric value definitions into a new array.
        /// </summary>
        /// <returns>A new array containing all of the event metric value definitions in this collection.</returns>
        public IEventMetricValueDefinition[] ToArray()
        {
            if (IsReadOnly) // Don't need the lock once we're read-only.
            {
                return m_List.ToArray();
            }

            lock (m_Definition.Lock) // But we do need the lock when it may still be changing.
            {
                return m_List.ToArray();
            }
        }

        /// <summary>
        /// The number of items currently in the dictionary.
        /// </summary>
        public int Count
        {
            get
            {
                if (IsReadOnly) // Don't need the lock once we're read-only.
                {
                    return m_List.Count;
                }

                lock (m_Definition.Lock) // But we do need the lock when it may still be changing.
                {
                    return m_List.Count;
                }
            }
        }

        /// <summary>
        /// Indicates if the definition is considered read only.  
        /// </summary>
        public bool IsReadOnly { get { return m_Definition.IsReadOnly; } }

        // Note: Apparently this documentation is out of date?  Remove apparently *is* supported unless IsReadOnly.
        /// <summary>
        /// Removing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        /// <param name="item">The EventMetricValueDefinition item to remove.</param>
        public bool Remove(IEventMetricValueDefinition item)
        {
            bool itemRemoved = false;

            lock (m_Definition.Lock)
            {
                if (IsReadOnly)
                {
                    throw new NotSupportedException("The collection is read-only");
                }

                //do a safe remove of the victim in the dictionary and list, if they are still present
                //if they aren't, we have a problem
                if (m_Dictionary.ContainsKey(item.Name))
                {
                    m_Dictionary.Remove(item.Name);
                    itemRemoved = true;
                }

                if (m_List.Contains(item))
                {
                    m_List.Remove(item);
                    itemRemoved = true;
                }
            }

            //and fire our event if there was really something to remove
            if (itemRemoved)
            {
                OnCollectionChanged(new CollectionChangedEventArgs<IEventMetricValueDefinitionCollection, IEventMetricValueDefinition>(this, item, CollectionAction.Removed));
            }

            return itemRemoved;
        }

        /// <summary>
        /// Scan the collection and mark each value with its index (only once IsReadOnly is true).
        /// </summary>
        public void SetAllIndex()
        {
            lock (m_Definition.Lock)
            {
                if (IsReadOnly) // Don't do it until the definition is actually locked.
                {
                    int index = 0;
                    foreach (EventMetricValueDefinition valueDefinition in m_List)
                    {
                        valueDefinition.MyIndex = index; // Efficiently set the cached index for all value columns.
                        index++;
                    }
                }
            }
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// This method is called every time a collection change event occurs to allow inheritors to override the change event.
        /// </summary>
        /// <remarks>If overridden, it is important to call this base implementation to actually fire the event.</remarks>
        /// <param name="e"></param>
        protected virtual void OnCollectionChanged(CollectionChangedEventArgs<IEventMetricValueDefinitionCollection, IEventMetricValueDefinition> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<IEventMetricValueDefinitionCollection, IEventMetricValueDefinition>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Internal Properties and Methods

        internal EventMetricValueDefinition Add(EventMetricValueDefinitionPacket packet)
        {
            //Even though this is an internal method used just during rehydration of data, we are going to 
            //enforce all the integrity checks to be sure we have good data

            //The one thing we CAN'T check is read only, because we're used when the collection IS read only.

            //make sure we got everything we require
            if (string.IsNullOrEmpty(packet.Name))
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Type == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            lock (m_Definition.Lock)
            {
                //make sure the name is unique
                if (m_Dictionary.ContainsKey(packet.Name))
                {
                    throw new ArgumentException("There is already a value definition with the provided name.", nameof(packet));
                }

                //create a new value definition
                var newDefinition = new EventMetricValueDefinition(m_Definition, packet);

                //forward the call to our one true add method
                Add(newDefinition);

                // set our index for the read-time position of the value.
                newDefinition.MyIndex = m_List.IndexOf(newDefinition);

                //and return the new object to our caller so they have the object we created from their input.
                return newDefinition;
            }
        }

        #endregion
    }
}
