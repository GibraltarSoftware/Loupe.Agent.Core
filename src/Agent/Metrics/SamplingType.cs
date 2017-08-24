namespace Gibraltar.Agent.Metrics
{
    /// <summary>
    /// Determines what the raw data for a given sampled metric is, and how it has to be processed to produce final data point values for display.
    /// </summary>
    /// <remarks>
    /// 	<para>In many cases it is necessary to store raw facts that are translated into the
    ///     final display value during the display process so that they work regardless of time
    ///     resolution.</para>
    /// 	<para>For example, to determine the percentage of processor time used for an
    ///     activity, you need to know a time interval to look across (say per second, per
    ///     hour, etc.), how many units of work were possible during that interval (time slices
    ///     of the processor) and how many were used by the process. By specifying the
    ///     TotalFraction type, the metric display system will automatically inspect the raw
    ///     and baseline values then translate them into a percentage.</para>
    /// 	<para>For more information on how to design sampled metrics including picking a
    ///     Sampling Type, see <a href="Metrics_SampledMetricDesign.html">Developer's Reference
    ///     - Metrics - Designing Sampled Metrics</a>.</para>
    /// 	<para>This enumeration is conceptually similar to the Performance Counter Type
    ///     enumeration provided by the runtime, but has been simplified for easier use.</para>
    /// </remarks>
    /// <seealso cref="!:Metrics_SampledMetricDesign.html" cat="Developer's Reference">Metrics - Designing Sampled Metrics</seealso>
    /// <seealso cref="SampledMetricDefinition" cat="Related Classes">SampledMetricDefinition Class</seealso>
    /// <seealso cref="SampledMetric" cat="Related Classes">SampledMetric Class</seealso>
    public enum SamplingType
    {
        /// <summary>
        /// Each sample is the raw value for display as this data point.
        /// </summary>
        RawCount = Monitor.MetricSampleType.RawCount,

        /// <summary>
        /// Each sample has the raw numerator and denominator of a fraction for display as the value for this data point. 
        /// </summary>
        RawFraction = Monitor.MetricSampleType.RawFraction,

        /// <summary>
        /// Each sample is the incremental change in the value for display as this data point.
        /// </summary>
        IncrementalCount = Monitor.MetricSampleType.IncrementalCount,

        /// <summary>
        /// Each sample has the separate incremental changes to the numerator and denominator of the fraction for display as this data point.
        /// </summary>
        IncrementalFraction = Monitor.MetricSampleType.IncrementalFraction,

        /// <summary>
        /// Each sample is the cumulative total of display value data points.
        /// </summary>
        TotalCount = Monitor.MetricSampleType.TotalCount,

        /// <summary>
        /// Each sample has the separate cumulative totals of the numerators and denominators of fraction value data points.
        /// </summary>
        TotalFraction = Monitor.MetricSampleType.TotalFraction,
    }
}
