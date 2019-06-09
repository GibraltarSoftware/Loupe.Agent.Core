
using System;
using System.Collections.Generic;
using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility.Data;




namespace Gibraltar.Monitor
{
    /// <summary>
    /// A single event metric instance object, representing one instance of an event metric definition.
    /// </summary>
    public class EventMetric : Metric, IEventMetric
    {
        private readonly EventMetricSampleCollection m_Samples;
        private readonly EventMetricDefinition m_MetricDefinition;
        private readonly EventMetricPacket m_Packet;

        /// <summary>Creates a new event metric object from the metric definition looked up with the provided key information.</summary>
        /// <remarks>The metric definition must already exist or an exception will be raised.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        public EventMetric(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName, string instanceName)
            : this((EventMetricDefinition)definitions[MetricDefinition.GetKey(metricTypeName, categoryName, counterName)], instanceName)
        {
        }

        /// <summary>
        /// Create a new event metric object from the provided metric definition
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The metric definition for the metric instance</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        public EventMetric(EventMetricDefinition definition, string instanceName)
            : this(definition, new EventMetricPacket(definition.Packet, instanceName))
        {
        }

        /// <summary>
        /// Create a new event metric object from the provided raw data packet
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The object that defines this metric</param>
        /// <param name="packet">The raw data packet</param>
        internal EventMetric(EventMetricDefinition definition, EventMetricPacket packet)
            : base(definition, packet)
        {
            // We created an EventMetricSampleCollection when base constructor called our OnSampleCollectionCreate().
            m_Samples = (EventMetricSampleCollection) base.Samples;
            m_MetricDefinition = definition;
            m_Packet = packet;
        }

        #region Public Properties and Methods

        /// <summary>Creates a new metric instance or returns an existing one from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not an Event Metric (or a derived class) an exception will be thrown.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        /// <returns>The event metric object for the specified event metric instance.</returns>
        public static EventMetric AddOrGet(MetricDefinitionCollection definitions, string metricTypeName, string categoryName, string counterName, string instanceName)
        {
            //we must have a definitions collection, or we have a problem
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            //we need to find the definition, adding it if necessary
            string definitionKey = MetricDefinition.GetKey(metricTypeName, categoryName, counterName);
            IMetricDefinition definition;

            if (definitions.TryGetValue(definitionKey, out definition))
            {
                //if the metric definition exists, but is of the wrong type we have a problem.
                if ((definition is EventMetricDefinition) == false)
                {
                    throw new ArgumentException("A metric already exists with the provided type, category, and counter name but it is not compatible with being an event metric.  Please use a different counter name.", nameof(counterName));
                }
            }
            else
            {
                //we didn't find one, make a new one
                definition = new EventMetricDefinition(definitions, metricTypeName, categoryName, counterName);
                definitions.Add(definition); // Add it to the collection, no longer done in the constructor.
                // ToDo: Reconsider this implementation; putting incomplete event metric definitions in the collection is not ideal,
                // and creating a metric from an empty event metric definition is fairly pointless.
            }

            //now we have our definition, proceed to create a new metric if it doesn't exist
            string metricKey = MetricDefinition.GetKey(metricTypeName, categoryName, counterName, instanceName);
            IMetric metric;

            //see if we can get the metric already.  If not, we'll create it
            lock (((MetricCollection)definition.Metrics).Lock) //make sure the get & add are atomic
            {
                if (definition.Metrics.TryGetValue(metricKey, out metric) == false)
                {
                    metric = new EventMetric((EventMetricDefinition)definition, instanceName);
                }
            }

            return (EventMetric)metric;
        }

        /// <summary>Creates a new metric instance or returns an existing one from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not an Event Metric (or a derived class) an exception will be thrown.
        /// Definitions are looked up and added to the active logging metrics collection (Log.Metrics)</remarks>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        /// <returns>The event metric object for the specified event metric instance.</returns>
        public static EventMetric AddOrGet(string metricTypeName, string categoryName, string counterName, string instanceName)
        {
            //just forward into our call that requires the definition to be specified
            return AddOrGet(Log.Metrics, metricTypeName, categoryName, counterName, instanceName);
        }


