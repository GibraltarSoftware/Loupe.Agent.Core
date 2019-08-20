using System;
using System.IO;
using System.IO.Compression;
using Loupe.Monitor;
using Loupe.Serialization;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Loupe.Data
{
    /// <summary>
    /// Reads a GLF file
    /// </summary>
    public class GLFReader : IDisposable
    {
        private readonly FileHeader m_FileHeader;
        private readonly SessionHeader m_SessionHeader;
        private readonly bool m_IsSessionStream;
        private Stream m_File;
        private Stream m_RawStream; // Formerly a MemoryStream, but now allows a copy of the original FileStream to be used.
        private Stream m_PacketStream;
        private bool m_IsDisposed;

        /// <summary>
        /// Details about the storage required for this session fragment
        /// </summary>
        public FragmentStorageSummary FragmentStorageSummary { get; private set; } 

        /// <summary>
        /// Create a new GLF reader to operate on the provided stream.  The GLFReader then owns the stream and will dispose it
        /// when disposed itself. (Use static GLFReader.IsGLF() to test a stream without giving it up.)
        /// </summary>
        /// <param name="file"></param>
        public GLFReader(Stream file)
        {
            m_File = file;

            m_IsSessionStream = IsGLF(m_File, out m_FileHeader);

            if (m_IsSessionStream)
            {
                //it's a session - load the session header
                byte[] header = new byte[m_FileHeader.DataOffset - FileHeader.HeaderSize];
                m_File.Position = FileHeader.HeaderSize;
                m_File.Read(header, 0, header.Length);
                m_File.Position = 0;

                m_SessionHeader = new SessionHeader(header);
                m_SessionHeader.HasData = true; //since we're on a file

                FragmentStorageSummary = new FragmentStorageSummary(m_SessionHeader.FileStartDateTime, m_SessionHeader.FileEndDateTime, file.Length);
                //BUG:  Should now validate the CRC's before we accept it as a valid file
            }
        }

        #region Static Properties and Methods

        /// <summary>
        /// Indicates if the specified fileName is an existing, accessible, valid GLF file.
        /// </summary>
        /// <param name="fileName">The full path to the file in question.</param>
        /// <returns></returns>
        public static bool IsGLF(string fileName)
        {
            bool isGlf = false;
            try
            {
                if (string.IsNullOrEmpty(fileName) == false && File.Exists(fileName))
                {
                    using (FileStream stream = FileHelper.OpenFileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (stream != null)
                            isGlf = IsGLF(stream);
                    }
                }
            }
            catch
            {
                isGlf = false; // We got an error just trying to verify it, so we surely won't be able to open it for real.
            }

            return isGlf;
        }

        /// <summary>
        /// Indicates if the provided file stream is for a GLF file.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <returns></returns>
        public static bool IsGLF(Stream fileStream)
        {
            return IsGLF(fileStream, out var fileHeader);
        }

        /// <summary>
        /// Indicates if the provided file stream is for a GLF file.
        /// </summary>
        public static bool IsGLF(Stream fileStream, out FileHeader fileHeader)
        {
            bool isSessionStream = false;
            fileHeader = null;

            //first is the stream even big enough?
            if (fileStream == null)
            {
                return false;
            }

            if (fileStream.CanSeek == false)
            {
                throw new ArgumentException("The provided data stream doesn't support seeking so it can't be used to read sessions.");
            }
            
            if (fileStream.Length >= FileHeader.HeaderSize) //any real file will be longer than the header, but we might be just getting a taste...
            {
                //lets see if it has the right type code
                byte[] fileHeaderRawData = new byte[FileHeader.HeaderSize];
                fileStream.Position = 0;
                fileStream.Read(fileHeaderRawData, 0, fileHeaderRawData.Length);
                fileStream.Position = 0;

                FileHeader sessionFileHeader = new FileHeader(fileHeaderRawData);

                if (sessionFileHeader.TypeCode == FileHeader.GLFTypeCode)
                {
                    fileHeader = sessionFileHeader;
                    isSessionStream = true;
                }
            }

            return isSessionStream;
        }

        #endregion

        /// <summary>
        /// Returns the major version of the serialization protocol in use
        /// </summary>
        public int MajorVersion { get { return m_FileHeader.MajorVersion; } }

        /// <summary>
        /// Returns the minor version of the serialization protocol in use
        /// </summary>
        public int MinorVersion { get { return m_FileHeader.MinorVersion; } }

        /// <summary>
        /// Ensures that the file Stream is positioned beyond the header ready to start reading packets, and the original
        /// provided Stream may be closed/disposed after this call.  (But do not alter the original Stream or its position
        /// because the same file handle may be retained.)
        /// </summary>
        public void EnsureDataLoaded()
        {
            if (m_IsSessionStream == false)
            {
                throw new InvalidOperationException("The provided file is not a valid Loupe data stream and can't be read.");
            }

            if (m_RawStream == null)
            {
                m_RawStream = m_File; // Outdated, but this distinguishes that it has been "loaded" and positioned in the packet data.

                // Position the stream at the start of the packet data.
                m_RawStream.Position = m_FileHeader.DataOffset;

                // And blank the original reference since it has now been loaded.
                m_File = null;
            }
        }


        /// <summary>
        /// Closes the GLF reader and releases any data stream owned by the GLF reader
        /// </summary>
        /// <remarks>The file stream provided to the GLF reader in the constructor is owned by the instance and will be
        /// disposed with the GLFReader instance.</remarks>
        public void Close()
        {
            //order here is conceptually important since streams and other objects shouldn't be disposed twice
            //and shouldn't access disposed objects so we go from outside in

            if (m_PacketStream != null)
            {
                //GZip has a habit of throwing a wobbler if it wasn't completely read, so be ready..
                try
                {
                    m_PacketStream.Dispose();
                }
                catch (Exception ex)
                {
#if DEBUG
                    Log.Write(LogMessageSeverity.Information, LogWriteMode.Queued, ex, Session.LogCategory, "First pass close attempt on packet stream failed", 
                        "Exception: {0}", ex.Message);
#endif
                    GC.KeepAlive(ex);
                }

                //forward check if another stream is a copy of this one so we don't double-dispose
                if (ReferenceEquals(m_RawStream, m_PacketStream))
                    m_PacketStream = null;
                if (ReferenceEquals(m_File, m_RawStream))
                    m_RawStream = null;

                m_PacketStream = null;
            }

            if (m_RawStream != null)
            {
                m_RawStream.Dispose();

                //forward check if another stream is a copy of this one so we don't double-dispose
                if (ReferenceEquals(m_File, m_PacketStream))
                    m_PacketStream = null;

                m_RawStream = null;
            }

            if (m_File != null)
            {
                m_File.Dispose(); // This stream belongs to us, so we need to dispose it.

                m_File = null;
            }

        }

        /// <summary>
        /// Indicates if the stream provided to the GLFReader is a valid session stream.
        /// </summary>
        public bool IsSessionStream
        {
            get
            {
                return m_IsSessionStream;
            }
        }

        /// <summary>
        /// The file header at the start of the stream.
        /// </summary>
        public FileHeader FileHeader { get { return m_FileHeader; } }

        /// <summary>
        /// The session header for the stream
        /// </summary>
        public SessionHeader SessionHeader { get { return m_SessionHeader; } }

        /// <summary>
        /// The raw GLF data stream.
        /// </summary>
        internal Stream RawStream
        {
            get
            {
                EnsureDataLoaded();
                return m_RawStream;
            }
        }

        /// <summary>
        /// Sets the position of the packet stream to the start of packet data and returns the stream.
        /// </summary>
        /// <returns>A readable Stream, positioned at the start of packet data.</returns>
        public Stream GetPacketStreamStart()
        {
            EnsureDataLoaded();
            if (m_FileHeader.MajorVersion > 1)
            {
                m_RawStream.Position = m_FileHeader.DataOffset;
                m_PacketStream = new GZipStream(m_RawStream, CompressionMode.Decompress, true);
                // m_PacketStream = new DeflateStream(m_RawStream, CompressionMode.Decompress, true);
                return m_PacketStream;
            }
            else
            {
                m_RawStream.Position = m_FileHeader.DataOffset;
                return m_RawStream;
            }
        }

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
            if (!m_IsDisposed)
            {
                m_IsDisposed = true; // Only Dispose stuff once

                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case

                    // We already have a Close(), so just invoke that
                    Close();
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here
            }
        }

        #endregion
    }
}