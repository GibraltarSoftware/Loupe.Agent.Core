using System;
using System.Runtime.CompilerServices;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Loupe.Core.Logging
{
    /// <summary>
    /// An intermediary class to log a Loupe log message from within Loupe libraries. 
    /// </summary>
    /// <remarks>This class knows how to formulate a Loupe log message including an optional XML details string and
    /// optionally including an associated Exception object.  Importantly, it knows how to acquire information about the
    /// source of a log message from the current call stack without filtering out stack frames to attribute the message
    /// within Loupe libraries, and acts as its own IMessageSourceProvider when handing it off to the central Log.
    /// Thus, this object must be created while still within the same call stack as the origination of the log message.
    /// Used internally by our CLR listener (etc).</remarks>
    internal class LocalLogMessage : LogMessageBase
    {
        /// <summary>
        /// Creates a LocalLogMessage object with default LogWriteMode behavior and an XML details string.
        /// </summary>
        /// <remarks>This constructor creates a LocalLogMessage with the default LogWriteMode behavior (Queued)
        /// and a specified XML details string (which may be null).  This constructor also allows the log message
        /// to be of local origin, so Loupe stack frames will not be automatically skipped over when determining
        /// the originator for internally-issued log messages.</remarks>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multiline description to use which can be a format string for for the arguments.  Can be null.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal LocalLogMessage(LogMessageSeverity severity, string logSystem, string categoryName, int skipFrames,
                                 string detailsXml, string caption, string description, params object[] args)
            : this(severity, LogWriteMode.Queued, logSystem, categoryName, skipFrames + 1, detailsXml, caption,
                   description, args)
        {
        }

        /// <summary>
        /// Creates a LocalLogMessage object with specified LogWriteMode behavior and an XML details string.
        /// </summary>
        /// <remarks>This constructor creates a LocalLogMessage with specified LogWriteMode behavior (queue-and-return
        /// or wait-for-commit) and XML details string (which may be null).  This constructor also allows the log message
        /// to be of local origin, so Loupe stack frames will not be automatically skipped over when determining
        /// the originator for internally-issued log messages.</remarks>
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
        /// <param name="description">Optional.  A multiline description to use which can be a format string for for the arguments.  Can be null.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal LocalLogMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem, string categoryName,
                                 int skipFrames, string detailsXml, string caption, string description, params object[] args)
            : this(severity, writeMode, logSystem, categoryName, skipFrames + 1, null, false, detailsXml, caption, description, args)
        {
        }

        /// <summary>
        /// Creates a LocalLogMessage object with specified LogWriteMode behavior, Exception object, and XML details string.
        /// </summary>
        /// <remarks>This constructor creates a LocalLogMessage with specified LogWriteMode behavior (queue-and-return
        /// or wait-for-commit), a specified Exception object to attach, and XML details string (which may be null).
        /// This constructor also allows the log message to be of local origin, so Loupe stack frames will not be
        /// automatically skipped over when determining the originator for internally-issued log messages.</remarks>
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
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multiline description to use which can be a format string for for the arguments.  Can be null.</param>
        /// <param name="args">Optional additional args to match up with the formatting string.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal LocalLogMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem, string categoryName,
                                 int skipFrames, Exception exception, bool attributeToException, string detailsXml, string caption,
                                 string description, params object[] args)
            : base(severity, logSystem, categoryName, skipFrames + 1, true, attributeToException, exception)
        {
            WriteMode = writeMode;
            Exception = exception;
            DetailsXml = detailsXml;

            Caption = caption; // Allow null, which will split it within Description.
            Description = description;
            MessageArgs = args;
        }
    }
}
