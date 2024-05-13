
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Gibraltar;
using Gibraltar.Messaging;
using Gibraltar.Monitor;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using Microsoft.Extensions.Options;

namespace Loupe.Agent.PerformanceCounters
{
    /// <summary>
    /// Registers and automatically processes standard performance counters
    /// </summary>
    /// <remarks>
    /// Registers a set of performance counters to be monitored and automatically polls them every interval
    /// of time to provide a clear picture of the system's performance while the application was running.
    /// </remarks>
    public class PerformanceMonitor : ILoupeMonitor
    {
        private readonly object m_Lock = new object();

        private readonly PerformanceConfiguration m_Configuration;

        private PerfCounterCollection m_DiskCounters;
        private PerfCounterCollection m_NetworkCounters;
        private PerfCounterCollection m_SystemCounters;
        private PerfCounterCollection m_MemoryCounters;

        private bool m_Initialized; //we use this because we initialize asynchronously.
        private bool m_Enabled;
        private bool m_BusyPolling;  //used so we know if we're in the middle of a poll.

        private bool m_EnableDiskCounters;
        private bool m_EnableNetworkCounters;
        private bool m_EnableSystemCounters;
        private bool m_EnableMemoryCounters;

        /// <summary>
        /// Create a performance monitor with the default configuration
        /// </summary>
        public PerformanceMonitor()
            : this(new PerformanceConfiguration())
        {

        }

