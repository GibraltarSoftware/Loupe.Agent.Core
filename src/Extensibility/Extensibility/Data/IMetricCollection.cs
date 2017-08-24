using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A collection of metrics, keyed by their unique ID and name
    /// </summary>
    /// <remarks>A metric has a unique ID to identify a particular instance of the metric (associated with one session) 
    /// and a name that is unique within a session but is designed for comparison of the same metric between sessions.</remarks>
    public interface IMetricCollection: IList<IMetric>
    {
        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        bool ContainsKey(Guid key);

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        bool ContainsKey(string key);

        /// <summary>
        /// The metric definition that owns this dictionary, meaning every metric is a specific instance of this metric definition.
        /// </summary>
        IMetricDefinition Definition { get; }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        bool TryGetValue(Guid key, out IMetric value);

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        bool TryGetValue(string key, out IMetric value);

        /// <summary>
        /// Retrieve metric object by its Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        IMetric this[Guid Id] { get; }

        /// <summary>
        /// Retrieve metric object by its name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        IMetric this[string key] { get; }
    }
}
