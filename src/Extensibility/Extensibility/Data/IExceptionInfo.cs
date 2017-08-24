namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// An interface which provides recorded information about an Exception.
    /// </summary>
    public interface IExceptionInfo
    {
        /// <summary>
        /// The full name of the type of the Exception.
        /// </summary>
        string TypeName { get; }

        /// <summary>
        /// The Message string of the Exception.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// A formatted string describing the source of an Exception.
        /// </summary>
        string Source { get; }

        /// <summary>
        /// A string dump of the Exception stack trace information.
        /// </summary>
        string StackTrace { get; }

        /// <summary>
        /// The information about this exception's inner exception (or null if none).
        /// </summary>
        IExceptionInfo InnerException { get; }
    }
}