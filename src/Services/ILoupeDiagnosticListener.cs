using System.Diagnostics;

namespace Loupe.Agent.Core.Services
{
    /// <summary>
    /// Interface to be implemented by any <see cref="DiagnosticSource"/> listeners.
    /// </summary>
    public interface ILoupeDiagnosticListener
    {
        /// <summary>
        /// Returns the name of the <see cref="DiagnosticSource"/> this implementation targets.
        /// </summary>
        string Name { get; }
    }
}