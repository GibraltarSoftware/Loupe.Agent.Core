using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Gibraltar
{
    /// <summary>
    /// A collection of file references that can be persisted.
    /// </summary>
    /// <remarks>
    /// The file cache provides a lightweight ability to store links to a number of files
    /// in an XML index file.</remarks>
    public class FileReferenceCollection : IList<FileReference>, IDisposable
    {
        private const int WaitShortInterval = 1000;
        private const int WaitLongInterval = 15000;

        private readonly ILogger m_Logger;
        private readonly Dictionary<string, FileReference> m_FilesDictionary = new Dictionary<string, FileReference>(StringComparer.OrdinalIgnoreCase);
        private readonly List<FileReference> m_FilesList = new List<FileReference>();
        private readonly string m_Path;
        private readonly string m_Filter;
        private readonly DirectoryInfo m_MonitoredDirectory;
        private readonly object m_DirectoryMonitorThreadLock = new object();

        private Thread m_DirectoryMonitorThread;
        private DateTime m_CurrentPollStartDt;
        private DateTime m_LastPollStartDt;

        private bool m_Disposed;


        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event CollectionChangeEventHandler CollectionChanged;

        /// <summary>
        /// Create a new directory monitor for the provided file reference collection on the provided path.
        /// </summary>
        /// <param name="path">The file path to monitor.</param>
        /// <param name="filter">A matching filter to files to look for in the specified path</param>
        public FileReferenceCollection(string path, string filter)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            m_Path = path.Trim();

            if (Directory.Exists(m_Path) == false)
                throw new DirectoryNotFoundException(string.Format("The path '{0}' does not exist and therefore can't be monitored.", path));

            m_Filter = filter;

            m_Logger = ApplicationLogging.CreateLogger<FileReferenceCollection>();

            //but now go off and find everything that already exists.
            m_MonitoredDirectory = new DirectoryInfo(m_Path);
            FileInfo[] reportDefinitionFiles = string.IsNullOrEmpty(m_Filter) ? m_MonitoredDirectory.GetFiles() : m_MonitoredDirectory.GetFiles(m_Filter);

            foreach (FileInfo definitionFile in reportDefinitionFiles)
            {
                FileReference newReference = new FileReference(definitionFile);
                Add(newReference);
            }

            //and fire up our background thread to monitor stuff....
            CreateDirectoryMonitorThread();
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            //we use the disposed flag to have our background thread stop checking.
            m_Disposed = true;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<FileReference> GetEnumerator()
        {
            return m_FilesList.GetEnumerator();
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
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">
        /// The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
        /// </exception>
        public void Add(FileReference item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            OnItemAdd(item, -1);
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">
        /// The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. 
        /// </exception>
        public void Clear()
        {
            //Only do this if we HAVE something, since events are fired.
            if (m_FilesList.Count > 0)
            {
                OnItemClear();
            }
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </param>
        public bool Contains(FileReference item)
        {
            return m_FilesDictionary.ContainsKey(item.FileNamePath);
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            //gateway to our inner dictionary 
            return m_FilesDictionary.ContainsKey(key);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array" /> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex" /> is less than 0.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="array" /> is multidimensional.
        ///                     -or-
        /// <paramref name="arrayIndex" /> is equal to or greater than the length of <paramref name="array" />.
        ///                     -or-
        /// The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1" /> is greater than the available space from <paramref name="arrayIndex" /> to the end of the destination <paramref name="array" />.
        ///                     -or-
        /// Type cannot be cast automatically to the type of the destination <paramref name="array" />.</exception>
        public void CopyTo(FileReference[] array, int arrayIndex)
        {
            m_FilesList.CopyTo(array, arrayIndex);
        }

        /// <summary>Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        /// <returns>
        /// true if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public bool Remove(FileReference item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "An item must be provided to remove it from the collection.");

            bool itemRemoved = OnItemRemove(item);

            return itemRemoved;
        }

        /// <summary>
        /// Removes the item with the specified key.
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <returns>true if <paramref name="key" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="key" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.</returns>
        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "A key must be provided to remove an item from the collection.");

            //otherwise, see if we can find the victim...
            bool itemRemoved;
            FileReference victim;
            if (TryGetValue(key, out victim))
            {
                itemRemoved = Remove(victim);
            }
            else
            {
                itemRemoved = false;
            }

            return itemRemoved;
        }


        /// <summary>
        /// Change the files name to the new unique name
        /// </summary>
        /// <param name="originalFileNamePath">The fully qualified file name and path to rename</param>
        /// <param name="newFileNamePath">The new fully qualified file name and path</param>
        /// <returns>True if a change was made to the collection, false otherwise.</returns>
        /// <remarks>If no item is found with the original file name or new file name an exception will be thrown.
        /// If an item is found with the new file name and the original file name an exception will be thrown.
        /// If no item is found with the original file name but one is found with the new file name, the operation
        /// will be considered successful but return false (indicating no change).</remarks>
        public bool Rename(string originalFileNamePath, string newFileNamePath)
        {
            if (string.IsNullOrEmpty(originalFileNamePath))
                throw new ArgumentNullException(nameof(originalFileNamePath), "An original file name and path must be provided to rename an item in the collection.");

            if (string.IsNullOrEmpty(newFileNamePath))
                throw new ArgumentNullException(nameof(newFileNamePath), "An new file name and path must be provided to rename an item in the collection.");

            //see what condition we fall into
            FileReference original;
            FileReference destination;

            TryGetValue(originalFileNamePath, out original);
            TryGetValue(newFileNamePath, out destination);
            
            if ((original == null ) && (destination == null))
            {
                throw new ArgumentOutOfRangeException(nameof(originalFileNamePath), "No item could be found in the collection by either the original or new name, indicating the file doesn't exist.");
            }

            if ((original != null ) && (destination != null))
            {
                throw new ArgumentOutOfRangeException(nameof(newFileNamePath), "There is currently an item with the provided new file name in the collection, so the original can't be renamed.");
            }

            //now we only do the rename if we found an original, otherwise we just return false.
            bool itemRenamed = false;

            if (original != null)
            {
                //we are going to process this as a remove/add because those are the events we generate.
                Remove(original);
                original.FileNamePath = newFileNamePath;
                original.Caption = Path.GetFileName(newFileNamePath);
                Add(original);

                itemRenamed = true;
            }

            return itemRenamed;
        }


        /// <summary> Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />. </summary>
        /// <returns>The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.</returns>
        public int Count
        {
            get { return m_FilesList.Count; } 
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
        public int IndexOf(FileReference item)
        {
            return m_FilesList.IndexOf(item);
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1" /> at the specified index.
        /// </summary>
        /// <param name="index">
        /// The zero-based index at which <paramref name="item" /> should be inserted.
        /// </param>
        /// <param name="item">
        /// The object to insert into the <see cref="T:System.Collections.Generic.IList`1" />.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The <see cref="T:System.Collections.Generic.IList`1" /> is read-only.
        /// </exception>
        public void Insert(int index, FileReference item)
        {
            if ((index < 0) || (index > m_FilesList.Count))
                throw new IndexOutOfRangeException(string.Format("The provided index {0} is beyond the ends of the collection.", index));

            if (item == null)
                throw new ArgumentNullException(nameof(item));

            //we do support inserting by index, provided it isn't a dupe.
            if (ContainsKey(item.FileNamePath))
                throw new ArgumentException("There is already an item with the key '{0}' in the collection", item.FileNamePath);

            OnItemAdd(item, index);
        }

        /// <summary>
        /// Removes the <see cref="T:System.Collections.Generic.IList`1" /> item at the specified index.
        /// </summary>
        /// <param name="index">
        /// The zero-based index of the item to remove.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The <see cref="T:System.Collections.Generic.IList`1" /> is read-only.
        /// </exception>
        public void RemoveAt(int index)
        {
            //find the item at the requested location
            FileReference victim = m_FilesList[index];

            //and pass it to our normal remove method
            Remove(victim);
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">
        /// The zero-based index of the element to get or set.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The property is set and the <see cref="T:System.Collections.Generic.IList`1" /> is read-only.
        /// </exception>
        public FileReference this[int index] 
        {
            get { return m_FilesList[index]; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if ((index < 0) || (index >= m_FilesList.Count))
                    throw new IndexOutOfRangeException(string.Format("The provided index {0} is beyond the ends of the collection.", index));

                //to set an item at an index, we're going to remove the item at that location
                //and then make sure the new key is unique.
                FileReference replacedValue = m_FilesList[index];

                if (replacedValue.FileNamePath.Equals(value.FileNamePath, StringComparison.OrdinalIgnoreCase))
                {
                    //if the item at the index has the same key we don't have to do a duplicate test, we're 
                    //doing an in place replacement.
                }
                else
                {
                    //make sure that there isn't an item in the collection with the key.
                    if (ContainsKey(value.FileNamePath))
                        throw new ArgumentException(string.Format("There is already a file reference with the full file name '{0}'", value.FileNamePath));
                }

                OnItemSet(index, value);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out FileReference value)
        {
            //gateway to our inner dictionary try get value
            return m_FilesDictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Called to clear all of the item found flags on existing file references prior to checking for updates.
        /// </summary>
        internal void ClearItemFoundFlags()
        {
            foreach (FileReference reference in m_FilesList)
            {
                reference.ItemFound = false;
            }
        }

        /// <summary>
        /// Remove all items in the collection that weren't found.
        /// </summary>
        internal void RemoveItemsNotFound()
        {
            List<FileReference> filesNotFound = new List<FileReference>();

            //find every item that doesn't have the item found flag set.
            foreach (FileReference reference in m_FilesList)
            {
                if (reference.ItemFound == false)
                {
                    filesNotFound.Add(reference);  
                }
            }

            //now remove all of the items that weren't found.
            foreach (FileReference reference in filesNotFound)
            {
                Remove(reference);
            }
        }

        /// <summary>
        /// Called to raise the CollectionChanged event
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnCollectionChanged(CollectionChangeEventArgs args)
        {
            CollectionChangeEventHandler tempEvent = CollectionChanged;
            if (tempEvent != null)
            {
                tempEvent(this, args);
            }
        }

        /// <summary>
        /// Add an item to the collection, optionally specifying the index
        /// </summary>
        /// <param name="item"></param>
        /// <param name="index">-1 to add to the end of the collection, index in the collection to insert at that location.</param>
        /// <remarks>Raises the CollectionChanged event.</remarks>
        protected virtual void OnItemAdd(FileReference item, int index)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "A new item must be provided to add it to the collection.");

            //add it to both lookup collections
            m_FilesDictionary.Add(item.FileNamePath, item);

            if (index < 0)
            {
                m_FilesList.Add(item);
            }
            else
            {
                m_FilesList.Insert(index, item);
            }

            //and fire the change event
            OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Add, item));
        }

        /// <summary>
        /// Clear all of the items in the collection
        /// </summary>
        /// <remarks>Raises the CollectionChanged event.</remarks>
        protected virtual void OnItemClear()
        {
            m_FilesList.Clear();
            m_FilesDictionary.Clear();

            //and raise the event so our caller knows we're cleared
            OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Refresh, null));
        }

        /// <summary>
        /// Called whenever an item has to be removed to the collection
        /// </summary>
        /// <param name="item"></param>
        /// <returns>True if an item was removed, false if none was found.</returns>
        /// <remarks>Raises the CollectionChanged event.</remarks>
        protected virtual bool OnItemRemove(FileReference item)
        {
            bool itemRemoved = false;

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A new item must be provided to remove it from the collection.");
            }

            if (m_FilesDictionary.ContainsKey(item.FileNamePath))
            {
                m_FilesDictionary.Remove(item.FileNamePath);
                itemRemoved = true; // we did remove something
            }

            //here we are relying on the IComparable implementation being a unique key and being fast.
            if (m_FilesList.Contains(item))
            {
                m_FilesList.Remove(item);
                itemRemoved = true; // we did remove something
            }

            //and fire our event if there was really something to remove
            if (itemRemoved)
                OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Remove, item));

            return itemRemoved;
        }

        /// <summary>
        /// Called to set an item to a specific location in the index, removing whatever is there
        /// </summary>
        /// <param name="index">The index of an existing item in the collection to set</param>
        /// <param name="item">The item to place at that location</param>
        /// <remarks>Raises an event for each collection change (remove and add) that may happen.</remarks>
        protected virtual void OnItemSet(int index, FileReference item)
        {
            //remove the item that's currently in the specified location...
            RemoveAt(index); //that will raise the right remove event...

            //now we know we're good so we can manipulate the collections.
            m_FilesDictionary.Add(item.FileNamePath, item);
            m_FilesList[index] = item;

            //and raise the update event.
            OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Add, item));
        }

        /// <summary>
        /// Called when it has been detected that the directory has been updated to determine what the update is.
        /// </summary>
        /// <returns>True if a real change was detected, false otherwise</returns>
        protected virtual bool OnDirectoryChange()
        {
            bool foundChanges = false;

            if (m_MonitoredDirectory.Exists)
            {
                m_CurrentPollStartDt = DateTime.Now;

                //get the set of file information so we can check for changes
                FileInfo[] reportDefinitionFiles = string.IsNullOrEmpty(m_Filter) ? m_MonitoredDirectory.GetFiles() : m_MonitoredDirectory.GetFiles(m_Filter);

                //mark all of the items in the collection so we know if we miss any.
                ClearItemFoundFlags();

                foreach (FileInfo definitionFile in reportDefinitionFiles)
                {
                    //do we have it already or not?
                    FileReference reference;
                    if (TryGetValue(definitionFile.FullName, out reference))
                    {
                        //see if this file has been touched since our last reference time.
                        if (definitionFile.LastWriteTime > reference.LastWriteTime)
                        {
                            //it has been updated
                            foundChanges = true;
                            reference.Refresh();
                        }
                    }
                    else
                    {
                        //we need to add this one.
                        foundChanges = true;
                        reference = new FileReference(definitionFile);
                        Add(reference);
                    }

                    //mark that we found it so we don't kill it in a minute..
                    reference.ItemFound = true;
                }

                //now get rid of all the ones that have gone away..
                RemoveItemsNotFound();

                //since we're done we can now set our last poll time to be our current poll time
                m_LastPollStartDt = m_CurrentPollStartDt;
            }

            return foundChanges;
        }

        private void CreateDirectoryMonitorThread()
        {
            lock (m_DirectoryMonitorThreadLock)
            {
                m_DirectoryMonitorThread = new Thread(DirectoryMonitorMain);
                m_DirectoryMonitorThread.Name = "Gibraltar Directory Monitor"; //name our thread so we can isolate it out of metrics and such
                m_DirectoryMonitorThread.IsBackground = true;
                m_DirectoryMonitorThread.Start();

                System.Threading.Monitor.PulseAll(m_DirectoryMonitorThreadLock);
            }
        }

        private bool CheckForChanges()
        {
            bool foundChanges;

            //refresh the directory information
            m_MonitoredDirectory.Refresh();

            //see if the directory has been updated since the last time we polled.
            if (m_MonitoredDirectory.LastWriteTime > m_LastPollStartDt)
            {
                foundChanges = OnDirectoryChange();
            }
            else
            {
                foundChanges = false;
            }

            return foundChanges;
        }

        private void DirectoryMonitorMain()
        {
            try
            {
                m_Logger.LogInformation("Background Directory Monitor Started\r\nThe background monitor thread for directory '{0}' is starting.", m_Path);

                while (m_Disposed == false)
                {
                    //check for changes
                    bool foundChanges = CheckForChanges();

                    //now we need to wait for the timer to expire.
                    if (foundChanges)
                    {
                        //sleep for our short interval
                        Thread.Sleep(WaitShortInterval);
                    }
                    else
                    {
                        Thread.Sleep(WaitLongInterval);
                    }
                }

                m_Logger.LogInformation("Background Directory Monitor Stopped\r\nThe background monitor thread for directory '{0}' is ending normally due to the collection being disposed.", m_Path);
            }
            catch (Exception ex)
            {
                //Write a message to the trace log.  Note that we pass the exception twice:  The first is for a shorter message and the second is so Gibraltar can log the
                //full rich exception information
                m_Logger.LogInformation(ex, "Error Processing Async File System Monitor Request\r\nWhile processing a file system monitor request, an exception was thrown: {0}", ex.Message);
            }
        }
    }
}
