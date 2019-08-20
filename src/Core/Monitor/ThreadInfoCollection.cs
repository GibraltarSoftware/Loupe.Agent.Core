using System;
using System.Collections.Generic;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Monitor
{
    /// <summary>
    /// A (sorted) collection of ThreadInfo objects.
    /// </summary>
    public sealed class ThreadInfoCollection : IList<ThreadInfo>
    {
        private readonly Dictionary<Guid, ThreadInfo> m_ThreadInfoByGuid = new Dictionary<Guid, ThreadInfo>();
        private readonly Dictionary<int, ThreadInfo> m_ThreadInfoByIndex = new Dictionary<int, ThreadInfo>();
        private readonly List<ThreadInfo> m_SortedThreadInfo = new List<ThreadInfo>();
        private readonly object m_Lock = new object();

        private bool m_SortNeeded;
        private int m_MaxThreadIndex;
        private ThreadInfo m_CachedThreadInfo; //this is a tetchy little performance optimization to save us thread info lookup time

        /// <summary>
        /// Create a new empty ThreadInfoCollection.
        /// </summary>
        public ThreadInfoCollection()
        {
            m_SortNeeded = false; // We start empty, so there's nothing to sort.
            m_MaxThreadIndex = 0;
        }

        /// <summary>
        /// Makes sure any new ThreadInfo items added to the collection have been re-sorted.
        /// </summary>
        private void EnsureSorted()
        {
            lock (m_Lock)
            {
                if (m_SortNeeded)
                {
                    m_SortedThreadInfo.Sort();
                    m_SortNeeded = false;
                }
            }
        }

        #region ICollection<ThreadInfo> Members

        /// <summary>
        /// Adds an item to the ThreadInfoCollection.
        /// </summary>
        /// <param name="item">The ThreadInfo item to add.</param>
        public void Add(ThreadInfo item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "A null ThreadInfo can not be added to the collection.");

            lock (m_Lock)
            {
                if (m_ThreadInfoByGuid.ContainsKey(item.Id))
                    throw new InvalidOperationException("The collection already contains the ThreadInfo item being added.");

                int threadIndex = item.ThreadIndex;
                if (m_ThreadInfoByIndex.ContainsKey(threadIndex) && Log.SilentMode == false)
                {
                    // This should never happen, so log an error if it's not an Agent client (which it shouldn't be).
                    // This mostly could happen when analyzing a session with an intermediate internal version which reports
                    // multiple ThreadInfo packets for the same ThreadId but does not include a ThreadIndex property.
                    ThreadInfo existingItem = m_ThreadInfoByIndex[threadIndex];
                    Log.Write(LogMessageSeverity.Warning, "Loupe.Session.ThreadInfo", "ThreadInfo index collision",
                              "Two ThreadInfo objects with different Guid IDs have the same ThreadIndex in the same session.\r\n" +
                              "ThreadIndex = {0}\r\nOld Guid ID = {1}\r\nNew Guid ID = {2}\r\nOld thread: {3}\r\nNew thread: {4}\r\n",
                              threadIndex, existingItem.Id, item.Id, existingItem.Description, item.Description);
                }

                if (threadIndex > m_MaxThreadIndex)
                    m_MaxThreadIndex = threadIndex; // Update the max ThreadIndex we've seen.

                m_ThreadInfoByIndex[threadIndex] = item; // Replace any collision in this (should never collide).
                m_ThreadInfoByGuid.Add(item.Id, item);
                m_SortedThreadInfo.Add(item);
                m_SortNeeded = true; // Mark that we've added a new item which isn't yet sorted.
            }
        }

        /// <summary>
        /// Clear the ThreadInfoCollection.
        /// </summary>
        public void Clear()
        {
            lock (m_Lock)
            {
                m_ThreadInfoByIndex.Clear();
                m_ThreadInfoByGuid.Clear();
                m_SortedThreadInfo.Clear();
                m_MaxThreadIndex = 0; // All gone, so clear this.
                m_SortNeeded = false; // We cleared them all, so there's nothing left to sort.
            }
        }

        /// <summary>
        /// Determines whether a given ThreadInfo item is already present in the ThreadInfoCollection.
        /// </summary>
        /// <param name="item">The ThreadInfo item of interest.</param>
        /// <returns>True if present, false if not.</returns>
        public bool Contains(ThreadInfo item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "A null ThreadInfo can not be queried in the collection.");

            lock (m_Lock)
            {
                return m_ThreadInfoByGuid.ContainsKey(item.Id);
            }
        }

        /// <summary>
        /// Determines whether the ThreadInfoCollection contains a ThreadInfo with a specified Guid ID.
        /// </summary>
        /// <param name="id">The Guid ID of the ThreadInfo of interest.</param>
        /// <returns>True if present, false if not.</returns>
        public bool ContainsKey(Guid id)
        {
            lock (m_Lock)
            {
                return m_ThreadInfoByGuid.ContainsKey(id);
            }
        }

        /// <summary>
        /// Determines whether the ThreadInfoCollection contains a ThreadInfo with a specified ThreadIndex.
        /// </summary>
        /// <param name="threadIndex">The unique ThreadIndex of the ThreadInfo of interest.</param>
        /// <returns>True if present, false if not.</returns>
        public bool ContainsKey(int threadIndex)
        {
            lock (m_Lock)
            {
                return m_ThreadInfoByIndex.ContainsKey(threadIndex);
            }
        }

        /// <summary>
        /// Copy the collected ThreadInfo objects to a target array, in sorted order.
        /// </summary>
        /// <param name="array">The target array (must be large enough to hold the Count of items starting at arrayIndex).</param>
        /// <param name="arrayIndex">The starting index in the target array at which to begin copying.</param>
        public void CopyTo(ThreadInfo[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array), "Can not CopyTo a null array");

            lock (m_Lock)
            {
                EnsureSorted();
                ((ICollection<ThreadInfo>)m_SortedThreadInfo).CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Gets the number of ThreadInfo items in the ThreadInfoCollection.
        /// </summary>
        public int Count
        {
            get
            {
                lock (m_Lock)
                {
                    return m_SortedThreadInfo.Count;
                }
            }
        }

        /// <summary>
        /// Gets the maximum ThreadIndex value ever added to the ThreadInfoCollection.
        /// </summary>
        public int MaxThreadIndex
        {
            get
            {
                lock (m_Lock)
                {
                    return m_MaxThreadIndex;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the ThreadInfoCollection is read-only.
        /// </summary>
        /// <returns>
        /// False because a ThreadInfoCollection is never read-only.
        /// </returns>
        public bool IsReadOnly { get { return false; } }

        /// <summary>
        /// Removes a specified ThreadInfo item from the ThreadInfoCollection.
        /// </summary>
        /// <param name="item">The ThreadInfo item to remove.</param>
        /// <returns>True if item was found and removed from the ThreadInfoCollection, false if not found.</returns>
        public bool Remove(ThreadInfo item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "A null ThreadInfo can not be removed from the collection.");

            lock (m_Lock)
            {
                if (m_ThreadInfoByGuid.ContainsKey(item.Id))
                {
                    m_SortedThreadInfo.Remove(item); // We don't need to re-sort after a removal (unless already needed).
                    m_ThreadInfoByGuid.Remove(item.Id);
                    m_ThreadInfoByIndex.Remove(item.ThreadIndex);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes any ThreadInfo item with the specified Guid ID from the ThreadInfoCollection.
        /// </summary>
        /// <param name="id">The Guid ID of the ThreadInfo to remove.</param>
        /// <returns>True if an item was found and removed from the ThreadInfoCollection, false if not found.</returns>
        public bool RemoveKey(Guid id)
        {
            lock (m_Lock)
            {
                if (m_ThreadInfoByGuid.TryGetValue(id, out var item))
                {
                    m_SortedThreadInfo.Remove(item);
                    m_ThreadInfoByGuid.Remove(id);
                    m_ThreadInfoByIndex.Remove(item.ThreadIndex);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes any ThreadInfo item with the specified ThreadIndex from the ThreadInfoCollection.
        /// </summary>
        /// <param name="threadIndex">The unique ThreadIndex of the ThreadInfo to remove.</param>
        /// <returns>True if an item was found and removed from the ThreadInfoCollection, false if not found.</returns>
        public bool RemoveKey(int threadIndex)
        {
            lock (m_Lock)
            {
                if (m_ThreadInfoByIndex.TryGetValue(threadIndex, out var item))
                {
                    m_SortedThreadInfo.Remove(item);
                    m_ThreadInfoByGuid.Remove(item.Id);
                    m_ThreadInfoByIndex.Remove(threadIndex);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Determines the index of a specific ThreadInfo in the ThreadInfoCollection (in sorted order).
        /// </summary>
        /// <param name="item">The ThreadInfo item to locate in the ThreadInfoCollection.</param>
        /// <returns>
        /// The index of the ThreadInfo item if found in the list; otherwise, -1.
        /// </returns>
        public int IndexOf(ThreadInfo item)
        {
            lock (m_Lock)
            {
                EnsureSorted();
                return m_SortedThreadInfo.IndexOf(item);
            }
        }

        /// <summary>
        /// ThreadInfoCollection is sorted and does not support direct modification.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, ThreadInfo item)
        {
            throw new NotSupportedException("ThreadInfoCollection is sorted and does not support direct modification.");
        }

        /// <summary>
        /// Remove the ThreadInfo item found at a specified index in the ThreadInfoCollection (in sorted order). (Supported but not recommended.)
        /// </summary>
        /// <param name="index">The index (in the sorted order) of a ThreadInfo item to remove.</param>
        public void RemoveAt(int index)
        {
            lock (m_Lock)
            {
                EnsureSorted();
                ThreadInfo victim = m_SortedThreadInfo[index];
                Remove(victim);
            }
        }

        /// <summary>
        /// Gets the element at the specified index. (Setting by index is not supported in ThreadInfoCollection.)
        /// </summary>
        /// <param name="sortIndex">The index (in the sorted order) of a ThreadInfo item to extract.</param>
        /// <returns>The ThreadInfo item at that index in the sorted order of this ThreadInfoCollection.</returns>
        ThreadInfo IList<ThreadInfo>.this[int sortIndex]
        {
            get { return this[sortIndex]; }
            set { throw new NotSupportedException("ThreadInfoCollection is sorted and does not support direct modification."); }
        }

        /// <summary>
        /// Gets a ThreadInfo item at a specified index (in the sorted order). (NOT BY ThreadId or ThreadIndex!
        /// Use TryGetValue to lookup by ThreadIndex or TryFindThreadId to lookup by ThreadId.)
        /// </summary>
        /// <param name="sortIndex">The index (in the sorted order) of a ThreadInfo item to extract.</param>
        /// <returns>The ThreadInfo item at that index in the sorted order of this ThreadInfoCollection.</returns>
        public ThreadInfo this[int sortIndex]
        {
            get
            {
                lock (m_Lock)
                {
                    if (sortIndex < 0 || sortIndex >= m_SortedThreadInfo.Count)
                        throw new ArgumentOutOfRangeException("index", "Selected index is outside the range of the collection");

                    EnsureSorted();
                    return m_SortedThreadInfo[sortIndex];
                }
            }
        }

        /// <summary>
        /// Gets a ThreadInfo item with a specified Guid ID.
        /// </summary>
        /// <param name="id">The Guid ID of the desired ThreadInfo.</param>
        /// <returns>The ThreadInfo item with the specified Guid ID.</returns>
        public ThreadInfo this[Guid id]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_ThreadInfoByGuid[id];
                }
            }
        }

        /// <summary>
        /// Get the ThreadInfo with a specified Guid ID.
        /// </summary>
        /// <param name="id">The Guid ID of the desired ThreadInfo.</param>
        /// <param name="threadInfo">Gets the ThreadInfo with the specified Guid ID if it exists in the ThreadInfoCollection.</param>
        /// <returns>True if found, false if not found.</returns>
        public bool TryGetValue(Guid id, out ThreadInfo threadInfo)
        {
            lock (m_Lock)
            {
                return m_ThreadInfoByGuid.TryGetValue(id, out threadInfo);
            }
        }

        /// <summary>
        /// Get the ThreadInfo with a specified ThreadIndex. (Use TryFindThreadId() to look up by ManagedThreadId.)
        /// </summary>
        /// <param name="threadIndex">The ThreadId of the desired ThreadInfo.</param>
        /// <param name="threadInfo">Gets the ThreadInfo with the specified ThreadIndex if it exists in the ThreadInfoCollection.</param>
        /// <returns>True if found, false if not found.</returns>
        public bool TryGetValue(int threadIndex, out ThreadInfo threadInfo)
        {
            //this method gets *hammered* so we do a cheap one element cache.
            threadInfo = m_CachedThreadInfo; //yep, outside the lock - because we're going to verify it in a second and we don't care what value it had.

            if (!ReferenceEquals(threadInfo, null) && (threadInfo.ThreadIndex == threadIndex)) //if it's actually what they wanted then hooray! no need to go into the lock.
                return true;

            lock (m_Lock)
            {
                var returnVal = m_ThreadInfoByIndex.TryGetValue(threadIndex, out threadInfo);
                m_CachedThreadInfo = threadInfo;
                return returnVal;
            }
        }

        /// <summary>
        /// Get the ThreadInfo with a specified ThreadId.  (Finds earliest match, but ThreadId may not be unique!  Use Guid if possible.)
        /// </summary>
        /// <param name="threadId">The ThreadId of the desired ThreadInfo.</param>
        /// <param name="threadInfo">Gets the ThreadInfo with the specified ThreadId if it exists in the ThreadInfoCollection.</param>
        /// <returns>True if found, false if not found.</returns>
        public bool TryFindThreadId(int threadId, out ThreadInfo threadInfo)
        {
            lock (m_Lock)
            {
                EnsureSorted();
                foreach (ThreadInfo info in m_SortedThreadInfo)
                {
                    if (info.ThreadId == threadId)
                    {
                        threadInfo = info;
                        return true;
                    }
                }

                // Otherwise, it doesn't exist in the collection.
                threadInfo = null;
                return false;
            }
        }

        #endregion

        #region IEnumerable<ThreadInfo> Members

        /// <summary>
        /// Returns an enumerator that iterates through the ThreadInfoCollection (in sorted order).
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<ThreadInfo> GetEnumerator()
        {
            lock (m_Lock)
            {
                EnsureSorted();
                return ((ICollection<ThreadInfo>)m_SortedThreadInfo).GetEnumerator();
            }
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Returns an enumerator that iterates through the ThreadInfoCollection (in sorted order).
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

        /// <summary>
        /// Scan the set of ThreadInfo in the collection and assign ThreadInstance numbers to ThreadName collisions.
        /// </summary>
        public void UniquifyThreadNames()
        {
            lock (m_Lock)
            {
                // First we need to make sure the instance number are all cleared.
                foreach (ThreadInfo currentInstance in this)
                    currentInstance.ThreadInstance = 0;

                int count = m_SortedThreadInfo.Count;
                Dictionary<string, ThreadInfo> currentInstanceByName = new Dictionary<string, ThreadInfo>(count * 2);

                // Now loop over the collection in order of ThreadIndex (usually complete, but could have holes).
                for (int index=1; index <= m_MaxThreadIndex; index++)
                {
                    if (m_ThreadInfoByIndex.TryGetValue(index, out var currentInstance) == false)
                        continue; // Isn't one of that index, continue with next one.

                    string threadName = currentInstance.Caption;
                    if (currentInstanceByName.TryGetValue(threadName, out var previousInstance))
                    {
                        int instanceNumber = previousInstance.ThreadInstance;
                        if (instanceNumber <= 0)
                        {
                            // We have another with the same caption, but the previous was the first, so assign that one....
                            instanceNumber = 1;
                            previousInstance.ThreadInstance = instanceNumber;
                        }
                        instanceNumber++; // Increment to the next instance number for the current instance.
                        currentInstance.ThreadInstance = instanceNumber; // And assign it.
                    }
                    // Otherwise, we're the first by that name, so leave our instance number at 0 for now.
                    currentInstanceByName[threadName] = currentInstance; // Add or replace the current one for that name.
                }
            }
        }
    }
}