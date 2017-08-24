namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// One sample of a Event metric
    /// </summary>
    public interface IEventMetricSample : IMetricSample
    {
        /// <summary>
        /// Compute the resultant value for this sample compared with the provided baseline sample.
        /// </summary>
        /// <remarks>
        /// <para>The baseline sample must be for a date and time prior to this sample for correct results.</para>
        /// <para>If the supplied trendValue isn't trendable, the number of samples with a non-null value will be counted.</para>
        /// <para>If the supplied trendValue is trendable, the Default Trend (average or sum) will be calculated for all
        /// samples between the supplied baseline sample and this sample, inclusive.</para>
        /// </remarks>
        /// <param name="baselineSample">The previous baseline sample to calculate a difference for</param>
        /// <param name="trendValue">The definition of the value from this event metric to trend.</param>
        /// <returns>The calculated counter value</returns>
        double ComputeValue(IEventMetricSample baselineSample, IEventMetricValueDefinition trendValue);

        /// <summary>
        /// The array of values associated with this sample.  Any value may be a null object.
        /// </summary>
        object[] Values { get; }
    }
}
