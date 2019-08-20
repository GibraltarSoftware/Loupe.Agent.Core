using System;
using System.Threading;
using System.Threading.Tasks;
using Loupe.Configuration;
using Loupe.Core.Data;
using Loupe.Core.IO.Internal;
using Loupe.Core.Messaging;
using Loupe.Core.Monitor;
using Loupe.Core.Server.Client;
using Loupe.Extensibility.Data;

namespace Loupe.Core.IO
{
    /// <summary>
    /// Performs constant, background publishing of sessions from the repository.
    /// </summary>
    public class RepositoryPublishEngine
    {
        private const string LogCategory = "Loupe.Repository.Publish";
        private const string BaseMultiprocessLockName = "repositoryPublish";
        private const string RepositoryPublishVersionName = "Repository Publish Version";
        private int BackgroundStartupDelay = 5000; //5 seconds
        private const int ShortCheckIntervalSeconds = 60;
        private const int LongCheckIntervalSeconds = 1800;
        private const int ExpiredCheckIntervalSeconds = 3600;

        private readonly object m_SessionPublishThreadLock = new object();
        private readonly string m_ProductName;
        private readonly string m_ApplicationName;
        private readonly ServerConfiguration m_Configuration;
        private readonly string m_RepositoryFolder;   //used to coordinate where to coordinate locks
        private readonly string m_MultiprocessLockName; //used to establish our unique publisher lock.

        private volatile bool m_Initialized; //designed to enable us to do our initialization in the background. PROTECTED BY THREADLOCK
        private volatile bool m_StopRequested;
        private volatile bool m_SessionPublishThreadFailed; //PROTECTED BY THREADLOCK
        private Thread m_SessionPublishThread; //PROTECTED BY THREADLOCK
        private RepositoryPublishClient m_Client; //PROTECTED BY THREADLOCK
        private bool m_ForceCheck; //PROTECTED BY THREADLOCK
        private DateTimeOffset m_LastCheck = DateTimeOffset.MinValue; //PROTECTED BY THREADLOCK
        private TimeSpan m_CheckInterval = new TimeSpan(0, 1, 0); //PROTECTED BY THREADLOCK
        private TimeSpan m_MultiprocessLockCheckInterval = new TimeSpan(0, 5, 0);
        private int m_FailedAttempts; //PROTECTED BY THREADLOCK

        internal RepositoryPublishEngine(Publisher publisher, AgentConfiguration configuration)
        {
            m_ProductName = publisher.SessionSummary.Product;
            m_ApplicationName = publisher.SessionSummary.Application;
            m_Configuration = configuration.Server;
            m_SessionPublishThreadFailed = true;  //otherwise we won't start it when we need to.

            //find the repository path we're using.  We use the same logic that the FileMessenger users.
            m_RepositoryFolder = LocalRepository.CalculateRepositoryPath(m_ProductName, configuration.SessionFile.Folder);

            //create the correct lock name for our scope.
            m_MultiprocessLockName = BaseMultiprocessLockName + "~" + m_ProductName + 
                (m_Configuration.SendAllApplications ? string.Empty : "~" + m_ApplicationName);

            //we have to make sure the multiprocess lock doesn't have any unsafe characters.
            m_MultiprocessLockName = FileSystemTools.SanitizeFileName(m_MultiprocessLockName);
        }

