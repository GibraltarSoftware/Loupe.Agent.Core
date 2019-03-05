namespace Loupe.Agent.PerformanceCounters
{
    /// <summary>
    /// Configuration information for performance monitoring
    /// </summary>
    public class PerformanceConfiguration
    {
        public PerformanceConfiguration()
        {
            EnableDiskMetrics = true;
            EnableMemoryMetrics = true;
            EnableNetworkMetrics = true;
            EnableProcessMetrics = true;
            EnableSystemMetrics = true;
        }

        /// <summary>
        /// When true, process performance information will be automatically captured for the current process
        /// </summary>
        /// <remarks>This includes basic information on processor and memory utilization for the running process.</remarks>
        public bool EnableProcessMetrics { get; set; }

        /// <summary>
        /// When true, disk performance information will be automatically captured
        /// </summary>
        public bool EnableDiskMetrics { get; set; }

        /// <summary>
        /// When true, extended .NET memory utilization information will be automatically captured
        /// </summary>
        /// <remarks>The extended information is primarily useful for narrowing down memory leaks.  Basic 
        /// memory utilization information (sufficient to identify if a leak is likely) is captured 
        /// as part of the EnableProcessPerformance option.</remarks>
        public bool EnableMemoryMetrics { get; set; }

        /// <summary>
        /// When true, network performance information will be automatically captured
        /// </summary>
        public bool EnableNetworkMetrics { get; set; }

        /// <summary>
        /// When true, system performance information will be automatically captured
        /// </summary>
        public bool EnableSystemMetrics { get; set; }
    }
}
