using System;
using System.Collections.Generic;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// A (sorted) collection of Application User objects
    /// </summary>
    public sealed class ApplicationUserCollection : IApplicationUserCollection
    {
        private readonly Dictionary<Guid, IApplicationUser> m_ApplicationUserByGuid = new Dictionary<Guid, IApplicationUser>();
        private readonly Dictionary<string, IApplicationUser> m_ApplicationUserByKey = new Dictionary<string, IApplicationUser>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IApplicationUser> m_ApplicationUserByUserName = new Dictionary<string, IApplicationUser>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IApplicationUser> m_SortedApplicationUser = new List<IApplicationUser>();
        private readonly object m_Lock = new object();

        private bool m_SortNeeded;
        private IApplicationUser m_CachedApplicationUser; //this is a tetchy little performance optimization to save us thread info lookup time

        /// <summary>
        /// Create a new empty ApplicationUserCollection.
        /// </summary>
        public ApplicationUserCollection()
        {
            m_SortNeeded = false; // We start empty, so there's nothing to sort.
        }

        /// <summary>
        /// Makes sure any new ApplicationUser items added to the collection have been re-sorted.
        /// </summary>
        private void EnsureSorted()
        {
            lock (m_Lock)
            {
                if (m_SortNeeded)
                {
                    m_SortedApplicationUser.Sort();
                    m_SortNeeded = false;
                }
            }
        }

        #region ICollection<ApplicationUser> Members

        /// <summary>
        /// Adds an item to the ApplicationUserCollection.
        /// </summary>
        /// <param name="item">The ApplicationUser item to add.</param>
        public void Add(IApplicationUser item)
        {
            if (item == null)
                throw new ArgumentNullException("item", "A null ApplicationUser can not be added to the collection.");

            if (string.IsNullOrEmpty(item.FullyQualifiedUserName))
                throw new ArgumentNullException("item", "An ApplicationUser with a null username can not be added to the collection.");

            var applicationUser = item as ApplicationUser;
            if (applicationUser == null)
                throw new InvalidOperationException("Only ApplicationUser concrete instances can be added");

            lock (m_Lock)
            {
                if (m_ApplicationUserByGuid.ContainsKey(applicationUser.Id))
                    throw new InvalidOperationException("The collection already contains the ApplicationUser item being added.");

                if (string.IsNullOrEmpty(applicationUser.Key))
                {
                    if (m_ApplicationUserByUserName.ContainsKey(applicationUser.FullyQualifiedUserName))
                        throw new InvalidOperationException("The collection already contains the ApplicationUser item being added.");

                    m_ApplicationUserByUserName.Add(applicationUser.FullyQualifiedUserName, applicationUser);
                }
                else
                {
                    if (m_ApplicationUserByKey.ContainsKey(applicationUser.Key))
                        throw new InvalidOperationException("The collection already contains the ApplicationUser item being added.");

                    m_ApplicationUserByKey.Add(applicationUser.Key, applicationUser);
                    m_ApplicationUserByUserName[applicationUser.FullyQualifiedUserName] = applicationUser; // we will overwrite whatever's there because we're a better match.
                }

                m_ApplicationUserByGuid.Add(applicationUser.Id, applicationUser);
                m_SortedApplicationUser.Add(applicationUser);
                m_SortNeeded = true; // Mark that we've added a new item which isn't yet sorted.
            }
        }

        void IApplicationUserCollection.Remove(IApplicationUser item)
        {
            throw new NotImplementedException();
        }

        void IApplicationUserCollection.Insert(int index, IApplicationUser item)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clear the ApplicationUserCollection.
        /// </summary>
        public void Clear()
        {
            lock (m_Lock)
            {
                m_ApplicationUserByKey.Clear();
                m_ApplicationUserByUserName.Clear();
                m_ApplicationUserByGuid.Clear();
                m_SortedApplicationUser.Clear();
                m_SortNeeded = false; // We cleared them all, so there's nothing left to sort.
            }
        }

        /// <summary>
        /// Determines whether a given ApplicationUser item is already present in the ApplicationUserCollection.
        /// </summary>
        /// <param name="item">The ApplicationUser item of interest.</param>
        /// <returns>True if present, false if not.</returns>
        public bool Contains(IApplicationUser item)
        {
            if (item == null)
                throw new ArgumentNullException("item", "A null ApplicationUser can not be queried in the collection.");

            var applicationUser = item as ApplicationUser;
            if (applicationUser == null)
                throw new InvalidOperationException("Only ApplicationUser concrete instances can be verified");

            lock (m_Lock)
            {
                return m_ApplicationUserByGuid.ContainsKey(applicationUser.Id);
            }
        }

        /// <summary>
        /// Determines whether the ApplicationUserCollection contains a ApplicationUser with a specified Guid ID.
        /// </summary>
        /// <param name="id">The Guid ID of the ApplicationUser of interest.</param>
        /// <returns>True if present, false if not.</returns>
        public bool ContainsKey(Guid id)
        {
            lock (m_Lock)
            {
                return m_ApplicationUserByGuid.ContainsKey(id);
            }
        }

        /// <summary>
        /// Determines whether the ApplicationUserCollection contains a ApplicationUser with a specified Key.
        /// </summary>
        /// <param name="key">The unique key of the ApplicationUser of interest.</param>
        /// <returns>True if present, false if not.</returns>
        public bool ContainsKey(string key)
        {
            lock (m_Lock)
            {
                return m_ApplicationUserByKey.ContainsKey(key);
            }
        }

        /// <summary>
        /// Determines whether the ApplicationUserCollection contains a ApplicationUser with a specified user name.
        /// </summary>
        /// <param name="userName">The fully qualified user name of the ApplicationUser of interest.</param>
        /// <returns>True if present, false if not.</returns>
        public bool ContainsUserName(string userName)
        {
            lock (m_Lock)
            {
                return m_ApplicationUserByUserName.ContainsKey(userName);
            }
        }

        /// <summary>
        /// Copy the collected ApplicationUser objects to a target array, in sorted order.
        /// </summary>
        /// <param name="array">The target array (must be large enough to hold the Count of items starting at arrayIndex).</param>
        /// <param name="arrayIndex">The starting index in the target array at which to begin copying.</param>
        public void CopyTo(IApplicationUser[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("array", "Can not CopyTo a null array");

            lock (m_Lock)
            {
                EnsureSorted();
                m_SortedApplicationUser.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Gets the number of ApplicationUser items in the ApplicationUserCollection.
        /// </summary>
        public int Count
        {
            get
            {
                lock (m_Lock)
                {
                    return m_SortedApplicationUser.Count;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the ApplicationUserCollection is read-only.
        /// </summary>
        /// <returns>
        /// False because a ApplicationUserCollection is never read-only.
        /// </returns>
        public bool IsReadOnly { get { return false; } }

        /// <summary>
        /// Removes a specified ApplicationUser item from the ApplicationUserCollection.
        /// </summary>
        /// <param name="item">The ApplicationUser item to remove.</param>
        /// <returns>True if item was found and removed from the ApplicationUserCollection, false if not found.</returns>
        public bool Remove(IApplicationUser item)
        {
            if (item == null)
                throw new ArgumentNullException("item", "A null ApplicationUser can not be removed from the collection.");

            var applicationUser = item as ApplicationUser;
            if (applicationUser == null)
                throw new InvalidOperationException("Only ApplicationUser concrete instances can be removed");

            lock (m_Lock)
            {
                if (m_ApplicationUserByGuid.ContainsKey(applicationUser.Id))
                {
                    m_SortedApplicationUser.Remove(applicationUser); // We don't need to re-sort after a removal (unless already needed).
                    m_ApplicationUserByGuid.Remove(applicationUser.Id);
                    m_ApplicationUserByUserName.Remove(applicationUser.FullyQualifiedUserName);

                    if (string.IsNullOrEmpty(applicationUser.Key) == false)
                        m_ApplicationUserByKey.Remove(applicationUser.Key);

                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes any ApplicationUser item with the specified Guid ID from the ApplicationUserCollection.
        /// </summary>
        /// <param name="id">The Guid ID of the ApplicationUser to remove.</param>
        /// <returns>True if an item was found and removed from the ApplicationUserCollection, false if not found.</returns>
        public bool Remove(Guid id)
        {
            lock (m_Lock)
            {
                if (m_ApplicationUserByGuid.TryGetValue(id, out var item))
                {
                    m_SortedApplicationUser.Remove(item);
                    m_ApplicationUserByGuid.Remove(id);
                    m_ApplicationUserByUserName.Remove(item.FullyQualifiedUserName);

                    if (string.IsNullOrEmpty(item.Key) == false)
                        m_ApplicationUserByKey.Remove(item.Key);

                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Determines the index of a specific ApplicationUser in the ApplicationUserCollection (in sorted order).
        /// </summary>
        /// <param name="item">The ApplicationUser item to locate in the ApplicationUserCollection.</param>
        /// <returns>
        /// The index of the ApplicationUser item if found in the list; otherwise, -1.
        /// </returns>
        public int IndexOf(ApplicationUser item)
        {
            lock (m_Lock)
            {
                EnsureSorted();
                return m_SortedApplicationUser.IndexOf(item);
            }
        }

        /// <inheritdoc />
        public int IndexOf(IApplicationUser item)
        {
            throw new NotImplementedException();
        }

        void IList<IApplicationUser>.Insert(int index, IApplicationUser item)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove the ApplicationUser item found at a specified index in the ApplicationUserCollection (in sorted order). (Supported but not recommended.)
        /// </summary>
        /// <param name="index">The index (in the sorted order) of a ApplicationUser item to remove.</param>
        public void RemoveAt(int index)
        {
            lock (m_Lock)
            {
                EnsureSorted();
                var victim = m_SortedApplicationUser[index];
                Remove(victim);
            }
        }

        /// <summary>
        /// Gets the element at the specified index. (Setting by index is not supported in ApplicationUserCollection.)
        /// </summary>
        /// <param name="sortIndex">The index (in the sorted order) of a ApplicationUser item to extract.</param>
        /// <returns>The ApplicationUser item at that index in the sorted order of this ApplicationUserCollection.</returns>
        IApplicationUser IList<IApplicationUser>.this[int sortIndex]
        {
            get { return this[sortIndex]; }
            set { throw new NotSupportedException("ApplicationUserCollection is sorted and does not support direct modification."); }
        }

        /// <summary>
        /// Gets a ApplicationUser item at a specified index (in the sorted order). (NOT BY ThreadId or ThreadIndex!
        /// Use TryGetValue to lookup by ThreadIndex or TryFindThreadId to lookup by ThreadId.)
        /// </summary>
        /// <param name="sortIndex">The index (in the sorted order) of a ApplicationUser item to extract.</param>
        /// <returns>The ApplicationUser item at that index in the sorted order of this ApplicationUserCollection.</returns>
        public IApplicationUser this[int sortIndex]
        {
            get
            {
                lock (m_Lock)
                {
                    if (sortIndex < 0 || sortIndex >= m_SortedApplicationUser.Count)
                        throw new ArgumentOutOfRangeException("index", "Selected index is outside the range of the collection");

                    EnsureSorted();
                    return m_SortedApplicationUser[sortIndex];
                }
            }
        }

        /// <summary>
        /// Gets a ApplicationUser item with a specified Guid ID.
        /// </summary>
        /// <param name="id">The Guid ID of the desired ApplicationUser.</param>
        /// <returns>The ApplicationUser item with the specified Guid ID.</returns>
        public IApplicationUser this[Guid id]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_ApplicationUserByGuid[id];
                }
            }
        }

        /// <summary>
        /// Get the ApplicationUser with a specified Guid ID.
        /// </summary>
        /// <param name="id">The Guid ID of the desired ApplicationUser.</param>
        /// <param name="applicationUser">Gets the ApplicationUser with the specified Guid ID if it exists in the ApplicationUserCollection.</param>
        /// <returns>True if found, false if not found.</returns>
        public bool TryGetValue(Guid id, out IApplicationUser applicationUser)
        {
            lock (m_Lock)
            {
                return m_ApplicationUserByGuid.TryGetValue(id, out applicationUser);
            }
        }

        /// <summary>
        /// Get the ApplicationUser with a specified Guid ID.
        /// </summary>
        /// <param name="id">The Guid ID of the desired ApplicationUser.</param>
        /// <param name="applicationUser">Gets the ApplicationUser with the specified Guid ID if it exists in the ApplicationUserCollection.</param>
        /// <returns>True if found, false if not found.</returns>
        public bool TryGetValue(Guid id, out ApplicationUser applicationUser)
        {
            var found = TryGetValue(id, out IApplicationUser appUser);
            if (found)
            {
                applicationUser = appUser as ApplicationUser;
            }
            else
            {
                applicationUser = null;
            }

            return (applicationUser != null);
        }

        /// <summary>
        /// Get the ApplicationUser with a specified Key. (Use TryFindUserName() to look up by fully qualified user name.)
        /// </summary>
        /// <param name="key">The unique key of the desired ApplicationUser.</param>
        /// <param name="applicationUser">Gets the ApplicationUser with the specified key if it exists in the ApplicationUserCollection.</param>
        /// <returns>True if found, false if not found.</returns>
        public bool TryGetValue(string key, out IApplicationUser applicationUser)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException("key");

            //this method gets *hammered* so we do a cheap one element cache.
            applicationUser = m_CachedApplicationUser; //yep, outside the lock - because we're going to verify it in a second and we don't care what value it had.

            if (!ReferenceEquals(applicationUser, null) && (string.Equals(key, applicationUser.Key))) //if it's actually what they wanted then hooray! no need to go into the lock.
                return true;

            lock (m_Lock)
            {
                var returnVal = m_ApplicationUserByKey.TryGetValue(key, out applicationUser);
                m_CachedApplicationUser = applicationUser;
                return returnVal;
            }
        }

        /// <summary>
        /// Get the ApplicationUser with a specified Key. (Use TryFindUserName() to look up by fully qualified user name.)
        /// </summary>
        /// <param name="key">The unique key of the desired ApplicationUser.</param>
        /// <param name="applicationUser">Gets the ApplicationUser with the specified key if it exists in the ApplicationUserCollection.</param>
        /// <returns>True if found, false if not found.</returns>
        public bool TryGetValue(string key, out ApplicationUser applicationUser)
        {
            var found = TryGetValue(key, out IApplicationUser appUser);
            if (found)
            {
                applicationUser = appUser as ApplicationUser;
            }
            else
            {
                applicationUser = null;
            }

            return (applicationUser != null);
        }

        /// <summary>
        /// Get the ApplicationUser with a specified fully qualified user name.
        /// </summary>
        /// <param name="userName">The fully qualified user name of the desired ApplicationUser.</param>
        /// <param name="applicationUser">Gets the ApplicationUser with the specified user name if it exists in the ApplicationUserCollection.</param>
        /// <returns>True if found, false if not found.</returns>
        public bool TryFindUserName(string userName, out ApplicationUser applicationUser)
        {
            if (string.IsNullOrEmpty(userName))
                throw new ArgumentNullException("userName");

            IApplicationUser appUser = null;
            lock (m_Lock)
            {
                m_ApplicationUserByUserName.TryGetValue(userName, out appUser);
            }

            applicationUser = appUser as ApplicationUser;
            return (applicationUser != null);
        }

        /// <summary>
        /// Set the specified value as a cached user if that user isn't present, returning the correct user from the collection
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        internal ApplicationUser TrySetValue(ApplicationUser user)
        {
            if (ReferenceEquals(user, null))
                throw new ArgumentNullException("user");

            if (string.IsNullOrEmpty(user.FullyQualifiedUserName))
                throw new InvalidOperationException("The provided user has no fully qualified user name");

            lock (m_Lock)
            {
                IApplicationUser existingUser;
                if (string.IsNullOrEmpty(user.Key) == false)
                {
                    //see if it exists already by key; if so we return that.
                    if (m_ApplicationUserByKey.TryGetValue(user.Key, out existingUser))
                        return existingUser as ApplicationUser;
                }

                //see if it exists already by user name; if so we return that.
                if (m_ApplicationUserByUserName.TryGetValue(user.FullyQualifiedUserName, out existingUser))
                    return existingUser as ApplicationUser;

                //If we got this far then it's not in our collection..
                Add(user);
                return user;
            }
        }

        #endregion

        #region IEnumerable<ApplicationUser> Members

        /// <summary>
        /// Returns an enumerator that iterates through the ApplicationUserCollection (in sorted order).
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<IApplicationUser> GetEnumerator()
        {
            lock (m_Lock)
            {
                EnsureSorted();
                return ((ICollection<IApplicationUser>)m_SortedApplicationUser).GetEnumerator();
            }
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Returns an enumerator that iterates through the ApplicationUserCollection (in sorted order).
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }

}
