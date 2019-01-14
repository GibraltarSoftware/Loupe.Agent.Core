using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Gibraltar.Messaging;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;

namespace Gibraltar.Data.Internal
{
    /// <summary>
    /// Performs repository level maintenance such as purging for size.  Should be used with collection repositories only.
    /// </summary>
    public class RepositoryMaintenance: IDisposable
    {
        /// <summary>
        /// The name of the repository lock used to serialize repository maintenance.
        /// </summary>
        public const string MutiprocessLockName = "Maintenance";

        private const string LogCategory = "Loupe.Repository.Maintenance";

        private readonly object m_Lock = new object();
        private readonly string m_RepositoryPath;
        private readonly string m_RepositoryArchivePath;

        private volatile bool m_PerformingMaintenance; //locked by m_Lock.
        private bool m_Disposed;
        private bool m_LoggingEnabled;
        private string m_SessionLockFolder;

        /// <summary>
        /// Raised every time the sessions collection changes.
        /// </summary>
        public event CollectionChangeEventHandler CollectionChanged;

        /// <summary>
        /// Create a repository maintenance object for the provided repository without the ability to perform pruning.
        /// </summary>
        /// <param name="repositoryPath"></param>
        /// <param name="loggingEnabled">Indicates if the maintenance process should log its actions.</param>
        public RepositoryMaintenance(string repositoryPath, bool loggingEnabled)
        {
            if (string.IsNullOrEmpty(repositoryPath))
            {
                throw new ArgumentNullException(nameof(repositoryPath));
            }

            //in this mode we aren't able to do things that use product/application.
            ProductName = null;
            ApplicationName = null;
            m_RepositoryPath = repositoryPath;
            m_SessionLockFolder = Path.Combine(m_RepositoryPath, FileMessenger.SessionLockFolderName);
            m_RepositoryArchivePath = Path.Combine(m_RepositoryPath, LocalRepository.RepositoryArchiveFolder);

            m_LoggingEnabled = loggingEnabled; //property does some propagation - safe for now, but don't risk it.
        }

        /// <summary>
        /// Create the repository maintenance object for the provided repository.
        /// </summary>
        /// <param name="repositoryPath">The full path to the base of the repository (which must contain an index)</param>
        /// <param name="productName">The product name of the application(s) to restrict pruning to.</param>
        /// <param name="applicationName">Optional.  The application within the product to restrict pruning to.</param>
        /// <param name="maxAgeDays">The maximum allowed days since the session fragment was closed to keep the fragment around.</param>
        /// <param name="maxSizeMegabytes">The maximum number of megabytes of session fragments to keep</param>
        /// <param name="loggingEnabled">Indicates if the maintenance process should log its actions.</param>
        public RepositoryMaintenance(string repositoryPath, string productName, string applicationName, int maxAgeDays, int maxSizeMegabytes, bool loggingEnabled)
        {
            if (string.IsNullOrEmpty(repositoryPath))
            {
                throw new ArgumentNullException(nameof(repositoryPath));
            }

            if (string.IsNullOrEmpty(productName))
            {
                throw new ArgumentNullException(nameof(productName));
            }

            ProductName = productName;
            ApplicationName = applicationName;
            MaxAgeDays = maxAgeDays;
            MaxSizeMegabytes = maxSizeMegabytes;
            m_RepositoryPath = repositoryPath;
            m_RepositoryArchivePath = Path.Combine(m_RepositoryPath, LocalRepository.RepositoryArchiveFolder);
            m_SessionLockFolder = Path.Combine(m_RepositoryPath, FileMessenger.SessionLockFolderName);

            m_LoggingEnabled = loggingEnabled; //property does some propagation - safe for now, but don't risk it.
        }

        #region Public Properties and Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (m_Disposed)
                return;

            // Call the underlying implementation
            Dispose(true);

            // SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The product to restrict purge operations to.
        /// </summary>
        public string ProductName { get; private set; }

        /// <summary>
        /// Optional.  The application name to restrict purge operations to.
        /// </summary>
        public string ApplicationName { get; private set; }

