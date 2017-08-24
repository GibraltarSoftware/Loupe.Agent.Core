using System;
using Loupe.Extensibility.Data;

namespace Gibraltar.Agent.Internal
{
    /// <summary>
    /// Adapts a SessionSummary predicate to the internal ISessionSummary used in the core
    /// </summary>
    internal class SessionSummaryPredicate
    {
        private readonly Predicate<SessionSummary> m_SessionSummaryPredicate;

        public SessionSummaryPredicate(Predicate<SessionSummary> sessionSummaryPredicate)
        {
            m_SessionSummaryPredicate = sessionSummaryPredicate;
        }

        /// <summary>
        /// The adapter predicate
        /// </summary>
        public bool Predicate(ISessionSummary sessionSummary)
        {
            //Unfortunately we have to wrap the session summary with another object to get the right type for the public delegate
            var wrappedObject = new SessionSummary(sessionSummary);

            return m_SessionSummaryPredicate(wrappedObject);
        }
    }
}
