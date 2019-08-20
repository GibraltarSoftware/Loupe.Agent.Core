using System;
using System.Diagnostics;
using Loupe.Core.Monitor;
using Loupe.Agent.PerformanceCounters.Serialization;
using Loupe.Core.IO.Serialization;
using Loupe.Core.Metrics;
using Loupe.Extensibility.Data;
using Log = Loupe.Core.Log;

namespace Loupe.Agent.PerformanceCounters
{
    /// <summary>
    /// A single tracked metric based on a Windows Performance Counter.
    /// </summary>
    /// <remarks>
    /// Each performance counter that is tracked for a session has its own performance counter metric associated with it.
    /// Use the Calculate method to generate display-ready value sets from the samples recorded for this metric.
    /// Metrics with the same category and counter can be directly compared.
    /// </remarks>
    public sealed class PerfCounterMetric : SampledMetric, IComparable<PerfCounterMetric>, IEquatable<PerfCounterMetric>
    {
        private readonly PerfCounterInstanceAlias m_InstanceAlias;

        private PerfCounterPollingState m_PollingState;

        private static volatile int s_OurPid = -1; //our process id
        private static readonly object s_ProcessNameLock = new object();
        private static string s_OurProcessInstanceNamePrefix; //used to help speed up / make more reliable our search for our instance name.
        private static string s_OurProcessInstanceName;
        private static DateTimeOffset s_OurProcessInstanceNameExpirationDt;

        /// <summary>
        /// Create a new performance counter metric object from the provided windows performance counter
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The metric definition for the provided windows performance counter</param>
        /// <param name="counter">The windows performance counter to add as a metric</param>
        public PerfCounterMetric(PerfCounterMetricDefinition definition, PerformanceCounter counter)
            : base(definition, new PerfCounterMetricPacket(definition.Packet, counter))
        {
            m_InstanceAlias = PerfCounterInstanceAlias.None;
            m_PollingState = PerfCounterPollingState.Inactive;
        }

        /// <summary>
        /// Create a new performance counter metric object from the provided windows performance counter
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The metric definition for the provided windows performance counter</param>
        /// <param name="counter">The windows performance counter to add as a metric</param>
        /// <param name="alias">An alias to use to determine the instance name instead of the instance of the supplied counter.</param>
        public PerfCounterMetric(PerfCounterMetricDefinition definition, PerformanceCounter counter, PerfCounterInstanceAlias alias)
            : base(definition, new PerfCounterMetricPacket(definition.Packet, counter))
        {
            m_InstanceAlias = alias;
            m_PollingState = PerfCounterPollingState.Inactive;
        }

        /// <summary>
        /// Create a new performance counter metric object from the provided raw data packet
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The object that defines this metric</param>
        /// <param name="packet">The raw data packet</param>
        internal PerfCounterMetric(PerfCounterMetricDefinition definition, PerfCounterMetricPacket packet)
            : base(definition, packet)
        {
            m_InstanceAlias = PerfCounterInstanceAlias.None;
            m_PollingState = PerfCounterPollingState.Inactive;
        }

        #region Public Properties and Methods


        /// <summary>Creates a new performance counter metric instance or returns an existing one from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Performance Counter Metric (or a derived class) an exception will be thrown.</remarks>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <returns>The Performance Counter Metric object for the specified instance.</returns>
        public static PerfCounterMetric AddOrGet(string categoryName, string counterName)
        {
            return AddOrGet(categoryName, counterName, string.Empty);
        }

        /// <summary>Creates a new performance counter metric instance or returns an existing one from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Performance Counter Metric (or a derived class) an exception will be thrown.</remarks>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="alias">An alias to use to determine the instance name instead of the instance of the supplied counter.</param>
        /// <returns>The Performance Counter Metric object for the specified instance.</returns>
        public static PerfCounterMetric AddOrGet(string categoryName, string counterName, PerfCounterInstanceAlias alias)
        {
            //make sure we have all of the variables we require
            if (string.IsNullOrEmpty(categoryName))
            {
                throw new ArgumentNullException(nameof(categoryName), "No category name was provided and one is required.");
            }

            if (string.IsNullOrEmpty(counterName))
            {
                throw new ArgumentNullException(nameof(counterName), "No counter name was provided and one is required.");
            }

            //Go ahead and make the windows counter
            string instanceName = GetInstanceName(alias);
            PerformanceCounter newPerformanceCounter = GetPerformanceCounter(categoryName, counterName, instanceName);

            //and now pass it to our normal add or get routine.
            return AddOrGet(newPerformanceCounter, alias);
        }


