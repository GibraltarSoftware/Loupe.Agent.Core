
using System;
using System.Diagnostics;
using Loupe.Monitor;
using Loupe.Extensibility.Data;

namespace Loupe.Agent.PerformanceCounters
{
    /// <summary>
    /// A collection of performance counter metric packets
    /// </summary>
    /// <remarks>See MetricCollection for most of the implementation.  This class only adds windows performance counter specific features.</remarks>
    public sealed class PerfCounterMetricCollection : MetricCollection
    {
        /// <summary>
        /// Create a new performance counter metric dictionary for the provided definition.
        /// </summary>
        /// <remarks>This dictionary is created automatically by the Metric Definition during its initialization.</remarks>
        /// <param name="metricDefinition"></param>
        internal PerfCounterMetricCollection(PerfCounterMetricDefinition metricDefinition)
            : base(metricDefinition)
        {
        }

        #region Public Properties and Methods

        /// <summary>
        /// Create a new metric instance for the provided performance counter object
        /// </summary>
        /// <remarks>This will create a new performance counter metric object, add it to this collection, and return it in one call.</remarks>
        /// <param name="newPerfCounter">The windows performance counter to add</param>
        /// <returns>The newly created performance counter metric packet object</returns>
        public PerfCounterMetric Add(PerformanceCounter newPerfCounter)
        {
            //just do the null check - everything else we do in the base object add
            if (newPerfCounter == null)
            {
                throw new ArgumentNullException(nameof(newPerfCounter), "A performance counter object must be provided to add it to the collection.");
            }

            //we first need to go waltz off and find the definition for this guy
            string key = PerfCounterMetricDefinition.GetKey(newPerfCounter);

            //if this key doesn't match our key then we have a problem - it isn't for our collection
            if (base.Definition.Name != key)
            {
                throw new ArgumentOutOfRangeException(nameof(newPerfCounter));
            }

            //New object - go ahead and create it for us.
            PerfCounterMetric newPerfCounterMetric = new PerfCounterMetric((PerfCounterMetricDefinition)base.Definition, newPerfCounter);

            //we do NOT add it to our collection - it JUST DID THAT in the constructor.  If we do it again, we'll get an exception

            //finally, return the performance counter packet object to our caller so they can find it easily.
            return newPerfCounterMetric;
        }

        /// <summary>
        /// Retrieve an item from the collection by the performance counter it represents if present.  
        /// If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The performance counter  to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(PerformanceCounter key, out IMetric value)
        {
            //translate to the string key form and then use the mainstream function
            return TryGetValue(PerfCounterMetric.GetKey(key), out value);
        }

        /// <summary>
        /// Retrieve performance counter metric packet object by the performance counter object it represents.
        /// If the item is not already in the collection, it will be added.
        /// </summary>
        /// <param name="counter">The performance counter object to get a metric packet object for.</param>
        /// <returns>A performance counter metric packet object that represents the provided performance counter object.</returns>
        public PerfCounterMetric this[PerformanceCounter counter]
        {
            get
            {
                //see if we already have this performance counter object.
                if (TryGetValue(counter, out var metric) == false)
                {
                    //nope, not already in our collection.  We need to add it.
                    metric = Add(counter);
                }

                //BUT: before we return, do a cast and type check. If the type isn't right, throw a better error than the one they're about to get.
                PerfCounterMetric perfCounterMetric = metric as PerfCounterMetric;
                if (perfCounterMetric == null)
                {
                    throw new InvalidCastException("There is already a metric with the same name as the performance counter, but it is not a performance counter metric.");
                }

                return perfCounterMetric;
            }
        }

        /// <summary>
        /// The definition of all of the metrics in this collection.
        /// </summary>
        public new PerfCounterMetricDefinition Definition => (PerfCounterMetricDefinition)base.Definition;

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out PerfCounterMetric value)
        {
            //We are playing a few games to get native typing here.  Because it's an OUt value, we
            //have to swap types around ourselves so we can cast.

            //gateway to our inner dictionary try get value
            bool result = base.TryGetValue(key, out var innerValue);

            value = (PerfCounterMetric)innerValue;

            return result;
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out PerfCounterMetric value)
        {
            //We are playing a few games to get native typing here.  Because it's an OUt value, we
            //have to swap types around ourselves so we can cast.

            //gateway to our inner dictionary try get value
            bool result = base.TryGetValue(key, out var innerValue);

            value = (PerfCounterMetric)innerValue;

            return result;
        }

        /// <summary>
        /// Retrieve the custom sampled metric by its zero-based index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public new PerfCounterMetric this[int index]
        {
            get => (PerfCounterMetric)base[index];
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Retrieve custom sampled metric object by its Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public new PerfCounterMetric this[Guid Id] => (PerfCounterMetric)base[Id];

        /// <summary>
        /// Retrieve custom sampled metric object by its name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public new PerfCounterMetric this[string key] => (PerfCounterMetric)base[key];

        #endregion
    }
}
