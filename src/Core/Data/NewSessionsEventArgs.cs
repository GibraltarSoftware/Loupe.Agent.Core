using System;
using Loupe.Extensibility.Data;

namespace Gibraltar.Data
{
    /// <summary>
    /// Supplies summary information about new sessions that are available to be retrieved or just retrieved into the repository
    /// </summary>
    public class NewSessionsEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new sessions event arguments container
        /// </summary>
        /// <param name="newSessions"></param>
        /// <param name="warningSessions"></param>
        /// <param name="errorSessions"></param>
        /// <param name="criticalSessions"></param>
        /// <param name="maxSeverity"></param>
        public NewSessionsEventArgs(int newSessions, int warningSessions, int errorSessions, int criticalSessions, LogMessageSeverity maxSeverity)
        {
            NewSessions = newSessions;
            WarningSessions = warningSessions;
            ErrorSessions = errorSessions;
            CriticalSessions = criticalSessions;
            MaxSeverity = maxSeverity;
        }

        /// <summary>
        /// The number of new sessions affected
        /// </summary>
        public int NewSessions { get; private set; }

        /// <summary>
        /// The number of new sessions with a max severity of warning.
        /// </summary>
        public int WarningSessions { get; private set; }

        /// <summary>
        /// The number of new sessions with a max severity of error.
        /// </summary>
        public int ErrorSessions { get; private set; }

        /// <summary>
        /// The number of new sessions with a max severity of critical.
        /// </summary>
        public int CriticalSessions { get; private set; }

        /// <summary>
        /// The maximum severity of new sessions.
        /// </summary>
        public LogMessageSeverity MaxSeverity { get; private set; }
    }

    /// <summary>
    /// An event handler for the New Sessions Event Arguments
    /// </summary>
    /// <param name="state"></param>
    /// <param name="e"></param>
    public delegate void NewSessionsEventHandler(object state, NewSessionsEventArgs e); 
}
