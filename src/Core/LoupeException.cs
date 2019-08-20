using System;
using System.Diagnostics;



namespace Loupe
{
    /// <summary>
    /// Represents errors that occur within Loupe during application execution.
    /// </summary>
    /// <remarks>This is a base class for any new Exception types we define and for generic exceptions we
    /// generate.  Custom Exception types defined by Loupe should try to derive from LoupeException
    /// so that they could be caught as this base type.  This may not currently be consistent, however.
    /// For more information, see the root class, Exception.</remarks>
    public class LoupeException : Exception
    {
        // This is a dummy wrapper around generic exceptions (for now)

        // This also has the problem that although Common\Agent is accessible to all of Loupe code,
        // we hide it from view in Loupe.Agent.dll, thus preventing customer code from being able to reference it!
        // This will have to be reconsidered at some point.  However, we likely want to replace most or all
        // exceptions with our ErrorNotifier system, because we don't want Loupe.Agent.dll to break vital customer
        // applications just because they made some mistake in accessing our code.  So they likely won't see
        // any of these exceptions, anyway.

        #region Debugging assistance
        /// <summary>
        /// A temporary flag to tell us whether to invoke a Debugger.Break() on all of our exceptions.
        /// </summary>
        /// <remarks>True enables breakpointing, false disables.  This should probably be replaced with an enum
        /// to support multiple modes, assuming the basic usage works out.</remarks>
        // Note: The ReSharper complaint-disable comments can be removed once this is referenced for configuration elsewhere.
        // ReSharper disable ConvertToConstant
        private static bool s_BreakPointGibraltarExceptions = false; // Can be changed in the debugger
        // ReSharper restore ConvertToConstant

        /// <summary>
        /// Automatically stop debugger like a breakpoint, if enabled.
        /// </summary>
        /// <remarks>This will check the state of GibraltarExceptions.s_BreakPointGibraltarExceptions</remarks>
        [Conditional("DEBUG")]
        // ReSharper disable MemberCanBeMadeStatic
        private void BreakPoint() // Deliberately not static so that "this" exists when the breakpoint hits, for convenience.
            // ReSharper restore MemberCanBeMadeStatic
        {
            if (s_BreakPointGibraltarExceptions && Debugger.IsAttached)
            {
                Debugger.Break(); // Stop here only when debugging
                // ...then Shift-F11 as needed to step out to where it is getting created...
                // ...hopefully to the point just before it gets thrown.
            }
        }
        #endregion

        /// <summary>
        /// Initializes a new instance of the LoupeException class.
        /// </summary>
        /// <remarks>This constructor initializes the Message property of the new instance to a system-supplied
        /// message that describes the error and takes into account the current system culture.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeException()
        {
            // Just the base default constructor, except...
            BreakPoint();
        }
        
        /// <summary>
        /// Initializes a new instance of the LoupeException class with a specified error message.
        /// </summary>
        /// <param name="message">The error message string.</param>
        /// <remarks>This constructor initializes the Message property of the new instance using the
        /// message parameter.  The InnerException property is left as a null reference.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeException(string message)
            : base(message)
        {
            // Just the base constructor, except...
            BreakPoint();
        }

        /// <summary>
        /// Initializes a new instance of the LoupeException class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message string.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a
        /// null reference if no inner exception is specified.</param>
        /// <remarks>An exception that is thrown as a direct result of a previous exception should include
        /// a reference to the previous exception in the innerException parameter.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeException(string message, Exception innerException)
            : base(message, innerException)
        {
            // Just the base constructor, except...
            BreakPoint();
        }
    }
}