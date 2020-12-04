using System;
using System.Globalization;

namespace Gibraltar.Agent.Metrics
{
    /// <summary>
    /// One sample of a Event metric
    /// </summary>
    /// <remarks>Specific Event metrics will have a derived implementation of this class, however
    /// clients should work with this interface when feasible to ensure compatibility with any Event
    /// metric implementation.</remarks>
    public sealed class EventMetricSample : IComparable<EventMetricSample>, IEquatable<EventMetricSample>
    {
        private const string LogCategory = "Loupe.Event Metric";

        private readonly EventMetric m_Metric;
        private readonly Monitor.EventMetricSample m_WrappedSample;

        /// <summary>
        /// Create a new API event metric sample object for the provided metric and internal event metric sample.
        /// </summary>
        /// <remarks>The metric sample is NOT? automatically added to the samples collection of the provided metric object.</remarks>
        /// <param name="metric">The metric object this sample applies to.</param>
        /// <param name="metricSample">The internal metric sample.</param>
        internal EventMetricSample(EventMetric metric, Monitor.EventMetricSample metricSample)
        {
            //and now that we've been created, make sure our metric definition set is locked.
            //metric.Definition.IsReadOnly = true; // ToDo: Double-check that this is set within internal sample.

            // Cache the Event-typed objects we passed to our general metric sample base, so we don't have to cast them.
            m_Metric = metric;
            m_WrappedSample = metricSample;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Set a value in this sample by its value column name.  
        /// </summary>
        /// <remarks>The value must be defined as part of the event metric definition associated with this sample
        /// or an exception will be thrown.  The data type must also be compatible with the data type configured
        /// on the event metric definition or no data will be recorded.</remarks>
        /// <param name="name">The unique name of the value being recorded (must match a value name in the metric definition).</param>
        /// <param name="value">The value to be recorded.</param>
        public void SetValue(string name, object value)
        {
            //make sure we got a name
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            //look up the value in the definition so we can find its offset into the array
            EventMetricValueDefinition curValueDefinition;

            if (Metric.Definition.ValueCollection.TryGetValue(name, out curValueDefinition) == false)
            {
#if DEBUG
                //if we're compiled in debug mode, tell the user they blew it.
                throw new ArgumentOutOfRangeException(nameof(name), name);
#else
                //trace log and return, nothing we can do.
                Log.Warning(LogCategory, "Unable to add metric value because the value definition could not be found.",
                    "Unable to add metric value to the current sample because there is no value definition named {1} for metric definition {0}",
                    Metric.Definition.Key, name);
                return;
#endif
            }

            //now use our overload that takes value to go from here.
            SetValue(curValueDefinition, value);
        }


        /// <summary>
        /// Records a value to the values array of this sample given its value definition.  
        /// </summary>
        /// <remarks>The value must be defined as part of the event metric definition associated with this sample
        /// or an exception will be thrown.  The data type must also be compatible with the data type configured
        /// on the event metric definition or no data will be recorded.
        /// If called more than once for the same value, the prior value will be replaced.</remarks>
        /// <param name="valueDefinition">The metric value definition object of the value to be recorded.</param>
        /// <param name="value">Optional. The value to be recorded.</param>
        /// <exception cref="ArgumentNullException">Throw if the valueDefinition is null.</exception>
        public void SetValue(EventMetricValueDefinition valueDefinition, object value)
        {
            //make sure we got a value definition
            if (valueDefinition == null)
            {
                throw new ArgumentNullException(nameof(valueDefinition));
            }

            m_WrappedSample.SetValue(valueDefinition.WrappedValueDefinition, value);

        }

        /// <summary>
        /// Records a value to the values array of this sample given the zero-based index of the value definition to be used.  
        /// </summary>
        /// <remarks>The value must be defined as part of the event metric definition associated with this sample
        /// or an exception will be thrown.  The data type must also be compatible with the data type configured
        /// on the event metric definition or no data will be recorded.
        /// If called more than once for the same value, the prior value will be replaced.</remarks>
        /// <param name="valueIndex">The zero-based index within the value definition of the value to be recorded.</param>
        /// <param name="value">Optional.  The value to be recorded.</param>
        public void SetValue(int valueIndex, object value)
        {
            EventMetricValueDefinitionCollection valueDefinitions = Metric.Definition.ValueCollection;

            //we don't have to check that the value index isn't null, but it does have to be in range
            if ((valueIndex < 0) || (valueIndex >= valueDefinitions.Count))
            {
#if DEBUG
                //if we're compiled in debug mode, tell the user they blew it.
                throw new ArgumentOutOfRangeException(nameof(valueIndex), valueIndex.ToString(CultureInfo.CurrentCulture));
#else
                //trace log and return, nothing we can do.
                Log.Warning(LogCategory, "Unable to add metric value because the value definition could not be found.",
                    "There is no value definition at index {1} for metric definition {0}",
                    Metric.Definition.Key, valueIndex);
                return;
#endif
            }

            //look up the value in the definition so we can process it
            EventMetricValueDefinition curValueDefinition = valueDefinitions[valueIndex];

            //and now we can use our one true method to add the value
            SetValue(curValueDefinition, value);
        }

        /// <summary>
        /// The Event metric this sample is for.
        /// </summary>
        public EventMetric Metric { get { return m_Metric; } }

        /// <summary>
        /// The raw value of the metric.
        /// </summary>
        public double GetValue()
        {
            return m_WrappedSample.Value;
        }

        // ToDo: Additional GetValue overrides to query individual values by name, index(?).

        /// <summary>
        /// A copy of all of the values associated with this sample, as an array.  Any value in the array may be a null object.
        /// </summary>
        public object[] GetValues()
        {
            object[] values = m_WrappedSample.Values;
            if (values == null)
                return new object[0];

            object[] returnValues = new object[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                returnValues[i] = values[i];
            }

            return returnValues;
        }

        /// <summary>
        /// Compare this object to another to determine sort order
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(EventMetricSample other)
        {
            //we just gateway to our base object.
            return WrappedSample.CompareTo(other.WrappedSample);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetricSample other)
        {
            //We're really just a type cast, refer to our base object
            return WrappedSample.Equals(other.WrappedSample);
        }

        /*
        /// <summary>
        /// The increasing sequence number of all sample packets for this metric to be used as an absolute order sort.
        /// </summary>
        public long Sequence { get { return m_WrappedSample.Sequence; } }
        

        /// <summary>
        /// The exact date and time the metric was captured.
        /// </summary>
        public DateTimeOffset Timestamp { get { return m_WrappedSample.Timestamp; } }
        */

        /// <summary>
        /// Write this sample to the current process log if it hasn't been written already
        /// </summary>
        /// <remarks>If the sample has not been written to the log yet, it will be written.  
        /// If it has been written, subsequent calls to this method are ignored.</remarks>
        public void Write()
        {
            m_WrappedSample.Write();
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a MetricSample and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            EventMetricSample otherMetricSample = obj as EventMetricSample;

            return Equals(otherMetricSample); // Just have type-specific Equals do the check (it even handles null)
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
            int myHash = m_WrappedSample.GetHashCode(); // Equals just defers to the WrappedSample

            return myHash;
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The internal event metric sample we're wrapping.
        /// </summary>
        internal Monitor.EventMetricSample WrappedSample { get { return m_WrappedSample; } }

        #endregion
    }
}
