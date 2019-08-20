using System;
using System.Collections;
using System.Collections.Generic;
using Loupe.Extensibility.Data;

namespace Loupe.Monitor
{
    /// <summary>
    /// The sorted list of all log messages
    /// </summary>
    public sealed class LogMessageCollection : IList<LogMessage>, IList 
    {
        private readonly SortedList<long, LogMessage> m_List = new SortedList<long, LogMessage>();
        private readonly Session m_Session;
        private readonly object m_Lock = new object(); //used to synchronize access to the collection

        private LogMessageTree m_ClassTree;
        private LogMessageTree m_CategoryTree;

        //some private tracking things we use to help callers customize their displays.
        private bool m_HasDetails;
        private bool m_HasExceptionInfos;
        private bool m_HasMultipleUsers;
        private bool m_HasMultipleLogSystems;
        private bool m_HasSourceLocation;

        private string m_ReferenceUser;
        private string m_ReferenceLogSystem;

        /// <summary>
        /// Raised every time the collection's contents are changed to allow subscribers to automatically track changes.
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs<LogMessageCollection, LogMessage>> CollectionChanged;

        /// <summary>
        /// Create a new collection for the specified session
        /// </summary>
        /// <param name="session"></param>
        internal LogMessageCollection(Session session)
        {
            //store off the session we're related to
            m_Session = session;
        }

        #region Private Properties and Methods

        private void GenerateTrees()
        {
            //set up the trees
            m_ClassTree = new LogMessageTree("class", "Classes", "Log Messages by class", ClassTreeFullName, ClassTreeMessageGroup);
            m_CategoryTree = new LogMessageTree("category", "Categories", "Log Messages by category", CategoryTreeFullName, CategoryTreeMessageGroup);

            //we are going to pass through every log message and categorize it.
            foreach (LogMessage logMessage in m_List.Values)
            {
                if (logMessage.HasMethodInfo)
                    m_ClassTree.AddMessage(logMessage);

                if (string.IsNullOrEmpty(logMessage.CategoryName) == false)
                    m_CategoryTree.AddMessage(logMessage);
            }
        }

        private static string[] CategoryTreeMessageGroup(LogMessage message)
        {
            return message.CategoryNames;
        }

        private static string CategoryTreeFullName(LogMessage message)
        {
            return message.CategoryName;
        }

        private static string[] ClassTreeMessageGroup(LogMessage message)
        {
            return message.ClassNames;
        }

        private static string ClassTreeFullName(LogMessage message)
        {
            return message.ClassName;
        }

        private void EnsureSorted()
        {
        /*
            if (m_SortRequired)
            {
                m_SortRequired = false;
                m_List.Sort();
            }
         **/
        }

        /// <summary>
        /// This method is called every time a collection change event occurs to allow inheritors to override the change event.
        /// </summary>
        /// <remarks>If overriden, it is important to call this base implementation to actually fire the event.</remarks>
        /// <param name="e"></param>
        private void OnCollectionChanged(CollectionChangedEventArgs<LogMessageCollection, LogMessage> e)
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler<CollectionChangedEventArgs<LogMessageCollection, LogMessage>> tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        private MetricValueCollection OnCalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset startDateTime, DateTimeOffset endDateTime)
        {
            EnsureSorted();

            MetricValueCollection newMetricValueCollection = new MetricValueCollection("Log Messages", "The number of log messages recorded per interval.", interval, intervals, "Messages");

            //And prep for our process loop
            DateTimeOffset windowStartDateTime = startDateTime;
            DateTimeOffset windowEndDateTime = CalculateOffset(windowStartDateTime, interval, intervals);


            //because we're doing a simple count by time interval, it's easiest to iterate
            //through all of the intervals and then count the matches within.
            int curSampleIndex = 0;

            //roll through the samples to find our first sample index we care about so we can count from there.
            for (curSampleIndex = 0; curSampleIndex < Count; curSampleIndex++)
            {
                if (this[curSampleIndex].Timestamp >= startDateTime)
                {
                    //bingo!  this is the first one we want
                    break;
                }
            }

            while (windowEndDateTime <= endDateTime)
            {
                //count how many messages we go through to get to our target.                
                int curMessageCount = 0;
                
                while(curSampleIndex < Count)
                {
                    if (this[curSampleIndex].Timestamp >= windowEndDateTime)
                    {
                        //this is the first one in the next interval (or later), so don't count it and stop looping.
                        break;
                    }
                    else
                    {
                        //this bad boy is in our range, count it
                        curMessageCount++;
                    }

                    //and make damn sure we advance our index by one to not get into an infinite loop
                    curSampleIndex++;
                }

                //record off this interval
                new MetricValue(newMetricValueCollection, windowStartDateTime, curMessageCount);

                //and prep for the next loop
                windowStartDateTime = windowEndDateTime;
                windowEndDateTime = CalculateOffset(windowStartDateTime, interval, intervals);
            }

            return newMetricValueCollection;
        }


