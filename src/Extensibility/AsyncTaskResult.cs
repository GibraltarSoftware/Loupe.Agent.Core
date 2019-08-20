using System;

namespace Loupe
{
    /// <summary>
    /// The result of processing an asynchronous task
    /// </summary>
    /// <remarks>For any value better than Error the task did complete its primary purpose.</remarks>
    [Flags]
    public enum AsyncTaskResult
    {
        /// <summary>
        /// The severity level is uninitialized and thus unknown.
        /// </summary>
        Unknown = 0,  // FxCop demands we have a defined 0.

        /// <summary>
        /// The task was canceled before it could complete or fail to complete.
        /// </summary>
        Canceled = 1,

        /// <summary>
        /// Recoverable error.
        /// </summary>
        /// <remarks>This is equal to TraceEventType.Error.</remarks>
        Error = 2, // = 2

        /// <summary>
        /// Noncritical problem.
        /// </summary>
        /// <remarks>This is equal to TraceEventType.Warning.</remarks>
        Warning = 4, // = 4

        /// <summary>
        /// Informational message.
        /// </summary>
        /// <remarks>This is equal to TraceEventType. Information</remarks>
        Information = 8, // = 8

        /// <summary>
        /// Debugging trace.
        /// </summary>
        /// <remarks>This is equal to TraceEventType.Verbose.</remarks>
        Success = 16, // = 16
    }
}
