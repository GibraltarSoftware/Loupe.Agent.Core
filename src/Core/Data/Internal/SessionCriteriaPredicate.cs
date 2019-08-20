using System;
using System.Diagnostics;
using Loupe.Core.Monitor;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Data.Internal
{

    /// <summary>
    /// Compares sessions to the supplied session criteria to determine if they match
    /// </summary>
    [DebuggerDisplay("Product: {Product} App: {Application} Criteria: {Criteria}")]
    internal class SessionCriteriaPredicate
    {
        private readonly string m_ProductName;
        private readonly string m_ApplicationName;
        private readonly SessionCriteria m_Criteria;

        private bool m_ActiveSession;
        private bool m_NewSessions;
        private bool m_CompletedSessions;
        private bool m_CrashedSessions;
        private bool m_CriticalSessions;
        private bool m_ErrorSessions;
        private bool m_WarningSessions;

        public SessionCriteriaPredicate(string productName, string applicationName, SessionCriteria criteria)
        {
            m_ProductName = productName;
            m_ApplicationName = applicationName;
            m_Criteria = criteria;

            //now parse out the criteria
            m_ActiveSession = ((m_Criteria & SessionCriteria.ActiveSession) == SessionCriteria.ActiveSession);
            m_NewSessions = ((m_Criteria & SessionCriteria.NewSessions) == SessionCriteria.NewSessions);
            m_CompletedSessions = ((m_Criteria & SessionCriteria.CompletedSessions) == SessionCriteria.CompletedSessions);
            m_CrashedSessions = ((m_Criteria & SessionCriteria.CrashedSessions) == SessionCriteria.CrashedSessions);
            m_CriticalSessions = ((m_Criteria & SessionCriteria.CriticalSessions) == SessionCriteria.CriticalSessions);
            m_ErrorSessions = ((m_Criteria & SessionCriteria.ErrorSessions) == SessionCriteria.ErrorSessions);
            m_WarningSessions = ((m_Criteria & SessionCriteria.WarningSessions) == SessionCriteria.WarningSessions);
        }

        public SessionCriteria Criteria { get { return m_Criteria; } }
        public string Application { get { return m_ApplicationName; } }
        public string Product { get { return m_ProductName; } }

        public bool Predicate(ISessionSummary sessionSummary)
        {
            //see if this session matches our criteria.
            if (!sessionSummary.Product.Equals(m_ProductName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(m_ApplicationName) && !sessionSummary.Application.Equals(m_ApplicationName, StringComparison.OrdinalIgnoreCase))
                return false;

            //at this point if we get a qualifying match we can just return true.
            if (m_ActiveSession && (Log.SessionSummary.Id == sessionSummary.Id))
                return true;

            if (sessionSummary.Status != SessionStatus.Running)
            {
                if (m_CompletedSessions)
                    return true;

                if (sessionSummary.IsNew && m_NewSessions)
                    return true;

                if (m_CrashedSessions && (sessionSummary.Status == SessionStatus.Crashed))
                    return true;

                if (m_CriticalSessions && (sessionSummary.CriticalCount > 0))
                    return true;

                if (m_ErrorSessions && (sessionSummary.ErrorCount > 0))
                    return true;

                if (m_WarningSessions && (sessionSummary.WarningCount > 0))
                    return true;
            }

            //if we didn't get there by now, we aren't going to match.
            return false;
        }
    }
}
