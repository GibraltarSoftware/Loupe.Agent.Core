using System;
using System.Collections;
using System.Collections.Generic;
using Loupe.Extensibility;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor.Serialization
{
    /// <summary>
    /// The session summary collection implementation for the local collection repository
    /// </summary>
    public class SessionSummaryCollection : ISessionSummaryCollection
    {
        private readonly object m_Lock = new object();
        private readonly IRepository m_Repository;
        private readonly List<ISessionSummary> m_List = new List<ISessionSummary>();
        private readonly Dictionary<Guid, ISessionSummary> m_Dictionary = new Dictionary<Guid, ISessionSummary>();

        /// <summary>
        /// Create an empty session summary collection
        /// </summary>
        public SessionSummaryCollection(IRepository repository)
        {
            m_Repository = repository;
        }

        /// <summary>
        /// Create a new collection by loading the provided summaries.
        /// </summary>
        public SessionSummaryCollection(IRepository repository, IList<ISessionSummary> sessions)
        {
            m_Repository = repository;

            m_List.AddRange(sessions);
            foreach (var sessionSummary in sessions)
            {
                m_Dictionary.Add(sessionSummary.Id, sessionSummary);
            }
        }

        #region Public Properties and Methods

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<ISessionSummary> GetEnumerator()
        {
            return m_List.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_List.GetEnumerator();
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        ///                 </param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        ///                 </exception>
        public void Add(ISessionSummary item)
        {
            AddItem(item);
        }

        private void AddItem(ISessionSummary item)
        {
            lock(m_Lock)
            {
                m_List.Add(item);
                m_Dictionary.Add(item.Id, item);
            }
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only. 
        ///                 </exception>
        public void Clear()
        {
            lock(m_Lock)
            {
                m_List.Clear();
                m_Dictionary.Clear();
            }
        }

        /// <summary>
        /// Indicates if the collection contains the key
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if a session summary with the key exists in the collection, false otherwise.</returns>
        public bool Contains(Guid key)
        {
            throw new NotSupportedException("The Contains method has not been implemented.");
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1"/> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> is found in the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        ///                 </param>
        public bool Contains(ISessionSummary item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            lock (m_Lock)
            {
                return m_Dictionary.ContainsKey(item.Id);
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.
        ///                 </param><param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.
        ///                 </param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.
        ///                 </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.
        ///                 </exception><exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.
        ///                     -or-
        ///                 <paramref name="arrayIndex"/> is equal to or greater than the length of <paramref name="array"/>.
        ///                     -or-
        ///                     The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.
        ///                 </exception>
        public void CopyTo(ISessionSummary[] array, int arrayIndex)
        {
            lock(m_Lock)
            {
                m_List.CopyTo(array, arrayIndex);    
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        ///                 </param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        ///                 </exception>
        public bool Remove(ISessionSummary item)
        {
            return RemoveItem(item);
        }

        private bool RemoveItem(ISessionSummary item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            //we want to be sure we remove any item with this key, not just this exact obect to preserve collection symmetry.
            if (m_Dictionary.TryGetValue(item.Id, out var ourCopy))
            {
                m_Dictionary.Remove(item.Id);
                m_List.Remove(item);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        public int Count { get { return m_List.Count; } }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, false.
        /// </returns>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire List.</summary>
        /// <param name="match">The <see cref="System.Predicate{T}">Predicate</see> delegate that defines the conditions of the elements to search for.</param>
        /// <remarks>
        /// The <see cref="System.Predicate{T}">Predicate</see> is a delegate to a method that returns true if the object passed to it matches the
        /// conditions defined in the delegate. The elements of the current List are individually passed to the <see cref="System.Predicate{T}">Predicate</see> delegate, moving forward in the List, starting with the first element and ending with the last element. Processing is
        /// stopped when a match is found.
        /// </remarks>
        /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, null.</returns>
        /// <exception caption="Argument Null Exception" cref="System.ArgumentNullException">match is a null reference (Nothing in Visual Basic)</exception>
        public ISessionSummary Find(Predicate<ISessionSummary> match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            lock(m_Lock)
            {
                //we only care about the FIRST match.
                foreach (var sessionSummary in m_List)
                {
                    if (match(sessionSummary))
                        return sessionSummary;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The <see cref="System.Predicate{T}">Predicate</see> delegate that defines the conditions of the elements to search for.</param>
        /// <remarks>
        /// The <see cref="System.Predicate{T}">Predicate</see> is a delegate to a method that returns true if the object passed to it matches the
        /// conditions defined in the delegate. The elements of the current List are individually passed to the <see cref="System.Predicate{T}">Predicate</see> delegate, moving forward in the List, starting with the first element and ending with the last element.
        /// </remarks>
        /// <returns>A List containing all the elements that match the conditions defined by the specified predicate, if found; otherwise, an empty List.</returns>
        /// <exception caption="Argument Null Exception" cref="System.ArgumentNullException">match is a null reference (Nothing in Visual Basic)</exception>
        public ISessionSummaryCollection FindAll(Predicate<ISessionSummary> match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            SessionSummaryCollection resultsCollection = new SessionSummaryCollection(m_Repository);

            lock (m_Lock)
            {
                foreach (var sessionSummary in m_List)
                {
                    if (match(sessionSummary))
                        resultsCollection.Add(sessionSummary);
                }
            }

            return resultsCollection;
        }

        /// <summary>
        /// Removes the first occurrence of a specified object
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(Guid key)
        {
            lock (m_Lock)
            {
                bool foundItem = false;
                if (m_Dictionary.TryGetValue(key, out var victim))
                {
                    foundItem = true;
                    m_Dictionary.Remove(key);
                    m_List.Remove(victim);
                }

                return foundItem;
            }
        }

        /// <summary>
        /// Attempt to get the item with the specified key, returning true if it could be found
        /// </summary>
        /// <returns>True if the item could be found, false otherwise</returns>
        public bool TryGetValue(Guid key, out ISessionSummary item)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1"/>.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="item"/> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.IList`1"/>.
        ///                 </param>
        public int IndexOf(ISessionSummary item)
        {
            lock(m_Lock)
            {
                return m_List.IndexOf(item);
            }
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.
        ///                 </param><param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1"/>.
        ///                 </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.
        ///                 </exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.
        ///                 </exception>
        public void Insert(int index, ISessionSummary item)
        {
            throw new NotSupportedException("Inserting an item into the collection at a specific location is not supported.");
        }

        /// <summary>
        /// Removes the <see cref="T:System.Collections.Generic.IList`1"/> item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.
        ///                 </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.
        ///                 </exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.
        ///                 </exception>
        public void RemoveAt(int index)
        {
            lock(m_Lock)
            {
                //find what the hell item they're talking about and remove that using our other overlaod.
                var item = m_List[index];
                Remove(item);
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set.
        ///                 </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.
        ///                 </exception><exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.Generic.IList`1"/> is read-only.
        ///                 </exception>
        public ISessionSummary this[int index]
        {
            get { return m_List[index]; }
            set { throw new NotSupportedException("Updated items by index is not supported."); }
        }

        /// <summary>
        /// get the item with the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public ISessionSummary this[Guid key]
        {
            get { return m_Dictionary[key]; }
        }

        #endregion
    }
}
