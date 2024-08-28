using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;

namespace Gibraltar.Data
{
    /// <summary>
    /// A very simple implementation of the Package type for use within the agent
    /// </summary>
    /// <remarks>Unlike the full package implementation this form has no index and does not merge session fragments.</remarks>
    public class SimplePackage : IDisposable
    {
        protected const string LogCategory = "Loupe.Repository.Package";
        protected const string FragmentsFolder = "SessionFragments";

        private readonly object m_Lock = new object();
        private readonly Dictionary<Guid, SessionFileInfo<SessionFragmentZipEntry>> m_Sessions = new();

        private ZipArchive m_Archive;
        private string m_TempFileName; // Used when we are creating a transient package to track the file for cleanup.
        private volatile bool m_Disposed;

        public event CollectionChangeEventHandler CollectionChanged;

        #region protected class SessionFragmentZipEntry

        /// <summary>
        /// Tracks session fragment header information for a fragment in the package
        /// </summary>
        protected class SessionFragmentZipEntry
        {
            /// <summary>
            /// The session header from the fragment
            /// </summary>
            public SessionHeader Header { get; set; }

            /// <summary>
            /// The length of the fragment file in bytes (uncompressed)
            /// </summary>
            public long FileSize { get; set; }

            /// <summary>
            /// The <seealso cref="ZipArchiveEntry"/>
            /// </summary>
            public ZipArchiveEntry ZipEntry { get; set; }
        }

        #endregion

        /// <summary>
        /// Create a new, empty package, writing to the provided stream.
        /// </summary>
        public SimplePackage()
        {
            //we are a new package, don't know what we are yet
            Caption = "New Package";

            //We are dirty - we're unsaved.
            IsDirty = true;

            if (!Log.SilentMode)
                Log.Write(LogMessageSeverity.Information, LogCategory, "Creating new package.", null);

            //assigning a temporary directory we'll extract everything into
            var tempPath = Path.GetTempPath();

            Directory.CreateDirectory(tempPath);
            var tempFileName = Path.Combine(tempPath, Path.GetRandomFileName());

            //and create it now so the zip file can be opened.
            m_Archive = ZipFile.Open(tempFileName, ZipArchiveMode.Create);

            //Note we didn't set the FileNamePath property - this is to signal to our code that
            //we're running in a temp space.
            m_TempFileName = tempFileName;

            SupportsFragments = true;
        }

