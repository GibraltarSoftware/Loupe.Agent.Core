
using System;
using Loupe.Extensibility.Data;



namespace Gibraltar.Monitor
{
    /// <summary>
    /// The collection of custom sampled metrics for a given sampled metric definition.
    /// </summary>
    public sealed class CustomSampledMetricDictionary : SampledMetricCollection
    {
        /// <summary>
        /// Create a new custom sampled metric dictionary for the provided definition.
        /// </summary>
        /// <remarks>This dictionary is created automatically by the Custom Sampled Metric Definition during its initialization.</remarks>
        /// <param name="metricDefinition">The definition of the custom sampled metric to create a metric dictionary for</param>
        internal CustomSampledMetricDictionary(CustomSampledMetricDefinition metricDefinition)
            : base(metricDefinition)
        {

        }

        /// <summary>
        /// Create a new metric object with the provided instance name and add it to the collection
        /// </summary>
        /// <param name="instanceName">The instance name to use, or blank or null for the default metric.</param>
        /// <returns>The new metric object that was added to the collection</returns>
        public CustomSampledMetric Add(string instanceName)
        {
            //Create a new metric object with the provided instance name (it will get added to us automatically)
            CustomSampledMetric newMetric = new CustomSampledMetric(Definition, instanceName);

            //finally, return the newly created metric object to our caller
            return newMetric;
        }

        /// <summary>
        /// The definition of all of the metrics in this collection.
        /// </summary>
        public new CustomSampledMetricDefinition Definition { get { return (CustomSampledMetricDefinition)base.Definition; } }


        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out CustomSampledMetric value)
        {
            //We are playing a few games to get native typing here.  Because it's an OUt value, we
            //have to swap types around ourselves so we can cast.

            //gateway to our inner dictionary try get value
            bool result = base.TryGetValue(key, out var innerValue);

            value = (CustomSampledMetric)innerValue;

            return result;
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out CustomSampledMetric value)
        {
            //We are playing a few games to get native typing here.  Because it's an OUt value, we
            //have to swap types around ourselves so we can cast.

            //gateway to our inner dictionary try get value
            bool result = base.TryGetValue(key, out var innerValue);

            value = (CustomSampledMetric)innerValue;

            return result;
        }

        /// <summary>
        /// Retrieve the custom sampled metric by its zero-based index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public new CustomSampledMetric this[int index]
        {
            get
            {
                return (CustomSampledMetric)base[index];
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Retrieve custom sampled metric object by its Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public new CustomSampledMetric this[Guid Id]
        {
            get
            {
                return (CustomSampledMetric)base[Id];
            }
        }

        /// <summary>
        /// Retrieve custom sampled metric object by its name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public new CustomSampledMetric this[string key]
        {
            get
            {
                return (CustomSampledMetric)base[key];
            }
        }
    }
}