        /// <summary>
        /// Create a performance monitor with the specified configuration
        /// </summary>
        /// <param name="configuration">The configuration options for the performance monitor</param>
        public PerformanceMonitor(PerformanceConfiguration configuration)
        {
            m_Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Create a PerformanceMonitor from the DI Container
        /// </summary>
        /// <param name="agentConfiguration"></param>
        public PerformanceMonitor(IOptions<AgentConfiguration> agentConfiguration)
        {
            m_Configuration = agentConfiguration.Value?.Performance ?? new PerformanceConfiguration();
        }

        string ILoupeMonitor.Caption => "Performance Monitor";

        /// <summary>
        /// Initialize / reinitialize the performance monitor with the provided configuration
        /// </summary>
        public void Initialize(Publisher publisher)
        {
#if DEBUG
            //don't log during initialize in a lock, we will probably deadlock.
            Log.Write(LogMessageSeverity.Information, "Gibraltar.Agent", "Starting asynchronous performance monitoring initialization", null);
#endif

            //we want to make sure anything that might want to mess with our data will wait until we're done initializing
            lock (m_Lock)
            {
                // we only can do performance counters on windows, so check that first.
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                if (isWindows)
                {
                    m_Enabled = true;

                    if ((m_Configuration.EnableDiskMetrics) && (m_DiskCounters == null))
                        InitializeDiskCounters();

                    if ((m_Configuration.EnableNetworkMetrics) && (m_NetworkCounters == null))
                        InitializeNetworkCounters();

                    if ((m_Configuration.EnableSystemMetrics) && (m_SystemCounters == null))
                        InitializeSystemCounters();

                    if ((m_Configuration.EnableMemoryMetrics) && (m_MemoryCounters == null))
                        InitializeMemoryCounters();

                    //copy the configuration to ensure invariance...
                    m_EnableDiskCounters = m_Configuration.EnableDiskMetrics;
                    m_EnableMemoryCounters = m_Configuration.EnableMemoryMetrics;
                    m_EnableNetworkCounters = m_Configuration.EnableNetworkMetrics;
                    m_EnableSystemCounters = m_Configuration.EnableSystemMetrics;
                }
                else
                {
                    m_Enabled = false;
                    m_EnableDiskCounters = false;
                    m_EnableMemoryCounters = false;
                    m_EnableNetworkCounters = false;
                    m_EnableSystemCounters = false;
                }

                //and now we're done initializing
                m_Initialized = true;

                System.Threading.Monitor.PulseAll(m_Lock);
            }

#if DEBUG
            //don't log during initialize in a lock, we will probably deadlock.
            Log.Write(LogMessageSeverity.Information, "Gibraltar.Agent", "Completed asynchronous performance monitoring initialization", null);
#endif
        }

        /// <summary>
        /// Poll all the actively configured counters.
        /// </summary>
        public void Poll()
        {
            //we have to be initialized to poll, and we should ignore attempts to poll
            //while still IN a poll.

            if (m_Initialized == false)
            {
                Log.Write(LogMessageSeverity.Warning, "Gibraltar.Agent", "Skipping counter poll because object is not yet initialized.",
                          "This shouldn't happen unless the Performance Monitor is being misused.");
            }
            else if (m_Enabled == false)
            {
                return;
            }
            else if (m_BusyPolling)
            {
                Log.Write(LogMessageSeverity.Information, "Gibraltar.Agent", "Skipping counter poll because we are still polling.",
                          "If this situation persists, slow down the requested poll rate because it is too fast for the system.");
            }
            else
            {
#if DEBUG_PERFMON
                Log.Write(LogMessageSeverity.Verbose, "Gibraltar.Agent", "Starting counter poll - requesting lock.", null);
#endif
                //now go get our lock and poll the counters
                lock (m_Lock)
                {
                    try
                    {
                        m_BusyPolling = true;

#if DEBUG_PERFMON
                        Log.Write(LogMessageSeverity.Verbose, "Gibraltar.Agent", "Polling performance counters.", null);
#endif

                        if ((m_SystemCounters != null) && (m_EnableSystemCounters))
                            m_SystemCounters.WriteSamples();

                        if ((m_MemoryCounters != null) && (m_EnableMemoryCounters))
                            m_MemoryCounters.WriteSamples();

                        if ((m_DiskCounters != null) && (m_EnableDiskCounters))
                            m_DiskCounters.WriteSamples();

                        if ((m_NetworkCounters != null) && (m_EnableNetworkCounters))
                            m_NetworkCounters.WriteSamples();

#if DEBUG_PERFMON
                        Log.Write(LogMessageSeverity.Verbose, "Gibraltar.Agent", "Completed performance counter poll.", null);
#endif
                    }
                    catch (Exception exception)
                    {
                        if (!Log.SilentMode) Log.Write(LogMessageSeverity.Information, LogWriteMode.Queued, exception, "Gibraltar.Agent",
                                  "Failed to poll performance counters due to an exception.",
                                  "Exception type: {0}\r\nException message: {1}\r\n",
                                  exception.GetType().FullName, exception.Message);
                    }
                    finally
                    {
                        //we are no longer in a poll. 
                        m_BusyPolling = false;
                    }

                    System.Threading.Monitor.PulseAll(m_Lock);
                }
            }
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

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        void IDisposable.Dispose()
        {
            //we don't really have anything to dispose.
        }


        private void InitializeDiskCounters()
        {
            //create a performance counter group for these guys
            m_DiskCounters = new PerfCounterCollection("Disk Counters", "System-wide disk performance counters (not specific to the process being monitored)");

            //register our favorite performance counters with the monitoring system
            try
            {
                //add in a monitor for each physical disk
                PerformanceCounterCategory logicalDiskCounters = new PerformanceCounterCategory("LogicalDisk");
                string[] driveNames = logicalDiskCounters.GetInstanceNames();

                //but before we ad the counters figure out what names are good.
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                Dictionary<string, DriveInfo> safeDrives = new Dictionary<string, DriveInfo>(allDrives.Length, StringComparer.OrdinalIgnoreCase);
                foreach (DriveInfo currentDrive in allDrives)
                {
                    //we don't want to stumble into an odd machine-specific problem accessing the 
                    //properties of a drive that's not ready or something.
                    try
                    {
                        if ((currentDrive.DriveType == DriveType.Fixed) && (currentDrive.IsReady))
                        {
                            //clean up the drive name
                            string cleanName = currentDrive.Name;
                            int colonIndex = cleanName.IndexOf(":");
                            if (colonIndex > -1)
                            {
                                //we just want up through the colon
                                cleanName = cleanName.Substring(0, colonIndex + 1);
                            }
                            safeDrives.Add(cleanName, currentDrive);
                        }
                    }
                    catch
                    {
                    }
                }

                foreach (string driveName in driveNames)
                {
                    //is it a valid drive to use?
                    
                    //we need to get rid of the leading number and space which we don't want in the label.
                    string cleanName = driveName.TrimStart(new [] { ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'});

                    if (safeDrives.ContainsKey(cleanName))
                    {
                        m_DiskCounters.Add("LogicalDisk", "% Disk Time", driveName);
                        m_DiskCounters.Add("LogicalDisk", "Avg. Disk Queue Length", driveName);
                        m_DiskCounters.Add("LogicalDisk", "Avg. Disk sec/Transfer", driveName);
                        m_DiskCounters.Add("LogicalDisk", "Disk Transfers/sec", driveName);
                        m_DiskCounters.Add("LogicalDisk", "Disk Reads/sec", driveName);
                        m_DiskCounters.Add("LogicalDisk", "Disk Writes/sec", driveName);
                        m_DiskCounters.Add("LogicalDisk", "Disk Bytes/sec", driveName);
                        m_DiskCounters.Add("LogicalDisk", "Free Megabytes", driveName);
                        m_DiskCounters.Add("LogicalDisk", "% Free Space", driveName);
                    }
                }
            }
            catch (Exception exception)
            {
                GC.KeepAlive(exception); //some warning prevention...
                Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, exception, "Gibraltar.Agent",
                          "Unable to record performance information for disk counters",
                          "Specific exception: {0}\r\nException message: {1}",
                          exception.GetType().FullName, exception.Message);
            }
        }

        private void InitializeNetworkCounters()
        {
            //create a performance counter group for these guys
            m_NetworkCounters = new PerfCounterCollection("Network Counters", "System-wide network performance counters (not specific to the process being monitored)");

            //register our favorite performance counters with the monitoring system
            try
            {
                //add in a monitor for each network interface
                PerformanceCounterCategory networkCounters = new PerformanceCounterCategory("Network Interface");
                string[] networkInterfaceNames = networkCounters.GetInstanceNames();

                foreach (string interfaceName in networkInterfaceNames)
                {
                    //eliminate the loopback interface, we never care about that.
                    if (interfaceName != "MS TCP Loopback interface" && interfaceName != "lo")
                    {
                        m_NetworkCounters.Add("Network Interface", "Bytes Received/sec", interfaceName);
                        m_NetworkCounters.Add("Network Interface", "Bytes Sent/sec", interfaceName);
                        m_NetworkCounters.Add("Network Interface", "Current Bandwidth", interfaceName);
                        m_NetworkCounters.Add("Network Interface", "Packets Received Errors", interfaceName);
                        m_NetworkCounters.Add("Network Interface", "Packets Outbound Errors", interfaceName);
                    }
                }
            }
            catch (Exception exception)
            {
                GC.KeepAlive(exception); //some warning prevention...
                Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, exception, "Gibraltar.Agent",
                          "Unable to record performance information for network counters", 
                          "Specific exception: {0}\r\nException message: {1}",
                          exception.GetType().FullName, exception.Message);
            }
        }

        private void InitializeMemoryCounters()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //create a performance counter group for these guys
                m_MemoryCounters = new PerfCounterCollection(".NET Memory Counters",
                    "Detailed memory utilization counters specific to .NET");

                //register our favorite performance counters with the monitoring system (These should all exist on Mono now, too)
                try
                {
                    m_MemoryCounters.Add(".NET CLR Memory", "# GC Handles", PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "# Bytes in all Heaps",
                        PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "# Gen 0 Collections",
                        PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "# Gen 1 Collections",
                        PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "# Gen 2 Collections",
                        PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "Large Object Heap Size",
                        PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "Gen 0 heap size", PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "Gen 1 heap size", PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "Gen 2 heap size", PerfCounterInstanceAlias.CurrentProcess);
                    m_MemoryCounters.Add(".NET CLR Memory", "% Time in GC", PerfCounterInstanceAlias.CurrentProcess);
                }
                catch (Exception exception)
                {
                    GC.KeepAlive(exception); //some warning prevention...
                    Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, exception, "Gibraltar.Agent",
                        "Unable to record performance information for .NET memory counters",
                        "Specific exception: {0}\r\nException message: {1}",
                        exception.GetType().FullName, exception.Message);
                }
            }            
        }

        private void InitializeSystemCounters()
        {
            //create a performance counter group for these guys
            m_SystemCounters = new PerfCounterCollection("System Counters", "System-wide performance counters (not specific to the process being monitored)");

            //register our favorite performance counters with the monitoring system
            try
            {
                m_SystemCounters.Add("System", "Processor Queue Length");
                m_SystemCounters.Add("Processor", "% Processor Time", "_Total");
                m_SystemCounters.Add("Memory", "Committed Bytes");
                m_SystemCounters.Add("Memory", "Available Bytes");
                m_SystemCounters.Add("Memory", "Pages/sec");
            }
            catch (Exception exception)
            {
                GC.KeepAlive(exception); //some warning prevention...
                Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, exception, "Gibraltar.Agent",
                          "Unable to record performance information for system counters",
                          "Specific exception: {0}\r\nException message: {1}",
                          exception.GetType().FullName, exception.Message);
            }
        }
    }
}