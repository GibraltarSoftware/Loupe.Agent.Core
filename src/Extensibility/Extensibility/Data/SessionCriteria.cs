using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// Selection criteria for session inclusion in a session package
    /// </summary>
    [Flags]
    public enum SessionCriteria
    {
        /// <summary>
        /// Default. Includes no sessions.
        /// </summary>
        None = 0,

        /// <summary>
        /// Include all sessions that are not actively running regardless of whether they've been sent before.
        /// </summary>
        CompletedSessions = 1,

        /// <summary>
        /// Include sessions that have not been sent or opened before.
        /// </summary>
        NewSessions = 2,

        /// <summary>
        /// Include sessions that failed to exit normally
        /// </summary>
        CrashedSessions = 4,

        /// <summary>
        /// Include sessions with one or more critical log message.
        /// </summary>
        CriticalSessions = 8,

        /// <summary>
        /// Include sessions with one or more error message.
        /// </summary>
        ErrorSessions = 16,

        /// <summary>
        /// Include sessions with one or more warning message.
        /// </summary>
        WarningSessions = 32,

        /// <summary>
        /// Include the session for the current process.
        /// </summary>
        ActiveSession = 64, 

        /// <summary>
        /// Include all sessions including the session for the current process regardless of whether they've been sent before.
        /// </summary>
        AllSessions = CompletedSessions | ActiveSession
    }
}
