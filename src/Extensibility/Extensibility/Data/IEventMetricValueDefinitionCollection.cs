using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A collection of event values for the parent metric definition.
    /// </summary>
    /// <remarks>This object is automatically created by the Event Metric Definition and is accessible through the Values property.</remarks>
    public interface IEventMetricValueDefinitionCollection : IList<IEventMetricValueDefinition>
    {
        /// <summary>
        /// The metric definition this value is associated with.
        /// </summary>
        IEventMetricDefinition Definition { get; }

        /// <summary>
        /// Determines whether the collection contains an element with the specified key.
        /// </summary>
        /// <param name="name">The value name to locate in the collection</param>
        /// <returns>True if the collection contains an element with the key; otherwise, false.</returns>
        bool ContainsKey(string name);

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="name">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>True if the collection contains an element with the specified key; otherwise false.</returns>
        bool TryGetValue(string name, out IEventMetricValueDefinition value);

        /// <summary>
        /// Retrieve a metric value definition by its name
        /// </summary>
        /// <param name="name">The value name to locate in the collection</param>
        /// <returns>The metric value definition.</returns>
        /// <remarks>Items are identified using ordinal, case insensitive string comparisons.  If no value exists with the provided name an exception will be thrown.</remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if no value exists with the provided name.</exception>
        IEventMetricValueDefinition this[string name] { get; }

        /// <summary>
        /// Retrieve the index of a metric value definition by its name
        /// </summary>
        /// <param name="name">The value name to locate in the collection</param>
        /// <remarks>Items are identified using ordinal, case insensitive string comparisons.  If no value exists with the provided name an exception will be thrown.</remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if no value exists with the provided name.</exception>
        int IndexOf(string name);
    }
}
