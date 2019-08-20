using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Loupe.Extensibility.Data;
using Loupe.Monitor;
using Loupe.Metrics;


namespace Loupe.Agent.Metrics
{
    /// <summary>
    /// A collection of event values for the parent event metric definition.
    /// </summary>
    /// <remarks>This object is automatically created by the Event Metric Definition and is accessible through the Values property.</remarks>
    internal sealed class EventMetricValueDefinitionCollection : IList<EventMetricValueDefinition>
    {
        private readonly Monitor.EventMetricValueDefinitionCollection m_WrappedCollection;
        private readonly Dictionary<IEventMetricValueDefinition, EventMetricValueDefinition> m_Externalized =
            new Dictionary<IEventMetricValueDefinition, EventMetricValueDefinition>();

        private readonly EventMetricDefinition m_Definition;

        private List<EventMetricValueDefinition> m_ValuesList;

        /// <summary>
        /// Create a new values dictionary for the specified metric definition
        /// </summary>
        /// <param name="definition">The parent metric definition object that will own this dictionary.</param>
        internal EventMetricValueDefinitionCollection(EventMetricDefinition definition)
        {
            m_Definition = definition;
            m_WrappedCollection = (Monitor.EventMetricValueDefinitionCollection)definition.WrappedDefinition.Values;
            m_WrappedCollection.CollectionChanged += WrappedCollection_CollectionChanged;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Create a new value definition with the supplied name and type.  The name must be unique in this collection.
        /// </summary>
        /// <remarks>Internally, only simple type are supported.  Any non-numeric, non-DateTimeOffset type will be converted
        /// to a string using the default ToString capability when it is recorded.</remarks>
        /// <param name="name">The unique name for this value definition.</param>
        /// <param name="type">The simple type of this value.</param>
        /// <returns>The newly created value definition.</returns>
        public EventMetricValueDefinition Add(string name, Type type)
        {
            //forward the call to our larger add method
            return Add(name, type, SummaryFunction.Count, null, null, null);
        }

        /// <summary>
        /// Create a new value definition with the supplied name and type.  The name must be unique in this collection.
        /// </summary>
        /// <remarks>Internally, only simple type are supported.  Any non-numeric, non-DateTimeOffset type will be converted
        /// to a string using the default ToString capability when it is recorded.</remarks>
        /// <param name="name">The unique name for this value definition.</param>
        /// <param name="type">The simple type of this value.</param>
        /// <param name="summaryFunction">The default way that individual samples of this value column can be aggregated
        /// to create a graphable summary. (Use SummaryFunction.Count for non-numeric types.)</param>
        /// <param name="unitCaption">A displayable caption for the units this value represents, or null for unit-less values.</param>
        /// <param name="caption">The end-user display caption for this value.</param>
        /// <param name="description">The end-user description for this value.</param>
        /// <returns>The newly created value definition.</returns>
        /// <exception cref="ArgumentNullException">The provided name or type are null.</exception>
        /// <exception cref="ArgumentException">There is already a definition with the provided name</exception>
        public EventMetricValueDefinition Add(string name, Type type, SummaryFunction summaryFunction, string unitCaption, string caption, string description)
        {
            lock (m_Definition.Lock)
            {
                //if we are read-only, you can't add a new value
                if (IsReadOnly)
                {
                    throw new InvalidOperationException("The collection is read-only");
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
                if (ContainsKey(name))
                {
                    throw new ArgumentException("There is already a value definition with the provided name.", nameof(name));
                }

                //create a new value definition
                EventMetricValueDefinition newDefinition = Externalize(m_WrappedCollection.Add(name, type, caption, description));
                newDefinition.SummaryFunction = summaryFunction;
                newDefinition.UnitCaption = unitCaption;

                //and return the new object to our caller so the have the object we created from their input.
                return newDefinition;
            }
        }


        /// <summary>
        /// The metric definition this value is associated with.
        /// </summary>
        public EventMetricDefinition Definition { get { return m_Definition; } }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="name">The value name to locate in the collection</param>
        /// <returns>True if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string name)
        {
            //gateway to our alternate inner dictionary
            return m_WrappedCollection.ContainsKey(name);
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="name">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string name, out EventMetricValueDefinition value)
        {
            lock (m_Definition.Lock)
            {
                //gateway to our inner dictionary try get value
                bool foundValue = m_WrappedCollection.TryGetValue(name, out var internalValue);
                value = foundValue ? Externalize(internalValue) : null;

                return foundValue;
            }
        }

        #endregion

        #region IEnumerable<MetricPacket> Members

        IEnumerator<EventMetricValueDefinition> IEnumerable<EventMetricValueDefinition>.GetEnumerator()
        {
            IEnumerable<IEventMetricValueDefinition> enumerable = m_WrappedCollection;
            return new Enumerator(this, enumerable.GetEnumerator());
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            IEnumerable<IEventMetricValueDefinition> enumerable = m_WrappedCollection;
            return new Enumerator(this, enumerable.GetEnumerator());
        }

        #endregion

        #region IList<MetricPacket> Members

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of it within the dictionary if it is found.
        /// </summary>
        /// <param name="item">The object to search for</param>
        /// <returns>The zero based index of the object within the dictionary or -1 if not found.</returns>
        public int IndexOf(EventMetricValueDefinition item)
        {
            if (item == null) return -1;

            lock (m_Definition.Lock)
            {
                return m_WrappedCollection.IndexOf(item.WrappedValueDefinition);
            }
        }

        ///<summary>Inserting objects by index is not supported because the collection is sorted.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        /// <exception cref="NotSupportedException">An attempt was made to set an object by index which is not supported.</exception>
        public void Insert(int index, EventMetricValueDefinition item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException("Setting an object by index is not supported.");
        }

        ///<summary>Removing objects by index is not supported.</summary>
        ///<remarks>This method is implemented only for IList interface support and will throw an exception if called.</remarks>
        /// <exception cref="NotSupportedException">An attempt was made to remove an object by index which is not supported.</exception>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException("Removing an object by index is not supported.");
        }

        /// <summary>
        /// Retrieve the metric definition by numeric index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">An attempt was made to set an object by index which is not supported.</exception>
        public EventMetricValueDefinition this[int index]
        {
            get
            {
                lock (m_Definition.Lock)
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
        /// Retrieve metric object by its name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public EventMetricValueDefinition this[string name]
        {
            get
            {
                lock (m_Definition.Lock)
                {
                    return Externalize(m_WrappedCollection[name]);
                }
            }
        }

        #endregion

        #region ICollection<EventMetricValueDefinition> Members


        /// <summary>
        /// Add an existing value definition item to this collection
        /// </summary>
        /// <param name="item">An existing value definition item associated with our metric definition</param>
        /// <exception cref="ArgumentNullException">The provided item was null.</exception>
        /// <exception cref="ArgumentException">The provided value definition item is not associated with our metric definition -or- 
        /// there is already a value definition with the same name as the provided item.</exception>
        public void Add(EventMetricValueDefinition item)
        {
            //we can't do the read-only check here because we use this method to add existing objects during re-hydration

            //make sure the input isn't null 
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            //make sure it's associated with our definition
            if (item.Definition != m_Definition)
            {
                throw new ArgumentException("The provided value definition item is not associated with our metric definition", nameof(item));
            }

            lock (m_Definition.Lock)
            {
                //and make sure it isn't a duplicate key
                if (ContainsKey(item.Name))
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.CurrentCulture, "There is already a value definition item with the name {0}", item.Name), nameof(item));
                }

                //and finally what the hell, go ahead and add it.
                m_WrappedCollection.Add(item.WrappedValueDefinition);
                m_Externalized[item.WrappedValueDefinition] = item;
            }

            //and fire our event, outside the lock
            //OnCollectionChanged(new CollectionChangedEventArgs<EventMetricValueDefinitionCollection, EventMetricValueDefinition>(this, item, CollectionAction.Added));
        }

