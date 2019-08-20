using System;
using System.Diagnostics;
using System.IO;
using Ionic.Zlib;
using Loupe.Core.Data;
using Loupe.Core.Serialization;
using Loupe.Extensibility.Data;

#pragma warning disable 1591

namespace Loupe.Core.IO
{
    public class GLFWriter : IDisposable
    {
        private const int MaxExpectedPacketSize = 1024;
        private const int BufferSize = 16 * 1024;

        private readonly Stream m_OutputStream;
        private readonly Stream m_PacketStream;
        private readonly PacketWriter m_PacketWriter;
        private readonly FileHeader m_FileHeader;
        private readonly SessionSummary m_SessionSummary;
        private readonly SessionHeader m_SessionHeader;
        private bool m_AutoFlush;
        private bool m_WeAreDisposed;

        /// <summary>
        /// Initialize the GLF writer for the provided session which has already been recorded.
        /// </summary>
        /// <param name="file">The file stream to write the session file into (should be empty)</param>
        /// <param name="sessionSummary"></param>
        /// <remarks>This constructor is designed for use with sessions that have already been completed and closed.</remarks>
        public GLFWriter(Stream file, SessionSummary sessionSummary)
            :this(file, sessionSummary, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
        }

        /// <summary>
        /// Initialize the GLF writer for the provided session which has already been recorded.
        /// </summary>
        /// <param name="file">The file stream to write the session file into (should be empty)</param>
        /// <param name="sessionSummary"></param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        /// <remarks>This constructor is designed for use with sessions that have already been completed and closed.</remarks>
        public GLFWriter(Stream file, SessionSummary sessionSummary, int majorVersion, int minorVersion)
            : this(file, sessionSummary, 1, null, majorVersion, minorVersion)
        {
        }

        /// <summary>
        /// Initialize the GLF writer for storing information about the current live session
        /// </summary>
        /// <param name="file">The file stream to write the session file into (should be empty)</param>
        /// <param name="sessionSummary"></param>
        /// <param name="fileSequence"></param>
        /// <param name="fileStartTime">Used during initial collection to indicate the real time this file became the active file.</param>
        /// <remarks>The file header is configured with a copy of the session summary, assuming that we're about to make a copy of the
        /// session. For live data collection the caller should supply the file start time to reflect the true time period
        /// covered by this file. </remarks>
        public GLFWriter(Stream file, SessionSummary sessionSummary, int fileSequence, DateTimeOffset? fileStartTime)
            : this(file, sessionSummary, fileSequence, fileStartTime, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
        }

        /// <summary>
        /// Initialize the GLF writer for storing information about the current live session
        /// </summary>
        /// <param name="file">The file stream to write the session file into (should be empty)</param>
        /// <param name="sessionSummary"></param>
        /// <param name="fileSequence"></param>
        /// <param name="fileStartTime">Used during initial collection to indicate the real time this file became the active file.</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        /// <remarks>The file header is configured with a copy of the session summary, assuming that we're about to make a copy of the
        /// session. For live data collection the caller should supply the file start time to reflect the true time period
        /// covered by this file. </remarks>
        public GLFWriter(Stream file, SessionSummary sessionSummary, int fileSequence, DateTimeOffset? fileStartTime, int majorVersion, int minorVersion)
        {
            //for use to use the stream, it has to support 
            if (file.CanSeek == false)
            {
                throw new ArgumentException("Provided stream can't be used because it doesn't support seeking", nameof(file));
            }

            m_SessionSummary = sessionSummary;
            m_OutputStream = file;

            // This logic will store GZip compressed files for protocol version 2 and beyond
            if (majorVersion > 1)
            {
                //we are explicitly *NOT* using the system GZipStream because it doesn't support flush.
                m_PacketStream = new GZipStream(m_OutputStream, CompressionMode.Compress, CompressionLevel.Default, true){ FlushMode = FlushType.Sync};

            }
            else
            {
                m_PacketStream = new MemoryStream(BufferSize + MaxExpectedPacketSize);
            }

            m_PacketWriter = new PacketWriter(m_PacketStream, majorVersion, minorVersion);

            //initialize the stream with the file header and session header
            m_FileHeader = new FileHeader(majorVersion, minorVersion);
            m_SessionHeader = new SessionHeader(sessionSummary);

            //There are two variants of the GLF format:  One for a whole session, one for a session fragment.
            if (fileStartTime.HasValue)
            {
                m_SessionHeader.FileId = Guid.NewGuid();
                m_SessionHeader.FileSequence = fileSequence;
                m_SessionHeader.FileStartDateTime = fileStartTime.Value;
                m_SessionHeader.FileEndDateTime = m_SessionHeader.EndDateTime;

                //by default, this is the last file - it won't be if we open another.
                m_SessionHeader.IsLastFile = true;
            }

            //we need to know how big the session header will be (it's variable sized) before we can figure out the data offset.
            byte[] sessionHeader = m_SessionHeader.RawData();

            //where are we going to start our data block?
            m_FileHeader.DataOffset = FileHeader.HeaderSize + sessionHeader.Length;

            byte[] header = m_FileHeader.RawData();
            m_OutputStream.Position = 0; //move to the start of the stream, we rely on this.
            m_OutputStream.Write(header, 0, FileHeader.HeaderSize);
            m_OutputStream.Write(sessionHeader, 0, sessionHeader.Length);
            m_OutputStream.Flush();

            m_OutputStream.Position = m_FileHeader.DataOffset; //so we are sure we start writing our data at the correct spot.

            // When we added GZip compression to streams we noticed that we were sometimes
            // losing a lot of data that went unflushed while programmers were testing in
            // Visual Studio.  To address this, we have the GZip stream flush to disk much
            // more aggressively when we detect that the debugger is attached.
#if !DEBUG // We only want this behavior for release builds, not for our own debug builds
// we might prefer the writing to be the more typical optimized behavior.
            AutoFlush = Debugger.IsAttached;
#endif
        }

