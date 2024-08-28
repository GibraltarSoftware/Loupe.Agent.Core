using System;
using System.Threading;

namespace Gibraltar
{
    /// <summary>
    /// Execute an asynchronous execution task
    /// </summary>
    public interface IAsynchronousTask : IDisposable
    {
        /// <summary>
        /// Execute the requested delegate asynchronously with the specified arguments.
        /// </summary>
        /// <remarks>A progress dialog is displayed after a few moments and updated asynchronously as the task continues.  If the user
        /// elects ot cancel the task, execution attempts to stop immediately and True is returned indicating the user canceled.</remarks>
        /// <param name="callBack">The method to be executed asynchronously</param>
        /// <param name="title">An end-user display title for this task.</param>
        /// <param name="state">Arguments to pass to the callBack delegate</param>
        void Execute(WaitCallback callBack, string title, object state);

        /// <summary>
        /// Execute the requested delegate asynchronously with the specified arguments.
        /// </summary>
        /// <remarks>A progress dialog is displayed after a few moments and updated asynchronously as the task continues.  If the user
        /// elects to cancel the task, execution attempts to stop immediately and True is returned indicating the user canceled.</remarks>
        /// <param name="callBack">The method to be executed asynchronously</param>
        /// <param name="arguments">The arguments to be passed to the asynchronous execution method</param>
        /// <returns>True if the user canceled, false otherwise.</returns>
        bool Execute(WaitCallback callBack, AsyncTaskArguments arguments);

        /// <summary>
        /// Execute the requested delegate asynchronously with the specified arguments.
        /// </summary>
        /// <remarks>A progress dialog is displayed after a few moments and updated asynchronously as the task continues.  If the user
        /// elects to cancel the task, execution attempts to stop immediately and True is returned indicating the user canceled.</remarks>
        /// <param name="callBack">The method to be executed asynchronously</param>
        /// <param name="arguments">The arguments to be passed to the asynchronous execution method</param>
        /// <param name="cancelable">Indicates if the operation supports canceling</param>
        /// <returns>True if the user canceled, false otherwise.</returns>
        bool Execute(WaitCallback callBack, AsyncTaskArguments arguments, bool cancelable);
    }
}
