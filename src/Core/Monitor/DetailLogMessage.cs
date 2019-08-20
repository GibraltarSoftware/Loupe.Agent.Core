
using System;
using System.Runtime.CompilerServices;
using Loupe.Extensibility.Data;
using Loupe.Logging;


namespace Loupe.Monitor
{
    /// <summary>
    /// An intermediary class to log a Loupe log message including an XML details string. 
    /// </summary>
    /// <remarks>This class knows how to formulate our most advanced log message format including an XML details string.
    /// Importantly, it knows how to acquire information about the source of a log message from the current call stack,
    /// and acts as its own IMessageSourceProvider when handing it off to the central Log.  Thus, this object must be
    /// created while still within the same call stack as the origination of the log message.
    /// Used internally by our external Loupe log API.</remarks>
    public class DetailLogMessage : LogMessageBase
    {
        /// <summary>
        /// Creates a DetailLogMessage object with default LogWriteMode behavior and an XML details string.
        /// </summary>
        /// <remarks>This constructor creates a DetailLogMessage with the default LogWriteMode behavior (Queued)
        /// and a specified XML details string (which may be null).</remarks>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public DetailLogMessage(LogMessageSeverity severity, string logSystem, string categoryName, int skipFrames,
                                string detailsXml, string caption, string description, params object[] args)
            : this(severity, LogWriteMode.Queued, logSystem, categoryName, skipFrames + 1, detailsXml, caption,
                   description, args)
        {
        }

        /// <summary>
        /// Creates a DetailLogMessage object with specified LogWriteMode behavior and an XML details string.
        /// </summary>
        /// <remarks>This constructor creates a DetailLogMessage with specified LogWriteMode behavior (queue-and-return
        /// or wait-for-commit) and XML details string (which may be null).</remarks>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public DetailLogMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem, string categoryName,
                                int skipFrames, string detailsXml, string caption, string description, params object[] args)
            : this(severity, writeMode, logSystem, categoryName, skipFrames + 1, null, false, detailsXml, caption,
                   description, args)
        {
        }

        /// <summary>
        /// Creates a DetailLogMessage object with specified LogWriteMode behavior, Exception object, and XML details string.
        /// </summary>
        /// <remarks>This constructor creates a DetailLogMessage with specified LogWriteMode behavior (queue-and-return
        /// or wait-for-commit), a specified Exception object to attach, and XML details string (which may be null).</remarks>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="exception">An exception associated with this log message (or null for none).</param>
        /// <param name="attributeToException">True if the call stack from where the exception was thrown should be used for log message attribution</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public DetailLogMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem, string categoryName,
                                int skipFrames, Exception exception, bool attributeToException, string detailsXml, string caption,
                                string description, params object[] args)
            : base(severity, logSystem, categoryName, skipFrames + 1, false, attributeToException, exception)
        {
            WriteMode = writeMode;
            Exception = exception;
            DetailsXml = detailsXml;

            Caption = caption; // Allow null, or should we force it to string.Empty?  Null will split it within Description.
            Description = description;
            MessageArgs = args;
        }
    }
}