        public virtual void Write(IPacket packet)
        {
            m_PacketWriter.Write(packet);

            if (m_AutoFlush || (m_PacketStream is MemoryStream && m_PacketStream.Position >= BufferSize))
                Flush();
        }

        public virtual void Write(Stream sessionPacketStream)
        {
            FileSystemTools.StreamContentPump(sessionPacketStream, m_OutputStream);
        }

        public bool AutoFlush {
            get => m_AutoFlush;
            set => m_AutoFlush = value;
        }

        public void Flush()
        {
            UpdateSessionHeader();

            if (m_PacketStream is MemoryStream stream)
            {
                int count = (int) stream.Position;
                if (count > 0)
                {
                    byte[] array = stream.ToArray();
                    m_OutputStream.Write(array, 0, count);

                    m_OutputStream.Flush();
                    stream.Position = 0;
                }
            }
            else
            {
                m_PacketStream.Flush();
                m_OutputStream.Flush();
            }
        }

        /// <summary>
        /// Update the session file with the latest session summary information.
        /// </summary>
        internal static void UpdateSessionHeader(GLFReader sourceFile, Stream updateFile)
        {
            long originalPosition = updateFile.Position;
            try
            {
                updateFile.Position = FileHeader.HeaderSize;

                byte[] header = sourceFile.SessionHeader.RawData();
                updateFile.Write(header, 0, header.Length);
            }
            finally
            {
                updateFile.Position = originalPosition; //move back to wherever it was
                updateFile.Flush(); //and make sure it's been written to disk.
            }
        }

        /// <summary>
        /// Update the session file with the latest session summary information.
        /// </summary>
        private void UpdateSessionHeader()
        {
            long originalPosition = m_OutputStream.Position;
            try
            {
                m_OutputStream.Position = FileHeader.HeaderSize;

                //The file includes up through now (after all, we're flushing it to disk)
                m_SessionHeader.EndDateTime = m_SessionSummary.EndDateTime; //this is ONLY changing what we write out in the file & the index, not the main packet...

                if (m_SessionHeader.HasFileInfo)
                {
                    m_SessionHeader.FileEndDateTime = m_SessionSummary.EndDateTime; //we convert to UTC during serialization, we want local time.
                }
                
                //session status updates are tricky...  We don't want the stream to reflect running 
                //once closed, but we do want to allow other changes.
                if ((m_SessionSummary.Status == SessionStatus.Crashed) || (m_SessionSummary.Status == SessionStatus.Normal))
                {
                    m_SessionHeader.StatusName = m_SessionSummary.Status.ToString();
                }

                //plus we want the latest statistics
                m_SessionHeader.MessageCount = m_SessionSummary.MessageCount;
                m_SessionHeader.CriticalCount = m_SessionSummary.CriticalCount;
                m_SessionHeader.ErrorCount = m_SessionSummary.ErrorCount;
                m_SessionHeader.WarningCount = m_SessionSummary.WarningCount;

                byte[] header = m_SessionHeader.RawData();
                m_OutputStream.Write(header, 0, header.Length);
            }
            finally
            {
                m_OutputStream.Position = originalPosition; //move back to wherever it was
            }
        }

        public virtual void Close(bool isLastFile)
        {
            //set our session end date in our header - this doesn't imply that we're clean or anything, just that this is the end.
            if (m_SessionSummary.Status == SessionStatus.Crashed)
                m_SessionHeader.StatusName = SessionStatus.Crashed.ToString();
            else
                m_SessionHeader.StatusName = SessionStatus.Normal.ToString();

            m_SessionHeader.IsLastFile = isLastFile;

            Flush(); //updates the session header on its own.

            //and we create our own PacketWriter, so handle that, too
            m_PacketWriter.Dispose();
        }

        public SessionHeader SessionHeader { get { return m_SessionHeader; } }

        #region IDisposable Members

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // and SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (!m_WeAreDisposed)
            {
                m_WeAreDisposed = true; // Only Dispose stuff once

                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case

                    // We already have a Close(), so just invoke that
                    Close(false); // Ah, we didn't have a clean end, so don't set the last-file marker
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here
            }
        }

        #endregion
    }
}
