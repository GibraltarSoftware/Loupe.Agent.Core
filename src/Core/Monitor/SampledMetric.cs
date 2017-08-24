using System;
using Gibraltar.Monitor.Internal;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// The base class for creating sampled metrics
    /// </summary>
    /// <remarks>
    /// A sampled metric always has a value for any timestamp between its start and end timestamps.
    /// It presumes any interim value by looking at the best fit sampling of the real world value
    /// and assuming it covers the timestamp in question.  It is therefore said to be contiguous for 
    /// the range of start and end.  Event metrics are only defined at the instant they are timestamped, 
    /// and imply nothing for other timestamps.  
    /// For event based metrics, use the EventMetric base class.</remarks>
    public abstract class SampledMetric : Metric 
    {
        private readonly SampledMetricDefinition m_MetricDefinition;
        private readonly SampledMetricPacket m_Packet;

        /// <summary>
        /// Create a new sampled metric object from the provided raw data packet
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The object that defines this metric</param>
        /// <param name="packet">The raw data packet</param>
        internal SampledMetric(SampledMetricDefinition definition, SampledMetricPacket packet)
            : base(definition, packet)
        {
            m_MetricDefinition = definition;
            m_Packet = packet;
        }


        #region Public Properties and Methods

        /// <summary>
        /// The definition of this metric object.
        /// </summary>
        public new SampledMetricDefinition Definition { get { return (SampledMetricDefinition)base.Definition; } }

        #endregion

        #region Private Properties and Methods

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
            MetricValueCollection newMetricValueCollection;

            //we really have two different algorithms:  If the user specified shortest, then we are just going
            //to use every sample we have and hope for the best (provided they are in the time range)
            //Otherwise, we have a BIG FANCY ALGORYTHM.
            if (interval == MetricSampleInterval.Shortest)
            {
                newMetricValueCollection = OnCalculateValuesShortest(startDateTime, endDateTime);
            }
            else
            {
                //since they aren't using shortest, we are going to use the intervals input option which better not be zero or negative.
                if (intervals < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(intervals), intervals, "The number of intervals must be positive and greater than zero.");
                }

                newMetricValueCollection = OnCalculateValuesOnInterval(interval, intervals, startDateTime, endDateTime);
            }

            //we've ripped through the whole data set and now have our value set.  Return it to our caller
            return newMetricValueCollection;
        }

        /// <summary>
        /// Calculate the value set for the provided date range inclusive with samples exactly on the provided interval.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="interval">The interval to bias to.</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="startDateTime">The exact date and time desired to start the value set.</param>
        /// <param name="endDateTime">The exact end date and time to not exceed.</param>
        /// <returns>A new metric value set with all calculated values.</returns>
        private MetricValueCollection OnCalculateValuesOnInterval(MetricSampleInterval interval, int intervals, DateTimeOffset startDateTime, DateTimeOffset endDateTime)
        {
            MetricValueCollection newMetricValueCollection = new MetricValueCollection(this, interval, intervals, Definition.UnitCaption);

            //based on the requested interval, calculate the delta between each sample and the tolerance (how close we have to be
            //to the requested time to be pulled forward in time as the best sample)
            TimeSpan sampleTolerance = CalculateOffsetTolerance(interval);

            DateTimeOffset targetDateTime = startDateTime;
            DateTimeOffset targetToleranceDateTime = targetDateTime + sampleTolerance;

            SampledMetricSample baselineSample = null;
            double previousMetricValue = 0;
            int firstSampleIndex = 0;

            //First, we need to find the value for the start date & time and the sample index into the collection.
            for (int curSampleIndex = 0; curSampleIndex < base.Samples.Count; curSampleIndex++)
            {
                SampledMetricSample curSample = (SampledMetricSample) Samples[curSampleIndex];

                //is this the sample that exactly covers our target date & time?  It is if it's equal to our target, or 
                //it is a more exact fit than the next sample.
                if ((curSample.Timestamp == targetDateTime) ||
                    ((curSample.Timestamp < targetToleranceDateTime) && (Samples[curSampleIndex + 1].Timestamp > targetToleranceDateTime)))
                {
                    //yes, this sample is the best fit for our start date & time.  The next one is after our "pull forward" tolerance.
                    //but to get a value, we may have to back up one more interval from this sample to establish its value
                    if (curSample.RequiresMultipleSamples)
                    {
                        //back up as much as we can - as close to one full interval as possible so we get the most accurate initial value sample
                        DateTimeOffset baselineTargetDateTime = CalculateOffset(targetDateTime, interval, -intervals);

                        //start with the first sample before us... if there is one.
                        for (int baselineSampleIndex = curSampleIndex - 1; baselineSampleIndex >= 0; baselineSampleIndex--)
                        {
                            //keep walking back until we are before our target (or run out of choices)
                            baselineSample = (SampledMetricSample) Samples[curSampleIndex];
                            if (baselineSample.Timestamp <= baselineTargetDateTime)
                            {
                                //this is our best fit - it's the first that covers our baseline date time. 
                                break;
                            }
                        }
                    }

                    //we either got our baseline or we didn't - if we didn't and we need it we can't calculate value.
                    if ((baselineSample != null) || (curSample.RequiresMultipleSamples == false))
                    {
                        //Calculate the initial metric value sample and add it to the collection.
                        previousMetricValue =
                            CalculateSample(newMetricValueCollection, baselineSample, curSample, startDateTime);
                    }

                    //and this sample becomes our baseline into the next routine
                    baselineSample = curSample;
                    firstSampleIndex = curSampleIndex;
                    break;  //and we're done - we only wanted to get the start value.
                }
            }
            
            //Now that we've found our first sample, and the offset into the collection for the first sample, we can go on (provide there are more samples)
            firstSampleIndex++;   //because we used the current sample in the for loop above.
            if (firstSampleIndex < (Samples.Count - 1))
            {
                //we now want to look for the first sample after the start date.  If the user was silly and requested an end date that is less 
                //than one interval from the start date, we'll never get into our while loop this way.
                targetDateTime = CalculateOffset(targetDateTime, interval, intervals);
                targetToleranceDateTime = targetDateTime + sampleTolerance;

                int curSampleIndex = firstSampleIndex; //we start with the first sample after what was used above.
                SampledMetricSample curSample = null;

                //keep looping until we fill up the timespan or run out of samples.
                while ((targetDateTime <= endDateTime) && (curSampleIndex < (Samples.Count - 1)))
                {
                    //if we have no sample on deck, we must have used it in the last pass of the loop (or this is the first pass)
                    //so get it now.
                    if (curSample == null )
                    {
                        curSample = (SampledMetricSample)Samples[curSampleIndex];
                    }

                    //is this the sample that exactly covers our target date & time?  It is if it's equal to our target, or 
                    //it is a more exact fit than the next sample.
                    if ((curSample.Timestamp == targetDateTime) ||
                        ((curSample.Timestamp < targetToleranceDateTime) && (Samples[curSampleIndex + 1].Timestamp > targetToleranceDateTime)))
                    {
                        //yes, this sample is the best fit for our start date & time.  The next one is after our "pull forward" tolerance.
                        if ((baselineSample != null) || (curSample.RequiresMultipleSamples == false))
                        {
                            //Calculate the next metric value sample and add it to the collection.
                            previousMetricValue =
                                CalculateSample(newMetricValueCollection, baselineSample, curSample, targetDateTime);
                        }

                        //and this sample becomes our baseline into the next round
                        baselineSample = curSample;

                        //and we need a new current sample.
                        curSample = null;
                        curSampleIndex++;

                        //and now we have recorded a metric value for the requested date & time if possible, move that forward.
                        targetDateTime = CalculateOffset(targetDateTime, interval, intervals);
                        targetToleranceDateTime = targetDateTime + sampleTolerance;
                    }
                    else if (curSample.Timestamp < targetToleranceDateTime)
                    {
                        //this sample AND the next sample are both before target tolerance - we're going to skip this guy and record nothing
                        //(because we have more samples than we need for the interval we want)
                        curSample = null;
                        curSampleIndex++;
                    }
                    else
                    {
                        //the sample on deck doesn't apply yet - it's in the future.  We need to "invent" a sample for the current date and time.
                        //we'll just re-record the last metric value
                        new MetricValue(newMetricValueCollection, targetDateTime, previousMetricValue);

                        //and now we have definitely recorded a metric value for the requested date & time, move that forward.
                        targetDateTime = CalculateOffset(targetDateTime, interval, intervals);
                        targetToleranceDateTime = targetDateTime + sampleTolerance;
                    }
                }
            }

            return newMetricValueCollection;
        }

        /// <summary>
        /// Calculate a value set for the provided date range inclusive using all of the sample data, even if 
        /// that produces irregular sample intervals.
        /// </summary>
        /// <param name="startDateTime">The exact date and time desired to start the value set.</param>
        /// <param name="endDateTime">The exact end date and time to not exceed.</param>
        /// <returns>A new metric value set with all calculated values.</returns>
        private MetricValueCollection OnCalculateValuesShortest(DateTimeOffset startDateTime, DateTimeOffset endDateTime)
        {
            MetricValueCollection newMetricValueCollection = new MetricValueCollection(this, MetricSampleInterval.Shortest, 0, Definition.UnitCaption);

            //ohhhhkay, now we need to figure out what samples we want to include and not include.
            //we want to start by getting the sample one interval before the user wants to have values for so we can have a value exactly 
            //on that starting point
            SampledMetricSample baselineSample = null;
            SampledMetricSample previousBaselineSample = null; 

            foreach (SampledMetricSample curSample in base.Samples)
            {
                //is this sample in our range?
                if (curSample.Timestamp < startDateTime)
                {
                    //It isn't, but there are still reasons we'd be interested in it.  
                    //because the next one could be in the range and we'll want to mess with this one.
                    previousBaselineSample = baselineSample;
                    baselineSample = curSample;
                }
                else if (curSample.Timestamp > endDateTime)
                {
                    //no, BUT if it's the first sample after our range, and we didn't end EXACTLY 
                    //on the range then we want to include it, but we'll short it back to the end of the timeframe.
                    if ((baselineSample != null) && (baselineSample.Timestamp < endDateTime))
                    {
                        CalculateSample(newMetricValueCollection, baselineSample, curSample, endDateTime);
                    }

                    //and no matter what, we're done.  Either we got a special "Bracket" sample, or there isn't one.
                    break;
                }
                else
                {
                    //the sample is in the range - neither after nor before.  We definitely use this sample.

                    //but before we do this, is this the FIRST one in the range?
                    if ((baselineSample != null) && (baselineSample.Timestamp < startDateTime))
                    {
                        //yes it is - the baseline is before our start date, and we're after or equal.
                        //If it's not only the last before we get in but ALSO the first one in isn't EXACTLY on the line, we need to 
                        //create a fake interim sample, based on this guy and the one BEFORE him.
                        if (curSample.Timestamp > startDateTime)
                        {
                            //we have to insert a fake sample based on the baseline and the guy that came before him.
                            //but guard against us not having enough previous information to do this (this will happen more often than not)
                            if ((previousBaselineSample != null) || (baselineSample.RequiresMultipleSamples == false))
                            {
                                CalculateSample(newMetricValueCollection, previousBaselineSample, baselineSample, startDateTime);
                            }
                        }
                    }

                    //calculate our sample, however we have to guard against there being no baseline
                    // (because we're the first sample of a metric that requires multiple samples)
                    if ((baselineSample != null) || (curSample.RequiresMultipleSamples == false))
                    {
                        CalculateSample(newMetricValueCollection, baselineSample, curSample, null);
                    }

                    //and this sample becomes the new baseline
                    baselineSample = curSample;
                }
            }

            return newMetricValueCollection;
        }

        /// <summary>
        /// Calculate one effective value from the provided objects
        /// </summary>
        /// <param name="metricValueCollection">The value set to add the new value to</param>
        /// <param name="baselineSample">The baseline to calculate from.  Only used (and required) for metrics that require multiple samples.</param>
        /// <param name="valueSample">The value sample to perform the calculation with.</param>
        /// <param name="timeStampOverride">An optional override timestamp</param>
        private static double CalculateSample(MetricValueCollection metricValueCollection, SampledMetricSample baselineSample, SampledMetricSample valueSample, DateTimeOffset? timeStampOverride)
        {
            double calculatedValue;

            if (valueSample.RequiresMultipleSamples == false)
            {
                calculatedValue = valueSample.ComputeValue(); //we just need one 
            }
            else
            {
                calculatedValue = valueSample.ComputeValue(baselineSample);
            }

            DateTimeOffset timeStamp = ((timeStampOverride == null) ? valueSample.Timestamp : (DateTimeOffset)timeStampOverride);

            //now create & add the value to our values collection. 
            new MetricValue(metricValueCollection, timeStamp, calculatedValue);

            return calculatedValue;
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The underlying packet 
        /// </summary>
        internal new SampledMetricPacket Packet { get { return (SampledMetricPacket)base.Packet; } }

        #endregion

    }
}
