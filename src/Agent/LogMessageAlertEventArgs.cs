using System;
using Loupe.Agent.Internal;
using Loupe.Extensibility.Data;

namespace Loupe.Agent
{
    /// <summary>
    /// EventArgs for Notification events.
    /// </summary>
    public class LogMessageAlertEventArgs : EventArgs
    {
        private readonly Messaging.NotificationEventArgs m_Event;
        private readonly LogMessageInfoCollection m_MessageCollection;

        internal LogMessageAlertEventArgs(Messaging.NotificationEventArgs eventArgs)
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

        /// <summary>
        /// A minimum length of time to wait until another notification may be issued, requested by the client upon return.
        /// </summary>
        public TimeSpan MinimumDelay
        {
            get { return m_Event.MinimumNotificationDelay; }
            set { m_Event.MinimumNotificationDelay = value; }
        }

        /// <summary>
        /// Indicates if there is sufficient configuration information to send the current session (either via Loupe Server or email)
        /// </summary>
        /// <param name="reason">If there is not sufficient configuration information this value will describe what was missing.</param>
        public bool CanSendSession(ref string reason)
        {
            return Monitor.Log.CanSendSessions(ref reason);
        }

        /// <summary>
        /// Set to automatically send the latest information on the current session when the event returns.
        /// </summary>
        /// <remarks>If there is insufficient configuration information to automatically send sessions this property
        /// will revert to false when set true.  To verify if there is sufficient configuration information, use CanSendSession</remarks>
        public bool SendSession
        {
            get
            {
                return m_Event.SendSession;
            }
            set
            {
                m_Event.SendSession = value;
            }
        }

        #endregion
    }
}