        /// <summary>Creates a new metric instance or returns an existing one from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not an Event Metric (or a derived class) an exception will be thrown.
        /// Definitions are looked up and added to the active logging metrics collection (Log.Metrics)</remarks>
        /// <param name="definition">The metric definition for the metric instance</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        /// <returns>The event metric object for the specified event metric instance.</returns>
        public static EventMetric AddOrGet(EventMetricDefinition definition, string instanceName)
        {
            //just forward into our call that requires the definition to be specified
            return AddOrGet(Log.Metrics, definition.MetricTypeName, definition.CategoryName, definition.CounterName, instanceName);
        }

        /// <summary>
        /// Create a new metric sample.  The caller must write this sample for it to be recorded.
        /// </summary>
        /// <remarks>To write this sample out to the log, use Log.Write.  If you are sampling multiple metrics at the same time,
        /// it is faster to create each of the samples and write them with one call to Log.Write instead of writing them out
        /// individually.</remarks>
        /// <returns>The new metric sample packet object</returns>
        public EventMetricSample CreateSample()
        {
            return new EventMetricSample(this, new EventMetricSamplePacket(this));
        }

        /// <summary>
        /// Indicates the relative sort order of this object to another of the same type.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(EventMetric other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.CompareTo(other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetric other)
        {
            //We're really just a type cast, refer to our base object
            return base.Equals(other);
        }


        /// <summary>
        /// The definition of this metric object.
        /// </summary>
        public new EventMetricDefinition Definition { get { return m_MetricDefinition; } }

        /// <summary>
        /// The set of raw samples for this metric
        /// </summary>
        public new EventMetricSampleCollection Samples { get { return m_Samples; } }

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
        /// <param name="trendValue">The specific event metric value to trend</param>
        /// <returns>A metric value set suitable for display</returns>
        public IMetricValueCollection CalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset? startDateTime, DateTimeOffset? endDateTime, IEventMetricValueDefinition trendValue)
        {
            DateTimeOffset effectiveStartDateTime = (startDateTime == null ? this.StartDateTime : (DateTimeOffset)startDateTime);
            DateTimeOffset effectiveEndDateTime = (endDateTime == null ? this.EndDateTime : (DateTimeOffset)endDateTime);

            return OnCalculateValues(interval, intervals, effectiveStartDateTime, effectiveEndDateTime, trendValue);
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The underlying packet 
        /// </summary>
        internal new EventMetricPacket Packet { get { return m_Packet; } }

        #endregion

        #region Base Object Overrides

        /// <summary>
        /// Invoked when deserializing a metric sample to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for metric samples in your derived metric, use this
        /// method to create and return your derived object to support the deserialization process.
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <param name="packet">The metric sample packet being deserialized</param>
        /// <returns>The metric sample-compatible object.</returns>
        protected override MetricSample OnMetricSampleRead(MetricSamplePacket packet)
        {
            //create a custom sampled metric sample object
            return new EventMetricSample(this, (EventMetricSamplePacket)packet);
        }

        /// <summary>
        /// Invoked by the base class to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for metric sample collection in your derived metric, use this
        /// method to create and return your derived object. 
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <returns>The MetricSampleCollection-compatible object.</returns>
        protected override MetricSampleCollection OnSampleCollectionCreate()
        {
            return new EventMetricSampleCollection(this);

        }


        /// <summary>
        /// Determines what specific samples to use and then calculates the effective values for each sample, returning the results in the provided
        /// new metric value set
        /// </summary>
        /// <remarks>Data covering the provided start and end date will be returned when possible with the goal being that the first metric value returned
        /// will coincide with the start date, and the last sample will be less than or equal to the end date.  Even if there are raw samples available coincident
        /// with the start date it may not be possible to provide a metric value for that date due to the need to have multiple samples to calculate most
        /// metrics.</remarks>
        /// <param name="interval">The interval to bias to.</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="startDateTime">The exact date and time desired to start the value set.</param>
        /// <param name="endDateTime">The exact end date and time to not exceed.</param>
        /// <returns>A new metric value set with all calculated values.</returns>
        protected override MetricValueCollection OnCalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset startDateTime, DateTimeOffset endDateTime)
        {
            //forward the call to our somewhat more elaborate common routine, substituting the default trend.
            return OnCalculateValues(interval, intervals, startDateTime, endDateTime, Definition.DefaultValue);
        }


        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Calculate one effective value from the provided objects
        /// </summary>
        /// <param name="metricValueCollection">Optional.  The value set to add the new value to</param>
        /// <param name="samples">The set of samples to put in this value</param>
        /// <param name="previousValue">The value of the prior sample in time.</param>
        /// <param name="previousCount">The number of event metric samples previously used (for running average)</param>
        /// <param name="timeStamp">The timestamp to use for the sample</param>
        /// <param name="trendValue">The specific event metric value to trend</param>
        internal double CalculateSample(MetricValueCollection metricValueCollection, IList<EventMetricSample> samples, double previousValue, int previousCount, DateTimeOffset timeStamp, EventMetricValueDefinition trendValue)
        {
            double calculatedValue;

            //There are three totally different routes we go:  Either we are calculating an explicit trend value, 
            //or we are just going to count the # of events.  This varies depending on whether the caller specified
            //a trend value
            if (trendValue == null)
            {
                //just count them
                calculatedValue = samples.Count;
            }
            //There are two overall ways we trend things:  Either we count the # of non-null values for non-trendables, or
            //we actually make a numeric trend            
            else if ((trendValue.IsTrendable == false) || (trendValue.DefaultTrend == EventMetricValueTrend.Count))
            {
                //count the number of things.
                int itemCount = 0;

                if (samples.Count > 0)
                {
                    int valueIndex = Definition.Values.IndexOf(trendValue); //the zero-based index in the value collection of the value we want

                    //Now count all of the samples with a non-null value, including us (inclusive)
                    foreach (EventMetricSample sample in samples)
                    {
                        if (sample.Values[valueIndex] != null)
                        {
                            itemCount++;
                        }
                    }
                }

                calculatedValue = itemCount;
            }
            else
            {
                //ohh kay, now we either sum or average.
                double sumValue = 0; //sum or average, we need the sum.
                int items = samples.Count; //for an average we will need the item count.
                int valueIndex = Definition.Values.IndexOf(trendValue);     //the zero-based index in the value collection of the value we want

                //if it's a running value we need to start with the previous sample's state
                if ((trendValue.DefaultTrend == EventMetricValueTrend.RunningAverage) 
                    || (trendValue.DefaultTrend == EventMetricValueTrend.RunningSum))
                {
                    //we need the previous running data.
                    sumValue = previousValue;
                    items += previousCount;
                }

                //Now add up the effective value of the requested trend line for every sample in the range, inclusive
                foreach(EventMetricSample currentSample in samples)
                {
                    sumValue += currentSample.GetEffectiveValue(valueIndex);
                }

                //figure out the calculated value.  For performance and safety, don't do a divide we don't have to
                if ((items > 1)
                    && ((trendValue.DefaultTrend == EventMetricValueTrend.Average)
                    || (trendValue.DefaultTrend == EventMetricValueTrend.RunningAverage)))
                {
                    calculatedValue = sumValue / items;
                }
                else
                {
                    calculatedValue = sumValue;
                }
            }

            //now create & add the value to our values collection. 
            if (metricValueCollection != null)
            {
                new MetricValue(metricValueCollection, timeStamp, calculatedValue);
            }

            return calculatedValue;
        }

