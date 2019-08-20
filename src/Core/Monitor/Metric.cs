
using System;
using System.Diagnostics;
using System.Threading;
using Loupe.Monitor.Serialization;
using Loupe.Extensibility.Data;



namespace Loupe.Monitor
{
    /// <summary>
    /// A single metric that has been captured.  A metric is a single measured value over time.  
    /// </summary>
    /// <remarks>
    /// To display the data captured for this metric, use Calculate Values to translate the raw captured data
    /// into displayable information.
    /// </remarks>
    [DebuggerDisplay("Name: {Name}, Id: {Id}, Caption: {Caption}")]
    public abstract class Metric : IMetric, IDisplayable
    {
        private readonly MetricDefinition m_MetricDefinition;
        private readonly MetricPacket m_Packet;
        private readonly MetricSampleCollection m_Samples;

        //these variables are not persisted but just used to manage our own state.
        private long m_SampleSequence = 0; //used when adding sampled metrics to ensure order.

        /// <summary>
        /// Create a new metric with the provided metric definition and metric packet.
        /// </summary>
        /// <remarks>Most derived classes will provide a more convenient implementation that will automatically
        /// create the correct metric packet instead of the caller having to first create it.  
        /// The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The definition for this metric</param>
        /// <param name="packet">The metric packet to use for this metric</param>
        internal Metric(MetricDefinition definition, MetricPacket packet)
        {
            //verify and store off our input
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            //one last safety check:  The definition and the packet better agree.
            if (definition.Id != packet.DefinitionId)
            {
                throw new ArgumentOutOfRangeException(nameof(packet), "The provided metric packet has a different definition Id than the provide metric definition.");
            }

            //and now that we know everything isn't null, go ahead and store things off
            m_MetricDefinition = definition;
            m_Packet = packet;

            //and force our inheritor to create the samples collection so we get the right derived class.
            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            m_Samples = OnSampleCollectionCreate();
            // ReSharper restore DoNotCallOverridableMethodsInConstructor

            //finally, add ourself to the metric definition's metrics collection
            m_MetricDefinition.Metrics.Add(this);
        }

        #region Public Properties and Methods

        /// <summary>
        /// The unique Id of this metric instance.  This can reliably be used as a key to refer to this item.
        /// </summary>
        /// <remarks>The key can be used to compare the same metric across different instances (e.g. sessions).
        /// This Id is always unique to a particular instance.</remarks>
        public Guid Id { get { return m_Packet.ID; } }

        /// <summary>
        /// The fully qualified name of the metric being captured.  
        /// </summary>
        /// <remarks>The name is for comparing the same metric in different sessions. They will have the same name but 
        /// not the same Id.</remarks>
        public string Name { get { return m_Packet.Name; } }

        /// <summary>
        /// A short caption of what the metric tracks, suitable for end-user display.
        /// </summary>
        public string Caption
        {
            get { return m_Packet.Caption; }
        }

        /// <summary>
        /// A description of what is tracked by this metric, suitable for end-user display.
        /// </summary>
        public string Description
        {
            get { return m_Packet.Description; }
        }

        /// <summary>
        /// The definition of this metric object.
        /// </summary>
        public IMetricDefinition Definition
        {
            get
            {
                return m_MetricDefinition;
            }   
        }

        /// <summary>
        /// The internal metric type of this metric definition
        /// </summary>
        public string MetricTypeName
        {
            get { return Definition.MetricTypeName;  }    
        }

        /// <summary>
        /// The category of this metric for display purposes.  Category is the top displayed hierarchy.
        /// </summary>
        public string CategoryName
        {
            get { return Definition.CategoryName; }
        }

        /// <summary>
        /// The display name of this metric (unique within the category name).
        /// </summary>
        public string CounterName
        {
            get { return Definition.CounterName; }
        }

        /// <summary>
        /// Gets or sets an instance name for this performance counter.
        /// </summary>
        public string InstanceName
        {
            get { return m_Packet.InstanceName; }
        }

