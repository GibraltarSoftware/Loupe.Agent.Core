#nullable enable
namespace Gibraltar.Agent.Internal
{
    /// <summary>
    /// Exchanges information between the Agent's IMessageSourceProvider implementation and the internal Monitor implementation.
    /// </summary>
    internal class MessageSourceProvider : Monitor.IMessageSourceProvider, IMessageSourceProvider
    {
        private readonly IMessageSourceProvider m_CallingProvider;

        public MessageSourceProvider(IMessageSourceProvider callingProvider)
        {
            m_CallingProvider = callingProvider;
        }

        /// <summary>
        /// Should return the simple name of the method which issued the log message.
        /// </summary>
        public string? MethodName
        {
            get { return m_CallingProvider.MethodName; }
        }

        /// <summary>
        /// Should return the full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        public string? ClassName
        {
            get { return m_CallingProvider.ClassName; }
        }

        /// <summary>
        /// Should return the name of the file containing the method which issued the log message.
        /// </summary>
        public string? FileName
        {
            get { return m_CallingProvider.FileName; }
        }

        /// <summary>
        /// Should return the line within the file at which the log message was issued.
        /// </summary>
        public int LineNumber
        {
            get { return m_CallingProvider.LineNumber; }
        }
    }
}
