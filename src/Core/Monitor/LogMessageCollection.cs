using System;
using System.Collections;
using System.Collections.Generic;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// The sorted list of all log messages
    /// </summary>
    public sealed class LogMessageCollection : IList<LogMessage>, IList<ILogMessage>, IList
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
        private bool m_HasMultipleThreads;
        private bool m_HasSourceLocation;

        private long m_CriticalMessages;
        private long m_ErrorMessages;
        private long m_WarningMessages;
        private long m_InformationalMessages;
        private long m_VerboseMessages;

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
            m_ClassTree = new LogMessageTree("class", "Classes", "Log Messages by class",
                                             m => m.ClassName, m => m.ClassNames);
            m_CategoryTree = new LogMessageTree("category", "Categories", "Log Messages by category",
                                                m => m.CategoryName, m => m.CategoryNames);

            //we are going to pass through every log message and categorize it.
            foreach (LogMessage logMessage in m_List.Values)
            {
                if (logMessage.HasMethodInfo)
                    m_ClassTree.AddMessage(logMessage);

                if (string.IsNullOrEmpty(logMessage.CategoryName) == false)
                    m_CategoryTree.AddMessage(logMessage);
            }
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
        /// <remarks>If overridden, it is important to call this base implementation to actually fire the event.</remarks>
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

                while (curSampleIndex < Count)
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
        /// number of intervals. For example, -1 intervals will give you one interval before the baseline.
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
        public Session Session => m_Session;

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
                if (m_List.Count == 0) return null;

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
                if (m_List.Count == 0) return null;

                EnsureSorted();

                return m_List.Values[m_List.Count - 1];
            }
        }

        /// <summary>
        /// Indicates if there is more than one thread associated with the log messages
        /// </summary>
        public bool HasMultipleThreads => m_HasMultipleThreads;

        /// <summary>
        /// The number of messages in the messages collection.
        /// </summary>
        public long MessageCount => m_List.Count;

        /// <summary>
        /// The number of critical messages in the messages collection.
        /// </summary>
        public long CriticalCount => m_CriticalMessages;

        /// <summary>
        /// The number of error messages in the messages collection.
        /// </summary>
        public long ErrorCount => m_ErrorMessages;

        /// <summary>
        /// The number of warning messages in the messages collection.
        /// </summary>
        public long WarningCount => m_WarningMessages;

        /// <summary>
        /// The number of information messages in the messages collection.
        /// </summary>
        public long InformationCount => m_InformationalMessages;

        /// <summary>
        /// The number of verbose messages in the messages collection.
        /// </summary>
        public long VerboseCount => m_VerboseMessages;

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

        /// <inheritdoc />
        IEnumerator<LogMessage> IEnumerable<LogMessage>.GetEnumerator()
        {
            EnsureSorted();

            //we use the sorted list for enumeration
            return m_List.Values.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator<ILogMessage> IEnumerable<ILogMessage>.GetEnumerator()
        {
            EnsureSorted();

            //we use the sorted list for enumeration
            return m_List.Values.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            EnsureSorted();

            //we use the sorted list for enumeration
            return m_List.Values.GetEnumerator();
        }

        /// <inheritdoc />
        public int IndexOf(LogMessage item)
        {
            EnsureSorted();

            return m_List.Values.IndexOf(item);
        }

        /// <inheritdoc />
        public int IndexOf(ILogMessage item)
        {
            EnsureSorted();

            return m_List.Values.IndexOf((LogMessage)item);
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
        /// Not supported.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, ILogMessage item)
        {
            //we don't support setting an object by index; we are sorted.
            throw new NotSupportedException();
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        object IList.this[int index]
        {
            get
            {
                EnsureSorted();
                return m_List.Values[index];
            }
            set => throw
                //we don't want to support setting an object by index, we are sorted.
                new NotSupportedException();
        }

        /// <inheritdoc />
        public LogMessage this[int index]
        {
            get
            {
                EnsureSorted();
                return m_List.Values[index];
            }
            set => throw
                //we don't want to support setting an object by index, we are sorted.
                new NotSupportedException();
        }

        /// <inheritdoc />
        ILogMessage IList<ILogMessage>.this[int index]
        {
            get
            {
                EnsureSorted();
                return m_List.Values[index];
            }
            set => throw
                //we don't want to support setting an object by index, we are sorted.
                new NotSupportedException();
        }

        /// <inheritdoc />
        public int Add(object value)
        {
            LogMessage logMessage = (LogMessage)value; // throws an exception if it can't cast to the right type
            Add(logMessage);
            return m_List.Values.IndexOf(logMessage);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public int IndexOf(object value)
        {
            EnsureSorted();
            return m_List.Values.IndexOf((LogMessage)value);
        }

        /// <inheritdoc />
        public void Insert(int index, object value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public void Add(LogMessage item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A new LogMessage item must be provided to add it to the collection.");
            }

            //add it to our internal list
            m_List.Add(item.Sequence, item);

            switch (item.Severity)
            {
                case LogMessageSeverity.Critical:
                    m_CriticalMessages++;
                    break;
                case LogMessageSeverity.Error:
                    m_ErrorMessages++;
                    break;
                case LogMessageSeverity.Warning:
                    m_WarningMessages++;
                    break;
                case LogMessageSeverity.Information:
                    m_InformationalMessages++;
                    break;
                case LogMessageSeverity.Verbose:
                    m_VerboseMessages++;
                    break;
            }

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

            if (m_HasMultipleThreads == false)
            {
                m_HasMultipleThreads = m_Session.Threads.Count > 1;
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
                    m_HasMultipleLogSystems = (m_ReferenceLogSystem.Equals(item.LogSystem, StringComparison.OrdinalIgnoreCase) == false);
                }
            }

            //and fire our event
            OnCollectionChanged(new CollectionChangedEventArgs<LogMessageCollection, LogMessage>(this, item, CollectionAction.Added));
        }

        /// <inheritdoc />
        public void Add(ILogMessage item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "A new LogMessage item must be provided to add it to the collection.");
            }

            Add((LogMessage)item);
        }

        /// <summary>
        /// Indicates if any of the log messages in the collection have detailed xml data
        /// </summary>
        public bool HasDetail => m_HasDetails;

        /// <summary>
        /// Indicates if any of the log messages in the collection have exception information recorded
        /// </summary>
        public bool HasExceptionInfo => m_HasExceptionInfos;

        /// <summary>
        /// Indicates if there is more than one user associated with the log messages
        /// </summary>
        public bool HasMultipleUsers => m_HasMultipleUsers;

        /// <summary>
        /// Indicates if there is more than one log system associated with the log messages
        /// </summary>
        public bool HasMultipleLogSystems => m_HasMultipleLogSystems;

        /// <summary>
        /// Indicates if any of the log messages have source code location information
        /// </summary>
        public bool HasSourceLocation => m_HasSourceLocation;

        /// <inheritdoc />
        public bool Contains(LogMessage item)
        {
            //here we are relying on the fact that the LogMessage object implements IComparable sufficiently to guarantee uniqueness
            return m_List.Values.Contains(item);
        }

        /// <inheritdoc />
        public bool Contains(ILogMessage item)
        {
            //here we are relying on the fact that the LogMessage object implements IComparable sufficiently to guarantee uniqueness
            return m_List.Values.Contains((LogMessage)item);
        }

        /// <inheritdoc />
        public void CopyTo(LogMessage[] array, int arrayIndex)
        {
            EnsureSorted();
            m_List.Values.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public void CopyTo(ILogMessage[] array, int arrayIndex)
        {
            EnsureSorted();
            var messages = new LogMessage[m_List.Count];
            m_List.Values.CopyTo(messages, 0);

            //now put that in the provided array
            messages.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public void CopyTo(Array array, int index)
        {
            EnsureSorted();
            var messages = new LogMessage[m_List.Count];
            m_List.Values.CopyTo(messages, 0);

            //now put that in the provided array
            messages.CopyTo(array, index);
        }

        /// <inheritdoc />
        public int Count => m_List.Count;

        /// <inheritdoc />
        public object SyncRoot => m_Lock;

        /// <inheritdoc />
        public bool IsSynchronized => true;

        /// <summary>
        /// Indicates if the collection is read only and therefore can't have items added or removed.
        /// </summary>
        /// <remarks>This collection is always read-only.  
        /// This property is required for ICollection compatibility</remarks>
        public bool IsReadOnly =>
            //we are always read only
            true;

        /// <inheritdoc />
        public bool IsFixedSize => false;

        /// <summary>
        /// Not supported, but no exception is returned.
        /// </summary>
        /// <param name="item">The LogMessage item to remove.</param>
        public bool Remove(LogMessage item)
        {
            return false;
        }

        /// <summary>
        /// Not supported, but no exception is returned.
        /// </summary>
        /// <param name="item">The LogMessage item to remove.</param>
        public bool Remove(ILogMessage item)
        {
            return false;
        }

        #endregion

    }
}
