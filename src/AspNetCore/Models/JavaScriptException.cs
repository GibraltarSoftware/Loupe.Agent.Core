#pragma warning disable 1591

using System.Collections.Generic;
using System.ComponentModel;

namespace Loupe.Agent.AspNetCore.Models
{
    /// <summary>
    /// Defines a JavaScript exception
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class JavaScriptException : System.Exception
    {
        /// <summary>
        /// Create a new exception
        /// </summary>
        /// <param name="message">The exception message</param>
        public JavaScriptException(string message) : base(message) { }

        /// <summary>
        /// Create a new exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="stackTrace">The stack trace, as a list of strings</param>
        public JavaScriptException(string message, IEnumerable<string>? stackTrace) : base(message)
        {
            if (stackTrace != null)
            {
                StackTrace = string.Join("\r  ", stackTrace);
            }
        }

        /// <summary>
        /// Show the stack trace for the exception
        /// </summary>
        public override string? StackTrace { get; }
    }
}