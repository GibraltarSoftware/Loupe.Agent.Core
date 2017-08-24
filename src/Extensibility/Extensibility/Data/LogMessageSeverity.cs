using System;

namespace Loupe.Extensibility.Data
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
        Unknown = 0,  // FxCop demands we have a defined 0.

        /// <summary>
        /// Fatal error or application crash.
        /// </summary>
        /// <remarks>This is equal to TraceEventType.Critical.  This also corresponds to Log4Net's Fatal.</remarks>
        Critical = 1,

        /// <summary>
        /// Recoverable error.
        /// </summary>
        /// <remarks>This is equal to TraceEventType.Error.  This also corresponds to Log4Net's Error.</remarks>
        Error = 2, // = 2

        /// <summary>
        /// Noncritical problem.
        /// </summary>
        /// <remarks>This is equal to TraceEventType.Warning.  This also corresponds to Log4Net's Warning.</remarks>
        Warning = 4, // = 4

        /// <summary>
        /// Informational message.
        /// </summary>
        /// <remarks>This is equal to TraceEventType. Information, This also corresponds to Log4Net's Information.</remarks>
        Information = 8, // = 8

        /// <summary>
        /// Debugging trace.
        /// </summary>
        /// <remarks>This is equal to TraceEventType.Verbose.  This also corresponds to Log4Net's Debug.</remarks>
        Verbose = 16, // = 16
    }
}
