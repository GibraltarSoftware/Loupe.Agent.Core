using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// An interface for accessing log message data.
    /// </summary>
    /// <remarks>Most string properties of this interface (except where otherwise noted) will not return null values.</remarks>
    public interface ILogMessage : IComparable<ILogMessage>, IEquatable<ILogMessage>
    {
        /// <summary>
        /// The session this log message refers to
        /// </summary>
        ISession Session { get; }

        /// <summary>
        /// A Guid identifying this log message, unique across all sessions.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The sequence number assigned to this log message, unique within this session.
        /// </summary>
        long Sequence { get; }

        /// <summary>
        /// The timestamp of this log message.
        /// </summary>
        DateTimeOffset Timestamp { get; }

        /// <summary>
        /// The severity of this log message (from Critical to Verbose).
        /// </summary>
        /// <remarks>Severities have lower numerical values the more severe a message is,
        /// with Critical = 1 and Verbose = 16 enabling numerical comparison to capture
        /// a given severity and worse or better.  For example, Severity &lt; LogMessageSeverity.Warning
        /// will match Error and Critical.  The enumeration values are the same as those used in the
        /// .NET Trace subsystem for equivalent severities.</remarks>
        LogMessageSeverity Severity { get; }

        /// <summary>
        /// The log system which issued this log message.
        /// </summary>
        /// <remarks>Internally, Loupe generally uses &quot;Gibraltar&quot; for its own messages as well as those
        /// logged directly to the Log object, and &quot;Trace&quot; for messages captured via the .NET Trace subsystem.
        /// You can use your own value by using the Log.Write methods which are designed to enable forwarding messages
        /// from other log systems.</remarks>
        string LogSystem { get; }

        /// <summary>
        /// The dot-delimited hierarchical category for this log message.
        /// </summary>
        string CategoryName { get; }

        /// <summary>
        /// The user name associated with this log message (often just the user who started the process).
        /// </summary>
        /// <remarks>If user anonymization is enabled in configuration this will reflect the anonymous value.</remarks>
        string UserName { get; }

        /// <summary>
        /// The simple caption string for this log message.
        /// </summary>
        string Caption { get; }        

        /// <summary>
        /// The longer description for this log message.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The optional details XML for this log message (as a string).  (Or null if none.)
        /// </summary>
        string Details { get; }

        /// <summary>
        /// The name of the method which originated this log message, unless unavailable.  (Can be null or empty.)
        /// </summary>
        string MethodName { get; }

        /// <summary>
        /// The full name of the class containing this method which originated the log message, unless unavailable.  (Can be null or empty.)
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// The full path to the file containing this definition of the method which originated the log message, if available.  (Can be null or empty.)
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// The line number in the file at which the this message originated, if available.
        /// </summary>
        int LineNumber { get; }

        /// <summary>
        /// Whether or not this log message includes attached Exception information.
        /// </summary>
        bool HasException { get; }

        /// <summary>
        /// The information about any Exception attached to this log message.  (Or null if none.)
        /// </summary>
        IExceptionInfo Exception { get; }

        /// <summary>
        /// The Managed Thread Id of the thread which originated this log message.
        /// </summary>
        /// <remarks>This is not the operating system thread Id as managed threads do not necessarily
        /// correspond to OS threads.</remarks>
        int ThreadId { get; }

        /// <summary>
        /// The name of the thread which originated this log message.
        /// </summary>
        string ThreadName { get; }

        /// <summary>
        /// The application domain identifier of the app domain which originated this log message.
        /// </summary>
        int DomainId { get; }

        /// <summary>
        /// The friendly name of the app domain which originated this log message.
        /// </summary>
        string DomainName { get; }

        /// <summary>
        /// Indicates whether the thread which originated this log message is a background thread.
        /// </summary>
        bool IsBackground { get; }

        /// <summary>
        /// Indicates whether the thread which originated this log message is a Thread Pool thread.
        /// </summary>
        bool IsThreadPoolThread { get; }

        /// <summary>
        /// Indicates if the log message has related thread information.  If false, some calls to thread information may throw exceptions.
        /// </summary>
        bool HasThreadInfo { get; }

        /// <summary>
        /// Indicates if the class name and method name are available.
        /// </summary>
        bool HasMethodInfo { get; }

        /// <summary>
        /// Indicates if the file name and line number are available.
        /// </summary>
        bool HasSourceLocation { get; }
    }
}