        /// <summary>Creates a new performance counter metric instance or returns an existing one from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Performance Counter Metric (or a derived class) an exception will be thrown.</remarks>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="instanceName">The name of the performance counter category instance, or an empty string (""), if the category contains a single instance.</param>
        /// <returns>The Performance Counter Metric object for the specified instance.</returns>
        public static PerfCounterMetric AddOrGet(string categoryName, string counterName, string instanceName)
        {
            //make sure we have all of the variables we require
            if (string.IsNullOrEmpty(categoryName))
            {
                throw new ArgumentNullException(nameof(categoryName), "No category name was provided and one is required.");
            }

            if (string.IsNullOrEmpty(counterName))
            {
                throw new ArgumentNullException(nameof(counterName), "No counter name was provided and one is required.");
            }

            //Go ahead and make the windows counter
            PerformanceCounter newPerformanceCounter = GetPerformanceCounter(categoryName, counterName, instanceName);
            
            //and now pass it to our normal add or get routine.
            return AddOrGet(newPerformanceCounter);
        }

        /// <summary>Creates a new performance counter metric instance or returns an existing one from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Performance Counter Metric (or a derived class) an exception will be thrown.</remarks>
        /// <param name="newPerformanceCounter">The windows performance counter to add a definition for</param>
        /// <returns>The Performance Counter Metric object for the specified instance.</returns>
        public static PerfCounterMetric AddOrGet(PerformanceCounter newPerformanceCounter)
        {
            return AddOrGet(newPerformanceCounter, PerfCounterInstanceAlias.None);
        }


        /// <summary>Creates a new performance counter metric instance or returns an existing one from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>If the metric definition doesn't exist, it will be created.  If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not a Performance Counter Metric (or a derived class) an exception will be thrown.</remarks>
        /// <param name="newPerformanceCounter">The windows performance counter to add a definition for</param>
        /// <param name="alias">An alias to use to determine the instance name instead of the instance of the supplied counter.</param>
        /// <returns>The Performance Counter Metric object for the specified instance.</returns>
        public static PerfCounterMetric AddOrGet(PerformanceCounter newPerformanceCounter, PerfCounterInstanceAlias alias)
        {
            //we need to find the definition, adding it if necessary
            string definitionKey = PerfCounterMetricDefinition.GetKey(newPerformanceCounter);

            if (Loupe.Core.Log.Metrics.TryGetValue(definitionKey, out var definition))
            {
                //if the metric definition exists, but is of the wrong type we have a problem.
                if ((definition is PerfCounterMetricDefinition) == false)
                {
                    throw new ArgumentException("A metric already exists with the provided type, category, and counter name but it is not compatible with being a performance counter metric.  This indicates a programming error in a client application or Loupe.");
                }
            }
            else
            {
                //we didn't find one, make a new one
                definition = new PerfCounterMetricDefinition(Loupe.Core.Log.Metrics, newPerformanceCounter);
            }

            //now we have our definition, proceed to create a new metric if it doesn't exist
            //Interesting note:  here is where we basically lock in an alias to be its initial value.
            string metricKey = GetKey(newPerformanceCounter);
            IMetric metric;

            //see if we can get the metric already.  If not, we'll create it
            lock (((MetricCollection)definition.Metrics).Lock) //make sure the get & add are atomic
            {
                if (definition.Metrics.TryGetValue(metricKey, out metric) == false)
                {
                    metric = new PerfCounterMetric((PerfCounterMetricDefinition)definition, newPerformanceCounter, alias);
                }
            }

            return (PerfCounterMetric)metric;
        }

        /// <summary>
        /// The windows performance counter for this metric.
        /// </summary>
        /// <remarks>A new object is returned every time this method is called.  Clients should cache this object
        /// for a set of continuous operations, but should not cache it for any length of time for reliability reasons.</remarks>
        public PerformanceCounter GetPerformanceCounter()
        {
            //our central get perf counter method correctly handles empty/null instancenames

            //but what is our real instance name?  Regardless of what we're recording as, we need to get our real name now.
            string instanceName = InstanceName;

            if (m_InstanceAlias != PerfCounterInstanceAlias.None)
            {
                instanceName = GetInstanceName(m_InstanceAlias);
            }

            return GetPerformanceCounter(CategoryName, CounterName, instanceName);
        }