        /// <summary>
        /// Create a repository publish engine for the specified local repository to the remote server.
        /// </summary>
        /// <param name="productName">Required. The product to restrict sending to.</param>
        /// <param name="applicationName">Optional.  The application to restrict sending to.</param>
        /// <param name="directory">Optional.  The base directory of the repository, overriding the system default.</param>
        /// <param name="serverConfiguration">The server to publish to.</param>
        public RepositoryPublishEngine(string productName, string applicationName, string directory,
            ServerConfiguration serverConfiguration)
        {
            if (serverConfiguration == null)
                throw new ArgumentNullException(nameof(serverConfiguration));

            if (string.IsNullOrWhiteSpace(productName))
                throw new ArgumentNullException(nameof(productName));

            m_ProductName = productName;
            m_ApplicationName = applicationName;
            m_Configuration = serverConfiguration;
            m_SessionPublishThreadFailed = true;  //otherwise we won't start it when we need to.

            //find the repository path we're using.  We use the same logic that the FileMessenger users.
            m_RepositoryFolder = LocalRepository.CalculateRepositoryPath(productName, directory);

            //create the correct lock name for our scope.
            m_MultiprocessLockName = BaseMultiprocessLockName + "~" + productName +
                                     (string.IsNullOrEmpty(applicationName) ? string.Empty : "~" + applicationName);

            //we have to make sure the multiprocess lock doesn't have any unsafe characters.
            m_MultiprocessLockName = FileSystemTools.SanitizeFileName(m_MultiprocessLockName);

            BackgroundStartupDelay = 0;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Indicates if the publisher has a valid configuration and is running.
        /// </summary>
        public bool IsActive
        {
            get
            {
                return (m_Initialized && (m_SessionPublishThreadFailed == false)); //protected by volatile
            }
        }

        /// <summary>
        /// Start the engine's background processing thread.  If it is currently running the call has no effect.
        /// </summary>
        public void Start()
        {
            EnsureSessionPublishThreadIsValid(); //all we have to do is fire up the background thread.
        }

        /// <summary>
        /// Stop publishing sessions
        /// </summary>
        /// <param name="waitForStop">Indicates if the caller wants to wait for the engine to stop before returning</param>
        /// <remarks></remarks>
        public void Stop(bool waitForStop)
        {
            if (IsActive)
            {
                //request the background thread stop
                m_StopRequested = true; //protected by volatile

                //and now wait for it.
                if (waitForStop)
                {
                    lock(m_SessionPublishThreadLock)
                    {
                        while (IsActive)
                        {
                            System.Threading.Monitor.Wait(m_SessionPublishThreadLock, 16);
                        }

                        System.Threading.Monitor.PulseAll(m_SessionPublishThreadLock);
                    }
                }
            }
        }

        #endregion

        #region Private Properties and Methods

        private void CreateMessageDispatchThread()
        {
            lock (m_SessionPublishThreadLock)
            {
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Information, LogCategory, "Starting Repository Publisher", "Initializing a new publisher thread.");

                //clear the dispatch thread failed flag so no one else tries to create our thread
                m_SessionPublishThreadFailed = false;

                m_SessionPublishThread = new Thread(RepositoryPublishMain);
                m_SessionPublishThread.Name = "Loupe Session Publisher"; //name our thread so we can isolate it out of metrics and such
                m_SessionPublishThread.IsBackground = true;
                m_SessionPublishThread.Start();

                //and prep ourselves for checking
                m_ForceCheck = true;

                System.Threading.Monitor.PulseAll(m_SessionPublishThreadLock);
            }
        }

        private async void RepositoryPublishMain()
        {
            //before we get going, lets stall for a few seconds.  We aren't a critical operation, and I don't 
            //want to get in the way of the application starting up.
            if (BackgroundStartupDelay > 0)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Information, LogCategory,
                        "Waiting for startup delay to start publisher",
                        "To avoid competing against other startup activities we're going to delay for {0} before we start.",
                        BackgroundStartupDelay);

                await Task.Delay(BackgroundStartupDelay).ConfigureAwait(false);
            }

