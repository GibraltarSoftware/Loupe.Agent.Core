using System;
using Loupe.Messaging;

namespace Loupe.Monitor
{
    /// <summary>
    /// Extends Loupe to monitor external data sources in the background
    /// </summary>
    public interface ILoupeMonitor : IEquatable<ILoupeMonitor>, IDisposable
    {
        /// <summary>
        /// A display caption for this monitor
        /// </summary>
        /// <remarks>End-user display caption for this monitor.  Captions are typically
        /// not unique to a given instance of a monitor.</remarks>
        string Caption { get; }

        /// <summary>
        /// Initialize the monitor so it is ready to be polled.
        /// </summary>
        /// <param name="publisher">The publisher that owns the monitor</param>
        void Initialize(Publisher publisher);

        /// <summary>
        /// Poll external data sources and record information
        /// </summary>
        void Poll();
    }
}