        /// <summary>
        /// Indicates whether the performance counter is currently being successfully polled or not.
        /// </summary>
        /// <remarks>This property is maintained by the performance counter polling system and is used
        /// to determine whether to log failures or not (only transitions from valid to invalid are logged)</remarks>
        public PerfCounterPollingState PollingState
        {
            get => m_PollingState;
            set => m_PollingState = value;
        }


        /// <summary>
        /// Compare this object to another to determine sort order
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PerfCounterMetric other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.CompareTo(other);
        }


        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(PerfCounterMetric other)
        {
            //we let our base object do the compare, we're really just casting things
            return base.Equals(other);
        }

        /// <summary>
        /// The definition of this metric object.
        /// </summary>
        public new PerfCounterMetricDefinition Definition => (PerfCounterMetricDefinition)base.Definition;

        #endregion

        #region Internal Properties and Methods

        internal static string GetInstanceName(PerfCounterInstanceAlias alias)
        {
            string instanceName = string.Empty;

            switch(alias)
            {
                case PerfCounterInstanceAlias.None:
                    break;
                case PerfCounterInstanceAlias.CurrentProcess:
                    instanceName = GetCurrentProcessInstanceName();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(alias));
            }

            return instanceName;
        }

        /// <summary>
        /// Generates a metric key for a the provided performance counter.  
        /// This is instance-specific and should not be used for a metric definition.
        /// </summary>
        /// <param name="newPerfCounter"></param>
        /// <returns></returns>
        internal static string GetKey(PerformanceCounter newPerfCounter)
        {
            //generate a key using the central method
            return MetricDefinition.GetKey(PerfCounterMetricDefinition.PerfCounterMetricType, newPerfCounter.CategoryName, newPerfCounter.CounterName, newPerfCounter.InstanceName);
        }


        /// <summary>
        /// Calculate the collection key for a performance counter metric packet
        /// </summary>
        internal static string GetKey(PerfCounterMetricPacket counter)
        {
            return counter.Name;
        }

        /// <summary>
        /// Calculate the collection key for a performance counter
        /// </summary>
        /// <param name="categoryName">The name of the performance counter category (performance object) with which this performance counter is associated.</param>
        /// <param name="counterName">The name of the performance counter.</param>
        /// <param name="instanceName">The name of the performance counter category instance, or an empty string (""), if the category contains a single instance.</param>
        internal static string GetKey(string categoryName, string counterName, string instanceName)
        {
            return MetricDefinition.GetKey(PerfCounterMetricDefinition.PerfCounterMetricType, categoryName, counterName, instanceName);
        }

        /// <summary>
        /// The underlying packet 
        /// </summary>
        internal new PerfCounterMetricPacket Packet => (PerfCounterMetricPacket)base.Packet;

        /// <summary>
        /// The set of raw samples for this metric
        /// </summary>
        internal new PerfCounterMetricSampleCollection Samples => (PerfCounterMetricSampleCollection)base.Samples;

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Get the PerfMon instance name for the current process
        /// </summary>
        /// <returns>The string instance name for the current process</returns>
        private static string GetCurrentProcessInstanceName()
        {
            lock (s_ProcessNameLock)
            {
                if (s_OurPid < 0)
                {
                    //we've never found out our PID - get that.
                    s_OurPid = Process.GetCurrentProcess().Id;
                }

                //because this lookup is relatively expensive, lets be sure we really need to make it.
                if (string.IsNullOrEmpty(s_OurProcessInstanceName) || (s_OurProcessInstanceNameExpirationDt < DateTimeOffset.Now))
                {
                    s_OurProcessInstanceNameExpirationDt = DateTimeOffset.Now.AddSeconds(1);
                    s_OurProcessInstanceName = GetProcessInstanceName(s_OurPid, ref s_OurProcessInstanceNamePrefix);
                }

                return s_OurProcessInstanceName;
            }
        }

        /// <summary>
        /// Get the PerfMon instance name for the process with the specified ID.
        /// </summary>
        /// <remarks>Returns an empty string if no Process ID was found for the specified PID</remarks>
        /// <param name="pid">The Process ID to look up</param>
        /// <param name="prefixHint">The invariant part of the name (regardless of the number of instances)</param>
        /// <returns>The instance name for the process or string.Empty if not found.</returns>
        private static string GetProcessInstanceName(int pid, ref string prefixHint)
        {
            string instanceName = string.Empty;
            PerformanceCounterCategory processCounters = new PerformanceCounterCategory("Process");

            string[] processNames = processCounters.GetInstanceNames();
            foreach (string processName in processNames)
            {
                //look up the ID of each process by its instance name, check the value to 
                //find out which has the PID we are looking for.  I'm open for a better idea
                //if you have one.

                if (string.IsNullOrEmpty(prefixHint) || (processName.StartsWith(prefixHint)))
                {
                    //we have to be careful - between when we pulled the instance names and now, the process we're checking may have exited.
                    try
                    {
                        using (PerformanceCounter cnt = GetPerformanceCounter("Process", "ID Process", processName))
                        {
                            //pull the raw value, it's the PID of the current process.
                            if ((int)cnt.RawValue == pid)
                            {
                                instanceName = processName;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        //we don't do anything right here - we just want to swallow the error.
                    }
                }
            }

            if ((string.IsNullOrEmpty(instanceName) == false) && (string.IsNullOrEmpty(prefixHint)))
            {
                //figure out our new prefix hint.
                int percentOffset = instanceName.LastIndexOf("%");
                if (percentOffset <= 0)
                {
                    //no percent symbol - the whole thing is the prefix.
                    prefixHint = instanceName;
                }
                else
                {
                    //pull off everything up to the %.
                    prefixHint = instanceName.Substring(0, percentOffset - 1).TrimEnd();
                }
            }

            return instanceName;
        }


        private static PerformanceCounter GetPerformanceCounter(string categoryName, string counterName, string instanceName)
        {
            PerformanceCounter newCounter;

            //we need to call the right counter method depending on whether we have an instance name or not
            if (string.IsNullOrEmpty(instanceName))
            {
                //create a new counter in read-only mode.
                newCounter = new PerformanceCounter(categoryName, counterName, true);
            }
            else
            {
                //create a new counter in read-only mode.
                newCounter = new PerformanceCounter(categoryName, counterName, instanceName, true);
            }
            return newCounter;
        }


        #endregion


        #region Base Object Overrides

        protected override MetricSample OnMetricSampleRead(MetricSamplePacket packet)
        {
            //create a new perf counter metric sample object
            return new PerfCounterMetricSample(this, (PerfCounterMetricSamplePacket)packet);
        }

        /// <summary>
        /// Invoked by the base class to allow inheritors to provide derived implementations
        /// </summary>
        /// <remarks>If you wish to provide a derived class for metric sample collection in your derived metric, use this
        /// method to create and return your derived object. 
        /// This is used during object construction, so implementations should treat it as a static method.</remarks>
        /// <returns>The MetricSampleCollection-compatible object.</returns>
        protected override MetricSampleCollection OnSampleCollectionCreate()
        {
            return new PerfCounterMetricSampleCollection(this);

        }



        #endregion
    }

    /// <summary>
    /// Indicates whether the performance counter is currently being successfully polled or not.
    /// </summary>
    public enum PerfCounterPollingState
    {
        /// <summary>
        /// The counter has been administratively excluded from polling.
        /// </summary>
        Inactive = 0,

        /// <summary>
        /// The counter is being polled successfully.
        /// </summary>
        Active = 1,

        /// <summary>
        /// The counter is being polled, but the last poll was not successful.
        /// </summary>
        Error = 2
    }

    /// <summary>
    /// Allows for referring to instance names by their logical role instead of a specific instance.
    /// </summary>
    /// <remarks>This is particularly useful for instance names that may change during the lifecycle of the process
    /// but should be treated as a single contiguous metric.</remarks>
    public enum PerfCounterInstanceAlias
    {
        /// <summary>
        /// Indicates no alias.
        /// </summary>
        None = 0,

        /// <summary>
        /// The current host process.
        /// </summary>
        CurrentProcess = 1
    }
}
