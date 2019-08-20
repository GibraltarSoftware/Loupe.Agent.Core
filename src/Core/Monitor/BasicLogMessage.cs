
using System;
using System.Runtime.CompilerServices;
using Loupe.Extensibility.Data;
using Loupe.Logging;


namespace Loupe.Monitor
{
    /// <summary>
    /// An intermediary class to log a basic log message for the Loupe API. 
    /// </summary>
    /// <remarks>This class knows how to formulate a log message for the basic Loupe API, including a caption and
    /// formatted description.  Importantly, it knows how to acquire information about the source of a log message
    /// from the current call stack, and acts as its own IMessageSourceProvider when handing it off to the central Log.
    /// Thus, this object must be created while still within the same call stack as the origination of the log message.
    /// It can also scan the format args for the first Exception object, or an Exception object to attach (or null) may be
    /// specified directly.  Used by our external Loupe log API.</remarks>
    public class BasicLogMessage : LogMessageBase
    {
        /// <summary>
        /// Creates a BasicLogMessage object with default LogWriteMode behavior and no attached Exception.
        /// </summary>
        /// <remarks>This constructor creates a BasicLogMessage with the default LogWriteMode behavior (Queued)
        /// and will also automatically look for any Exception passed among the params args and attach the first
        /// one found as the Exception object for this log message (if any).</remarks>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public BasicLogMessage(LogMessageSeverity severity, string logSystem, string categoryName, int skipFrames,
                               string caption, string description, params object[] args)
            : this(severity, LogWriteMode.Queued, logSystem, categoryName, skipFrames + 1, null, false,
                   caption, description, args)
        {
        }

        /// <summary>
        /// Creates a BasicLogMessage object with specified LogWriteMode behavior and no attached Exception.
        /// </summary>
        /// <remarks>This constructor creates a BasicLogMessage with a specified LogWriteMode behavior (queue-and-return
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
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public BasicLogMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem, string categoryName,
                               int skipFrames, string caption, string description, params object[] args)
            : this(severity, writeMode, logSystem, categoryName, skipFrames + 1, null, false, caption, description, args)
        {
        }

        /// <summary>
        /// Creates a BasicLogMessage object with specified LogWriteMode behavior and specified Exception object to attach.
        /// </summary>
        /// <remarks>This constructor creates a BasicLogMessage with a specified LogWriteMode behavior (queue-and-return
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
        /// <param name="attributeToException">True if the call stack from where the exception was thrown should be used for log message attribution</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public BasicLogMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem, string categoryName,
                               int skipFrames, Exception exception, bool attributeToException, string caption, string description, params object[] args)
            : base(severity, logSystem, categoryName, skipFrames + 1, false, attributeToException, exception)
        {
            WriteMode = writeMode;
            Exception = exception;
            DetailsXml = null;

            Caption = caption;
            Description = description;
            MessageArgs = args;
        }
    }
}
