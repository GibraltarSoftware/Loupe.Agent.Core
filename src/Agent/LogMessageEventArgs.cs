using System;
using Gibraltar.Agent.Internal;
using Loupe.Extensibility.Data;

namespace Gibraltar.Agent
{
    /// <summary>
    /// EventArgs for Notification events.
    /// </summary>
    public class LogMessageEventArgs : EventArgs
    {
        private readonly Messaging.NotificationEventArgs m_Event;
        private readonly LogMessageInfoCollection m_MessageCollection;

        internal LogMessageEventArgs(Messaging.NotificationEventArgs eventArgs)
        {
            m_Event = eventArgs;
            m_MessageCollection = new LogMessageInfoCollection(eventArgs.Messages);
        }

        #region Public Properties and Methods

        /// <summary>
        /// The set of one or more log messages for this notification event in a read-only collection.
        /// </summary>
        public ILogMessageCollection Messages { get { return m_MessageCollection; } }

        /// <summary>
        /// The strongest log message severity included in this notification event.
        /// </summary>
        public LogMessageSeverity TopSeverity { get { return m_Event.TopSeverity; } }

        /// <summary>
        /// The total number of log messages included in this notification event.
        /// </summary>
        public int TotalCount { get { return m_Event.TotalCount; } }

        /// <summary>
        /// The number of Critical log messages included in this notification event.
        /// </summary>
        public int CriticalCount { get { return m_Event.CriticalCount; } }

        /// <summary>
        /// The number of Error log messages included in this notification event.
        /// </summary>
        public int ErrorCount { get { return m_Event.ErrorCount; } }

        /// <summary>
        /// The number of Warning log messages included in this notification event.
        /// </summary>
        public int WarningCount { get { return m_Event.WarningCount; } }

        /// <summary>
        /// The number of log messages which have an attached Exception included in this notification event.
        /// </summary>
        public int ExceptionCount { get { return m_Event.ExceptionCount; } }

        #endregion
    }
}