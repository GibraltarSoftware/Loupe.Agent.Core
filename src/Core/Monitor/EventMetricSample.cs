
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Gibraltar.Monitor.Internal;
using Loupe.Extensibility.Data;



namespace Gibraltar.Monitor
{
    /// <summary>
    /// One sample of a Event metric
    /// </summary>
    /// <remarks>Specific Event metrics will have a derived implementation of this class, however
    /// clients should work with this interface when feasible to ensure compatibility with any Event
    /// metric implementation.</remarks>
    public class EventMetricSample : MetricSample, IComparable<EventMetricSample>, IEquatable<EventMetricSample>, IEventMetricSample
    {
        private const string LogCategory = "Loupe";

        /// <summary>
        /// Create a new Event metric sample object for the provided metric and raw sample packet.
        /// </summary>
        /// <remarks>The metric sample is automatically added to the samples collection of the provided metric object.</remarks>
        /// <param name="metric">The metric object this sample applies to.</param>
        /// <param name="metricSamplePacket">The raw sample data packet.</param>
        internal EventMetricSample(EventMetric metric, EventMetricSamplePacket metricSamplePacket)
            : base(metric, metricSamplePacket)
        {
            //and now that we've been created, make sure our metric definition set is locked.
            metric.Definition.IsReadOnly = true;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Add a value to the values array of this sample by name.  
        /// </summary>
        /// <remarks>The value must be defined as part of the event metric definition associated with this sample
        /// or an exception will be thrown.  The data type must also be compatible with the data type configured
        /// on the event metric definition or no data will be recorded.</remarks>
        /// <param name="name">The unique name of the value being recorded (must match a value name in the metric definition)</param>
        /// <param name="value">The value to be recorded.</param>
        public void SetValue(string name, object value)
        {
            //make sure we got a name
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            //look up the value in the definition so we can find its offset into the array
            IEventMetricValueDefinition curValueDefinition;

            if (Metric.Definition.Values.TryGetValue(name, out curValueDefinition) == false)
            {
#if DEBUG
                //if we're compiled in debug mode, tell the user they blew it.
                throw new ArgumentOutOfRangeException(nameof(name), name);
#else
                //log and return, nothing we can do.
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to add metric value to the current sample due to missing value definition", "There is no value definition named {1} for metric definition {0}",
                        Metric.Definition.Name, name);
                return;
#endif
            }
            
            //now use our overload that takes value to go from here.
            SetValue((EventMetricValueDefinition)curValueDefinition, value);
        }


        /// <summary>
        /// Records a value to the values array of this sample given its value definition.  
        /// </summary>
        /// <remarks>The value must be defined as part of the event metric definition associated with this sample
        /// or an exception will be thrown.  The data type must also be compatible with the data type configured
        /// on the event metric definition or no data will be recorded.
        /// If called more than once for the same value, the prior value will be replaced.</remarks>
        /// <param name="valueDefinition">The metric value definition object of the value to be recorded.</param>
        /// <param name="value">The value to be recorded.</param>
        public void SetValue(EventMetricValueDefinition valueDefinition, object value)
        {
            //make sure we got a value definition
            if (valueDefinition == null)
            {
                throw new ArgumentNullException(nameof(valueDefinition));
            }

            //look up the numerical index in the collection so we know what offset to put it in the array at
            int valueIndex = Metric.Definition.Values.IndexOf(valueDefinition);

            //if we didn't find it, we're hosed
            if (valueIndex < 0)
            {
#if DEBUG
                //if we're compiled in debug mode, tell the user they blew it.
                throw new ArgumentOutOfRangeException(nameof(valueDefinition), valueDefinition.Name);
#else
                //log and return, nothing we can do.
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to add metric value to the current sample due to missing value definition", "There is no value definition named {1} for metric definition {0}",
                        Metric.Definition.Name, valueDefinition.Name);
                return;
#endif
            }
            
            //coerce it into the right type
            object storedValue;
            if (value == null)
            {
                //you can always store a null.  And we can't really check it more, so there.
                storedValue = null;
            }
            else
            {
                //get the type so we can verify it
                Type valueType = value.GetType();

                //is it close enough to what we're expecting?
                if (valueDefinition.IsTrendable) 
                {
                    if (EventMetricDefinition.IsTrendableValueType(valueType))
                    {
                        storedValue = value;
                    }
                    else
                    {
                        //no, it should be trendable and it isn't.  store null.
                        storedValue = null;
                    }
                }
                else
                {
                    //we don't care what it is because we're going to coerce it to a string.
                    storedValue = value.ToString();
                }
            }

            //now write out the value to the correct spot in the array
            Packet.Values[valueIndex] = storedValue;
        }

        /// <summary>
        /// Records a value to the values array of this sample given the zero-based index of the value definition to be used.  
        /// </summary>
        /// <remarks>The value must be defined as part of the event metric definition associated with this sample
        /// or an exception will be thrown.  The data type must also be compatible with the data type configured
        /// on the event metric definition or no data will be recorded.
        /// If called more than once for the same value, the prior value will be replaced.</remarks>
        /// <param name="valueIndex">The zero-based index within the value definition of the value to be recorded.</param>
        /// <param name="value">The value to be recorded.</param>
        public void SetValue(int valueIndex, object value)
        {
            IEventMetricValueDefinitionCollection valueDefinitions = Metric.Definition.Values;

            //we don't have to check that the value index isn't null, but it does have to be in range
            if ((valueIndex < 0) || (valueIndex > (valueDefinitions.Count - 1)))
            {
#if DEBUG
                //if we're compiled in debug mode, tell the user they blew it.
                throw new ArgumentOutOfRangeException(nameof(valueIndex), valueIndex.ToString(CultureInfo.CurrentCulture));
#else
                //log and return, nothing we can do.
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to add metric value to the current sample due to missing value definition","There is no value definition at index {1} for metric definition {0}",
                        Metric.Definition.Name, valueIndex);
                return;
#endif
            }