        /// <summary>
        /// Clearing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        /// <exception cref="InvalidOperationException">The definition has been committed and is now read-only</exception>
        public void Clear()
        {
            lock (m_Definition.Lock)
            {
                if (IsReadOnly)
                {
                    throw new InvalidOperationException("The collection is read-only");
                }

                m_WrappedCollection.Clear();
                m_Externalized.Clear();
            }

            //and raise the event so our caller knows we're cleared
            //OnCollectionChanged(new CollectionChangedEventArgs<EventMetricValueDefinitionCollection, EventMetricValueDefinition>(this, null, CollectionAction.Cleared));
        }

        /// <summary>
        /// Indicates whether the collection already contains the specified definition object
        /// </summary>
        /// <remarks>Even if the object doesn't exist in the collection, if another object with the same key exists then 
        /// an exception will be thrown if the supplied object is added to the collection.  See Add for more information.</remarks>
        /// <param name="item">The event metric value definition object to look for</param>
        /// <returns>True if the object already exists in the collection, false otherwise</returns>
        public bool Contains(EventMetricValueDefinition item)
        {
            //here we are relying on the fact that the comment object implements IComparable sufficiently to guarantee uniqueness
            return m_WrappedCollection.Contains(item?.WrappedValueDefinition);
        }

        /// <summary>
        /// Copy the current list of event metric value definitions into the provided array starting at the specified index.
        /// </summary>
        /// <remarks>The array must be large enough to handle the entire contents of this dictionary starting at the provided array index.</remarks>
        /// <param name="array">The array to copy into</param>
        /// <param name="arrayIndex">The index to start inserting at</param>
        public void CopyTo(EventMetricValueDefinition[] array, int arrayIndex)
        {
            int length = array.Length - arrayIndex;
            if (length <= 0)
                return;

            lock (m_Definition.Lock)
            {
                IEventMetricValueDefinition[] internalArray = m_WrappedCollection.ToArray();
                if (length > internalArray.Length)
                    length = internalArray.Length;

                for (int i = 0; i < length; i++)
                {
                    array[arrayIndex + i] = Externalize(internalArray[i]);
                }
            }
        }

