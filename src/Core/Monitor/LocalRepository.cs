using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using Gibraltar.Data;
using Gibraltar.Data.Internal;
using Gibraltar.Messaging;
using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// The local collection repository, a minimalistic repository
    /// </summary>
    public class LocalRepository : IRepository
    {
        /// <summary>
        /// The log category
        /// </summary>
        protected const string LogCategory = "Loupe.Local Repository";

        private const string RepositoryTempFolder = "temp";
        internal const string RepositoryArchiveFolder = "archive";
        internal const string RepositoryKeyFile = "repository.gak";
        internal const string ComputerKeyFile = "computer.gak";

        private readonly object m_Lock = new object();
        private readonly object m_QueueLock = new object();
        private readonly Queue<RefreshRequest> m_RefreshRequests = new Queue<RefreshRequest>(); //protected by QUEUELOCK
        private readonly string m_Caption;
        private readonly string m_RepositoryPath;
        private readonly Guid m_RepositoryId;
        private readonly string m_RepositoryTempPath;
        private readonly string m_SessionLockFolder;

        private Dictionary<Guid, SessionFileInfo<FileInfo>> m_SessionCache; //protected by LOCK
        private bool m_LoggingEnabled;
        private string m_RepositoryArchivePath;
        private bool m_AsyncRefreshThreadActive; //protected by QUEUELOCK

        #region Private Class RefreshRequest

        /// <summary>
        /// A single request to refresh our local cache of file information
        /// </summary>
        private class RefreshRequest
        {
            public RefreshRequest(bool force, SessionCriteria sessionCriteria)
            {
                Criteria = sessionCriteria;
                Force = force;
                Timestamp = DateTime.Now;
            }

            /// <summary>
            /// When the request was made
            /// </summary>
            public DateTime Timestamp { get; private set; }

            /// <summary>
            /// What sessions should be covered by the request
            /// </summary>
            public SessionCriteria Criteria { get; private set; }

            /// <summary>
            /// If a refresh should be forced even if we don't think the data is dirty
            /// </summary>
            public bool Force { get; private set; }

        }

        #endregion

        /// <summary>
        /// Raised every time the sessions collection changes.
        /// </summary>
        public event CollectionChangeEventHandler CollectionChanged;

        /// <summary>
        /// Open a specific local repository
        /// </summary>
        /// <param name="productName">The product name for operations in this repository</param>
        /// <param name="overridePath">The path to use instead of the default path for the repository</param>
        public LocalRepository(string productName, string overridePath = null)
        {
            m_Caption = productName;

            //now, before we can use the product name we need to make sure it doesn't have any illegal characters.
            m_RepositoryPath = CalculateRepositoryPath(productName, overridePath);
            m_RepositoryTempPath = Path.Combine(m_RepositoryPath, RepositoryTempFolder);
            m_SessionLockFolder = Path.Combine(m_RepositoryPath, FileMessenger.SessionLockFolderName);
            m_RepositoryArchivePath = Path.Combine(m_RepositoryPath, RepositoryArchiveFolder);

            //we want the directories to exist, but we don't worry about permissions because that should have already happened when the repository path was calculated.
            try
            {
                Directory.CreateDirectory(m_RepositoryTempPath);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
            }

            try
            {
                Directory.CreateDirectory(m_RepositoryArchivePath);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
            }

            using (GetMaintenanceLock())
            {
                //if the repository doesn't have a readme file and our basic information, create that.
                string readme = Path.Combine(m_RepositoryPath, "_readme.txt");
                if (File.Exists(readme) == false)
                {
                    try
                    {
                        File.WriteAllText(readme, "This directory contains log files.  You may delete log files (*.glf) safely, however renaming them is not recommended.");
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Unable to create readme file in repository", "Path: {0}\r\nException: {1}", m_RepositoryPath, ex.Message);
                    }
                }

                string repositoryIdFile = Path.Combine(m_RepositoryPath, RepositoryKeyFile);
                if (File.Exists(repositoryIdFile))
                {
                    //read back the existing repository id
                    try
                    {
                        string rawRepositoryId = File.ReadAllText(repositoryIdFile, Encoding.UTF8);
                        m_RepositoryId = new Guid(rawRepositoryId);
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Unable to read repository Id, a new one will be created", "Path: {0}\r\nException: {1}", m_RepositoryPath, ex.Message);
                    }
                }

                //create a new repository id
                if (m_RepositoryId == Guid.Empty)
                {
                    m_RepositoryId = Guid.NewGuid();
                    try
                    {
                        File.WriteAllText(repositoryIdFile, m_RepositoryId.ToString(), Encoding.UTF8);
                        File.SetAttributes(repositoryIdFile, FileAttributes.Hidden);
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Unable to store repository Id in repository.  This will lead to server integration challenges", "Path: {0}\r\nException: {1}", m_RepositoryPath, ex.Message);
                    }
                }
            }
        }

        #region Public Properties and Methods

        /// <summary>
        /// Calculate the best path for the log folder and the repository
        /// </summary>
        public static string CalculateRepositoryPath(string productName, string overridePath = null)
        {
            var repositoryFolder = PathManager.FindBestPath(PathType.Collection, overridePath);

            //now, we either just calculated a DEFAULT folder (which is just the base directory) or a final one.
            if (string.IsNullOrEmpty(overridePath))
            {
                //we may need to adjust product name - we have to make sure it's valid for being a directory.
                productName = FileSystemTools.SanitizeFileName(productName); //we use the more restrictive file name rules since we're doing just one directory
                repositoryFolder = Path.Combine(repositoryFolder, productName);
            }

            return repositoryFolder;
        }

        /// <summary>
        /// Calculate the best path for the default 
        /// </summary>
        public static string DefaultRepositoryPath { get { return PathManager.FindBestPath(PathType.Collection, null); } }

        /// <summary>
        /// A unique id for this repository.
        /// </summary>
        public Guid Id { get { return m_RepositoryId; } }

        /// <summary>
        /// Indicates if there are unsaved changes.
        /// </summary>
        public bool IsDirty { get { return false; } }

        /// <summary>
        /// Indicates if the repository is read only (sessions can't be added or removed).
        /// </summary>
        public bool IsReadOnly { get { return false; } }

        /// <summary>
        /// The unique name for this repository (typically the file name or URI).
        /// </summary>
        public string Name { get { return m_RepositoryPath; } }

        /// <summary>
        /// A short end-user caption to display for the repository.
        /// </summary>
        public string Caption { get { return m_Caption; } }

        /// <summary>
        /// An extended end-user description of the repository.
        /// </summary>
        public string Description { get { return m_RepositoryPath; } }

        /// <summary>
        /// Indicates if the repository supports fragment files or not.  Most do.
        /// </summary>
        public bool SupportsFragments { get { return true; } }

        /// <summary>
        /// The set of products, applications, and versions loaded into the repository
        /// </summary>
        public IList<IRepositoryProduct> Products { get { throw new NotImplementedException("Getting the set of products, applications, and versions from the local repository isn't implemented yet"); } }


        /// <summary>
        /// Add a session (full file stream or fragment of a session) to the repository
        /// </summary>
        /// <param name="sessionStream">A stream of the session data (full file or fragment) to add</param>
        /// <remarks><para>If the session already exists in the repository its information will be 
        /// merged with the provided session and the result saved in the repository</para>
        /// <para>If the stream is to a session file fragment then it will either be added
        /// to the set of fragments (if it doesn't exist) or ignored (if it does).</para>
        /// <para>The stream will be disposed of by the repository, potentially some time after
        /// the call is completed.  It must remain valid and unmodified for that time to ensure
        /// that the session can be processed correctly and efficiently.</para></remarks>
        /// <returns>True if the session was added, false if it already existed</returns>
        public bool AddSession(Stream sessionStream)
        {
            string fileNamePath;
            using (var sourceGlfFile = new GLFReader(sessionStream))
            {
                if (sourceGlfFile.IsSessionStream == false)
                {
                    throw new InvalidOperationException("The provided stream is not a session file");
                }

                SessionHeader sessionHeader = sourceGlfFile.SessionHeader;

                string fileName = string.Format("{0}-{1}-{2}.glf", sessionHeader.Id, sessionHeader.FileId, sessionHeader.FileSequence);
                fileNamePath = Path.Combine(m_RepositoryPath, fileName);

                lock (m_Lock)
                {
                    if (File.Exists(fileNamePath))
                        return false;

                    using (var fileStream = File.OpenWrite(fileNamePath))
                    {
                        sessionStream.Position = 0;
                        FileSystemTools.StreamContentCopy(sessionStream, fileStream, false);
                        fileStream.SetLength(fileStream.Position);
                    }

                    if (m_SessionCache != null)
                    {
                        //we have already loaded our cache so we have to poke this into it to be sure we're immediately current
                        SessionFileInfo<FileInfo> sessionFileInfo;
                        if (m_SessionCache.TryGetValue(sessionHeader.Id, out sessionFileInfo))
                        {
                            //add this file fragment to the existing session info
                            sessionFileInfo.AddFragment(sessionHeader, new FileInfo(fileNamePath), true);
                        }
                        else
                        {
                            //create a new session file info - this is the first we've seen this session.
                            sessionFileInfo = new SessionFileInfo<FileInfo>(sessionHeader, new FileInfo(fileNamePath), true);
                            m_SessionCache.Add(sessionFileInfo.Id, sessionFileInfo);
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Load a session by its Id
        /// </summary>
        /// <returns>A session object representing the specified session.  If no session can be
        /// found with the provided Id an exception will be thrown.</returns>
        /// <exception cref="FileNotFoundException" caption="FileNotFoundException">Thrown if no session exists with the specified Id</exception>
        /// <param name="sessionId">The unique Id of the session to be loaded.</param>
        /// <param name="fragmentIds">Optional.  The unique ids of the files to load</param>
        public Session GetSession(Guid sessionId, Guid[] fragmentIds = null)
        {
            Session requestedSession = null;

            //if we got a fragment list, make a lookup for it..
            var idLookup = new Dictionary<Guid, Guid>();
            if (fragmentIds != null)
            {
                foreach (var fragmentId in fragmentIds)
                {
                    idLookup.Add(fragmentId, fragmentId);
                }
            }

            lock (m_Lock)
            {
                EnsureCacheLoaded();

                SessionFileInfo<FileInfo> sessionFileInfo;
                if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo)) //we want the not found exception
                {
                    var loadingCollection = new SessionCollection();

                    foreach (var fragment in sessionFileInfo.Fragments)
                    {
                        if (fragmentIds != null)
                        {
                            //we want this particular fragment, not all fragments.
                            var header = LoadSessionHeader(fragment.FullName);
                            if (idLookup.ContainsKey(header.FileId) == false)
                                continue;
                        }
                        requestedSession = loadingCollection.Add(fragment.FullName);
                    }
                }
            }

            if (requestedSession == null)
                throw new FileNotFoundException("Unable to find any session in the repository with the id " + sessionId);

            return requestedSession;
        }

        /// <summary>
        /// Retrieve the ids of the sessions files known locally for the specified session
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public IList<Guid> GetSessionFileIds(Guid sessionId)
        {
            var sessionFileIds = new List<Guid>();
            lock (m_Lock)
            {
                EnsureCacheLoaded();

                SessionFileInfo<FileInfo> sessionFileInfo;
                if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo)) //we want the not found exception
                {
                    foreach (var fragment in sessionFileInfo.Fragments)
                    {
                        try
                        {
                            var header = LoadSessionHeader(fragment.FullName);
                            sessionFileIds.Add(header.FileId);
                        }
                        catch (Exception ex)
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, true, LogCategory, "Unable to read session fragment file header due to " + ex.GetType(), "We will skip this file for the session.\r\nSession Id: {0}\r\n{1}", sessionId, ex.Message);
                        }
                    }
                }
            }

            return sessionFileIds;
        }

        /// <summary>
        /// Load a session by its Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be loaded.</param>
        /// <returns>A session object representing the specified session.  If no session can be
        /// found with the provided Id an exception will be thrown.</returns>
        public ISession LoadSession(Guid sessionId)
        {
            throw new NotSupportedException("Loading sessions from a local repository isn't implemented yet.");
        }

        /// <summary>
        /// Indicates if the database should log operations to Gibraltar or not.
        /// </summary>
        public bool IsLoggingEnabled { get { return m_LoggingEnabled; } set { m_LoggingEnabled = value; } }

        /// <summary>
        /// Get a generic stream for the contents of a session
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to retrieve the stream for.</param>
        /// <returns>A stream that should be immediately copied and then disposed.  If no session could be found with the provided Id an exception will be thrown.</returns>
        public Stream LoadSessionStream(Guid sessionId)
        {
            Session requestedSession = GetSession(sessionId);

            //To avoid using up a lot of memory if the session is large we create a temporary file with the stream and then
            //return a seekable pointer to that.
            FileStream tempStream = FileSystemTools.GetTempFileStream();
            requestedSession.Write(tempStream);
            tempStream.Seek(0, SeekOrigin.Begin);
            return tempStream;
        }

        /// <summary>
        /// Get a generic stream for the contents of a session file
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to retrieve the stream for.</param>
        /// <param name="fileId">The unique Id of the session file to retrieve the stream for.</param>
        /// <returns>A stream that should be immediately copied and then disposed.  If no file could be found with the provided Id an exception will be thrown.</returns>
        public Stream LoadSessionFileStream(Guid sessionId, Guid fileId)
        {
            lock (m_Lock)
            {
                Stream existingFile;
                if (TryLoadSessionFileStream(sessionId, fileId, out existingFile))
                {
                    using (existingFile)
                    {
                        //To avoid using up a lot of memory if the session is large we create a temporary file with the stream and then
                        //return a seekable pointer to that.
                        FileStream tempStream = FileSystemTools.GetTempFileStream();
                        FileSystemTools.StreamContentCopy(existingFile, tempStream, true);
                        return tempStream;
                    }
                }
                else
                {
                    throw new InvalidOperationException("There is no session file with the Id " + fileId + " for session Id " + sessionId);
                }
            }
        }

        /// <summary>
        /// Try to get a stream pointing to a live file
        /// </summary>
        /// <returns>True if a file stream was found, false otherwise</returns>
        /// <returns>A stream that should be immediately copied and then disposed.</returns>
        public bool TryLoadSessionFileStream(Guid sessionId, Guid fileId, out Stream stream)
        {
            EnsureCacheLoaded();

            FileInfo file = null;
            stream = null;
            lock (m_Lock)
            {
                SessionFileInfo<FileInfo> sessionFileInfo;
                if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo) == false)
                {
                    return false;
                }

                file = FindFragment(sessionFileInfo, fileId);
                if (file == null)
                {
                    return false;
                }

                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
        }

        /// <summary>
        /// Find the session fragments in our local repository for the specified session Id.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="sessionHeader"></param>
        /// <param name="sessionFragments"></param>
        public void LoadSessionFiles(Guid sessionId, out SessionHeader sessionHeader, out IList<FileInfo> sessionFragments)
        {
            EnsureCacheLoaded();

            lock (m_Lock)
            {
                SessionFileInfo<FileInfo> sessionFileInfo;
                if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo))
                {
                    sessionHeader = sessionFileInfo.Header;
                    sessionFragments = sessionFileInfo.Fragments;
                }
                else
                {
                    sessionHeader = null;
                    sessionFragments = null;
                }
            }
        }

        /// <summary>
        /// Perform an immediate, synchronous refresh
        /// </summary>
        public void Refresh()
        {
            Refresh(false, true, SessionCriteria.AllSessions);
        }

        /// <summary>
        /// Update the local repository with the latest information from the file system
        /// </summary>
        public void Refresh(bool async, bool force)
        {
            Refresh(async, force, SessionCriteria.AllSessions);
        }

        /// <summary>
        /// Update the local repository with the latest information from the file system
        /// </summary>
        internal void Refresh(bool async, bool force, SessionCriteria sessionCriteria)
        {
            if (async)
            {
                //because we want minimize any possibility in holding up the caller (which could be the file system monitor)
                //we queue all requests and even use a dedicated lock to minimize contention
                lock(m_QueueLock)
                {
                    if (m_RefreshRequests.Count < 10) //circuit breaker for extreme cases
                        m_RefreshRequests.Enqueue(new RefreshRequest(force, sessionCriteria));

                    if (m_AsyncRefreshThreadActive == false)
                    {
                        m_AsyncRefreshThreadActive = true;
                        ThreadPool.QueueUserWorkItem(AsyncRefresh);
                    }
                }
            }
            else
            {
                PerformRefresh(force, sessionCriteria);
            }
        }

        /// <summary>
        /// Remove a session from the repository and all folders by its Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be removed</param>
        /// <returns>True if a session existed and was removed, false otherwise.</returns>
        /// <remarks>If no session is found with the specified Id then no exception is thrown.  Instead,
        /// false is returned.  If a session is found and removed True is returned.  If there is a problem
        /// removing the specified session (and it exists) then an exception is thrown.  The session will
        /// be removed from all folders that may reference it as well as user history and preferences.</remarks>
        public bool Remove(Guid sessionId)
        {
            lock (m_Lock)
            {
                EnsureCacheLoaded();

                SessionFileInfo<FileInfo> sessionFileInfo = null;
                if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo) == false)
                    return false; //can't remove what ain't there.

                //kill all of these files
                bool fileRemoved = false;
                var filesToRemove = new List<FileInfo>(sessionFileInfo.Fragments); //since the collection could get modified as we go.
                foreach (var fragment in filesToRemove)
                {
                    fileRemoved = FileHelper.SafeDeleteFile(fragment.FullName) || fileRemoved;
                }
                return fileRemoved;
            }
        }

        /// <summary>
        /// Remove sessions from the repository and all folders by its Id
        /// </summary>
        /// <param name="sessionIds">An array of the unique Ids of the sessions to be removed</param>
        /// <returns>True if a session existed and was removed, false otherwise.</returns>
        /// <remarks>If no sessions are found with the specified Ids then no exception is thrown.  Instead,
        /// false is returned.  If at least one session is found and removed True is returned.  If there is a problem
        /// removing one or more of the specified sessions (and it exists) then an exception is thrown.  The sessions will
        /// be removed from all folders that may reference it as well as user history and preferences.</remarks>
        public bool Remove(IList<Guid> sessionIds)
        {
            bool fileRemoved = false;
            foreach (var sessionId in sessionIds)
            {
                fileRemoved = Remove(sessionId) || fileRemoved;
            }
            return fileRemoved;
        }

        /// <summary>
        /// Remove a session from the repository and all folders by its Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be removed</param>
        /// <param name="fileId">The unique Id of the session fragment to be removed</param>
        /// <returns>True if a session existed and was removed, false otherwise.</returns>
        /// <remarks>If no session is found with the specified Id then no exception is thrown.  Instead,
        /// false is returned.  If a session is found and removed True is returned.  If there is a problem
        /// removing the specified session (and it exists) then an exception is thrown.  The session will
        /// be removed from all folders that may reference it as well as user history and preferences.</remarks>
        public bool Remove(Guid sessionId, Guid fileId)
        {
            lock (m_Lock)
            {
                EnsureCacheLoaded();

                SessionFileInfo<FileInfo> sessionFileInfo = null;
                if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo) == false)
                    return false; //can't remove what ain't there.

                //now scan the files in the cache to see if the file they want is still there.
                FileInfo victim = null;
                foreach (var fragment in sessionFileInfo.Fragments)
                {
                    var fileHeader = LoadSessionHeader(fragment.FullName);
                    if ((fileHeader != null) && (fileHeader.FileId == fileId))
                    {
                        victim = fragment;
                        break;
                    }
                }

                if (victim == null)
                    return false;

                return FileHelper.SafeDeleteFile(victim.FullName);
            }
        }

        /// <summary>
        /// Find if session data (more than just the header information) exists for a session with the provided Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be checked.</param>
        /// <returns>True if the repository has at least some session data in the repository, false otherwise.</returns>
        public bool SessionDataExists(Guid sessionId)
        {
            lock (m_Lock)
            {
                EnsureCacheLoaded();
                return m_SessionCache.ContainsKey(sessionId);
            }
        }

        /// <summary>
        /// Find if session data (more than just the header information) exists for a session with the provided Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be checked.</param>
        /// <param name="fileId">The unique Id of the session fragment to be checked.</param>
        /// <returns>True if the repository has the indicated session fragment in the repository, false otherwise.</returns>
        public bool SessionDataExists(Guid sessionId, Guid fileId)
        {
            lock (m_Lock)
            {
                EnsureCacheLoaded();

                FileInfo fragmentFileInfo = null;

                SessionFileInfo<FileInfo> sessionFileInfo;
                if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo))
                {
                    fragmentFileInfo = FindFragment(sessionFileInfo, fileId);
                }

                return (fragmentFileInfo != null);
            }
        }

        /// <summary>
        /// Find if a session exists with the provided Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be checked.</param>
        /// <returns>True if the session exists in the repository, false otherwise.</returns>
        public bool SessionExists(Guid sessionId)
        {
            lock (m_Lock)
            {
                EnsureCacheLoaded();
                return m_SessionCache.ContainsKey(sessionId);
            }
        }

        /// <summary>
        /// Find if the session is running with the provided Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be checked.</param>
        /// <returns>True if the session exists in the repository and is running, false otherwise.</returns>
        public bool SessionIsRunning(Guid sessionId)
        {
            lock (m_Lock)
            {
                EnsureCacheLoaded();

                SessionFileInfo<FileInfo> sessionFileInfo;
                if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo))
                {
                    return sessionFileInfo.IsRunning;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Set or clear the New flag for a sessions
        /// </summary>
        /// <param name="sessionId">The session to affect</param>
        /// <param name="isNew">True to mark the sessions as new, false to mark them as not new.</param>
        /// <returns>True if a session was changed, false otherwise.</returns>
        public bool SetSessionNew(Guid sessionId, bool isNew)
        {
            bool modifiedAnyFile = false;
            lock (m_Lock)
            {
                EnsureCacheLoaded();

                string destinationDirectory = isNew ? m_RepositoryPath : m_RepositoryArchivePath;

                modifiedAnyFile = SetSessionNew(destinationDirectory, sessionId, isNew);

                InvalidateCache();
            }

            return modifiedAnyFile;
        }

        /// <summary>
        /// Set or clear the New flag for a list of sessions
        /// </summary>
        /// <param name="sessionIds">The sessions to affect</param>
        /// <param name="isNew">True to mark the sessions as new, false to mark them as not new.</param>
        /// <returns>True if a session was changed, false otherwise.</returns>
        public bool SetSessionsNew(IList<Guid> sessionIds, bool isNew)
        {
            bool modifiedAnyFile = false;
            lock (m_Lock)
            {
                EnsureCacheLoaded();

                string destinationDirectory = isNew ? m_RepositoryPath : m_RepositoryArchivePath;

                foreach (var sessionId in sessionIds)
                {
                    modifiedAnyFile = SetSessionNew(destinationDirectory, sessionId, isNew);
                }

                InvalidateCache();
            }

            return modifiedAnyFile;
        }


        /// <summary>
        /// Retrieves all the sessions that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The <see cref="System.Predicate{T}">Predicate</see> delegate that defines the conditions of the sessions to search for.</param>
        /// <remarks>
        /// The <see cref="System.Predicate{T}">Predicate</see> is a delegate to a method that returns true if the object passed to it matches the
        /// conditions defined in the delegate. The sessions of the repository are individually passed to the <see cref="System.Predicate{T}">Predicate</see> delegate, moving forward in the List, starting with the first session and ending with the last session.
        /// </remarks>
        /// <returns>A List containing all the sessions that match the conditions defined by the specified predicate, if found; otherwise, an empty List.</returns>
        public ISessionSummaryCollection Find(Predicate<ISessionSummary> match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            var collection = new SessionSummaryCollection(this);

            lock (m_Lock)
            {
                EnsureCacheLoaded();

                foreach (var sessionFileInfo in m_SessionCache.Values)
                {
                    if (match(sessionFileInfo.Header))
                        collection.Add(sessionFileInfo.Header);
                }
            }

            return collection;
        }

        /// <summary>
        /// The set of all sessions in the repository.
        /// </summary>
        /// <remarks><para>This contains the summary information. To load the full contents of a
        /// a session where local data files are available use the LoadSession method.</para>
        /// <para>The supplied collection is a binding list and supports update events for the 
        /// individual sessions and contents of the repository.</para></remarks>
        public virtual ISessionSummaryCollection Sessions { get { throw new NotImplementedException("A general sessions collection isn't available in the raw local repository"); } }

        /// <summary>
        /// A temporary path within the repository that can be used to store working data
        /// </summary>
        public string TempPath { get { return m_RepositoryTempPath; } }

        /// <summary>
        /// Attempt to load the session header from the specified file, returning null if it can't be loaded
        /// </summary>
        /// <param name="sessionFileNamePath">The full file name &amp; path</param>
        /// <returns>The session header, or null if it can't be loaded</returns>
        public static SessionHeader LoadSessionHeader(string sessionFileNamePath)
        {
            SessionHeader header = null;

            try
            {
                FileStream sourceFile = null;
                using (sourceFile = FileHelper.OpenFileStream(sessionFileNamePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (sourceFile == null)
                    {
#if DEBUG
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Verbose, LogCategory, "Unable to open session file, it is probably locked",
                                      "While attempting to open the local session fragment at '{0}', probably because it is still being written to.",
                                      sessionFileNamePath);
#endif
                    }
                    else
                    {
                        if (GLFReader.IsGLF(sourceFile))
                        {
                            using (var sourceGlfFile = new GLFReader(sourceFile))
                            {
                                header = sourceGlfFile.SessionHeader;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Unexpected exception while attempting to load a session header",
                              "While opening the file '{0}' an exception was thrown reading the session header. This may indicate a corrupt file.\r\nException: {1}\r\n",
                              sessionFileNamePath, ex.Message);
            }

            return header;
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// The path on disk to the repository
        /// </summary>
        protected string RepositoryPath { get { return m_RepositoryPath; } }

        /// <summary>
        /// The path on disk to the repository session locks
        /// </summary>
        protected string RepositoryLockPath { get { return m_SessionLockFolder; } }

        /// <summary>
        /// Called whenever the collection changes.
        /// </summary>
        /// <param name="e"></param>
        /// <remarks>Note to inheritors:  If overriding this method, you must call the base implementation to ensure
        /// that the appropriate events are raised.</remarks>
        protected virtual void OnCollectionChanged(CollectionChangeEventArgs e)
        {
            //save the delegate field in a temporary field for thread safety
            CollectionChangeEventHandler tempEvent = CollectionChanged;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        /// <summary>
        /// Called by the base class to refresh cached data
        /// </summary>
        /// <param name="force"></param>
        protected virtual void OnRefresh(bool force)
        {

        }

        /// <summary>
        /// The current session cache
        /// </summary>
        protected virtual Dictionary<Guid, SessionFileInfo<FileInfo>> SessionCache
        {
            get
            {
                EnsureCacheLoaded();
                return m_SessionCache;
            }
        }

        #endregion

        #region Private Properties and Methods

        private void AsyncRefresh(object state)
        {
            bool exitSet = false;
            try
            {
                RefreshRequest request;
                do
                {
                    request = null;
                    lock(m_QueueLock)
                    {
                        if (m_RefreshRequests.Count == 0)
                        {
                            //the queue is empty, lets explicitly bail and mark that we're doing so to guarantee no race conditions with parties queuing.
                            m_AsyncRefreshThreadActive = false;
                            exitSet = true;
                            return;
                        }

                        request = m_RefreshRequests.Dequeue();
                    }

                    if (request != null)
                        PerformRefresh(request.Force, request.Criteria);

                } while (request != null);
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                    Log.RecordException(0, ex, null, LogCategory, true);
            }
            finally
            {
                if (!exitSet)
                {
                    //we want to be really, really sure we don't leave the thread active option set when we're no longer running even in ThreadAbort cases.
                    lock(m_QueueLock)
                    {
                        m_AsyncRefreshThreadActive = false;
                    }
                }
            }
        }

        private void PerformRefresh(bool force, SessionCriteria sessionCriteria)
        {
            if (force)
                UpdateCache(sessionCriteria);
            else
                InvalidateCache();

            OnRefresh(force);
        }

        private IDisposable GetMaintenanceLock()
        {
            return InterprocessLockManager.Lock(this, m_RepositoryPath, RepositoryMaintenance.MutiprocessLockName, 0, true);
        }

        private void EnsureCacheLoaded()
        {
            lock (m_Lock)
            {
                if (m_SessionCache == null)
                    UpdateCache();
            }
        }

        private void InvalidateCache()
        {
            lock (m_Lock)
            {
                m_SessionCache = null;
            }
        }

        /// <summary>
        /// Finds the specified file fragment in the provided session if it exists.
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="sessionFileInfo"></param>
        /// <returns></returns>
        private static FileInfo FindFragment(SessionFileInfo<FileInfo> sessionFileInfo, Guid fileId)
        {
            FileInfo file = null;
            foreach (var fileInfo in sessionFileInfo.Fragments)
            {
                var header = LoadSessionHeader(fileInfo.FullName);
                if ((header != null) && (header.FileId == fileId))
                {
                    file = fileInfo;
                    break;
                }
            }
            return file;
        }

        /// <summary>
        /// Immediately update the cache from disk
        /// </summary>
        protected void UpdateCache(SessionCriteria sessionCriteria = SessionCriteria.AllSessions)
        {
            lock (m_Lock)
            {
                var newSessionCache = LoadSessions(sessionCriteria);
                m_SessionCache = newSessionCache;
            }
        }

        /// <summary>
        /// Scan the repository directory for all of the log files for this repository an build an index on the fly
        /// </summary>
        /// <returns>A new index of the sessions in the folder</returns>
        private Dictionary<Guid, SessionFileInfo<FileInfo>> LoadSessions(SessionCriteria sessionCriteria = SessionCriteria.AllSessions)
        {
            if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Creating index of local repository sessions", "Repository Folder: {0}", m_RepositoryPath);

            Dictionary<Guid, SessionFileInfo<FileInfo>> sessions = new Dictionary<Guid, SessionFileInfo<FileInfo>>();
            Dictionary<Guid, SessionFileInfo<FileInfo>> crashConversionCandidates = new Dictionary<Guid, SessionFileInfo<FileInfo>>();

            //load up our index
            LoadSessionsFromDirectory(m_RepositoryPath, sessions, crashConversionCandidates, true);

            //optimization - for special case where we are only interested in new sessions and active we ignore the archive.
            if ((sessionCriteria & SessionCriteria.CompletedSessions) > 0)
                LoadSessionsFromDirectory(m_RepositoryArchivePath, sessions, crashConversionCandidates, false);

            //We have to check sessions from the regular and archive paths to see if they're running because
            //the true open session file won't be in any of these lists (it's locked)
            var runningSessionCache = new Dictionary<Guid, bool>();
            foreach (var sessionFileInfo in sessions.Values)
            {
                bool isRunning;
                if (runningSessionCache.TryGetValue(sessionFileInfo.Id, out isRunning) == false)
                {
                    runningSessionCache.Add(sessionFileInfo.Id, IsSessionRunning(sessionFileInfo.Id));
                }
                sessionFileInfo.IsRunning = isRunning;
            }

            //If our session load identified any running session files then perform crashed session conversion
            foreach (var sessionFileInfo in crashConversionCandidates.Values)
            {
                CheckAndPerformCrashedSessionConversion(sessionFileInfo);
            }

            if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Finished creating index of local repository sessions", "Repository Folder: {0}\r\nSessions found: {1}", m_RepositoryPath, sessions.Count);
            return sessions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="sessions"></param>
        /// <param name="crashConversionCandidates"></param>
        /// <param name="isNew"></param>
        private void LoadSessionsFromDirectory(string directory, Dictionary<Guid, SessionFileInfo<FileInfo>> sessions, Dictionary<Guid, SessionFileInfo<FileInfo>> crashConversionCandidates, bool isNew)
        {
            if (!Directory.Exists(directory))
                return;

            FileInfo[] allSessionFiles;
            try
            {
                //even though we just pre-checked it may have been deleted between there and here.
                var directoryInfo = new DirectoryInfo(directory);
                allSessionFiles = directoryInfo.GetFiles("*." + Log.LogExtension, SearchOption.TopDirectoryOnly);
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (UnauthorizedAccessException) //if we are in-flight deleting the directory we'll get this.
            {
                return;
            }

            foreach (var sessionFragment in allSessionFiles)
            {
                var sessionHeader = LoadSessionHeader(sessionFragment.FullName);

                if (sessionHeader == null)
                {
                    if (m_LoggingEnabled)
                        Log.Write(LogMessageSeverity.Information, LogCategory, "Skipping local repository fragment because the session header couldn't be loaded", "File: {0}", sessionFragment.Name);
                }
                else
                {
                    SessionFileInfo<FileInfo> sessionFileInfo;
                    if (sessions.TryGetValue(sessionHeader.Id, out sessionFileInfo))
                    {
                        //add this file fragment to the existing session info
                        sessionFileInfo.AddFragment(sessionHeader, sessionFragment, isNew);
                    }
                    else
                    {
                        //create a new session file info - this is the first we've seen this session.
                        sessionFileInfo = new SessionFileInfo<FileInfo>(sessionHeader, sessionFragment, isNew);
                        sessions.Add(sessionFileInfo.Id, sessionFileInfo);
                    }

                    //and if the session header thought it was running, we need to queue it for potential crashed session conversion.
                    if ((sessionHeader.Status == SessionStatus.Running) && (!crashConversionCandidates.ContainsKey(sessionHeader.Id)))
                        crashConversionCandidates.Add(sessionHeader.Id, sessionFileInfo);
                }
            }
        }

        /// <summary>
        /// Indicates if the current session is running
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        private bool IsSessionRunning(Guid sessionId)
        {
            //this method is faster than actually acquiring the lock 
            if (InterprocessLockManager.QueryLockAvailable(this, m_SessionLockFolder, sessionId.ToString()))
                return false;

            return true;
        }

        /// <summary>
        /// Checks a running session to see if it should be converted to a crashed session, and if so converts it.
        /// </summary>
        /// <param name="session">The full set of session information</param>
        /// <returns>True if it was changed, false otherwise.</returns>
        private bool CheckAndPerformCrashedSessionConversion(SessionFileInfo<FileInfo> session)
        {
            bool haveChanges = false;

            try
            {
                using (InterprocessLock sessionLock = GetRunningSessionLock(session.Id))
                {
                    if (sessionLock == null)
                        return haveChanges; // It's still locked (thus still running), continue to the next running session.

                    bool convertedCurrentSession = false;
                    foreach (var fileFragment in session.Fragments)
                    {
                        // And change each one to indicate that it's crashed.
                        if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Opening Session File", "Opening session {0} using file '{1}'.", session.Id, fileFragment.FullName);
                        FileStream sourceFile = null;
                        GLFReader sourceGlfFile = null;

                        try
                        {
                            sourceFile = FileHelper.OpenFileStream(fileFragment.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                            if (sourceFile == null)
                            {
                                if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to Mark Session as Crashed", "Unable to completely convert session {0} from being marked as running to crashed in repository at '{1}' because the fragment '{2}' could not be opened",
                                          session.Id, m_RepositoryPath, fileFragment.Name);

                                continue; // Otherwise, try the next fragment.
                            }

                            sourceGlfFile = new GLFReader(sourceFile);

                            //update the GLF to crashed
                            sourceGlfFile.SessionHeader.StatusName = SessionStatus.Crashed.ToString();
                            GLFWriter.UpdateSessionHeader(sourceGlfFile, sourceFile);
                            convertedCurrentSession = true;
                        }
                        catch (Exception ex)
                        {
                            GC.KeepAlive(ex);

                            if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Unable to Mark Session as Crashed", "Unable to completely convert session {0} from being marked as running to crashed in repository at '{1}' because the fragment '{2}' failed due to an exception:\r\n {2}",
                                      session.Id, m_RepositoryPath, fileFragment.Name, ex);
                        }
                        finally
                        {
                            //we have to dispose of our file writer
                            if (sourceGlfFile != null)
                            {
                                sourceGlfFile.Dispose();
                            }

                            if (sourceFile != null)
                            {
                                sourceFile.Dispose();
                            }
                        }
                    }

                    if (convertedCurrentSession)
                    {
                        // We've converted this session, so mark that we had changes...
                        haveChanges = true;
                        session.Header.StatusName = SessionStatus.Crashed.ToString(); //we have to update this or they'll not see the current status.
                        sessionLock.DisposeProxyOnClose = true; // We've removed session from index, so we won't need that lock again.
                    }
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to Mark Session as Crashed", "Unable to completely convert a session from being marked as running to crashed in repository at {0} due to an exception:\r\n {1}", m_RepositoryPath, ex);
            }

            return haveChanges;
        }

        /// <summary>
        /// Get the current running session lock
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns>Null if the lock couldn't be acquired, the InterprocessLock otherwise</returns>
        private InterprocessLock GetRunningSessionLock(Guid sessionId)
        {
            return InterprocessLockManager.Lock(this, m_SessionLockFolder, sessionId.ToString(), 0, true);
        }

        /// <summary>
        /// Create a consistent, sanitized file name for an auto send consent specification
        /// </summary>
        /// <param name="product"></param>
        /// <param name="applicationName"></param>
        /// <returns>The file name</returns>
        private static string GenerateConsentFileName(string product, string applicationName)
        {
            string file = null;
            if (string.IsNullOrEmpty(applicationName))
                file = string.Format("{0}.gasc", product);
            else
                file = string.Format("{0}_{1}.gasc", product, applicationName);

            return FileSystemTools.SanitizeFileName(file, true);
        }

        /// <summary>
        /// Changes the new status of a single session
        /// </summary>
        /// <param name="destinationDirectory"></param>
        /// <param name="sessionId"></param>
        /// <param name="isNew"></param>
        /// <returns></returns>
        private bool SetSessionNew(string destinationDirectory, Guid sessionId, bool isNew)
        {
            bool modifiedAnyFile = false;
            SessionFileInfo<FileInfo> sessionFileInfo;
            if (m_SessionCache.TryGetValue(sessionId, out sessionFileInfo))
            {
                foreach (var fragment in sessionFileInfo.Fragments)
                {
                    try
                    {
                        if (!destinationDirectory.Equals(fragment.DirectoryName, StringComparison.OrdinalIgnoreCase))
                        {
                            var destinationFileNamePath = Path.Combine(destinationDirectory, fragment.Name);

                            //make sure there isn't a file already there (can happen in rare race conditions)
                            FileHelper.SafeDeleteFile(destinationFileNamePath);

                            //and then move the file to the new location.
                            Directory.CreateDirectory(destinationDirectory);
                            fragment.MoveTo(Path.Combine(destinationDirectory, fragment.Name));
                            modifiedAnyFile = true;
                        }
                    }
                    catch (FileNotFoundException ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Information, LogWriteMode.Queued, ex, true, LogCategory, "While changing a session fragment file new state to " + isNew + " the file was not found", 
                                "It's most likely already been moved to the appropriate status or was deleted.\r\nSession Id: {0}\r\nFragment: {1}\r\nException:\r\n{2}: {3}", sessionId, fragment.Name, ex.GetType(), ex.Message);
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, true, LogCategory, "Unable to update session fragment new state to " + isNew + " due to " + ex.GetType(), 
                                "It's most likely in use by another process, so we'll have another opportunity to get it later.\r\nSession Id: {0}\r\nFragment: {1}\r\nException:\r\n{2}: {3}", sessionId, fragment.Name, ex.GetType(), ex.Message);
                    }
                }

                //and if we moved it, drop it from the cache so we don't try to touch it again this round (we'll dump the whole cache in a second)
                m_SessionCache.Remove(sessionId);
            }

            return modifiedAnyFile;
        }

        #endregion
    }
}
