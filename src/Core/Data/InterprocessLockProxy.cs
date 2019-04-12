using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Gibraltar.Data
{
    /// <summary>
    /// A class to hold a file lock for this process (app domain) and pass it fairly to other waiting threads before release.
    /// </summary>
    internal class InterprocessLockProxy
    {
        private const int LockPollingDelay = 16; // 16 ms wait between attempts to open a lock file.
        private const int BackOffDelay = LockPollingDelay * 3; // 48 ms wait when another process requests a turn.
        private const string LogCategory = "Loupe.Interprocess Lock.Proxy";

        private readonly Queue<InterprocessLock> m_WaitQueue = new Queue<InterprocessLock>();
        private readonly object m_QueueLock = new object();
        private readonly string m_IndexPath;
        private readonly string m_LockName;
        private readonly string m_LockFullFileNamePath;
        private readonly bool m_DeleteOnClose; // File persistence policy for this lock (should delete unless a high-traffic lock).
        private readonly ILogger m_Logger;

        private InterprocessLock m_CurrentLockTurn;
        private FileLock m_FileLock;
        private FileLock m_LockRequest;
        private DateTimeOffset m_MinTimeNextTurn = DateTimeOffset.MinValue;
        private bool m_DisposeOnClose; // Object persistence policy for this instance (should delete if not a reused lock).
        private bool m_Disposed;


        /// <summary>
        /// Raised when the lock is disposed.
        /// </summary>
        internal event EventHandler Disposed;

        internal InterprocessLockProxy(string indexPath, string lockName, bool deleteOnClose)
        {
            m_Logger = ApplicationLogging.CreateLogger<InterprocessLock>();

            m_IndexPath = indexPath;
            m_LockName = lockName;
            m_DeleteOnClose = deleteOnClose;
            m_LockFullFileNamePath = GetLockFileName(indexPath, lockName);
        }

        #region Public Properties and Methods

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The full (unique) name for the lock, combining the index path and lock name.
        /// </summary>
        public string FullName { get { return m_LockFullFileNamePath; } }

        /// <summary>
        /// The name of the repository this lock controls access to.
        /// </summary>
        public string IndexPath { get { return m_IndexPath; } }

        /// <summary>
        /// The name of the lock within the repository.
        /// </summary>
        public string LockName { get { return m_LockName; } }

        /// <summary>
        /// Whether this lock instance has been disposed (and thus does not hold any locks).
        /// </summary>
        public bool IsDisposed { get { return m_Disposed; } }

        /// <summary>
        /// Reports how many threads are in the queue waiting on the lock (some may have timed out and given up already).
        /// (Reports -1 if the proxy is idle (no current turn).)
        /// </summary>
        public int WaitingCount { get { return (m_CurrentLockTurn == null) ? -1 : m_WaitQueue.Count; } }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The lock request with the current turn to hold or wait for the lock.
        /// </summary>
        internal InterprocessLock CurrentLockTurn { get { return m_CurrentLockTurn; } }

        /// <summary>
        /// The requesting owner of the current turn for the lock.
        /// </summary>
        internal object CurrentTurnOwner { get { return m_CurrentLockTurn == null ? null : m_CurrentLockTurn.Owner; } }

        /// <summary>
        /// The thread with the current turn for the lock.
        /// </summary>
        internal Thread CurrentTurnThread { get { return m_CurrentLockTurn == null ? null : m_CurrentLockTurn.OwningThread; } }

        /// <summary>
        /// The ManagedThreadId of the thread with the current turn for the lock, or -1 if none.  (For debug convenience only.)
        /// </summary>
        internal int CurrentTurnThreadId
        {
            get
            {
                if (m_CurrentLockTurn == null)
                    return -1; // TODO or 0?

                return m_CurrentLockTurn.OwningThread.ManagedThreadId;
            }
        }

        /// <summary>
        /// Object persistence policy for this instance:  Whether to dispose this instance when file lock is released.
        /// </summary>
        internal bool DisposeOnClose
        {
            get { return m_DisposeOnClose; }
            set { m_DisposeOnClose = value; }
        }

        /// <summary>
        /// Check the thread with the current turn for the lock and grant a secondary lock if applicable.
        /// </summary>
        /// <param name="candidateLock">An unexpired lock request on the current thread, or null to just check the turn thread.</param>
        /// <returns>The Thread with the current turn for the lock, or null if there are none holding or waiting.</returns>
        internal Thread CheckCurrentTurnThread(InterprocessLock candidateLock)
        {
            if (candidateLock != null && candidateLock.OwningThread != Thread.CurrentThread)
                throw new InvalidOperationException("A lock request may only be waited on by the thread which created it.");

            lock (m_QueueLock)
            {
                if (m_CurrentLockTurn != null)
                {
                    Thread currentOwningThread = m_CurrentLockTurn.OwningThread;
                    if (candidateLock != null && Thread.CurrentThread == currentOwningThread)
                    {
                        candidateLock.GrantTheLock(m_CurrentLockTurn); // Set it as a secondary lock on that holder (same thread).
                        if (candidateLock.ActualLock == m_CurrentLockTurn) // Sanity-check that it was successful.
                            candidateLock.OurLockProxy = this; // So its dispose-on-close setting pass-through can function.
                    }

                    return currentOwningThread; // Whether it's a match or some other thread.
                }

                return null; // No thread owns the lock.
            }
        }

        /// <summary>
        /// Queue a lock request (RepositoryLock instance).  Must be followed by a call to AwaitOurTurnOrTimeout (which can block).
        /// </summary>
        /// <param name="lockRequest"></param>
        internal void QueueRequest(InterprocessLock lockRequest)
        {
            if (string.Equals(lockRequest.FullName, m_LockFullFileNamePath, StringComparison.OrdinalIgnoreCase) == false)
                throw new InvalidOperationException("A lock request may not be queued to a proxy for a different full name.");

            if (lockRequest.OwningThread != Thread.CurrentThread)
                throw new InvalidOperationException("A lock request may only be queued by the thread which created it.");

            lock (m_QueueLock)
            {
                m_WaitQueue.Enqueue(lockRequest);
            }
        }

        /// <summary>
        /// Wait for our turn to have the lock (and wait for the lock) up to our time limit
        /// </summary>
        /// <param name="lockRequest"></param>
        /// <returns></returns>
        internal bool AwaitOurTurnOrTimeout(InterprocessLock lockRequest)
        {
            if (lockRequest.IsExpired)
                throw new InvalidOperationException("Can't wait on an expired lock request.");

            if (string.Equals(lockRequest.FullName, m_LockFullFileNamePath, StringComparison.OrdinalIgnoreCase) == false)
                throw new InvalidOperationException("A lock request may not be queued to a proxy for a different full name.");

            if (lockRequest.OwningThread != Thread.CurrentThread)
                throw new InvalidOperationException("A lock request may only be waited on by the thread which created it.");

            lockRequest.OurLockProxy = this; // Mark the request as pending with us.

            // Do NOT clear out current lock owner, this will allow DequeueNextRequest to find one already there, if any.
            bool ourTurn = StartNextTurn(lockRequest); // Gets its own queue lock.
            if (ourTurn == false)
            {
                // It's not our turn yet, we need to wait our turn.  Are we willing to wait?
                if (lockRequest.WaitForLock && lockRequest.WaitTimeout > DateTimeOffset.Now)
                    ourTurn = lockRequest.AwaitTurnOrTimeout();

                // Still not our turn?
                if (ourTurn == false)
                {
                    if (!CommonCentralLogic.SilentMode)
                    {
                        // Who actually has the lock right now?
                        if (m_CurrentLockTurn != null)
                        {
                            Thread currentOwningThread = m_CurrentLockTurn.OwningThread;
                            int currentOwningThreadId = -1;
                            string currentOwningThreadName = "null";
                            if (currentOwningThread != null) // To make sure we can't get a null-ref exception from logging this...
                            {
                                currentOwningThreadId = currentOwningThread.ManagedThreadId;
                                currentOwningThreadName = currentOwningThread.Name ?? string.Empty;
                            }

                            m_Logger.LogDebug("{0}\r\nA lock request gave up because it is still being held by another thread.\r\n" +
                                                          "Lock file: {1}\r\nCurrent holding thread: {2} ({3})",
                                                          lockRequest.WaitForLock ? "Lock request timed out" : "Lock request couldn't wait",
                                                          m_LockFullFileNamePath, currentOwningThreadId, currentOwningThreadName);
                        }
                        else
                        {
                            m_Logger.LogError("Lock request turn error\r\nA lock request failed to get its turn but the current lock turn is null.  " +
                                              "This probably should not happen.\r\nLock file: {0}\r\n", m_LockFullFileNamePath);
                        }
                    }

                    lockRequest.Dispose(); // Expire the request.
                    return false; // Failed to get the lock.  Time to give up.
                }
            }

            // Yay, now it's our turn!  Do we already hold the lock?

            bool validLock;
            if (m_FileLock != null)
                validLock = true; // It's our request's turn and this proxy already holds the lock!
            else
                validLock = TryGetLock(lockRequest); // Can we get the lock?

            // Do we actually have the lock now?
            if (validLock)
            {
                lockRequest.GrantTheLock(lockRequest); // It owns the actual lock itself now.
            }
            else
            {
                if (!CommonCentralLogic.SilentMode)
                {
                    m_Logger.LogTrace("{0}\r\nA lock request gave up because it could not obtain the file lock.  " +
                                                  "It is most likely still held by another process.\r\nLock file: {1}",
                                                  lockRequest.WaitForLock ? "Lock request timed out" : "Lock request couldn't wait",
                                                  m_LockFullFileNamePath);
                }

                lockRequest.Dispose(); // Failed to get the lock.  Expire the request and give up.
            }

            return validLock;
        }

        internal static string GetLockFileName(string indexPath, string lockName)
        {
            return Path.Combine(indexPath, lockName + "." + InterprocessLock.LockFileExtension);
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Try to get the actual file lock on behalf of the current request.
        /// </summary>
        /// <param name="currentRequest"></param>
        /// <returns></returns>
        private bool TryGetLock(InterprocessLock currentRequest)
        {
            bool waitForLock = currentRequest.WaitForLock;
            DateTimeOffset lockTimeout = currentRequest.WaitTimeout;
            bool validLock = false;

            while (waitForLock == false || DateTimeOffset.Now < lockTimeout)
            {
                if (DateTimeOffset.Now >= m_MinTimeNextTurn) // Make sure we aren't in a back-off delay.
                {
                    m_FileLock = GetFileLock(m_LockFullFileNamePath); // TODO: DeleteOnClose no longer supported in our file opens.
                    if (m_FileLock != null)
                    {
                        // We have the lock!  Close our lock request if we have one so later we can detect if anyone else does.
                        if (m_LockRequest != null)
                        {
                            m_LockRequest.Dispose();
                            m_LockRequest = null;
                        }

                        validLock = true; // Report that we have the lock now.
                    }
                }
                // Otherwise, just pretend we couldn't get the lock in this attempt.

                if (validLock == false && waitForLock)
                {
                    // We didn't get the lock and we want to wait for it, so try to open a lock request.
                    if (m_LockRequest == null)
                        m_LockRequest = GetLockRequest(m_LockFullFileNamePath); // Tell the other process we'd like a turn.

                    // Then we should allow some real time to pass before trying again because file opens aren't very fast.
                    Thread.Sleep(LockPollingDelay);
                }
                else
                {
                    // We either got the lock or the user doesn't want to keep retrying, so exit the loop.
                    break;
                }
            }

            return validLock;
        }

        /// <summary>
        /// Find the next request still waiting and signal it to go.  Or return true if the current caller may proceed.
        /// </summary>
        /// <param name="currentRequest">The request the caller is waiting on, or null for none.</param>
        /// <returns>True if the caller's supplied request is the next turn, false otherwise.</returns>
        private bool StartNextTurn(InterprocessLock currentRequest)
        {
            lock (m_QueueLock)
            {
                int dequeueCount = DequeueNextRequest(); // Find the next turn if there isn't one already underway.
                if (m_CurrentLockTurn != null)
                {
                    // If we popped a new turn off the queue make sure it gets started.
                    if (dequeueCount > 0)
                        m_CurrentLockTurn.SignalMyTurn(); // Signal the thread waiting on that request to proceed.

                    if (ReferenceEquals(m_CurrentLockTurn, currentRequest)) // Is the current request the next turn?
                    {
                        return true; // Yes, so skip waiting and just tell our caller they can go ahead (and wait for the lock).
                    }
                }
                else
                {
                    // Otherwise, nothing else is waiting on the lock!  Time to shut it down.

                    if (m_LockRequest != null)
                    {
                        m_LockRequest.Dispose(); // Release the lock request (an open read) since we're no longer waiting on it.
                        m_LockRequest = null;
                    }

                    if (m_FileLock != null)
                    {
                        m_FileLock.Dispose(); // Release the OS file lock.
                        m_FileLock = null;
                    }

                    if (m_DisposeOnClose)
                        Dispose();
                }

                return false;
            }
        }

        private int DequeueNextRequest()
        {
            lock (m_QueueLock)
            {
                int dequeueCount = 0;

                // Make sure we don't thread-abort in the middle of this logic.
                try
                {
                }
                finally
                {
                    while (m_CurrentLockTurn == null && m_WaitQueue.Count > 0)
                    {
                        m_CurrentLockTurn = m_WaitQueue.Dequeue();
                        dequeueCount++;

                        if (m_CurrentLockTurn.IsExpired)
                        {
                            m_CurrentLockTurn.Dispose(); // There's no one waiting on that request, so just discard it.
                            m_CurrentLockTurn = null; // Get the next one (if any) on next loop.
                        }
                        else
                        {
                            m_CurrentLockTurn.Disposed += Lock_Disposed; // Subscribe to their Disposed event.  Now we care.
                        }
                    }
                }

                return dequeueCount;
            }
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        private void Dispose(bool releaseManaged)
        {
            if (releaseManaged)
            {
                // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                // Other objects may be referenced in this case

                lock (m_QueueLock)
                {
                    if (!m_Disposed)
                    {
                        m_Disposed = true; // Make sure we don't do it more than once.

                        // Empty our queue (although it should already be empty!).
                        while (m_WaitQueue.Count > 0)
                        {
                            InterprocessLock lockInstance = m_WaitQueue.Dequeue();
                            //lockInstance.Disposed -= Lock_Disposed; // Suppress the events, don't start new turns!
                            lockInstance.Dispose(); // Tell any threads still waiting that their request has expired.
                        }

                        if (m_CurrentLockTurn == null)
                        {
                            // No thread is currently prepared to do this, so clear them here.
                            if (m_LockRequest != null)
                            {
                                m_LockRequest.Dispose();
                                m_LockRequest = null;
                            }

                            if (m_FileLock != null)
                            {
                                m_FileLock.Dispose();
                                m_FileLock = null;
                            }
                        }

                        // We're not fully disposed until the current lock owner gets disposed so we can release the lock.
                        // But fire the event to tell the RepositoryLockManager that we are no longer a valid proxy.
                        OnDispose();
                    }
                }
            }
            else
            {
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here

                // But we need to make sure the file opens get cleaned up, so we will anyway if we have to.... ???
                if (m_LockRequest != null)
                {
                    m_LockRequest.Dispose();
                    m_LockRequest = null;
                }

                if (m_FileLock != null)
                {
                    m_FileLock.Dispose();
                    m_FileLock = null;
                }
            }
        }

        private void OnDispose()
        {
            EventHandler tempEvent = Disposed;

            if (tempEvent != null)
            {
                tempEvent.Invoke(this, new EventArgs());
            }
        }

        private void Lock_Disposed(object sender, EventArgs e)
        {
            InterprocessLock disposingLock = (InterprocessLock)sender;
            disposingLock.Disposed -= Lock_Disposed; // Unsubscribe.

            //we need to remove this object from the lock collection
            lock (m_QueueLock)
            {
                // Only remove the lock if the one we're disposing is the original top-level lock for that key.
                if (m_CurrentLockTurn == null || ReferenceEquals(m_CurrentLockTurn, disposingLock) == false)
                    return; // Wasn't our current holder, so we don't care about it.

                m_CurrentLockTurn = null; // It's disposed, no longer current owner.

                if (m_Disposed == false)
                {
                    // We're releasing the lock for this thread.  We need to check if any other process has a request pending.
                    // And if so, we need to force this process to wait a minimum delay, even if we don't have one waiting now.
                    if (m_FileLock != null && CheckLockRequest(m_LockFullFileNamePath))
                    {
                        m_MinTimeNextTurn = DateTimeOffset.Now.AddMilliseconds(BackOffDelay); // Back off for a bit.
                        m_FileLock.Dispose(); // We have to give up the OS lock because other processes need a chance.
                        m_FileLock = null;
                    }

                    StartNextTurn(null); // Find and signal the next turn to go ahead (also handles all-done).
                }
                else
                {
                    // We're already disposed, so we'd better release the lock and request now if we still have them!
                    if (m_LockRequest != null)
                    {
                        m_LockRequest.Dispose();
                        m_LockRequest = null;
                    }

                    if (m_FileLock != null)
                    {
                        m_FileLock.Dispose();
                        m_FileLock = null;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to get an exclusive lock on a specified file.
        /// </summary>
        /// <param name="lockFullFileNamePath">The full-path file name for the lock file.</param>
        /// <returns>A file stream to the maintenance file if locked, null otherwise</returns>
        /// <remarks>Callers should check the provided handle for null to ensure they got the lock on the file.
        /// If it is not null, it must be disposed to release the lock in a timely manner.</remarks>
        private FileLock GetFileLock(string lockFullFileNamePath)
        {
            FileLock fileLock = null;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lockFullFileNamePath));
            }
            catch (UnauthorizedAccessException ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                m_Logger.LogWarning("Unable to create directory to index path, locking will not be feasible.  Exception: {0}", ex);
#endif
                throw; // we aren't going to try to spinlock on this.. we failed.
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                m_Logger.LogWarning("Unable to create directory to index path, locking will not be feasible.  Exception: {0}", ex);
#endif
            }

            try
            {
                // We share Read so that other processes who desire the lock can open for read to signal that to us.
                // (Except on Mono we have to use a separate file for the request signal, but this is still okay for the lock.)
                fileLock = OpenFileAccess(lockFullFileNamePath, FileAccess.Write, FileShare.Read, m_DeleteOnClose);
            }
            //catch (ThreadAbortException) //not available in .NET Core 1.1
            //{
            //    if (fileLock != null)
            //        fileLock.Dispose(); // Make sure it's cleaned up if the requesting thread is aborting!

            //    throw; 
            //}
            catch
            {
                //don't care why we failed, we just did - so no lock for you!
                fileLock = null;
            }

            return fileLock;
        }

        /// <summary>
        /// Attempts to request a turn at an exclusive lock on a specified file.
        /// </summary>
        /// <param name="lockFullFileNamePath">The full-path file name for the lock file.</param>
        /// <returns>A LockFile holding a lock request if available, null otherwise</returns>
        /// <remarks>Callers should check the provided handle for null to ensure they got a valid lock request on the file.
        /// If it is not null, it must be disposed to release the request when expired or full lock is acquired.</remarks>
        private FileLock GetLockRequest(string lockFullFileNamePath)
        {
            FileLock fileLock;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lockFullFileNamePath));
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                m_Logger.LogWarning("Unable to create directory to index path, locking will not be feasible.  Exception: {0}", ex);
#endif
            }

            // We share ReadWrite so that we overlap with an open lock (unshared write) and other requests (open reads).
            FileShare fileShare = FileShare.ReadWrite;

            try
            {
                // This is meant to overlap with other requestors, so it should never delete on close; others may still have it open.
                fileLock = OpenFileAccess(lockFullFileNamePath, FileAccess.Read, fileShare, false);
            }
            catch
            {
                // We don't care why we failed, we just did - so no lock for you!
                fileLock = null;
            }

            return fileLock;
        }

        /// <summary>
        /// Check if a lock request is pending (without blocking).
        /// </summary>
        /// <param name="lockFullFileNamePath">The full-path file name to request a lock on.</param>
        /// <returns>True if a lock request is pending (an open read), false if no reads are open on the file.</returns>
        private bool CheckLockRequest(string lockFullFileNamePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lockFullFileNamePath));
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                m_Logger.LogWarning("Unable to create directory to index path, locking will not be feasible.  Exception: {0}", ex);
#endif
            }

            // We share Write because we'll check this while we already have an unshared write open!
            FileAccess fileAccess = FileAccess.Read;
            FileShare fileShare = FileShare.Write;
            bool deleteOnClose = false; // This overlaps with holding a write lock, so don't delete the file when successful.

            try
            {
                using (FileLock fileLockRequest = OpenFileAccess(lockFullFileNamePath, fileAccess, fileShare, deleteOnClose))
                {
                    return (fileLockRequest == null); // There's an open read on it if we could NOT open an unshared read.
                }
            }
            catch
            {
                // We don't care why we failed, we just did - so assume there IS a request pending.
                return true;
            }
        }

        /// <summary>
        /// Open a file for the specified fileAccess and fileShare, or return null if open fails (avoids exceptions).
        /// </summary>
        /// <param name="fullFileNamePath">The full-path file name to open for the specified access.</param>
        /// <param name="fileAccess">The FileAccess with which to open the file.</param>
        /// <param name="fileShare">The FileShare to allow to overlap with this open.</param>
        /// <param name="manualDeleteOnClose">Whether the (successfully-opened) FileLock returned should delete the file
        /// upon dispose.</param>
        /// <returns>A disposable FileLock opened with the specified access and sharing), or null if the attempt failed.</returns>
        private FileLock OpenFileAccess(string fullFileNamePath, FileAccess fileAccess, FileShare fileShare, bool manualDeleteOnClose)
        {
            uint flags = 0;

            FileLock fileOpen = null;

            FileStream fileStream = null;
            try
            {
                // Make sure we don't thread-abort in the FileStream() ctor.
                try
                {
                }
                finally
                {
                    fileStream = new FileStream(fullFileNamePath, FileMode.OpenOrCreate, fileAccess, fileShare,
                                                8192, (FileOptions)flags);
                }
            }
            //catch (ThreadAbortException) //not available in .NET Core 1.1
            //{
            //    if (fileStream != null)
            //        fileStream.Dispose(); // Make sure this gets cleaned up if the requesting thread is aborting!

            //    throw; 
            //}
            catch
            {
                fileStream = null;
            }

            if (fileStream != null)
                fileOpen = new FileLock(fileStream, fullFileNamePath, FileMode.OpenOrCreate, fileAccess, fileShare, manualDeleteOnClose);


            return fileOpen;
        }

        #endregion

    }
}
