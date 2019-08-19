using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Gibraltar.Data;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;

namespace Gibraltar.Messaging
{
    /// <summary>
    /// Monitors the discovery directory of the local file system for discovery file changes.
    /// </summary>
    public class LocalServerDiscoveryFileMonitor
    {
        private readonly object m_Lock = new object();
        private readonly object m_QueueLock = new object();
        private readonly Dictionary<string, LocalServerDiscoveryFile> m_DiscoveryFiles = new Dictionary<string, LocalServerDiscoveryFile>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<FileSystemEventArgs> m_FileEventQueue = new Queue<FileSystemEventArgs>();

        private volatile bool m_ActiveThread; //indicates if we have an active thread pool request
        private FileSystemWatcher m_FileSystemWatcher;

        /// <summary>
        /// Event raised when a file change is detected.
        /// </summary>
        public event LocalServerDiscoveryFileEventHandler FileChanged;

        /// <summary>
        /// Event raised when a file change is detected.
        /// </summary>
        public event LocalServerDiscoveryFileEventHandler FileDeleted;
        
        #region Public Properties and Methods

        /// <summary>
        /// Begin monitoring for file changes
        /// </summary>
        public void Start()
        {
            lock (m_Lock)
            {
                if (m_FileSystemWatcher == null)
                {
                    string discoveryPath = PathManager.FindBestPath(PathType.Discovery);
                    m_FileSystemWatcher = new FileSystemWatcher(discoveryPath, LocalServerDiscoveryFile.FileFilter);
                    m_FileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    m_FileSystemWatcher.Changed += FileSystemWatcherOnChanged;
                    m_FileSystemWatcher.Deleted += FileSystemWatcherOnDeleted;
                }

                m_FileSystemWatcher.EnableRaisingEvents = true;

                //we need to *force* an event for every existing file.
                var fileSystemEntries = Directory.GetFileSystemEntries(m_FileSystemWatcher.Path, LocalServerDiscoveryFile.FileFilter);
                foreach (var fileSystemEntry in fileSystemEntries)
                {
                    CheckRaiseChangedEvent(fileSystemEntry);
                }
            }
        }

