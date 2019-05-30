using System.Diagnostics;

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
    }
}