        /// <summary>
        /// The full path to the base of the repository that is being maintained
        /// </summary>
        public string RepositoryPath { get { return m_RepositoryPath; } }

        /// <summary>
        /// The maximum allowed days since the session fragment was closed to keep the fragment around.
        /// </summary>
        public int MaxAgeDays { get; set; }

        /// <summary>
        /// The maximum number of megabytes of session fragments to keep
        /// </summary>
        public int MaxSizeMegabytes { get; set; }

        /// <summary>
        /// The last time a maintenance run was started.
        /// </summary>
        public DateTimeOffset LastMaintenanceRunDateTime { get; private set; }

        /// <summary>
        /// Indicates if the database should log operations to Gibraltar or not.
        /// </summary>
        public bool IsLoggingEnabled
        {
            get
            {
                return m_LoggingEnabled;
            }
            set
            {
                m_LoggingEnabled = value;
            }
        }

        /// <summary>
        /// Indicates whether maintenance is currently being performed on the repository.
        /// </summary>
        public bool PerformingMaintenance
        {
            get
            {
                //marked as volatile so we'll have a very current value, but we're just reading.
                return m_PerformingMaintenance;
            }
        }

        /// <summary>
        /// Run the maintenance cycle.
        /// </summary>
        /// <param name="asyncronous">True to have maintenance performed on a background thread, allowing the current process to continue.</param>
        public void PerformMaintenance(bool asyncronous)
        {
            QueueAsyncMaintenance(AsyncPerformMaintenance, asyncronous);
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Attempts to execute the provided maintenance delegate on a dedicated thread.
        /// </summary>
        /// <param name="maintenanceDelegate">The maintenance delegate to execute</param>
        /// <param name="asyncronous">True to return as soon as the request has been submitted, false to wait for completion.</param>
        /// <remarks>If there is already a maintenance operation underway then a new request is not queued. If not set to asynchronous
        /// execution the call will block until the pending maintenance is complete.  If another maintainer is running then maintenance
        /// will be considered immediately complete.</remarks>
        protected void QueueAsyncMaintenance(WaitCallback maintenanceDelegate, bool asyncronous)
        {
            //if we're currently performing maintenance, return immediately.
            lock (m_Lock)
            {
                if (m_PerformingMaintenance == false)
                {
                    //otherwise, queue a maintenance action.  We always use the background thread for consistency, 
                    //the question is just whether or not we wait to return.
                    m_PerformingMaintenance = true;
                    LastMaintenanceRunDateTime = DateTimeOffset.Now;
                    ThreadPool.QueueUserWorkItem(maintenanceDelegate);
                }

                System.Threading.Monitor.PulseAll(m_Lock);
            }

            if (asyncronous == false)
            {
                //to convert to synchronous we now need to wait for the performing maintenance flag to be set to false.
                lock (m_Lock)
                {
                    while (m_PerformingMaintenance)
                    {
                        System.Threading.Monitor.Wait(m_Lock, 16); //wake back up 10 times a second to check if we're still in maintenance.
                    }

                    //now that we're done with the lock, make sure we notify every other waiting thread.
                    System.Threading.Monitor.PulseAll(m_Lock);
                }
            }
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        /// <remarks> Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.</remarks>
        protected virtual void Dispose(bool releaseManaged)
        {
            m_Disposed = true; //so we can catch it when we get multiply disposed.

            if (releaseManaged)
            {
                // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                // Other objects may be referenced in this case

                //make sure that we aren't performing maintenance in the background... we want to give it time to get its act together.
                lock (m_Lock)
                {
                    DateTimeOffset fileLockTimeout = DateTimeOffset.Now.AddMilliseconds(2000);

                    while ((m_PerformingMaintenance) && (fileLockTimeout > DateTimeOffset.Now))
                    {
                        System.Threading.Monitor.Wait(m_Lock, 100); //wake back up 10 times a second to check if we're still in maintenance.
                    }

                    //now that we're done with the lock, make sure we notify every other waiting thread.
                    System.Threading.Monitor.PulseAll(m_Lock);
                }
            }
            // Free native resources here (alloc's, etc)
            // May be called from within the finalizer, so don't reference other objects here
        }

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

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Execute repository maintenance on a background thread.
        /// </summary>
        private void AsyncPerformMaintenance(object state)
        {
            try
            {
                //make sure if we log something it never blocks, since that could ultimately result in a deadlock.
                Publisher.ThreadMustNotBlock();

                bool collectionChanged = false;

                //since external objects can freely modify our configuration, save a copy so we run with a consistent perspective.
                int maxSizeMegabytes = MaxSizeMegabytes;
                int maxAgeDays = MaxAgeDays;
                if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Information, LogCategory, "Starting Repository Maintenance", "Starting repository maintenance on repository stored at {0}, removing fragments older than {1} days and keeping the repository under {2:N0} megabytes.", RepositoryPath, maxAgeDays, maxSizeMegabytes);

                //before we do anything more, we have to get the maintenance lock.
                using (InterprocessLock maintenanceLock = InterprocessLockManager.Lock(this, m_RepositoryPath, MutiprocessLockName, 0, true))
                {
                    if(maintenanceLock == null)
                    {
                        //we couldn't get the lock, so no maintenance today.
                        if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to Perform Maintenance - Repository Locked", "Unable to get the maintenance file lock on the repository stored at {0}, not performing maintenance.", RepositoryPath);
                        return;
                    }

                    //We may not be able to do pruning - we used to be used just for index DB update (not valid any more)
                    if ((string.IsNullOrEmpty(ApplicationName) == false) && (Log.IsSessionEnding == false))
                    {
                        //find out if there is any maintenance to do - we start by age and it'll let us know if we have anything else to do.
                        bool capacityPruningRequired;
                        bool sessionsRemoved = ProcessPruneForAge(maxAgeDays, maxSizeMegabytes, out capacityPruningRequired);
                        collectionChanged = (collectionChanged || sessionsRemoved);

                        //make sure capacity pruning is enabled.
                        if (maxSizeMegabytes <= 0)
                            capacityPruningRequired = false; 

                        //anything more to do?
                        if ((capacityPruningRequired) && (Log.IsSessionEnding == false))
                        {
                            sessionsRemoved = ProcessPruneForSize(MaxSizeMegabytes);
                            collectionChanged = (collectionChanged || sessionsRemoved);
                        }

                        ////and do crashed session conversion.
                        //bool crashedSessionsChanged = ProcessCrashedSessionConversion();
                        //collectionChanged = (collectionChanged || crashedSessionsChanged);
                    }
                }

                if (collectionChanged)
                    OnCollectionChanged(new CollectionChangeEventArgs(CollectionChangeAction.Refresh, null));
                if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Information, LogCategory, "Repository Maintenance Complete", "Repository maintenance completed on repository stored at {0}", RepositoryPath);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to Perform Repository Maintenance", "Unable to complete repository maintenance successfully due to an exception: {0}", ex);
            }
            finally
            {
                //now we need to mark that we're done, and this is done with a lock for multithread purity.
                lock(m_Lock)
                {
                    m_PerformingMaintenance = false;
                    System.Threading.Monitor.PulseAll(m_Lock);
                }
            }
        }

