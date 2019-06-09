using System.Diagnostics;
using Gibraltar.Monitor;

namespace Loupe.Agent.Core.Services
{
    /// <summary>
    /// Interface for the fluent Loupe Agent builder.
    /// </summary>
    public interface ILoupeAgentBuilder
    {
        /// <summary>
        /// Adds a <see cref="DiagnosticSource"/> listener.
        /// </summary>
        /// <typeparam name="T">The type of the listener, which must implement <see cref="ILoupeDiagnosticListener"/>.</typeparam>
        /// <returns>The builder instance.</returns>
        ILoupeAgentBuilder AddListener<T>() where T : class, ILoupeDiagnosticListener;

        /// <summary>
        /// Adds a <see cref="ILoupeMonitor" /> monitor.
        /// </summary>
        /// <typeparam name="T">The type of the monitor, which must implement <see cref="ILoupeMonitor"/>.</typeparam>
        /// <returns>The builder instance.</returns>
        ILoupeAgentBuilder AddMonitor<T>() where T : class, ILoupeMonitor;
    }
}