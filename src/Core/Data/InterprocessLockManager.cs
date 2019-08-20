using System;
using System.Collections.Generic;
using System.Threading;

namespace Loupe.Core.Data
{
    /// <summary>
    /// A multiprocess lock manager for repositories
    /// </summary>
    /// <remarks>Manages locking first within the process and then extends the process lock to multiple processes
    /// by locking a file on disk.  Designed for use with the Using statement as opposed to the Lock statement.</remarks>
    public static class InterprocessLockManager
    {
        private static readonly object g_Lock = new object();
        private static readonly Dictionary<string, InterprocessLockProxy> g_Proxies = new Dictionary<string, InterprocessLockProxy>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Attempt to lock the repository with the provided index path.
        /// </summary>
        /// <param name="requester">The object that is requesting the lock (useful for debugging purposes)</param>
        /// <param name="indexPath">The fully qualified path to the directory containing the index file of the repository</param>
        /// <param name="lockName">The name of the lock to get (locks are a combination of index and this name)</param>
        /// <param name="timeoutSeconds">The maximum number of seconds to wait on the lock before giving up.</param>
        /// <returns>A Repository Lock object if the lock could be obtained or Null if the lock timed out.</returns>
        public static InterprocessLock Lock(object requester, string indexPath, string lockName, int timeoutSeconds)
        {
            return Lock(requester, indexPath, lockName, timeoutSeconds, false);
        }

        /// <summary>
        /// Attempt to lock the repository with the provided index path.
        /// </summary>
        /// <param name="requester">The object that is requesting the lock (useful for debugging purposes)</param>
        /// <param name="indexPath">The fully qualified path to the directory containing the index file of the repository</param>
        /// <param name="lockName">The name of the lock to get (locks are a combination of index and this name)</param>
        /// <param name="timeoutSeconds">The maximum number of seconds to wait on the lock before giving up.</param>
        /// <param name="deleteOnClose">Whether the lock file should be deleted on close or left around for reuse.</param>
        /// <returns>A Repository Lock object if the lock could be obtained or Null if the lock timed out.</returns>
        public static InterprocessLock Lock(object requester, string indexPath, string lockName, int timeoutSeconds, bool deleteOnClose)
        {
            if (requester == null)
                throw new ArgumentNullException(nameof(requester));

            if (indexPath == null)
                throw new ArgumentNullException(nameof(indexPath));

            if (lockName == null)
                throw new ArgumentNullException(nameof(lockName));

            InterprocessLock candidateLock = new InterprocessLock(requester, indexPath, lockName, timeoutSeconds);

            // Lookup or create the proxy for the requested lock.
            InterprocessLockProxy lockProxy;
            lock (g_Lock)
            {
                if (g_Proxies.TryGetValue(candidateLock.FullName, out lockProxy) == false)
                {
                    // Didn't exist, need to make one.
                    lockProxy = new InterprocessLockProxy(indexPath, lockName, deleteOnClose);

#if DEBUG
                    if (string.Equals(lockProxy.FullName, candidateLock.FullName, StringComparison.OrdinalIgnoreCase) == false)
                        throw new InvalidOperationException("Proxy generated a different full name than the candidate lock.");
#endif

                    lockProxy.Disposed += LockProxy_Disposed;
                    g_Proxies.Add(lockProxy.FullName, lockProxy);
                }

                // Does the current thread already hold the lock?  (If it was still waiting on it, we couldn't get here.)
                Thread currentTurnThread = lockProxy.CheckCurrentTurnThread(candidateLock);
                if (Thread.CurrentThread == currentTurnThread && candidateLock.ActualLock != null)
                {
                    return candidateLock; // It's a secondary lock, so we don't need to queue it or wait.
                }
                // Or is the lock currently held by another thread that we don't want to wait for?
                if (currentTurnThread != null && candidateLock.WaitForLock == false)
                {
                    candidateLock.Dispose(); // We don't want to wait for it, so don't bother queuing an expired request.
                    return null; // Just fail out.
                }

                lockProxy.QueueRequest(candidateLock); // Otherwise, queue the request inside the lock to keep the proxy around.
            }

            // Now we have the proxy and our request is queued.  Make sure some thread is trying to get the file lock.
            bool ourTurn = false; // Assume false.
            try
            {
                ourTurn = lockProxy.AwaitOurTurnOrTimeout(candidateLock);
            }
            finally
            {
                if (ourTurn == false)
                {
                    // We have to make sure this gets disposed if we didn't get the lock, even if a ThreadAbortException occurs.
                    candidateLock.Dispose(); // Bummer, we didn't get it.  Probably already disposed, but safe to do again.
                    candidateLock = null; // Clear it out to report the failure.
                }
            }
            // Otherwise... yay, we got it!

            return candidateLock;
        }

        /// <summary>
        /// Query whether a particular lock is available without holding on to it.
        /// </summary>
        /// <param name="requester">The object that is querying the lock (useful for debugging purposes)</param>
        /// <param name="indexPath">The fully qualified path to the directory containing the index file of the repository</param>
        /// <param name="lockName">The name of the lock to query (locks are a combination of index and this name)</param>
        /// <returns>True if the lock could have been obtained.  False if the lock could not be obtained without waiting.</returns>
        public static bool QueryLockAvailable(object requester, string indexPath, string lockName)
        {
            string fileName = InterprocessLockProxy.GetLockFileName(indexPath, lockName);
            if (System.IO.File.Exists(fileName) == false)
                return true; // Lock file didn't exist, so we could have obtained it.

            bool lockAvailable;
            using (InterprocessLock attemptedLock = Lock(requester, indexPath, lockName, 0, true))
            {
                lockAvailable = (attemptedLock != null);
            }

            return lockAvailable;
        }

        #region Event Handlers

        static void LockProxy_Disposed(object sender, EventArgs e)
        {
            InterprocessLockProxy disposingProxy = (InterprocessLockProxy)sender;

            lock (g_Lock)
            {
                string lockKey = disposingProxy.FullName;
                // Only remove the proxy if the one we're disposing is the one in our collection for that key.
                if (g_Proxies.TryGetValue(lockKey, out var actualProxy) && ReferenceEquals(actualProxy, disposingProxy))
                {
                    g_Proxies.Remove(lockKey);
                }
                System.Threading.Monitor.PulseAll(g_Lock);
            }
        }

        #endregion
    }
}