        /// <summary>
        /// Calculates the offset date from the provided baseline for the specified interval
        /// </summary>
        /// <remarks>
        /// To calculate a backwards offset (the date that is the specified interval before the baseline) use a negative
        /// number of invervals. For example, -1 intervals will give you one interval before the baseline.
        /// </remarks>
        /// <param name="baseline">The date and time to calculate an offset date and time from</param>
        /// <param name="interval">The interval to add or subtract from the baseline</param>
        /// <param name="intervals">The number of intervals to go forward or (if negative) backwards</param>
        /// <returns></returns>
        private DateTimeOffset CalculateOffset(DateTimeOffset baseline, MetricSampleInterval interval, int intervals)
        {
            DateTimeOffset returnVal;  //just so we're initialized with SOMETHING.
            int intervalCount = intervals;

            //since they aren't using shortest, we are going to use the intervals input option which better not be zero or negative.
            if ((intervals == 0) && (interval != MetricSampleInterval.Shortest))
            {
                throw new ArgumentOutOfRangeException(nameof(intervals), intervals, "The number of intervals can't be zero if the interval isn't set to Shortest.");
            }

            switch (interval)
            {
                case MetricSampleInterval.Default: //use how the data was recorded
                    //default and ours is default - use second.
                    returnVal = CalculateOffset(baseline, MetricSampleInterval.Second, intervalCount);
                    break;
                case MetricSampleInterval.Shortest:
                    //exlicitly use the shortest value available, 16 milliseconds
                    returnVal = baseline.AddMilliseconds(16); //interval is ignored in the case of the "shortest" configuration
                    break;
                case MetricSampleInterval.Millisecond:
                    returnVal = baseline.AddMilliseconds(intervalCount);
                    break;
                case MetricSampleInterval.Second:
                    returnVal = baseline.AddSeconds(intervalCount);
                    break;
                case MetricSampleInterval.Minute:
                    returnVal = baseline.AddMinutes(intervalCount);
                    break;
                case MetricSampleInterval.Hour:
                    returnVal = baseline.AddHours(intervalCount);
                    break;
                case MetricSampleInterval.Day:
                    returnVal = baseline.AddDays(intervalCount);
                    break;
                case MetricSampleInterval.Week:
                    returnVal = baseline.AddDays(intervalCount * 7);
                    break;
                case MetricSampleInterval.Month:
                    returnVal = baseline.AddMonths(intervalCount);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(interval));
            }

