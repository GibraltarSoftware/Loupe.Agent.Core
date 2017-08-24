using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The definition of a single metric that has been captured.  
    /// </summary>
    /// <remarks>
    /// Individual metrics capture a stream of values for a metric definition which can then be displayed and manipulated.
    /// </remarks>
    public interface IMetricDefinition : IComparable<IMetricDefinition>, IEquatable<IMetricDefinition>
    {
        /// <summary>
        /// The unique Id of this metric definition packet.  This can reliably be used as a key to refer to this item.
        /// </summary>
        /// <remarks>The key can be used to compare the same definition across different instances (e.g. sessions).
        /// This Id is always unique to a particular instance.</remarks>
        Guid Id { get; }

        /// <summary>
        /// The name of the metric definition being captured.  
        /// </summary>
        /// <remarks>The name is for comparing the same definition in different sessions. They will have the same name but 
        /// not the same Id.</remarks>
        string Name { get; }

        /// <summary>
        /// A short display string for this metric definition, suitable for end-user display.
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// A description of what is tracked by this metric, suitable for end-user display.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The recommended default display interval for graphing. 
        /// </summary>
        MetricSampleInterval Interval { get; }

        /// <summary>
        /// The internal metric type of this metric definition
        /// </summary>
        /// <remarks>Metric types distinguish different metric capture libraries from each other, ensuring
        /// that we can correctly correlate the same metric between sessions and not require category names 
        /// to be globally unique.  If you are creating a new metric, pick your own metric type that will
        /// uniquely identify your library or namespace.</remarks>
        string MetricTypeName { get; }

        /// <summary>
        /// The definitions collection that contains this definition.
        /// </summary>
        /// <remarks>This parent pointer should be used when walking from an object back to its parent instead of taking
        /// advantage of the static metrics definition collection to ensure your application works as expected when handling
        /// data that has been loaded from a database or data file.  The static metrics collection is for the metrics being
        /// actively captured in the current process, not for metrics that are being read or manipulated.</remarks>
        IMetricDefinitionCollection Definitions { get; }

        /// <summary>
        /// The set of metrics that use this definition.
        /// </summary>
        /// <remarks>All metrics with the same definition are of the same object type.</remarks>
        IMetricCollection Metrics { get; }

        /// <summary>
        /// The category of this metric for display purposes. This can be a period delimited string to represent a variable height hierarchy
        /// </summary>
        string CategoryName { get; }


        /// <summary>
        /// An array of the individual category names within the specified category name which is period delimited.
        /// </summary>
        string[] CategoryNames { get; }

        /// <summary>
        /// The display name of this metric (unique within the category name).
        /// </summary>
        string CounterName { get; }

        /// <summary>
        /// The sample type of the metric.  Indicates whether the metric represents discrete events or a continuous value.
        /// </summary>
        SampleType SampleType { get; }
    }
}
