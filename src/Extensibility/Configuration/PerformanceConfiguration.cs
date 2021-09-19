using System.Text;
using Loupe.Configuration;

namespace Loupe.Configuration
{
    /// <summary>
    /// Configuration information for performance monitoring
    /// </summary>
    public class PerformanceConfiguration
    {
        /// <summary>
        /// Initialize a default performance monitoring configuration
        /// </summary>
        public PerformanceConfiguration()
        {
            Enabled = true;
            EnableDiskMetrics = true;
            EnableMemoryMetrics = true;
            EnableNetworkMetrics = true;
            EnableProcessMetrics = true;
            EnableSystemMetrics = true;
        }

        /// <summary>
        /// Enable or disable loading the performance monitor
        /// </summary>
        public bool Enabled { get; set; }

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

        /// <inheritdoc />
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("\tEnabled: {0}\r\n", Enabled);
            stringBuilder.AppendFormat("\tEnable Process Metrics: {0}\r\n", EnableProcessMetrics);
            stringBuilder.AppendFormat("\tEnable Disk Metrics: {0}\r\n", EnableDiskMetrics);
            stringBuilder.AppendFormat("\tEnable Memory Metrics: {0}\r\n", EnableMemoryMetrics);
            stringBuilder.AppendFormat("\tEnable Network Metrics: {0}\r\n", EnableNetworkMetrics);
            stringBuilder.AppendFormat("\tEnable System Metrics: {0}\r\n", EnableSystemMetrics);

            return stringBuilder.ToString();
        }
    }
}
