using System;




namespace Gibraltar.Data
{
    /// <summary>
    /// Information about the Package Send Events.
    /// </summary>
    public class PackageSendEventArgs : AsyncTaskResultEventArgs
    {
        /// <summary>
        /// Create a new result arguments object from the provided information
        /// </summary>
        /// <param name="fileSize">The number of bytes in the package, if sent succesfully.</param>
        /// <param name="result">The final status of the task</param>
        /// <param name="message">Optional. A display message to complement the result.</param>
        /// <param name="exception">Optional. An exception object to allow the caller to do its own interpretation of an exception.</param>
        public PackageSendEventArgs(int fileSize, AsyncTaskResult result, string message, Exception exception)
            :base(result, message, exception)
        {
            FileSize = fileSize;
        }

        /// <summary>
        /// The number of bytes in the package, if sent succesfully.
        /// </summary>
        public int FileSize { get; private set; }
    }

}