            InterprocessLock backgroundLock = null; //we can't do our normal using trick in this situation.
            try
            {
                //we have two totally different modes:  Either WE'RE the background processor or someone else is.
                //if we are then we move on to start publishing.  If someone else is then we just poll
                //the lock to see if whoever owned it has exited.                 
                backgroundLock = GetLock(0);
                while ((backgroundLock == null) && (m_StopRequested == false))
                {
                    //we didn't get the lock - so someone else is currently the main background thread.
                    SleepUntilNextCheck(m_MultiprocessLockCheckInterval);
                    backgroundLock = GetLock(0);
                }

                //if we got the lock then we want to go ahead and perform background processing.
                if (backgroundLock != null)
                {
                    //here is where we want to keep looping - it will return every time the subscription changes.
                    await RepositoryPublishLoop().ConfigureAwait(false);

                    //release the lock; we'll get it on the next round.
                    backgroundLock.Dispose();
                    backgroundLock = null;
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
            }
            finally
            {
                if (backgroundLock != null)
                    backgroundLock.Dispose();

                lock (m_SessionPublishThreadLock)
                {
                    //clear the dispatch thread variable since we're about to exit.
                    m_SessionPublishThread = null;

                    //we want to write out that we had a problem and mark that we're failed so we'll get restarted.
                    m_SessionPublishThreadFailed = true;

                    System.Threading.Monitor.PulseAll(m_SessionPublishThreadLock);
                }

                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Information, LogCategory, "Background session publisher thread has stopped", null);
            }
        }

        /// <summary>
        /// Called when we're the one true publisher for our data to have us poll for data to push and push as soon as available.
        /// </summary>
        private async Task RepositoryPublishLoop()
        {
            // Now we need to make sure we're initialized.
            lock (m_SessionPublishThreadLock)
            {
                //are we initialized?  
                EnsureInitialized();

                System.Threading.Monitor.PulseAll(m_SessionPublishThreadLock);
            }

            //if we managed to initialize completely then lets get rockin'
            if (m_Initialized)
            {
                while (m_StopRequested == false)
                {
                    //make sure the server is available.  if not there's no point in proceeding.
                    var serverStatus = await m_Client.CanConnect().ConfigureAwait(false);
                    if (serverStatus.IsValid)
                    {
                        //make sure SendSessionsOnExit is set (since we were active)
                        string ignoreMessage = string.Empty;
                        if ((Log.SendSessionsOnExit == false) && (Log.CanSendSessionsOnExit(ref ignoreMessage)))
                        {
                            Log.SendSessionsOnExit = true;
                        }                        

                        //make sure we're set to the shorter check interval.
                        m_CheckInterval = new TimeSpan(0, 0, ShortCheckIntervalSeconds);
                        m_FailedAttempts = 0;

                        //perform one process cycle.  This is isolated so that whenever it needs to end it can just return and we'll sleep.  
                        await m_Client.PublishSessions(false, m_Configuration.PurgeSentSessions).ConfigureAwait(false);
                    }
                    else
                    {
                        if (serverStatus.Status == HubStatus.Expired)
                        {
                            //use the extra long delay since it's very unlikely it'll get fixed.
                            m_CheckInterval = new TimeSpan(0, 0, ExpiredCheckIntervalSeconds);
                        }
                        else if (m_FailedAttempts > 5)
                        {
                            m_CheckInterval = new TimeSpan(0, 0, LongCheckIntervalSeconds);
                            m_FailedAttempts++;
                        }
                        else
                        {
                            m_CheckInterval = new TimeSpan(0, 0, ShortCheckIntervalSeconds);
                            m_FailedAttempts++;
                        }
#if DEBUG
                        Log.Write(LogMessageSeverity.Warning, LogCategory, "Sessions will not be published to the server because it can't be contacted",
                                  "Status: {0}\r\nMessage: {1}\r\nCheck Interval: {2}", serverStatus.Status, serverStatus.Message, m_CheckInterval);
#endif
                    }

                    //now it's time to rest our sleep interval unless there is a force request.
                    SleepUntilNextCheck(m_CheckInterval);
                }
            }            
        }

        private void EnsureInitialized()
        {
            if (m_Initialized == false)
            {
                lock(m_SessionPublishThreadLock)
                {
                    try
                    {
                        //set up the repository client to work with the collection repository
                        LocalRepository repository = Log.Repository;

                        if (m_RepositoryFolder.Equals(repository.Name, StringComparison.OrdinalIgnoreCase) == false)
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Information, LogCategory,
                                    string.Format("Overriding repository for publishing from '{0}' to '{1}'",
                                        repository.Name, m_RepositoryFolder), null);

                            //we're using a different directory for our repository to monitor.. go ahead and create it.
                            repository = new LocalRepository(m_ProductName, m_RepositoryFolder);
                        }

                        if (m_Configuration != null && m_Configuration.Enabled)
                        {
                            //We read the configuration to determine whether to pass in the application name or not.
                            m_Client = new RepositoryPublishClient(repository, m_ProductName,
                                (m_Configuration.SendAllApplications ? null : m_ApplicationName),
                                m_Configuration);
                        }

                        //finally!  We're good to go!
                        m_Initialized = true;
                    }
                    catch (Exception ex)
                    {
                        GC.KeepAlive(ex);
#if DEBUG
                        Log.Write(LogMessageSeverity.Error, "Unable to initialize repository publish engine", "While attempting to initialize the repository publish engine class in the publisher an exception was thrown:\r\n{0}", ex.Message);
                        Log.DebugBreak();
#endif
                    }

                    System.Threading.Monitor.PulseAll(m_SessionPublishThreadLock);
                }
            }
        }

        /// <summary>
        /// Makes sure that there is an active, valid session publishing thread
        /// </summary>
        /// <remarks>This is a thread-safe method that acquires the session publishing thread lock on its own, so
        /// the caller need not have that lock prior to calling this method.  If the session publishing thread has
        /// failed a new one will be started.</remarks>
        private void EnsureSessionPublishThreadIsValid()
        {
            //see if for some mystical reason our message dispatch thread failed.
            if (m_SessionPublishThreadFailed)
            {
                //OK, now - even though the thread was failed in our previous line, we now need to get the thread lock and check it again
                //to make sure it didn't get changed on another thread.
                lock (m_SessionPublishThreadLock)
                {
                    if (m_SessionPublishThreadFailed)
                    {
                        //we need to recreate the message thread
                        CreateMessageDispatchThread();
                    }

                    System.Threading.Monitor.PulseAll(m_SessionPublishThreadLock);
                }
            }
        }

        /// <summary>
        /// Get a multiprocess lock for the subscription engine.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private InterprocessLock GetLock(int timeout)
        {
            return InterprocessLockManager.Lock(this, m_RepositoryFolder, m_MultiprocessLockName, timeout);
        }

        private void SleepUntilNextCheck(TimeSpan checkInterval)
        {
            lock (m_SessionPublishThreadLock)
            {
                //time to sleep.
                DateTimeOffset nextCheckTime = m_LastCheck + checkInterval;
                while ((m_ForceCheck == false)
                    && (nextCheckTime > DateTimeOffset.Now))
                {
                    System.Threading.Monitor.Wait(m_SessionPublishThreadLock, 1000);
                }

                //to make sure we don't get into a fast loop outside, assume that if we're not sleeping
                //then we're going to do a subscription check, or whatever we're sleeping between checks.
                m_LastCheck = DateTimeOffset.Now;
                m_ForceCheck = false;

                System.Threading.Monitor.PulseAll(m_SessionPublishThreadLock);
            }
        }

        #endregion
    }
}
