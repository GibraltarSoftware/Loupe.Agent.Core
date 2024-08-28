using System;
using System.Collections.Generic;
using Loupe.Extensibility.Data;

#pragma warning disable 1591
namespace Gibraltar.Monitor
{
    /// <summary>
    /// A collection of logMessageGroups, ordered by date/time and accessible by unique ID or numerical index.
    /// </summary>
    public class LogMessageGroupCollection : IList<LogMessageGroup>
    {
        private readonly Dictionary<string, LogMessageGroup> m_Dictionary = new Dictionary<string, LogMessageGroup>(StringComparer.InvariantCultureIgnoreCase);
        private readonly SortedList<LogMessageGroup, LogMessageGroup> m_List = new SortedList<LogMessageGroup, LogMessageGroup>(); //logMessageGroup itself implements IComparable, so it will determine order
        private readonly object m_Lock = new object();

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs<LogMessageGroupCollection, LogMessageGroup>> CollectionChanged;

        /// <summary>
        /// Create a new log message group collection for the root log message group.
        /// </summary>
        internal LogMessageGroupCollection()
        {
        }

        /// <summary>
        /// Create a new log message group collection for child groups of the provided log message group
        /// </summary>
        /// <param name="parent"></param>
        internal LogMessageGroupCollection(LogMessageGroup parent)
        {
            Parent = parent;
        }

        /// <summary>
        /// The parent group that owns this group collection.  May be null in the case of the root message group collection.
        /// </summary>
        public LogMessageGroup Parent { get; private set; }

        /// <summary>
        /// Called whenever the collection changes.
        /// </summary>
        /// <param name="e"></param>
        /// <remarks>Note to inheritors:  If overriding this method, you must call the base implementation to ensure
        /// that the appropriate events are raised.</remarks>
        protected virtual void OnCollectionChanged(CollectionChangedEventArgs<LogMessageGroupCollection, LogMessageGroup> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<LogMessageGroupCollection, LogMessageGroup>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary 
                return m_Dictionary.ContainsKey(key);
            }
        }


        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out LogMessageGroup value)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_Dictionary.TryGetValue(key, out value);
            }
        }

        IEnumerator<LogMessageGroup> IEnumerable<LogMessageGroup>.GetEnumerator()
        {
            //we use the sorted list for enumeration
            return m_List.Values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            //we use the sorted list for enumeration
            return m_List.GetEnumerator();
        }

        public int IndexOf(LogMessageGroup item)
        {
            lock (m_Lock)
            {
                return m_List.IndexOfKey(item);
            }
        }

        public void Insert(int index, LogMessageGroup item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            LogMessageGroup victim;
            lock (m_Lock)
            {
                //find the item at the requested location
                victim = m_List.Values[index];
            }

            //and pass that to our normal remove method.  Must be called outside the lock because it fires an event.
            Remove(victim);
        }

        public LogMessageGroup this[int index]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_List.Values[index];
                }
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Add the specified LogMessageGroup item to the collection
        /// </summary>
        /// <param name="item">The new LogMessageGroup item to add</param>
        public void Add(LogMessageGroup item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "A new logMessageGroup item must be provided to add it to the collection.");

            lock (m_Lock)
            {
                //add it to both lookup collections
                m_Dictionary.Add(item.Name, item);
                m_List.Add(item, item);
            }

            //and fire our event outside the lock
            OnCollectionChanged(new CollectionChangedEventArgs<LogMessageGroupCollection, LogMessageGroup>(this, item, CollectionAction.Added));
        }

        /// <summary>
        /// Add a new group to this group collection with the provided name and the provided initial log message
        /// </summary>
        /// <param name="groupName">The unique name of the group within this collection</param>
        /// <param name="message">The first message to add to the group</param>
        /// <returns>The new log message group that was added</returns>
        public LogMessageGroup Add(string groupName, ILogMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            LogMessageGroup newGroup = new LogMessageGroup(groupName, Parent, message);

            Add(newGroup);

            return newGroup;
        }

        /// <summary>
        /// Clear the entire contents of the logMessageGroup collection
        /// </summary>
        public void Clear()
        {
            int count;
            lock (m_Lock)
            {
                //Only do this if we HAVE something, since events are fired.
                count = m_List.Count;
                if (count > 0)
                {
                    m_List.Clear();
                    m_Dictionary.Clear();
                }
            }

            if (count > 0)
            {
                //and raise the event so our caller knows we're cleared
                OnCollectionChanged(new CollectionChangedEventArgs<LogMessageGroupCollection, LogMessageGroup>(this, null, CollectionAction.Cleared));
            }
        }

        /// <summary>
        /// Indicates if the supplied collection object is present in the collection
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(LogMessageGroup item)
        {
            lock (m_Lock)
            {
                //here we are relying on the fact that the logMessageGroup object implements IComparable sufficiently to guarantee uniqueness
                return m_List.ContainsKey(item);
            }
        }

        public void CopyTo(LogMessageGroup[] array, int arrayIndex)
        {
            lock (m_Lock)
            {
                m_List.Values.CopyTo(array, arrayIndex);
            }
        }

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

        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Remove the specified LogMessageGroup item.  If the LogMessageGroup isn't in the collection, no exception is thrown.
        /// </summary>
        /// <param name="item">The LogMessageGroup item to remove.</param>
        public bool Remove(LogMessageGroup item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A logMessageGroup item must be provided to remove it from the collection.");
            }

            bool result;

            lock (m_Lock)
            {
                //we have to remove it from both collections, and we better not raise an error if not there.
                result = m_Dictionary.Remove(item.Name);

                //here we are relying on the IComparable implementation being a unique key and being fast.
                result |= m_List.Remove(item);
            }

            //and fire our event if there was really something to remove
            if (result)
            {
                OnCollectionChanged(new CollectionChangedEventArgs<LogMessageGroupCollection, LogMessageGroup>(this, item, CollectionAction.Removed));
            }

            return result;
        }
    }
}