        /// <summary>
        /// Removes old session fragments.
        /// </summary>
        /// <returns>True if any files were removed, false otherwise</returns>
        private bool ProcessPruneForAge(int maxAgeDays, int maxSizeMegabytes, out bool capacityPruningRequired)
        {
            int filesRemoved = 0;
            long fileBytesRemaining = 0;
            var fileFragments = AllExistingFileFragments();
            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-1 * maxAgeDays);

            //we don't need to sort this set because we're killing all of the old data.
            foreach (var fileFragment in fileFragments)
            {
                bool fileRemoved = false;
                if (fileFragment.CreationTimeUtc < cutoff)
                {
                    //but we need to peek and see if it's for a running session.  We only
                    //remove them for space reasons, not for age.
                    Guid? sessionId = SafeGetSessionId(fileFragment.FullName);

                    if((sessionId.HasValue) && (IsSessionRunning(sessionId.Value) == false))
                    {
                        //add this session in as our 
                        fileRemoved = RemoveSessionFragment(fileFragment);
                    }
                }
                if (fileRemoved)
                    filesRemoved++;
                else
                    fileBytesRemaining += fileFragment.Length;
            }

            if (filesRemoved > 0)
            {
                if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Information, LogCategory, "Files Pruned for Age", "Removed {0} files because they are older than the maximum age of {1} for session fragments.", filesRemoved, maxAgeDays);
            }