        /// <summary>
        /// Stop monitoring for file changes
        /// </summary>
        public void Stop()
        {
            lock (m_Lock)
            {
                if (m_FileSystemWatcher != null)
                {
                    m_FileSystemWatcher.EnableRaisingEvents = false;
                    m_FileSystemWatcher.Dispose();
                    m_FileSystemWatcher = null;
                }
            }            
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Raises the FileChanged event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnFileChanged(LocalServerDiscoveryFileEventArgs e)
        {
            LocalServerDiscoveryFileEventHandler handler = FileChanged;
            if (handler != null) handler(this, e);
        }

        /// <summary>
        /// Raises the FileDeleted event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnFileDeleted(LocalServerDiscoveryFileEventArgs e)
        {
            LocalServerDiscoveryFileEventHandler handler = FileDeleted;
            if (handler != null) handler(this, e);
        }

        #endregion

        #region Private Properties and Methods

        private void EnsureQueueProcessorRunning()
        {
            if (!m_ActiveThread)
            {
                m_ActiveThread = true;
                ThreadPool.QueueUserWorkItem(AsyncProcessQueue);
            }
        }

        /// <summary>
        /// Called from the thread pool to process all of the items in the queue
        /// </summary>
        /// <param name="state"></param>
        private void AsyncProcessQueue(object state)
        {
            try //since we're directly on the thread pool we must have a top-level exception handler.
            {
                FileSystemEventArgs pendingEvent = null;
                do
                {
                    pendingEvent = null;
                    lock (m_QueueLock)
                    {
                        if (m_FileEventQueue.Count > 0)
                        {
                            pendingEvent = m_FileEventQueue.Dequeue();
                        }
                        else
                        {
                            m_ActiveThread = false; //we're done and we're going to exit.
                        }
                    }

                    if (pendingEvent != null)
                    {
                        //raise the event!
                        if (pendingEvent.ChangeType == WatcherChangeTypes.Deleted)
                        {
                            CheckRaiseDeletedEvent(pendingEvent.FullPath);
                        }
                        else if ((pendingEvent.ChangeType == WatcherChangeTypes.Created) ||
                                 (pendingEvent.ChangeType == WatcherChangeTypes.Changed))
                        {
                            CheckRaiseChangedEvent(pendingEvent.FullPath);
                        }
                    }
                } while (pendingEvent != null);
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                {
                    Log.Write(LogMessageSeverity.Warning, NetworkMessenger.LogCategory, "local server discovery file monitor queue event threw an exception, queue processing will pause",
                        "While we were dequeueing items or raising events an exception was thrown.  Queue processing will be interrupted until the next request comes in " +
                        "and the request that caused the exception will be dropped.\r\n{0} exception thrown:\r\n{1}", ex.GetType(), ex.Message);
                }
                m_ActiveThread = false; //if we had an exception we need to be sure we can fire up again
            }
        }

        private void CheckRaiseChangedEvent(string fullPath)
        {
            LocalServerDiscoveryFileEventArgs eventArgs = null;
            lock (m_Lock)
            {
                LocalServerDiscoveryFile newItem = null;
                if (!m_DiscoveryFiles.ContainsKey(fullPath))
                {
                    //we don't actually process change events, only adds.
                    try
                    {
                        newItem = new LocalServerDiscoveryFile(fullPath);
                        if (newItem.IsAlive)
                        {
                            m_DiscoveryFiles.Add(fullPath, newItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode)
                        {
                            Log.Write(LogMessageSeverity.Information, NetworkMessenger.LogCategory, "Unable to load local server discovery file due to " + ex.GetType() + " exception", 
                                "While attempting to load a local server discovery file an exception was thrown.  If this is because the file wasn't found " +
                                "or was incomplete it can be ignored.  An incomplete file will raise another event when complete that will cause it to be re-processed.\r\n" +
                                "File: {0}\r\nException: {1}", fullPath, ex.Message);
                        }
                    }

                    if (newItem != null)
                    {
                        eventArgs = new LocalServerDiscoveryFileEventArgs(newItem);
                    }
                }
            }

            //raise the event outside of our lock
            if (eventArgs != null)
            {
                OnFileChanged(eventArgs);
            }
        }

        private void CheckRaiseDeletedEvent(string fullPath)
        {
            LocalServerDiscoveryFileEventArgs eventArgs = null;
            lock (m_Lock)
            {
                if (m_DiscoveryFiles.TryGetValue(fullPath, out var victim))
                {
                    //indeed it existed so we want to raise the event.
                    m_DiscoveryFiles.Remove(fullPath);
                    eventArgs = new LocalServerDiscoveryFileEventArgs(victim);
                }
            }

            //raise the event outside of the lock.
            if (eventArgs != null)
            {
                OnFileDeleted(eventArgs);
            }
        }

        private void FileSystemWatcherOnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            //since this is being raised by the OS time is tight - we dispatch it to a queue and return as fast as possible.
            lock (m_FileEventQueue)
            {
                m_FileEventQueue.Enqueue(fileSystemEventArgs);
            }

            EnsureQueueProcessorRunning();
        }

        private void FileSystemWatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            lock (m_FileEventQueue)
            {
                m_FileEventQueue.Enqueue(fileSystemEventArgs);
            }

            EnsureQueueProcessorRunning();
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for LocalServerDiscoveryFile events.
    /// </summary>
    public class LocalServerDiscoveryFileEventArgs: EventArgs
    {
        /// <summary>
        /// The file that was affected
        /// </summary>
        public LocalServerDiscoveryFile File { get; set; }

        /// <summary>
        /// Create a new event argument
        /// </summary>
        /// <param name="file"></param>
        public LocalServerDiscoveryFileEventArgs(LocalServerDiscoveryFile file)
        {
            File = file;
        }
    }

    /// <summary>
    /// Standard event handler delegate for the LocalServerDiscoveryFile Event arguments
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void LocalServerDiscoveryFileEventHandler(object sender, LocalServerDiscoveryFileEventArgs e);
}