            //look up the value in the definition so we can process it
            IEventMetricValueDefinition curValueDefinition = valueDefinitions[valueIndex];

            //and now we can use our one true method to add the value
            SetValue((EventMetricValueDefinition)curValueDefinition, value);
        }

        /// <summary>
        /// Get the effective value, substituting zero for null.
        /// </summary>
        /// <param name="valueIndex">The numeric index of the value to retrieve</param>
        /// <returns></returns>
        internal double GetEffectiveValue(int valueIndex)
        {
            double returnVal;

            //get the raw value - it could be any type, could be null.
            object rawValue = Values[valueIndex];
            
            //Now start handling it depending on what it is and what type it is, occasionally we have to mess with the data more explicitly

            //if it's null, we return zero.
            if (rawValue == null)
            {
                returnVal = 0;
            }
            else if (rawValue is TimeSpan)
            {
                returnVal = ((TimeSpan)rawValue).TotalMilliseconds;
            }
            else if ((rawValue is DateTimeOffset) 
                || (rawValue is DateTime))
            {
                //no direct conversion to double.
                returnVal = 1; //we basically have to count the number of occurrences, we can't convert to/from 
            }
            else
            {
                //We are going to do a conversion, not a cast, to double because we may lose precision, etc.
                returnVal = Convert.ToDouble(rawValue, CultureInfo.InvariantCulture);
            }

            return returnVal;
        }

        /// <summary>
        /// The Event metric this sample is for.
        /// </summary>
        public new EventMetric Metric { get { return (EventMetric)base.Metric; } }

        /// <summary>
        /// The raw packet for this event metric sample.
        /// </summary>
        internal new EventMetricSamplePacket Packet { get { return (EventMetricSamplePacket)base.Packet; } }

        /// <summary>
        /// The raw value of this metric.  Depending on the metric definition, this may be meaningless and instead a 
        /// calculation may need to be performed.
        /// </summary>
        public override double Value
        {
            get
            {
                double value;

                //There are two possible values:  Either we have a default numerical value assigned and can
                //return it in raw form, or we will return 1 (we are a count of 1)
                IEventMetricValueDefinition defaultValueDefinition = Metric.Definition.DefaultValue;
                
                if (defaultValueDefinition == null)
                {
                    //no default value defined, return one
                    value = 1;  //we are automatically a count of one, that way if someone sums a set of instances they get a count
                }
                else
                {
                    //We need to read the object value from our values collection.  It could be null, it could be of a different type....

                    //If it isn't trendable, etc. we're going to return it as null
                    if (defaultValueDefinition.IsTrendable)
                    {
                        //We have a default value so we're going to return what it has - either null or a numerical value
                        int valueIndex = Metric.Definition.Values.IndexOf(defaultValueDefinition);

#if DEBUG
                        Debug.Assert(valueIndex >= 0);  //it has to be because we got the object above, so I'm only doing an assert 
#endif
                        //all trendable values are castable
                        if (Values[valueIndex] == null)
                        {
                            //Lets translate all cases of null into NaN since we aren't defined as Double?
                            value = double.NaN;
                        }
                        else
                        {
                            //use our get effective value routine since it has any conversion overrides we need
                            value = GetEffectiveValue(valueIndex);
                        }
                    }
                    else
                    {
                        value = double.NaN;
                    }
                }


                return value;
            }
        }

        /// <summary>
        /// Compute the resultant value for this sample compared with the provided baseline sample.
        /// </summary>
        /// <remarks>
        /// <para>The baseline sample must be for a date and time prior to this sample for correct results.</para>
        /// <para>If the supplied trendValue isn't trendable, the number of samples with a non-null value will be counted.</para>
        /// <para>If the supplied trendValue is trendable, the Default Trend (average or sum) will be calculated for all
        /// samples between the supplied baseline sample and this sample, inclusive.</para>
        /// </remarks>
        /// <param name="baselineSample">The previous baseline sample to calculate a difference for</param>
        /// <param name="trendValue">The definition of the value from this event metric to trend.</param>
        /// <returns>The calculated counter value</returns>
        public double ComputeValue(IEventMetricSample baselineSample, IEventMetricValueDefinition trendValue)
        {
            //we have to figure out all of the samples to include between these points.
            List<EventMetricSample> valueSamples = new List<EventMetricSample>();

            EventMetricSampleCollection samples = Metric.Samples;
            int startIndex = (baselineSample == null) ? 0 : samples.IndexOf(baselineSample);
            int ourIndex = samples.IndexOf(this);

            for (int sampleIndex = startIndex; sampleIndex <= ourIndex; sampleIndex++)
            {
                valueSamples.Add(samples[sampleIndex]);
            }

            return Metric.CalculateSample(null, valueSamples, 0, 0, Timestamp, (EventMetricValueDefinition)trendValue);
        }

        /// <summary>
        /// The array of values associated with this sample.  Any value may be a null object.
        /// </summary>
        public object[] Values { get { return Packet.Values; } }

        /// <summary>
        /// Compare this object to another to determine sort order
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(EventMetricSample other)
        {
            //we just gateway to our base object.
            return base.CompareTo(other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetricSample other)
        {
            //We're really just a type cast, refer to our base object
            return base.Equals(other);
        }


        #endregion
    }
}