            capacityPruningRequired = ((fileBytesRemaining / (decimal)1048576) > maxSizeMegabytes); //bytes to MB

            return (filesRemoved > 0);
        }

        /// <summary>
        /// Remove the oldest files to get the total repository space down to the allowed limit.
        /// </summary>
        /// <param name="maxSizeMegabytes"></param>
        /// <returns>True if any files were removed, false otherwise</returns>
        private bool ProcessPruneForSize(int maxSizeMegabytes)
        {
            //exit early if we are shutting down
            if (Log.IsSessionEnding)
            {
                return false;
            }

            //get the set of all of the sessions in the repository so we can work out which ones to kill.
            int filesRemoved = 0;
            long bytesRemoved = 0;
            var fileFragments = AllExistingFileFragments();

            long maxSizeBytes = maxSizeMegabytes * 1024 * 1024;
            long currentSizeBytes = 0;
            DateTime runningSessionCutoffDateTime = DateTime.UtcNow.AddMinutes(-1); //we won't delete a file that wasn't closed at least one minutes ago.

            //now we need to sort the list of files 
            fileFragments.Sort(SortFilesByUpdateDateDesc);

            //and iterate these....
            foreach (var fileFragment in fileFragments)
            {
                //Add this to our current set of files and see if we're now over the line.  If so, we start deleting here.
                bool fileRemoved = false;
                if ((currentSizeBytes + fileFragment.Length > maxSizeBytes) && (fileFragment.LastWriteTimeUtc < runningSessionCutoffDateTime))
                {
                    //we may not be able to actually remove the file.  it really shouldn't happen, but it could.
                    fileRemoved = RemoveSessionFragment(fileFragment);
                }

                if (fileRemoved)
                {
                    bytesRemoved += fileFragment.Length;
                    filesRemoved++;
                }
                else
                {
                    //this file is staying, add it to our on-disk size.
                    currentSizeBytes += fileFragment.Length;                        
                }

                //exit early if we are shutting down
                if (Log.IsSessionEnding)
                {
                    break;
                }
            }

            if (filesRemoved > 0) 
            {
                if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Information, LogCategory, "Files Pruned for Size", "Removed {0} files to free up {1:N0} bytes.", filesRemoved, bytesRemoved);
            }

            return (filesRemoved > 0);
        }

        private List<FileInfo> AllExistingFileFragments()
        {
            string fragmentPattern = string.Format("{0}*.{1}", FileMessenger.SessionFileNamePrefix(ProductName, ApplicationName), Log.LogExtension);

            var repository = new DirectoryInfo(m_RepositoryPath);
            var fileFragments = new List<FileInfo>(repository.GetFiles(fragmentPattern, SearchOption.TopDirectoryOnly));
            if (Directory.Exists(m_RepositoryArchivePath))
            {
                var archiveFolder = new DirectoryInfo(m_RepositoryArchivePath);
                fileFragments.AddRange(archiveFolder.GetFiles(fragmentPattern));
            }
            return fileFragments;
        }

        private bool RemoveSessionFragment(FileInfo victimFile)
        {
            bool fileRemoved = false;

            //make sure the file no longer exists on disk
            try
            {
                //see if we can get a lock on the file; if not we won't be able to delete it.
                using (FileLock fileLock = FileHelper.GetFileLock(victimFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Delete))
                {
                    if (fileLock != null)
                    {
                        victimFile.Delete();
                        fileRemoved = true;
                    }
                    else
                    {
                        if (victimFile.Exists)
                        {
                            if (m_LoggingEnabled)
                                Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to Delete Session Fragment", "Unable to delete the session fragment at '{0}' because it could not be locked.", victimFile.FullName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                if (m_LoggingEnabled) Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Unable to Delete Session Fragment", "Unable to delete the session fragment at '{0}' because of an exception: {1}", victimFile.FullName, ex);
            }

            return fileRemoved;
        }

        /// <summary>
        /// Attempt to load the session Id from the specified file, returning null if it can't be loaded.
        /// </summary>
        /// <param name="sessionFileNamePath"></param>
        /// <returns></returns>
        private Guid? SafeGetSessionId(string sessionFileNamePath)
        {
            var sessionHeader = SafeGetSessionHeader(sessionFileNamePath);

            return sessionHeader == null ? (Guid?)null : sessionHeader.Id;
        }

        /// <summary>
        /// Attempt to load the session header from the specified file, returning null if it can't be loaded
        /// </summary>
        /// <param name="sessionFileNamePath">The full file name &amp; path</param>
        /// <returns>The session header, or null if it can't be loaded</returns>
        private SessionHeader SafeGetSessionHeader(string sessionFileNamePath)
        {
#if DEBUG
            if (m_LoggingEnabled)
                Log.Write(LogMessageSeverity.Verbose, LogCategory, "Opening session file to read header", "Opening file {0}.", sessionFileNamePath);
#endif
            SessionHeader header = null;

            try
            {
                FileStream sourceFile = null;
                using (sourceFile = FileHelper.OpenFileStream(sessionFileNamePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (sourceFile == null)
                    {
                        if (m_LoggingEnabled)
                            Log.Write(LogMessageSeverity.Verbose, LogCategory, "Unable to open session file, it is probably locked",
                                      "While attempting to open the local session fragment at '{0}', probably because it is still being written to.",
                                      sessionFileNamePath);
                    }

                    using (var sourceGlfFile = new GLFReader(sourceFile))
                    {
                        if (sourceGlfFile.IsSessionStream)
                            header = sourceGlfFile.SessionHeader;
                    }
                }
            }
            catch (Exception ex)
            {
                if (m_LoggingEnabled)
                    Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Unexpected exception while attempting to load a session header",
                        "While opening the file '{0}' an exception was thrown reading the session header. Since this routine is designed to not generate exceptions this may indicate a flaw in the logic of the routine.\r\nException: {1}\r\n",
                        sessionFileNamePath, ex.Message);
            }

            return header;
        }

        /// <summary>
        /// Sorts by LastWrite, then create time, then name as a last attempt.
        /// </summary>
        /// <param name="leftFile"></param>
        /// <param name="rightFile"></param>
        /// <returns></returns>
        private static int SortFilesByUpdateDateDesc(FileInfo leftFile, FileInfo rightFile)
        {
            int returnVal = leftFile.LastWriteTimeUtc.CompareTo(rightFile.LastWriteTimeUtc);

            if (returnVal == 0)
                returnVal = leftFile.CreationTimeUtc.CompareTo(rightFile.LastWriteTimeUtc);

            if (returnVal == 0)
                returnVal = String.Compare(leftFile.FullName, rightFile.FullName, StringComparison.Ordinal); //full name to be sure we get a unique value.

            return -1 * Math.Sign(returnVal); //what we did above was an ascending compare, we want the descending.
        }

        private bool IsSessionRunning(Guid sessionId)
        {
            // Hmmm, is it really done, or still running after calling EndSession()?
            using (InterprocessLock sessionLock = InterprocessLockManager.Lock(this, m_SessionLockFolder, sessionId.ToString(), 0, true))
            {
                if (sessionLock == null)
                {
                    // It's holding the lock, so it must still be active!
                    return true;
                }
                // Otherwise, it has released the lock, so it really is closed as it said.  We're good.
                return false;
            }
        }

        #endregion
    }
}