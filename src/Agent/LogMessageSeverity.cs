using System;
using Microsoft.Extensions.Logging;

namespace Gibraltar.Agent
{
    /// <summary>
    /// This enumerates the severity levels used by Loupe log messages.
    /// </summary>
    /// <remarks>The values for these levels are chosen to directly map to the TraceEventType enum
    /// for the five levels we support.  These also can be mapped from Log4Net event levels,
    /// with slight name changes for Fatal->Critical and for Debug->Verbose.</remarks>
    [Flags]
    public enum LogMessageSeverity
    {
        /// <summary>
        /// The severity level is uninitialized and thus unknown.
        /// </summary>
        None = 0,  // FxCop demands we have a defined 0.

        /// <summary>
        /// Fatal error or application crash.
        /// </summary>
        Critical = LogLevel.Critical,

        /// <summary>
        /// Recoverable error.
        /// </summary>
        Error = LogLevel.Error,

        /// <summary>
        /// Noncritical problem.
        /// </summary>
        Warning = LogLevel.Warning,

        /// <summary>
        /// Informational message.
        /// </summary>
        Information = LogLevel.Information,

        /// <summary>
        /// Debugging trace.
        /// </summary>
        Verbose = LogLevel.Debug,
    }
}
