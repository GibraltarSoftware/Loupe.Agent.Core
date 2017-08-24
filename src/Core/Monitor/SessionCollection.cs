using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Gibraltar.Data;
using IEnumerator=System.Collections.IEnumerator;




namespace Gibraltar.Monitor
{
    /// <summary>
    /// A collection of loaded sessions
    /// </summary>
    /// <remarks>Used to load sessions from their raw file streams</remarks>
    public class SessionCollection : IList<Session>
    {
        private readonly Dictionary<Guid, Session> m_SessionsById = new Dictionary<Guid, Session>();
        private readonly SortedList<Session, Session> m_SessionsList = new SortedList<Session, Session>();
        private readonly object m_Lock = new object(); //MT Safety lock

        /// <summary>
        /// Add a session from the specified file
        /// </summary>
        /// <param name="fileNamePath">The fully qualified file name to load.</param>
        /// <returns>The session object that was affected</returns>
        public Session Add(string fileNamePath)
        {
            //NOTE: This is deliberately NOT a using block; the inner Add will dispose it.
            var fileStream = File.Open(fileNamePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Add(fileStream, true);
        }

        /// <summary>
        /// Add a session from the provided GLF File Stream
        /// </summary>
        /// <param name="fileStream">A file stream of a GLF File to read (may always be disposed by caller upon return).</param>
        /// <returns>The session object that was affected</returns>
        /// <remarks>The stream will be copied immediately and its Position will be restored before returning, so the caller is
        /// responsible for eventually disposing it.  Use the other overload to pass a stream which is no longer needed by the
        /// caller.</remarks>
        public Session Add(Stream fileStream)
        {
            return Add(fileStream, false); // Pass through to the main one, telling it to copy the stream.
        }

        /// <summary>
        /// Add a session from the provided GLF File stream
        /// </summary>
        /// <param name="fileStream">A file stream of a GLF File to read.</param>
        /// <param name="useOriginalStream">If true, the caller no longer owns the stream and must not further use or dispose
        /// it. If false, the method will copy the contents of the stream before returning and will restore its Position,
        /// so the caller is responsible for eventually disposing it.</param>
        /// <returns>The session object that was affected.</returns>
        public Session Add(Stream fileStream, bool useOriginalStream)
        {
            if (fileStream == null)
            {
                throw new ArgumentNullException(nameof(fileStream));
            }

            long originalPosition = fileStream.Position;
            if (GLFReader.IsGLF(fileStream) == false) // This also throws an exception if the stream is not seekable.
            {
                fileStream.Position = originalPosition;
                throw new ArgumentException("The provided data stream is not a valid GLF session stream.", nameof(fileStream));
            }

            Stream newStream;
            if (useOriginalStream == false)
            {
                // Uh-oh, the caller wants to keep the stream themselves.  We need to punt a copy from the original stream.
                fileStream.Position = 0; // Reset its position to the start of the file to copy from the start.
                newStream = FileSystemTools.GetTempFileStreamCopy(fileStream); // Get a delete-on-close temp file copied from it.
                newStream.Position = originalPosition; // Should it do this?

                fileStream.Position = originalPosition; // And set it back where it had been.
            }
            else
            {
                // Otherwise, they're saying the file will stick around for us.  We own it now.  Or rather, the GLFReader will.
                newStream = fileStream;
            }

            GLFReader glfReader = new GLFReader(newStream);

            //now forward to our GLF Reader add which is shared with other things
            Session newSession = Add(glfReader);

            return newSession;
        }

        internal Session Add(GLFReader sessionFileReader)
        {
            if (sessionFileReader == null)
            {
                throw new ArgumentNullException(nameof(sessionFileReader));
            }

            Session session = null;

            if (sessionFileReader.IsSessionStream)
            {
                lock (m_Lock)
                {
                    //OK, it's a GLF.  But do we already have a session this relates to?
                    m_SessionsById.TryGetValue(sessionFileReader.SessionHeader.Id, out session);

                    if (session == null)
                    {
                        //gotta make a new session
                        session = new Session(sessionFileReader);
                        Add(session);
                    }
                    else
                    {
                        //there is already a session - but does it have this fragment?
                        session.Fragments.Add(sessionFileReader);
                    }
                }
            }

            return session;
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public void Add(Session item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            lock (m_Lock)
            {
                //make sure it isn't already here by name
                if (m_SessionsById.ContainsKey(item.Id))
                {
                    throw new ArgumentException("There is already a field definition with the provided name.", nameof(item));
                }

                //otherwise we just add it.
                m_SessionsById.Add(item.Id, item);
                m_SessionsList.Add(item, item);
            }
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. </exception>
        public void Clear()
        {
            lock (m_Lock)
            {
                m_SessionsById.Clear();
                m_SessionsList.Clear();
            }
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        public bool Contains(Session item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            lock (m_Lock)
            {
                return m_SessionsById.ContainsKey(item.Id);
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array" /> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex" /> is less than 0.</exception>
        public void CopyTo(Session[] array, int arrayIndex)
        {
            lock (m_Lock)
            {
                m_SessionsList.Values.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<Session> GetEnumerator()
        {
            return m_SessionsList.Values.GetEnumerator();
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
        /// Removes the occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <returns>
        /// true if <paramref name="sessionId" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="sessionId" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        /// <param name="sessionId">They of the session to remove.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public bool Remove(Guid sessionId)
        {
            bool itemRemoved = false;
            lock (m_Lock)
            {
                Session victim;
                if (TryGetValue(sessionId, out victim))
                {
                    //just try to remove it from the two collections, you never know.
                    if (m_SessionsList.Remove(victim))
                    {
                        itemRemoved = true;
                    }

                    if (m_SessionsById.Remove(sessionId))
                    {
                        itemRemoved = true;
                    }
                }
            }

            return itemRemoved;
        }

        /// <summary>
        /// Removes the occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public bool Remove(Session item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            bool itemRemoved = false;
            lock (m_Lock)
            {
                //just try to remove it from the two collections, you never know.
                if (m_SessionsList.Remove(item))
                {
                    itemRemoved = true;
                }

                if (m_SessionsById.Remove(item.Id))
                {
                    itemRemoved = true;
                }
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
                    return m_SessionsList.Count;
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
        public int IndexOf(Session item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            lock (m_Lock)
            {
                return m_SessionsList.IndexOfValue(item);
            }
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1" />.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="ID" /> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="ID">The unique ID of the session to locate in the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        public int IndexOf(Guid ID)
        {
            lock (m_Lock)
            {
                return IndexOf(m_SessionsById[ID]);
            }
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1" /> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
        /// <param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1" /> is read-only.</exception>
        public void Insert(int index, Session item)
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
        public Session this[int index]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_SessionsList.Values[index];
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
        public Session this[Guid ID]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_SessionsById[ID];
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
        public bool TryGetValue(Guid key, out Session value)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_SessionsById.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Determines whether the collection contaions an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element iwth the key; otherwise, false.</returns>
        public bool ContainsKey(Guid key)
        {
            lock (m_Lock)
            {
                return m_SessionsById.ContainsKey(key);
            }
        }
    }
}
