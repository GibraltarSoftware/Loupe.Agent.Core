using System;
using System.Collections.Generic;
using System.Text;

namespace Loupe.Configuration
{
    /// <summary>
    /// The configuration for a background monitor for Loupe
    /// </summary>
    public interface IMonitorConfiguration
    {
        /// <summary>
        /// When false, the monitor is disabled even if otherwise configured.
        /// </summary>
        /// <remarks>This allows for explicit disable/enable without removing the existing configuration
        /// or worrying about the default configuration.</remarks>
        bool Enabled { get; }

        /// <summary>
        /// The fully qualified name of the class that implements IMonitor for this configuration
        /// </summary>
        string MonitorTypeName { get; }
    }
}