        /// <summary>
        /// Indicates whether this is the default metric instance for this metric definition or not.
        /// </summary>
        /// <remarks>The default instance has a null instance name.  This property is provided as a convenience to simplify
        /// client code so you don't have to distinguish empty strings or null.</remarks>
        public bool IsDefault
        {
            get { return (string.IsNullOrEmpty(m_Packet.InstanceName)); }
        }

        /// <summary>
        /// The earliest start date and time of the raw data samples.
        /// </summary>
        public DateTimeOffset StartDateTime 
        { 
            get 
            { 
                //if we have no samples, we need to throw a better exception that what we're about to get.
                if (m_Samples.Count == 0)
                {
                    throw new InvalidOperationException("there are no samples recorded for this metric so the start date is not available.");
                }

                return m_Samples.First.Timestamp; 
            }
        }

        /// <summary>
        /// The last date and time of the raw data samples.
        /// </summary>
        public DateTimeOffset EndDateTime 
        { 
            get 
            {
                //if we have no samples, we need to throw a better exception that what we're about to get.
                if (m_Samples.Count == 0)
                {
                    throw new InvalidOperationException("there are no samples recorded for this metric so the start date is not available.");
                }

                return m_Samples.Last.Timestamp;
            } 
        }

        /// <summary>
        /// The sample type of the metric.  Indicates whether the metric represents discrete events or a continuous value.
        /// </summary>
        public SampleType SampleType
        {
            get { return Definition.SampleType; }
        }

        #region IComparable and IEquatable methods

        /// <summary>
        /// Compare this Metric to another Metric to determine sort order
        /// </summary>
        /// <remarks>Metric instances are sorted by their Name property.</remarks>
        /// <param name="other">The Metric object to compare this Metric object against</param>
        /// <returns>An int which is less than zero, equal to zero, or greater than zero to reflect whether
        /// this Metric should sort as being less-than, equal to, or greater-than the other Metric, respectively.</returns>
        public int CompareTo(Metric other)
        {
            //quick identity comparison based on guid
            if (m_Packet.ID == other.Id)
            {
                return 0;
            }

            //Now we try to sort by name.  We already guard against uniqueness
            int compareResult = string.Compare(m_Packet.Name, other.Name, StringComparison.OrdinalIgnoreCase);

            return compareResult;
        }

        /// <summary>
        /// Determines if the provided Metric object is identical to this object.
        /// </summary>
        /// <param name="other">The Metric object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(Metric other)
        {
            if (ReferenceEquals(other, this))
            {
                return true; // ReferenceEquals means we're the same object, definitely equal.
            }

            // Careful, it could be null; check it without recursion
            if (ReferenceEquals(other, null))
            {
                return false; // Since we're a live object we can't be equal to a null instance.
            }

            //they are the same if their Guid's match.
            return (Id == other.Id);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a Metric and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            Metric otherMetric = obj as Metric;

            return Equals(otherMetric); // Just have type-specific Equals do the check (it even handles null)
        }

        /// <summary>
        /// Provides a representative hash code for objects of this type to spread out distribution
        /// in hash tables.
        /// </summary>
        /// <remarks>Objects which consider themselves to be Equal (a.Equals(b) returns true) are
        /// expected to have the same hash code.  Objects which are not Equal may have the same
        /// hash code, but minimizing such overlaps helps with efficient operation of hash tables.
        /// </remarks>
        /// <returns>
        /// An int representing the hash code calculated for the contents of this object.
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = Id.GetHashCode(); // The ID is all that Equals checks!

            return myHash;
        }

        /// <summary>
        /// Compares two Metric instances for equality.
        /// </summary>
        /// <param name="left">The Metric to the left of the operator</param>
        /// <param name="right">The Metric to the right of the operator</param>
        /// <returns>True if the two Metrics are equal.</returns>
        public static bool operator ==(Metric left, Metric right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two Metric instances for inequality.
        /// </summary>
        /// <param name="left">The Metric to the left of the operator</param>
        /// <param name="right">The Metric to the right of the operator</param>
        /// <returns>True if the two Metrics are not equal.</returns>
        public static bool operator !=(Metric left, Metric right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ! ReferenceEquals(right, null);
            }
            return ! left.Equals(right);
        }

