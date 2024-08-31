using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The definition of a sampled (time series, single value) metric 
    /// </summary>
    /// <remarks>
    /// A sampled metric always has a value for any timestamp between its start and end timestamps.
    /// It presumes any interim value by looking at the best fit sampling of the real world value
    /// and assuming it covers the timestamp in question.  It is therefore said to be contiguous for 
    /// the range of start and end.  Event metrics are only defined at the instant they are timestamped, 
    /// and imply nothing for other timestamps.  
    /// For event based metrics, use the EventMetricDefinition base class.</remarks>
    public interface ISampledMetricDefinition : IMetricDefinition, IComparable<ISampledMetricDefinition>, IEquatable<ISampledMetricDefinition>
    {
        /// <summary>
        /// The display caption for the calculated values captured under this metric
        /// </summary>
        string UnitCaption { get;  }
    }
}
