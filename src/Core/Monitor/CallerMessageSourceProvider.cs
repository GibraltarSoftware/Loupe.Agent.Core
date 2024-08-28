using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Determines the source of a log message by using the compile-time creator of the class.
    /// </summary>
    public class CallerMessageSourceProvider : IMessageSourceProvider
    {

        /// <summary>
        /// Creates a MessageSourceProvider object to be used as an IMessageSourceProvider.
        /// </summary>
        /// <param name="caller">The object logging the message.</param>
        /// <param name="methodName">The simple name of the method which issued the log message.</param>
        /// <param name="fileName">The name of the file containing the method which issued the log message.</param>
        /// <param name="lineNumber">The line within the file at which the log message was issued.</param>
        /// <remarks>This constructor is used only for the convenience of the Log class when it needs to generate
        /// an IMessageSourceProvider for construction of internally-generated packets without going through the
        /// usual direct PublishToLog() mechanism.</remarks>
        public CallerMessageSourceProvider(object caller, [CallerMemberName] string methodName = null,
            [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            MethodName = methodName;
            ClassName = caller.GetType().FullName;
            FileName = fileName;
            LineNumber = lineNumber;
        }

        #region IMessageSourceProvider properties

        /// <summary>
        /// The simple name of the method which issued the log message.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// The full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        public string ClassName { get; }

        /// <summary>
        /// The name of the file containing the method which issued the log message.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// The line within the file at which the log message was issued.
        /// </summary>
        public int LineNumber { get; }

        #endregion
    }
}
