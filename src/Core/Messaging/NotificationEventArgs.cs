
using System;
using Loupe.Monitor;
using Loupe.Extensibility.Data;



namespace Loupe.Messaging
{
    /// <summary>
    /// EventArgs for Notification events.
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        private bool m_SendSession;

        /// <summary>
        /// The set of one or more log messages for this notification event.
        /// </summary>
        public readonly ILogMessage[] Messages;

        /// <summary>
        /// The strongest log message severity included in this notification event.
        /// </summary>
        public readonly LogMessageSeverity TopSeverity;

        /// <summary>
        /// The total number of log messages included in this notification event.
        /// </summary>
        public readonly int TotalCount;

        /// <summary>
        /// The number of Critical log messages included in this notification event.
        /// </summary>
        public readonly int CriticalCount;

        /// <summary>
        /// The number of Error log messages included in this notification event.
        /// </summary>
        public readonly int ErrorCount;

        /// <summary>
        /// The number of Warning log messages included in this notification event.
        /// </summary>
        public readonly int WarningCount;

        /// <summary>
        /// The number of log messages which have an attached Exception included in this notification event.
        /// </summary>
        public readonly int ExceptionCount;

        /// <summary>
        /// A minimum length of time to wait until another notification may be issued, requested by the client upon return.
        /// </summary>
        public TimeSpan MinimumNotificationDelay;

        /// <summary>
        /// Set to automatically send the latest information on the current session when the event returns.
        /// </summary>
        /// <remarks>If there is insufficient configuration information to automatically send sessions this property
        /// will revert to false when set true.  To verify if there is sufficient configuration information, use CanSendSession</remarks>
        public bool SendSession
        {
            get
            {
                return m_SendSession;
            }
            set
            {
                if (m_SendSession == value)
                    return;

                if (value == false)
                {
                    //just do it - doesn't matter if we're valid or if we are already false.
                    m_SendSession = false; 
                }
                else
                {
                    //we must be setting to true.
                    string message = null;
                    if (Log.CanSendSessions(ref message))
                    {
                        m_SendSession = true;
                    }
                }
            }
        }

        internal NotificationEventArgs(ILogMessage[] messages, TimeSpan defaultMinWait)
        {
            MinimumNotificationDelay = defaultMinWait;
            Messages = messages;

            TopSeverity = LogMessageSeverity.Verbose;
            foreach (ILogMessage message in messages)
            {
                if (message == null)
                    continue;

                TotalCount++;
                LogMessageSeverity severity = message.Severity;
                if (severity < TopSeverity) // Severity compares in reverse, Critical = 1, Verbose = 16.
                    TopSeverity = severity; // Remember the new top severity.

                switch (severity)
                {
                    case LogMessageSeverity.Critical:
                        CriticalCount++;
                        break;

                    case LogMessageSeverity.Error:
                        ErrorCount++;
                        break;

                    case LogMessageSeverity.Warning:
                        WarningCount++;
                        break;
                }

                if (message.HasException)
                    ExceptionCount++;
            }
        }
    }
}
