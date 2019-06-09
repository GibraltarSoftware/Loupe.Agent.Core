using System;
using System.IO;
using System.Runtime.InteropServices;


namespace Gibraltar.Data
{
    /// <summary>
    /// A wrapper for conveniently holding a file lock where the stream access is not necessarily needed.
    /// </summary>
    public sealed class FileLock : IDisposable
    {
        // Or these could be used in release, too, if access to them is needed.
        private readonly string m_FileName;
        private readonly FileMode m_CreationMode;
        private readonly FileShare m_FileShare;
        private readonly FileAccess m_FileAccess;
        private readonly bool m_DeleteOnClose;

        private FileStream m_FileStream;
        private bool m_HaveStream;
        private bool m_IsWindows;

        private FileLock(string fileName, FileMode creationMode, FileAccess fileAccess, FileShare fileShare, bool manualDeleteOnClose)
        {
            m_FileName = fileName;
            m_CreationMode = creationMode;
            m_FileShare = fileShare;
            m_FileAccess = fileAccess;
            m_DeleteOnClose = manualDeleteOnClose;
            m_IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        internal FileLock(FileStream fileStream, string fileName, FileMode creationMode, FileAccess fileAccess, FileShare fileShare, bool manualDeleteOnClose)
            : this(fileName, creationMode, fileAccess, fileShare, manualDeleteOnClose)
        {
            m_FileStream = fileStream;
            m_HaveStream = (m_FileStream != null);
            m_IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

#if DEBUG
        /// <summary>
        /// The file name locked by this instance. (DEBUG only)
        /// </summary>
        public string FileName { get { return m_FileName; } }

        /// <summary>
        /// The CreationMode used to obtain this lock. (DEBUG only)
        /// </summary>
        public FileMode CreationMode { get { return m_CreationMode; } }

        /// <summary>
        /// The FileAccess used by this lock. (DEBUG only)
        /// </summary>
        public FileAccess FileAccess { get { return m_FileAccess; } }

        /// <summary>
        /// The FileShare allowed by this lock. (DEBUG only)
        /// </summary>
        public FileShare FileShare { get { return m_FileShare; } }
#endif


        /// <summary>
        /// Release the file lock and the resources held by this instance.
        /// </summary>
        public void Dispose()
        {
            //On non-windows systems, locks are on the file not the directory so we need to delete the file before we release it.
            if (m_DeleteOnClose && m_IsWindows == false)
            {
                // For Mono, delete it while we still have it open (exclusively) to avoid a race condition.
                FileHelper.SafeDeleteFile(m_FileName); // Opens don't stop deletes!
            }

            if (m_HaveStream)
                m_FileStream.Dispose();

            m_HaveStream = false;
            m_FileStream = null;

            //and now we try to delete it if we were supposed to.
            if (m_DeleteOnClose && m_IsWindows)
            {
                // Not Mono, we can only delete it after we have closed it.
                FileHelper.SafeDeleteFile(m_FileName); // Delete will fail if anyone else has it open.  That's okay.
            }
        }
    }
}
