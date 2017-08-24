using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Gibraltar.Monitor.Net
{
    /// <summary>
    /// Monitors the details of a process.
    /// </summary>
    /// <remarks>Creates metrics for each thread in the process and other aspects of the process.
    /// This can be the current process (if created without specifying a process ID) or an arbitrary
    /// running process that the user has access to (if the Process ID is specified)</remarks>
    public class ProcessMonitor : IDisposable
    {
        /// <summary>
        /// The metric type for all process monitor metrics.
        /// </summary>
        public const string ProcessMetricType = "Process";

        /// <summary>
        /// The metric name for Nonpaged System Memory Size
        /// </summary>
        public const string CounterNonpagedSystemMemorySize = "Nonpaged System Memory Size";

        /// <summary>
        /// The metric name for Paged Memory Size
        /// </summary>
        public const string CounterPagedMemorySize = "Paged Memory Size";

        /// <summary>
        /// The metric name for Paged System Memory Size
        /// </summary>
        public const string CounterPagedSystemMemorySize = "Paged System Memory Size";

        /// <summary>
        /// The metric name for Peak Paged Memory Size
        /// </summary>
        public const string CounterPeakPagedMemorySize = "Peak Paged Memory Size";

        /// <summary>
        /// The metric name for Peak Virtual Memory Size
        /// </summary>
        public const string CounterPeakVirtualMemorySize = "Peak Virtual Memory Size";

        /// <summary>
        /// The metric name for Peak Working Set
        /// </summary>
        public const string CounterPeakWorkingSet = "Peak Working Set";

        /// <summary>
        /// The metric name for Private Memory Size
        /// </summary>
        public const string CounterPrivateMemorySize = "Private Memory Size";

        /// <summary>
        /// The metric name for Privileged Processor Time
        /// </summary>
        public const string CounterPrivilegedProcessorTime = "Privileged Processor Time";

        /// <summary>
        /// The metric name for Total Processor Time
        /// </summary>
        public const string CounterTotalProcessorTime = "Total Processor Time";

        /// <summary>
        /// The metric name for User Processor Time
        /// </summary>
        public const string CounterUserProcessorTime = "User Processor Time";

        /// <summary>
        /// The metric name for Virtual Memory Size
        /// </summary>
        public const string CounterVirtualMemorySize = "Virtual Memory Size";

        /// <summary>
        /// The metric name for Working Set
        /// </summary>
        public const string CounterWorkingSet = "Working Set";

        private readonly List<CustomSampledMetricDefinition> m_MetricDefinitions = new List<CustomSampledMetricDefinition>();
        private readonly int m_ProcessId;
        private readonly object m_Lock = new object();
        private readonly bool m_MonitorThreads;
        private readonly Process m_MonitoredProcess;

        //our structured list of metrics we have for each thread we're monitoring
        private readonly Dictionary<int, List<CustomSampledMetric>> m_MonitoredThreads = new Dictionary<int, List<CustomSampledMetric>>();
        private CustomSampledMetric m_ProcessPercentProcessorTime;
        private CustomSampledMetric m_ProcessPercentUserProcessorTime;
        private CustomSampledMetric m_ProcessPercentPrivilegedProcessorTime;
        private CustomSampledMetric m_ProcessNonpagedSystemMemorySize;
        private CustomSampledMetric m_ProcessPagedMemorySize;
        private CustomSampledMetric m_ProcessPagedSystemMemorySize;
        private CustomSampledMetric m_ProcessPeakPagedMemorySize;
        private CustomSampledMetric m_ProcessPeakVirtualMemorySize;
        private CustomSampledMetric m_ProcessPeakWorkingSet;
        private CustomSampledMetric m_ProcessPrivateMemorySize;
        private CustomSampledMetric m_ProcessVirtualMemorySize;
        private CustomSampledMetric m_ProcessWorkingSet;

        /// <summary>
        /// Create a new process monitor for the current process
        /// </summary>
        /// <param name="monitorThreads">If true, metrics on individual threads in the process will be gathered.</param>
        public ProcessMonitor(bool monitorThreads)
        {
            //use the current process ID
            m_ProcessId = Process.GetCurrentProcess().Id;
            m_MonitoredProcess = Process.GetProcessById(m_ProcessId); 
            m_MonitorThreads = monitorThreads;

            Register();
        }

        /// <summary>
        /// Create a new process monitor for the specified Process Id
        /// </summary>
        /// <remarks>If the specified process Id doesn't currently exist or isn't accessible to the current user, an exception will be thrown</remarks>
        /// <param name="monitorThreads">If true, metrics on individual threads in the process will be gathered.</param>
        /// <param name="processId">The process Id of the running process to connect to.</param>
        public ProcessMonitor(bool monitorThreads, int processId)
        {
            m_ProcessId = processId;
            m_MonitoredProcess = Process.GetProcessById(m_ProcessId);
            m_MonitorThreads = monitorThreads;

            Register();
        }

        #region Public Properties and Methods

        /// <summary>
        /// Record information about the monitored process immediately.
        /// </summary>
        /// <remarks>This method is thread-safe, and may take some time to run.</remarks>
        public void Poll()
        {
            List<MetricSample> newSamples = new List<MetricSample>();

            if (m_MonitoredProcess.HasExited)
            {
                //nothing we can do.
                return;
            }

            //refresh our process so we get the latest metrics
            m_MonitoredProcess.Refresh();

            //grab our process-specific information.  We always grab all this stuff.
            try
            {
                TimeSpan executionTime = DateTime.Now - m_MonitoredProcess.StartTime;
                newSamples.Add(m_ProcessPercentProcessorTime.CreateSample(m_MonitoredProcess.TotalProcessorTime.Ticks, executionTime.Ticks));
                newSamples.Add(m_ProcessPercentUserProcessorTime.CreateSample(m_MonitoredProcess.UserProcessorTime.Ticks, executionTime.Ticks));
                newSamples.Add(m_ProcessPercentPrivilegedProcessorTime.CreateSample(m_MonitoredProcess.PrivilegedProcessorTime.Ticks, executionTime.Ticks));
                newSamples.Add(m_ProcessNonpagedSystemMemorySize.CreateSample(m_MonitoredProcess.NonpagedSystemMemorySize64));
                newSamples.Add(m_ProcessPagedMemorySize.CreateSample(m_MonitoredProcess.PagedMemorySize64));
                newSamples.Add(m_ProcessPagedSystemMemorySize.CreateSample(m_MonitoredProcess.PagedSystemMemorySize64));
                newSamples.Add(m_ProcessPeakPagedMemorySize.CreateSample(m_MonitoredProcess.PeakPagedMemorySize64));
                newSamples.Add(m_ProcessPeakVirtualMemorySize.CreateSample(m_MonitoredProcess.PeakVirtualMemorySize64));
                newSamples.Add(m_ProcessPeakWorkingSet.CreateSample(m_MonitoredProcess.PeakWorkingSet64));
                newSamples.Add(m_ProcessPrivateMemorySize.CreateSample(m_MonitoredProcess.PrivateMemorySize64));
                newSamples.Add(m_ProcessVirtualMemorySize.CreateSample(m_MonitoredProcess.VirtualMemorySize64));
                newSamples.Add(m_ProcessWorkingSet.CreateSample(m_MonitoredProcess.WorkingSet64));

            }
            catch(Exception ex)
            {
                //we really should never get an exception here, unless we're monitoring a remote process which has exited.
                GC.KeepAlive(ex);
            }

            //now do the threads, if we're monitoring threads
            if (m_MonitorThreads)
            {
#if MONITOR_OS_THREADS
                Dictionary<int, List<CustomSampledMetric>> monitoredThreads;
                ProcessThreadCollection processThreads = m_MonitoredProcess.Threads;

                lock (m_Lock)
                {
                    //make sure all of our threads are registered
                    EnsureThreadsRegistered(m_MonitoredProcess.Threads);

                    //we have to make a copy of our thread collection because it may change while we're processing
                    monitoredThreads = new Dictionary<int, List<CustomSampledMetric>>(m_MonitoredThreads);
                }

                //iterate through each of the threads, recording metrics for each.
                foreach (KeyValuePair<int, List<CustomSampledMetric>> curThreadKeyPair in monitoredThreads)
                {
                    try
                    {
                        ProcessThread monitoredThread = null;

                        //get the ProcessThread object for this thread
                        foreach (ProcessThread curProcessThread in processThreads)
                        {
                            //is this our ID?
                            if (curProcessThread.Id == curThreadKeyPair.Key)
                            {
                                //bingo! but make sure that it isn't terminated.
                                if (curProcessThread.ThreadState != ThreadState.Terminated)
                                {
                                    //save off this process thread object so it's available outside our loop
                                    monitoredThread = curProcessThread;
                                }
                                break;
                            }
                        }

                        //if we got a thread object, record the metrics
                        if (monitoredThread == null)
                        {
                            //no thread object, remove the metrics
                            UnregisterThread(curThreadKeyPair.Key);
                        }
                        else
                        {
                            //many of our metrics are based on the runtime of the thread - lets figure that out.
                            TimeSpan executionTime = DateTime.Now - monitoredThread.StartTime;

                            //iterate through each metric registered so we can record the current value.
                            foreach (CustomSampledMetric curMetric in curThreadKeyPair.Value)
                            {
                                TimeSpan curTimeSpanSample;
                                switch (curMetric.CounterName)
                                {
                                    case "Total Processor Time":
                                        curTimeSpanSample = monitoredThread.TotalProcessorTime;
                                        break;
                                    case "User Processor Time":
                                        curTimeSpanSample = monitoredThread.UserProcessorTime;
                                        break;
                                    case "Privileged Processor Time":
                                        curTimeSpanSample = monitoredThread.PrivilegedProcessorTime;
                                        break;
                                    default:
#if DEBUG
                                        Trace.TraceError("Unexpected thread metric: " + curMetric.CounterName);
#endif
                                        curTimeSpanSample = new TimeSpan(0); //we have to initialize it to make sure all paths initialize it
                                        break;
                                }

                                //create a new sample packet for this metric.  We just create it - it automatically adds itself to the metric.
                                newSamples.Add(curMetric.CreateSample(curTimeSpanSample.Ticks, executionTime.Ticks));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //most commonly, an exception is because the thread has now exited and we have to account for that.
#if DEBUG
                        Trace.TraceInformation("Received exception, will stop monitoring thread. Exception: {0}", ex);
#else
                        GC.KeepAlive(ex);
#endif
                        UnregisterThread(curThreadKeyPair.Key);
                    }
                }
#endif
            }

            //Now log all of the samples we got
            Log.Write(newSamples);
        }

        /// <summary>
        /// The process object being monitored
        /// </summary>
        public Process MonitoredProcess
        {
            get { return m_MonitoredProcess; }
        }

        /// <summary>
        /// The unique numeric Id of the process being monitored.
        /// </summary>
        public int ProcessId
        {
            get { return m_ProcessId; }
        }

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // and suppress our GC
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (releaseManaged)
            {
                // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                // Other objects may be referenced in this case

                // dispose our process object
                m_MonitoredProcess.Dispose();
            }
            // Free native resources here (alloc's, etc)
            // May be called from within the finalizer, so don't reference other objects here
        }

        #endregion

        #region Private Properties and Methods

#if MONITOR_OS_THREADS
        /// <summary>
        /// Updates the thread registration to reflect the current state.  (Must be called inside the lock.)
        /// </summary>
        /// <param name="currentThreads"></param>
        private void EnsureThreadsRegistered(ProcessThreadCollection currentThreads)
        {
            List<ProcessThread> missingThreads = new List<ProcessThread>();
            List<int> victimThreads = new List<int>();

            //See if we are missing any threads
            foreach (ProcessThread curThread in currentThreads)
            {
                //we only want to add monitoring to threads that are active
                if (curThread.ThreadState != ThreadState.Terminated)
                {
                    //do we already have a metric for it?
                    if (m_MonitoredThreads.ContainsKey(curThread.Id) == false)
                    {
                        //we don't have this thread - we need to register it
                        missingThreads.Add(curThread);
                    }
                }
            }

            //Now see if we have any extra threads - perhaps a few threads have disappeared.
            //We do a quick cheat:  Check the number of registered threads and the number
            //we are adding and see if it's the same as the number of current threads.
            if ((m_MonitoredThreads.Count + missingThreads.Count) != (currentThreads.Count))
            {
                //we have one or or gratuitous threads.
                foreach (KeyValuePair<int, List<CustomSampledMetric>> curThreadKeyPair in m_MonitoredThreads)
                {
                    bool foundThread = false;

                    //get the thread object for this tread
                    foreach (ProcessThread curProcessThread in currentThreads)
                    {
                        if (curProcessThread.Id == curThreadKeyPair.Key)
                        {
                            //we found this thread, now is it terminated?
                            if (curProcessThread.ThreadState != ThreadState.Terminated)
                            {
                                foundThread = true;
                            }
                            break;
                        }
                    }

                    //if the thread is marked as terminated or is no longer in the collection we need to remove it.
                    if (foundThread == false)
                    {
                        //doesn't exist any more, we're going to need to remove it.
                        victimThreads.Add(curThreadKeyPair.Key);
                    }
                }
            }

            //Register new threads, kill old threads.
            foreach (ProcessThread curThread in missingThreads)
            {
                RegisterThread(curThread);
            }

            foreach (int curThreadId in victimThreads)
            {
                UnregisterThread(curThreadId);
            }
        }
#endif

        /// <summary>
        /// Register all of the individual metrics we use
        /// </summary>
        private void Register()
        {
            lock (m_Lock)
            {
                //register our process-wide metrics
                CustomSampledMetricDefinition curSampledMetricDefinition;

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Processor", CounterTotalProcessorTime, MetricSampleType.TotalFraction);
                curSampledMetricDefinition.Description = "The percentage of processor capacity used by the process.";
                curSampledMetricDefinition.UnitCaption = "%";
                m_ProcessPercentProcessorTime = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Processor", CounterUserProcessorTime, MetricSampleType.TotalFraction);
                curSampledMetricDefinition.Description = "The percentage of processor capacity used by the process for non-privileged tasks.";
                curSampledMetricDefinition.UnitCaption = "%";
                m_ProcessPercentUserProcessorTime = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Processor", CounterPrivilegedProcessorTime, MetricSampleType.TotalFraction);
                curSampledMetricDefinition.Description = "The percentage of processor capacity used by the process for privileged tasks.";
                curSampledMetricDefinition.UnitCaption = "%";
                m_ProcessPercentPrivilegedProcessorTime = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterNonpagedSystemMemorySize, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The amount of nonpaged system memory allocated for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessNonpagedSystemMemorySize = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterPagedMemorySize, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The amount of paged memory allocated for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessPagedMemorySize = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterPagedSystemMemorySize, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The amount of pageable system memory allocated for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessPagedSystemMemorySize = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterPeakPagedMemorySize, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The maximum amount of memory in the virtual memory paging file for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessPeakPagedMemorySize = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterPeakVirtualMemorySize, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The maximum amount of virtual memory used for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessPeakVirtualMemorySize = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterPeakWorkingSet, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The maximum amount of physical memory used for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessPeakWorkingSet = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterPrivateMemorySize, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The amount of private memory allocated for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessPrivateMemorySize = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterVirtualMemorySize, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The amount of virtual memory allocated for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessVirtualMemorySize = curSampledMetricDefinition.Metrics.Add(null);

                curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Memory", CounterWorkingSet, MetricSampleType.RawCount);
                curSampledMetricDefinition.Description = "The amount of physical memory allocated for the process.";
                curSampledMetricDefinition.UnitCaption = "Bytes";
                m_ProcessWorkingSet = curSampledMetricDefinition.Metrics.Add(null);

                //register the different metrics we use for threads
                if (m_MonitorThreads)
                {
#if MONITOR_OS_THREADS
                    curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Thread", CounterTotalProcessorTime, MetricSampleType.TotalFraction);
                    curSampledMetricDefinition.Description = "The percentage of processor capacity used by this thread.";
                    curSampledMetricDefinition.UnitCaption = "%";
                    m_MetricDefinitions.Add(curSampledMetricDefinition);

                    curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Thread", CounterUserProcessorTime, MetricSampleType.TotalFraction);
                    curSampledMetricDefinition.Description = "The percentage of processor capacity used by the process for non-privileged tasks.";
                    curSampledMetricDefinition.UnitCaption = "%";
                    m_MetricDefinitions.Add(curSampledMetricDefinition);

                    curSampledMetricDefinition = new CustomSampledMetricDefinition(Log.Metrics, ProcessMetricType, "Process.Thread", CounterPrivilegedProcessorTime, MetricSampleType.TotalFraction);
                    curSampledMetricDefinition.Description = "The percentage of processor capacity used by the process for privileged tasks.";
                    curSampledMetricDefinition.UnitCaption = "%";
                    m_MetricDefinitions.Add(curSampledMetricDefinition);

                    //register all of our threads.
                    EnsureThreadsRegistered(m_MonitoredProcess.Threads);
#endif
                }
            }
        }

#if MONITOR_OS_THREADS
        /// <summary>
        /// Registers a new thread, creating all of the necessary metrics.
        /// </summary>
        /// <param name="newThread">The process thread object to add</param>
        private void RegisterThread(ProcessThread newThread)
        {
            lock (m_Lock)
            {
                //Set up our thread data structure so we can hold all of the individual counters.
                List<CustomSampledMetric> threadMetrics = new List<CustomSampledMetric>();

                //now lets create each of the custom counters we need
                //BUGBUG:  This sucks; too many magic values.
                foreach (CustomSampledMetricDefinition curMetricDefinition in m_MetricDefinitions)
                {
                    //add the specific counter for this definition
                    threadMetrics.Add(curMetricDefinition.Metrics.Add(newThread.Id.ToString(CultureInfo.InvariantCulture)));
                }

                //add a trace log message that this was registered
                Trace.TraceInformation("New Thread with id {0} is now being monitored.", newThread.Id);

                //and now that it's set up, add it to our set of monitored threads
                m_MonitoredThreads.Add(newThread.Id, threadMetrics);
            }
        }

        /// <summary>
        /// Unregisters a thread, ending polling on relevant metrics.
        /// </summary>
        /// <param name="victimThreadId">The ThreadId of the thread to unregister.</param>
        private void UnregisterThread(int victimThreadId)
        {
            lock (m_Lock)
            {
                //Remove the victim thread object.  We don't actually remove the metric - you never do, you just stop
                //polling the metrics again.
                if (m_MonitoredThreads.ContainsKey(victimThreadId))
                {
                    m_MonitoredThreads.Remove(victimThreadId);

                    Trace.TraceInformation("Thread with id {0} will no longer be monitored because it has been terminated.", victimThreadId);
                }
            }
        }
#endif

        #endregion

    }
}