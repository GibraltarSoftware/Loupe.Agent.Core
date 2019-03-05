using System;
using System.Collections.Generic;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// A collection of metric samples for a metric.
    /// </summary>
    public class MetricSampleCollection : IMetricSampleCollection
    {
        private readonly SortedList<long, IMetricSample> m_List = new SortedList<long, IMetricSample>(); //we use sequence # for order
        private readonly Metric m_Metric;
        private readonly object m_Lock = new object();

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs<IMetricSampleCollection, IMetricSample>> CollectionChanged;

        /// <summary>
        /// Create a new sample collection for the specified metric object
        /// </summary>
        /// <param name="metric"></param>
        public MetricSampleCollection(Metric metric)
        {
            //store off the metric we're related to
            m_Metric = metric;
        }

        /// <summary>
        /// The metric this collection of samples is related to
        /// </summary>
        public IMetric Metric { get { return m_Metric; } }

        /// <summary>
        /// The first object in the collection
        /// </summary>
        public IMetricSample First
        {
            get
            {
                lock (m_Lock)
                {
                    return m_List.Values[0];
                }
            }
        }

        /// <summary>
        /// The last object in the collection
        /// </summary>
        public IMetricSample Last
        {
            get
            {
                lock (m_Lock)
                {
                    return m_List.Values[m_List.Count - 1];
                }
            }
        }

        #region Protected Properties and Methods

        /// <summary>
        /// This method is called every time a collection change event occurs to allow inheritors to override the change event.
        /// </summary>
        /// <remarks>If overridden, it is important to call this base implementation to actually fire the event.</remarks>
        /// <param name="e"></param>
        protected virtual void OnCollectionChanged(CollectionChangedEventArgs<IMetricSampleCollection, IMetricSample> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<IMetricSampleCollection, IMetricSample>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region IEnumerable<MetricSample> Members

        IEnumerator<IMetricSample> IEnumerable<IMetricSample>.GetEnumerator()
        {
            //we use the sorted list for enumeration
            return m_List.Values.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            //we use the sorted list for enumeration
            return m_List.GetEnumerator();
        }

        #endregion

        #region IList<MetricSample> Members

        /// <summary>
        /// Retrieves the numerical index of the specified item in the collection or -1 if not found.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(IMetricSample item)
        {
            lock (m_Lock)
            {
                return m_List.IndexOfKey(item.Sequence);
            }
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, IMetricSample item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        /// <summary>
        /// Remove a sample at a specific numerical index.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            IMetricSample victim;
            bool result;

            lock (m_Lock)
            {
                //find the item at the requested location
                victim = m_List.Values[index];
                //and pass that to our private remove method
                result = RemoveItem(victim);
            }

            //and fire our event if we changed something, outside the lock
            if (result)
            {
                OnCollectionChanged(new CollectionChangedEventArgs<IMetricSampleCollection, IMetricSample>(this, victim, CollectionAction.Removed));
            }

            return;
        }

        /// <summary>
        /// Select a metric sample by its numerical index
        /// </summary>
        /// <remarks>Setting a metric sample to a particular index is not supported and will result in an exception being thrown.</remarks>
        /// <param name="index"></param>
        /// <returns></returns>
        public IMetricSample this[int index]
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

        #endregion

        #region ICollection<MetricSample> Members

        /// <summary>
        /// Add the specified MetricSample item to the collection
        /// </summary>
        /// <param name="item">The new MetricSample item to add</param>
        public void Add(IMetricSample item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A new  metric sample item must be provided to add it to the collection.");
            }

            lock (m_Lock)
            {
                //add it to both lookup collections
                m_List.Add(item.Sequence, item);
            }

            //and fire our event
            OnCollectionChanged(new CollectionChangedEventArgs<IMetricSampleCollection, IMetricSample>(this, item, CollectionAction.Added));
        }

        /// <summary>
        /// Remove all elements from the collection.
        /// </summary>
        /// <remarks>This event is substantially more efficient than removing them all individually.  Only one collection
        /// change event will be raised.</remarks>
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
                }
            }

            if (count > 0)
            {
                //and raise the event so our caller knows we're cleared
                OnCollectionChanged(new CollectionChangedEventArgs<IMetricSampleCollection, IMetricSample>(this, null, CollectionAction.Cleared));
            }
        }

        /// <summary>
        /// Determines whether an element is in the collection.
        /// </summary>
        /// <remarks>This method determines equality using the default equality comparer for the type of values in the list.  It performs
        /// a linear search and therefore is an O(n) operation.</remarks>
        /// <param name="item">The object to locate in the collection.</param>
        /// <returns>true if the item is found in the collection; otherwise false.</returns>
        public bool Contains(IMetricSample item)
        {
            lock (m_Lock)
            {
                //here we are relying on the fact that the MetricSample object implements IComparable sufficiently to guarantee uniqueness
                return m_List.ContainsKey(item.Sequence);
            }
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(long key)
        {
            lock (m_Lock)
            {
                //gateway to our inner dictionary 
                return m_List.ContainsKey(key);
            }
        }

        /// <summary>
        /// Copies the entire collection to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <remarks>Elements are copied to the array in the same order in which the enumerator iterates them from the collection.  The provided array 
        /// must be large enough to contain the entire contents of this collection starting at the specified index.  This method is an O(n) operation.</remarks>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the collection.  The Array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(IMetricSample[] array, int arrayIndex)
        {
            lock (m_Lock)
            {
                m_List.Values.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Copy the entire collection of metric samples into a new array.
        /// </summary>
        /// <returns>A new array containing all of the metric samples in this collection.</returns>
        public IMetricSample[] ToArray()
        {
            MetricSample[] array;
            lock (m_Lock)
            {
                int count = Count;
                array = new MetricSample[count];
                CopyTo(array, 0);
            }

            return array;
        }

        /// <summary>
        /// The number of items currently in the collection
        /// </summary>
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

        /// <summary>
        /// Indicates if the collection is read only and therefore can't have items added or removed.
        /// </summary>
        /// <remarks>This collection is never read-only.  
        /// This property is required for ICollection compatibility</remarks>
        public bool IsReadOnly
        {
            //we are never read-only.
            get { return false; }
        }

        /// <summary>
        /// Remove the specified MetricSample item.  If the MetricSample isn't in the collection, no exception is thrown.
        /// </summary>
        /// <param name="item">The MetricSample item to remove.</param>
        public bool Remove(IMetricSample item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A metric sample item must be provided to remove it from the collection.");
            }

            bool result = RemoveItem(item);

            //and fire our event
            //and fire our event if we changed something
            if (result)
            {
                OnCollectionChanged(new CollectionChangedEventArgs<IMetricSampleCollection, IMetricSample>(this, item, CollectionAction.Removed));
            }

            return result;
        }

        /// <summary>
        /// Remove the specified MetricSample item without firing an event.  The outer Remove method should fire the event.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private bool RemoveItem(IMetricSample item)
        {
            lock (m_Lock)
            {
                bool result = m_List.Remove(item.Sequence);
                return result;
            }
        }

        #endregion
    }
}
