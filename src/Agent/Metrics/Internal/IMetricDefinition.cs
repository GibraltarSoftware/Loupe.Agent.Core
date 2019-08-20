using System;
using Loupe.Core.Metrics;
using Loupe.Extensibility.Data;
using Loupe.Metrics;

namespace Loupe.Agent.Metrics.Internal
{
    /// <summary>
    /// The definition of a single metric that has been captured.  
    /// </summary>
    /// <remarks>
    /// Individual metrics capture a stream of values for a metric definition which can then be displayed and manipulated.
    /// </remarks>
    internal interface IMetricDefinition : IComparable<IMetricDefinition>, IEquatable<IMetricDefinition>
    {
        /// <summary>
        /// The unique Id of this metric definition packet.  This can reliably be used as a key to refer to this item, within the same session which created it.
        /// </summary>
        /// <remarks>The Id is limited to a specific session, and thus identifies a consistent unchanged definition. The
        /// Id can <b>not</b> be used to identify a definition across different sessions, which could have different
        /// actual definitions due to changing user code.  See the Key property to identify a metric definition across
        /// different sessions.</remarks>
        Guid Id { get; }

        /// <summary>
        /// The name of the metric definition being captured.  
        /// </summary>
        /// <remarks>The Key is the combination of metrics system label, category name, and counter name to uniquely
        /// identify a specific metric definition.  It can also identify the same definition across different sessions.
        /// They will have the same name but not the same Id.</remarks>
        string Key { get; }

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
        SamplingInterval Interval { get; }

        /// <summary>
        /// The metric capture system label under which this metric definition was created.
        /// </summary>
        /// <remarks>This label distinguish metrics defined and captured by different libraries from each other,
        /// ensuring that metrics defined by different development groups will fall under separate namespaces and not
        /// require category names to be globally unique across third party libraries linked by an application.
        /// Pick your own label which will uniquely identify your library or namespace.</remarks>
        string MetricsSystem { get; }

        /*
        /// <summary>
        /// The definitions collection that contains this definition.
        /// </summary>
        /// <remarks>This parent pointer should be used when walking from an object back to its parent instead of taking
        /// advantage of the static metrics definition collection to ensure your application works as expected when handling
        /// data that has been loaded from a database or data file.  The static metrics collection is for the metrics being
        /// actively captured in the current process, not for metrics that are being read or manipulated.</remarks>
        MetricDefinitionCollection Definitions { get; }

        /// <summary>
        /// The set of metrics that use this definition.
        /// </summary>
        /// <remarks>All metrics with the same definition are of the same object type.</remarks>
        MetricCollection Metrics { get; }
        */

        /// <summary>
        /// The category of this metric for display purposes. This can be a period delimited string to represent a variable height hierarchy.
        /// </summary>
        string CategoryName { get; }

        /// <summary>
        /// The display name of this metric (unique within the category name).
        /// </summary>
        string CounterName { get; }

        /// <summary>
        /// The sample type of the metric.  Indicates whether the metric represents discrete events or a continuous value.
        /// </summary>
        SampleType SampleType { get; }

        /// <summary>
        /// Indicates if the definition can still be changed or is read-only because an metric instance has been created from it.
        /// </summary>
        /// <remarks>If a metric definition is read-only, that means the definition can't be changed in a way that would invalidate
        /// metrics or metric samples recorded with it.  Display-only values (such as captions and descriptions) can always be changed,
        /// and new metrics can always be added to a metric definition.</remarks>
        bool IsReadOnly { get; }

        /*
        /// <summary>
        /// Indicates if the definition is part of the current live metric definition collection
        /// </summary>
        /// <remarks>The same process can be recording metrics and reading metrics from a data source such as a file.  This flag indicates
        /// whether this metric definition is for playback purposes (it represents previously recorded data) or is part of the active
        /// metric capture capability of the current process.</remarks>
        bool IsLive { get; }

        /// <summary>
        /// Invoked by the base class to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for the metric dictionary in your derived metric, use this
        /// method to create and return your derived object. 
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <returns>The MetricCollection-compatible object.</returns>
        MetricCollection OnMetricDictionaryCreate();

        /// <summary>
        /// Object Change Locking object.
        /// </summary>
        object Lock { get; }
        */

        /// <summary>
        /// The internal definition it wraps.
        /// </summary>
        MetricDefinition WrappedDefinition { get; }
    }
}