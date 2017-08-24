using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// Defines one value that can be associated with an event metric.
    /// </summary>
    public interface IEventMetricValueDefinition : IEquatable<IEventMetricValueDefinition>
    {
        /// <summary>
        /// The default way that individual samples will be aggregated to create a graphable trend.
        /// </summary>
        EventMetricValueTrend DefaultTrend { get; }

        /// <summary>
        /// The metric definition this value is associated with.
        /// </summary>
        IEventMetricDefinition Definition { get; }

        /// <summary>
        /// The unique name for this value within the event definition.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The end-user display caption for this value.
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// The end-user description for this value.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The simple type of all data recorded for this value.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Indicates whether the metric value can be graphed as a trend.
        /// </summary>
        bool IsTrendable { get; }

        /// <summary>
        /// The units of measure for the data captured with this value (if trendable)
        /// </summary>
        string UnitCaption { get; }

        /// <summary>
        /// The index of this value definition (and related values) within the values collection.
        /// </summary>
        /// <remarks>Since sample values are provided as an object array it is useful to cache the 
        /// index of an individual value to rapidly retrieve specific values from each sample.</remarks>
        int Index { get; }
    }
}
