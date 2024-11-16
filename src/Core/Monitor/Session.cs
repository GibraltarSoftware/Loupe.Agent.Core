using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Gibraltar.Data;
using Gibraltar.Monitor.Serialization;
using Gibraltar.Serialization;
using Loupe.Extensibility.Data;
using System.Reflection;
using static System.Collections.Specialized.BitVector32;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Contains the log information for a single execution cycle
    /// </summary>
    /// <remarks>A session contains the thread, event, and metric information captured when it originally was executing
    /// and can be extended with analysis information including comments and markers.</remarks>
    [DebuggerDisplay("{Caption} ({Id})")]
    public class Session : IDisplayable, IComparable<Session>, IEquatable<Session>, ISessionPacketCache, IDisposable
    {
        /// <summary>
        /// The log category
        /// </summary>
        public const string LogCategory = "Loupe.Session";

        private readonly List<GLFReader> m_UnloadedPacketStreams = new List<GLFReader>();
        private readonly SessionFragmentCollection m_Fragments;
        private readonly ThreadInfoCollection m_Threads = new ThreadInfoCollection();
        private readonly ApplicationUserCollection m_Users = new ApplicationUserCollection();
        private readonly SortedDictionary<string, SessionAssemblyInfo> m_Assemblies = new SortedDictionary<string, SessionAssemblyInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly LogMessageCollection m_Messages;
        private readonly MetricDefinitionCollection m_MetricDefinitions = new MetricDefinitionCollection();

        private bool m_IsDirty;
        private bool m_IsNew;
        private SessionSummary m_SessionSummary;
        private SessionClosePacket m_EndInfo;
        private bool m_HasCorruptData;
        private int m_PacketsLostCount;
        private long m_LastSequence;

        private bool m_LoadingStreams; //used to suppress recursive load stream events.
        private bool m_AnyStreamLoaded; //indicates if we ever parsed a stream, used during Write to determine if we can just return the waiting stream.
        private bool m_IsDisposed; //indicates if we're disposed.

        /// <summary>
        /// Occurs when a property value changes. 
        /// </summary>
        /// <remarks>The PropertyChanged event can indicate all properties on the object have changed by using a null reference or String.Empty as the property name.</remarks>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Create a new session by reading the specified packet stream
        /// </summary>
        /// <param name="dataReader">The data stream reader to load as all or part of a session</param>
        public Session(GLFReader dataReader)
        {
            if (dataReader == null)
            {
                throw new ArgumentNullException(nameof(dataReader));
            }

            // Initialize empty container for file storage summary information
            FileStorageSummary = new FileStorageSummary();

            //initialize objects that require a pointer to us
            m_Fragments = new SessionFragmentCollection(this);
            m_Messages = new LogMessageCollection(this);

            //set up our session start info from the GLFReader
            m_SessionSummary = new SessionSummary(new SessionSummaryPacket(dataReader.SessionHeader));
            IntegrateHeader(dataReader.SessionHeader);

            //is this a full session, or a session fragment?  
            if (dataReader.SessionHeader.HasFileInfo)
            {
                //this will both add it to the fragments collection AND add the stream to our cache
                Fragments.Add(dataReader);
            }
            else
            {
                //this isn't a file fragment, it's a whole session - so we need to add the packet stream
                //to the cache ourselves (normally that'd be done by the Fragments collection)
                AddPacketStream(dataReader);
            }

            //now that we've read up everything, make sure we have a caption on start info.  
            if (string.IsNullOrEmpty(m_SessionSummary.Caption))
            {
                m_SessionSummary.Caption = DefaultCaption(m_SessionSummary);
            }

            //and connect to our files collection changed event so we will notify callers if someone adds a new data file
            m_Fragments.CollectionChanged += m_Files_CollectionChanged;
        }

        #region Private Properties and Methods

        /// <summary>
        /// Notify our subscribers that a property has changed and mark that we're dirty.
        /// </summary>
        /// <param name="propertyName">The property that changed</param>
        private void SendPropertyChanged(String propertyName)
        {
            //we must be dirty - we changed!
            m_IsDirty = true;

            if ((PropertyChanged != null))
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }


        void m_Files_CollectionChanged(object sender, CollectionChangedEventArgs<SessionFragmentCollection, SessionFragment> e)
        {
            //notify our callers that all of our properties have changed
            SendPropertyChanged(null);
        }

        /// <summary>
        /// See if our PerformanceCounter library is around and if so register it for deserialization.
        /// </summary>
        /// <returns></returns>
        private void RegisterPerfCounterFactory(PacketReader packetReader)
        {
            var factoryTypeName = "Loupe.Agent.PerformanceCounters.Serialization.PerfCounterMetricPacketFactory";

            // See if the type has already been loaded for any reason
            var factoryType = Type.GetType(factoryTypeName);

            if (factoryType == null)
            {
                // It hasn't - probe for the specific assembly we expect and load that assembly
                // to load the type.
                var assemblyName = "Loupe.Agent.PerformanceCounters.dll";
                var binFolder = AppDomain.CurrentDomain.BaseDirectory;
                var assemblyPath = Path.Combine(binFolder, assemblyName);

                if (!File.Exists(assemblyPath))
                {
                    return;
                }

                var assembly = Assembly.LoadFrom(assemblyPath);
                factoryType = assembly.GetType(factoryTypeName);
            }

            if (factoryType == null)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Error, "", "Unable to load performance counter data because library doesn't have the expected factory type", "We looked for a type called {0} but it wasn't found, so we'll skip Perf Counter data.", factoryTypeName);
                return;
            }

            // Create an instance of PerfCounterMetricPacketFactory
            var factoryInstance = Activator.CreateInstance(factoryType, this);

            // Get the Register method
            var registerMethod = factoryType.GetMethod("Register");

            if (registerMethod == null)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Error, "", "Unable to load performance counter data because library doesn't support registration", "We attempted to invoke the register method of the performance counter assembly but it was not found, so we'll skip it.");
                return;
            }

            // Invoke the Register method
            registerMethod.Invoke(factoryInstance, new [] { packetReader });
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Integrate the provided session header into the session
        /// </summary>
        /// <param name="newHeader"></param>
        internal void IntegrateHeader(SessionHeader newHeader)
        {
            var newStatus = (SessionStatus)Enum.Parse(typeof(SessionStatus), newHeader.StatusName, true);
            if (newStatus > Summary.Status)
            {
                Summary.Status = newStatus; // It's a new status, so update our record.
            }
            else if (newStatus < Summary.Status)
            {
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Warning, LogCategory, "Inconsistent session status change when integrating header",
                    "Prior status for session: {0}\r\nStatus from new header: {1}\r\n" +
                    "Violates status progression rules Running->Normal->Crashed, so new status will be ignored.\r\n",
                    Summary.Status, newStatus);
            }

            if (Summary.EndDateTime < newHeader.EndDateTime)
                Summary.EndDateTime = newHeader.EndDateTime;

            if (Summary.MessageCount < newHeader.MessageCount)
                Summary.MessageCount = newHeader.MessageCount;

            if (Summary.CriticalCount < newHeader.CriticalCount)
                Summary.CriticalCount = newHeader.CriticalCount;

            if (Summary.ErrorCount < newHeader.ErrorCount)
                Summary.ErrorCount = newHeader.ErrorCount;

            if (Summary.WarningCount < newHeader.WarningCount)
                Summary.WarningCount = newHeader.WarningCount;
        }

        /// <summary>
        /// Load any session files into the session that haven't already been loaded.
        /// </summary>
        /// <remarks>If all files are loaded this method returns immediately.</remarks>
        internal void EnsureDataLoaded()
        {
            //we don't want to continue if we're completely loaded OR in the process of loading.
            if ((m_LoadingStreams) || (IsLoaded))
                return;

            //we have streams that haven't been loaded yet. Lets load them.           
            m_LoadingStreams = true; //so we don't get into an infinite loop trying to load.

            var loadedStreams = new List<GLFReader>(m_UnloadedPacketStreams.Count);

            try
            {
                while (m_UnloadedPacketStreams.Count > 0)
                {
                    using (var glfReader = m_UnloadedPacketStreams[0]) //we want to dispose of these as we go to save RAM.
                    {
                        try
                        {
                            //this is best effort- add this stream to our list of loaded streams BEFORE we try to process it
                            loadedStreams.Add(glfReader);
                            LoadPacketStream(glfReader);
                        }
                        catch (ThreadAbortException ex)
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Information, LogWriteMode.Queued, ex, LogCategory, "Aborting loading packet stream.", "A thread abort exception was thrown on our worker thread so we'll stop trying to load the session.");
                            m_PacketsLostCount++; // there may be multiple packets lost, but there will be at least one;
                        }
                        catch (Exception ex)
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Warning, LogWriteMode.WaitForCommit, ex, LogCategory, "Exception loading packet stream.", "{0}: {1}", ex.GetType(), ex.Message);
                            m_PacketsLostCount++; // there may be multiple packets lost, but there will be at least one;
                        }

                        m_UnloadedPacketStreams.Remove(glfReader); //we've now loaded it.
                    }
                }

                // Having read all the fragments, we can now sort the FileStorageSummary info meaningfully
                FileStorageSummary.Summarize();
            }
            finally
            {
                //since this option suppresses our desire to load we need to ensure it's cleared, even if we throw an exception
                m_LoadingStreams = false;
            }

            //and if any streams showed up while we were loading, we better recurse and try again.
            if (IsLoaded == false)
                EnsureDataLoaded();
        }

        internal void LoadPacketStream(GLFReader glfReader)
        {
            if (glfReader == null)
                throw new ArgumentNullException(nameof(glfReader));

            var packetStream = glfReader.GetPacketStreamStart(); // Resets stream to start of packet data.

            if (packetStream == null)
            {
                throw new ArgumentException("The provided GLFReader did not have a packet stream.", nameof(glfReader));
            }

            m_AnyStreamLoaded = true;

            var packetManager = new PacketManager(packetStream);
            var packetReader = new PacketReader(null, true, glfReader.MajorVersion, glfReader.MinorVersion);

            //register our packet factories we need
            var sessionPacketFactory = new SessionPacketFactory();
            sessionPacketFactory.Register(packetReader);

            var logMessagePacketFactory = new LogMessagePacketFactory(this);
            logMessagePacketFactory.Register(packetReader);

            var metricPacketFactory = new MetricPacketFactory(this);
            metricPacketFactory.Register(packetReader);

            var metricDefinitionPacketFactory = new MetricDefinitionPacketFactory(this);
            metricDefinitionPacketFactory.Register(packetReader);

            var metricSamplePacketFactory = new MetricSamplePacketFactory(this);
            metricSamplePacketFactory.Register(packetReader);

            //HACK: our PerfCounter factory.
            RegisterPerfCounterFactory(packetReader);

            Stream stream;
            var packetCount = 0;
            IPacket previousPacket = null; //this is just used for debugging and can't be moved to inner scope.

            while ((stream = packetManager.GetNextPacket()) != null)
            {
                IPacket nextPacket = null;
#if DEBUG
                previousPacket = null; //this is just used for debugging.

                // Debug code to display packet contents
                if (packetCount < 0) // Condition is never true, but this allows us to go here in debugger session
                {
                    var b = new byte[stream.Length];
                    stream.Read(b, 0, (int)stream.Length);
                    stream.Position = 0;
                    var s = "";
                    for (var i = 0; i < stream.Length; i++)
                    {
                        var ch = (char)b[i];
                        s += (b[i] > 20 && b[i] < 127) ? ch.ToString() : ".";
                    }
                    Console.WriteLine(s);
                    Console.WriteLine(BitConverter.ToString(b));
                }
#endif

                try
                {
                    nextPacket = packetReader.ReadPacket(stream);
                    packetCount++;
                }
                catch (Exception ex)
                {
                    m_HasCorruptData = true;
                    m_PacketsLostCount++;

                    //we used to just drop the current packet and continue for GibraltarSerializationException,
                    //but in the field it doesn't seem there's ever a case where once we go bad we can recover.
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory,
                            "Exception during packet stream read, unable to continue deserializing data",
                            "While attempting to deserialize packet number {0} an exception was reported. This has failed the stream so serialization will stop.\r\nException: {1}",
                            packetCount, ex.Message);
                    break; //and busta move.
                }

                if (nextPacket != null)
                {
                    try
                    {
                        SessionSummaryPacket sessionSummaryPacket;
                        SessionClosePacket sessionClosePacket;
                        SessionFragmentPacket sessionFragmentPacket;
                        ThreadInfoPacket threadInfoPacket;
                        AssemblyInfoPacket assemblyInfoPacket;
                        LogMessagePacket logMessagePacket;
                        MetricPacket metricPacket;
                        MetricSamplePacket metricSamplePacket;
                        MetricDefinitionPacket metricDefinitionPacket;
                        EventMetricValueDefinitionPacket eventMetricValueDefinitionPacket;
                        ApplicationUserPacket applicationUserPacket;

                        // NOTE:  When adding to this list, you need to put the MOST derived object type on top
                        // of anything it is derived from, since "is" will match base objects.
                        // E.g. (object is base) is true even if the object.GetType != base.
                        // Also, notice that we use "as" to safely cast and then test the cached
                        // result to do the check, with variables declared above for each type.
                        // This is because FxCop warns about the redundant cast operation done by
                        // "is" which discards the resulting cast which we then need to do again.
                        // Make sure to follow this pattern when adding new cases to this if-else-if chain.

                        if ((logMessagePacket = nextPacket as LogMessagePacket) != null) //this covers trace packet too
                        {
                            if (m_Messages.ContainsKey(logMessagePacket.Sequence) == false)
                            {
                                // The new serialized version of LMP adds a ThreadIndex field.  Older versions will set this
                                // field from the ThreadId field, so we can rely on ThreadIndex in any case.
                                if (m_Threads.TryGetValue(logMessagePacket.ThreadIndex, out var threadInfo) == false)
                                {
                                    // Uh-oh.  This should never happen.
                                    threadInfo = null; // We couldn't find the ThreadInfo for this log message!
                                }

                                ApplicationUser applicationUser = null;
                                if (string.IsNullOrEmpty(logMessagePacket.UserName) == false)
                                    m_Users.TryFindUserName(logMessagePacket.UserName, out applicationUser);

                                var logMessage = new LogMessage(this, threadInfo, applicationUser, logMessagePacket);
                                m_Messages.Add(logMessage);
                            }
                        }
                        //NOTE:  If you are tempted to add special code for your derived MetricPacket object, DON'T  
                        //- see how the packet object factory works for metrics.
                        else if ((metricPacket = nextPacket as MetricPacket) != null)
                        {
                            if (m_MetricDefinitions.ContainsMetricKey(metricPacket.ID) == false)
                            {
                                //We need to find the definition object for this metric so we can create the metric object
                                if (m_MetricDefinitions.TryGetValue(metricPacket.DefinitionId, out var definition) == false)
                                {
                                    //Trace out that we are going to have to drop this metric value because we don't have 
                                    //the metric it applies to
                                    if (!Log.SilentMode) Log.Write(LogMessageSeverity.Error, LogCategory, "Unable to find the metric definition referenced by the current metric.", "The metric packet will be dropped.  Metric definition is ID {0}", metricPacket.DefinitionId);
                                }
                                else
                                {
                                    if (definition.Metrics.ContainsKey(metricPacket.ID) == false)
                                    {
                                        //We have to get the metric object using the object factory, and the metric constructor will add it
                                        //to the definition's metric collection, so we don't have to.
                                        ((IPacketObjectFactory<Metric, MetricDefinition>)nextPacket).GetDataObject((MetricDefinition)definition);
                                    }
                                }
                            }
                        }
                        //NOTE:  If you are tempted to add special code for your derived MetricSamplePacket object, DON'T 
                        // - see how the packet object factory works for metrics.
                        else if ((metricSamplePacket = nextPacket as MetricSamplePacket) != null)
                        {
                            //these are the individual metric samples that are associated with a metric

                            //we need to find the metric for this packet before we can add it
                            if (m_MetricDefinitions.TryGetMetricValue(metricSamplePacket.MetricId, out var metric) == false)
                            {
                                //Trace out that we are going to have to drop this metric value because we don't have 
                                //the metric it applies to
                                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Error, LogCategory, "Unable to find the metric referenced by the current metric sample packet.", "The metric packet will be dropped.  Metric is ID {0}", metricSamplePacket.MetricId);
                            }
                            else
                            {
                                if (((MetricSampleCollection)metric.Samples).ContainsKey(metricSamplePacket.Sequence) == false)
                                {
                                    //now have the packet object give us the wrapping data object.  It will automatically add itself to the collection
                                    ((IPacketObjectFactory<MetricSample, Metric>)nextPacket).GetDataObject((Metric)metric);
                                }
                            }
                        }
                        //NOTE:  If you are tempted to add special code for your derived MetricDefinitionPacket object, DON'T 
                        // - see how the packet object factory works for metrics.
                        else if ((metricDefinitionPacket = nextPacket as MetricDefinitionPacket) != null)
                        {
                            if (m_MetricDefinitions.ContainsKey(metricDefinitionPacket.ID) == false)
                            {
                                //We have to get the metric definition object using the object factory, which auto-adds it to the collection.
                                ((IPacketObjectFactory<MetricDefinition, MetricDefinitionCollection>)metricDefinitionPacket).GetDataObject(m_MetricDefinitions);
                            }
                        }
                        //Here's a bogus special case:  Event metric definitions have a set of value definitions that need to be 
                        //associated with it.  So we handle them explicitly.
                        else if ((eventMetricValueDefinitionPacket = nextPacket as EventMetricValueDefinitionPacket) != null)
                        {
                            //We need to find the definition object for this value so we can create the value definition object
                            var definition = (EventMetricDefinition)m_MetricDefinitions[eventMetricValueDefinitionPacket.DefinitionId];

                            if (definition.Values.ContainsKey(eventMetricValueDefinitionPacket.Name) == false)
                            {
                                //because we don't support inheritance in this special case, we don't need to do any packet factory nonsense
                                ((EventMetricValueDefinitionCollection)definition.Values).Add(eventMetricValueDefinitionPacket);
                            }
                        }
                        else if ((threadInfoPacket = nextPacket as ThreadInfoPacket) != null)
                        {
                            var threadInfo = new ThreadInfo(threadInfoPacket);
                            if (m_Threads.Contains(threadInfo) == false)
                            {
                                m_Threads.Add(threadInfo);
                            }
                        }
                        else if ((assemblyInfoPacket = nextPacket as AssemblyInfoPacket) != null)
                        {
                            var sessionAssemblyInfo = new SessionAssemblyInfo(assemblyInfoPacket);
                            if (m_Assemblies.ContainsKey(sessionAssemblyInfo.FullName) == false)
                            {
                                m_Assemblies.Add(sessionAssemblyInfo.FullName, sessionAssemblyInfo);
                            }
                        }
                        else if ((applicationUserPacket = nextPacket as ApplicationUserPacket) != null)
                        {
                            var applicationUser = new ApplicationUser(applicationUserPacket);
                            m_Users.TrySetValue(applicationUser);
                        }
                        else if ((sessionSummaryPacket = nextPacket as SessionSummaryPacket) != null)
                        {
                            // We only take this packet if we don't have one already.  We should always have one from constructor.
                            if (m_SessionSummary == null)
                            {
                                m_SessionSummary = new SessionSummary(sessionSummaryPacket);
                                // Session summary was null, so we don't have a record of the session status.
                                // So get the status from the EndInfo packet, if there has been one.
                                if (m_EndInfo != null)
                                    m_SessionSummary.Status = m_EndInfo.EndingStatus;
                            }
                        }
                        else if ((sessionClosePacket = nextPacket as SessionClosePacket) != null)
                        {
                            if (m_SessionSummary != null && (m_EndInfo == null ?
                                sessionClosePacket.EndingStatus >= m_SessionSummary.Status :
                                sessionClosePacket.EndingStatus > m_SessionSummary.Status))
                            {
                                // This can only advance one way, Running -> Normal -> Crashed.
                                m_EndInfo = sessionClosePacket; // Replace any previous one, this packet is a new state.
                                m_SessionSummary.Status = m_EndInfo.EndingStatus;
                            }
                        }
                        else if ((sessionFragmentPacket = nextPacket as SessionFragmentPacket) != null)
                        {
                            var sessionFragment = new SessionFragment(sessionFragmentPacket);
                            if (m_Fragments.Contains(sessionFragment.Id) == false)
                            {
                                m_Fragments.Add(sessionFragment);
                            }
                        }
                        else
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Error, LogCategory, "Packet stream contained unexpected object.", "This can be due to loading a newer file format into an older version of Gibraltar. Object: {0}", nextPacket);
                        }

                        //if this is a sequence #'d packet (and they really all should be) then we need to check if it's our new top sequence.
                        GibraltarPacket gibraltarPacket;
                        if ((gibraltarPacket = nextPacket as GibraltarPacket) != null)
                        {
                            m_LastSequence = Math.Max(m_LastSequence, gibraltarPacket.Sequence);
                        }
                    }
                    catch (Exception ex)
                    {
                        GC.KeepAlive(ex);
#if DEBUG
                        throw;
#else
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Unexpected exception while loading packet stream", "{0}", ex.Message);
#endif
                    }
                }


