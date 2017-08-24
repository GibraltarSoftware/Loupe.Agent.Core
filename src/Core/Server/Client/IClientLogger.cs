using System;
using Loupe.Extensibility.Data;

namespace Gibraltar.Server.Client
{
    public interface IClientLogger
    {
        /// <summary>
        /// Indicates if only minimal logging should be performed
        /// </summary>
        bool SilentMode { get; }

        /// <summary>
        /// Write a trace message directly to the Gibraltar log.
        /// </summary>
        /// <remarks>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer
        /// callers.</remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        void Write(LogMessageSeverity severity, string category, string caption, string description,
            params object[] args);

        /// <summary>
        /// Write a log message directly to the Gibraltar log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True if the call stack from where the exception was thrown should be used for log message attribution</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        void Write(LogMessageSeverity severity, Exception exception, bool attributeToException, string category,
            string caption, string description, params object[] args);
    }
}
