using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The definition of an event metric, necessary before any specific metric can be created.
    /// </summary>
    /// <remarks>
    /// A sampled metric always has a value for any timestamp between its start and end timestamps.
    /// It presumes any interim value by looking at the best fit sampling of the real world value
    /// and assuming it covers the timestamp in question.  It is therefore said to be contiguous for 
    /// the range of start and end.  Event metrics are only defined at the instant they are timestamped, 
    /// and imply nothing for other timestamps.  
    /// For sampled metrics, use the SampledMetric base class.</remarks>
    public interface IEventMetricDefinition : IMetricDefinition, IComparable<IEventMetricDefinition>, IEquatable<IEventMetricDefinition>
    {
        /// <summary>
        /// The default value to display for this event metric.  Typically this should be a trendable value.
        /// </summary>
        IEventMetricValueDefinition DefaultValue { get; }

        /// <summary>
        /// The set of values defined for this metric definition
        /// </summary>
        /// <remarks>Any number of different values can be recorded along with each event to provide additional trends and filtering ability
        /// for later client analysis.</remarks>
        IEventMetricValueDefinitionCollection Values { get; }
    }
}
