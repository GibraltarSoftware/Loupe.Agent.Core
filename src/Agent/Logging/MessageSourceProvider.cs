
using System.Runtime.CompilerServices;
using Loupe.Logging;


namespace Loupe.Agent.Logging
{
    /// <summary>
    /// A basic class to determine the source of a log message and act as an IMessageSourceProvider. 
    /// </summary>
    /// <remarks>This class knows how to acquire information about the source of a log message from the current call stack,
    /// and acts as a IMessageSourceProvider to use when handing off a log message to the central Log.
    /// Thus, this object must be created while still within the same call stack as the origination of the log message.
    /// </remarks>
    public class MessageSourceProvider : IMessageSourceProvider
    {
        private readonly string m_MethodName;
        private readonly string m_ClassName;
        private readonly string m_FileName;
        private readonly int m_LineNumber;

        /// <summary>
        /// Parameterless constructor for derived classes.
        /// </summary>
        protected MessageSourceProvider()
        {
            m_MethodName = null;
            m_ClassName = null;
            m_FileName = null;
            m_LineNumber = 0;
        }

        /// <summary>
        /// Creates a MessageSourceProvider object to be used as an IMessageSourceProvider.
        /// </summary>
        /// <param name="className">The full name of the class (with namespace) whose method issued the log message.</param>
        /// <param name="methodName">The simple name of the method which issued the log message.</param>
        /// <remarks>This constructor is used only for the convenience of the Log class when it needs to generate
        /// an IMessageSourceProvider for construction of internally-generated packets without going through the
        /// usual direct PublishToLog() mechanism.</remarks>
        public MessageSourceProvider(string className, string methodName)
        {
            m_MethodName = methodName;
            m_ClassName = className;
            m_FileName = null;
            m_LineNumber = 0;
        }

        /// <summary>
        /// Creates a MessageSourceProvider object to be used as an IMessageSourceProvider.
        /// </summary>
        /// <param name="className">The full name of the class (with namespace) whose method issued the log message.</param>
        /// <param name="methodName">The simple name of the method which issued the log message.</param>
        /// <param name="fileName">The name of the file containing the method which issued the log message.</param>
        /// <param name="lineNumber">The line within the file at which the log message was issued.</param>
        /// <remarks>This constructor is used only for the convenience of the Log class when it needs to generate
        /// an IMessageSourceProvider for construction of internally-generated packets without going through the
        /// usual direct PublishToLog() mechanism.</remarks>
        public MessageSourceProvider(string className, string methodName, string fileName, int lineNumber)
        {
            m_MethodName = methodName;
            m_ClassName = className;
            m_FileName = fileName;
            m_LineNumber = lineNumber;
        }

        /// <summary>
        /// Creates a MessageSourceProvider object to be used as an IMessageSourceProvider.
        /// </summary>
        /// <remarks>This constructor is used only for the convenience of the Log class when it needs to generate
        /// an IMessageSourceProvider for construction of internally-generated packets without going through the
        /// usual direct PublishToLog() mechanism.</remarks>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public MessageSourceProvider(int skipFrames)
        {
            Core.CommonCentralLogic.FindMessageSource(skipFrames + 1, true, null,
                                              out m_ClassName, out m_MethodName, out m_FileName, out m_LineNumber);

            OnMessageSourceCalculate(ref m_ClassName, ref m_MethodName, ref m_FileName, ref m_LineNumber);
        }

        #region IMessageSourceProvider properties

        /// <summary>
        /// The simple name of the method which issued the log message.
        /// </summary>
        public string MethodName { get { return m_MethodName; } }

        /// <summary>
        /// The full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        public string ClassName { get { return m_ClassName; } }

        /// <summary>
        /// The name of the file containing the method which issued the log message.
        /// </summary>
        public string FileName { get { return m_FileName; } }

        /// <summary>
        /// The line within the file at which the log message was issued.
        /// </summary>
        public int LineNumber { get { return m_LineNumber; } }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Override to adjust message source information after they have been automatically calculated using the default algorithm
        /// </summary>
        /// <param name="className">The full name of the class (with namespace) whose method issued the log message.</param>
        /// <param name="methodName">The simple name of the method which issued the log message.</param>
        /// <param name="fileName">The name of the file containing the method which issued the log message.</param>
        /// <param name="lineNumber">The line within the file at which the log message was issued.</param>
        protected virtual void OnMessageSourceCalculate(ref string className, ref string methodName, ref string fileName, ref int lineNumber)
        {

        }

        #endregion
    }
}
