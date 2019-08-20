using System.Collections;
using System.Collections.Generic;
using Loupe.Data;
#pragma warning disable 1591

namespace Loupe.Monitor.Serialization
{
    public class LogMessageEnumerable: IEnumerable<LogMessage>
    {
        private readonly Session m_Session;
        private readonly ThreadInfoCollection m_Threads;
        private readonly ApplicationUserCollection m_Users;
        private readonly List<GLFReader> m_AvailableReaders;

        public LogMessageEnumerable(Session session, ThreadInfoCollection threads, ApplicationUserCollection users, List<GLFReader> availableReaders)
        {
            m_Session = session;
            m_Threads = threads;
            m_Users = users;
            m_AvailableReaders = availableReaders;
        }

        public IEnumerator<LogMessage> GetEnumerator()
        {
            if (m_AvailableReaders.Count == 0) //we were already loaded, so go for the collection..
                return ((IEnumerable<LogMessage>)m_Session.Messages).GetEnumerator();

            return new LogMessageEnumerator(m_Session, m_Threads, m_Users, m_AvailableReaders);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
