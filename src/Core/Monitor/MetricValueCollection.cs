using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Gibraltar.Monitor.Internal;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// A set of display-ready values for a metric. 
    /// </summary>
    /// <remarks>
    /// These are after any necessary calculation or interpolation.
    /// To get a value set, use the Calculate method on a metric.  For best performance, specify the least accurate
    /// interval you need to graph.
    /// </remarks>
    public class MetricValueCollection : IMetricValueCollection, IList, IDisplayable
    {
        //MetricValue itself implements IComparable, so it will determine order
        private readonly SortedList<IMetricValue, IMetricValue> m_ValueList = new SortedList<IMetricValue, IMetricValue>(); // LOCKED
        private readonly object m_Lock = new object();
        private readonly string m_UnitCaption;
        private readonly MetricSampleInterval m_Interval;
        private readonly int m_Intervals;

        private string m_Caption;
        private string m_Description;
        private volatile MetricValue m_MinValueMetricValue; // Only changed inside the LOCK, volatile for unlocked reads
        private volatile MetricValue m_MaxValueMetricValue; // Only changed inside the LOCK, volatile for unlocked reads
        private double m_AverageValue; //Protected by LOCK
        private double m_PercentileValue; //Protected by LOCK
        private bool m_StatsCurrent; //Protected by LOCK
        private long m_SampleSequence; //used when adding sampled metrics to ensure order.

        /// <summary>Create a new metric value set.</summary>
        /// <param name="metric">The metric this value set relates to.</param>
        /// <param name="interval">The interval to bias to.</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="unitCaption">An end-user short display caption for the unit axis.</param>
        public MetricValueCollection(Metric metric, MetricSampleInterval interval, int intervals, string unitCaption)
        {
            //copy our metric's caption and description
            m_Caption = metric.Caption;
            m_Description = metric.Description;
            m_Interval = interval;
            m_Intervals = intervals;
            m_UnitCaption = unitCaption;
        }

        /// <summary>Create a new metric value set.</summary>
        /// <param name="trendValue">The event metric value this value set relates to.</param>
        /// <param name="interval">The interval to bias to.</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="unitCaption">An end-user short display caption for the unit axis.</param>
        public MetricValueCollection(EventMetricValueDefinition trendValue, MetricSampleInterval interval, int intervals, string unitCaption)
        {
            //copy our metric's caption and description
            m_Caption = trendValue.Caption;
            m_Description = trendValue.Description;
            m_Interval = interval;
            m_Intervals = intervals;
            m_UnitCaption = unitCaption;
        }

        /// <summary>Create a new metric value set.</summary>
        /// <param name="caption">The end-user caption for this metric value set.</param>
        /// <param name="description">The end-user description for this metric value set.</param>
        /// <param name="interval">The interval to bias to.</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="unitCaption">An end-user short display caption for the unit axis.</param>
        public MetricValueCollection(string caption, string description, MetricSampleInterval interval, int intervals, string unitCaption)
        {
            //copy our metric's caption and description
            m_Caption = caption;
            m_Description = description;
            m_Interval = interval;
            m_Intervals = intervals;
            m_UnitCaption = unitCaption;
        }

        /// <summary>Create a new metric value set.</summary>
        /// <param name="caption">The end-user caption for this metric value set.</param>
        /// <param name="description">The end-user description for this metric value set.</param>
        /// <param name="interval">The interval.</param>
        /// <param name="unitCaption">An end-user short display caption for the unit axis.</param>
        public MetricValueCollection(string caption, string description, TimeSpan interval, string unitCaption)
        {
            //copy our metric's caption and description
            m_Caption = caption;
            m_Description = description;
            
            // TODO: We should probably store the TimeSpan instead of the MetricSampleInterval
            m_Interval =  MetricSampleInterval.Millisecond;
            m_Intervals = (int)interval.TotalMilliseconds;
            m_UnitCaption = unitCaption;
        }

        #region Public Properties and Methods

        /// <summary>
        /// A display caption for this metric set.
        /// </summary>
        public string Caption { get { return m_Caption; } set { m_Caption = value; } }

        /// <summary>
        /// A description of this metric set.
        /// </summary>
        public string Description { get { return m_Description; } set { m_Description = value; } }

        /// <summary>
        /// The unit of intervals between samples.  If not set to default or shortest, see the Intervals property for how many intervals between samples.
        /// </summary>
        /// <remarks>To get a higher resolution data set in the case when the sample interval is not set to shortest, use the CalculateValues method on the metric.</remarks>
        public MetricSampleInterval Interval { get { return m_Interval; } }

        /// <summary>
        /// The number of intervals between samples in the interval set.
        /// </summary>
        /// <remarks>This property is not meaningful if the sample interval is set to default or shortest.</remarks>
        public int Intervals { get { return m_Intervals; } }

        /// <summary>
        /// The start date and time of this value set interval.  This may not represent all of the data available in the metric.
        /// </summary>
        public DateTimeOffset StartDateTime 
        { 
            get 
            { 
                //we're a sorted list by date & time, so the start is just the first item, really.
                //Never forget that it's indexed from zero!
                return this[0].Timestamp;
            } 
        }

        /// <summary>
        /// The end date and time of this value set interval.  This may not represent all of the data available in the metric.
        /// </summary>
        public DateTimeOffset EndDateTime
        {
            get
            {
                //we're a sorted list by date & time, so the start is just the last item, really.  
                //Never forget that it's indexed from zero!
                return this[Count - 1].Timestamp;
            }
        }

        /// <summary>
        /// The smallest value in the value set, useful for setting ranges for display.  The minimum value may be negative.
        /// </summary>
        public double MinValue { get { return m_MinValueMetricValue.Value; } }

        /// <summary>
        /// The metric object with the smallest value in the value set, useful for setting ranges for display.  The minimum value may be negative.
        /// </summary>
        public IMetricValue MinValueMetricValue { get { return m_MinValueMetricValue; } }

        /// <summary>
        /// The largest value in the value set, useful for setting ranges for display.  The maximum value may be negative.
        /// </summary>
        public double MaxValue { get { return m_MaxValueMetricValue.Value; } }

        /// <summary>
        /// The metric object with the largest value in the value set, useful for setting ranges for display.  The maximum value may be negative.
        /// </summary>
        public IMetricValue MaxValueMetricValue { get { return m_MaxValueMetricValue; } }

        /// <summary>
        /// The average value in the value set, useful for setting ranges for display.  The average value may be negative.
        /// </summary>
        public double AverageValue
        {
            get
            {
                //since the average is a double we still have to use lock on it to protect multi-threaded access.
                lock (m_Lock)
                {
                    return m_AverageValue;
                }
            }
        }

        /// <summary>
        /// The 95th percentile value in the value set.  The percentile value may be negative.
        /// </summary>
        public double PercentileValue
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_StatsCurrent == false)
                        CalculateValues();

                    return m_PercentileValue;
                }
            }
        }

        /// <summary>
        /// The display caption for the values in this set
        /// </summary>
        public string UnitCaption { get { return m_UnitCaption; } }


        IEnumerator IEnumerable.GetEnumerator()
        {
            //we use the sorted list for enumeration
            return m_ValueList.Values.GetEnumerator();
        }

        IEnumerator<IMetricValue> IEnumerable<IMetricValue>.GetEnumerator()
        {
            //we use the sorted list for enumeration
            return m_ValueList.Values.GetEnumerator();
        }

        /// <summary>
        /// Searches for the specified value and returns the zero-based index of the first occurrence
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(IMetricValue item)
        {
            lock (m_Lock)
            {
                return m_ValueList.IndexOfKey(item);
            }
        }

        /// <summary>
        /// Not Supported.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, IMetricValue item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        /// <summary>
        /// Removes the item at the specified index.  REMOVING IS NOT CURRENTLY SUPPORTED.
        /// </summary>
        /// <remarks>
        /// The elements that follow the removed element are moved up to occupy the vacated spot.  The indexes of the elements that are moved are also updated.
        /// </remarks>
        /// <param name="index">The zero-based index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            IMetricValue victim;
            lock (m_Lock)
            {
                //find the item at the requested location
                victim = m_ValueList.Values[index];
            }

            //and pass that to our normal remove method.  Must be called outside the lock because it fires an event?
            Remove(victim); // Actually, this will throw an exception... not supported.
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set. 
        ///                 </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.IList"/>. 
        ///                 </exception><exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.IList"/> is read-only. 
        ///                 </exception><filterpriority>2</filterpriority>
        object IList.this[int index] { get { return ((IList<IMetricValue>)this)[index]; } set { throw new NotSupportedException(); } }

        /// <summary>
        /// Gets the item in the collection at the specified zero-based index.
        /// </summary>
        /// <remarks>Setting an item at a specific index is not supported.</remarks>
        /// <param name="index">The zero-based index to retrieve an item for</param>
        /// <returns>The item at the specified index</returns>
        public IMetricValue this[int index]
        {
            get
            {
                lock (m_Lock)
                {
                    return m_ValueList.Values[index];
                }
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Adds the supplied MetricValue item to this collection.
        /// </summary>
        /// <remarks>The MetricValue item must refer to this metric value set, and have a unique timestamp.</remarks>
        /// <param name="item">The new MetricValue item to add.</param>
        public void Add(IMetricValue item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "No metric value item was provided to add to the collection");
            }

            if (item.ValueCollection != this)
            {
                throw new ArgumentException("The provided metric value item was not created for this metric value set.", nameof(item));
            }

            lock (m_Lock)
            {
                //OK, now add it to our sorted list.
                m_ValueList.Add(item, item);

                MetricValue nativeValue = (MetricValue)item;

                //since we were successful, now compare it with our min & max values, etc. to make sure that they are up to date.
                if (m_MinValueMetricValue == null)
                {
                    //we are automatically the new minimum value
                    m_MinValueMetricValue = nativeValue;
                }
                else if (m_MinValueMetricValue.Value > nativeValue.Value)
                {
                    //we are less than the previous minimum value, making us the new minimum value
                    m_MinValueMetricValue = nativeValue;
                }

                if (m_MaxValueMetricValue == null)
                {
                    //we are automatically the new maximum value
                    m_MaxValueMetricValue = nativeValue;
                }
                else if (m_MaxValueMetricValue.Value < nativeValue.Value)
                {
                    //we are greater than the old maximum value, making us the new maximum value
                    m_MaxValueMetricValue = nativeValue;
                }

                //to avoid overflow we have to calculate our average incrementally, otherwise it could go bad....
                m_AverageValue = ((nativeValue.Value - m_AverageValue) / (m_ValueList.Count)) + m_AverageValue;

                m_StatsCurrent = false; //even if they were, they aren't now.
            }
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <returns>
        /// The position into which the new element was inserted.
        /// </returns>
        /// <param name="value">The <see cref="T:System.Object"/> to add to the <see cref="T:System.Collections.IList"/>. 
        ///                 </param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only.
        ///                     -or- 
        ///                     The <see cref="T:System.Collections.IList"/> has a fixed size. 
        ///                 </exception><filterpriority>2</filterpriority>
        public int Add(object value)
        {
            Add((IMetricValue)value);
            return IndexOf((IMetricValue)value);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.IList"/> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Object"/> is found in the <see cref="T:System.Collections.IList"/>; otherwise, false.
        /// </returns>
        /// <param name="value">The <see cref="T:System.Object"/> to locate in the <see cref="T:System.Collections.IList"/>. 
        ///                 </param><filterpriority>2</filterpriority>
        public bool Contains(object value)
        {
            return Contains((IMetricValue)value);
        }

        /// <summary>
        /// Clearing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        public void Clear()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="value"/> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="value">The <see cref="T:System.Object"/> to locate in the <see cref="T:System.Collections.IList"/>. 
        ///                 </param><filterpriority>2</filterpriority>
        public int IndexOf(object value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.IList"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="value"/> should be inserted. 
        ///                 </param><param name="value">The <see cref="T:System.Object"/> to insert into the <see cref="T:System.Collections.IList"/>. 
        ///                 </param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.IList"/>. 
        ///                 </exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only.
        ///                     -or- 
        ///                     The <see cref="T:System.Collections.IList"/> has a fixed size. 
        ///                 </exception><exception cref="T:System.NullReferenceException"><paramref name="value"/> is null reference in the <see cref="T:System.Collections.IList"/>.
        ///                 </exception><filterpriority>2</filterpriority>
        public void Insert(int index, object value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Determines whether an element is in the collection.
        /// </summary>
        /// <remarks>This method determines equality using the default equality comparer for the type of values in the list.  It performs
        /// a linear search and therefore is an O(n) operation.</remarks>
        /// <param name="item">The object to locate in the collection.</param>
        /// <returns>true if the item is found in the collection; otherwise false.</returns>
        public bool Contains(IMetricValue item)
        {
            lock (m_Lock)
            {
                //here we are relying on the fact that the MetricValue object implements IComparable sufficiently to guarantee uniqueness
                return m_ValueList.ContainsKey(item);
            }
        }

        /// <summary>
        /// Copies the entire collection to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <remarks>Elements are copied to the array in the same order in which the enumerator iterates them from the collection.  The provided array 
        /// must be large enough to contain the entire contents of this collection starting at the specified index.  This method is an O(n) operation.</remarks>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the collection.  The Array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(IMetricValue[] array, int arrayIndex)
        {
            lock (m_Lock)
            {
                m_ValueList.Values.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection"/>. The <see cref="T:System.Array"/> must have zero-based indexing. 
        ///                 </param><param name="index">The zero-based index in <paramref name="array"/> at which copying begins. 
        ///                 </param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null. 
        ///                 </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero. 
        ///                 </exception><exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.
        ///                     -or- 
        ///                 <paramref name="index"/> is equal to or greater than the length of <paramref name="array"/>.
        ///                     -or- 
        ///                     The number of elements in the source <see cref="T:System.Collections.ICollection"/> is greater than the available space from <paramref name="index"/> to the end of the destination <paramref name="array"/>. 
        ///                 </exception><exception cref="T:System.ArgumentException">The type of the source <see cref="T:System.Collections.ICollection"/> cannot be cast automatically to the type of the destination <paramref name="array"/>. 
        ///                 </exception><filterpriority>2</filterpriority>
        public void CopyTo(Array array, int index)
        {
            lock (m_Lock)
            {
                m_ValueList.Values.CopyTo((IMetricValue[])array, index);
            }
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
                    return m_ValueList.Count;
                }
            }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </summary>
        /// <returns>
        /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public object SyncRoot { get { return m_Lock; } }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe).
        /// </summary>
        /// <returns>
        /// true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe); otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public bool IsSynchronized { get { return true; } }

        /// <summary>
        /// Indicates if the collection is read only and therefore can't have items added or removed.
        /// </summary>
        /// <remarks>This collection is always read-only.  
        /// This property is required for ICollection compatibility</remarks>
        public bool IsReadOnly
        {
            //yes, we are read only.
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IList"/> has a fixed size.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.IList"/> has a fixed size; otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public bool IsFixedSize { get { return true; } }

        /// <summary>
        /// Removing objects is not supported.
        /// </summary>
        /// <remarks>This method is implemented only for ICollection interface support and will throw an exception if called.</remarks>
        /// <param name="item">The MetricValue item to remove.</param>
        public bool Remove(IMetricValue item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <param name="value">The <see cref="T:System.Object"/> to remove from the <see cref="T:System.Collections.IList"/>. 
        ///                 </param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList"/> is read-only.
        ///                     -or- 
        ///                     The <see cref="T:System.Collections.IList"/> has a fixed size. 
        ///                 </exception><filterpriority>2</filterpriority>
        public void Remove(object value)
        {
            Remove((IMetricValue)value);
        }


        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// A unique, increasing sequence number each time it's called.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <returns></returns>
        internal long GetSampleSequence()
        {
            long returnVal = Interlocked.Increment(ref m_SampleSequence);
            return returnVal;
        }

        #endregion

        /// <summary>
        /// Calculate the average and percentile values, since they can't be incrementally updated.
        /// </summary>
        private void CalculateValues()
        {
            lock (m_Lock)
            {
                //we have to iterate the entire set of details to sort them.
                int count = 0;
                double[] allSamples = new double[Count]; 
                foreach (IMetricValue metricValue in this)
                {
                    allSamples[count] = metricValue.Value; //this creates a coupling that count is incremented AFTER here.
                    count++;
                }

                //to work out the percentile we have to throw out the last 5% of the values.
                int includeCount = Convert.ToInt32(0.95 * count); //this appears to do an arithmetic round.
                if (count > 0)
                {
                    Array.Sort(allSamples);
                }

                //now the sample we want is the value right at our limit because we're already sorted.
                if (includeCount > 0)
                {
                    m_PercentileValue = allSamples[includeCount - 1];
                }
                else if (count == 1) //we aren't relying on arithmetic round, more of a truncate behavior.
                {
                    m_PercentileValue = allSamples[0];
                }
                else
                {
                    m_PercentileValue = 0;
                }

                m_StatsCurrent = true;
            }
        }
    }
}
