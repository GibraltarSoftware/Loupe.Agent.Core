
using System;
using Microsoft.Extensions.Logging;



namespace Gibraltar.Agent
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
        None = 0,  // FxCop demands we have a defined 0.

        /// <summary>
        /// The task was canceled before it could complete or fail to complete.
        /// </summary>
        Canceled = 1,

        /// <summary>
        /// The task failed.
        /// </summary>
        Error = LogLevel.Error,

        /// <summary>
        /// The task at least partially succeeded but there was a noncritical problem.
        /// </summary>
        Warning = LogLevel.Warning, 

        /// <summary>
        /// The task succeeded but generated an informational message.
        /// </summary>
        Information = LogLevel.Information, 

        /// <summary>
        /// The task succeeded completely.
        /// </summary>
        Success = LogLevel.Debug,
    }
}
