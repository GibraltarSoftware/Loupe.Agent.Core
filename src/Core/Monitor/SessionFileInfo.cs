using System;
using System.Collections.Generic;
using System.Diagnostics;
using Gibraltar.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Tracks session headers and fragments from one or more fragments
    /// </summary>
    /// <remarks>Designed to help assemble a virtual index from a set of file fragments.</remarks>
    [DebuggerDisplay("Id: {Id}, IsRunning: {IsRunning}")]
    public class SessionFileInfo<T> where T : class
    {
        private readonly Guid m_SessionId;
        private readonly SortedList<int, T> m_SessionFragments = new SortedList<int, T>();

        private SessionHeader m_SessionHeader;

        /// <summary>
        /// Create a new file information tracking object
        /// </summary>
        public SessionFileInfo(SessionHeader sessionHeader, T fileInfo, bool isNew)
        {
#if DEBUG
            Debug.Assert(sessionHeader != null);
            Debug.Assert(fileInfo != null);
#endif
            m_SessionId = sessionHeader.Id;

            //this will be our best session header since it's the first
            m_SessionHeader = sessionHeader;
            m_SessionHeader.IsNew = isNew;
            m_SessionFragments.Add(sessionHeader.FileSequence, fileInfo);
        }

        /// <summary>
        /// The session id
        /// </summary>
        public Guid Id { get { return m_SessionId; } }

        /// <summary>
        /// Indicates if the session is actually running (regardless of its session state)
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// The best session header from all the loaded fragments
        /// </summary>
        public SessionHeader Header { get { return m_SessionHeader; } }

        /// <summary>
        /// The list of fragments that have been found
        /// </summary>
        public IList<T> Fragments { get { return m_SessionFragments.Values; } }

        /// <summary>
        /// Add another fragment to this session's information
        /// </summary>
        /// <param name="sessionHeader"></param>
        /// <param name="fileInfo"></param>
        /// <param name="isNew"></param>
        public void AddFragment(SessionHeader sessionHeader, T fileInfo, bool isNew)
        {
            lock(m_SessionFragments)
            {
                //if a file got duplicated or copied for some reason (which can happen if someone is messing around in the log directory)
                //then we could get a duplicate item.  We need to make sure we don't process that.
                if (!m_SessionFragments.ContainsKey(sessionHeader.FileSequence))
                {
                    //If this header is newer than our previous best it takes over (headers are cumulative)
                    if (sessionHeader.FileSequence > m_SessionHeader.FileSequence)
                    {
                        sessionHeader.IsNew = m_SessionHeader.IsNew; //preserve our existing setting...
                        m_SessionHeader = sessionHeader;
                    }

                    m_SessionHeader.IsNew = m_SessionHeader.IsNew || isNew; //if any are new, it's new.

                    //and we add this file info to our set in its correct order.
                    m_SessionFragments.Add(sessionHeader.FileSequence, fileInfo);
                }
            }
        }
    }
}
