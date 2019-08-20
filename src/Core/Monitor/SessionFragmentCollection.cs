using System;
using System.Collections;
using System.Collections.Generic;
using Loupe.Core.Data;



namespace Loupe.Core.Monitor
{
    /// <summary>
    /// An ordered list of the individual fragments that were captured for the session.
    /// </summary>
    public class SessionFragmentCollection : IList<SessionFragment>
    {
        private readonly Session m_Session;
        private readonly Dictionary<Guid, SessionFragment> m_SessionFilesById = new Dictionary<Guid, SessionFragment>();
        private readonly SortedList<DateTimeOffset, SessionFragment> m_SessionFilesList = new SortedList<DateTimeOffset, SessionFragment>();
        private readonly object m_Lock = new object(); //MT Safety lock

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs<SessionFragmentCollection, SessionFragment>> CollectionChanged;

        internal SessionFragmentCollection(Session session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            m_Session = session;
        }


        #region Public Properties and Methods

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<SessionFragment> GetEnumerator()
        {
            lock (m_Lock)
            {
                return m_SessionFilesList.Values.GetEnumerator();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Add a new session file from the provided data stream.
        /// </summary>
        /// <param name="glfReader"></param>
        /// <returns>The session file object for the provided data stream.</returns>
        internal SessionFragment Add(GLFReader glfReader)
        {
            if (glfReader == null)
            {
                throw new ArgumentNullException(nameof(glfReader));
            }

            if (glfReader.IsSessionStream == false)
            {
                throw new ArgumentException("The provided file is not a valid session file and can't be loaded.");
            }

            SessionFragment sessionFragment;

            lock (m_Lock)
            {
                //make sure we don't already have this file
                m_SessionFilesById.TryGetValue(glfReader.SessionHeader.FileId, out sessionFragment);
                if (sessionFragment == null)
                {
                    //we don't already have this file - add it
                    sessionFragment = new SessionFragment(glfReader);
                    AddItem(sessionFragment); //not our Add overload because we don't want to send the collection changed event while in our lock.
                }

                //and lets refresh our session's cached information if we are increasing values.
                m_Session.IntegrateHeader(glfReader.SessionHeader);
            }

            OnCollectionChanged(new CollectionChangedEventArgs<SessionFragmentCollection, SessionFragment>(this, sessionFragment, CollectionAction.Added));

            return sessionFragment;
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public void Add(SessionFragment item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            lock (m_Lock)
            {
                //make sure it isn't already here by name
                if (m_SessionFilesById.ContainsKey(item.Id))
                {
                    throw new ArgumentException("There is already a session file with the provided ID.", nameof(item));
                }

                AddItem(item);
            }

            OnCollectionChanged(new CollectionChangedEventArgs<SessionFragmentCollection, SessionFragment>(this, item, CollectionAction.Added));
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. </exception>
        public void Clear()
        {
            lock (m_Lock)
            {
                m_SessionFilesById.Clear();
                m_SessionFilesList.Clear();
            }

            OnCollectionChanged(new CollectionChangedEventArgs<SessionFragmentCollection, SessionFragment>(this, null, CollectionAction.Cleared));
        }

        /// <summary>
        /// Determines whether the dictionary contains a specific value.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item" /> is found in the dictionary; otherwise, false.
        /// </returns>
        /// <param name="item">The object to locate in the dictionary.</param>
        public bool Contains(SessionFragment item)
        {
            if (item == null) return false;
            lock (m_Lock)
            {
                return m_SessionFilesById.ContainsKey(item.Id);
            }
        }

        /// <summary>
        /// Determines whether the dictionary contains an item with the provided key.
        /// </summary>
        /// <returns>
        /// true if <paramref name="fileID" /> is found in the dictionary; otherwise, false.
        /// </returns>
        /// <param name="fileID">The ID of the object to locate in the dictionary.</param>
        public bool Contains(Guid fileID)
        {
            lock (m_Lock)
            {
                return m_SessionFilesById.ContainsKey(fileID);
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array" /> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex" /> is less than 0.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="array" /> is multidimensional.-or-<paramref name="arrayIndex" /> is equal to or greater than the length of <paramref name="array" />.-or-The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1" /> is greater than the available space from <paramref name="arrayIndex" /> to the end of the destination <paramref name="array" />.-or-Type <cref name="Loupe.Agent.SessionFragment" /> cannot be cast automatically to the type of the destination <paramref name="array" />.</exception>
        public void CopyTo(SessionFragment[] array, int arrayIndex)
        {
            lock (m_Lock)
            {
                m_SessionFilesList.Values.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public bool Remove(SessionFragment item)
        {
            if (item == null) return false;

            bool itemRemoved = false;
            lock (m_Lock)
            {
                //just try to remove it from the two collections, you never know.
                if (m_SessionFilesList.Remove(item.StartDateTime))
                {
                    itemRemoved = true;
                }

                if (m_SessionFilesById.Remove(item.Id))
                {
                    itemRemoved = true;
                }
            }

            if (itemRemoved)
            {
                OnCollectionChanged(new CollectionChangedEventArgs<SessionFragmentCollection, SessionFragment>(this, item, CollectionAction.Removed));
            }

            return itemRemoved;
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <returns>
        /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        public int Count
        {
            get
            {
                lock (m_Lock)
                {
                    return m_SessionFilesList.Count;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.
        /// </returns>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1" />.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="item" /> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        public int IndexOf(SessionFragment item)
        {
            lock (m_Lock)
            {
                return m_SessionFilesList.IndexOfValue(item);
            }
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1" /> by its id.
        /// </summary>
        /// <returns>
        /// The index of the session fragment with the provided <paramref name="ID" /> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="ID">The unique ID of the session fragment to locate in the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        public int IndexOf(Guid ID)
        {
            lock (m_Lock)
            {
                return IndexOf(m_SessionFilesById[ID]);
            }
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1" /> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
        /// <param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public void Insert(int index, SessionFragment item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Removes the <see cref="T:System.Collections.Generic.IList`1" /> item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public SessionFragment this[int index]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_SessionFilesList.Values[index];
                }
            }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="ID">The name of the field to get.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="ID" /> is not a valid name in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public SessionFragment this[Guid ID]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_SessionFilesById[ID];
                }
            }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out SessionFragment value)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_SessionFilesById.TryGetValue(key, out value);
            }
        }


        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Called whenever the collection changes.
        /// </summary>
        /// <param name="e"></param>
        /// <remarks>Note to inheritors:  If overriding this method, you must call the base implmenetation to ensure
        /// that the appropriate events are raised.</remarks>
        protected virtual void OnCollectionChanged(CollectionChangedEventArgs<SessionFragmentCollection, SessionFragment> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<SessionFragmentCollection, SessionFragment>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Add a new session fragment to the collection.
        /// </summary>
        /// <param name="item"></param>
        private void AddItem(SessionFragment item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            lock (m_Lock)
            {
                //otherwise we just add it.
                m_SessionFilesById.Add(item.Id, item);
                m_SessionFilesList.Add(item.StartDateTime, item);

                //If the objects for this session file haven't been integrated into the session, we need to track that so we'll load them on demand.
                if (item.Loaded == false)
                {
                    item.Loaded = true; //if there's a problem, lets not try this again.
                    m_Session.AddPacketStream(item.Reader);
                }
            }
        }

        #endregion
    }
}
