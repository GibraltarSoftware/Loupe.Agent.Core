using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single metric that has been captured.  A metric is a single measured value over time.  
    /// </summary>
    /// <remarks>
    /// To display the data captured for this metric, use Calculate Values to translate the raw captured data
    /// into displayable information.
    /// </remarks>
    public interface IMetric
    {
        /// <summary>
        /// The unique Id of this metric instance.  This can reliably be used as a key to refer to this item.
        /// </summary>
        /// <remarks>The key can be used to compare the same metric across different instances (e.g. sessions).
        /// This Id is always unique to a particular instance.</remarks>
        Guid Id { get; }

        /// <summary>
        /// The fully qualified name of the metric being captured.  
        /// </summary>
        /// <remarks>The name is for comparing the same metric in different sessions. They will have the same name but 
        /// not the same Id.</remarks>
        string Name { get; }

        /// <summary>
        /// A short caption of what the metric tracks, suitable for end-user display.
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// A description of what is tracked by this metric, suitable for end-user display.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The definition of this metric object.
        /// </summary>
        IMetricDefinition Definition { get; }

        /// <summary>
        /// The internal metric type of this metric definition
        /// </summary>
        string MetricTypeName { get; }

        /// <summary>
        /// The category of this metric for display purposes.  Category is the top displayed hierarchy.
        /// </summary>
        string CategoryName { get; }

        /// <summary>
        /// The display name of this metric (unique within the category name).
        /// </summary>
        string CounterName { get; }

        /// <summary>
        /// Gets or sets an instance name for this performance counter.
        /// </summary>
        string InstanceName { get; }

        /// <summary>
        /// Indicates whether this is the default metric instance for this metric definition or not.
        /// </summary>
        /// <remarks>The default instance has a null instance name.  This property is provided as a convenience to simplify
        /// client code so you don't have to distinguish empty strings or null.</remarks>
        bool IsDefault { get; }

        /// <summary>
        /// The earliest start date and time of the raw data samples.
        /// </summary>
        DateTimeOffset StartDateTime { get; }

        /// <summary>
        /// The last date and time of the raw data samples.
        /// </summary>
        DateTimeOffset EndDateTime { get; }

        /// <summary>
        /// The sample type of the metric.  Indicates whether the metric represents discrete events or a continuous value.
        /// </summary>
        SampleType SampleType { get; }

        /// <summary>
        /// Calculate displayable values based on the full information captured for this metric, 
        /// returning all dates available at the default interval.
        /// </summary>
        /// <remarks>
        /// The raw values may not be suitable for display depending on the unit the values are captured in, and
        /// depending on how the data was sampled it may not display well because of uneven sampling if processed
        /// directly.
        /// </remarks>
        /// <returns>A metric value set suitable for display</returns>
        IMetricValueCollection CalculateValues();

        /// <summary>
        /// Calculate displayable values based on the full information captured for this metric with the specified interval 
        /// for all dates available
        /// </summary>
        /// <remarks>
        /// The raw values may not be suitable for display depending on the unit the values are captured in, and
        /// depending on how the data was sampled it may not display well because of uneven sampling if processed
        /// directly.
        /// </remarks>
        /// <param name="interval">The requested data sample size</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <returns>A metric value set suitable for display</returns>
        IMetricValueCollection CalculateValues(MetricSampleInterval interval, int intervals);

        /// <summary>
        /// Calculate displayable values based on the full information captured for this metric
        /// </summary>
        /// <remarks>
        /// The raw values may not be suitable for display depending on the unit the values are captured in, and
        /// depending on how the data was sampled it may not display well because of uneven sampling if processed
        /// directly.
        /// </remarks>
        /// <param name="interval">The requested data sample size</param>
        /// <param name="intervals">The number of intervals to have between each value exactly.</param>
        /// <param name="startDateTime">The earliest date to retrieve data for</param>
        /// <param name="endDateTime">The last date to retrieve data for</param>
        /// <returns>A metric value set suitable for display</returns>
        IMetricValueCollection CalculateValues(MetricSampleInterval interval, int intervals, DateTimeOffset? startDateTime, DateTimeOffset? endDateTime);

        /// <summary>
        /// The set of raw samples for this metric
        /// </summary>
        IMetricSampleCollection Samples { get; }
    }

}