            return returnVal;
        }
        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// The session this collection of log messages is related to
        /// </summary>
        public Session Session { get { return m_Session; } }

        /// <summary>
        /// A tree of log message groups for the log messages by category.
        /// </summary>
        public LogMessageTree CategoryTree
        {
            get
            {
                if (m_CategoryTree == null)
                {
                    GenerateTrees();
                }

                return m_CategoryTree;
            }
        }

        /// <summary>
        /// A tree of log message groups for messages by namespace and class.
        /// </summary>
        public LogMessageTree ClassTree
        {
            get
            {
                if (m_ClassTree == null)
                {
                    GenerateTrees();
                }

                return m_ClassTree;
            }
        }

        /// <summary>
        /// The first object in the collection
        /// </summary>
        public LogMessage First
        {
            get
            {
                EnsureSorted();
                return m_List.Values[0];
            }
        }

        /// <summary>
        /// The last object in the collection
        /// </summary>
        public LogMessage Last
        {
            get
            {
                EnsureSorted();
                return m_List.Values[m_List.Count - 1];
            }
        }

        /// <summary>
        /// Calculate the number of messages per second present in the entire messages collection.
        /// </summary>
        /// <returns>A metric value set suitable for display</returns>
        public MetricValueCollection CalculateValues()
        {
            //forward to our grander overload
            return CalculateValues(MetricSampleInterval.Second, 1, m_Session.Summary.StartDateTime, m_Session.Summary.EndDateTime);
        }

        /// <summary>
        /// Calculate the number of messages per interval present in the entire messages collection.
        /// </summary>
        /// <param name="interval">The requested data sample size</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <returns>A metric value set suitable for display</returns>
        public MetricValueCollection CalculateValues(MetricSampleInterval interval, int intervals)
        {
            //forward to our grander overload
            return CalculateValues(interval, intervals, m_Session.Summary.StartDateTime, m_Session.Summary.EndDateTime);
        }

        /// <summary>
        /// Calculate the number of messages per interval present in the specified time range.
        /// </summary>
        /// <param name="interval">The requested data sample size</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="startDateTime">The earliest date to retrieve data for</param>
        /// <param name="endDateTime">The last date to retrieve data for</param>
        /// <returns>A metric value set suitable for display</returns>
        public MetricValueCollection CalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset? startDateTime, DateTimeOffset? endDateTime)
        {
            DateTimeOffset effectiveStartDateTime = (startDateTime == null ? m_Session.Summary.StartDateTime : (DateTimeOffset)startDateTime);
            DateTimeOffset effectiveEndDateTime = (endDateTime == null ? m_Session.Summary.EndDateTime : (DateTimeOffset)endDateTime);

            //HOLD UP - enforce our floor for sensible intervals
            if ((intervals < 0) || ((interval != MetricSampleInterval.Shortest) && (intervals == 0)))
            {
                throw new ArgumentOutOfRangeException(nameof(intervals), intervals, "Negative intervals are not supported for calculating values.  Specify an interval count greater than zero, except for the Shortest interval where the interval count is ignored.");
            }

            if ((interval == MetricSampleInterval.Millisecond) && (intervals < 16))
            {
                intervals = 16;
            }

            return OnCalculateValues(interval, intervals, effectiveStartDateTime, effectiveEndDateTime);
        }



        IEnumerator<LogMessage> IEnumerable<LogMessage>.GetEnumerator()
        {
            EnsureSorted();

            //we use the sorted list for enumeration
            return m_List.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            EnsureSorted();

            //we use the sorted list for enumeration
            return m_List.Values.GetEnumerator();
        }

        /// <summary>
        /// Retrieves the numerical index of the specified item in the collection or -1 if not found.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(LogMessage item)
        {
            EnsureSorted();

            return m_List.Values.IndexOf(item);
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, LogMessage item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.IList" />.
        /// </summary>
        /// <param name="value">The <see cref="T:System.Object" /> to remove from the <see cref="T:System.Collections.IList" />. </param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList" /> is read-only.-or- The <see cref="T:System.Collections.IList" /> has a fixed size. </exception><filterpriority>2</filterpriority>
        public void Remove(object value)
        {
            Remove((LogMessage)value);
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            //we don't support removing a log message.
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set. </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.IList" />. </exception>
        /// <exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.IList" /> is read-only. </exception><filterpriority>2</filterpriority>
        object IList.this[int index] 
        {
            get
            {
                EnsureSorted();
                return m_List.Values[index];
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <remarks>Setting a metric sample to a particular index is not supported and will result in an exception being thrown.</remarks>
        /// <param name="index"></param>
        /// <returns></returns>
        public LogMessage this[int index]
        {
            get
            {
                EnsureSorted();
                return m_List.Values[index];
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.IList" />.
        /// </summary>
        /// <returns>
        /// The position into which the new element was inserted.
        /// </returns>
        /// <param name="value">The <see cref="T:System.Object" /> to add to the <see cref="T:System.Collections.IList" />. </param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList" /> is read-only.-or- The <see cref="T:System.Collections.IList" /> has a fixed size. </exception><filterpriority>2</filterpriority>
        public int Add(object value)
        {
            LogMessage logMessage = (LogMessage) value; // throws an exception if it can't cast to the right type
            Add(logMessage);
            return m_List.Values.IndexOf(logMessage);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.IList" /> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Object" /> is found in the <see cref="T:System.Collections.IList" />; otherwise, false.
        /// </returns>
        /// <param name="value">The <see cref="T:System.Object" /> to locate in the <see cref="T:System.Collections.IList" />. </param><filterpriority>2</filterpriority>
        public bool Contains(object value)
        {
            return m_List.Values.Contains((LogMessage)value);
        }

        /// <summary>
        /// Determines if the collection contains the specified sequence number key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(long key)
        {
            return m_List.ContainsKey(key);
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public void Clear()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.IList" />.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="value" /> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="value">The <see cref="T:System.Object" /> to locate in the <see cref="T:System.Collections.IList" />. </param><filterpriority>2</filterpriority>
        public int IndexOf(object value)
        {
            EnsureSorted();
            return m_List.Values.IndexOf((LogMessage)value);
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.IList" /> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="value" /> should be inserted. </param>
        /// <param name="value">The <see cref="T:System.Object" /> to insert into the <see cref="T:System.Collections.IList" />. </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.IList" />. </exception>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.IList" /> is read-only.-or- The <see cref="T:System.Collections.IList" /> has a fixed size. </exception>
        /// <exception cref="T:System.NullReferenceException"><paramref name="value" /> is null reference in the <see cref="T:System.Collections.IList" />.</exception><filterpriority>2</filterpriority>
        public void Insert(int index, object value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Add the specified LogMessage item to the collection
        /// </summary>
        /// <param name="item">The new LogMessage item to add</param>
        public void Add(LogMessage item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A new LogMessage item must be provided to add it to the collection.");
            }

            //add it to our internal list
            m_List.Add(item.Sequence, item);

            //see if we need to change one of our tracking flags.
            if (m_HasDetails == false)
            {
                m_HasDetails = (string.IsNullOrEmpty(item.Details) == false);
            }

            if (m_HasExceptionInfos == false)
            {
                m_HasExceptionInfos = item.HasException;
            }

            if (m_HasSourceLocation == false)
            {
                m_HasSourceLocation = item.HasSourceLocation;
            }

            if (m_HasMultipleUsers == false)
            {
                //a little tricker: we want to go true if there are two different, non-null user values in the log collection.
                if (string.IsNullOrEmpty(m_ReferenceUser))
                {
                    //this is the first non-null value.
                    m_ReferenceUser = item.UserName;
                }
                else if (string.IsNullOrEmpty(item.UserName) == false)
                {
                    m_HasMultipleUsers = (m_ReferenceUser.Equals(item.UserName, StringComparison.OrdinalIgnoreCase) == false);
                }
            }

            if (m_HasMultipleLogSystems == false)
            {
                //a little tricker: we want to go true if there are two different, non-null user values in the log collection.
                if (string.IsNullOrEmpty(m_ReferenceLogSystem))
                {
                    //this is the first non-null value.
                    m_ReferenceLogSystem = item.LogSystem;
                }
                else if (string.IsNullOrEmpty(item.LogSystem) == false)
                {
                    m_HasMultipleLogSystems = (m_ReferenceLogSystem.Equals(item.UserName, StringComparison.OrdinalIgnoreCase) == false);
                }
            }

            //and fire our event
            OnCollectionChanged(new CollectionChangedEventArgs<LogMessageCollection, LogMessage>(this, item, CollectionAction.Added));
        }

        /// <summary>
        /// Indicates if any of the log messages in the collection have detailed xml data
        /// </summary>
        public bool HasDetail { get { return m_HasDetails; } }

        /// <summary>
        /// Indicates if any of the log messages in the collection have exception information recorded
        /// </summary>
        public bool HasExceptionInfo { get { return m_HasExceptionInfos; } }

        /// <summary>
        /// Indicates if there is more than one user associated with the log messages
        /// </summary>
        public bool HasMultipleUsers { get { return m_HasMultipleUsers; } }

        /// <summary>
        /// Indicates if there is more than one log system associated with the log messages
        /// </summary>
        public bool HasMultipleLogSystems { get { return m_HasMultipleLogSystems; } }

        /// <summary>
        /// Indicates if any of the log messages have source code location information
        /// </summary>
        public bool HasSourceLocation { get { return m_HasSourceLocation; } }

        /// <summary>
        /// Determines whether an element is in the collection.
        /// </summary>
        /// <remarks>This method determines equality using the default equality comparer for the type of values in the list.  It performs
        /// a linear search and therefore is an O(n) operation.</remarks>
        /// <param name="item">The object to locate in the collection.</param>
        /// <returns>true if the item is found in the collection; otherwise false.</returns>
        public bool Contains(LogMessage item)
        {
            //here we are relying on the fact that the MetricSample object implements IComparable sufficiently to guarantee uniqueness
            return m_List.Values.Contains(item);
        }

        /// <summary>
        /// Copies the entire collection to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <remarks>Elements are copied to the array in the same order in which the enumerator iterates them from the collection.  The provided array 
        /// must be large enough to contain the entire contents of this collection starting at the specified index.  This method is an O(n) operation.</remarks>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the collection.  The Array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(LogMessage[] array, int arrayIndex)
        {
            EnsureSorted();
            m_List.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection" />. The <see cref="T:System.Array" /> must have zero-based indexing. </param>
        /// <param name="index">The zero-based index in <paramref name="array" /> at which copying begins. </param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array" /> is null. </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is less than zero. </exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="array" /> is multidimensional.-or- <paramref name="index" /> is equal to or greater than the length of <paramref name="array" />.-or- The number of elements in the source <see cref="T:System.Collections.ICollection" /> is greater than the available space from <paramref name="index" /> to the end of the destination <paramref name="array" />. </exception>
        /// <exception cref="T:System.ArgumentException">The type of the source <see cref="T:System.Collections.ICollection" /> cannot be cast automatically to the type of the destination <paramref name="array" />. </exception><filterpriority>2</filterpriority>
        public void CopyTo(Array array, int index)
        {
            EnsureSorted();
            LogMessage[] messages = new LogMessage[m_List.Count];
            m_List.Values.CopyTo(messages, index);

            //now put that in the provided array
            messages.CopyTo(array, index);
        }

        /// <summary>
        /// The number of items currently in the collection
        /// </summary>
        public int Count
        {
            get { return m_List.Count; }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.
        /// </summary>
        /// <returns>
        /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public object SyncRoot
        {
            get { return m_Lock; } 
        }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).
        /// </summary>
        /// <returns>
        /// true if access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe); otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public bool IsSynchronized
        {
            get { return true; } 
        }

        /// <summary>
        /// Indicates if the collection is read only and therefore can't have items added or removed.
        /// </summary>
        /// <remarks>This collection is always read-only.  
        /// This property is required for ICollection compatibility</remarks>
        public bool IsReadOnly
        {
            //we are always read only
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IList" /> has a fixed size.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.IList" /> has a fixed size; otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public bool IsFixedSize
        {
            get { return false; } 
        }

        /// <summary>
        /// Not supported, but no exception is returned.
        /// </summary>
        /// <param name="item">The LogMessage item to remove.</param>
        public bool Remove(LogMessage item)
        {
            return false;
        }

        #endregion

    }
}
