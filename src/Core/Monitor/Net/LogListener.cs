using System;
using System.Diagnostics;
using System.Text;
using Loupe.Configuration;
using Loupe.Core.Logging;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Loupe.Core.Monitor.Net
{
    /// <summary>
    /// Implements a TraceListener to forward Trace and Debug messages from
    /// System.Diagnostics.Trace to the Loupe Agent.
    /// </summary>
    /// <remarks>
    /// 	<para>This class is not normally used directly but instead is registered in the
    ///     App.Config file according to the example above.</para>
    /// 	<para>
    ///         For more information on how to record log messages with the Loupe Agent,
    ///         see the <see cref="Log">Log Class</see>.
    ///     </para>
    /// 	<para>For more information on logging using Trace, see <a href="Logging_Trace.html">Developer's Reference - Logging - Using with Trace</a>.</para>
    /// </remarks>
    /// <seealso cref="!:Logging_Trace.html" cat="Developer's Reference">Logging - Using with Trace</seealso>
    /// <example>
    /// 	<code lang="XML" title="Loupe Trace Listener Registration" description="The easiest way to add Loupe to an application is to register it in the App.Config XML file. Each application has an XML configuration file that is used to hold options that can be changed without recompiling the application. These options apply to all users of the application.">
    /// 		<![CDATA[
    /// <?xml version="1.0" encoding="utf-8" ?>
    /// <configuration>
    /// <!-- You may already have a <system.diagnostics> section in your configuration file,
    ///    possibly also including the <trace> and <listeners> tags.  If this is the case,
    ///    you only need to add the line that starts <add name="Loupe" ... /> to the file -->
    ///  <system.diagnostics>
    ///    <trace>
    ///      <listeners>
    ///        <add name="Loupe" type="Loupe.Agent.LogListener, Loupe.Agent"/>
    ///      </listeners>
    ///    </trace>
    ///  </system.diagnostics>
    /// </configuration>]]>
    /// 	</code>
    /// </example>
    public sealed class LogListener : TraceListener
    {
        [ThreadStatic]
        private static StringBuilder t_buffer; // Each thread has its own... (Must be initialized by each thread!)
        [ThreadStatic]
        private static int t_IndentSize; // set (per-thread) on the first write after a line has ended.
        [ThreadStatic]
        private static int t_IndentLevel; // set (per-thread) on the first write after a line has ended.
        [ThreadStatic]
        private static bool t_IndentSaved; // set (per-thread) when we save the Indent info.
        [ThreadStatic]
        private static string t_IndentSizeCache;
        [ThreadStatic]
        private static string t_IndentLevelCache;

        private static ListenerConfiguration m_Configuration;

        // The indent stuff needs to be saved so we can pass it along when we wrap up the message, but it's intended
        // effect is defined when the line *starts*.  TODO: How to handle manually-set NeedIndent and inline \n ???
        private const string LogSystem = "Trace";
        private const string LogListenerCategory = "Trace";

        /// <summary>
        /// Create a new instance of the log listener.
        /// </summary>
        /// <remarks>The log listener should be managed by the Listener class instead of being directly
        /// managed by trace - do not add to the Trace Listener through its normal registration.</remarks>
        public LogListener()
        {
            //we have to directly mark silent mode because we don't want to cause initialization during our constructor.
            Log.SilentMode = true;

            //note in this case we really will try to force the issue and initialize.
            Log.IsLoggingActive();
            m_Configuration = Log.Configuration.Listener;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Indicates if the trace listener is thread safe or not
        /// </summary>
        /// <remarks>The Loupe Log Listener is thread safe.</remarks>
        public override bool IsThreadSafe
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Writes trace and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            if (Log.IsLoggingActive() == false)
                return;

            string indent = ComputeIndentString(IndentLevel, IndentSize);

            SimpleLogMessage logMessage = new SimpleLogMessage((LogMessageSeverity)eventType, LogSystem,
                LogListenerCategory, 2, indent + "Event {0}", id);
            logMessage.PublishToLog();
        }

        /// <summary>
        /// Writes trace information, a message, and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="message">A message to write.</param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (Log.IsLoggingActive() == false)
                return;

            string indent = ComputeIndentString(IndentLevel, IndentSize);

            SimpleLogMessage logMessage = (id != 0)
                ? new SimpleLogMessage((LogMessageSeverity)eventType, LogSystem, LogListenerCategory, 2,
                    indent + "Event {0}: {1}", id, message)
                : new SimpleLogMessage((LogMessageSeverity)eventType, LogSystem, LogListenerCategory, 2,
                    indent + (message ?? string.Empty));

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Writes trace information, a formatted array of objects and event information to the listener specific output.
        /// </summary>
        /// <param name="eventCache">A TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="format">A format string that contains zero or more format items, which correspond to objects in the args array.</param>
        /// <param name="args">An object array containing zero or more objects to format.</param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, object[] args)
        {
            if (Log.IsLoggingActive() == false)
                return;

            string indent = ComputeIndentString(IndentLevel, IndentSize);

            SimpleLogMessage logMessage = (id != 0)
                ? new SimpleLogMessage((LogMessageSeverity)eventType, LogSystem, LogListenerCategory, 2,
                    string.Format("{2}Event {0}: {1}", id, format, indent), args)
                : new SimpleLogMessage((LogMessageSeverity)eventType, LogSystem, LogListenerCategory, 2,
                    indent + (format ?? string.Empty), args);
            logMessage.PublishToLog();
        }

        /// <summary>
        /// When overridden in a derived class, writes the specified message to the listener you create in the derived class.
        /// </summary>
        /// <param name="message">A message to write. </param><filterpriority>2</filterpriority>
        public override void Write(string message)
        {
            if (Log.IsLoggingActive() == false)
                return;

            bool needIndent = NeedIndent;
            WriteMessage(message, false, ref needIndent, IndentLevel, IndentSize);
            NeedIndent = needIndent;
        }

        /// <summary>
        /// When overridden in a derived class, writes a message to the listener you create in the derived class, followed by a line terminator.
        /// </summary>
        /// <param name="message">A message to write. </param><filterpriority>2</filterpriority>
        public override void WriteLine(string message)
        {
            if (Log.IsLoggingActive() == false)
                return;

            bool needIndent = NeedIndent;
            WriteMessage(message, true, ref needIndent, IndentLevel, IndentSize);
            NeedIndent = needIndent;
        }

        /// <summary>
        /// Flush the information to disk.
        /// </summary>
        public override void Flush()
        {
            if (Log.IsLoggingActive() == false)
                return;

            try
            {
                if (t_buffer == null) // If this is our first access by this thread...
                {
                    t_buffer = new StringBuilder(); // ...then initialize this thread's buffer.
                }

                if (t_buffer.Length > 0)
                {
                    bool needIndent = false;
                    WriteMessage(null, true, ref needIndent, 0, 0);

                }
            }
            catch (Exception exception)
            {
                // Don't let a log message cause problems
                GC.KeepAlive(exception);
            }
        }

        /// <summary>
        /// Close the listener because Trace is shutting down.
        /// </summary>
        public override void Close()
        {
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// When overridden in a derived class, records a thread-specific indentation.
        /// </summary>
        protected override void WriteIndent()
        {
            if (Log.IsLoggingActive() == false)
                return;

            WriteIndent(IndentLevel, IndentSize);
            NeedIndent = false;
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Called to write out an indention on the current thread's string buffer
        /// </summary>
        private static void WriteIndent(int indentLevel, int indentSize)
        {
            if (t_buffer == null) // If this is our first access by this thread...
            {
                t_buffer = new StringBuilder(); // ...then initialize this thread's buffer.
            }

            t_buffer.Append(ComputeIndentString(indentLevel, indentSize));

            if (t_IndentSaved == false)
            {
                // Only save these once per aggregated log message, to lock in their effect.
                t_IndentSize = indentSize;
                t_IndentLevel = indentLevel;
                t_IndentSaved = true;
#if STACK_DUMP
                t_debugException = new GibraltarStackInfoException("Trace - WriteIndent(): "+t_IndentSize+" * "+t_IndentLevel, t_debugException);
#endif
            }
#if STACK_DUMP
            else
            {
                t_debugException = new GibraltarStackInfoException("Trace - WriteIndent(): Ignored", t_debugException);
            }
#endif
        }
        private static string ComputeIndentString(int indentLevel, int indentSize)
        {
            if (indentSize <= 0 || indentLevel <= 0)
                return string.Empty; // Shortcut bail if size or level is 0 (or illegal negative).

            string powerString;
            string sizeString;
            string levelString;

            if (t_IndentSizeCache != null && t_IndentSizeCache.Length == indentSize)
            {
                sizeString = t_IndentSizeCache; // Use the cached string, already the right size.
            }
            else
            {
                // We have to compute it.
                t_IndentLevelCache = null; // Size changed.  Invalidate the indent level cache string.
                powerString = " ";
                sizeString = string.Empty;
                for (int size = indentSize; size > 1; size >>= 1)
                {
                    if ((size & 1) != 0)
                        sizeString += powerString;

                    powerString += powerString; // Double it for next higher bit.
                }
                sizeString += powerString; // There has to be a final 1 bit at the top, we weeded out 0.

                t_IndentSizeCache = sizeString; // Store this for reuse efficiency.
            }

            if (t_IndentLevelCache != null && t_IndentLevelCache.Length == (indentLevel * indentSize))
            {
                levelString = t_IndentLevelCache; // Use the cached string, already the right level & size.
            }
            else
            {
                powerString = sizeString;
                levelString = string.Empty;
                for (int level = indentLevel; level > 1; level >>= 1)
                {
                    if ((level & 1) != 0)
                        levelString += powerString;

                    powerString += powerString; // Double it for next higher bit.
                }
                levelString += powerString; // There has to be a final 1 bit at the top, we weeded out 0.

                t_IndentLevelCache = levelString; // Store this for reuse efficiency.
            }

            return levelString;
        }

        private static void WriteMessage(string message, bool endLine, ref bool needIndent, int indentLevel, int indentSize)
        {
            try
            {
                if (t_buffer == null) // If this is our first access by this thread...
                {
                    t_buffer = new StringBuilder(); // ...then initialize this thread's buffer.
                }

                if (needIndent)
                {
                    WriteIndent(indentLevel, indentSize);
                }

                if (string.IsNullOrEmpty(message) == false)
                {
                    t_buffer.Append(message);
                }

                if (endLine)
                {
                    //t_buffer.Append("<Line>");
#if STACK_DUMP
                    t_debugException = new GibraltarStackInfoException("Trace - WriteLine()", t_debugException);
                    Exception dumpException = t_debugException;
#else
                    const Exception dumpException = null;
#endif

                    // Then pull the contents of the buffer as a complete line into a log message.
                    SimpleLogMessage logMessage = new SimpleLogMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued,
                                                                       LogSystem, LogListenerCategory, 3, dumpException,
                                                                       t_buffer.ToString());
#if STACK_DUMP
                    t_debugException = null;
#endif

                    t_buffer.Length = 0; // Reset the buffer to empty again (do it here just in case publishing crashes).
                    t_IndentSaved = false; // Mark our indent info as no longer valid.
                    t_IndentLevel = 0; // Reset to 0's here so we don't have to check t_IndentSaved before using them above.
                    t_IndentSize = 0; // Should we leave this one, or reset it here, too?
                    needIndent = true;
                    logMessage.PublishToLog(); // And send the completed log message to the Log.
                }
#if STACK_DUMP
                else
                {
                    t_debugException = new GibraltarStackInfoException("Trace - Write()", t_debugException);
                }
#endif
            }
            catch (Exception)
            {
                // We generally just want to suppress all exceptions, but if we're actively debugging...

                Log.DebugBreak(); // Use our debugging breakpoint.  This is ignored in production.
            }
        }


        #endregion
    }
}