        /// <summary>
        /// Compares if one Metric instance should sort less than another.
        /// </summary>
        /// <param name="left">The Metric to the left of the operator</param>
        /// <param name="right">The Metric to the right of the operator</param>
        /// <returns>True if the Metric to the left should sort less than the Metric to the right.</returns>
        public static bool operator <(Metric left, Metric right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one Metric instance should sort greater than another.
        /// </summary>
        /// <param name="left">The Metric to the left of the operator</param>
        /// <param name="right">The Metric to the right of the operator</param>
        /// <returns>True if the Metric to the left should sort greater than the Metric to the right.</returns>
        public static bool operator >(Metric left, Metric right)
        {
            return (left.CompareTo(right) > 0);
        }

        #endregion

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
        public DateTimeOffset CalculateOffset(DateTimeOffset baseline, MetricSampleInterval interval, int intervals)
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
                    if (Definition.Interval != MetricSampleInterval.Default)
                    {
                        returnVal = CalculateOffset(baseline, Definition.Interval, intervalCount);
                    }
                    else
                    {
                        //default and ours is default - use second.
                        returnVal = CalculateOffset(baseline, MetricSampleInterval.Second, intervalCount);
                    }
                    break;
                case MetricSampleInterval.Shortest:
                    //explicitly use the shortest value available, 16 milliseconds
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

        /// <summary>
        /// Calculates the amount we will "pull forward" a future sample by to fit it to our requested interval.
        /// </summary>
        /// <remarks>Tolerance allows for us to ignore small variations in exact timestamps for the purposes of fitting the best data.</remarks>
        /// <param name="interval"></param>
        /// <returns></returns>
        public static TimeSpan CalculateOffsetTolerance(MetricSampleInterval interval)
        {
            TimeSpan returnVal;

            switch (interval)
            {
                case MetricSampleInterval.Default:
                case MetricSampleInterval.Shortest:
                case MetricSampleInterval.Millisecond:
                    //same as millisecond; we will use 1 clock tick
                    returnVal = new TimeSpan(1);
                    break;
                case MetricSampleInterval.Second:
                    //10 milliseconds
                    returnVal = new TimeSpan(0, 0, 0, 0, 10);
                    break;
                case MetricSampleInterval.Minute:
                    //2 seconds
                    returnVal = new TimeSpan(0, 0, 0, 2);
                    break;
                case MetricSampleInterval.Hour:
                    //1 minute
                    returnVal = new TimeSpan(0, 1, 0);
                    break;
                case MetricSampleInterval.Day:
                    //30 minutes
                    returnVal = new TimeSpan(0, 30, 0);
                    break;
                case MetricSampleInterval.Week:
                    //12 hours 
                    returnVal = new TimeSpan(12, 0, 0);
                    break;
                case MetricSampleInterval.Month:
                    //two days
                    returnVal = new TimeSpan(2, 0, 0, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(interval));
            }

            return returnVal;
        }

        /// <summary>
        /// Calculate displayable values based on the full information captured for this metric, 
        /// returning all dates available at the default interval.
        /// </summary>
        /// <remarks>
        /// The raw values may not be suitable for display depending on the unit the values are captured in, and
        /// depending on how the data was sampled it may not display well because of uneven sampling if processed
        /// directly.
        /// </remarks>
        /// <returns>A metric value set suitable for display</returns>
        public IMetricValueCollection CalculateValues()
        {
            //handle the special case where there are no samples so we can't figure out start & end.
            if (m_Samples.Count == 0)
            {
                return CalculateValues(MetricSampleInterval.Default, 1, null, null);
            }

            //forward to our grander overload
            return CalculateValues(MetricSampleInterval.Default, 1, this.StartDateTime, this.EndDateTime);
        }

        /// <summary>
        /// Calculate displayable values based on the full information captured for this metric with the specified interval 
        /// for all dates available
        /// </summary>
        /// <remarks>
        /// The raw values may not be suitable for display depending on the unit the values are captured in, and
        /// depending on how the data was sampled it may not display well because of uneven sampling if processed
        /// directly.
        /// </remarks>
        /// <param name="interval">The requested data sample size</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <returns>A metric value set suitable for display</returns>
        public IMetricValueCollection CalculateValues(MetricSampleInterval interval, int intervals)
        {
            //handle the special case where there are no samples so we can't figure out start & end.
            if (m_Samples.Count == 0)
            {
                return CalculateValues(interval, intervals, null, null);
            }

            //forward to our grander overload
            return CalculateValues(interval, intervals, this.StartDateTime, this.EndDateTime);
        }

        /// <summary>
        /// Calculate displayable values based on the full information captured for this metric
        /// </summary>
        /// <remarks>
        /// The raw values may not be suitable for display depending on the unit the values are captured in, and
        /// depending on how the data was sampled it may not display well because of uneven sampling if processed
        /// directly.
        /// </remarks>
        /// <param name="interval">The requested data sample size</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="startDateTime">The earliest date to retrieve data for</param>
        /// <param name="endDateTime">The last date to retrieve data for</param>
        /// <returns>A metric value set suitable for display</returns>
        public IMetricValueCollection CalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset? startDateTime, DateTimeOffset? endDateTime)
        {
            //we want to substitute in defaults, but be wary of what happens when we don't have any samples...
            DateTimeOffset effectiveStartDateTime = (startDateTime.HasValue ? (DateTimeOffset)startDateTime 
                : (m_Samples.Count > 0) ? StartDateTime : DateTimeOffset.MinValue );
            DateTimeOffset effectiveEndDateTime = (endDateTime.HasValue ? (DateTimeOffset)endDateTime
                : (m_Samples.Count > 0) ? EndDateTime : DateTimeOffset.MinValue);
            
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


        /// <summary>
        /// The set of raw samples for this metric
        /// </summary>
        public IMetricSampleCollection Samples { get { return m_Samples; } }

        #endregion

        #region Internal Properties and Methods
   
        /// <summary>
        /// The underlying packet 
        /// </summary>
        internal MetricPacket Packet { get { return m_Packet; } }

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

        /// <summary>
        /// Invoked when deserializing a metric sample to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for metric samples in your derived metric, use this
        /// method to create and return your derived object to support the deserialization process.
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <param name="packet">The metric sample packet being deserialized</param>
        /// <returns>The metric sample-compatible object.</returns>
        protected abstract MetricSample OnMetricSampleRead(MetricSamplePacket packet);

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Invoked by the base class to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for metric sample collection in your derived metric, use this
        /// method to create and return your derived object. 
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <returns>The MetricSampleCollection-compatible object.</returns>
        protected abstract MetricSampleCollection OnSampleCollectionCreate();

        /// <summary>
        /// Determines what specific samples to use and then calculates the effective values for each sample, returning the results in the provided
        /// new metric value set
        /// </summary>
        /// <remarks><para>Data covering the provided start and end date will be returned when possible with the goal being that the first metric value returned
        /// will coincide with the start date, and the last sample will be less than or equal to the end date.  Even if there are raw samples available coincident
        /// with the start date it may not be possible to provide a metric value for that date due to the need to have multiple samples to calculate most
        /// metrics.</para>
        /// <para>When there are no samples available an empty collection should be returned.  In this case the start and end date may be outside the range of the session.</para></remarks>
        /// <param name="interval">The interval to bias to.</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="startDateTime">The exact date and time desired to start the value set.</param>
        /// <param name="endDateTime">The exact end date and time to not exceed.</param>
        /// <returns>A new metric value set with all calculated values.</returns>
        protected abstract MetricValueCollection OnCalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset startDateTime,
                                                 DateTimeOffset endDateTime);




        #endregion
    }
}