        /// <summary>
        /// Copy the current list of event metric value definitions into a new array.
        /// </summary>
        /// <returns>A new array containing all of the event metric value definitions in this collection.</returns>
        public EventMetricValueDefinition[] ToArray()
        {
            lock (m_Definition.Lock) // Don't let the definition change while we check this.
            {
                if (m_ValuesList == null) // See if we have cached the externalized list since the last modification.
                {
                    // We must build the ValuesList.
                    IEventMetricValueDefinition[] internalValues = m_WrappedCollection.ToArray();
                    int count = internalValues.Length;
                    m_ValuesList = new List<EventMetricValueDefinition>(count);

                    for (int i = 0; i < count; i++)
                    {
                        m_ValuesList[i] = Externalize(internalValues[i]);
                    }
                }

                return m_ValuesList.ToArray(); // Now return a copy as an array.
            }
        }

        /// <summary>
        /// The number of items currently in the dictionary.
        /// </summary>
        public int Count
        {
            get { return m_WrappedCollection.Count; }
        }

        /// <summary>
        /// Indicates if the dictionary is considered read only.  
        /// </summary>
        public bool IsReadOnly
        {
            get { return m_Definition.IsReadOnly; }
        }

        // Note: Apparently this documentation is out of date?  Remove apparently *is* supported unless IsReadOnly.
        /// <summary>
        /// Removing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        /// <param name="item">The EventMetricValueDefinition item to remove.</param>
        public bool Remove(EventMetricValueDefinition item)
        {
            bool itemRemoved;
            lock (m_Definition.Lock)
            {
                if (IsReadOnly)
                {
                    throw new InvalidOperationException("The collection is read-only");
                }

                itemRemoved = m_WrappedCollection.Remove(item?.WrappedValueDefinition);
            }

            /*
            //and fire our event if there was really something to remove
            if (itemRemoved)
            {
                OnCollectionChanged(new CollectionChangedEventArgs<EventMetricValueDefinitionCollection, EventMetricValueDefinition>(this, item, CollectionAction.Removed));
            }
            */

            return itemRemoved;
        }

        #endregion

        #region Private Properties and Methods

        /*
        /// <summary>
        /// This method is called every time a collection change event occurs to allow inheritors to override the change event.
        /// </summary>
        /// <remarks>If overridden, it is important to call this base implementation to actually fire the event.</remarks>
        /// <param name="e"></param>
        private void OnCollectionChanged(CollectionChangedEventArgs<EventMetricValueDefinitionCollection, EventMetricValueDefinition> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<EventMetricValueDefinitionCollection, EventMetricValueDefinition>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }
        */

        private void WrappedCollection_CollectionChanged(object sender,
            CollectionChangedEventArgs<IEventMetricValueDefinitionCollection, IEventMetricValueDefinition> e1)
        {
            // We should already have the lock?
            lock (m_Definition.Lock)
            {
                // Invalidate our cache of the externalized values list, so it will be rebuilt on next access.
                m_ValuesList = null;
            }
        }

        #endregion

        #region Internal Properties and Methods

        internal Monitor.EventMetricValueDefinitionCollection WrappedCollection { get { return m_WrappedCollection; } }

        internal EventMetricValueDefinition Externalize(IEventMetricValueDefinition valueDefinition)
        {
            if (valueDefinition == null)
                return null;

            lock (m_Definition.Lock)
            {
                if (m_Externalized.TryGetValue(valueDefinition, out var externalDefinition) == false)
                {
                    externalDefinition = new EventMetricValueDefinition(m_Definition, (Monitor.EventMetricValueDefinition)valueDefinition);
                    m_Externalized[valueDefinition] = externalDefinition;
                }

                return externalDefinition;
            }
        }

        #endregion

        #region Private helper class

        private class Enumerator : IEnumerator<EventMetricValueDefinition>
        {
            private readonly IEnumerator<IEventMetricValueDefinition> m_Enumerator;
            private readonly EventMetricValueDefinitionCollection m_Collection;

            public Enumerator(EventMetricValueDefinitionCollection collection, IEnumerator<IEventMetricValueDefinition> enumerator)
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

            public EventMetricValueDefinition Current
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
