using System;
using System.Runtime.CompilerServices;
using Loupe.Core.Monitor;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Loupe.Core.Logging
{
    /// <summary>
    /// Base class for log message template classes. 
    /// </summary>
    /// <remarks>This class knows how to translate from a simple logging API like Trace into our more all-encompassing
    /// Loupe Log collector.  Importantly, it knows how to acquire information about the source of a log message
    /// from the current call stack, and acts as its own IMessageSourceProvider when handing it off to the central Log.
    /// Thus, this object must be created while still within the same call stack as the origination of the log message.
    /// Used internally by our Trace Listener and external Loupe log API.</remarks>
    public abstract class LogMessageBase
    {
        private readonly LogMessageSeverity m_Severity;
        private readonly string m_LogSystem;
        private readonly string m_CategoryName;
        private readonly IMessageSourceProvider m_MessageSourceProvider;

        private string m_Caption;
        private string m_Description;
        private object[] m_MessageArgs;
        private string m_DetailsXml;
        private Exception m_Exception;
        private LogWriteMode m_WriteMode; // queued-and-return or wait-for-commit
        private bool m_AttributeToException;

        /// <summary>
        /// Base constructor for log message template classes.
        /// </summary>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        protected LogMessageBase(LogMessageSeverity severity, string logSystem, string categoryName)
        {
            m_Severity = severity;
            m_LogSystem = logSystem;
            m_CategoryName = categoryName;
        }

        /// <summary>
        /// Base constructor for log message template classes where the message should be attributed to the exception.
        /// </summary>
        /// <param name="severity">The severity of the log message.</param>
        /// <param name="logSystem">The name of the logging system the message was issued through, such as "Trace" or
        /// "Loupe".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        /// <param name="localOrigin">True if logging a message originating in Loupe code.
        /// False if logging a message from the client application.</param>
        /// <param name="attributeToException">True if the call stack from where the exception was thrown should be used for log message attribution</param>
        /// <param name="exception">When attributeToException is used, this exception object is used to determine the calling location</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected LogMessageBase(LogMessageSeverity severity, string logSystem, string categoryName, int skipFrames, bool localOrigin, bool attributeToException, Exception exception)
        {
            m_Severity = severity;
            m_LogSystem = logSystem;
            m_CategoryName = categoryName;

            if (attributeToException && (ReferenceEquals(exception, null) == false))
            {
                //try to use the exception as the source provider..
                var exceptionSourceProvider = new ExceptionSourceProvider(exception);
                if (string.IsNullOrEmpty(exceptionSourceProvider.ClassName) == false)
                {
                    //yep, we found something.
                    m_MessageSourceProvider = exceptionSourceProvider;
                }
            }

            m_MessageSourceProvider = m_MessageSourceProvider ?? new MessageSourceProvider(skipFrames + 1, localOrigin);
        }

        #region Public Property Accessors

        /// <summary>
        /// The severity of the log message.
        /// </summary>
        public LogMessageSeverity Severity { get { return m_Severity; } }

        /// <summary>
        /// The name of the logging system the message was issued through, such as "Trace" or "Loupe".
        /// </summary>
        public string LogSystem { get { return m_LogSystem; } }

        /// <summary>
        /// The logging category or application subsystem category that the log message is associated with,
        /// such as "Trace", "Console", "Exception", or the logger name in Log4Net.
        /// </summary>
        public string CategoryName { get { return m_CategoryName; } }

        /// <summary>
        /// A single line display caption.  It will not be format-expanded.
        /// </summary>
        public string Caption
        {
            get { return m_Caption; }
            protected set { m_Caption = value; }
        }

        /// <summary>
        /// Optional.  A multiline description to use which can be a format string for for the arguments.  Can be null.
        /// </summary>
        public string Description
        {
            get { return m_Description; }
            protected set { m_Description = value; }
        }

        /// <summary>
        /// Optional additional args to match up with the formatting string.
        /// </summary>
        public object[] MessageArgs
        {
            get { return m_MessageArgs; }
            protected set { m_MessageArgs = value; }
        }

        /// <summary>
        /// Optional.  An XML document with extended details about the message.  Can be null.
        /// </summary>
        public string DetailsXml
        {
            get { return m_DetailsXml; }
            protected set { m_DetailsXml = value; }
        }

        /// <summary>
        /// An exception associated with this log message (or null for none).
        /// </summary>
        public Exception Exception
        {
            get { return m_Exception; }
            protected set { m_Exception = value; }
        }

        /// <summary>
        /// Record this log message based on where the exception was thrown, not where this method was called
        /// </summary>
        public bool AttributeToException
        {
            get { return m_AttributeToException; }
            protected set { m_AttributeToException = value; }
        }

        /// <summary>
        /// Whether to queue-and-return or wait-for-commit.
        /// </summary>
        public LogWriteMode WriteMode
        {
            get { return m_WriteMode; }
            protected set { m_WriteMode = value; }
        }

#endregion

        /// <summary>
        /// This static helper method looks through an array of objects (eg. the param args) for the first Exception.
        /// </summary>
        /// <param name="args">An array of objects which might or might not contain an Exception.</param>
        /// <returns>The first element of the array which is an Exception (or derived from Exception),
        /// or null if none is found.</returns>
        protected static Exception FirstException(object[] args)
        {
            if (args == null)
            {
                return null;
            }

            for (int i = 0; i < args.Length; i++)
            {
                Exception exception = args[i] as Exception;
                if (!ReferenceEquals(exception, null))
                {
                    return exception;
                }
            }

            return null;
        }
            
        /// <summary>
        /// Publish this SimpleLogMessage to the Loupe central log.
        /// </summary>
        public void PublishToLog()
        {
            // We pass a null for the user name so that Log.WriteMessage() will figure it out for itself.
            Log.WriteMessage(m_Severity, m_WriteMode, m_LogSystem, m_CategoryName, m_MessageSourceProvider, null,
                             m_Exception, m_DetailsXml, m_Caption, m_Description, m_MessageArgs);
        }
    }
}
