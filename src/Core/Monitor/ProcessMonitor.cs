using System;
using System.Diagnostics;
using System.Collections.Generic;
using Gibraltar.Messaging;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Monitors the details of a process.
    /// </summary>
    /// <remarks>Creates metrics for each thread in the process and other aspects of the process.
    /// This can be the current process (if created without specifying a process ID) or an arbitrary
    /// running process that the user has access to (if the Process ID is specified)</remarks>
    public class ProcessMonitor : ILoupeMonitor
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
        private readonly Process m_MonitoredProcess;
        private readonly object m_Lock = new object();

        //our structured list of metrics we have for each thread we're monitoring
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
        public ProcessMonitor()
        {
            //use the current process ID
            m_MonitoredProcess = Process.GetCurrentProcess();
            m_ProcessId = m_MonitoredProcess.Id;
        }

        /// <summary>
        /// Create a new process monitor for the specified Process Id
        /// </summary>
        /// <remarks>If the specified process Id doesn't currently exist or isn't accessible to the current user, an exception will be thrown</remarks>
        /// <param name="processId">The process Id of the running process to connect to.</param>
        public ProcessMonitor(int processId)
        {
            m_ProcessId = processId;
            m_MonitoredProcess = Process.GetProcessById(m_ProcessId);
        }

        #region Public Properties and Methods

        /// <inheritdoc />
        public string Caption => "Loupe Process Monitor";

        /// <inheritdoc />
        public void Initialize(Publisher publisher)
        {
            Register();
        }

        /// <inheritdoc />
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
#if DEBUG
                Trace.TraceInformation("Received exception recording process counters, some counters will not be recorded. Exception: {0}", ex);
#else
                GC.KeepAlive(ex);
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

        /// <inheritdoc />
        public bool Equals(ILoupeMonitor monitor)
        {
            return ReferenceEquals(this, monitor);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ProcessMonitor)obj);
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
                // dispose our process object
                m_MonitoredProcess?.Dispose();
            }
        }

        #endregion

        #region Private Properties and Methods

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
            }
        }

        #endregion
    }
}