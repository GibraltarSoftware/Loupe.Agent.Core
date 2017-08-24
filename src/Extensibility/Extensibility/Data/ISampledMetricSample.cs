namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// One sample of a sampled metric
    /// </summary>
    public interface ISampledMetricSample : IMetricSample
    {
        /// <summary>
        /// Compute the resultant value for this sample.
        /// </summary>
        /// <remarks>
        /// This override is only useful if RequiresMultipleSamples is false.  In all other cases, you need to first
        /// identify a baseline sample to compare this sample with to determine the final value.
        /// </remarks>
        /// <returns>The calculated counter value</returns>
        double ComputeValue();

        /// <summary>
        /// Compute the resultant value for this sample compared with the provided baseline sample
        /// </summary>
        /// <remarks>
        /// The baseline sample must be for a date and time prior to this sample for correct results.
        /// </remarks>
        /// <param name="baselineSample">The previous baseline sample to calculate a difference for</param>
        /// <returns>The calculated counter value</returns>
        double ComputeValue(ISampledMetricSample baselineSample);

        /// <summary>
        /// Indicates whether two samples are required to calculate a metric value or not. 
        /// </summary>
        /// <remarks>
        /// Only a few counter types - notably the NumberOfItems types - are in their final form
        /// from a single sample.  All others require two samples to compare.
        /// </remarks>
        bool RequiresMultipleSamples { get; }
    }
}
