
using System;
using System.Collections;
using System.Collections.Generic;
using Loupe.Core.Metrics;
using Loupe.Core.Monitor;


namespace Loupe.Agent.Metrics.Internal
{
    /// <summary>
    /// The collection of custom sampled metrics for a given sampled metric definition.
    /// </summary>
    internal sealed class SampledMetricCollection
    {
        private readonly CustomSampledMetricDictionary m_WrappedCollection;
        private readonly Dictionary<CustomSampledMetric, SampledMetric> m_Externalized =
            new Dictionary<CustomSampledMetric, SampledMetric>();

        private readonly SampledMetricDefinition m_MetricDefinition;
        private readonly object m_Lock = new object();

        /// <summary>
        /// Create a new custom sampled metric dictionary for the provided definition.
        /// </summary>
        /// <remarks>This dictionary is created automatically by the Custom Sampled Metric Definition during its initialization.</remarks>
        /// <param name="metricDefinition">The definition of the custom sampled metric to create a metric dictionary for</param>
        internal SampledMetricCollection(SampledMetricDefinition metricDefinition)
        {
            m_MetricDefinition = metricDefinition;
            m_WrappedCollection = metricDefinition.WrappedDefinition.Metrics;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(Guid key)
        {
            //gateway to our inner dictionary 
            return m_WrappedCollection.ContainsKey(key);
        }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            //gateway to our alternate inner dictionary
            return m_WrappedCollection.ContainsKey(key);
        }

        /// <summary>
        /// Create a new metric object with the provided instance name and add it to the collection
        /// </summary>
        /// <param name="instanceName">The instance name to use, or blank or null for the default metric.</param>
        /// <returns>The new metric object that was added to the collection</returns>
        public SampledMetric Add(string instanceName)
        {
            lock (m_Lock)
            {
                //Create a new metric object with the provided instance name (it will get added to us automatically)
                SampledMetric newMetric = new SampledMetric(m_MetricDefinition, instanceName);

                //finally, return the newly created metric object to our caller
                return newMetric;
            }
        }

        /// <summary>
        /// The definition of all of the metrics in this collection.
        /// </summary>
        public SampledMetricDefinition Definition { get { return m_MetricDefinition; } }


        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out SampledMetric value)
        {
            lock (m_Lock)
            {
                //We are playing a few games to get native typing here.  Because it's an OUt value, we
                //have to swap types around ourselves so we can cast.

                //gateway to our inner dictionary try get value
                bool result = m_WrappedCollection.TryGetValue(key, out var innerValue);

                value = result ? Externalize(innerValue) : null;

                return result;
            }
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out SampledMetric value)
        {
            lock (m_Lock)
            {
                //We are playing a few games to get native typing here.  Because it's an OUt value, we
                //have to swap types around ourselves so we can cast.

                //gateway to our internal dictionary try get value
                bool result = m_WrappedCollection.TryGetValue(key, out var innerValue);

                value = result ? Externalize(innerValue) : null;

                return result;
            }
        }

        /// <summary>
        /// Retrieve the custom sampled metric by its zero-based index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">An attempt was made to set an object by index which is not supported.</exception>
        public SampledMetric this[int index]
        {
            get
            {
                lock (m_Lock)
                {
                    return Externalize(m_WrappedCollection[index]);
                }
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException("Setting an object by index is not supported.");
            }
        }

        /// <summary>
        /// Retrieve custom sampled metric object by its Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public SampledMetric this[Guid Id]
        {
            get
            {
                lock (m_Lock)
                {
                    return Externalize(m_WrappedCollection[Id]);
                }
            }
        }

        /// <summary>
        /// Retrieve custom sampled metric object by its name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public SampledMetric this[string key]
        {
            get
            {
                lock (m_Lock)
                {
                    return Externalize(m_WrappedCollection[key]);
                }
            }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Object Change Locking object.
        /// </summary>
        internal object Lock { get { return m_Lock; } }

        internal CustomSampledMetricDictionary WrappedCollection
        {
            get { return m_WrappedCollection; }
        }

        internal SampledMetric Externalize(CustomSampledMetric eventMetric)
        {
            if (eventMetric == null)
                return null;

            lock (m_Lock)
            {
                if (m_Externalized.TryGetValue(eventMetric, out var externalDefinition) == false)
                {
                    externalDefinition = new SampledMetric(m_MetricDefinition, eventMetric);
                    m_Externalized[eventMetric] = externalDefinition;
                }

                return externalDefinition;
            }
        }

        internal void Internalize(SampledMetric metric)
        {
            lock (m_Lock)
            {
                CustomSampledMetric internalMetric = metric.WrappedMetric;

                m_Externalized[internalMetric] = metric;
            }
        }

        #endregion

        #region Private helper class

        private class Enumerator : IEnumerator<SampledMetric>
        {
            private readonly IEnumerator<CustomSampledMetric> m_Enumerator;
            private readonly SampledMetricCollection m_Collection;

            public Enumerator(SampledMetricCollection collection, IEnumerator<CustomSampledMetric> enumerator)
            {
                m_Collection = collection;
                m_Enumerator = enumerator;
            }

            public void Dispose()
            {
                m_Enumerator.Dispose();
            }

            public bool MoveNext()
            {
                return m_Enumerator.MoveNext();
            }

            public void Reset()
            {
                m_Enumerator.Reset();
            }

            public SampledMetric Current
            {
                get { return m_Collection.Externalize(m_Enumerator.Current); }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }
        }

        #endregion
    }
}