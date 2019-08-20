using System;



namespace Loupe.Agent
{
    /// <summary>
    /// Information about the Package Send Events.
    /// </summary>
    public sealed class PackageSendEventArgs : EventArgs
    {
        internal PackageSendEventArgs(Loupe.Data.PackageSendEventArgs args)
        {
            FileSize = args.FileSize;
            Result = (AsyncTaskResult)args.Result;
            Message = args.Message;
            Exception = args.Exception;
        }

        /// <summary>
        /// The final status of the task.
        /// </summary>
        public AsyncTaskResult Result { get; private set; }

        /// <summary>
        /// Optional.  An end-user display message to complement the result.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Optional.  An exception object to allow custom interpretation of an exception.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// The number of bytes in the package, if sent successfully.
        /// </summary>
        public int FileSize { get; private set; }
    }

}
