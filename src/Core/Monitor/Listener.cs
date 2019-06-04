using System;
using System.Collections.Concurrent;
using System.Threading;
using Gibraltar.Messaging;
using Gibraltar.Monitor.Net;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// The central listener that manages the configuration of the individual listeners
    /// </summary>
    public static class Listener
    {
        private const string LogCategory = "Loupe.Monitor";

        private static readonly object m_MonitorThreadLock = new object();
        private static readonly object m_ListenerLock = new object();
        private static readonly object m_ConfigLock = new object();

        private static Publisher m_Publisher; //the active Agent publisher //LOCKED BY CONFIGLOCK
        private static AgentConfiguration m_AgentConfiguration; //the active Agent configuration //LOCKED BY CONFIGLOCK
        private static ListenerConfiguration m_Configuration; //the active listener configuration //LOCKED BY CONFIGLOCK
        private static bool m_PendingConfigChange; //LOCKED BY CONFIGLOCK
        private static bool m_Initialized; //LOCKED BY CONFIGLOCK; (update only)

        private static Thread m_MonitorThread; //LOCKED BY MONITORTHREADLOCK

        //our various listeners we're controlling
        private static bool m_ConsoleListenerRegistered; //LOCKED BY LISTENERLOCK
        private static CLRListener m_CLRListener; //LOCKED BY LISTENERLOCK
        private static GCEventListener m_GCEventListener; //LOCKED BY LISTENERLOCK

        private static readonly ConcurrentQueue<ILoupeMonitor> m_PendingMonitors;
        private static readonly ConcurrentDictionary<ILoupeMonitor, ILoupeMonitor> m_Monitors;

        private static MetricSampleInterval m_SamplingInterval = MetricSampleInterval.Minute;
        private static DateTimeOffset m_PollingStarted;
        private static bool m_EventsInitialized;
        private static bool m_SuppressAlerts;

        static Listener()
        {
            m_PendingMonitors = new ConcurrentQueue<ILoupeMonitor>();
            m_Monitors = new ConcurrentDictionary<ILoupeMonitor, ILoupeMonitor>();

            //create the background thread we need so we can respond to requests.
            CreateMonitorThread();
        }

        /// <summary>
        /// Apply the provided listener configuration
        /// </summary>
        /// <param name="agentConfiguration"></param>
        /// <param name="async"></param>
        /// <remarks>If calling initialization from a path that may have started with the trace listener,
        /// you must set suppressTraceInitialize to true to guarantee that the application will not deadlock
        /// or throw an unexpected exception.</remarks>
        public static void Initialize(Publisher publisher, AgentConfiguration agentConfiguration, bool async)
        {
            var listenerConfiguration = agentConfiguration.Listener;
            //get a configuration lock so we can update the configuration
            lock (m_ConfigLock)
            {
                m_Publisher = publisher;

                //and store the configuration; it's processed by the background thread.
                m_AgentConfiguration = agentConfiguration; // Set the top config before the local Listener config.
                m_Configuration = listenerConfiguration; // Monitor thread looks for this to be non-null before proceeding.
                m_PendingConfigChange = true;

                //wait for our events to initialize always on our background thread
                while (m_EventsInitialized == false)
                {
                    System.Threading.Monitor.Wait(m_ConfigLock, 16);
                }

                //and if we're doing a synchronous init then we even wait for the polled listeners.
                while ((async == false) && (m_PendingConfigChange))
                {
                    System.Threading.Monitor.Wait(m_ConfigLock, 16);
                }

                System.Threading.Monitor.PulseAll(m_ConfigLock);
            }
        }

        /// <summary>
        /// Indicates if the listeners have been initialized the first time yet.
        /// </summary>
        public static bool Initialized { get { return m_Initialized; } }

        /// <summary>
        /// Add the specified monitor to the listener, which should already be configured.
        /// </summary>
        /// <remarks>The monitor may be polled immediately after it is subscribed so it should be
        /// configured.</remarks>
        public static void Subscribe(ILoupeMonitor monitor)
        {
            //Add to our *pending* monitors collection so it will be background initialized
            m_PendingMonitors.Enqueue(monitor);
        }

        /// <summary>
        /// Remove the specified monitor from the listener
        /// </summary>
        /// <remarks>If the monitor object isn't subscribed no error is raised.  If the object is being polled
        /// there may be a short delay before it is unregistered.</remarks>
        /// <returns>True if the monitor was previously registered and has now been removed</returns>
        public static bool Unsubscribe(ILoupeMonitor monitor)
        {
            return m_Monitors.TryRemove(monitor, out var storedMonitor);
        }

        #region Private Properties and Methods

        private static void CreateMonitorThread()
        {
            lock (m_MonitorThreadLock)
            {
                m_MonitorThread = new Thread(MonitorThreadMain);
                m_MonitorThread.IsBackground = true;
                m_MonitorThread.Name = "Loupe Agent Monitor"; //name our thread so we can isolate it out of metrics and such
                m_MonitorThread.Start();

                System.Threading.Monitor.PulseAll(m_MonitorThreadLock);
            }
        }

        private static void MonitorThreadMain()
        {
            try
            {
                //First, we need to make sure we're initialized
                lock (m_ConfigLock)
                {
                    while (m_Configuration == null)
                    {
                        System.Threading.Monitor.Wait(m_ConfigLock, 1000);
                    }

                    System.Threading.Monitor.PulseAll(m_ConfigLock);
                }

                //KM: BUGBUG
                Subscribe(new ProcessMonitor());

                //we now have our first configuration - go for it.  This interacts with Config lock internally as it goes.
                UpdateMonitorConfiguration();

                //now we go into our wait process loop.
                m_PollingStarted = DateTimeOffset.Now;
                while (Log.IsSessionEnding == false) // Only do performance polling if we aren't shutting down.
                {
                    //mark the start of our cycle
                    var previousPollStart = DateTimeOffset.UtcNow; //this really should be UTC - we aren't storing it.

                    //poll our monitors
                    PerformPoll();

                    //now we need to wait for the timer to expire, but the user can update it periodically so we don't want to just
                    //assume it is unchanged for the entire wait duration.
                    DateTimeOffset targetNextPoll;
                    do
                    {
                        long waitInterval;

                        lock (m_ListenerLock)
                        {
                            waitInterval = GetTimerInterval(m_SamplingInterval);
                            System.Threading.Monitor.PulseAll(m_ListenerLock);
                        }

                        bool configUpdated;

                        //the target next poll is exactly as you'd expect - the number of milliseconds from the start of the previous poll.
                        int adjustedWaitInterval = (int)(previousPollStart.AddMilliseconds(waitInterval) - DateTimeOffset.Now).TotalMilliseconds;

                        //but enforce a floor so we don't go crazy cycling.
                        if (adjustedWaitInterval < 1000)
                        {
                            adjustedWaitInterval = 1000;
                        }

                        //set that to be our target next poll.
                        targetNextPoll = previousPollStart.AddMilliseconds(adjustedWaitInterval);

                        //process any monitors waiting to initialize
                        while (m_PendingMonitors.TryDequeue(out var newMonitor))
                        {
                            try
                            {
                                newMonitor.Initialize(m_Publisher);

                                //if we didn't fail then it must be ready to go.. add it to our working collection
                                m_Monitors.TryAdd(newMonitor, newMonitor);
                            }
                            catch (Exception ex)
                            {
                                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, true, LogCategory,
                                    "Unable to add monitor " + newMonitor.Caption + " to Loupe due to " + ex.GetBaseException().GetType(),
                                    "The monitor will be skipped.\r\n{0}", ex.Message);
                            }
                        }

                        //and sleep that amount since the user won't have a chance to change their mind.
                        configUpdated = WaitOnConfigUpdate(adjustedWaitInterval);

                        if (configUpdated)
                        {
                            //apply the update.
                            UpdateMonitorConfiguration();
                        }
                    } while (targetNextPoll > DateTimeOffset.UtcNow && Log.IsSessionEnding == false);
                }
            }
            catch (Exception exception)
            {
                GC.KeepAlive(exception);
#if DEBUG
                throw; // In debug builds throw it unhandled so we definitely find out about it.
#endif
            }
            finally
            {
                //we need to shut down all of the current and pending monitors so we don't leak memory or resources.
                foreach (var pendingMonitor in m_PendingMonitors)
                {
                    pendingMonitor.SafeDispose();
                }

                foreach (var loupeMonitor in m_Monitors.Values)
                {
                    loupeMonitor.SafeDispose();
                }
                m_Monitors.Clear();

                m_MonitorThread = null; // We're out of the loop and about to exit the thread, so clear the thread reference.
            }
        }

        /// <summary>
        /// wait up to the specified number of milliseconds for a configuration update.
        /// </summary>
        /// <param name="maxWaitInterval"></param>
        /// <returns></returns>
        private static bool WaitOnConfigUpdate(int maxWaitInterval)
        {
            bool configUpdated;

            DateTimeOffset waitEndTime = DateTimeOffset.UtcNow.AddMilliseconds(maxWaitInterval);

            lock (m_ConfigLock)
            {
                while ((waitEndTime > DateTimeOffset.UtcNow) //haven't waited as long as we're supposed to
                    && ((m_PendingConfigChange == false) || (m_Configuration == null))) //don't have a config change
                {
                    System.Threading.Monitor.Wait(m_ConfigLock, maxWaitInterval);
                }

                configUpdated = ((m_PendingConfigChange) && (m_Configuration != null));

                System.Threading.Monitor.PulseAll(m_ConfigLock);
            }

            return configUpdated;
        }

        private static void UpdateMonitorConfiguration()
        {
            ListenerConfiguration newConfiguration;

            Log.ThreadIsInitializer = true;  //so if we wander back into Log.Initialize we won't block.

            //get the lock while we grab the configuration so we know it isn't changed out under us
            lock (m_ConfigLock)
            {
                newConfiguration = m_Configuration;

                System.Threading.Monitor.PulseAll(m_ConfigLock);
            }

            //immediately reflect this change in our multithreaded event listeners
            InitializeConsoleListener(newConfiguration);
            InitializeCLRListener(newConfiguration);
            InitializeGCEventListener(newConfiguration);

            lock (m_ConfigLock)
            {
                m_EventsInitialized = true;
                m_PendingConfigChange = false;
                m_Initialized = true;

                System.Threading.Monitor.PulseAll(m_ConfigLock);
            }

            Log.ThreadIsInitializer = false;
        }

        private static void InitializeConsoleListener(ListenerConfiguration configuration)
        {
            try
            {
                lock (m_ListenerLock)
                {
                    //we can't register the console listener more than once, and it isn't designed for good register/unregister handling
                    //so we just have a simple bool to see if we did it.
                    if ((m_ConsoleListenerRegistered == false) && (configuration.EnableConsole))
                    {
                        ConsoleListener.RegisterConsoleIntercepter();
                        m_ConsoleListenerRegistered = true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, true, "Gibraltar.Agent",
                        "Unable to initialize Common Language Runtime Listener due to " + ex.GetBaseException().GetType().Name,
                        "While attempting to do a routine initialization / re-initialization of the console listener, an exception was raised: {0}", ex.Message);
            }
        }

        private static void InitializeCLRListener(ListenerConfiguration configuration)
        {
            try
            {
                lock (m_ListenerLock)
                {
                    if (m_CLRListener == null)
                    {
                        m_CLRListener = new CLRListener();
                    }

                    //it slices and dices what is allowed internally.
                    m_CLRListener.Initialize(configuration);

                    System.Threading.Monitor.PulseAll(m_ListenerLock);
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);

                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, true, "Gibraltar.Agent",
                 "Unable to initialize Common Language Runtime Listener due to " + ex.GetBaseException().GetType().Name,
                    "While attempting to do a routine initialization / re-initialization of the CLR listener, an exception was thrown: {0}", ex.Message);
            }
        }

        private static void InitializeGCEventListener(ListenerConfiguration configuration)
        {
            try
            {
                lock (m_ListenerLock)
                {
                    if (m_GCEventListener == null && (configuration.EnableGCEvents))
                    {
                        m_GCEventListener = new GCEventListener();
                    }

                    System.Threading.Monitor.PulseAll(m_ListenerLock);
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);

                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, true, "Gibraltar.Agent",
                        "Unable to initialize Common Language Runtime Listener due to " + ex.GetBaseException().GetType().Name,
                        "While attempting to do a routine initialization / re-initialization of the CLR listener, an exception was thrown: {0}", ex.Message);
            }
        }
        private static void PerformPoll()
        {
            if (m_Monitors != null)
            {
                foreach (var monitor in m_Monitors.Values)
                {
                    try
                    {
                        monitor.Poll();
                    }
                    catch (Exception ex)
                    {
                        if (ex is ThreadAbortException)
                            throw;

                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, true, "Gibraltar.Agent", 
                                "Unable to poll monitor due to " + ex.GetBaseException().GetType().Name, 
                                "While attempting to do a routine poll of a monitor, an exception was thrown: {0}", ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Determines the number of milliseconds in the provided interval for the timer object.
        /// </summary>
        /// <remarks>The values Default and Shortest are automatically treated as Minute by this function, effectively
        /// making once a minute the system default.</remarks>
        /// <param name="referenceInterval">The interval to calculate milliseconds for</param>
        /// <returns>The number of milliseconds between timer polls</returns>
        private static long GetTimerInterval(MetricSampleInterval referenceInterval)
        {
            //we have to convert the reference interval into the correct # of milliseconds
            long milliseconds = -1; //a safe choice because it means the timer will fire exactly once.

            switch (referenceInterval)
            {
                case MetricSampleInterval.Default:
                case MetricSampleInterval.Shortest:
                case MetricSampleInterval.Millisecond:
                    //we won't go below once a second
                    milliseconds = 1000;
                    break;
                case MetricSampleInterval.Minute:
                    milliseconds = 60000;   //sorta by definition
                    break;
                case MetricSampleInterval.Second:
                    milliseconds = 1000;   //sorta by definition
                    break;
                case MetricSampleInterval.Hour:
                    milliseconds = 3600000;
                    break;
                case MetricSampleInterval.Day:
                    milliseconds = 86400000; //get yer own calculator
                    break;
                case MetricSampleInterval.Week:
                    milliseconds = 604800000; //I mean who's going to do that, really. BTW:  Just barely a 32 bit number.
                    break;
                case MetricSampleInterval.Month:
                    milliseconds = DateTime.DaysInMonth(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month) * 86400000; //now I'm just being a smartass.
                    break;
                default:
                    break;
            }

            //before we return:  We poll artificially fast for the first few minutes and first hour.
            long secondsPolling = (long)(DateTimeOffset.Now - m_PollingStarted).TotalSeconds;
            if ((milliseconds > 5000) && (secondsPolling < 120))
            {
                milliseconds = 5000;
            }
            else if ((milliseconds > 15000) && (secondsPolling < 3600))
            {
                milliseconds = 15000;
            }

            return milliseconds;
        }

        #endregion
    }
}