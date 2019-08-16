namespace Gibraltar.Monitor
{
    /// <summary>
    /// An interface by which conversion classes can provide the details of the source of a log message.
    /// </summary>
    /// <remarks>Unavailable fields may return null.</remarks>
    public interface IMessageSourceProvider
    {
        // Note: We don't support passing the originating threadId and rely on receiving log messages still on the same thread.

        /// <summary>
        /// Should return the simple name of the method which issued the log message.
        /// </summary>
        string MethodName { get; }

        /// <summary>
        /// Should return the full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// Should return the name of the file containing the method which issued the log message.
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// Should return the line within the file at which the log message was issued.
        /// </summary>
        int LineNumber { get; }
    }
}
