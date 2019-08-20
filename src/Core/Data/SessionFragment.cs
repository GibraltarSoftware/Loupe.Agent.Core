using System;
using System.IO;
using Loupe.Core.IO;
using Loupe.Core.IO.Serialization;

namespace Loupe.Core.Data
{
    /// <summary>
    /// A single stream of the session's information as it was originally recorded.
    /// </summary>
    /// <remarks>When a session is originally recorded to a log file it may be split
    /// between multiple files due to the length of the session, time it was running,
    /// or to allow part of the session to be submitted for review while the session is 
    /// still running.  Once closed, a session fragment is immutable.</remarks>
    public class SessionFragment
    {
        private readonly SessionFragmentPacket m_Packet;
        private readonly GLFReader m_SessionFile;

        internal SessionFragment(GLFReader sessionFile)
        {
            if (sessionFile == null)
            {
                throw new ArgumentNullException(nameof(sessionFile));
            }

            if (sessionFile.SessionHeader.HasFileInfo == false)
            {
                throw new ArgumentException("The session file is missing file information and can't be used as a session fragment.  This indicates a coding error in the framework.", nameof(sessionFile));
            }

            m_SessionFile = sessionFile;
            m_Packet = new SessionFragmentPacket();
            m_Packet.ID = m_SessionFile.SessionHeader.FileId;
            m_Packet.FileStartDateTime = m_SessionFile.SessionHeader.FileStartDateTime;
            m_Packet.FileEndDateTime = m_SessionFile.SessionHeader.FileEndDateTime;
            m_Packet.IsLastFile = m_SessionFile.SessionHeader.IsLastFile;

            //and we need to make sure our session file actually has the packet stream loaded.
            sessionFile.EnsureDataLoaded();
        }

        internal SessionFragment(SessionFragmentPacket sessionFragmentPacket)
        {
            if (sessionFragmentPacket == null)
            {
                throw new ArgumentNullException(nameof(sessionFragmentPacket));
            }

            m_Packet = sessionFragmentPacket;

            Loaded = true; //since we don't have the session file object we don't support delay loading
        }

        /// <summary>
        /// The unique Id of this session fragment.
        /// </summary>
        public Guid Id { get { return m_Packet.ID; } }

        /// <summary>
        /// The date &amp; time the fragment was started.
        /// </summary>
        public DateTimeOffset StartDateTime { get { return m_Packet.FileStartDateTime; } }

        /// <summary>
        /// The date &amp; time the fragment was closed.
        /// </summary>
        public DateTimeOffset EndDateTime { get { return m_Packet.FileEndDateTime; } }

        /// <summary>
        /// Indicates if this is the last fragment of the session.
        /// </summary>
        public bool IsLastFile { get { return m_Packet.IsLastFile;  } }

        /// <summary>
        /// The number of messages recorded in the entire session up through this fragment.
        /// </summary>
        public int MessageCount { get { return m_SessionFile.SessionHeader.MessageCount; } }

        /// <summary>
        /// The number of critical messages recorded in the entire session up through this fragment.
        /// </summary>
        public int CriticalCount { get { return m_SessionFile.SessionHeader.CriticalCount; } }

        /// <summary>
        /// The number of error messages recorded in the entire session up through this fragment.
        /// </summary>
        public int ErrorCount { get { return m_SessionFile.SessionHeader.ErrorCount; } }

        /// <summary>
        /// The number of warning messages recorded in the entire session up through this fragment.
        /// </summary>
        public int WarningCount { get { return m_SessionFile.SessionHeader.WarningCount; } }

        /// <summary>
        /// The compressed stream size
        /// </summary>
        public long Size { get { return m_SessionFile.FragmentStorageSummary.FragmentSize; }}

        /// <summary>
        /// Indicates if the fragment has been parsed and loaded into memory.
        /// </summary>
        internal bool Loaded { get; set; }

        /// <summary>
        /// The raw binary stream of the packets within the session fragment. (Generally use Reader property instead.)
        /// </summary>
        private Stream RawStream { get { return m_SessionFile.RawStream; } }

        /// <summary>
        /// The GLFReader which owns the open stream for this fragment (could be null if not created from one).
        /// </summary>
        internal GLFReader Reader { get { return m_SessionFile; } }

        internal SessionFragmentPacket Packet { get { return m_Packet; } }
    }
}
