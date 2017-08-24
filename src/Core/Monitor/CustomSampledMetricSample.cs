
using System;
using System.Globalization;
using Gibraltar.Monitor.Internal;



namespace Gibraltar.Monitor
{
    /// <summary>
    /// A single sample of a custom sampled metric.  Sampled metrics generally require calculation to produce data, see ComputeValue.
    /// </summary>
    public sealed class CustomSampledMetricSample : SampledMetricSample, IComparable<CustomSampledMetricSample>, IEquatable<CustomSampledMetricSample>
    {
        private double? m_Value; //We calculate this on first reference, then cache the result because it can take time.
        private double? m_BaseValue; //We calculate this on first reference, then cache the result because it can take time.

        /// <summary>
        /// Create a new sample object for the provided metric and raw sample packet.
        /// </summary>
        /// <remarks>The metric sample is automatically added to the samples collection of the provided metric object.</remarks>
        /// <param name="metric">The metric object this sample applies to.</param>
        /// <param name="metricSamplePacket">The raw sample data packet.</param>
        internal CustomSampledMetricSample(CustomSampledMetric metric, CustomSampledMetricSamplePacket metricSamplePacket)
            : base(metric, metricSamplePacket, (metric.Definition).RequiresMultipleSamples)
        {

        }

        #region Public Properties and Methods

        /// <summary>
        /// Compute the counter value for this sample compared with the provided baseline sample (if any)
        /// </summary>
        /// <remarks>
        /// A baseline sample is required when the current metric requires multiple samples to determine results.
        /// The baseline sample must be for a date and time prior to this sample for correct results.
        /// </remarks>
        /// <param name="baselineSample">The previous baseline sample to calculate a difference for</param>
        /// <returns>The calculated counter value</returns>
        public override double ComputeValue(SampledMetricSample baselineSample)
        {
            if ((baselineSample == null) && (RequiresMultipleSamples))
            {
                throw new ArgumentNullException(nameof(baselineSample), "A baseline metric sample is required and none was provided.");
            }

            if ((baselineSample != null) && (baselineSample.Timestamp > Timestamp))
            {
                throw new ArgumentOutOfRangeException(nameof(baselineSample), baselineSample.Timestamp, "The baseline sample must be for a date & time before this sample to be valid for comparison.");
            }

            //Now lets do some math!  The math we have to do depends on the sampled metric type.
            MetricSampleType metricType = Metric.Definition.MetricSampleType;

            //First, eliminate the values that don't need math at all
            if (RequiresMultipleSamples == false)
            {
                return Value;
            }

            //and now we're down to stuff that requires math.
            double calculatedResult;
            CustomSampledMetricSamplePacket baselineSamplePacket = (CustomSampledMetricSamplePacket) baselineSample.Packet;

            if (metricType == MetricSampleType.TotalCount)
            {
                //here we want to calculate the difference between the start and end of our sampled period, ignoring interim samples.
                calculatedResult = Packet.RawValue - baselineSamplePacket.RawValue;
            }
            else if (metricType == MetricSampleType.TotalFraction)
            {
                double valueDelta = Packet.RawValue - baselineSamplePacket.RawValue;
                double baseDelta = Packet.BaseValue - baselineSamplePacket.BaseValue;

                //Protect from a divide by zero case.
                if ((baseDelta == 0) && (valueDelta != 0))
                {
                    throw new DivideByZeroException(string.Format(CultureInfo.InvariantCulture, "The baseline delta is zero however the value delta is not, indicating a data collection problem in the original data.  Value delta: {0}", valueDelta));
                }

                calculatedResult = valueDelta / baseDelta;
            }
            else if (metricType == MetricSampleType.IncrementalCount) 
            {
                //The new value is just the total at the end, so we just get the value property which knows enough to sum things.
                calculatedResult = Value;
            }
            else if (metricType == MetricSampleType.IncrementalFraction)
            {
                double value = Value;
                double baseValue = BaseValue;

                //Protect from a divide by zero case.
                if ((baseValue == 0) && (value != 0))
                {
                    throw new DivideByZeroException(string.Format(CultureInfo.InvariantCulture, "The baseline value is zero however the value is not, indicating a data collection problem in the original data.  Value: {0}", value));
                }

                calculatedResult = value / baseValue;
            }
            else 
            {
                // This is dumb, but FxCop doesn't seem to notice that the duplicate casts are in non-overlapping code paths.
                // So to make it happy, moving the cast outside these last two if's (now nested instead of chained).
                // Note: This will throw an exception if it fails to cast, before we check the MetricSampleType enum.
                CustomSampledMetricSample customSample = (CustomSampledMetricSample)baselineSample;
                if (metricType == MetricSampleType.RawCount)
                {
                    //we need to do a weighted average of the values in the range
                    //now life gets more fun - we have to do a weighted average of everything in between the baseline sample and this sample.
                    CustomSampledMetricSample[] samples = SampleRange(customSample);
                    calculatedResult = CalculateWeightedAverageValue(samples);
                }
                else if (metricType == MetricSampleType.RawFraction)
                {
                    //we do a weighted average of the values in the range, then divide
                    CustomSampledMetricSample[] samples = SampleRange(customSample);
                    double value = CalculateWeightedAverageValue(samples);
                    double baseValue = CalculateWeightedAverageBaseValue(samples);

                    //Protect from a divide by zero case.
                    if ((baseValue == 0) && (value != 0))
                    {
                        throw new DivideByZeroException(string.Format(CultureInfo.InvariantCulture, "The baseline value is zero however the value is not, indicating a data collection problem in the original data.  Value: {0}", value));
                    }

                    calculatedResult = value / baseValue;
                }
                else
                {
                    //oh hell.  We probably should have used a switch statement, but I didn't. Why?  Perhaps someone will 
                    //call that out in code review, but I think it was because of how much code is in each of these cases.
                    throw new ArgumentOutOfRangeException();
                }
            }

            return calculatedResult;
        }

