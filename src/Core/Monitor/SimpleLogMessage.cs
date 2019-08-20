
using System;
using System.Runtime.CompilerServices;
using Loupe.Extensibility.Data;
using Loupe.Logging;


namespace Loupe.Monitor
{
    /// <summary>
    /// An intermediary class to log a simple log message (as from Trace). 
    /// </summary>
    /// <remarks>This class knows how to translate from a simple logging API like Trace into our more all-encompassing
    /// Loupe Log collector.  Importantly, it knows how to acquire information about the source of a log message
    /// from the current call stack, and acts as its own IMessageSourceProvider when handing it off to the central Log.
    /// Thus, this object must be created while still within the same call stack as the origination of the log message.
    /// It can also scan the format args for the first Exception object, or an Exception object to attach (or null) may be
    /// specified directly.  Used internally by our Trace Listener and external Loupe log API.</remarks>
    public class SimpleLogMessage : LogMessageBase
    {
        /// <summary>
        /// Creates a SimpleLogMessage object with default LogWriteMode behavior and automatically scan for an Exception object among args.
        /// </summary>
        /// <remarks>This constructor creates a SimpleLogMessage with the default LogWriteMode behavior (Queued)
        /// and will also automatically look for any Exception passed among the params args and attach the first
        /// one found as the Exception object for this log message (if any).</remarks>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="message">A message string with optional formatting, which may span multiple lines.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public SimpleLogMessage(LogMessageSeverity severity, string logSystem, string categoryName, int skipFrames,
                                string message, params object[] args)
            : this(severity, LogWriteMode.Queued, logSystem, categoryName, skipFrames + 1, FirstException(args),
                   message, args)
        {
        }

        /// <summary>
        /// Creates a SimpleLogMessage object with specified LogWriteMode behavior and automatically scan for an Exception object among args.
        /// </summary>
        /// <remarks>This constructor creates a SimpleLogMessage with a specified LogWriteMode behavior (queue-and-return
        /// or wait-for-commit) and will also automatically look for any Exception passed among the params args
        /// and attach the first one found as the Exception object for this log message (if any).</remarks>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="message">A message string with optional formatting, which may span multiple lines.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public SimpleLogMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem, string categoryName,
                                int skipFrames, string message, params object[] args)
            : this(severity, writeMode, logSystem, categoryName, skipFrames + 1, FirstException(args), message, args)
        {
        }

        /// <summary>
        /// Creates a SimpleLogMessage object with specified LogWriteMode behavior and specified Exception object to attach.
        /// </summary>
        /// <remarks>This constructor creates a SimpleLogMessage with a specified LogWriteMode behavior (queue-and-return
        /// or wait-for-commit) and with a specified Exception object (which may be null) to attach to this log message.
        /// The format args will not be scanned for an Exception object by this overload.</remarks>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="exception">An exception associated with this log message (or null for none).</param>
        /// <param name="message">A message string with optional formatting, which may span multiple lines.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public SimpleLogMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem, string categoryName,
                                int skipFrames, Exception exception, string message, params object[] args)
            : base(severity, logSystem, categoryName, skipFrames + 1, false, false, null)
        {
            WriteMode = writeMode;
            Exception = exception;
            DetailsXml = null;

            Caption = null; // null signals that we need to split the description to find the caption when publishing.
            Description = message;
            MessageArgs = args;
        }
    }
}