#if DEBUG
                previousPacket = nextPacket;
                //we only want previous packet for debugging purposes, but if we don't
                //at least try to do something with it then the compiler eliminates it.
                if (previousPacket == null)
                {
                    GC.KeepAlive(previousPacket);
                }
#endif
            }

            // Capture packet storage summary
            FileStorageSummary.Merge(packetReader.GetStorageSummary(), glfReader.FragmentStorageSummary);

            //now that we've read up everything, make sure we have a caption on start info.  
            if ((m_SessionSummary != null) && (string.IsNullOrEmpty(m_SessionSummary.Caption)))
            {
                m_SessionSummary.Caption = DefaultCaption(m_SessionSummary);
            }

            m_Threads.UniquifyThreadNames(); // And distinguish any thread name (Caption) collisions.
        }

        /// <summary>
        /// Generates a reasonable default caption for the provided session that has no caption
        /// </summary>
        /// <param name="sessionSummary">The session summary object to generate a default caption for</param>
        /// <returns>The default caption</returns>
        internal static string DefaultCaption(SessionSummary sessionSummary)
        {
            var defaultCaption = string.Empty;

            //We are currently shooting for <appname> <Short Date> <Short time>
            if (string.IsNullOrEmpty(sessionSummary.Application))
            {
                defaultCaption += "(Unknown app)";
            }
            else
            {
                //we want to truncate the application if it's over a max length
                if (sessionSummary.Application.Length > 32)
                {
                    defaultCaption += sessionSummary.Application.Substring(0, 32);
                }
                else
                {
                    defaultCaption += sessionSummary.Application;
                }
            }

            defaultCaption += " " + sessionSummary.StartDateTime.DateTime.ToShortDateString();

            defaultCaption += " " + sessionSummary.StartDateTime.DateTime.ToShortTimeString();

            return defaultCaption;
        }

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// The file storage summary tracking information for this session.
        /// </summary>
        public FileStorageSummary FileStorageSummary { get; private set; }

        /// <summary>
        /// Add the provided GLF stream (could be for an entire session or a session fragment) to the list of streams to be loaded.
        /// </summary>
        /// <param name="glfReader">The GLFReader which owns the stream to be added.</param>
        public void AddPacketStream(GLFReader glfReader)
        {
            if (glfReader == null)
            {
                throw new ArgumentNullException(nameof(glfReader));
            }

            m_UnloadedPacketStreams.Add(glfReader);
        }

        /// <summary>
        /// The number of bytes of unloaded stream data.
        /// </summary>
        public long UnloadedStreamLength
        {
            get
            {
                if (m_UnloadedPacketStreams.Count == 0)
                    return 0;

                long length = 0;
                foreach (var glfReader in m_UnloadedPacketStreams)
                {
                    var rawStream = glfReader.RawStream;
                    if (rawStream.CanSeek)
                        length += rawStream.Length;
                }

                return length;
            }
        }

        /// <summary>
        /// The set of all metrics tracked in this session.
        /// </summary>
        public MetricDefinitionCollection MetricDefinitions
        {
            get
            {
                //make sure all files are loaded
                EnsureDataLoaded(); //this is very fast if all are already loaded

                //there is always a collection.  It may be empty, but it's always there.
                return m_MetricDefinitions;
            }
        }

        /// <summary>
        /// Write our data to the specified stream.  This does not directly clear the dirty or new flags
        /// because the object can't confirm that the stream was persisted.
        /// </summary>
        /// <param name="stream">The writable stream to persist to</param>
        public void Write(Stream stream)
        {
            Write(stream, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion);
        }

        /// <summary>
        /// Write our data to the specified stream.  This does not directly clear the dirty or new flags
        /// because the object can't confirm that the stream was persisted.
        /// </summary>
        /// <param name="stream">The writable stream to persist to</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        public void Write(Stream stream, int majorVersion, int minorVersion)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            //there are two paths to this:  If we have NEVER loaded our data and have just one stream
            //we can copy that to the provided stream.
            if ((m_AnyStreamLoaded == false) && (m_UnloadedPacketStreams.Count == 1))
            {
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Copying session stream directly from unloaded original packet stream", "This is the fastest path");

                //we still have to make a valid file - so we have to use GLF writer.  This ensures the summary data is current.
                var sessionReader = m_UnloadedPacketStreams[0];
                var sessionStream = sessionReader.RawStream; // Get the raw GLF stream (including header)
                FileSystemTools.StreamContentCopy(sessionStream, stream); // Copy the entire GLF stream, then restore its Position.
            }
            else
            {
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Writing session stream by serializing session", "This slower path is used because some data has already been loaded or we don't have the original streams");

                //make sure all files are loaded
                EnsureDataLoaded(); //this is very fast if all are already loaded

                using (var writer = new GLFWriter(stream, m_SessionSummary, majorVersion, minorVersion))
                {
                    //order really should be unnecessary due to packet dependency tracking, but we tend to order them anyway.

                    //first, the summary information of the session
                    writer.Write(m_SessionSummary.Packet);

                    //information on the fragments it was recorded into
                    foreach (var sessionFile in m_Fragments)
                        writer.Write(sessionFile.Packet);

                    //Threads
                    foreach (var threadInfo in m_Threads)
                        writer.Write(threadInfo.Packet);

                    //Assemblies
                    foreach (var pair in m_Assemblies)
                        writer.Write(pair.Value.Packet);

                    //Log Message data (we should really serialize data last after metric definitions...
                    foreach (var message in (IList<LogMessage>)m_Messages)
                        writer.Write(message.MessagePacket);

                    //metric definitions and, internally, metric samples.
                    foreach (MetricDefinition curMetricDefinition in m_MetricDefinitions)
                    {
                        writer.Write(curMetricDefinition.Packet);

                        //Is this an event metric?  We have to serialize out more information for them. (yeah, very bogus)
                        var eventMetricDefinition = curMetricDefinition as EventMetricDefinition;
                        if (eventMetricDefinition != null)
                        {
                            foreach (EventMetricValueDefinition curValueDefinition in eventMetricDefinition.Values)
                            {
                                writer.Write(curValueDefinition.Packet);
                            }
                        }

                        foreach (Metric curMetric in curMetricDefinition.Metrics)
                        {
                            writer.Write(curMetric.Packet);
                            foreach (MetricSample curSample in curMetric.Samples)
                                writer.Write(curSample.Packet);
                        }
                    }

                    //finally, the optional end info packet to show we're really done.
                    if (m_EndInfo != null)
                        writer.Write(m_EndInfo);
                }
            }
        }

        /// <summary>
        /// Session properties from the session start process.
        /// </summary>
        public IDictionary<string, string> Properties { get { return m_SessionSummary.Properties; } }

        /// <summary>
        /// The collection of data files that were recorded for this session.
        /// </summary>
        /// <remarks>There may be many files for a single session.  Each file has a unique ID
        /// and can only be loaded once.</remarks>
        public SessionFragmentCollection Fragments
        {
            get
            {
                //this is the one place we do NOT want to make sure the files are loaded because
                //delay loading is ALL about fragments!
                return m_Fragments;
            }
        }

        /// <summary>
        /// The list of threads associated with this session.  Threads are sorted by their unique thread Id.
        /// </summary>
        public ThreadInfoCollection Threads
        {
            get
            {
                //make sure all files are loaded
                EnsureDataLoaded(); //this is very fast if all are already loaded

                return m_Threads;
            }
        }

        /// <summary>
        /// The list of users associated with this application.  
        /// </summary>
        public ApplicationUserCollection Users
        {
            get
            {
                //make sure all files are loaded
                EnsureDataLoaded(); //this is very fast if all are already loaded

                return m_Users;
            }
        }


        /// <summary>
        /// The list of threads associated with this session.  Threads are sorted by their unique thread Id.
        /// </summary>
        ThreadInfoCollection ISessionPacketCache.Threads
        {
            get
            {
                //in this case we do NOT want to force data to load because we may be running in the iterator
                return m_Threads;
            }
        }

        /// <summary>
        /// The list of users associated with this application.  
        /// </summary>
        ApplicationUserCollection ISessionPacketCache.Users
        {
            get
            {
                //in this case we do NOT want to force data to load because we may be running in the iterator
                return m_Users;
            }
        }

        /// <summary>
        /// The list of assemblies associated with this session.  Assemblies are sorted by their unique full names.
        /// </summary>
        public SortedDictionary<string, SessionAssemblyInfo> Assemblies
        {
            get
            {
                //make sure all files are loaded
                EnsureDataLoaded(); //this is very fast if all are already loaded

                return m_Assemblies;
            }
        }

        /// <summary>
        /// The list of log messages associated with this session.
        /// </summary>
        public LogMessageCollection Messages
        {
            get
            {
                //make sure all files are loaded
                EnsureDataLoaded(); //this is very fast if all are already loaded

                return m_Messages;
            }
        }

        /// <summary>
        /// The set of log messages for this session.
        /// </summary>
        /// <returns>An enumerable of the messages</returns>
        /// <remarks>This method provides an enumerable that reads the session data from the data file each time it is iterated
        /// so it won't consume excessive memory even if the file is very large or contains very large messages.</remarks>
        public IEnumerable<LogMessage> GetMessages()
        {
            return new LogMessageEnumerable(this, m_Threads, m_Users, m_UnloadedPacketStreams);
        }

        /// <summary>
        /// A short end-user display caption 
        /// </summary>
        public string Caption
        {
            get
            {
                return m_SessionSummary.Caption;
            }
            set
            {
                if (m_SessionSummary.Caption != value)
                {
                    m_SessionSummary.Caption = value;

                    //and signal that we changed a property we expose
                    SendPropertyChanged("Caption");
                }
            }
        }

        /// <summary>
        /// An extended description without formatting.
        /// </summary>
        public string Description { get { return m_SessionSummary.ApplicationDescription; } }

        /// <summary>
        /// The summary properties for this session.
        /// </summary>
        public SessionSummary Summary { get { return m_SessionSummary; } }

        /// <summary>
        /// A constant, unique identifier for this session.
        /// </summary>
        public Guid Id { get { return m_SessionSummary.Id; } }

        /// <summary>
        /// Indicates if the session has been loaded into memory or not.
        /// </summary>
        public bool IsLoaded
        {
            get
            {
                return (m_UnloadedPacketStreams.Count == 0);
            }
        }

        /// <summary>
        /// Force the session to completely load into memory.
        /// </summary>
        public void Load()
        {
            EnsureDataLoaded();
        }

        /// <summary>
        /// Force the session to load, but only the specified fragments.
        /// </summary>
        /// <param name="fragmentIds"></param>
        public void Load(Guid[] fragmentIds)
        {
            //clear our pending load list - we're going to manually figure it out.
            m_UnloadedPacketStreams.Clear();

            foreach (var fragmentId in fragmentIds)
            {
                if (Fragments.TryGetValue(fragmentId, out var fragment))
                {
                    m_UnloadedPacketStreams.Add(fragment.Reader);
                }
            }

            EnsureDataLoaded();
        }

        /// <summary>
        /// The final status of the session.
        /// </summary>
        public SessionStatus Status { get { return m_SessionSummary.Status; } }

        /// <summary>
        /// The worst severity of the log messages in the session
        /// </summary>
        public LogMessageSeverity TopSeverity
        {
            get
            {
                if (m_SessionSummary.CriticalCount > 0)
                {
                    return LogMessageSeverity.Critical;
                }

                if (m_SessionSummary.ErrorCount > 0)
                {
                    return LogMessageSeverity.Error;
                }

                if (m_SessionSummary.WarningCount > 0)
                {
                    return LogMessageSeverity.Warning;
                }

                return LogMessageSeverity.Verbose;
            }
        }

        /// <summary>
        /// The number of messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public long MessageCount { get { return m_SessionSummary.MessageCount; } }

        /// <summary>
        /// The number of critical messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public long CriticalCount { get { return m_SessionSummary.CriticalCount; } }

        /// <summary>
        /// The number of error messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public long ErrorCount { get { return m_SessionSummary.ErrorCount; } }

        /// <summary>
        /// The number of error messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public long WarningCount { get { return m_SessionSummary.WarningCount; } }


        /// <summary>
        /// Indicates whether there are changes to this session that have not been saved.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                return m_IsDirty;
            }
            set
            {
                m_IsDirty = value;
            }
        }

        /// <summary>
        /// Indicates whether a session is new to this package (has never been saved)
        /// </summary>
        public bool IsNew
        {
            get
            {
                return m_IsNew;
            }
            set
            {
                m_IsNew = value;
            }
        }

        /// <summary>
        /// Indicates whether a session had errors during deserialization and has lost some packets.
        /// </summary>
        public bool HasCorruptData
        {
            get
            {
                return m_HasCorruptData;
            }
        }

        /// <summary>
        /// Indicates how many packets were lost due to errors in deserialization.
        /// </summary>
        public int PacketsLostCount
        {
            get
            {
                return m_PacketsLostCount;
            }
        }

        /// <summary>
        /// The last sequence number in the session data.  Will force the session to be entirely parsed.
        /// </summary>
        public long LastSequence
        {
            get
            {
                EnsureDataLoaded();

                return m_LastSequence;
            }
        }

        #endregion

        #region IComparable and IEquatable Methods

        // Note: This was an explicit IComparable<Session> implementation rather than implicit, which prevents it from being public.
        // I don't see why we would want this not public, because it makes using CompareTo more difficult.
        // The use of <Session> in the generic should prevent this from running into other interface implementations
        // with the same method signature (which would be the main reason for needing an explicit implementation, right?).
        // Hopefully changing this to a public implicit implementation (the norm) will not have unanticipated consequences.
        // This mark and comment can be removed once we're sure this change doesn't break anything.

        /// <summary>
        /// Compares this Session object to another to determine sorting order.
        /// </summary>
        /// <remarks>Session instances are sorted primarily by their StartDateTime property.</remarks>
        /// <param name="other">The other Session object to compare this object to.</param>
        /// <returns>An int which is less than zero, equal to zero, or greater than zero to reflect whether
        /// this Session should sort as being less-than, equal to, or greater-than the other
        /// Session, respectively.</returns>
        public int CompareTo(Session other)
        {
            int result;

            //Fast equality check: are the GUID's the same?
            if (Id == other.Id)
            {
                return 0;
            }

            //The rest of this is about comparing for sorting.  We never want to have an equal here.

            //we compare sessions first based on date & Time
            if (m_SessionSummary.StartDateTime < other.Summary.StartDateTime)
            {
                //definitively - we're first
                result = -1;
            }
            else if (m_SessionSummary.StartDateTime > other.Summary.StartDateTime)
            {
                //definitely - we're last
                result = 1;
            }
            else
            {
                //if start date & time are the same, sort by application name
                result = string.Compare(m_SessionSummary.Product, other.Summary.Product, StringComparison.InvariantCulture);

                //if start date & time are the same, sort by application name
                if (result == 0)
                {
                    result = string.Compare(m_SessionSummary.Application, other.Summary.Application, StringComparison.InvariantCulture);
                }

                //if that is a match, sort by version
                if (result == 0)
                {
                    result = m_SessionSummary.ApplicationVersion.CompareTo(other.Summary.ApplicationVersion);
                }

                //if that's still a match, compare GUID to enforce a consistent order
                if (result == 0)
                {
                    result = Id.CompareTo(other.Id);
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether the provided Session object is equal to this Session object.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">The Session object to compare with this Session object.</param>
        public bool Equals(Session other)
        {
            // Careful, it could be null; check it without recursion
            if (ReferenceEquals(other, null))
            {
                return false; // Since we're a live object we can't be equal to a null instance.
            }

            //they are equal if they have the same Guid
            return Id.Equals(other.Id);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a Session and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            var otherSession = obj as Session;

            return Equals(otherSession); // Just have type-specific Equals do the check (it even handles null)
        }

        /// <summary>
        /// Provides a representative hash code for objects of this type to spread out distribution
        /// in hash tables.
        /// </summary>
        /// <remarks>Objects which consider themselves to be Equal (a.Equals(b) returns true) are
        /// expected to have the same hash code.  Objects which are not Equal may have the same
        /// hash code, but minimizing such overlaps helps with efficient operation of hash tables.
        /// </remarks>
        /// <returns>
        /// An int representing the hash code calculated for the contents of this object.
        /// </returns>
        public override int GetHashCode()
        {
            var myHash = Id.GetHashCode(); // The ID is all that Equals checks!

            return myHash;
        }

        /// <summary>
        /// Compares two Session instances for equality.
        /// </summary>
        /// <param name="left">The Session to the left of the operator</param>
        /// <param name="right">The Session to the right of the operator</param>
        /// <returns>True if the two Sessions are equal.</returns>
        public static bool operator ==(Session left, Session right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two Session instances for inequality.
        /// </summary>
        /// <param name="left">The Session to the left of the operator</param>
        /// <param name="right">The Session to the right of the operator</param>
        /// <returns>True if the two Sessions are not equal.</returns>
        public static bool operator !=(Session left, Session right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return !ReferenceEquals(right, null);
            }
            return !left.Equals(right);
        }

        /// <summary>
        /// Compares if one Session instance should sort less than another.
        /// </summary>
        /// <param name="left">The Session to the left of the operator</param>
        /// <param name="right">The Session to the right of the operator</param>
        /// <returns>True if the Session to the left should sort less than the Session to the right.</returns>
        public static bool operator <(Session left, Session right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one Session instance should sort greater than another.
        /// </summary>
        /// <param name="left">The Session to the left of the operator</param>
        /// <param name="right">The Session to the right of the operator</param>
        /// <returns>True if the Session to the left should sort greater than the Session to the right.</returns>
        public static bool operator >(Session left, Session right)
        {
            return (left.CompareTo(right) > 0);
        }

        #endregion

        #region IDisposable Members

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // and SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (!m_IsDisposed)
            {
                m_IsDisposed = true; // Only Dispose stuff once

                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    foreach (var unloadedPacketStream in m_UnloadedPacketStreams)
                    {
                        unloadedPacketStream.Dispose();
                    }

                    m_UnloadedPacketStreams.Clear();
                }
            }
        }

        #endregion
    }
}
