using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A collection of metric definitions, keyed by their unique ID and name
    /// </summary>
    /// <remarks>
    /// <para>A metric definition has a unique ID to identify a particular instance of the definition(associated with one session) 
    /// and a name that is unique within a session but is designed for comparison of the same definition between sessions.</para>
    /// <para>This class is sealed because it is owned by either the single static active Log class (for metric collection in the current process)
    /// or during replay is automatically created as part of base objects and there is no way to inject an alternative implementation.</para>
    /// </remarks>
    public interface IMetricDefinitionCollection : IList<IMetricDefinition>
    {
        /// <summary>
        /// Retrieve a metric given its unique Id.
        /// </summary>
        /// <param name="metricId">The unique Id of the metric to retrieve</param>
        /// <returns></returns>
        IMetric Metric(Guid metricId);

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        bool ContainsKey(Guid key);

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="name">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        bool ContainsKey(string name);

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        bool ContainsKey(string metricTypeName, string categoryName, string counterName);

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        bool ContainsMetricKey(Guid key);

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        bool TryGetValue(Guid key, out IMetricDefinition value);

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="name">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        bool TryGetValue(string name, out IMetricDefinition value);

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="metricTypeName">The unique metric type</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        bool TryGetValue(string metricTypeName, string categoryName, string counterName, out IMetricDefinition value);

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        bool TryGetMetricValue(Guid key, out IMetric value);

    }
}