        /// <summary>
        /// Create a new package by loading the specified file.  It must be a package file, not a log file.
        /// </summary>
        /// <remarks>To load any single GLF or GLP file into a new Package object see the static method LoadFileAsPackage().</remarks>
        /// <param name="fileNamePath">The full path to the package file to open.</param>
        public SimplePackage(string fileNamePath)
        {
            if (string.IsNullOrEmpty(fileNamePath))
            {
                throw new ArgumentNullException(nameof(fileNamePath), "Unable to create package from file because no file was specified.");
            }

            if (File.Exists(fileNamePath) == false)
            {
                throw new FileNotFoundException("No existing package could be found with the provided file name", fileNamePath);
            }

            //open the zip archive - if the file doesn't exist we want to fail now.
            m_Archive = ZipFile.Open(fileNamePath, ZipArchiveMode.Read);

            Description = FileNamePath = fileNamePath;

            Caption = Path.GetFileNameWithoutExtension(FileNamePath);

            //and we start out not dirty (setting properties can make us dirty).
            IsDirty = false;

            //See if we are a legacy package or not by checking for the database file.
            if (m_Archive.GetEntry("index.vdb3") != null) // no index means we support fragments.
            {
                SupportsFragments = false;

                IsReadOnly = true; // we can't update legacy packages
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Adds the provided session to the package
        /// </summary>
        /// <param name="sessionStream"></param>
        public void AddSession(Stream sessionStream)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("Package is read only and can't be modified");

            using (var glfReader = new GLFReader(sessionStream)) // This will dispose the stream when it is disposed.
            {
                if (!glfReader.IsSessionStream)
                    throw new GibraltarException("The data stream provided is not a valid session data stream.");

                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Verbose, LogCategory, "Stream is session file, attempting to load", null);

                lock (m_Lock)
                {
                    ClearSessionCache();

                    //If the archive is in read mode we need to re-open it in an editable mode.
                    if (m_Archive.Mode == ZipArchiveMode.Read)
                    {
                        m_Archive.Dispose();
                        m_Archive = ZipFile.Open(GetWorkingFileNamePath(), ZipArchiveMode.Update);
                    }

                    //Add this stream to our zip archive
                    var fileName = glfReader.SessionHeader.HasFileInfo ? GenerateFragmentPath(glfReader.SessionHeader.Id, glfReader.SessionHeader.FileId)
                        : GenerateSessionPath(glfReader.SessionHeader.Id);

                    ZipArchiveEntry fragmentEntry;
                    if (m_Archive.Mode == ZipArchiveMode.Update) //as opposed to create, which it would be if it was brand new.
                    {
                        fragmentEntry = m_Archive.GetEntry(fileName);
                        if (fragmentEntry != null)
                        {
                            if (glfReader.SessionHeader.HasFileInfo) return; //it's already here, and fragments are immutable.

                            //Otherwise we have to delete it so we can add it.
                            fragmentEntry.Delete();
                        }
                    }

                    fragmentEntry = m_Archive.CreateEntry(fileName, CompressionLevel.NoCompression); //session files are already highly compressed, no reason to waste effort.

                    using (var zipStream = fragmentEntry.Open())
                    {
                        sessionStream.CopyTo(zipStream);
                    }

                    IsDirty = true;
                }

                OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Add, glfReader.SessionHeader.Id));
            }
        }

        /// <summary>
        /// Remove a session from the package
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns>True if any sessions were removed.</returns>
        public bool Remove(Guid sessionId)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("Package is read only and can't be modified");

            var itemRemoved = false;

            lock (m_Lock)
            {
                var fileTemplate = $"{FragmentsFolder}/{sessionId}";
                var sessionEntries = m_Archive.Entries.Where(e => e.FullName.StartsWith(fileTemplate)).ToList();

                foreach (var sessionEntry in sessionEntries)
                {
                    sessionEntry.Delete();

                    itemRemoved = true;

                    IsDirty = true;
                }
            }

            OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Remove, sessionId));

            return itemRemoved;
        }

        /// <summary>
        /// Remove a list of sessions from the package
        /// </summary>
        /// <returns>True if any sessions were removed.</returns>
        public bool Remove(IList<Guid> sessionIds)
        {
            bool itemRemoved = false;

            foreach (var sessionId in sessionIds)
            {
                var removed = Remove(sessionId);

                itemRemoved = itemRemoved || removed;
            }

            return itemRemoved;
        }

        /// <summary>
        /// Save the package, overwriting any existing data
        /// </summary>
        /// <param name="progressMonitors"></param>
        public void Save(ProgressMonitorStack progressMonitors)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("Package is read only and can't be modified");

            if (string.IsNullOrEmpty(FileNamePath))
            {
                throw new FileNotFoundException("Unable to save the current package because no path has been set to save to.");
            }

            Save(progressMonitors, FileNamePath);
        }

        /// <summary>
        /// Save the package to the specified file name and path, overwriting any existing data
        /// </summary>
        /// <param name="progressMonitors"></param>
        /// <param name="fileNamePath"></param>
        public void Save(ProgressMonitorStack progressMonitors, string fileNamePath)
        {
            if (string.IsNullOrEmpty(fileNamePath))
            {
                throw new ArgumentNullException(nameof(fileNamePath), "Unable to save the current package because no path has been set to save to.");
            }

            //normalize the destination
            fileNamePath = Path.GetFullPath(fileNamePath);

            //make sure the path exists so we can save into it.
            FileSystemTools.EnsurePathExists(fileNamePath);

            lock (m_Lock)
            {
                var steps = 2;
                var completedSteps = 0;
                using (var ourMonitor = progressMonitors.NewMonitor(this, "Saving Package", steps))
                {
                    //Do the save (if this fails, we failed)
                    ourMonitor.Update("Saving Package File to Disk", completedSteps++);

#if NET8_0_OR_GREATER
                    m_Archive.Comment = Description;
#endif
                    m_Archive.Dispose(); // ZipArchive saves as it goes, so we just have to dispose it to get it to finalize.
                    m_Archive = null;

                    //If this is our first save then we are establishing the filename.
                    if (string.IsNullOrEmpty(m_TempFileName) == false)
                    {
                        File.Copy(m_TempFileName, fileNamePath, true);
                        try
                        {
                            File.Delete(m_TempFileName);
                        }
                        catch (Exception ex)
                        {
                            if (!Log.SilentMode) Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory,
                                "Unable to Delete Package Temporary File due to " + ex.GetType(),
                                "Unable to delete the temporary working file '{0}'. This means we'll use more disk space than we should but otherwise should not impact the application.",
                                m_TempFileName);

                        }

                        // And clear the temp path - we're no longer at that path.
                        m_TempFileName = null;
                    }
                    else if (fileNamePath.Equals(FileNamePath, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        File.Copy(FileNamePath, fileNamePath, true);
                    }

                    ourMonitor.Update("Confirming package contents", completedSteps++);

                    //Since we were successful at saving, this is our new path and we are no longer dirty.
                    FileNamePath = fileNamePath;
                    Caption = Path.GetFileNameWithoutExtension(FileNamePath);
                    m_Archive = ZipFile.Open(FileNamePath, ZipArchiveMode.Read); // We can open in read/write mode because we know it exists.
                    ClearSessionCache();
                    IsDirty = false;
                }
            }
        }

        /// <summary>
        /// Write the entire package to the provided stream (which must be seekable)
        /// </summary>
        /// <param name="stream"></param>
        public void Write(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanWrite)
                throw new ArgumentException("The provided stream must be writable.", nameof(stream));

            lock (m_Lock)
            {
                // Ensure the archive is properly disposed before trying to access the file directly.
                m_Archive.Dispose();

                // Open the temporary file for reading.
                using (var fileStream = new FileStream(GetWorkingFileNamePath(), FileMode.Open, FileAccess.Read))
                {
                    fileStream.CopyTo(stream);
                }

                // Since we've closed the archive, we need to reopen it if the class continues to be used.
                m_Archive = ZipFile.Open(GetWorkingFileNamePath(), ZipArchiveMode.Read);
            }
        }

        /// <summary>
        /// The display caption for the package
        /// </summary>
        public string Caption { get; set; }

        /// <summary>
        /// The end user display description for the package
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Indicates if there is unsaved data in the repository.
        /// </summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Indicates if the repository is read only (sessions can't be added or removed).
        /// </summary>
        /// <remarks>Legacy packages are read only since we can't update them in a compatible fashion</remarks>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Determines if the provided file is likely to be a Loupe Package
        /// </summary>
        /// <param name="fileNamePath">The file to check if it's a Loupe package</param>
        /// <returns></returns>
        public static bool IsPackage(string fileNamePath)
        {
            var zipFileHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

            if (File.Exists(fileNamePath))
            {
                using (var fileStream = File.OpenRead(fileNamePath))
                {
                    var fileHeader = new byte[4];
                    var readLength = fileStream.Read(fileHeader, 0, fileHeader.Length);
                    if (readLength >= 4)
                    {
                        // make sure the header is a zip file.
                        for (var i = 0; i < fileHeader.Length; i++)
                        {
                            if (fileHeader[i] != zipFileHeader[i])
                            {
                                return false; // The headers do not match
                            }
                        }
                        return true; // The headers match
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Indicates if the repository supports fragment files or not.  Most do.
        /// </summary>
        /// <remarks>Legacy packages do not support fragments, newer packages do.</remarks>
        public bool SupportsFragments { get; private set; }

        /// <summary>
        /// The current full path to the package.  It may be null or empty if this package has never been saved.
        /// </summary>
        public string FileNamePath { get; private set; }

        /// <summary>
        /// Clear all the sessions from the package
        /// </summary>
        public void Clear()
        {
            if (IsReadOnly)
                throw new InvalidOperationException("Package is read only and can't be modified");

            lock (m_Lock)
            {
                ClearSessionCache();

                //now wipe them out the fragments
                var deadEntries = m_Archive.Entries.Where(e => e.FullName.StartsWith(FragmentsFolder, StringComparison.OrdinalIgnoreCase));
                foreach (var zipArchiveEntry in deadEntries)
                {
                    zipArchiveEntry.Delete();
                }
            }
        }

        /// <summary>
        /// Get summary statistics about the sessions in the repository
        /// </summary>
        /// <param name="sessions">The number of sessions in the entire repository</param>
        /// <param name="files">The number of files for all the sessions in the repository</param>
        /// <param name="fileBytes">The total number of bytes used by files in the repository</param>
        /// <param name="problemSessions">The number of sessions with problems in the folder and all folders it contains</param>
        /// <remarks>Problems are crashed sessions or sessions with critical or error messages.</remarks>
        public void GetStats(out int sessions, out int problemSessions, out int files, out long fileBytes)
        {
            problemSessions = 0;
            fileBytes = 0;
            files = 0;

            lock (m_Lock)
            {
                EnsureIndexLoaded(false);

                sessions = m_Sessions.Count;

                foreach (var session in m_Sessions.Values)
                {
                    if ((session.Header.ErrorCount > 0)
                        || (session.Header.CriticalCount > 0)
                        || (session.Header.StatusName.Equals(SessionStatus.Crashed.ToString(), StringComparison.OrdinalIgnoreCase)))
                    {
                        problemSessions++;
                    }

                    files += session.Fragments.Count;
                    foreach (var fragment in session.Fragments)
                    {
                        fileBytes += fragment.ZipEntry.Length;
                    }
                }
            }
        }

        /// <summary>
        /// Get the list of session headers currently in the package
        /// </summary>
        public IList<SessionHeader> GetSessionHeaders()
        {
            lock (m_Lock)
            {
                EnsureIndexLoaded(false);

                // Snapshot the session headers only from our cache.  The zip entries aren't safe outside our lock.
                var headers = m_Sessions.Select(s => s.Value.Header).ToList();

                return headers;
            }
        }

        /// <summary>
        /// Load the specified session
        /// </summary>
        /// <returns>The loaded session.  If no session can be found with the specified Id an ArgumentOutOfRangeException will be thrown.</returns>
        public Session GetSession(Guid sessionId, Guid? fileId = null)
        {
            Session requestedSession = null;
            lock (m_Lock)
            {
                //make sure our index is clean and usable
                EnsureIndexLoaded(false);

                if (m_Sessions.TryGetValue(sessionId, out var sessionFileInfo) == false)
                {
                    throw new ArgumentOutOfRangeException(nameof(sessionId), "There is no session in the package with the provided id");
                }

                //now load up all the session fragments.
                var loadingCollection = new SessionCollection();

                foreach (var fragment in sessionFileInfo.Fragments)
                {
                    if (fileId != null)
                    {
                        //we need to check the file Id to see if it's what they requested
                        var header = LoadSessionHeader(fragment.ZipEntry); //this carefully just reads the header bytes to save perf.
                        if (header.FileId != fileId.Value)
                        {
                            continue;
                        }
                    }

                    //we need a seek-able stream - so we'll extract the fragment into a temp file and go with that.
                    var tempCopy = new TempFileStream(fragment.ZipEntry.Open());
                    requestedSession = loadingCollection.Add(tempCopy, true);
                }
            }

            if (requestedSession == null)
                throw new ArgumentOutOfRangeException(nameof(fileId), "There is no session file in the package with the provided file id");

            return requestedSession;
        }

        /// <summary>
        /// Retrieve the ids of the sessions files known locally for the specified session
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns>A list of the file Ids for the specified session found in the local repository.  If the session doesn't exist an empty list is returned.</returns>
        public IList<Guid> GetSessionFileIds(Guid sessionId)
        {
            lock (m_Lock)
            {
                EnsureIndexLoaded(false);

                if (m_Sessions.TryGetValue(sessionId, out var sessionFileInfo) == false)
                {
                    //This is a change from previous to match IRepository behavior.
                    return new List<Guid>();
                }

                var fileIds = new List<Guid>();
                foreach (var fragment in sessionFileInfo.Fragments)
                {
                    //this is kinda crappy - we have to make a copy of the whole fragment to get a seekable stream to read the id.
                    using (var tempCopy = new TempFileStream(fragment.ZipEntry.Open()))
                    using (var reader = new GLFReader(tempCopy))
                    {
                        fileIds.Add(reader.SessionHeader.FileId);
                    }
                }

                return fileIds;
            }
        }

        /// <summary>
        /// Dispose managed and unmanaged resources
        /// </summary>
        /// <param name="releaseManaged">Indicates if it's safe to release the managed resources</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (m_Disposed == false)
            {
                m_Disposed = true;

                if (releaseManaged)
                {
                    //and we have to dispose of our zip file so we can be sure we release the lock on it immediately.
                    if (m_Archive != null)
                    {
                        m_Archive.Dispose();
                        m_Archive = null;
                    }

                    // Plus, we don't want to leave a temp file around if they were just using us for transient generation.
                    if (m_TempFileName != null)
                    {
                        try
                        {
                            File.Delete(m_TempFileName);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called whenever the collection changes.
        /// </summary>
        /// <param name="e"></param>
        /// <remarks>Note to inheritors:  If overriding this method, you must call the base implementation to ensure
        /// that the appropriate events are raised.</remarks>
        protected virtual void OnCollectionChanged(CollectionChangeEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Get the session headers for all session fragments in the package
        /// </summary>
        /// <returns></returns>
        protected IList<SessionFragmentZipEntry> GetSessionFileHeaders()
        {
            lock (m_Lock)
            {
                EnsureIndexLoaded(false);

                // Snapshot the session headers only from our cache.  The zip entries aren't safe outside our lock.
                var headers = m_Sessions.SelectMany(s => s.Value.Fragments)
                    .ToList();

                return headers;
            }
        }

        private void AddSessionHeaderToIndex(SessionHeader sessionFragmentHeader, ZipArchiveEntry sessionFragment)
        {
            var fragmentEntry = new SessionFragmentZipEntry
            {
                Header = sessionFragmentHeader,
                ZipEntry = sessionFragment
            };

            lock (m_Lock)
            {
                if (m_Sessions.TryGetValue(sessionFragmentHeader.Id, out var sessionFileInfo))
                {
                    //add this file fragment to the existing session info
                    sessionFileInfo.AddFragment(sessionFragmentHeader, fragmentEntry, true);
                }
                else
                {
                    //create a new session file info - this is the first we've seen this session.
                    sessionFileInfo = new SessionFileInfo<SessionFragmentZipEntry>(sessionFragmentHeader, fragmentEntry, true);
                    m_Sessions.Add(sessionFileInfo.Id, sessionFileInfo);
                }
            }
        }

        /// <summary>
        /// Load the index from the current archive by reading all the file fragments.
        /// </summary>
        private void EnsureIndexLoaded(bool forceLoad)
        {
            lock (m_Lock)
            {
                if ((forceLoad == false) && (IsDirty == false) && (m_Sessions.Count > 0))
                {
                    return; //we previously loaded and there's been no change to make us need to reload.
                }

                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Information, LogCategory, "Loading package file", "File name: {0}", FileNamePath);

                ClearSessionCache();

                //To reliably read the index data we have to flush it.
                m_Archive.Dispose();
                IsDirty = false;

                // And for our data to be usable we have to open the archive in read mode (otherwise properties like Length aren't available)
                m_Archive = ZipFile.Open(GetWorkingFileNamePath(), ZipArchiveMode.Read);

                //we need to load up an index of these items
                foreach (var zipEntry in m_Archive.Entries)
                {
                    //we're looking for a specific pattern of names
                    if (((zipEntry.FullName.StartsWith(FragmentsFolder, StringComparison.OrdinalIgnoreCase)) //Where a simple package stores its fragments
                        || (zipEntry.FullName.EndsWith(Log.PackageExtension, StringComparison.OrdinalIgnoreCase)))) //A normal package with a damaged index
                    {
                        try
                        {
                            var sessionHeader = LoadSessionHeader(zipEntry);

                            if (sessionHeader != null)
                            {
                                AddSessionHeaderToIndex(sessionHeader, zipEntry);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, true, LogCategory,
                                    "Unable to parse file fragment name in simple package due to " + ex.GetType(), "File Name: {0}\r\nException: {1}", zipEntry.Name, ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// the path within the archive for a session with no file fragment information
        /// </summary>
        protected static string GenerateSessionPath(Guid sessionId)
        {
            return $"{FragmentsFolder}/{sessionId}.{Log.LogExtension}";
        }

        /// <summary>
        /// The path within the archive for a session fragment file
        /// </summary>
        protected static string GenerateFragmentPath(Guid sessionId, Guid fileId)
        {
            return $"{FragmentsFolder}/{sessionId}~{fileId}.{Log.LogExtension}";
        }

        /// <summary>
        /// A lock to control access to the zip archive
        /// </summary>
        protected object Lock => m_Lock;

        /// <summary>
        /// The zip archive.
        /// </summary>
        protected ZipArchive Archive => m_Archive;

        /// <summary>
        /// The file name and path to the working archive file
        /// </summary>
        /// <returns></returns>
        private string GetWorkingFileNamePath()
        {
            return m_TempFileName ?? FileNamePath;
        }

        /// <summary>
        /// Invalidate our session cache
        /// </summary>
        private void ClearSessionCache()
        {
            lock (m_Lock)
            {
                m_Sessions.Clear();
            }
        }


        /// <summary>
        /// Attempt to load the session header from the specified entry, returning null if it can't be loaded
        /// </summary>
        /// <param name="zipEntry">The current entry</param>
        /// <returns>The session header, or null if it can't be loaded</returns>
        private static SessionHeader LoadSessionHeader(ZipArchiveEntry zipEntry)
        {
            if (!Log.SilentMode)
                Log.Write(LogMessageSeverity.Verbose, LogCategory, "Opening session file from zip file to read header", "Opening zip entry '{0}'.", zipEntry.Name);

            SessionHeader header = null;

            try
            {
                using (var sourceFile = zipEntry.Open())
                {
                    //we need a SEEKABLE stream, so we need to make a memory stream to be seek-able.


                    //read the file header first to find out how large the session header is (which can be any size)
                    var fileHeaderBuffer = new byte[FileHeader.HeaderSize];
                    sourceFile.Read(fileHeaderBuffer, 0, fileHeaderBuffer.Length);
                    FileHeader fileHeader = null;
                    using (var memoryStream = new MemoryStream(fileHeaderBuffer))
                    {
                        if (GLFReader.IsGLF(memoryStream, out fileHeader) == false)
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Verbose, LogCategory, "Session file does not check out as a GLF file, skipping", "Zip entry: '{0}'.", zipEntry.Name);
                            return header;
                        }
                    }

                    var sessionHeaderBuffer = new byte[fileHeader.DataOffset];
                    //we have to copy the file header into it because we can't re-read those bytes (non-seek-able stream)
                    fileHeaderBuffer.CopyTo(sessionHeaderBuffer, 0);
                    sourceFile.Read(sessionHeaderBuffer, fileHeaderBuffer.Length, sessionHeaderBuffer.Length - fileHeaderBuffer.Length);
                    using (var memoryStream = new MemoryStream(sessionHeaderBuffer))
                    {
                        using (var sourceGlfFile = new GLFReader(memoryStream))
                        {
                            if (sourceGlfFile.IsSessionStream)
                                header = sourceGlfFile.SessionHeader;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Unexpected exception while attempting to load a session header",
                        "While opening the zip file entry '{0}' an exception was thrown reading the session header. Since this routine is designed to not generate exceptions this may indicate a flaw in the logic of the routine.\r\nException: {1}\r\n",
                        zipEntry.Name, ex.Message);
            }

            return header;
        }
    }
}
