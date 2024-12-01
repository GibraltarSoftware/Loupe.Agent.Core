using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gibraltar.Data.Internal;
using Gibraltar.Monitor;
using Gibraltar.Server.Client;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Gibraltar.Data
{
    /// <summary>
    /// Packages up sessions collected on the local computer and sends them via server or file transport.
    /// </summary>
    public class Packager : IDisposable
    {
        /// <summary>
        /// The log category for the packager
        /// </summary>
        public const string LogCategory = "Loupe.Packager";

        private readonly LocalRepository m_Repository;
        private readonly AgentConfiguration m_Configuration;
        private readonly object m_TransportThreadLock = new object();
        private readonly Queue<TransportPackageBase> m_TransportQueue = new Queue<TransportPackageBase>();
        private readonly Queue<TransportPackageBase> m_CleanupQueue = new Queue<TransportPackageBase>();
        private static readonly string[] UserListFormat;
        private static readonly int UserListMaxCount;

        private bool m_TransportThreadDispatched; //PROTECTED BY TRANSPORTTHREADLOCK
        private bool m_Disposed;

        /// <summary>
        /// Raised at the start of the packaging and sending process (after all input is collected)
        /// </summary>
        public event EventHandler BeginSend;

        /// <summary>
        /// Raised at the end of the packaging and sending process with completion status information.
        /// </summary>
        public event PackageSendEventHandler EndSend;

        static Packager()
        {
            UserListFormat = new[]
                                 {
                                     "Anonymous", // No names found!
                                     "{0}", // Just one distinct name found.
                                     "{0} and {1}", // Two distinct names found.
                                     "{0}, {1}, et. al.", // Three or more distinct names found.
                                 };

            UserListMaxCount = UserListFormat.Length - 1;
        }

        /// <summary>
        /// Create a new packager for the current process.
        /// </summary>
        public Packager()
            : this(Log.SessionSummary.Product, Log.SessionSummary.Application)
        {
        }

        /// <summary>
        /// Create a new packager for the current process.
        /// </summary>
        public Packager(string productName)
            : this(productName, null)
        {
        }

        /// <summary>
        /// Create a new packager for the current process.
        /// </summary>
        public Packager(string productName, string applicationName)
            : this(productName, applicationName, Log.Configuration.SessionFile.Folder)
        {
        }

        /// <summary>
        /// Create a new packager for the current process.
        /// </summary>
        public Packager(string productName, string applicationName, string repositoryFolder)
        {
            if (string.IsNullOrEmpty(productName))
            {
                throw new ArgumentNullException(nameof(productName));
            }

            ProductName = productName;
            ApplicationName = applicationName;
            Caption = string.Format(CultureInfo.InvariantCulture, "{0} Logs", ProductName);

            //and load up our configuration from App.Config to fill in the holes.
            m_Configuration = Log.Configuration;

            //now connect to the right local repository to package from.
            m_Repository = new LocalRepository(productName, repositoryFolder);
        }

        #region Public Properties and Methods

        /// <summary>
        /// The product name of the current running application this packager was initialized with.
        /// </summary>
        public string ProductName { get; private set; }

        /// <summary>
        /// The name of the current running application this packager was initialized with.
        /// </summary>
        public string ApplicationName { get; private set; }

        /// <summary>
        /// A caption for the resulting package
        /// </summary>
        public string Caption { get; set; }

        /// <summary>
        /// A description for the resulting package.
        /// </summary>
        public string Description { get; set; }

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
        /// Write the completed package to the provided full file name and path without extension.
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="fullFileNamePath">The file name and path to write the final package to</param>
        /// <param name="progressMonitors">The asynchronous progress monitoring stack.</param>
        /// <returns>The Package Send Event Arguments object that was also used for the EndSend event.</returns>
        /// <remarks>The EndSend event will be raised when the send operation completes.
        /// Any provided extension will be removed and replaced with the standard Gibraltar package extension.</remarks>
        /// <exception cref="ArgumentNullException">A required parameter was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The provided file information is not a fully qualified file name and path.</exception>
        public PackageSendEventArgs SendToFile(SessionCriteria sessions, bool markAsRead, string fullFileNamePath, ProgressMonitorStack progressMonitors)
        {
            if (sessions == SessionCriteria.None)
                return new PackageSendEventArgs(0, AsyncTaskResult.Success, "No sessions requested", null);

            //check for invalid arguments.
            if (string.IsNullOrEmpty(fullFileNamePath))
                throw new ArgumentNullException(nameof(fullFileNamePath));
            if (Path.IsPathRooted(fullFileNamePath) == false)
                throw new ArgumentOutOfRangeException(nameof(fullFileNamePath), "The provided fullFileNamePath is not fully qualified");
            if (progressMonitors == null)
                throw new ArgumentNullException(nameof(progressMonitors));

            return ActionSendToFile(new object[] { sessions, markAsRead, fullFileNamePath, progressMonitors });
        }

        /// <summary>
        /// Write the completed package to the provided full file name and path without extension.
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="fullFileNamePath">The file name and path to write the final package to</param>
        /// <param name="asyncSend">True to have the package and send process run asynchronously.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.
        /// Any provided extension will be removed and replaced with the standard Gibraltar package extension.</remarks>
        /// <exception cref="ArgumentNullException">A required parameter was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The provided file information is not a fully qualified file name and path.</exception>
        public void SendToFile(SessionCriteria sessions, bool markAsRead, string fullFileNamePath, bool asyncSend)
        {
            if (sessions == SessionCriteria.None)
                return;

            //check for invalid arguments.
            if (string.IsNullOrEmpty(fullFileNamePath))
                throw new ArgumentNullException(nameof(fullFileNamePath));

            if (Path.IsPathRooted(fullFileNamePath) == false)
                throw new ArgumentOutOfRangeException(nameof(fullFileNamePath), "The provided fullFileNamePath is not fully qualified");

            AsyncTaskExecute(AsyncSendToFile, "Packaging Sessions", new object[] { sessions, markAsRead, fullFileNamePath }, asyncSend);
        }

        /// <summary>
        /// Write the completed package to the provided full file name and path without extension.
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="fullFileNamePath">The file name and path to write the final package to</param>
        /// <param name="asyncSend">True to have the package and send process run asynchronously.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.
        /// Any provided extension will be removed and replaced with the standard Gibraltar package extension.</remarks>
        /// <exception cref="ArgumentNullException">A required parameter was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The provided file information is not a fully qualified file name and path.</exception>
        public void SendToFile(Predicate<ISessionSummary> sessionMatchPredicate, bool markAsRead, string fullFileNamePath, bool asyncSend)
        {
            if (sessionMatchPredicate == null)
                throw new ArgumentNullException(nameof(sessionMatchPredicate));

            //check for invalid arguments.
            if (string.IsNullOrEmpty(fullFileNamePath))
                throw new ArgumentNullException(nameof(fullFileNamePath));

            if (Path.IsPathRooted(fullFileNamePath) == false)
                throw new ArgumentOutOfRangeException(nameof(fullFileNamePath), "The provided fullFileNamePath is not fully qualified");

            AsyncTaskExecute(AsyncSendToFile, "Packaging Sessions", new object[] { sessionMatchPredicate, markAsRead, fullFileNamePath }, asyncSend);
        }

        /// <summary>
        /// Send the specified packages to our session data server as configured
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="serverConfiguration">Optional.  The connection options to use instead of the current configured server connection</param>
        /// <param name="asyncSend">True to have the package and send process run asynchronously.</param>
        /// <returns>The Package Send Event Arguments object that was also used for the EndSend event.</returns>
        /// <remarks>The EndSend event will be raised when the send operation completes.</remarks>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions, bool asyncSend,
            ServerConfiguration serverConfiguration = null)
        {
            if (sessions == SessionCriteria.None)
                return;

            AsyncTaskExecute(AsyncSendToServer, "Sending Sessions",
                new object[] { sessions, markAsRead, purgeSentSessions, serverConfiguration, false }, 
                asyncSend);
        }

        /// <summary>
        /// Send the specified packages to our session data server as configured
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="suppressLogging">True to disable activity logging (typically because this is an automatic packager operation)</param>
        /// <param name="serverConfiguration">Optional.  The connection options to use instead of the current configured server connection</param>
        /// <param name="asyncSend">True to have the package and send process run asynchronously.</param>
        /// <returns>The Package Send Event Arguments object that was also used for the EndSend event.</returns>
        /// <remarks>The EndSend event will be raised when the send operation completes.</remarks>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        internal void SendToServer(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions, bool asyncSend, bool suppressLogging,
            ServerConfiguration serverConfiguration = null)
        {
            if (sessions == SessionCriteria.None)
                return;

            AsyncTaskExecute(AsyncSendToServer, "Sending Sessions",
                new object[] { sessions, markAsRead, purgeSentSessions, serverConfiguration, suppressLogging },
                asyncSend);
        }

        /// <summary>
        /// Send the specified packages to our session data server as configured
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="serverConfiguration">Optional.  The connection options to use instead of the current configured server connection</param>
        /// <param name="asyncSend">True to have the package and send process run asynchronously.</param>
        /// <returns>The Package Send Event Arguments object that was also used for the EndSend event.</returns>
        /// <remarks>The EndSend event will be raised when the send operation completes.</remarks>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(Predicate<ISessionSummary> sessionMatchPredicate, bool markAsRead, bool purgeSentSessions, bool asyncSend,
            ServerConfiguration serverConfiguration = null)
        {
            if (sessionMatchPredicate == null)
                throw new ArgumentNullException(nameof(sessionMatchPredicate));

            AsyncTaskExecute(AsyncSendToServer, "Sending Sessions",
                new object[] { sessionMatchPredicate, markAsRead, purgeSentSessions, serverConfiguration, false },
                asyncSend);
        }


        /// <summary>
        /// Send the specified packages to our session data server as configured
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="serverConfiguration">Optional.  The connection options to use instead of the current configured server connection</param>
        /// <param name="asyncSend">True to have the package and send process run asynchronously.</param>
        /// <param name="suppressLogging">True to disable activity logging (typically because this is an automatic packager operation)</param>
        /// <returns>The Package Send Event Arguments object that was also used for the EndSend event.</returns>
        /// <remarks>The EndSend event will be raised when the send operation completes.</remarks>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        internal void SendToServer(Predicate<ISessionSummary> sessionMatchPredicate, bool markAsRead, bool purgeSentSessions, bool asyncSend, bool suppressLogging,
            ServerConfiguration serverConfiguration = null)
        {
            if (sessionMatchPredicate == null)
                throw new ArgumentNullException(nameof(sessionMatchPredicate));

            AsyncTaskExecute(AsyncSendToServer, "Sending Sessions",
                new object[] { sessionMatchPredicate, markAsRead, purgeSentSessions, serverConfiguration, suppressLogging },
                asyncSend);
        }

        /// <summary>
        /// Send the specified packages to our session data server as configured
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="serverConfiguration">Optional.  The connection options to use instead of the current configured server connection</param>
        /// <param name="progressMonitors">Optional. The asynchronous progress monitoring stack.</param>
        /// <returns>The Package Send Event Arguments object that was also used for the EndSend event.</returns>
        /// <remarks>The EndSend event will be raised when the send operation completes.</remarks>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid</exception>
        public async Task<PackageSendEventArgs> SendToServer(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions,
            ServerConfiguration serverConfiguration = null, ProgressMonitorStack progressMonitors = null)
        {
            if (sessions == SessionCriteria.None)
                return new PackageSendEventArgs(0, AsyncTaskResult.Success, "No sessions requested", null);

            return await ActionSendToServer(new object[] { sessions, markAsRead, purgeSentSessions, serverConfiguration, progressMonitors }).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines if it can correctly connect to the server and send data
        /// </summary>
        /// <param name="overrideConfiguration">Indicates if any of the configuration information provided on this call should be used</param>
        /// <param name="serverConfiguration">The connection configuration to use if overriding the server configuration</param>
        /// <returns>The hub status information for the specified configuration</returns>
        public static async Task<HubConnectionStatus> CanSendToServer(bool overrideConfiguration, ServerConfiguration serverConfiguration)
        {
            HubConnectionStatus status;
            if (overrideConfiguration)
            {
                status = await HubConnection.CanConnect(serverConfiguration).ConfigureAwait(false);                
            }
            else
            {
                status = await HubConnection.CanConnect(Log.Configuration.Server).ConfigureAwait(false);
            }

            return status;
        }

        /// <summary>
        /// Determines if it can correctly connect to the server and send data
        /// </summary>
        /// <returns>The hub status information for the specified configuration</returns>
        public static async Task<HubConnectionStatus> CanSendToServer()
        {
            return await CanSendToServer(false, null).ConfigureAwait(false);
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// </summary>
        /// <remarks>
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.</remarks>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            //because we're multithreaded and asynchronous we need to be sure we're not in use.
            if (!m_Disposed)
            {
                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case
                    if (m_TransportQueue.Count > 0)
                    {
                        TransportPackageBase[] packages = m_TransportQueue.ToArray();
                        foreach (TransportPackageBase package in packages)
                        {
                            package.Dispose();
                        }
                        m_TransportQueue.Clear();
                    }

                    if (m_CleanupQueue.Count > 0)
                    {
                        TransportPackageBase[] packages = m_CleanupQueue.ToArray();
                        foreach (TransportPackageBase package in packages)
                        {
                            package.Dispose();
                        }
                        m_CleanupQueue.Clear();
                    }
                }

                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here

                m_Disposed = true; // Make sure we don't do it more than once
            }
        }


        /// <summary>
        /// Get a dataset of all of the sessions that should be included in our package
        /// </summary>
        /// <returns></returns>
        protected ISessionSummaryCollection FindPackageSessions(SessionCriteria sessionCriteria, ProgressMonitorStack progressMonitors, 
            bool suppressLogging, out bool hasProblemSessions)
        {
            if (sessionCriteria == SessionCriteria.None)
            {
                //special case:  All they asked for is none, which means, well... none.
                hasProblemSessions = false;
                return null;
            }

            hasProblemSessions = false;
            ISessionSummaryCollection packageSessions;
            int completedSteps = 0;

            using (ProgressMonitor ourMonitor = progressMonitors.NewMonitor(this, "Finding Sessions to Report", 3))
            {
                //special case:  if session criteria includes active session then we have to split the file.
                //Go ahead and end the current file.  We need to be sure that there is an up to date file when the copy runs.
                if ((SessionCriteria.ActiveSession & sessionCriteria) == SessionCriteria.ActiveSession)
                    Log.EndFile( 0,"Creating Package including active session", suppressLogging);

                //run the maintenance merge to make sure we have the latest sessions.
                ourMonitor.Update("Updating Session List", completedSteps++);
                m_Repository.Refresh(false, true, sessionCriteria);

                //find all of the sessions so we can subset it downstream.
                ourMonitor.Update("Finding Sessions", completedSteps++);

                packageSessions = m_Repository.Find(new SessionCriteriaPredicate(ProductName, ApplicationName, sessionCriteria).Predicate);
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Sessions list loaded", "There are {0} matching sessions in the collection repository.", packageSessions.Count);
                ourMonitor.Update(string.Format("{0} matching sessions found", packageSessions.Count), completedSteps++);
            }

            return packageSessions;
        }


        /// <summary>
        /// Get a dataset of all of the sessions that should be included in our package
        /// </summary>
        /// <returns></returns>
        protected ISessionSummaryCollection FindPackageSessions(Predicate<ISessionSummary> sessionPredicate, ProgressMonitorStack progressMonitors, 
            bool suppressLogging, out bool hasProblemSessions)
        {
            if (sessionPredicate == null)
            {
                //special case:  All they asked for is none, which means, well... none.
                hasProblemSessions = false;
                return null;
            }

            hasProblemSessions = false;
            ISessionSummaryCollection packageSessions;
            int completedSteps = 0;

            using (ProgressMonitor ourMonitor = progressMonitors.NewMonitor(this, "Finding Sessions to Report", 3))
            {
                //Go ahead and end the current file - we will assume the caller may want it.  We need to be sure that there is an up to date file when the copy runs.
                Log.EndFile(0, "Creating Package including active session", suppressLogging);

                //run the maintenance merge to make sure we have the latest sessions.
                ourMonitor.Update("Updating Session List", completedSteps++);
                m_Repository.Refresh(false, true);

                //find all of the sessions so we can subset it downstream.
                ourMonitor.Update("Finding Sessions", completedSteps++);

                packageSessions = m_Repository.Find(sessionPredicate);
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Sessions list loaded", "There are {0} matching sessions in the collection repository.", packageSessions.Count);
                ourMonitor.Update(string.Format("{0} matching sessions found", packageSessions.Count), completedSteps++);
            }

            return packageSessions;
        }

        /// <summary>
        /// Called to raise the BeginSend event at the start of the packaging and sending process (after all input is collected)
        /// </summary>
        /// <remarks>If overriding this method, be sure to call Base.OnBeginSend to ensure that the event is still raised to its caller.</remarks>
        protected virtual void OnBeginSend()
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler tempEvent = BeginSend;

            if (tempEvent != null)
            {
                tempEvent(this, new EventArgs());
            }
        }

        /// <summary>
        /// Called to raise the EndSend event at the end of the packaging and sending process with completion status information.
        /// </summary>
        /// <remarks>If overriding this method, be sure to call Base.OnBeginSend to ensure that the event is still raised to its caller.</remarks>
        protected virtual void OnEndSend(PackageSendEventArgs e)
        {
            //save the delegate field in a temporary field for thread safety
            PackageSendEventHandler tempEvent = EndSend;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Provides a common wrapper to async task execution that can be synchronous with exception generation
        /// </summary>
        /// <param name="asyncTask">The task method to call</param>
        /// <param name="title">The title for the operation being performed</param>
        /// <param name="state">All of the state to pass to the task</param>
        /// <param name="processAsynchronously">True to return immediately after dispatch, false to wait until completion and then throw an exception if there is an error.</param>
        private void AsyncTaskExecute(WaitCallback asyncTask, string title, object state, bool processAsynchronously)
        {
            var execTask = new AsynchronousTask();
            execTask.Execute(asyncTask, title, state);

            if (processAsynchronously == false)
            {
                //stall out until the task is complete
                while (execTask.Completed == false)
                {
                    Thread.Sleep(16);
                }

                //and now we need to find out the final status.
                if ((execTask.TaskResults != null) && (execTask.TaskResults.Result == AsyncTaskResult.Error))
                {
                    //rethrow the internal exception if it exists, otherwise we'll have to make one.
                    if (execTask.TaskResults.Exception != null)
                    {
                        throw execTask.TaskResults.Exception;
                    }

                    //not our favorite thing to do because this isn't a public exception type.
                    throw new GibraltarException(execTask.TaskResults.Message, execTask.TaskResults.Exception);
                }
            }
        }

        /// <summary>
        /// Thread-safe get of the package queue length
        /// </summary>
        /// <returns></returns>
        private int CleanupQueueLength
        {
            get
            {
                lock (m_CleanupQueue)
                {
                    System.Threading.Monitor.PulseAll(m_CleanupQueue);
                    return m_CleanupQueue.Count;
                }
            }
        }

        /// <summary>
        /// Thread-safe cleanup dequeue
        /// </summary>
        /// <returns></returns>
        private TransportPackageBase CleanupDequeue()
        {
            lock (m_CleanupQueue)
            {
                System.Threading.Monitor.PulseAll(m_CleanupQueue);

                //queue throws an exception when empty, which is generally not what we want.
                if (m_CleanupQueue.Count == 0)
                    return null;

                return m_CleanupQueue.Dequeue();
            }
        }

        /// <summary>
        /// Thread-safe cleanup enqueue
        /// </summary>
        /// <param name="package"></param>
        private void CleanupEnqueue(TransportPackageBase package)
        {
            lock (m_CleanupQueue)
            {
                System.Threading.Monitor.PulseAll(m_CleanupQueue);
                m_CleanupQueue.Enqueue(package);
            }
        }

        /// <summary>
        /// Thread-safe get of the package queue length
        /// </summary>
        /// <returns></returns>
        private int TransportQueueLength
        {
            get
            {
                lock(m_TransportQueue)
                {
                    System.Threading.Monitor.PulseAll(m_TransportQueue);
                    return m_TransportQueue.Count;
                }
            }
        }

        /// <summary>
        /// Thread-safe transport dequeue
        /// </summary>
        /// <returns></returns>
        private TransportPackageBase TransportDequeue()
        {
            lock (m_TransportQueue)
            {                
                System.Threading.Monitor.PulseAll(m_TransportQueue);

                //queue throws an exception when empty, which is generally not what we want.
                if (m_TransportQueue.Count == 0)
                    return null;

                return m_TransportQueue.Dequeue();
            }
        }

        /// <summary>
        /// Thread-safe transport enqueue
        /// </summary>
        /// <param name="package"></param>
        private void TransportEnqueue(TransportPackageBase package)
        {
            lock (m_TransportQueue)
            {
                System.Threading.Monitor.PulseAll(m_TransportQueue);
                m_TransportQueue.Enqueue(package);
            }

            //make sure the queue processor is running.
            EnsureTransportThreadRunning();
        }

        /// <summary>
        /// Makes sure the background transport thread is running.
        /// </summary>
        private void EnsureTransportThreadRunning()
        {
            lock(m_TransportThreadLock)
            {
                if (m_TransportThreadDispatched == false)
                {
                    //we need to queue to the thread pool
                    ThreadPool.QueueUserWorkItem(AsyncTransportPackage);
                    m_TransportThreadDispatched = true;
                }

                System.Threading.Monitor.PulseAll(m_TransportThreadLock);
            }
        }

        /// <summary>
        /// Creates a transportable package of the selected sessions in the local collection repository.
        /// </summary>
        /// <remarks>Multi-thread safe.</remarks>
        private SimplePackage CreateTransportablePackage(ProgressMonitorStack progressMonitors, ProgressMonitor ourMonitor, ISessionSummaryCollection selectedSessions, int maxPackageSizeBytes, out bool hasProblemSessions, PackagingState packagingState)
        {
            packagingState.IsComplete = true; //when we detect that we must break into two packages we set this false.
            hasProblemSessions = false;

            //we have to have a working package
            ourMonitor.Update("Opening New Package");
            SimplePackage newPackage = new SimplePackage();

            //calculate a temporary file to put the package in.
            string temporaryFileNamePath = FileSystemTools.GetTempFileName();
            //it just made a file in that location, best to blow it away before we go further (get temp file name creates a 0 byte file)
            File.Delete(temporaryFileNamePath);

            //we merge sessions one by one because we have to check the size of the package after each.
            int sessionsMergedInPackage = 0;
            int sessionsSizeBytes = 0;
            bool foundStartSession = (packagingState.LastSessionId == null) ? true : false;
            List<string> userNameList = new List<string>(UserListMaxCount); // List of the first 0-3 user names found.

            foreach (var session in selectedSessions)
            {
                //wait, have we hit our start session yet?
                if (foundStartSession == false)
                {
                    //if this is our matching start session id then this is the last one we sent, so we'll start on the next one.
                    if (session.Id == packagingState.LastSessionId.Value)
                    {
                        foundStartSession = true;
                    }
                }
                else
                {
                    if (sessionsSizeBytes == 0)
                    {
                        //this is the first session we're adding to this package - we already advanced our pointer last time around.
                        ourMonitor.Update("Adding session to package...");
                    }
                    else
                    {
                        ourMonitor.Update("Adding session to package...", ourMonitor.CompletedSteps + 1);
                    }

                    //we should include this session, and then see how large we are.
                    try
                    {
                        //get the session as a stream so we can check its size.  We may have a cached stream from the last iteration.
                        Stream sessionStream = null;
                        if ((packagingState.NextSessionId.HasValue) && (packagingState.NextSessionId == session.Id) && (packagingState.NextSessionStream != null))
                        {
                            sessionStream = packagingState.NextSessionStream;
                            packagingState.NextSessionId = null;
                            packagingState.NextSessionStream = null;
                        }
                        else
                        {
                            if (packagingState.NextSessionStream != null)
                            {
                                //huh.  a mismatched stream - that means we have an algorithm error.
                                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Warning, LogCategory, "Packaging Session State is out of Sync", "We carried forward a package session state but the NextSessionId didn't match the next session, so we'll clean up the unused stream");
                                packagingState.NextSessionStream.Dispose();
                                packagingState.NextSessionStream = null;
                            }

                            try
                            {
                                sessionStream = m_Repository.LoadSessionStream(session.Id); //deliberately not doing "using" - we don't generally dispose the stream in here.
                            }
                            catch (Exception ex)
                            {
                                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogWriteMode.Queued, ex, LogCategory, "Session to be packaged is not in the repository.", ex.Message);
                            }
                        }

                        //there are cases where we won't get a stream back - typically because there's a problem with it or it's still in use
                        if (sessionStream == null)
                        {
                            if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Session was unexpectedly not available to be included in the package", "The repository wasn't able to load the session for inclusion into the package even though it should be available.  Session Id: {0}", session.Id);
                        }
                        else
                        {
                            string sessionUserName = null;
                            long sessionLength = sessionStream.Length;

                            //the question is:  how large is this session?
                            if ((sessionLength + sessionsSizeBytes) > maxPackageSizeBytes)
                            {
                                //we're going to exceed our length.. or ARE we?  lets save the package (so we get the benefit of compression) and check again.
                                newPackage.Save(progressMonitors, temporaryFileNamePath); //because we need to find out the file size to know where we are.
                                sessionsSizeBytes = (int)FileSystemTools.GetFileSize(temporaryFileNamePath);
                            }

                            //OK, we've done the best we can - we now know the true size of the package.
                            if ((sessionLength + sessionsSizeBytes) > maxPackageSizeBytes)
                            {
                                //two ways we can handle this:  If this is the FIRST session in the package then we have a special case:  The session can not fit in a package.
                                //Otherwise, we'll end this package and start another.

                                if (sessionsMergedInPackage == 0)
                                {
                                    //we have nothing to lose really:  Try to merge the session in case it will compress down to fit.
                                    newPackage.AddSession(sessionStream); // This will copy and dispose sessionStream before returning.
                                    packagingState.LastSessionId = session.Id; //and if we stop on the next loop then this is the session we ended on.
                                    sessionsMergedInPackage++;

                                    //we're going to exceed our length.. or ARE we?  lets save the package (so we get the benefit of compression) and check again.
                                    newPackage.Save(progressMonitors, temporaryFileNamePath); //because we need to find out the file size to know where we are.
                                    sessionsSizeBytes = (int)FileSystemTools.GetFileSize(temporaryFileNamePath);

                                    //if we still are too big then it's curtains for this session, it's just not safe to send it.  Reset the package and keep
                                    //going on to the next session
                                    if (sessionsSizeBytes > maxPackageSizeBytes)
                                    {
                                        if (!Log.SilentMode)
                                            Log.Write(LogMessageSeverity.Warning, LogCategory, "Session is too large to fit in a package", "The current session has {0} bytes of session data, when packaged is {1} bytes which would exceed the max of {2} bytes.  This session will be skipped.", sessionLength, sessionsSizeBytes, maxPackageSizeBytes);

                                        //dump this package and reset so we're ready for a new one.
                                        newPackage.Dispose();
                                        newPackage = new SimplePackage();
                                        File.Delete(temporaryFileNamePath);
                                        sessionsMergedInPackage = 0;
                                        sessionsSizeBytes = 0;

                                        //and super special case:  We need to dump the stream too because nothing is going to do that later.
                                        //sessionStream.Dispose(); // It's already disposed from when we added it to the package.
                                    }
                                    else
                                    {
                                        // We're actually keeping it, so check it's user name.
                                        sessionUserName = session.UserName;
                                    }

                                }
                                else
                                {
                                    if (!Log.SilentMode)
                                        Log.Write(LogMessageSeverity.Verbose, LogCategory, "Package is projected to exceed max allowed size, no more sessions will be added.", "The current package size is {0} bytes of session data, this new package is {1} bytes which would exceed the max of {2} bytes.", sessionsSizeBytes, sessionLength, maxPackageSizeBytes);
                                    packagingState.IsComplete = false; //we defaulted this to true at the top.

                                    //since we're going to have to stop for this package, but we don't want to lose the work that went into getting the stream
                                    //we need to store it all off.
                                    packagingState.NextSessionId = session.Id;
                                    packagingState.NextSessionStream = sessionStream; // Save it to try in the next package.
                                    break;
                                }
                            }
                            else
                            {
                                //go ahead and merge this session.
                                newPackage.AddSession(sessionStream); // This will copy and dispose the sessionStream before returning.
                                packagingState.LastSessionId = session.Id; //and if we stop on the next loop then this is the session we ended on.
                                sessionsSizeBytes += (int)sessionLength; //it's an approximation but it'll do until we do the next hard save.
                                sessionsMergedInPackage++;
                                sessionUserName = session.UserName;
                            }

                            if (userNameList.Count < UserListMaxCount && string.IsNullOrEmpty(sessionUserName) == false)
                            {
                                if (userNameList.Contains(sessionUserName) == false)
                                {
                                    userNameList.Add(sessionUserName);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        packagingState.LastSessionId = session.Id; //in case we have an error below we want to know we tried this one.
                        if (!Log.SilentMode) Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Unable to add selected session to transport package", "While attempting to add a session that was selected to the transport package an exception was thrown.  The session will be skipped.  Exception:\r\n{0}", ex.Message);
                    }
                }
            }

            //make sure the package actually HAS any sessions in it - it's possible that each session we wanted to put in errored out
            //or was oversize or something.
            if (sessionsMergedInPackage == 0)
            {
                newPackage.Dispose();
                newPackage = null;

                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Verbose, LogCategory, "Suppressing empty working package", "No exportable sessions were identified in the last set of sessions to write out, so the package would have been empty.");
            }

            if (newPackage != null)
            {
                //Do a final save of our package.
                newPackage.Save(progressMonitors, temporaryFileNamePath); //because we need to find out the file size to know where we are.

                //and we need to write a final, transportable package to go where the user wants it
                //calculate a good caption & description
                string effectiveAppName = string.IsNullOrEmpty(ApplicationName) ? ProductName : string.Format("{0} {1}", ProductName, ApplicationName);
                int userNameCount = Math.Min(userNameList.Count, UserListMaxCount);
                string effectiveUserName = string.Format(UserListFormat[userNameCount], userNameList.ToArray());

                int files;
                long bytes;
                newPackage.GetStats(out var sessions, out var problemSessions, out files, out bytes);

                //now we can figure out the right caption & description
                string sessionPlural = "s"; // Assume usually plural...
                string problemLabel = "a"; // Assume usually singular...
                if (problemSessions > 0)
                {
                    if (problemSessions == 1) // We're only putting plural on problem session(s); session just says "Total".
                        sessionPlural = String.Empty; // So we can borrow this for problem session(s).
                    else
                        problemLabel = "Multiple";

                    hasProblemSessions = true;
                    newPackage.Caption = string.Format("{0} Sessions from {1} ({2} Problem Session{3} of {4} Total)",
                                                       effectiveAppName, effectiveUserName, problemSessions, sessionPlural, sessions);
                    newPackage.Description = string.Format("!!!This Package Contains {0} Problem Session{1}!!!\r\n\r\nProduct: {2}\r\nComputer: {3}\r\nUser:{4}\r\nTotal Sessions: {5}\r\nProblem Sessions: {6}.  A problem session has at least one error or has crashed.\r\nGenerated: {7}\r\n",
                                                           problemLabel, sessionPlural, effectiveAppName, Log.SessionSummary.HostName,
                                                           Log.SessionSummary.FullyQualifiedUserName, sessions, problemSessions, DateTimeOffset.Now);
                }
                else
                {
                    if (sessions == 1)
                        sessionPlural = String.Empty;

                    newPackage.Caption = string.Format("{0} Sessions from {1} ({2} Session{3})",
                                                       effectiveAppName, effectiveUserName, sessions, sessionPlural);
                    newPackage.Description = string.Format("Product: {0}\r\nComputer: {1}\r\nUser:{2}\r\nTotal Sessions: {3}\r\nGenerated: {4}\r\n",
                                                           effectiveAppName, Log.SessionSummary.HostName, Log.SessionSummary.FullyQualifiedUserName, sessions, DateTimeOffset.Now);
                }

                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Verbose, LogCategory, "Completed creating working package", "Package is momentarily written to a temporary file.\r\nCaption: {0}\r\nDescription: {1}\r\nPackaging Complete: {2}", newPackage.Caption, newPackage.Description, packagingState.IsComplete);
            }

            return newPackage;
        }

        /// <summary>
        /// Log the request information for a send to server request
        /// </summary>
        private static void LogSendToServer(bool markAsRead, bool purgeSentSessions, ServerConfiguration serverConfiguration)
        {
            StringBuilder message = new StringBuilder(1024);

            string hubConnectionParameters;
            if (serverConfiguration == null)
            {
                message.AppendLine("Sessions are being sent using application default Server settings.");
                hubConnectionParameters = string.Empty;
            }
            else
            {
                if (serverConfiguration.UseGibraltarService)
                {
                    message.AppendLine("Sessions are being sent to the Loupe Service.\r\n");
                    hubConnectionParameters = "\r\nHub Customer: {0}";
                }
                else
                {
                    message.AppendLine("Sessions are being sent to a private Loupe Server.\r\n");
                    hubConnectionParameters = "\r\nServer: {1}\r\nPort: {2}\r\nUse SSL: {3}\r\nApplication Base Directory: {4}\r\nRepository: {5}";
                }
            }

            if ((markAsRead) && (purgeSentSessions))
            {
                message.AppendLine("Sessions will be marked as read and removed from the local computer once confirmed by the server.");
            }
            else if (markAsRead)
            {
                message.AppendLine("Sessions will be marked as read once confirmed by the server.");
            }
            else if (purgeSentSessions)
            {
                message.AppendLine("Sessions will be removed from the local computer once confirmed by the server.");
            }

            if (string.IsNullOrEmpty(hubConnectionParameters) == false)
            {
                message.AppendFormat(hubConnectionParameters, serverConfiguration.CustomerName, serverConfiguration.Server, serverConfiguration.Port, 
                    serverConfiguration.UseSsl, serverConfiguration.ApplicationBaseDirectory, serverConfiguration.Repository);
            }

            Log.Write(LogMessageSeverity.Information, LogCategory, "Sending sessions to Server", message.ToString());
        }

        /// <summary>
        /// work the transport queue until it's empty.
        /// </summary>
        /// <param name="state"></param>
        private void AsyncTransportPackage(object state)
        {
            try //because we're called from the thread pool we have to catch exceptions.
            {
                //there had better be a package queued or we shouldn't have gotten fired up.
                TransportPackageBase currentPackage = TransportDequeue();

                while (currentPackage != null)
                {
                    //send this bad boy.
                    try
                    {
                        currentPackage.Send(new ProgressMonitorStack("Background Package Transport")); //we aren't relaying progress from this thread back to our main progress.
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Failed to transport package", "While attempting to transport a queued package an exception was thrown:\r\n{0)", ex.Message);
                    }

                    //queue it for cleanup
                    CleanupEnqueue(currentPackage);

                    //see if there are any more and if not (and we're going to exit) we need to clear our dispatched flag.
                    lock(m_TransportThreadLock) //when getting multiple locks, think through order.  In our case doesn't matter YET but this is the order they are gotten elsewhere.
                    {
                        lock(m_TransportQueue)
                        {
                            currentPackage = TransportDequeue();
                            if (currentPackage == null)
                            {
                                m_TransportThreadDispatched = false; //so the next queue attempt will re-dispatch.
                            }

                            System.Threading.Monitor.PulseAll(m_TransportQueue);
                        }

                        System.Threading.Monitor.PulseAll(m_TransportThreadLock);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.RecordException(0, ex, null, LogCategory, true);
            }
        }

        /// <summary>
        /// Performs the actual packaging and storing of sessions in a file, safe for async calling.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        private PackageSendEventArgs ActionSendToFile(object state)
        {
            OnBeginSend();

            if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Starting asynchronous send to file", null);

            Predicate<ISessionSummary> sessionPredicate = null;
            SessionCriteria? sessionCriteria = null;
            bool markAsRead;
            ProgressMonitorStack progressMonitors;
            string destinationFileNamePath;

            //two ways that state may be:  We may be receiving an async task argument or an array of objects.
            AsyncTaskArguments asyncTaskArguments = state as AsyncTaskArguments;
            if (asyncTaskArguments != null)
            {
                object[] arguments = (object[])asyncTaskArguments.State;
                if (arguments[0] is SessionCriteria)
                {
                    sessionCriteria = (SessionCriteria)arguments[0];
                }
                else
                {
                    sessionPredicate = (Predicate<ISessionSummary>)arguments[0];
                }
                markAsRead = (bool)arguments[1];
                destinationFileNamePath = (string)arguments[2];
                progressMonitors = asyncTaskArguments.ProgressMonitors;
            }
            else
            {
                //we got an array with the two elements we want.
                object[] arguments = (object[])state;
                if (arguments[0] is SessionCriteria)
                {
                    sessionCriteria = (SessionCriteria)arguments[0];
                }
                else
                {
                    sessionPredicate = (Predicate<ISessionSummary>)arguments[0];
                }
                markAsRead = (bool)arguments[1];
                destinationFileNamePath = (string)arguments[2];
                progressMonitors = (ProgressMonitorStack)arguments[3];
            }

            Log.Write(LogMessageSeverity.Information, LogCategory, "Sending package to file", "When complete sessions will be in the file '{0}'.{1}", destinationFileNamePath, (markAsRead ? "If successful, sessions will be marked as read and not sent again." : null));

            PackageSendEventArgs returnVal;
            using (ProgressMonitor ourMonitor = progressMonitors.NewMonitor(this, "Sending session information to file", 1))
            {
                //clean up the file name to make sure it has the right extension and no goofiness we can't deal with.
                string currentExtension = null;
                string targetExtension = "." + Log.PackageExtension;
                destinationFileNamePath = destinationFileNamePath.TrimEnd();
                try
                {
                    currentExtension = Path.GetExtension(destinationFileNamePath);
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
#if DEBUG
                    Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Unable to determine extension of proposed file name", "File Name: {0}\r\nException: {1}", destinationFileNamePath, ex.Message);
#endif
                }

                if (string.IsNullOrEmpty(currentExtension))
                {
                    //no existing extension, add it
                    destinationFileNamePath = destinationFileNamePath.TrimEnd() + targetExtension;
                }
                else if (currentExtension.Equals(targetExtension, StringComparison.OrdinalIgnoreCase) == false)
                {
                    //replace the existing extension.
                    destinationFileNamePath = destinationFileNamePath.Substring(0, destinationFileNamePath.Length - currentExtension.Length) + targetExtension;
                }

                if (markAsRead)
                {
                    //we have three steps
                    ourMonitor.Update("Finding sessions...", 0, 3);
                }
                else
                {
                    ourMonitor.Update("Finding sessions...", 0, 2);
                }

                FileTransportPackage fileTransportPackage = null;
                try //so we can be sure we dispose the file transport package
                {
                    bool hasProblemSessions;
                    var selectedSessions = sessionCriteria.HasValue ? FindPackageSessions(sessionCriteria.Value, progressMonitors, false, out hasProblemSessions)
                        : FindPackageSessions(sessionPredicate, progressMonitors, false, out hasProblemSessions);

                    //see if there's anything to actually package...
                    if ((selectedSessions != null) && (selectedSessions.Count > 0))
                    {
                        ourMonitor.Update("Packaging Sessions...", 1);

                        //we are going to use a sub-progress monitor here so we can go session-by-session.
                        using (ProgressMonitor innerMonitor = progressMonitors.NewMonitor(this, "Packaging Sessions...", selectedSessions.Count))
                        {
                            //what's the max package size?  it's the free space of the drive we're writing to or 2GB, whichever is less.
                            DriveInfo targetDrive = new DriveInfo(Path.GetPathRoot(destinationFileNamePath));

                            int maxPackageSize = (int)Math.Min(targetDrive.AvailableFreeSpace, 2147483647); //one byte below 2GB

                            PackagingState packagingState = new PackagingState();

                            //create our one transportable package.  It'll get disposed when the file transport package below gets disposed.
                            SimplePackage newPackage = CreateTransportablePackage(progressMonitors, innerMonitor, selectedSessions, maxPackageSize, out hasProblemSessions, packagingState);

                            //we may not get back a transportable package in some cases - like when all of the remaining sessions are not exportable.
                            if (newPackage != null)
                            {
                                fileTransportPackage = new FileTransportPackage(ProductName, ApplicationName, newPackage, destinationFileNamePath);
                            }
                        }
                    }

                    if (fileTransportPackage == null)
                    {
                        //this is really a duplicate of the output a few clauses above
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Verbose, LogCategory, "No Sessions to Send", "The packager process didn't find any sessions to send in the package based on the selection criteria.");
                        ourMonitor.Update("No sessions were found to send.", 3, 3);
                        returnVal = new PackageSendEventArgs(0, AsyncTaskResult.Information, "No sessions were found to send.", null);
                    }
                    else
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Verbose, LogCategory, "Writing file package to destination", "The final package is being written to '{0}'", destinationFileNamePath);
                        ourMonitor.Update("Saving package to disk...", 2);
                        fileTransportPackage.Send(progressMonitors);
                        returnVal = fileTransportPackage.Status;

                        if (returnVal.Result == AsyncTaskResult.Success)
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Verbose, LogCategory, "Package Written to Disk", "The packager was able to write a package to {0}", destinationFileNamePath);

                            if (markAsRead)
                            {
                                ourMonitor.Update("Cleaning up temporary data...", 3);
                                fileTransportPackage.MarkContentsAsRead(m_Repository);
                            }

                            ourMonitor.Update("Packaging complete.");
                        }
                    }
                }
                finally
                {
                    //make sure we dispose the transport package so it disposes the inner package.
                    if (fileTransportPackage != null)
                    {
                        fileTransportPackage.Dispose();
                    }
                }
            }

            // This will extract the caption automatically, including (the first line of) the status message.
            string statusCaption;
            LogMessageSeverity resultSeverity;
            switch (returnVal.Result)
            {
                case AsyncTaskResult.Unknown:
                    statusCaption = "Completed asynchronous package send to file";
                    resultSeverity = LogMessageSeverity.Warning;
                    break;
                case AsyncTaskResult.Canceled:
                    statusCaption = "Canceled asynchronous package send to file";
                    resultSeverity = LogMessageSeverity.Warning;
                    break;
                case AsyncTaskResult.Error:
                    statusCaption = "Completed asynchronous package send to file with errors";
                    resultSeverity = LogMessageSeverity.Error;
                    break;
                case AsyncTaskResult.Warning:
                    statusCaption = "Completed asynchronous package send to file with warnings";
                    resultSeverity = LogMessageSeverity.Warning;
                    break;
                case AsyncTaskResult.Information:
                    statusCaption = "Completed asynchronous package send to file";
                    resultSeverity = LogMessageSeverity.Information;
                    break;
                case AsyncTaskResult.Success:
                    statusCaption = "Completed asynchronous package send to file";
                    resultSeverity = LogMessageSeverity.Verbose;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!Log.SilentMode)
                Log.WriteMessage(resultSeverity, LogWriteMode.Queued, 0, returnVal.Exception, null, statusCaption, returnVal.Message); 

            if (asyncTaskArguments != null)
            {
                asyncTaskArguments.TaskResult = returnVal;
            }

            OnEndSend(returnVal);
            return returnVal;
        }

        /// <summary>
        /// Performs the actual packaging and transmission of sessions via SDS, safe for async calling.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">The server configuration specified is invalid</exception>
        private async Task<PackageSendEventArgs> ActionSendToServer(object state)
        {
            OnBeginSend();

            if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Starting asynchronous send to web", null);

            Predicate<ISessionSummary> sessionPredicate = null;
            SessionCriteria? sessionCriteria = null;
            bool markAsRead;
            bool purgeSentSessions;
            ProgressMonitorStack progressMonitors;
            bool suppressLogging = false;

            ServerConfiguration serverConfiguration = null;

            //two ways that state may be:  We may be receiving an async task argument or an array of objects.

            AsyncTaskArguments asyncTaskArguments = state as AsyncTaskArguments;
            if (asyncTaskArguments != null)
            {
                object[] arguments = (object[])asyncTaskArguments.State;
                if (arguments[0] is SessionCriteria)
                {
                    sessionCriteria = (SessionCriteria)arguments[0];
                }
                else
                {
                    sessionPredicate = (Predicate<ISessionSummary>)arguments[0];
                }
                markAsRead = (bool)arguments[1];
                purgeSentSessions = (bool)arguments[2];
                serverConfiguration = (ServerConfiguration)arguments[3];
                suppressLogging = (bool) arguments[4];
                progressMonitors = asyncTaskArguments.ProgressMonitors;
            }
            else
            {
                //we got an array with the elements we want.
                object[] arguments = (object[])state;
                if (arguments[0] is SessionCriteria)
                {
                    sessionCriteria = (SessionCriteria)arguments[0];
                }
                else
                {
                    sessionPredicate = (Predicate<ISessionSummary>)arguments[0];
                }
                markAsRead = (bool)arguments[1];
                purgeSentSessions = (bool)arguments[2];
                serverConfiguration = (ServerConfiguration)arguments[3];
                progressMonitors = (ProgressMonitorStack)arguments[4];
                suppressLogging = (bool)arguments[5];
            }

            if (!suppressLogging)
                LogSendToServer(markAsRead, purgeSentSessions, serverConfiguration);

            //before we waste any time, lets see if we're going to be successful.
            if (serverConfiguration != null)
            {
                //this is a little odd but we need to change exception types - this throws an invalid operation exception, we need an argument exception.
                try
                {
                    serverConfiguration.Validate();
                }
                catch (InvalidOperationException ex)
                {
                    throw new ArgumentException(ex.Message, ex);
                }
            }
            else
            {
                //find our running configuration and validate that.
                Log.Configuration.Server.Validate();
            }

            PackageSendEventArgs returnVal;
            using (ProgressMonitor ourMonitor = progressMonitors.NewMonitor(this, "Sending session information...", 1))
            {
                ourMonitor.Update("Finding sessions...", 0);

                bool hasProblemSessions;
                var selectedSessions = sessionCriteria.HasValue ? FindPackageSessions(sessionCriteria.Value, progressMonitors, suppressLogging, out hasProblemSessions)
                    : FindPackageSessions(sessionPredicate, progressMonitors, suppressLogging, out hasProblemSessions);

                //see if there's anything to actually package...
                if ((selectedSessions != null) && (selectedSessions.Count > 0))
                {
                    ourMonitor.Update("Sending Sessions...", 0);

                    //we are going to use a sub-progress monitor here so we can go session-by-session.
                    using (ProgressMonitor innerMonitor = progressMonitors.NewMonitor(this, "Sending Sessions...", selectedSessions.Count))
                    {
                        //now we connect to the server and send each of these sessions
                        innerMonitor.Update("Connecting to server...");
                        RepositoryPublishClient publishClient;

                        if (serverConfiguration != null)
                        {
                            publishClient = new RepositoryPublishClient(m_Repository, serverConfiguration);
                        }
                        else
                        {
                            publishClient = new RepositoryPublishClient(m_Repository);
                        }

                        int currentStep = 0, totalSteps = selectedSessions.Count;

                        int fileSizeBytes = 0;
                        AsyncTaskResult result;
                        string statusMessage;
                        Exception taskException = null;

                        //try to connect.  If we can't do that, there's no point.
                        var status = await publishClient.CanConnect().ConfigureAwait(false);
                        if (status.IsValid == false)
                        {
                            result = AsyncTaskResult.Error;
                            statusMessage = string.Format("Unable to send sessions to server.\r\n{0}\r\nIt's possible that your Internet connection is unavailable or that software on your computer is blocking network traffic.\r\n\r\nVerify that you have an active Internet connection and that you don't have software on your computer that will block applications communicating to the Internet.", status.Message);
                        }
                        else
                        {
                            try
                            {
                                foreach (var session in selectedSessions)
                                {
                                    DateTimeOffset sessionUpdatedTime = session.EndDateTime;

                                    innerMonitor.Update(string.Format("Sending session {0} of {1}...", currentStep + 1, totalSteps), currentStep, totalSteps);

                                    await publishClient.UploadSession(session.Id, 2, purgeSentSessions).ConfigureAwait(false); //we give it a maximum of two retries before we give up on the connection.  

                                    if (markAsRead)
                                    {
                                        var localRepository = publishClient.Repository;
                                        try
                                        {
                                            localRepository.SetSessionNew(session.Id, false);
                                        }
                                        catch (Exception ex)
                                        {
                                            if (!Log.SilentMode)
                                                Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Error marking an included session as read", "Unable to mark a session we successfully uploaded as read.  This won't prevent sessions from being sent.  Exception:\r\n{0}", ex.Message);
                                        }
                                    }

                                    currentStep++;
                                }

                                //no error? we're all good!
                                result = AsyncTaskResult.Success;
                                statusMessage = string.Format("{0} Sessions Sent.", totalSteps);
                                ourMonitor.Update(statusMessage, 1, 1);
                            }
                            catch (Exception ex)
                            {
                                result = AsyncTaskResult.Error;
                                statusMessage = "Unable to send sessions to server.\r\n\r\nIt's possible that your Internet connection is unavailable or that software on your computer is blocking network traffic.\r\n\r\nVerify that you have an active Internet connection and that you don't have software on your computer that will block applications communicating to the Internet.";
                                taskException = ex;
                            }
                        }

                        returnVal = new PackageSendEventArgs(fileSizeBytes, result, statusMessage, taskException);
                    }
                }
                else
                {
                    if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "No Sessions to Send", "The packager process didn't find any sessions to send in the package based on the selection criteria.");
                    ourMonitor.Update("No sessions were found to send.", 1, 1);
                    returnVal = new PackageSendEventArgs(0, AsyncTaskResult.Information, "No sessions were found to send.", null);
                }

                if (asyncTaskArguments != null)
                {
                    asyncTaskArguments.TaskResult = returnVal;
                }
            }

            // This will extract the caption automatically, including (the first line of) the status message.
            string statusCaption;
            LogMessageSeverity resultSeverity;
            switch (returnVal.Result)
            {
                case AsyncTaskResult.Unknown:
                    statusCaption = "Completed asynchronous session transmission to server";
                    resultSeverity = LogMessageSeverity.Warning;
                    break;
                case AsyncTaskResult.Canceled:
                    statusCaption = "Canceled asynchronous session transmission to server";
                    resultSeverity = LogMessageSeverity.Warning;
                    break;
                case AsyncTaskResult.Error:
                    statusCaption = "Completed asynchronous session transmission to server with errors";
                    resultSeverity = LogMessageSeverity.Error;
                    break;
                case AsyncTaskResult.Warning:
                    statusCaption = "Completed asynchronous session transmission to server with warnings";
                    resultSeverity = LogMessageSeverity.Warning;
                    break;
                case AsyncTaskResult.Information:
                    statusCaption = "Completed asynchronous session transmission to server";
                    resultSeverity = LogMessageSeverity.Information;
                    break;
                case AsyncTaskResult.Success:
                    statusCaption = "Completed asynchronous session transmission to server";
                    resultSeverity = LogMessageSeverity.Verbose;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!Log.SilentMode)
                Log.WriteMessage(resultSeverity, LogWriteMode.Queued, 0, returnVal.Exception, null, statusCaption, returnVal.Message);

            if (asyncTaskArguments != null)
            {
                asyncTaskArguments.TaskResult = returnVal;
            }

            OnEndSend(returnVal);
            return returnVal;
        } 

        /// <summary>
        /// WaitCallback compatible wrapper for ActionSendToFile.
        /// </summary>
        /// <param name="state"></param>
        private void AsyncSendToFile(object state)
        {
            try //this is called from a background thread so exceptions would be fatal to the application.
            {
                //we just wrapper our inner function
                ActionSendToFile(state);
            }
            catch (Exception ex)
            {
                SafeHandleAsyncSendException(state, "Unable to create and write package to a file", ex);
            }
        }

        /// <summary>
        /// WaitCallback compatible wrapper for ActionSendToWeb.
        /// </summary>
        /// <param name="state"></param>
        private void AsyncSendToServer(object state)
        {
            try //this is called from a background thread so exceptions would be fatal to the application.
            {
                //we just wrapper our inner function
                var task = Task.Run(() => ActionSendToServer(state));
                var results = task.Result;
            }
            catch (Exception ex)
            {
                SafeHandleAsyncSendException(state, "Unable to create and send package", ex);
            }
        }

        private void SafeHandleAsyncSendException(object state, string message, Exception ex)
        {
            //unwrap any AggregateExceptions..
            if (ex is System.AggregateException)
            {
                ex = ex.GetBaseException();
            }

            var asyncTaskArguments = state as AsyncTaskArguments;
            if (asyncTaskArguments != null)
            {
                asyncTaskArguments.TaskResult = new AsyncTaskResultEventArgs(AsyncTaskResult.Error, message, ex);
            }

            //and we need to try hard to raise our on end event if at all possible.
            OnEndSend(new PackageSendEventArgs(0, AsyncTaskResult.Error, message, ex));
        }

        #endregion

        #region Private Class PackagingState

        private class PackagingState
        {
            /// <summary>
            /// The last session that we either packaged or attempted to package, and should start immediately after.
            /// </summary>
            public Guid? LastSessionId { get; set; }

            /// <summary>
            /// The session to start the next package on.  The NextSessionStream property will be set if this is set.
            /// </summary>
            public Guid? NextSessionId { get; set; }

            /// <summary>
            /// A working session stream that could not be stored into the last package.
            /// </summary>
            public Stream NextSessionStream { get; set; }

            /// <summary>
            /// Indicates if all of the sessions have been packaged (packaging is therefore complete) or not
            /// </summary>
            public bool IsComplete { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Used to provide information on the status of a package send.
    /// </summary>
    /// <param name="sender">The packager object raising the event</param>
    /// <param name="e">The information on the package send event</param>
    public delegate void PackageSendEventHandler(object sender, PackageSendEventArgs e);
}