        /// <summary>
        /// The custom sampled metric this sample is for.
        /// </summary>
        public new CustomSampledMetric Metric { get { return (CustomSampledMetric)base.Metric; } }

        /// <summary>
        /// Compares this sample to another sample to determine if they are the same or how to sort them. 
        /// </summary>
        /// <remarks>This comparison guarantees absolute sorting of samples in order.</remarks>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(CustomSampledMetricSample other)
        {
            //gateway to our base object
            return base.CompareTo(other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(CustomSampledMetricSample other)
        {
            //We're really just a type cast, refer to our base object
            return base.Equals(other);
        }

        /// <summary>
        /// The base value for this sample if the metric sample type is a fraction.
        /// </summary>
        public double BaseValue
        {
            get
            {
                //how we calculate base value depends on what our type is.  In many cases, it's meaningless.
                switch(Metric.Definition.MetricSampleType)
                {
                    case MetricSampleType.RawCount:
                    case MetricSampleType.IncrementalCount:
                    case MetricSampleType.TotalCount:
                        //not valid - these don't use baseline
                        throw new ArgumentException("Base values are not available for metrics that are not fractions.");
                    case MetricSampleType.RawFraction:
                    case MetricSampleType.IncrementalFraction:
                    case MetricSampleType.TotalFraction:
                        //we're good.
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                //now we go ahead and sum up everything up through ourself.
                if (m_BaseValue == null)
                {
                    //we haven't calculated it yet.  We need to find our immediately prior sample.
                    int ourIndex = Metric.Samples.IndexOf(this);

                    //if we aren't the first, we need to add our base value to the prior base value.
                    if (ourIndex == 0)
                    {
                        m_BaseValue = Packet.BaseValue;
                    }
                    else
                    {
                        //get the prior sample
                        CustomSampledMetricSample priorSample = Metric.Samples[ourIndex - 1];

                        //and now get its base value to add to ours.
                        m_BaseValue = priorSample.BaseValue + Packet.BaseValue;
                    }
                }

                //and return our calculated base value.
                return (double)m_BaseValue;
            }
        }

        /// <summary>
        /// The value for this sample.
        /// </summary>
        public new double Value
        {
            get
            {
                double returnVal;

                //how we calculate base value depends on what our type is.  In many cases, it's meaningless.
                switch (Metric.Definition.MetricSampleType)
                {
                    case MetricSampleType.IncrementalCount:
                    case MetricSampleType.IncrementalFraction:
                        //now we go ahead and sum up everything up through ourself.
                        if (m_Value == null)
                        {
                            //we haven't calculated it yet.  We need to find our immediately prior sample.
                            int ourIndex = Metric.Samples.IndexOf(this);

                            //if we aren't the first, we need to add our base value to the prior base value.
                            if (ourIndex == 0)
                            {
                                m_Value = Packet.RawValue;
                            }
                            else
                            {
                                //get the prior sample
                                CustomSampledMetricSample priorSample = Metric.Samples[ourIndex - 1];

                                //and now get its base value to add to ours.
                                m_Value = priorSample.Value + Packet.RawValue;
                            }
                        }

                        //and our return value will be our now stored value
                        returnVal = (double)m_Value;
                        break;

                    case MetricSampleType.RawCount:
                    case MetricSampleType.RawFraction:
                        //it is what it is
                        returnVal = Packet.RawValue;
                        break;

                    case MetricSampleType.TotalCount:
                    case MetricSampleType.TotalFraction:
                        //it's the difference between this one and the prior one.
                        if (m_Value == null)
                        {
                            //we haven't calculated it yet.  We need to find our immediately prior sample.
                            int ourIndex = Metric.Samples.IndexOf(this);

                            //if we aren't the first, we need to add our base value to the prior base value.
                            if (ourIndex == 0)
                            {
                                m_Value = Packet.RawValue;
                            }
                            else
                            {
                                //get the prior sample
                                CustomSampledMetricSample priorSample = Metric.Samples[ourIndex - 1];

                                //and now get its base value to add to ours.
                                m_Value = Packet.RawValue - priorSample.Packet.RawValue;
                            }
                        }

                        //and our return value will be our now stored value
                        returnVal = (double)m_Value;

                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }


                //and return our calculated base value.
                return returnVal;
            }
        }


        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Get the raw sampled metric packet for this sample.
        /// </summary>
        internal new CustomSampledMetricSamplePacket Packet { get { return (CustomSampledMetricSamplePacket)base.Packet; } }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Calculate the weighted average of the basis values based on the amount of time they apply (the weights)
        /// </summary>
        /// <param name="basis">An array of basis values</param>
        /// <param name="weight">An array of timespan values</param>
        /// <returns>The weighted average</returns>
        private static double WeightedAverage(double[] basis, TimeSpan[] weight)
        {
            double returnValue = 0;

            //sum all the weights to get the net weight
            TimeSpan allTime = new TimeSpan(0);

            foreach (TimeSpan span in weight)
            {
                allTime += span;
            }

            //now that we've summed up all of the timespans, we have to add up a weighted value by applying
            //each basis value in ratio to its timeframe
            for (int curSampleIndex = 0; curSampleIndex < basis.Length; curSampleIndex++)
            {
                returnValue += (basis[curSampleIndex] * (weight[curSampleIndex].TotalMilliseconds / allTime.TotalMilliseconds));
            }

            return returnValue;
        }

        /// <summary>
        /// Create an ordered array of all of the samples between the baseline sample and this one, inclusive
        /// </summary>
        /// <param name="baselineSample"></param>
        /// <returns></returns>
        private CustomSampledMetricSample[] SampleRange(CustomSampledMetricSample baselineSample)
        {
            CustomSampledMetricSample[] returnVal;

            //First, cheap trick:  Is the baseline and this sample the same?
            if (baselineSample == this)
            {
                //an array of one
                returnVal = new CustomSampledMetricSample[1];
                returnVal[0] = baselineSample;
                return returnVal;
            }

            //Now we need to check the real cases:  Find the first sample, find our sample, find everything in between.
            CustomSampledMetricSampleCollection samples = Metric.Samples;

            int baselineIndex = samples.IndexOf(baselineSample);
            int upperBoundIndex = samples.IndexOf(this);

            //we better have gotten values for both or this is bad
            if ((baselineIndex < 0) || (upperBoundIndex < 0))
            {
                throw new ArgumentOutOfRangeException(nameof(baselineSample), "Unable to use the provided baseline because either it or our sample are not in the same sample collection.");
            }

            if (baselineIndex > upperBoundIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(baselineSample), "Unable to use the provided baseline because it is later than our sample in the samples collection, indicating they are in the wrong order.");
            }

            //If the upper bound index (which is our index) is before the end of the sample set, we want to include one more sample to handle
            //the time interval slide between the raw value and the metric sample (if any).
            if ((upperBoundIndex < (samples.Count - 1)) && (Packet.RawTimestamp != Timestamp))
            {
                //we want to include one more sample to be sure we get the whole time range
                upperBoundIndex++;
            }

            //Otherwise, we want to define our array to hold all of the necessary elements and then copy them.
            returnVal = new CustomSampledMetricSample[(upperBoundIndex - baselineIndex) + 1];

            for (int curSampleIndex = baselineIndex; curSampleIndex <= upperBoundIndex; curSampleIndex++)
            {
                returnVal[curSampleIndex - baselineIndex] = samples[curSampleIndex];
            }

            return returnVal;
        }

        /// <summary>
        /// Used by the total sampling type to average all of the raw values to extrapolate the right value between baseline and this sample.
        /// </summary>
        /// <param name="sampleRange">The range of samples that includes the timespan we're looking for</param>
        /// <returns></returns>
        private double CalculateWeightedAverageValue(CustomSampledMetricSample[] sampleRange)
        {
            CustomSampledMetricSample baselineSample = sampleRange[0];

            //now assemble a weightable set of data to average together
            double[] valueBasis = new double[sampleRange.Length];
            TimeSpan[] valueWeight = new TimeSpan[sampleRange.Length];

            for (int curSampleIndex = 1; curSampleIndex < sampleRange.Length; curSampleIndex++)
            {
                //compare this sample and the one before it.
                valueBasis[curSampleIndex - 1] = sampleRange[curSampleIndex].Packet.RawValue -
                                                 sampleRange[curSampleIndex - 1].Packet.RawValue;

                //when calculating the weighted time, we must only include the time within our timeframe, which may mean we have to adjust things a bit.
                DateTimeOffset baselineTimeStamp = ((sampleRange[curSampleIndex - 1].Packet.RawTimestamp <
                                               baselineSample.Packet.Timestamp)
                                                  ? baselineSample.Packet.Timestamp
                                                  : sampleRange[curSampleIndex - 1].Packet.RawTimestamp);

                //we also need to potentially bound the ending time to handle the last sample being normally beyond the end of our range
                DateTimeOffset sampleTimeStamp = ((sampleRange[curSampleIndex].Packet.RawTimestamp < Timestamp)
                                                ? Timestamp
                                                : sampleRange[curSampleIndex].Packet.RawTimestamp);

                //it isn't accidental we're using the raw timestamp.  When comparing averages, the small differences between when a raw value became the 
                //current value and when we sampled it can have a significant effect on the average.
                valueWeight[curSampleIndex - 1] = sampleTimeStamp - baselineTimeStamp;
            }

            //We are casting down to lose some resolution...  makes you wonder if we should be using double.
            return WeightedAverage(valueBasis, valueWeight);
        }

        /// <summary>
        /// Used by the total sampling type to average all of the raw values to extrapolate the right value between baseline and this sample.
        /// </summary>
        /// <param name="sampleRange">The range of samples that includes the timespan we're looking for</param>
        /// <returns></returns>
        private double CalculateWeightedAverageBaseValue(CustomSampledMetricSample[] sampleRange)
        {
            CustomSampledMetricSample baselineSample = sampleRange[0];

            //now assemble a weightable set of data to average together
            double[] valueBasis = new double[sampleRange.Length];
            TimeSpan[] valueWeight = new TimeSpan[sampleRange.Length];

            for (int curSampleIndex = 1; curSampleIndex < sampleRange.Length; curSampleIndex++)
            {
                //compare this sample and the one before it.
                valueBasis[curSampleIndex - 1] = sampleRange[curSampleIndex].Packet.BaseValue -
                                                 sampleRange[curSampleIndex - 1].Packet.BaseValue;

                //when calculating the weighted time, we must only include the time within our timeframe, which may mean we have to adjust things a bit.
                DateTimeOffset baselineTimeStamp = ((sampleRange[curSampleIndex - 1].Packet.RawTimestamp <
                                               baselineSample.Packet.Timestamp)
                                                  ? baselineSample.Packet.Timestamp
                                                  : sampleRange[curSampleIndex - 1].Packet.RawTimestamp);

                //we also need to potentially bound the ending time to handle the last sample being normally beyond the end of our range
                DateTimeOffset sampleTimeStamp = ((sampleRange[curSampleIndex].Packet.RawTimestamp < Timestamp)
                                                ? Timestamp
                                                : sampleRange[curSampleIndex].Packet.RawTimestamp);

                //it isn't accidental we're using the raw timestamp.  When comparing averages, the small differences between when a raw value became the 
                //current value and when we sampled it can have a significant effect on the average.
                valueWeight[curSampleIndex - 1] = sampleTimeStamp - baselineTimeStamp;
            }

            //We are casting down to lose some resolution...  makes you wonder if we should be using double.
            return WeightedAverage(valueBasis, valueWeight);
        }

        #endregion

    }
}
