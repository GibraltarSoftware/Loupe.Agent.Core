using System;
using System.Collections.Generic;
using System.Text;
using Gibraltar.Messaging;
using Loupe.Configuration;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Extends Loupe to monitor external data sources in the background
    /// </summary>
    public interface IMonitor : IEquatable<IMonitor>, IDisposable
    {
        /// <summary>
        /// A display caption for this monitor
        /// </summary>
        /// <remarks>End-user display caption for this monitor.  Captions are typically
        /// not unique to a given instance of a monitor.</remarks>
        string Caption { get; }

        /// <summary>
        /// Called by the publisher every time the configuration has been updated.
        /// </summary>
        /// <param name="configuration">The configuration block for this monitor</param>
        void ConfigurationUpdated(IMonitorConfiguration configuration);

        /// <summary>
        /// Initialize the monitor so it is ready to be polled.
        /// </summary>
        /// <param name="publisher">The publisher that owns the monitor</param>
        /// <param name="configuration">The configuration block for this monitor</param>
        void Initialize(Publisher publisher, IMonitorConfiguration configuration);

        /// <summary>
        /// Poll external data sources and record information
        /// </summary>
        void Poll();
    }
}
