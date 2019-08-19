
using System;
using System.Diagnostics;
using System.Collections.Generic;
using Gibraltar.Monitor;
using Gibraltar.Monitor.Serialization;
using Loupe.Agent.PerformanceCounters.Serialization;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Loupe.Agent.PerformanceCounters
{
    /// <summary>
    /// A collection of performance counters that can be polled at the same time.
    /// </summary>
    /// <remarks>If a performance counter is removed from the registered set of performance counters, it will automatically be removed from
    /// any performance counter group it is a member of.</remarks>
    public sealed class PerfCounterCollection : IList<PerfCounterMetric>, IDisplayable
    {
        private string m_Caption;
        private string m_Description;
        private readonly SortedDictionary<string, PerfCounterMetric> m_CounterMetricDictionary;
        private readonly SortedList<PerfCounterMetric, PerfCounterMetric> m_CounterMetricList;
        private readonly object m_Lock = new object();

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs<PerfCounterCollection, PerfCounterMetric>> CollectionChanged;

        /// <summary>
        /// Create a new performance counter group referencing the supplied collection of performance counter objects
        /// </summary>
        /// <param name="caption">A display caption for this performance counter group (useful for administrative purposes)</param>
        /// <param name="description">A description of this performance counter group (useful for administrative purposes)</param>
        public PerfCounterCollection(string caption, string description)
        {
            //create our collections
            m_CounterMetricDictionary = new SortedDictionary<string, PerfCounterMetric>();
            m_CounterMetricList = new SortedList<PerfCounterMetric, PerfCounterMetric>();

            //store off our input arguments
            m_Caption = caption;
            m_Description = description;
        }

        #region Protected Properties and Methods

        private void OnCollectionChanged(CollectionChangedEventArgs<PerfCounterCollection, PerfCounterMetric> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<PerfCounterCollection, PerfCounterMetric>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// Add an existing performance counter object to our collection.
        /// </summary>
        /// <param name="newPerformanceCounter">The new performance counter object to add.</param>
        public void Add(PerformanceCounter newPerformanceCounter)
        {

            if (newPerformanceCounter == null)
            {
                throw new ArgumentNullException(nameof(newPerformanceCounter), "A performance counter object must be provided to add it to the collection.");
            }

            //make sure we don't already have it
            string key = PerfCounterMetric.GetKey(newPerformanceCounter);

            //we're about to modify the collection, get a lock.  We don't want the lock to cover the changed event since
            //we really don't know how long that will take, and it could be deadlock prone.
            lock (m_Lock)
            {
                if(m_CounterMetricDictionary.ContainsKey(key))
                {
                    throw new ArgumentException("The specified performance counter is already in the collection.", nameof(newPerformanceCounter));
                }

                //OK, now go and get the metric we need for this.
                PerfCounterMetric newMetric = PerfCounterMetric.AddOrGet(newPerformanceCounter);

                //and use our normal add
                Add(newMetric);
            }
        }

        /// <summary>
        /// Add a performance counter object to our collection. If it doesn't exist, it will be created.
        /// </summary>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <returns>The windows performance counter object that was created.</returns>
        public void Add(string categoryName, string counterName)
        {
            Add(categoryName, counterName, string.Empty);
        }

        /// <summary>
        /// Add a performance counter object to our collection. If it doesn't exist, it will be created.
        /// </summary>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="instanceName">The name of the performance counter category instance, or an empty string (""), if the category contains a single instance.</param>
        public void Add(string categoryName, string counterName, string instanceName)
        {
            //we can't have a null category our counter.
            if (string.IsNullOrEmpty(categoryName))
            {
                throw new ArgumentNullException(nameof(categoryName));
            }

            if (string.IsNullOrEmpty(counterName))
            {
                throw new ArgumentNullException(nameof(counterName));
            }

            //see if the performance counter metric already exists.  It quite probably does.
            PerfCounterMetric newMetric = PerfCounterMetric.AddOrGet(categoryName, counterName, instanceName);

            //now we can use our normal routine.
            Add(newMetric);
        }

        /// <summary>
        /// Add a performance counter object to our collection. If it doesn't exist, it will be created.
        /// </summary>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="alias">An alias to use to determine the instance name instead of the instance of the supplied counter.</param>
        public void Add(string categoryName, string counterName, PerfCounterInstanceAlias alias)
        {
            //we can't have a null category our counter.
            if (string.IsNullOrEmpty(categoryName))
            {
                throw new ArgumentNullException(nameof(categoryName));
            }

            if (string.IsNullOrEmpty(counterName))
            {
                throw new ArgumentNullException(nameof(counterName));
            }

            //see if the performance counter metric already exists.  It quite probably does.
            PerfCounterMetric newMetric = PerfCounterMetric.AddOrGet(categoryName, counterName, alias);

            //now we can use our normal routine.
            Add(newMetric);
        }

        /// <summary>
        /// The display caption for this performance counter group
        /// </summary>
        public string Caption
        {
            get => m_Caption;
            set => m_Caption = value?.Trim();
        }

        /// <summary>
        /// The display description for this performance counter group
        /// </summary>
        public string Description
        {
            get => m_Description;
            set => m_Description = value?.Trim();
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string categoryName, string counterName)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary 
                return m_CounterMetricDictionary.ContainsKey(PerfCounterMetric.GetKey(categoryName, counterName, string.Empty));
            }
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="instanceName">The name of the performance counter category instance, or an empty string (""), if the category contains a single instance.</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string categoryName, string counterName, string instanceName)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary 
                return m_CounterMetricDictionary.ContainsKey(PerfCounterMetric.GetKey(categoryName, counterName, instanceName));
            }
        }

        /// <summary>
        /// Record a sample of every performance counter in this group
        /// </summary>
        /// <remarks>This method is thread safe, however it may take some time to run (performance counter performance is highly variable) so care should be taken
        /// not to call this in a performance critical code section.</remarks>
        public void WriteSamples()
        {
            //cause every performance counter to log itself.

            //we are going to do this in two passes:  One to get all of the samples, the second to write them to the log file.  This 
            //will let us write them as a single call and minimize locking.
            List<MetricSample> samples = new List<MetricSample>(Count);

            //For multi-threaded cleanliness, we're going to get an array of our members to iterate, guaranteeing that our 
            //values are constant while we do what could take a minute.

            //we need to hold the collection constant for just an instant while we copy out its members into a buffer.
            PerfCounterMetric[] curMetrics;
            lock (m_Lock)
            {
                curMetrics = new PerfCounterMetric[Count];
                CopyTo(curMetrics, 0);
            }

            foreach (var curMetric in curMetrics)
            {
                //sample this counter
                try
                {
                    var curCounter = curMetric.GetPerformanceCounter();

                    //when we create the sample packet, it queries the underlying performance counter value.
                    var curSample = new PerfCounterMetricSample(curMetric, new PerfCounterMetricSamplePacket(curCounter, curMetric));

                    //and add it to our collection
                    samples.Add(curSample);

                    //and we are clearly successful with this metric, so make sure it's set that way
                    curMetric.PollingState = PerfCounterPollingState.Active;
                }
                // ReSharper disable RedundantCatchClause
                catch (Exception exception)
                {
#if DEBUG
                    GC.KeepAlive(exception); //some warning prevention...

                    //rethrow the exception - let our bad boy callers know we done bad
                    throw;
#else
                    //before we log anything, we want to do stateful logging - only log information the first time we have a failure.
                    if ((curMetric != null) && (curMetric.PollingState != PerfCounterPollingState.Error))
                    {
                        //mark the counter in error so we don't report subsequent problems.
                        curMetric.PollingState = PerfCounterPollingState.Error;

                        if (!Log.SilentMode) Log.Write(LogMessageSeverity.Information, LogWriteMode.Queued, exception, "Gibraltar.Agent", "Performance counter error", "The performance counter {0} threw an exception ({1}) while being polled, and will be excluded from metrics until it can be successfully polled again.\r\nException message: {2}",
                            curMetric.Name, exception.GetType().Name, exception.Message);
                    }
#endif
                }
                // ReSharper restore RedundantCatchClause
            }

            //now that we've sampled everything, write out those samples to the log
            Log.Write(samples);
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string categoryName, string counterName, out PerfCounterMetric value)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_CounterMetricDictionary.TryGetValue(PerfCounterMetric.GetKey(categoryName, counterName, string.Empty), out value);
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="instanceName">The name of the performance counter category instance, or an empty string (""), if the category contains a single instance.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string categoryName, string counterName, string instanceName, out PerfCounterMetric value)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary try get value
                return m_CounterMetricDictionary.TryGetValue(PerfCounterMetric.GetKey(categoryName, counterName, instanceName), out value);
            }
        }

        #endregion

        #region IEnumerable<PerfCounterMetric> Members

        IEnumerator<PerfCounterMetric> IEnumerable<PerfCounterMetric>.GetEnumerator()
        {
            return m_CounterMetricDictionary.Values.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_CounterMetricDictionary.GetEnumerator();
        }

        #endregion

        #region IList<PerfCounterMetric> Members


        /// <summary>
        /// Searches for the specified value and returns the zero-based index of the first occurrence
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(PerfCounterMetric item)
        {
            lock (m_Lock)
            {
                return m_CounterMetricList.IndexOfValue(item);
            }
        }

        /// <summary>
        /// Not Supported.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, PerfCounterMetric item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <remarks>
        /// The elements that follow the removed element are moved up to occupy the vacated spot.  The indexes of the elements that are moved are also updated.
        /// </remarks>
        /// <param name="index">The zero-based index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            PerfCounterMetric victim;
            lock (m_Lock)
            {
                //find the item at the requested location
                victim = m_CounterMetricList.Values[index];
            }

            //and pass that to our normal remove method.  Must be called outside the lock because it fires an event.
            Remove(victim);
        }

        /// <summary>
        /// Gets the item in the collection at the specified zero-based index.
        /// </summary>
        /// <remarks>Setting an item at a specific index is not supported.</remarks>
        /// <param name="index">The zero-based index to retrieve an item for</param>
        /// <returns>The item at the specified index</returns>
        public PerfCounterMetric this[int index]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_CounterMetricList.Values[index];
                }
            }
            set => throw new NotSupportedException();
        }

        #endregion

        #region ICollection<PerfCounterMetric> Members

        /// <summary>
        /// Add an existing performance counter metric item to this group
        /// </summary>
        /// <param name="item">The performance counter metric item to add.</param>
        public void Add(PerfCounterMetric item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "An existing performance counter metric item must be provided to add it to the collection.");
            }

            //we're about to modify the collection, get a lock.  We don't want the lock to cover the changed event since
            //we really don't know how long that will take, and it could be deadlock prone.
            lock (m_Lock)
            {
                if(m_CounterMetricDictionary.ContainsKey(item.Name))
                {
                    throw new ArgumentException("The specified performance counter metric item is already in the collection.", nameof(item));
                }

                //add it to our collections.
                m_CounterMetricDictionary.Add(item.Name, item);
                m_CounterMetricList.Add(item, item);
            }

            //and fire our event
            OnCollectionChanged(new CollectionChangedEventArgs<PerfCounterCollection, PerfCounterMetric>(this, item, CollectionAction.Added));
            
        }

        /// <summary>
        /// Clears the entire collection.
        /// </summary>
        public void Clear()
        {
            int count;
            lock (m_Lock)
            {
                //Only do this if we HAVE something, since events are fired.
                count = m_CounterMetricList.Count;
                if (count > 0)
                {
                    m_CounterMetricList.Clear();
                }
            }

            if (count > 0)
            {
                //and raise the event so our caller knows we're cleared, should be done outside the lock.
                OnCollectionChanged(new CollectionChangedEventArgs<PerfCounterCollection, PerfCounterMetric>(this, null, CollectionAction.Cleared));
            }
        }

        /// <summary>
        /// Indicates whether the collection contains the specified performance counter
        /// </summary>
        /// <param name="item">The performance counter object to check for</param>
        /// <returns>True if the item is found, false otherwise.</returns>
        public bool Contains(PerfCounterMetric item)
        {
            lock (m_Lock)
            {
                //here we are relying on the fact that the comment object implements IComparable sufficiently to guarantee uniqueness
                return m_CounterMetricList.ContainsValue(item);
            }
        }

        /// <summary>
        /// Copies the entire collection into the provided array, starting at the specified index.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(PerfCounterMetric[] array, int arrayIndex)
        {
            lock (m_Lock)
            {
                m_CounterMetricList.Values.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// The number of items in the collection.
        /// </summary>
        public int Count
        {
            get
            {
                lock (m_Lock)
                {
                    return m_CounterMetricList.Count;
                }
            }
        }

        /// <summary>
        /// Returns false, this collection is not read only.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Remove the specified PerfCounterMetric item.  If the PerfCounterMetric isn't in the collection, no exception is thrown.
        /// </summary>
        /// <param name="item">The PerfCounterMetric item to remove.</param>
        public bool Remove(PerfCounterMetric item)
        {
            bool result = false;

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A performance counter metric item must be provided to remove it from the collection.");
            }

            lock (m_Lock)
            {
                //we have to remove it from both collections, and we better not raise an error if not there.
                if (m_CounterMetricDictionary.ContainsKey(item.Name))
                {
                    m_CounterMetricList.Remove(item);
                    result = true; // we did remove something
                }

                if (m_CounterMetricList.ContainsKey(item))
                {
                    m_CounterMetricList.Remove(item);
                    result = true; // we did remove something
                }
            }

            if (result)
            {
                //and fire our event if we actually removed something.
                OnCollectionChanged(new CollectionChangedEventArgs<PerfCounterCollection, PerfCounterMetric>(this, item, CollectionAction.Removed));
            }

            return result;
        }


        #endregion

    }
}