        private MetricValueCollection OnCalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset startDateTime, DateTimeOffset endDateTime, IEventMetricValueDefinition trendValue)
        {
            MetricValueCollection newMetricValueCollection;

            //we really have two different algorithms:  If the user specified shortest, then we are just going
            //to use every sample we have and hope for the best (provided they are in the time range)
            //Otherwise, we have a BIG FANCY ALGORYTHM.
            if (interval == MetricSampleInterval.Shortest)
            {
                newMetricValueCollection = OnCalculateValuesShortest(startDateTime, endDateTime, trendValue);
            }
            else
            {
                //since they aren't using shortest, we are going to use the intervals input option which better not be zero or negative.
                if (intervals < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(intervals), intervals, "The number of intervals must be positive and greater than zero.");
                }

                //forward to our big boy method.
                newMetricValueCollection = OnCalculateValuesInterval(interval, intervals, startDateTime, endDateTime, trendValue);
            }

            //we've ripped through the whole data set and now have our value set.  Return it to our caller
            return newMetricValueCollection;
        }



        private MetricValueCollection OnCalculateValuesShortest(DateTimeOffset startDateTime, DateTimeOffset endDateTime, IEventMetricValueDefinition trendValue)
        {
            //there are two possibilities for unit caption:  Either use the caption from the trend definition or, if that's null, we'll just do Count.
            string unitCaption = null;
            if (trendValue != null)
            {
                unitCaption = trendValue.UnitCaption;
            }

            if (string.IsNullOrEmpty(unitCaption))
            {
                unitCaption = "Events";
            }

            MetricValueCollection newMetricValueCollection;
            if (trendValue == null)
            {
                //initialize with our metric object for default caption/description behavior
                newMetricValueCollection = new MetricValueCollection(this, MetricSampleInterval.Shortest, 0, unitCaption);
            }
            else
            {
                //initialize with our trend for trend-specific captions and descriptions
                newMetricValueCollection = new MetricValueCollection((EventMetricValueDefinition)trendValue, MetricSampleInterval.Shortest, 0, unitCaption);
            }

            double lastValue = 0.0;

            //First, we need to find the value for the start date & time and the sample index into the collection.
            for (int curSampleIndex = 0; curSampleIndex < Samples.Count; curSampleIndex++)
            {
                //get the sample on deck
                EventMetricSample curSample = Samples[curSampleIndex];

                //Is this sample in our range?
                if (curSample.Timestamp >= startDateTime)
                {
                    //Go ahead and calculate the value, adding it to our set.  We don't use a baseline because we're looking at each individual value.
                    CalculateSample(newMetricValueCollection, new List<EventMetricSample>(new[] {curSample}), lastValue, curSampleIndex, curSample.Timestamp, (EventMetricValueDefinition)trendValue);
                }
                else if (curSample.Timestamp > endDateTime)
                {
                    //we're done - there are no more samples to consider, break out of the for loop
                    break;
                }
            }

            return newMetricValueCollection;
        }

        private MetricValueCollection OnCalculateValuesInterval(MetricSampleInterval interval, int intervals, DateTimeOffset startDateTime, DateTimeOffset endDateTime, IEventMetricValueDefinition trendValue)
        {
            //there are two possibilities for unit caption:  Either use the caption from the trend definition or, if that's null, we'll just do Count.
            string unitCaption = null;
            if (trendValue != null)
            {
                unitCaption = trendValue.UnitCaption;
            }

            if (string.IsNullOrEmpty(unitCaption))
            {
                unitCaption = "Events";
            }

            MetricValueCollection newMetricValueCollection;
            if (trendValue == null)
            {
                //initialize with our metric object for default caption/description behavior
                newMetricValueCollection = new MetricValueCollection(this, interval, intervals, unitCaption);
            }
            else
            {
                //initialize with our trend for trend-specific captions and descriptions
                newMetricValueCollection = new MetricValueCollection((EventMetricValueDefinition)trendValue, interval, intervals, unitCaption);
            }

            //And prep for our process loop
            DateTimeOffset windowStartDateTime = startDateTime;
            DateTimeOffset windowEndDateTime = CalculateOffset(windowStartDateTime, interval, intervals);
            double lastValue = 0.0;

            //now loop through all of the intervals in the range to create the sample ranges.
            int lastUsedSampleIndex = 0;
            while (windowStartDateTime < endDateTime)
            {
                //find all of the samples in the current range...
                List<EventMetricSample> samples = new List<EventMetricSample>();
                for (int curSampleIndex = lastUsedSampleIndex; curSampleIndex < Samples.Count; curSampleIndex++)
                {
                    //get the sample on deck
                    EventMetricSample curSample = Samples[curSampleIndex];

                    //if we're outside of the window (before or after) then break out because we've found this whole bucket.
                    if ((curSample.Timestamp < windowStartDateTime) || (curSample.Timestamp > windowEndDateTime))
                    {
                        //we'll need to look at this sample in the next window to see if it fits.
                        lastUsedSampleIndex = curSampleIndex;
                        break;
                    }

                    //this sample is in the bucket 
                    samples.Add(curSample);
                }

                //add a metric value for this bucket now that we know everything in it.
                lastValue = CalculateSample(newMetricValueCollection, samples, lastValue, lastUsedSampleIndex, windowEndDateTime, (EventMetricValueDefinition)trendValue);

                //and now we have recorded a metric value for the requested date & time if possible, move that forward.
                windowStartDateTime = windowEndDateTime; //we're moving on to the next window
                windowEndDateTime = CalculateOffset(windowEndDateTime, interval, intervals);

                //if the target is beyond our end date & time, set to the end date and time so we calculate our last sample
                if (windowEndDateTime > endDateTime)
                {
                    windowEndDateTime = endDateTime;
                }
            }

            return newMetricValueCollection;
        }

        #endregion
    }
}
