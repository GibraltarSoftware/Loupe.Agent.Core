
using System;
using Loupe.Core.Monitor;
using Loupe.Logging;


namespace Loupe.Core
{
    /// <summary>
    /// Serves as an IMessageSourceProvider to attribute a message to the code location which threw an Exception.
    /// </summary>
    /// <remarks>This class looks at the <see CREF="System.Diagnostics.StackTrace">StackTrace</see> of a thrown Exception,
    /// rather than the current call stack, to attribute a message to the code location which threw that Exception rather
    /// than to where the call is made to log the Exception.</remarks>
    public class ExceptionSourceProvider : IMessageSourceProvider
    {
        private readonly string m_MethodName;
        private readonly string m_ClassName;
        private readonly string m_FileName;
        private readonly int m_LineNumber;

        /// <summary>
        /// Construct an ExceptionSourceProvider based on a provided Exception.
        /// </summary>
        /// <remarks>The first (closest) stack frame of the first (outer) Exception will be taken as the
        /// originator of a log message using this as its IMessageSourceProvider.</remarks>
        /// <param name="exception">The Exception whose first stack frame is the declared originator.</param>
        public ExceptionSourceProvider(Exception exception)
        {
            // We never skipped Loupe frames here, so go ahead with trustSkipFrames = true to disable that check.
            CommonCentralLogic.FindMessageSource(0, true, exception, out m_ClassName, out m_MethodName,
                                              out m_FileName, out m_LineNumber);
        }

        /// <summary>
        /// Should return the simple name of the method which issued the log message.
        /// </summary>
        public string MethodName
        {
            get { return m_MethodName; }
        }

        /// <summary>
        /// Should return the full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        public string ClassName
        {
            get { return m_ClassName; }
        }

        /// <summary>
        /// Should return the name of the file containing the method which issued the log message.
        /// </summary>
        public string FileName
        {
            get { return m_FileName; }
        }

        /// <summary>
        /// Should return the line within the file at which the log message was issued.
        /// </summary>
        public int LineNumber
        {
            get { return m_LineNumber; }
        }
    }
}
