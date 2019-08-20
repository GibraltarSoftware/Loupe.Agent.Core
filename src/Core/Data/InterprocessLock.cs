using System;
using System.IO;
using System.Threading;

namespace Loupe.Core.Data
{
    /// <summary>
    /// Represents an exclusive lock on a repository within a process and between processes.
    /// </summary>
    /// <remarks>To be valid, the repository lock object must be obtained from the RepositoryLockManager class.
    /// When you're done with a lock, dispose the repository lock to release it.</remarks>
    public sealed class InterprocessLock : IDisposable
    {
        /// <summary>
        /// The file extension of lock files used to lock repositories..
        /// </summary>
        public const string LockFileExtension = "lock";

        private readonly string m_IndexPath;
        private readonly string m_LockName;
        private readonly string m_LockFullFileNamePath;
        private readonly bool m_WaitForLock;
        private readonly object m_MyLock = new object(); // For locking inter-thread signals to this instance.

        private Thread m_OwningThread;
        private object m_OwningObject;
        private InterprocessLockProxy m_OurLockProxy;
        private InterprocessLock m_ActualLock;
        private DateTimeOffset m_WaitTimeout; // Might be locked by MyLock?
        private bool m_MyTurn; // LOCKED by MyLock
        private bool m_Disposed; // LOCKED by MyLock

        /// <summary>
        /// Raised when the lock is disposed.
        /// </summary>
        internal event EventHandler Disposed;

        internal InterprocessLock(object requester, string indexPath, string lockName, int timeoutSeconds)
        {
            m_OwningObject = requester;
            m_OwningThread = Thread.CurrentThread;
            m_IndexPath = indexPath;
            m_LockName = lockName;
            m_ActualLock = null;
            m_MyTurn = false;
            m_WaitForLock = (timeoutSeconds > 0);
            m_WaitTimeout = m_WaitForLock ? DateTimeOffset.Now.AddSeconds(timeoutSeconds) : DateTimeOffset.Now;
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
        /// The object that is currently holding the lock.
        /// </summary>
        public object Owner { get { return m_OwningObject; } }

        /// <summary>
        /// The thread that created and waits on this request and owns the lock when this request is granted.
        /// </summary>
        public Thread OwningThread { get { return m_OwningThread; } }

        /// <summary>
        /// The ManagedThreadId of the thread that owns this lock instance.
        /// </summary>
        public int OwningThreadId { get { return m_OwningThread.ManagedThreadId; } }

        /// <summary>
        /// Whether this lock request is willing to wait (finite) for the lock or return immediately if not available.
        /// </summary>
        public bool WaitForLock { get { return m_WaitForLock; } }

        /// <summary>
        /// The clock time at which this lock request wants to stop waiting for the lock and give up.
        /// (MaxValue once the lock is granted, MinValue if the lock was denied.)
        /// </summary>
        public DateTimeOffset WaitTimeout { get { return m_WaitTimeout; } }
        // TODO: Above needs lock wrapper?

        /// <summary>
        /// The actual holder of the lock if we are a secondary lock on the same thread, or ourselves if we hold the file lock.
        /// </summary>
        public InterprocessLock ActualLock { get { return m_ActualLock; } }

        /// <summary>
        /// Reports if this lock object holds a secondary lock rather than the actual lock (or no lock).
        /// </summary>
        public bool IsSecondaryLock { get { return m_ActualLock != null && (ReferenceEquals(m_ActualLock, this) == false); } }

        /// <summary>
        /// Reports if this request instance has expired and should be skipped over because no thread is still waiting on it.
        /// </summary>
        public bool IsExpired
        {
            get
            {
                lock (m_MyLock)
                {
                    return m_Disposed || m_WaitTimeout == DateTimeOffset.MinValue;
                }
            }
        }

        /// <summary>
        /// Whether this lock instance has been disposed (and thus does not hold any locks).
        /// </summary>
        public bool IsDisposed { get { return m_Disposed; } }

        /// <summary>
        /// Gets or sets the dispose-on-close policy for the lock proxy associated with this lock instance.
        /// </summary>
        public bool DisposeProxyOnClose
        {
            get { return (m_OurLockProxy == null) ? false : m_OurLockProxy.DisposeOnClose; }
            set
            {
                if (m_OurLockProxy != null)
                    m_OurLockProxy.DisposeOnClose = value;
            }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The proxy who will actually hold the file lock on our behalf.
        /// </summary>
        internal InterprocessLockProxy OurLockProxy
        {
            get { return m_OurLockProxy; }
            set { m_OurLockProxy = value; }
        }

        internal void GrantTheLock(InterprocessLock actualLock)
        {
            if (actualLock != null && actualLock.IsDisposed == false && actualLock.OwningThread == m_OwningThread &&
                string.Equals(actualLock.FullName, m_LockFullFileNamePath, StringComparison.OrdinalIgnoreCase))
            {
                // We don't need to lock around this because we're bypassing the proxy's queue and staying only on our own thread.
                m_ActualLock = actualLock;
                m_WaitTimeout = DateTimeOffset.MaxValue; // We have a lock (sort of), so reset our timeout to forever.
            }
            else
            {
                // It's an invalid call, so make sure our setting is cleared out.
                m_ActualLock = null;
                // Note: Should this case always throw an exception?
#if DEBUG
                throw new InvalidOperationException("Can't set a secondary lock from an invalid actual lock.");
#endif
            }
        }

        internal void SignalMyTurn()
        {
            lock (m_MyLock)
            {
                m_MyTurn = true; // Flag it as being our turn.

                System.Threading.Monitor.PulseAll(m_MyLock); // And signal Monitor.Wait that we changed the state.
            }
        }

        internal bool AwaitTurnOrTimeout()
        {
            TimeSpan howLong;
            lock (m_MyLock)
            {
                if (m_WaitForLock) // Never changes, so check it first.
                {
                    while (m_MyTurn == false && m_Disposed == false) // Either flag and we're done waiting.
                    {
                        howLong = m_WaitTimeout - DateTimeOffset.Now;
                        if (howLong.TotalMilliseconds <= 0)
                        {
                            m_WaitTimeout = DateTimeOffset.MinValue; // Mark timeout as expired.
                            return false; // Our time is up!
                        }

                        // We don't need to do a pulse here, we're the only ones waiting, and we didn't change any state.
                        System.Threading.Monitor.Wait(m_MyLock, howLong);
                    }
                }

                // Now we've done any allowed waiting as needed, check what our status is.

                if (m_Disposed || m_MyTurn == false)
                    return false; // We're expired!
                else
                    return true; // Otherwise, we're not disposed and it's our turn!

                // We don't need to do a pulse here, we're the only ones waiting, and we didn't change any state.
            }
        }

        internal static string GetLockFileName(string indexPath, string lockName)
        {
            return Path.Combine(indexPath, lockName + "." + LockFileExtension);
        }

        #endregion

        #region Private Properties and Methods

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

                lock (m_MyLock)
                {
                    if (!m_Disposed)
                    {
                        m_Disposed = true; // Make sure we don't do it more than once.
                        m_WaitTimeout = DateTimeOffset.MinValue;
                        m_OwningThread = null;
                        m_OwningObject = null;
                    }

                    System.Threading.Monitor.PulseAll(m_MyLock); // No one should be waiting, but we did change state, so...
                }

                OnDispose(); // Fire whether it's first time or redundant.  Subscribers must sanity-check and can unsubscribe.

                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here
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

        #endregion
    }
}
