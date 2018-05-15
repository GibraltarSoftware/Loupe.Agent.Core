using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Gibraltar.Data;
using Gibraltar.Data.Internal;
using Gibraltar.Messaging;
using Gibraltar.Monitor.Internal;
using Gibraltar.Server.Client;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Handles interfacing with a single log file for the purpose of writing log messages.
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// The file extension (without period) for a Gibraltar Log File.  Used internally to Gibraltar.
        /// </summary>
        public const string LogExtension = FileMessenger.LogExtension;

        /// <summary>
        /// The file extension (without period) for a Gibraltar Package File.
        /// </summary>
        public const string PackageExtension = FileMessenger.PackageExtension;

        /// <summary>
        /// A standard file filter for standard file dialogs that allows selection of packages and logs.
        /// </summary>
        public const string FileFilter = "Package File(*." + PackageExtension + ")|*." + PackageExtension + "|Log File (*." + LogExtension + ")|*." + LogExtension + "|All Files (*.*)|*.*";

        /// <summary>
        /// A standard file filter for standard file dialogs that allows selection of logs.
        /// </summary>
        public const string FileFilterLogsOnly = "Log File (*." + LogExtension + ")|*." + LogExtension + "|All Files (*.*)|*.*";

        /// <summary>
        /// A standard file filter for standard file dialogs that allows selection of packages.
        /// </summary>
        public const string FileFilterPackagesOnly = "Package File(*." + PackageExtension + ")|*." + PackageExtension + "|All Files (*.*)|*.*";

        /// <summary>
        /// The log system name for Gibraltar
        /// </summary>
        public const string ThisLogSystem = "Gibraltar";

        /// <summary>
        /// The category for trace messages
        /// </summary>
        public const string Category = "Trace";

        /// <summary>
        /// The default category name, replacing a null or empty category.
        /// </summary>
        private const string GeneralCategory = "General";

        /// <summary>
        /// The default category name for a dedicated Exception message.
        /// </summary>
        public const string ExceptionCategory = "System.Exception";

        internal static readonly string[] LineBreakTokens = new[] { "\r\n", "\n\r", "\n", "\r" };
        internal const string LineBreakString = "\r\n";

        private static SessionSummary s_SessionStartInfo;
        private static Publisher s_Publisher;         //Our one and only publisher
        private static RepositoryPublishEngine s_PublishEngine; //We have zero or one publish engine
        private static Packager s_ActivePackager; //PROTECTED BY LOCK
        private static LocalRepository s_Repository;

        private static readonly MetricDefinitionCollection s_Metrics = new MetricDefinitionCollection();

        [ThreadStatic] private static ThreadInfo t_CurrentThreadInfo; // ThreadInfo for the current thread, for efficiency.

        private static bool s_SendSessionsOnExit; // protected by SYNCOBJECT

        private static AgentConfiguration s_RunningConfiguration;

        private static readonly object s_SyncObject = new object(); //the general lock for the log object.
        private static readonly object s_InitializingLock = new object();
        private static readonly object s_ConsentLock = new object(); //used just for the Auto Send Consent to keep it from interfering with anything else.
        private static readonly object s_NotifierLock = new object(); //used for initializing Notifier instances.

        private volatile static bool s_Initialized; //protected by being volatile
        private volatile static bool s_InitializationNeverAttempted = true; //protected by being volatile
        private volatile static bool s_Initializing; //PROTECTED BY INITIALIZING and volatile
        private volatile static bool s_ExplicitStartSessionCalled; // protected by being volatile

        private static Notifier s_MessageAlertNotifier; // PROTECTED BY NOTIFIERLOCK (weak check outside lock allowed)
        private static UserResolutionNotifier s_UserResolutionNotifier;

        // A thread-specific static flag for each thread to identify if this thread is the current initialize
        [ThreadStatic]
        private static bool t_ThreadIsInitializer; // false by default for each thread


        /// <summary>
        /// Handler for the Initialize event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void InitializingEventHandler(object sender, LogInitializingEventArgs e);

        /// <summary>
        /// Raised whenever the log system is being started to enable programmatic configuration.
        /// </summary>
        /// <remarks>You can cancel initialization by setting the cancel property to true in the event arguments. 
        /// If canceled, the log system will not record any information but allow all calls to be made.
        /// Even if canceled it is possible for the logging system to attempt to reinitialize if a call 
        /// is explicitly made to start a session.</remarks>
        public static event InitializingEventHandler Initializing;

        #region Debugging assistance

        /// <summary>
        /// A temporary flag to tell us whether to invoke a Debugger.Break() when Log.DebugBreak() is called.
        /// </summary>
        /// <remarks>True enables breakpointing, false disables.  This should probably be replaced with an enum
        /// to support multiple modes, assuming the basic usage works out.</remarks>
        public static bool BreakPointEnable
        {
            get { return CommonCentralLogic.BreakPointEnable; }
            set { CommonCentralLogic.BreakPointEnable = value; }
        }

        /// <summary>
        /// Automatically stop debugger like a breakpoint, if enabled.
        /// </summary>
        /// <remarks>This will check the state of Log.BreakPointEnable and whether a debugger is attached,
        /// and will breakpoint only if both are true.  This should probably be extended to handle additional
        /// configuration options using an enum, assuming the basic usage works out.  This method is conditional
        /// upon a DEBUG build and will be safely ignored in release builds, so it is not necessary to wrap calls
        /// to this method in #if DEBUG (acts much like Debug class methods).</remarks>
        [Conditional("DEBUG")]
        public static void DebugBreak()
        {
            if (BreakPointEnable && Debugger.IsAttached)
            {
                Debugger.Break(); // Stop here only when debugging
                // ...then Shift-F11 to step out to where it is getting called...
            }
        }

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// Indicates if the logging system should be running in silent mode (for example when running in the agent).
        /// </summary>
        /// <remarks>Pass-through to the setting in CommonFileTools.</remarks>
        public static bool SilentMode
        {
            get { return CommonCentralLogic.SilentMode; }
            set { CommonCentralLogic.SilentMode = value; }
        }

        /// <summary>
        /// Indicates if the process is running under the Mono runtime or the full .NET CLR.
        /// </summary>
        public static bool IsMonoRuntime
        {
            get
            {
                return CommonCentralLogic.IsMonoRuntime;
            }
        }


        /// <summary>
        /// Indicates if logging is active, performing initialization if necessary
        /// </summary>
        /// <returns>True if logging is active, false if it isn't at this time.</returns>
        /// <remarks>The very first time this is used it will attempt to start the logging system even if 
        /// it hasn't already been started.  If that call is canceled through our Initializing event then 
        /// it will return false.  After the first call it will indicate if logging is currently initialized
        /// and not attempt to initialize.</remarks>
        public static bool IsLoggingActive()
        {
            if (s_Initialized)
                return true; //this is our fastest case - we're up and running, nothing more to say.

            //The behavior here isn't obvious:  We go in if initialization has never been done OR is being 
            //done for the first time right now.  That ensures consistency between threads.
            if (s_InitializationNeverAttempted)
            {
                Initialize(null);
            }

            return s_Initialized;
        }

        /// <summary>
        /// Indicates if the log system has been initialized and is operational
        /// </summary>
        /// <remarks>Once true it will never go false, however if false it may go true at any time.</remarks>
        internal static bool Initialized { get { return s_Initialized; } }

        /// <summary>
        /// Attempt to initialize the log system.  If it is already initialized it will return immediately.
        /// </summary>
        /// <param name="configuration">Optional.  A default configuration to start with instead of the configuration file.</param>
        /// <returns>True if the initialization has completed (on this call or prior),
        /// false if a re-entrant call returns to avoid deadlocks and infinite recursion.</returns>
        /// <remarks>If calling initialization from a path that may have started with the trace listener,
        /// you must set suppressTraceInitialize to true to guarantee that the application will not deadlock
        /// or throw an unexpected exception.</remarks>
        public static bool Initialize(AgentConfiguration configuration)
        {
            //NOTE TO MAINTAINERS:  THIS CLASS RELIES ON THE INITIALIZER VARIABLES BEING VOLATILE TO AVOID LOCKS

            if (t_ThreadIsInitializer)
                return false; //this is a re-entrant call, return before we try to get any locks to ensure no deadlocks.

            //ARE we initialized?
            if (s_Initialized)
                return true; // Initialization has already been run before, so report it complete.

            //OK, not initialized yet - either we're initializing or we need to stall until we're initialized.
            bool performInitialization = false;
            lock(s_InitializingLock) //still need this lock to be sure we're the one and only thread that attempts to do initialization.
            {
                if (s_Initializing == false)
                {
                    //since it isn't kicking over yet we need to start that process...
                    s_Initializing = true;
                    performInitialization = true;
                }

                System.Threading.Monitor.PulseAll(s_InitializingLock);
            }

            if (performInitialization)
            {
                t_ThreadIsInitializer = true; // so if we wander around and re-enter initialize we won't block.

                //we have to be sure that any initialization failure won't damage our lock state which would deadlock logging
                try
                {
                    s_Initialized = OnInitialize(configuration);
                }
                finally
                {
                    t_ThreadIsInitializer = false;
                    s_InitializationNeverAttempted = false; // we no longer want people to call into us. this should be set before we release the waiting threads waiting on Initializing.

                    //and we are no longer trying to initialize.
                    lock(s_InitializingLock)
                    {
                        s_Initializing = false;

                        System.Threading.Monitor.PulseAll(s_InitializingLock);
                    }

                    //and set the logger down to the server connection now that it's valid
                    HubConnection.Logger = new ClientLogger();
                }
            }
            else
            {
                //we need to stall until the initialization status is determinate - either we're initialized or not.

                // Careful, don't block if we're called from a critical thread or we could deadlock.
                if (Publisher.QueryThreadMustNotBlock() == false)
                {
                    //it's initializing, but not yet complete - we need to stall until it is.
                    lock (s_InitializingLock)
                    {
                        while (s_Initializing) //the status is still indeterminate.
                        {
                            System.Threading.Monitor.Wait(s_InitializingLock);
                        }

                        System.Threading.Monitor.PulseAll(s_InitializingLock);
                    }
                }
            }

            return s_Initialized; // Initialization is now done, report completion.
        }

        /// <summary>
        /// The running publisher configuration.  This is always safe even when logging is disabled.
        /// </summary>
        public static AgentConfiguration Configuration
        {
            get
            {
                EnsureSummaryIsAvailable();

                return s_RunningConfiguration;
            }
        }

        /// <summary>
        /// The common information about the active log session.  This is always safe even when logging is disabled.
        /// </summary>
        public static SessionSummary SessionSummary
        {
            get
            {
                EnsureSummaryIsAvailable();

                return s_SessionStartInfo;
            }
        }

        /// <summary>
        /// Get the official Error Alert Notifier instance.  Will create it if it doesn't already exist.
        /// </summary>
        public static Notifier MessageAlertNotifier
        {
            get
            {
                if (s_MessageAlertNotifier == null)
                {
                    lock (s_NotifierLock) // Must get the lock to make sure only one thread can try this at a time.
                    {
                        if (s_MessageAlertNotifier == null) // Double-check that it's actually still null.
                            s_MessageAlertNotifier = new Notifier(LogMessageSeverity.Warning, "Message Alert");
                    }
                }

                return s_MessageAlertNotifier; // It's never altered again once created, so we can just read it now.
            }
        }

        /// <summary>
        /// Get the official user resolution notifier instance.  Will create it if it doesn't already exist.
        /// </summary>
        public static UserResolutionNotifier UserResolutionNotifier
        {
            get
            {
                if (s_UserResolutionNotifier == null)
                {
                    lock (s_NotifierLock) // Must get the lock to make sure only one thread can try this at a time.
                    {
                        if (s_UserResolutionNotifier == null) // Double-check that it's actually still null.
                            s_UserResolutionNotifier = new UserResolutionNotifier(s_RunningConfiguration.Publisher.EnableAnonymousMode);
                    }
                }

                return s_UserResolutionNotifier; // It's never altered again once created, so we can just read it now.
            }
        }

        /// <summary>
        /// The current process's collection repository
        /// </summary>
        public static LocalRepository Repository
        {
            get 
            { 
                //when valid this should have been set up during initialization, which is done explicitly.
                return s_Repository;
            }
        }

        /// <summary>
        /// Indicates if we have sufficient configuration information to automatically send packages while running (via email or server).
        /// </summary>
        /// <remarks>This checks whether there is sufficient configuration to submit sessions using the current configuration.</remarks>
        /// <returns></returns>
        public static bool CanSendSessions(ref string message)
        {
            if (s_Initialized == false)
            {
                message = "Gibraltar is not currently enabled";
                return false;
            }

            if (message == null)
                message = string.Empty;

            bool goodToGo = false;

            //if neither mode are enabled supply that message.
            PackagerConfiguration packager = s_RunningConfiguration.Packager;

            if ((packager.AllowServer == false) && (packager.AllowEmail == false))
            {
                message = "Neither email or server packaging is allowed with the current packager configuration";
            }
            else
            {
                if ((packager.AllowEmail) && (IsEmailSubmissionConfigured(ref message))) //only test if allowed
                {
                    goodToGo = true;
                }
                else if ((packager.AllowServer) && (IsHubSubmissionConfigured(ref message))) //only test if allowed
                {
                    goodToGo = true;
                }
            }

            return goodToGo;            
        }

        /// <summary>
        /// Indicates if we have sufficient configuration information to automatically send packages upon exit (via email or server).
        /// </summary>
        /// <remarks>This checks whether there is sufficient configuration to submit sessions through the packager upon exit.
        /// It also checks that the packager executable can be found.</remarks>
        /// <returns></returns>
        public static bool CanSendSessionsOnExit(ref string message)
        {
            bool goodToGo = CanSendSessions(ref message);

            //we also check that the executable is around since we have to fire up packager externally.
            if (goodToGo)
            {
                goodToGo = CanFindPackager(ref message);
            }

            return goodToGo;
        }

        /// <summary>
        /// Indicates if we have sufficient configuration information to automatically send packages by email submission.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Does not check if email submission is allowed</remarks>
        public static bool IsEmailSubmissionConfigured(ref string message)
        {
            if (s_Initialized == false)
            {
                message = "Gibraltar is not currently enabled";
                return false;
            }

            bool goodToGo = true;
            if (message == null)
                message = string.Empty;

            // Do we appear to have sufficient configuration?
            PackagerConfiguration packager = s_RunningConfiguration.Packager;
            if (string.IsNullOrEmpty(packager.DestinationEmailAddress))
            {
                message += "No destination email address was provided for the packager.\r\n";
                goodToGo = false;
            }

            // ToDo: Consider having it also check the outgoing email server config (which is more than just ours).

            return goodToGo;
        }

        /// <summary>
        /// Indicates if we have sufficient configuration information to automatically send packages to a Loupe Server.
        /// </summary>
        /// <remarks>This checks whether there is sufficient configuration to submit sessions through a server.
        /// It does NOT check whether the packager is configured to allow submission through a server, because
        /// they may also be sent directly from Agent without using the packager.</remarks>
        /// <returns></returns>
        public static bool IsHubSubmissionConfigured(ref string message)
        {
            if (s_Initialized == false)
            {
                message = "Gibraltar is not currently enabled";
                return false;
            }

            bool goodToGo = true;
            if (message == null)
                message = string.Empty;

            // Do we appear to have sufficient configuration?
            ServerConfiguration server = s_RunningConfiguration.Server;
            if (server.Enabled == false)
            {
                message += "Server configuration is missing or disabled.\r\n";
                goodToGo = false; // Can't use it if it's disabled.
            }
            else if (server.UseGibraltarService)
            {
                // Using the Loupe Service requires a customer name.
                if (string.IsNullOrEmpty(server.CustomerName))
                {
                    message += "No customer name was provided for the Loupe Service.\r\n";
                    goodToGo = false; // Can't use Loupe Service if no customer name is configured.
                }
            }
            else
            {
                // Using a private server requires a server name and a port that is not negative (0 means default).
                if (string.IsNullOrEmpty(server.Server))
                {
                    message += "No server name was provided for the server.\r\n";
                    goodToGo = false; // Can't use a private server if no server name is configured.
                }
                else if (server.Port < 0)
                {
                    message += "An invalid server port was configured.\r\n";
                    goodToGo = false; // Can't use a private server if the port is not valid.
                }
            }

            return goodToGo;
        }

        /// <summary>
        /// Indicates if the packager executable is available where this process can find it.
        /// </summary>
        public static bool CanFindPackager(ref string message)
        {
            if (message == null)
                message = string.Empty;

            // Is the packager executable available?
            string packagerFileNamePath = GetPackagerFileNamePath();
            bool goodToGo = File.Exists(packagerFileNamePath);

            if (goodToGo == false)
            {
                message += "The packager utility could not be found in the same directory as the application.\r\n";
            }

            return goodToGo;
        }

        /// <summary>
        /// Indicates if the agent should package &amp; send sessions for the current application after this session exits.
        /// </summary>
        /// <remarks>When true the system will automatically </remarks>
        public static bool SendSessionsOnExit
        {
            get
            {
                lock (s_SyncObject)
                {
                    System.Threading.Monitor.PulseAll(s_SyncObject);

                    return s_SendSessionsOnExit;
                }
            }
            [MethodImplAttribute(MethodImplOptions.NoInlining)] // Does this work here? Is it needed?
            set
            {
                SetSendSessionsOnExit(value);
            }
        }

        /// <summary>
        /// Indicates if the StartSession API method was ever explicitly called.
        /// </summary>
        /// <remarks>If StartSession was not explicitly called then an ApplicationExit event will implicitly call
        /// EndSession for easy Gibraltar drop-in support.  If StartSession was explicitly called then we expect
        /// the client to make a corresponding explicit EndSession call, and the Agent's ApplicationExit handler
        /// will not call EndSession.</remarks>
        public static bool ExplicitStartSessionCalled
        {
            get { return s_ExplicitStartSessionCalled; }
        }

        /// <summary>
        /// Our one metric definition collection for capturing metrics in this process
        /// </summary>
        /// <remarks>
        /// For performance reasons, it is important that there is only a single instance of a particular metric
        /// for any given process.  This is managed automatically provided only this metrics collection is used.
        /// If there is a duplicate metric in the data stream, that information will be discarded when the log 
        /// file is read (but there is no effect at runtime).
        /// </remarks>
        public static MetricDefinitionCollection Metrics
        {
            get { return s_Metrics; }
        }

        /// <summary>
        /// Reports whether EndSession() has been called to formally end the session.
        /// </summary>
        public static bool IsSessionEnding { get { return CommonCentralLogic.IsSessionEnding; } }

        /// <summary>
        /// Reports whether EndSession() has completed flushing the end-session command to the log.
        /// </summary>
        public static bool IsSessionEnded { get { return CommonCentralLogic.IsSessionEnded; } }

        /// <summary>
        /// Record the provided set of metric samples to the log.
        /// </summary>
        /// <remarks>When sampling multiple metrics at the same time, it is faster to make a single write call
        /// than multiple calls.</remarks>
        /// <param name="samples">A list of metric samples to record.</param>
        public static void Write(List<MetricSample> samples)
        {
            if (s_Initialized == false)
                return;

            if (samples == null || samples.Count == 0)
                return;

            IMessengerPacket[] packetArray = new IMessengerPacket[samples.Count]; // An array to hold the batch of packets.
            // Now iterate over each sample, putting them into our array.
            int index = 0;
            foreach (MetricSample curSample in samples)
            {
                packetArray[index] = curSample.Packet;
                index++;
            }

            s_Publisher.Publish(packetArray, false);
        }

        /// <summary>
        /// Record the provided metric sample to the log.
        /// </summary>
        /// <remarks>Most applications should use another object or the appropriate log method on this object to
        /// create log information instead of manually creating log packets and writing them here.  This functionality
        /// is primarily for internal support of the various log listeners that support third party log systems.</remarks>
        /// <param name="sample"></param>
        public static void Write(MetricSample sample)
        {
            if (s_Initialized == false)
                return;

            if (sample == null)
                return;

            // We must wrap the packet as an array here, the underlying publisher method now takes them as a batch.
            s_Publisher.Publish(new[] {sample.Packet}, false);
        }


        /// <summary>
        /// The version information for the Gibraltar Agent.
        /// </summary>
        public static Version AgentVersion
        {
            get
            {
                EnsureSummaryIsAvailable();

                return s_SessionStartInfo.AgentVersion;
            }
        }

        /// <summary>
        /// Write a trace message directly to the Gibraltar log.
        /// </summary>
        /// <remarks>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer
        /// callers.</remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Write(LogMessageSeverity severity, string category, string caption, string description, params object[] args)
        {
            if (s_Initialized == false)
                return;

            // skipFrames = 1 to skip out to our caller instead of right here.
            LocalLogMessage logMessage = new LocalLogMessage(severity, ThisLogSystem, category, 1, null, caption,
                                                             description, args);
            logMessage.PublishToLog(); // tell the SimpleLogMessage to publish itself (back through us).
        }

        /// <summary>
        /// Write a log message directly to the Gibraltar log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(LogMessageSeverity severity, LogWriteMode writeMode, Exception exception, string category, string caption,
                                 string description, params object[] args)
        {
            if (s_Initialized == false)
                return;

            // skipFrames = 1 to skip out to our caller instead of right here.
            LocalLogMessage logMessage = new LocalLogMessage(severity, writeMode, ThisLogSystem, category,
                                                             1, exception, false, null, caption, description, args);
            logMessage.PublishToLog(); // tell the SimpleLogMessage to publish itself (back through us).
        }

        /// <summary>
        /// Write a log message directly to the Gibraltar log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(LogMessageSeverity severity, LogWriteMode writeMode, Exception exception, int skipFrames, string category, string caption,
                                 string description, params object[] args)
        {
            if (s_Initialized == false)
                return;

            // skipFrames = 1 to skip out to our caller instead of right here.
            LocalLogMessage logMessage = new LocalLogMessage(severity, writeMode, ThisLogSystem, category,
                                                             skipFrames + 1, exception, false, null, caption, description, args);
            logMessage.PublishToLog(); // tell the SimpleLogMessage to publish itself (back through us).
        }

        /// <summary>
        /// Write a log message directly to the Gibraltar log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True if the call stack from where the exception was thrown should be used for log message attribution</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(LogMessageSeverity severity, LogWriteMode writeMode, Exception exception, bool attributeToException, string category, string caption,
                                 string description, params object[] args)
        {
            if (s_Initialized == false)
                return;

            // skipFrames = 1 to skip out to our caller instead of right here.
            LocalLogMessage logMessage = new LocalLogMessage(severity, writeMode, ThisLogSystem, category,
                                                             1, exception, attributeToException, null, caption, description, args);
            logMessage.PublishToLog(); // tell the SimpleLogMessage to publish itself (back through us).
        }

        /// <summary>
        /// Write a trace message directly to the Gibraltar log with an optional attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This overload of WriteMessage() is provided as an API hook for simple wrapping methods
        /// which need to attribute a log message to their own outer callers.  Passing a skipFrames of 0 would
        /// designate the caller of this method as the originator; a skipFrames of 1 would designate the caller
        /// of the caller of this method as the originator, and so on.  It will then extract information about
        /// the originator automatically based on the indicated stack frame.  Bridge logic adapting from a logging
        /// system which already determines and provides information about the originator (such as log4net) into
        /// Gibraltar should use the other overload of WriteMessage(), passing a customized IMessageSourceProvider.</para>
        /// <para>This method also requires explicitly selecting the LogWriteMode between Queued (the normal default,
        /// for optimal performance) and WaitForCommit (to help ensure critical information makes it to disk, e.g. before
        /// exiting the application upon return from this call).  See the LogWriteMode enum for more information.</para>
        /// <para>This method also allows an optional Exception object to be attached to the log message (null for
        /// none).  And the message may be a simple message string, or a format string followed by arguments.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional.  A variable number of arguments to insert into the formatted description string.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteMessage(LogMessageSeverity severity, LogWriteMode writeMode, int skipFrames, Exception exception,
                                        string detailsXml, string caption, string description, params object[] args)
        {
            if (s_Initialized == false)
                return;

            if (skipFrames < 0)
                skipFrames = 0; // Less than 0 is illegal (it would mean us!), correct it to designate our immediate caller.

            LocalLogMessage logMessage = new LocalLogMessage(severity, writeMode, ThisLogSystem, Category, skipFrames + 1,
                                                               exception, false, detailsXml, caption, description, args);
            logMessage.PublishToLog(); // tell the DetailLogMessage to publish itself (back through us).
        }

        /// <summary>
        /// Write a trace message directly to the Gibraltar log with an optional attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This overload of WriteMessage() is provided as an API hook for simple wrapping methods
        /// which need to attribute a log message to their own outer callers.  Passing a skipFrames of 0 would
        /// designate the caller of this method as the originator; a skipFrames of 1 would designate the caller
        /// of the caller of this method as the originator, and so on.  It will then extract information about
        /// the originator automatically based on the indicated stack frame.  Bridge logic adapting from a logging
        /// system which already determines and provides information about the originator (such as log4net) into
        /// Gibraltar should use the other overload of WriteMessage(), passing a customized IMessageSourceProvider.</para>
        /// <para>This method also requires explicitly selecting the LogWriteMode between Queued (the normal default,
        /// for optimal performance) and WaitForCommit (to help ensure critical information makes it to disk, e.g. before
        /// exiting the application upon return from this call).  See the LogWriteMode enum for more information.</para>
        /// <para>This method also allows an optional Exception object to be attached to the log message (null for
        /// none).  And the message may be a simple message string, or a format string followed by arguments.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True if the call stack from where the exception was thrown should be used for log message attribution</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional.  A variable number of arguments to insert into the formatted description string.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WriteMessage(LogMessageSeverity severity, LogWriteMode writeMode, int skipFrames, Exception exception, bool attributeToException,
                                        string detailsXml, string caption, string description, params object[] args)
        {
            if (s_Initialized == false)
                return;

            if (skipFrames < 0)
                skipFrames = 0; // Less than 0 is illegal (it would mean us!), correct it to designate our immediate caller.

            LocalLogMessage logMessage = new LocalLogMessage(severity, writeMode, ThisLogSystem, Category, skipFrames + 1,
                                                               exception, attributeToException, detailsXml, caption, description, args);
            logMessage.PublishToLog(); // tell the DetailLogMessage to publish itself (back through us).
        }

        /// <summary>
        /// Write a Verbose trace message directly to the Gibraltar log.
        /// </summary>
        /// <remarks>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations
        /// should instead use the Log.Write(...) overloads.</remarks>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Trace(string format, params object[] args)
        {
            if (s_Initialized == false)
                return;

            // skipFrames = 1 to skip out to our caller instead of right here.
            // Null caption tells it to automatically extract the caption from the description after formatting.
            LocalLogMessage logMessage = new LocalLogMessage(LogMessageSeverity.Verbose, ThisLogSystem, Category,
                                                             1, null, null, format, args);
            logMessage.PublishToLog(); // tell the LocalLogMessage to publish itself (back through us).
        }

        /// <summary>
        /// Write a Verbose trace message directly to the Gibraltar log.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations
        /// should instead use the Log.Write(...) overloads.</para>
        /// <para>This method also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Trace(Exception exception, string format, params object[] args)
        {
            if (s_Initialized == false)
                return;

            // skipFrames = 1 to skip out to our caller instead of right here.
            LocalLogMessage logMessage = new LocalLogMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued, ThisLogSystem,
                                                             Category, 1, exception, false, null, null, format, args);
            logMessage.PublishToLog(); // tell the LocalLogMessage to publish itself (back through us).
            // Write(LogMessageSeverity.Verbose, exception, 2, format, args);
        }


        /// <summary>
        /// Record an unexpected Exception to the Gibraltar central log, formatted automatically.
        /// </summary>
        /// <remarks><para>This method provides an easy way to record an Exception as a separate message which will be
        /// attributed to the code location which threw the Exception rather than where this method was called from.
        /// The category will default to "Exception" if null, and the message will be formatted automatically based on the
        /// Exception.  The severity will be determined by the canContinue parameter:  Critical for fatal errors (canContinue
        /// is false), Error for non-fatal errors (canContinue is true).</para>
        /// <para>This method is intended for use with top-level exception catching for errors not anticipated in a
        /// specific operation, but when it is not appropriate to alert the user because the error does not impact their
        /// work flow or will be otherwise handled gracefully within the application.  For unanticipated errors which
        /// disrupt a user activity, see the <see CREF="ReportException">ReportException</see> method.</para></remarks>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.</param>
        /// <param name="exception">An Exception object to record as a log message.  This call is ignored if null.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the exception.  Can be null.</param>
        /// <param name="category">The application subsystem or logging category that the message will be associated with.</param>
        /// <param name="canContinue">True if the application can continue after this call, false if this is a fatal error
        /// and the application can not continue after this call.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void RecordException(int skipFrames, Exception exception, string detailsXml, string category,
                                           bool canContinue)
        {
            if (s_Initialized == false)
                return;

            if (skipFrames < 0)
                skipFrames = 0;

            MessageSourceProvider sourceProvider = new MessageSourceProvider(skipFrames + 1, true);

            RecordException(sourceProvider, exception, detailsXml, category, canContinue, false, false);
        }

        /// <summary>
        /// Record an unexpected Exception to the Gibraltar central log, formatted automatically.
        /// </summary>
        /// <remarks><para>This method provides an easy way to record an Exception as a separate message which will be
        /// attributed to the code location which threw the Exception rather than where this method was called from.
        /// The category will default to "Exception" if null, and the message will be formatted automatically based on the
        /// Exception.  The severity will be determined by the canContinue parameter:  Critical for fatal errors (canContinue
        /// is false), Error for non-fatal errors (canContinue is true).</para>
        /// <para>This method is intended for use with top-level exception catching for errors not anticipated in a
        /// specific operation, but when it is not appropriate to alert the user because the error does not impact their
        /// work flow or will be otherwise handled gracefully within the application.  For unanticipated errors which
        /// disrupt a user activity, see the <see CREF="ReportException">ReportException</see> method.</para></remarks>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message (NOT the exception source information).</param>
        /// <param name="exception">An Exception object to record as a log message.  This call is ignored if null.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the exception.  Can be null.</param>
        /// <param name="category">The application subsystem or logging category that the message will be associated with.</param>
        /// <param name="canContinue">True if the application can continue after this call, false if this is a fatal error
        /// and the application can not continue after this call.</param>
        public static void RecordException(IMessageSourceProvider sourceProvider, Exception exception, string detailsXml,
                                           string category, bool canContinue)
        {
            if (s_Initialized == false)
                return;

            RecordException(sourceProvider, exception, detailsXml, category, canContinue, false, false);
        }

        /// <summary>
        /// Record an unexpected Exception to the Gibraltar central log, formatted automatically.
        /// </summary>
        /// <remarks><para>This method provides an easy way to record an Exception as a separate message which will be
        /// attributed to the code location which threw the Exception rather than where this method was called from.
        /// The category will default to "Exception" if null, and the message will be formatted automatically based on the
        /// Exception.  The severity will be determined by the canContinue parameter:  Critical for fatal errors (canContinue
        /// is false), Error for non-fatal errors (canContinue is true).</para>
        /// <para>This method is intended for use with top-level exception catching for errors not anticipated in a
        /// specific operation, but when it is not appropriate to alert the user because the error does not impact their
        /// work flow or will be otherwise handled gracefully within the application.  For unanticipated errors which
        /// disrupt a user activity, see the <see CREF="ReportException">ReportException</see> method.</para></remarks>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message (NOT the exception source information).</param>
        /// <param name="exception">An Exception object to record as a log message.  This call is ignored if null.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the exception.  Can be null.</param>
        /// <param name="category">The application subsystem or logging category that the message will be associated with.</param>
        /// <param name="canContinue">True if the application can continue after this call, false if this is a fatal error
        /// and the application can not continue after this call.</param>
        /// <param name="reporting">True if the error will also be reported to the user. (private use)</param>
        /// <param name="blocking">True if reporting to user and waiting for user response; otherwise should be false. (private use)</param>
        private static void RecordException(IMessageSourceProvider sourceProvider, Exception exception, string detailsXml,
                                           string category, bool canContinue, bool reporting, bool blocking)
        {
            if (s_Initialized == false)
                return;

            if (exception == null)
                return;

            LogMessageSeverity severity = canContinue ? LogMessageSeverity.Error : LogMessageSeverity.Critical;

            ExceptionSourceProvider exceptionSourceProvider = new ExceptionSourceProvider(exception);

            IMessageSourceProvider finalSourceProvider;
            
            string recorded;
            string recordedLocation = string.Empty;
            if (exceptionSourceProvider.ClassName == null)
            {
                finalSourceProvider = sourceProvider;
                recorded = string.Empty; // Already have the recording location in the fSP, don't add it to description.
            }
            else
            {
                finalSourceProvider = exceptionSourceProvider;
                // Source will be attributed to the exception origin, so add the reporting location to the description.
                recorded = string.Format("Reported by: {0}.{1}\r\n", sourceProvider.ClassName, sourceProvider.MethodName);
                if (sourceProvider.FileName != null)
                {
                    recordedLocation = string.Format("Location: Line {0} of file '{1}'\r\n", sourceProvider.LineNumber,
                                                     sourceProvider.FileName);
                }
            }

            if (string.IsNullOrEmpty(category))
                category = ExceptionCategory;

            string caption = (exception.Message ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(caption))
                caption = exception.GetType().Name;

            string reportString = canContinue ?
                "\r\nThis non-fatal error {0} be reported to the user{1}, then execution may continue.\r\n" :
                "\r\nThis fatal error {0} be reported to the user{1}, then the application will exit.\r\n";

            reportString = string.Format(reportString, (reporting ? "will" : "will not"), reporting ? (blocking ?
                " and wait for their response" : " without waiting on their response") : string.Empty);

            WriteMessage(severity, LogWriteMode.WaitForCommit, ThisLogSystem, category, finalSourceProvider, null, exception,
                         detailsXml, null, "{0}\r\nException type: {1}\r\n{2}{3}{4}", caption, exception.GetType().FullName,
                         recorded, recordedLocation, reportString);

            if (canContinue == false)
                SessionSummary.Status = SessionStatus.Crashed;
        }

        /// <summary>
        /// Write a complete log message to the Gibraltar central log.
        /// </summary>
        /// <remarks>Used as an API entry point for interfaces for other logging systems to hand off log messages
        /// into Gibraltar.  This method ONLY supports being invoked on the same thread which originated the log
        /// message.</remarks>
        /// <param name="severity">The severity enum value of the log message.</param>
        /// <param name="writeMode">A LogWriteMode enum value indicating whether to simply queue the log message
        /// and return quickly, or to wait for the log message to be committed to disk before returning.</param>
        /// <param name="logSystem">The name of the originating log system, such as "Trace", "Log4Net",
        /// or "Gibraltar".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <param name="userName">The effective username associated with the execution task which
        /// issued the log message.</param>
        /// <param name="exception">An Exception object attached to this log message, or null if none.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional.  A variable number of arguments to insert into the formatted description string.</param>
        public static void WriteMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem,
                                        string categoryName, IMessageSourceProvider sourceProvider, string userName,
                                        Exception exception, string detailsXml, string caption, string description, params object[] args)
        {
            if (s_Initialized == false)
                return;

            IMessengerPacket packet = MakeLogPacket(severity, logSystem, categoryName, sourceProvider, userName,
                                                    exception, detailsXml, caption, description, args);
            Write(new[] {packet}, writeMode); // Write the assembled packet to our queue
        }

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file
        /// (instead of waiting for an automatic maintenance roll-over) in order to allow the logs of
        /// an ongoing session up to that point to be collected and submitted (or opened in the viewer)
        /// for analysis without shutting down the subject application.</remarks>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndFile()
        {
            if (s_Initialized == false)
                return;

            EndFile(1, string.Empty); // No reason declared, attribute it to our immediate caller.
        }

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file
        /// (instead of waiting for an automatic maintenance roll-over) in order to allow the logs of
        /// an ongoing session up to that point to be collected and submitted (or opened in the viewer)
        /// for analysis without shutting down the subject application.</remarks>
        /// <param name="reason">An optionally-declared reason for invoking this operation (may be null or empty).</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndFile(string reason)
        {
            if (s_Initialized == false)
                return;

            EndFile(1, reason); // Pass on reason, attribute it to our immediate caller.
        }

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file
        /// (instead of waiting for an automatic maintenance roll-over) in order to allow the logs of
        /// an ongoing session up to that point to be collected and submitted (or opened in the viewer)
        /// for analysis without shutting down the subject application.</remarks>
        /// <param name="skipFrames">The number of stack frames to skip out to find the original caller.</param>
        /// <param name="reason">An optionally-declared reason for invoking this operation (may be null or empty).</param>
        public static void EndFile(int skipFrames, string reason)
        {
            if (s_Initialized == false)
                return;

            if (skipFrames < 0) // Sanity check, in case we decide to make this overload public also...
                skipFrames = 0;// Illegal skipFrames value, attribute it to our immediate caller instead.

            // Remember to increment skipFrames as we pass it down a new stack frame level.
            IMessageSourceProvider sourceProvider = new MessageSourceProvider(skipFrames+1, true);

            const string endFormat = "Current log file ending by request{0}{1}";
            const string newFormat = "New log file opened by request{0}{1}";
            const string noReasonTerminator = ".";
            const string reasonDelimiter = ": ";
            string formatArg0;
            string formatArg1;

            if (string.IsNullOrEmpty(reason))
            {
                formatArg0 = noReasonTerminator;
                formatArg1 = string.Empty;
            }
            else
            {
                formatArg0 = reasonDelimiter;
                formatArg1 = reason;
            }

            // Make a packet to mark the end of the current log file and why it ended there.
            IMessengerPacket endPacket = MakeLogPacket(LogMessageSeverity.Information, ThisLogSystem, Category,
                                                       sourceProvider, string.Empty, null, null,
                                                       string.Format(endFormat, formatArg0, formatArg1), null);

            // Make a command packet to trigger the actual file close.
            IMessengerPacket commandPacket = new CommandPacket(MessagingCommand.CloseFile);

            // Make a packet to force a new file open, mark why it rolled over, and key off of for completion.
            IMessengerPacket newPacket = MakeLogPacket(LogMessageSeverity.Information, ThisLogSystem, Category,
                                                       sourceProvider, string.Empty, null, null,
                                                       string.Format(newFormat, formatArg0, formatArg1), null);

            // Now send them as a batch to enforce back-to-back processing, and wait for the last one to commit to disk.
            Write(new [] { endPacket, commandPacket, newPacket }, LogWriteMode.WaitForCommit);
        }

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally or explicitly crashed.
        /// </summary>
        /// <remarks><para>This will put the Gibraltar log into an ending state in which it will flush everything still
        /// in its queue and then switch to a background thread to process any further log messages.  All log messages
        /// submitted after this call will block the submitting thread until they are committed to disk, so that any
        /// foreground thread still logging final items will be sure to get them through before they exit.  This is
        /// called automatically when an ApplicationExit event is received, and can also be called directly (such as
        /// if that event would not function).</para>
        /// <para>If EndSession is never called, the log will reflect that the session must have crashed.</para></remarks>
        /// <param name="endingStatus">The explicit ending status to declare for this session, <see cref="SessionStatus.Normal">Normal</see>
        /// or <see cref="SessionStatus.Crashed">Crashed</see>.</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <param name="reason">A simple reason to declare why the application is ending as Normal or as Crashed, or may be null.</param>
        public static void EndSession(SessionStatus endingStatus, IMessageSourceProvider sourceProvider, string reason)
        {
            if (s_Initialized == false)
                return;

            if (endingStatus < SessionStatus.Normal || s_Publisher == null)
                return;

            IMessengerPacket exitReason = null;
            lock (s_SyncObject)
            {
                if (IsSessionEnding == false)
                {
                    //first time in - we need to kick off the packager if we're set to send on exit.
                    if (s_SendSessionsOnExit)
#pragma warning disable 4014
                        SendSessionData(); //we want this to be async so no need to wait.
#pragma warning restore 4014

                    ShutdownPublishEngine(); //self-checks if the publish engine doesn't exist and is therefore "shutdown"
                }

                CommonCentralLogic.DeclareSessionIsEnding(); // Flag that the session will be marked as ending.

                SessionStatus oldStatus = SessionSummary.Status;

                // Status can only progress one-way:  Running -> Normal -> Crashed.  Never backwards.
                if (endingStatus > oldStatus)
                {
                    SessionSummary.Status = endingStatus;

                    bool normalEnd = (endingStatus == SessionStatus.Normal);

                    const string captionFormat = "Session ending {0}{1}{2}";
                    const string noReasonTerminator = ".";
                    const string reasonDelimiter = ": ";
                    string state;
                    string formatArg1;
                    string formatArg2;

                    if (string.IsNullOrEmpty(reason))
                    {
                        formatArg1 = noReasonTerminator;
                        formatArg2 = string.Empty;
                    }
                    else
                    {
                        formatArg1 = reasonDelimiter;
                        formatArg2 = reason;
                    }

                    string descriptionFormat;
                    if (normalEnd)
                    {
                        state = "normally";
                        descriptionFormat = "Session state changed from {0} to {1}.\r\n"+
                            "Any further EndSession calls will not be reported unless declaring the session crashed.";
                    }
                    else
                    {
                        state = "as crashed";
                        descriptionFormat = "Session state changed from {0} to {1}.\r\n"+
                            "Any further EndSession calls will not be reported since sessions declared crashed can not be set back to normal.";
                    }

                    exitReason = MakeLogPacket(LogMessageSeverity.Verbose, ThisLogSystem, Category, sourceProvider,
                                               null, null, null, string.Format(captionFormat, state, formatArg1, formatArg2),
                                               descriptionFormat, oldStatus, endingStatus);
                }
            
                System.Threading.Monitor.PulseAll(s_SyncObject);
            }

            // Mark the session as a normal/crashed exit and tell the messaging system that the application is exiting.
            // This must be done outside the lock because we block until its done and we could deadlock!
            List<IMessengerPacket> batch = new List<IMessengerPacket>();
            batch.Add(new SessionClosePacket(SessionSummary.Status));
            if (exitReason != null)
            {
                batch.Add(exitReason);
            }
            batch.Add(new CommandPacket(MessagingCommand.ExitMode, SessionSummary.Status));

            //now that we've created our batch write them out.
            Write(batch.ToArray(), LogWriteMode.WaitForCommit);

            CommonCentralLogic.DeclareSessionHasEnded(); // Flag that the session has been marked as ended (normal or explicitly crashed).
        }

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally or explicitly crashed.
        /// </summary>
        /// <remarks><para>This will put the Gibraltar log into an ending state in which it will flush everything still
        /// in its queue and then switch to a background thread to process any further log messages.  All log messages
        /// submitted after this call will block the submitting thread until they are committed to disk, so that any
        /// foreground thread still logging final items will be sure to get them through before they exit.  This is
        /// called automatically when an ApplicationExit event is received, and can also be called directly (such as
        /// if that event would not function).</para>
        /// <para>If EndSession is never called, the log will reflect that the session must have crashed.</para></remarks>
        /// <param name="endingStatus">The explicit ending status to declare for this session, <see cref="SessionStatus.Normal">Normal</see>
        /// or <see cref="SessionStatus.Crashed">Crashed</see>.</param>
        /// <param name="skipFrames">The number of stack frames to skip out to find the original caller.</param>
        /// <param name="reason">A simple reason to declare why the application is ending as Normal or as Crashed, or may be null.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndSession(SessionStatus endingStatus, int skipFrames, string reason)
        {
            if (s_Initialized == false)
                return;

            EndSession(endingStatus, new MessageSourceProvider(skipFrames + 1, true), reason);
        }

        /// <summary>
        /// Called to activate the logging system.  If it is already active then this has no effect.
        /// </summary>
        /// <param name="configuration">Optional.  An initial default configuration to use instead of the configuration file.</param>
        /// <param name="skipFrames"></param>
        /// <param name="reason"></param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void StartSession(AgentConfiguration configuration, int skipFrames, string reason)
        {
            s_ExplicitStartSessionCalled = true;

            //if we're already initialized then there's nothing more to do so just return.
            if (s_Initialized)
                return;

            StartSession(configuration, new MessageSourceProvider(skipFrames + 1, true), reason);
        }

        /// <summary>
        /// Called to activate the logging system.  If it is already active then this has no effect.
        /// </summary>
        /// <param name="configuration">Optional.  An initial default configuration to use instead of the configuration file.</param>
        /// <param name="sourceProvider"></param>
        /// <param name="reason"></param>
        public static void StartSession(AgentConfiguration configuration, IMessageSourceProvider sourceProvider, string reason)
        {
            s_ExplicitStartSessionCalled = true;

            //if we're already initialized then there's nothing more to do so just return.
            if (s_Initialized)
                return;

            //otherwise we try to initialize and log a message.
            Initialize(configuration);

            if (s_Initialized)
            {
                if (string.IsNullOrEmpty(reason))
                    reason = "Session started";

                WriteMessage(LogMessageSeverity.Information, LogWriteMode.Queued, ThisLogSystem, Category, sourceProvider, null, null, null, reason, null );
            }            
        }

        /// <summary>
        /// Send sessions using packager
        /// </summary>
        /// <param name="criteria">Optional.  A session criteria to use</param>
        /// <param name="sessionMatchPredicate">Optional.  A session match predicate to use</param>
        /// <param name="asyncSend"></param>
        /// <returns>True if the send was processed, false if it was not due to configuration or another active send</returns>
        /// <remarks>Either a criteria or sessionMatchPredicate must be provided</remarks>
        public static async Task<bool> SendSessions(SessionCriteria? criteria, Predicate<ISessionSummary> sessionMatchPredicate, bool asyncSend)
        {
            if ((criteria.HasValue == false) && (sessionMatchPredicate == null))
            {
                Write(LogMessageSeverity.Information, Packager.LogCategory, "Send session command ignored due to no criteria specified", "A session match predicate wasn't provided so nothing would be selected to send, skipping the send.");
                return false;
            }

            bool result = false;
            Packager newPackager = null;
            lock(s_SyncObject)
            {
                if (s_ActivePackager == null)
                {
                    //we aren't doing a package, lets create a new one and process with it
                    newPackager = new Packager();
                    s_ActivePackager = newPackager; //this claims our spot
                }
            
                System.Threading.Monitor.PulseAll(s_SyncObject);
            }

            if (newPackager == null)
            {
                //someone else is sending
                Write(LogMessageSeverity.Information, Packager.LogCategory, "Send session command ignored due to ongoing send", "There is already a packager session send going on for the current application so this second request will be ignored to prevent interference.");
            }
            else
            {
                try
                {
                    string message = null;
                    if (IsHubSubmissionConfigured(ref message) && (await Packager.CanSendToServer().ConfigureAwait(false)).IsValid)
                    {
                        //we DON'T release the active packager here, we do it in the event handler.
                        ServerConfiguration config = s_RunningConfiguration.Server;
                        newPackager.EndSend += Packager_EndSend;

                        if (criteria.HasValue)
                            newPackager.SendToServer(criteria.Value, true, config.PurgeSentSessions, false, false, null, null, 0, false, null, null, asyncSend);
                        else
                            newPackager.SendToServer(sessionMatchPredicate, true, config.PurgeSentSessions, false, false, null, null, 0, false, null, null, asyncSend);

                        result = true;
                    }
                    else
                    {
                        //we can't send.
                        Write(LogMessageSeverity.Information, Packager.LogCategory, "Send session command ignored due to configuration", "Either the current configuration doesn't support server or email submission or the server is not available.");

                        //no good, dispose and clear the packager.
                        lock(s_SyncObject)
                        {
                            newPackager.Dispose();
                            s_ActivePackager = null;
                        
                            System.Threading.Monitor.PulseAll(s_SyncObject);
                        }
                    }
                }
                    // ReSharper disable EmptyGeneralCatchClause
                catch
                    // ReSharper restore EmptyGeneralCatchClause
                {
                    // That should never throw an exception, but it would not be good if it killed NotifyDispatchMain() and
                    // skipped setting the next notify time.  So we'll swallow any exceptions here.
                }

            }

            return result;
        }

        /// <summary>
        /// Set the SendSessionsOnExit setting.  (Should only be called through the SendSessionsOnExit property in Monitor.Log or Agent.Log.)
        /// </summary>
        /// <param name="value"></param>
        public static void SetSendSessionsOnExit(bool value)
        {
            bool valueChanged = false;
            bool suppressedSet = false;
            string suppressMessage = string.Empty;

            //We work with the session boolean in a lock to be fully MT safe, but we don't want to log in that lock
            //to ensure we can't deadlock, so we have to save our options to log after we release the lock.
            lock (s_SyncObject)
            {
                if (value != s_SendSessionsOnExit)
                {
                    if (value == false)
                    {
                        if (IsSessionEnding == false) // Can only cancel it if it hasn't been launched yet:  i.e. not already exiting.
                        {
                            valueChanged = true;
                            s_SendSessionsOnExit = false;
                        }
                        // Otherwise, we can't cancel it, so ignore the change (won't log anything, either).
                    }
                    else
                    {
                        //before we just put it to true, we better make sure we CAN send.
                        if (CanSendSessionsOnExit(ref suppressMessage))
                        {
                            valueChanged = true;
                            s_SendSessionsOnExit = true;
                            if (IsSessionEnding)
#pragma warning disable 4014
                                SendSessionData(); // Already in exit mode, we need to kick off the after-exit packager.
#pragma warning restore 4014
                            // Otherwise, it will be fired off when EndSession() is called if the property is still true.
                        }
                        else
                        {
                            //we're suppressing the attempt - we'll need to log that.
                            suppressedSet = true;
                        }
                    }
                }

                System.Threading.Monitor.PulseAll(s_SyncObject);
            }

            //now that we're not in the sync lock, go ahead and log what the deal is.
            if (valueChanged)
            {
                string caption = value ? "Session Data will be sent on exit" : "Session Data will not be sent on exit";
                string description = value ? "Session data will be submitted after the session exits." : "Session data will no longer be submitted after the session exits because the option was cleared.";
                WriteMessage(LogMessageSeverity.Information, LogWriteMode.Queued, 2, null, null, caption, description);
            }

            if (suppressedSet)
            {
                WriteMessage(LogMessageSeverity.Warning, LogWriteMode.Queued, 2, null, null, "Unable to Send Session Data on Exit",
                             "A request was made to send session data after the session exits, but the current configuration can't support it.\r\n{0}", suppressMessage);
            }
        }



        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Publish the provided raw packet to the stream.
        /// </summary>
        /// <remarks>This functionality is primarily for internal support of the various log listeners that support
        /// third party log systems.  This overload uses the default LogWriteMode.Queued.  To specify wait-for-commit
        /// behavior, use the overload with a LogWriteMode argument.</remarks>
        /// <param name="packet">The log packet to write</param>
        internal static void Write(IMessengerPacket packet)
        {
            //we explicitly are not checking initialized because we are part of the initialize.
            if (s_Publisher == null)
                return;

            // Wrap the packet as an array and pass it off to our more-general overload.
            Write(new[] { packet }, LogWriteMode.Queued); // Use normal Queued mode by default.
        }

        /// <summary>
        /// Publish a batch of raw packets to the stream, specifying the LogWriteMode to use.
        /// </summary>
        /// <remarks>This functionality is primarily for internal support of the various log listeners that support
        /// third party log systems.</remarks>
        /// <param name="packetArray">An array of the log packets to write.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        internal static void Write(IMessengerPacket[] packetArray, LogWriteMode writeMode)
        {
            //we explicitly are not checking initialized because we are part of the initialize.
            if (s_Publisher == null)
                return;

            if (packetArray == null || packetArray.Length == 0)
                return;

            //before we publish, are these log messages? if so we have to count them
            foreach (IMessengerPacket packet in packetArray)
            {
                LogMessagePacket logPacket = packet as LogMessagePacket;
                if (logPacket != null)
                    s_SessionStartInfo.UpdateMessageStatistics(logPacket);
            }

            if (writeMode == LogWriteMode.WaitForCommit)
            {
                s_Publisher.Publish(packetArray, true);
            }
            else
            {
                s_Publisher.Publish(packetArray, false);
            }
        }

        /// <summary>
        /// Create a complete log message WITHOUT sending it to the Gibraltar central log.
        /// </summary>
        /// <remarks>This method is used internally to construct a complete LogMessagePacket, which can then be
        /// bundled with other packets (in an array) to be submitted to the log as a batch.  This method ONLY
        /// supports being invoked on the same thread which is originating the log message.</remarks>
        /// <param name="severity">The severity enum value of the log message.</param>
        /// <param name="logSystem">The name of the originating log system, such as "Trace", "Log4Net",
        /// or "Gibraltar".</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which can be a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <param name="userName">The effective username associated with the execution task which
        /// issued the log message.</param>
        /// <param name="exception">An Exception object attached to this log message, or null if none.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted description string.</param>
        internal static IMessengerPacket MakeLogPacket(LogMessageSeverity severity, string logSystem, string category,
                                                       IMessageSourceProvider sourceProvider, string userName, Exception exception,
                                                       string detailsXml, string caption, string description, params object[] args)
        {
            if (s_Initialized == false)
                return null;

            LogMessagePacket packet = new LogMessagePacket();
            // We require that we're still on the same thread.
            ThreadInfo threadInfo = GetCurrentThreadInfo(); // Make sure the info for the current thread is in our cache
            packet.ThreadInfoPacket = threadInfo.Packet; // Set the ThreadInfoPacket property in the LogMessagePacket
            packet.ThreadIndex = threadInfo.ThreadIndex; // This is how we actually identify the ThreadInfo we mean!
            packet.ThreadId = threadInfo.ThreadId; // This is used by older code to identify the thread, but it's not unique!

            // Some sanity-checks against null arguments.
            if (string.IsNullOrEmpty(logSystem))
            {
                logSystem = "Unknown";
            }

            if (string.IsNullOrEmpty(category))
            {
                category = GeneralCategory;
            }

            if (s_RunningConfiguration.Publisher.EnableAnonymousMode)
            {
                userName = string.Empty; // For now blank all user name data in anonymous mode.
            }
            else
            {
                var userPrincipal = ClaimsPrincipal.Current;
                if (string.IsNullOrEmpty(userName))
                {
                    try
                    {
                        // These are wrapped in a try/catch because supposedly they could throw an exception.
                        var threadIdentity = (userPrincipal != null) ? userPrincipal.Identity : null;
                        if (threadIdentity != null)
                        {
                            userName = threadIdentity.Name;
                        }
                        else
                        {
                            // Passing true means we get nothing if not actually impersonating (fast?).
                            WindowsIdentity windowsIdentity;
                            try
                            {
                                windowsIdentity = WindowsIdentity.GetCurrent(true); // Note: May not work in Mono?
                            }
                            catch
                            {
                                windowsIdentity = null;
                            }

                            if (windowsIdentity != null)
                            {
                                userName = windowsIdentity.Name;
                            }
                        }
                    }
                    catch (System.Security.SecurityException)
                    {
                        userName = null;
                    }
                }

                if (string.IsNullOrEmpty(userName))
                {
                    // Get from session info.
                    userName = s_SessionStartInfo.FullyQualifiedUserName;
                }

                //if our principal and our explicit user name don't agree then ditch the principal, it won't be valid.
                if ((ReferenceEquals(userPrincipal, null) == false)
                    && (ReferenceEquals(userPrincipal.Identity, null) == false))
                {
                    userPrincipal = string.Equals(userName, userPrincipal.Identity.Name, StringComparison.OrdinalIgnoreCase)
                        ? userPrincipal
                        : null;
                }

                packet.UserPrincipal = userPrincipal;
            }

            string formattedDescription;
            if (args == null || args.Length == 0)
            {
                // Since we aren't calling SafeFormat(), we have to protect against a null message ourselves here.
                formattedDescription = description ?? string.Empty;
            }
            else
            {
                // Note: This will handle the case of a null message, so let it have the original message to report.
                formattedDescription = CommonCentralLogic.SafeFormat(CultureInfo.CurrentCulture, description, args);
            }

            NormalizeCaptionDescription(ref caption, ref formattedDescription); // Fix line breaks and extract Caption if needed.

            packet.Severity = severity;
            packet.LogSystem = logSystem;
            packet.CategoryName = category;
            packet.UserName = userName;
            packet.Caption = caption;
            packet.Description = formattedDescription;
            packet.Details = detailsXml;
            packet.SetException(exception);
            packet.SetSourceInfo(sourceProvider);

            return packet;
        }

        private static ThreadInfo GetCurrentThreadInfo()
        {
            if (s_Initialized == false)
                return null;

            //see if we already have a thread info object for the requested thread Id
            if (t_CurrentThreadInfo == null)
            {
                //we don't have it, go and create it.  We rely on still being on the
                //thread in question so we can get OUR thread information
                t_CurrentThreadInfo = new ThreadInfo();
            }

            return t_CurrentThreadInfo;
        }

        /// <summary>
        /// Indicates if the calling thread is part of the log initialization process
        /// </summary>
        internal static bool ThreadIsInitializer
        {
            get { return t_ThreadIsInitializer; }
            set { t_ThreadIsInitializer = value; }
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Get the full file name and path to where the packager would need to be for us to use it.
        /// </summary>
        /// <returns></returns>
        private static string GetPackagerFileNamePath()
        {
            //assume the packager assembly is in our directory, what path would that be?
            string proposedPath = typeof(Log).GetTypeInfo().Assembly.CodeBase;

            //get rid of the standard code base prefix if it's there.
            if (proposedPath.StartsWith(@"file:", StringComparison.OrdinalIgnoreCase))
            {
                proposedPath = proposedPath.Substring(5);

                //now remove all backslashes at this point, since they can't mean a UNC path.
                proposedPath = proposedPath.TrimStart(new char[] {'\\', '/'});
            }

            proposedPath = Path.GetDirectoryName(proposedPath);

            //the packager we're looking for depends on the version of .NET
            string packagerExeName;
            if (SessionSummary.RuntimeVersion.Major >= 4)
            {
                packagerExeName = "Gibraltar.Packager.NET40.exe";
            }
            else
            {
                packagerExeName = "Gibraltar.Packager.exe";
            }

            string packagerFileNamePath = Path.Combine(proposedPath, packagerExeName);

            return packagerFileNamePath;
        }

        /// <summary>
        /// Attempt to create a process to send the data for the current application using the packager.
        /// </summary>
        private static async Task SendSessionData()
        {
            try
            {
                string packagerFileNamePath = GetPackagerFileNamePath();

                ProcessStartInfo packagerStartInfo = new ProcessStartInfo(packagerFileNamePath);
                packagerStartInfo.CreateNoWindow = true;
                packagerStartInfo.UseShellExecute = false;
                packagerStartInfo.WorkingDirectory = Path.GetDirectoryName(packagerFileNamePath);

                //we need our process Id to signal the packager to wait for us to exit.
                int ourPid = Process.GetCurrentProcess().Id;
                StringBuilder argumentString = new StringBuilder(2048);
                
                //there are two ways to build the command:  either we're sending to the server or to email.
                string message = string.Empty; //we don't use it, but we have to send it
                if ((IsHubSubmissionConfigured(ref message)) && (await Packager.CanSendToServer().ConfigureAwait(false)).IsValid)
                {
                    argumentString.AppendFormat("/s /w \"{0}\" /m server /p \"{1}\" /a \"{2}\" ",
                                                ourPid, s_SessionStartInfo.Product, s_SessionStartInfo.Application);

                    //now see if we're sending customer or server information.
                    ServerConfiguration server = s_RunningConfiguration.Server;
                    if (server.UseGibraltarService)
                    {
                        argumentString.AppendFormat("/customer \"{0}\" ", server.CustomerName);
                    }
                    else
                    {
                        argumentString.AppendFormat("/server \"{0}\" ", server.Server);

                        if (server.UseSsl)
                        {
                            argumentString.Append("/ssl \"true\" ");
                        }

                        if (server.Port != 0)
                        {
                            argumentString.AppendFormat("/port \"{0}\" ", server.Port);
                        }

                        if (string.IsNullOrEmpty(server.ApplicationBaseDirectory) == false)
                        {
                            argumentString.AppendFormat("/directory \"{0}\" ", server.ApplicationBaseDirectory);
                        }

                        if (string.IsNullOrEmpty(server.Repository) == false)
                        {
                            argumentString.AppendFormat("/repository \"{0}\" ", server.Repository);
                        }
                    }

                    //and determine if we should purge sent sessions...
                    if (server.PurgeSentSessions)
                    {
                        argumentString.AppendFormat("/purgeSentSessions \"true\" ");
                    }
                }
                else
                {
#if DEBUG
                    Write(LogMessageSeverity.Information, "Gibraltar.Agent.Send Session", "Can not send via Server, attempting via email",
                          "When preparing to send session data upon exit as requested a check of server configuration and server "+
                          "connectivity failed:\r\n{0}", message);
#endif

                    argumentString.AppendFormat("/s /w \"{0}\" /m email /p \"{1}\" /a \"{2}\" /d \"{3}\" ",
                                                ourPid, s_SessionStartInfo.Product, s_SessionStartInfo.Application, s_RunningConfiguration.Packager.DestinationEmailAddress);

                    if (!string.IsNullOrEmpty(s_RunningConfiguration.Packager.FromEmailAddress))
                    {
                        argumentString.AppendFormat(" /f \"{0}\" ", s_RunningConfiguration.Packager.FromEmailAddress);
                    }
                }

                //finally, did they override the file path we're supposed to use?
                if (string.IsNullOrEmpty(s_RunningConfiguration.SessionFile.Folder) == false)
                {
                    argumentString.AppendFormat("/folder \"{0}\" ", s_RunningConfiguration.SessionFile.Folder);
                }

                packagerStartInfo.Arguments = argumentString.ToString();

                Process.Start(packagerStartInfo); //and we don't care what happens so once we've launched it we are outta here.
#if DEBUG
                // Only log this for a Debug build?
                Write(LogMessageSeverity.Information, "Gibraltar.Agent.Send Session", "Packager process started to send session after exit",
                      "Gibraltar.Packager.exe was launched to submit this session data after this process ({0}) exits.", ourPid);
#endif
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                // Only log this for a Debug build?
                Write(LogMessageSeverity.Information, LogWriteMode.Queued, ex, "Gibraltar.Agent.Send Session", "Error sending session on exit",
                      "An error occurred while attempting to submit session data upon exit as requested, " +
                      "and the session could not be sent.  Check that configuration of email and server " +
                      "are each valid or disabled, and check the status of the Loupe server or subscription.");
                DebugBreak();
#endif
            }
        }

        private static void NormalizeCaptionDescription(ref string caption, ref string description)
        {
            if (description == null)
                description = string.Empty; // Must be a legal string.

            if (description.Length > 0)
            {
                char lastChar = description[description.Length - 1];
                if (lastChar != '\n' && lastChar != '\r')
                    description += LineBreakString; // Make sure Description ends in a line break, unless it's empty.
            }

            string[] descriptionLines = description.Split(LineBreakTokens, StringSplitOptions.None);
            int lineCount = descriptionLines.Length;

            if (caption == null)
            {
                // Need to extract the Caption, leave off line-break and trim trailing whitespace.
                caption = (lineCount > 0) ? descriptionLines[0].TrimEnd() : string.Empty;

                // Now re-join the Description with optimal line break strings, including the one at the end automatically.
                description = (lineCount > 1) ? string.Join(LineBreakString, descriptionLines, 1, lineCount - 1) : string.Empty;
            }
            else
            {
                // Caption is already a valid string, so we don't extract it from description, just trim trailing whitespace.
                caption = caption.TrimEnd();

                // And just normalize the Description's line breaks, including the one at the end automatically.
                description = string.Join(LineBreakString, descriptionLines);
            }
        }

        /// <summary>
        /// Perform the critical central initialization and indicate if we should be active or not
        /// </summary>
        /// <returns>True if initialization was completed and logging can now commence, false otherwise.</returns>
        private static bool OnInitialize(AgentConfiguration configuration)
        {
            //make sure that we don't ever run again once we're initialized, that would be bad.
            if (s_Initialized)
                return true;

            //Find out if we are going to be able to initialize or not.
            bool suppressInitialize = false;
            InitializingEventHandler tempEvent = Initializing;

            AgentConfiguration initialConfiguration = configuration ?? new AgentConfiguration();
            if (tempEvent != null)
            {
                //we need to see if our callers will let us initialize.
                LogInitializingEventArgs eventArgs = new LogInitializingEventArgs(initialConfiguration);
                try
                {
                    tempEvent(null, eventArgs);
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
#if DEBUG
                    DebugBreak();
#endif
                    //treat a fail as a cancel, so there's really nothing to do here - can't log it.
                    eventArgs.Cancel = true;
                }

                suppressInitialize = eventArgs.Cancel;
            }

            if (suppressInitialize)
                return false; //we are not going to start up, so we stay shut down.

            //Now that we aren't going to cancel, go ahead and store this as our running configuration and complete the initialization.

            //sanitize the configuration
            try
            {
                initialConfiguration.Sanitize();
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                    DebugBreak();
#endif
            }

            s_RunningConfiguration = initialConfiguration;

            //if we're in debug mode then force the central silent mode option.
            if (s_RunningConfiguration.Publisher.EnableDebugMode)
                Log.SilentMode = false;

            s_SessionStartInfo = new SessionSummary(s_RunningConfiguration);

            if (s_RunningConfiguration.SessionFile.Enabled)
                s_Repository = new LocalRepository(s_SessionStartInfo.Product, s_RunningConfiguration.SessionFile.Folder);

            //initialize our publisher
            string sessionName = string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", s_SessionStartInfo.Product,
                                               s_SessionStartInfo.Application,
                                               s_SessionStartInfo.ApplicationVersion,
                                               DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture));

            s_Publisher = new Publisher(sessionName, s_RunningConfiguration, s_SessionStartInfo);

            //record our session start info right now so we're sure it's the first packet we have.
            Write(s_SessionStartInfo.Packet);
            
            //initialize the listener architecture.
            Listener.Initialize(s_RunningConfiguration, true);

            //and we need to load up the session publisher if it's enabled.
            StartPublishEngine(); //this checks to see if it can start based on configuration

            //and now we're initialized!
            return true;
        }

        private static void EnsureSummaryIsAvailable()
        {
            //make sure we aren't in a race condition starting to initialize...
            if ((s_Initialized == false) 
                && ((s_RunningConfiguration == null) || (s_SessionStartInfo == null)))
            {
                //it's initializing, but not yet complete - we need to stall until it is.
                lock (s_InitializingLock)
                {
                    while (s_Initializing) //the status is still indeterminate.
                    {
                        System.Threading.Monitor.Wait(s_InitializingLock);
                    }

                    //OK, right now we HAVE the initializing lock so no thread can sneak in and have us damage what it's up to.
                    s_RunningConfiguration = new AgentConfiguration();
                    s_SessionStartInfo = new SessionSummary(s_RunningConfiguration);

                    System.Threading.Monitor.PulseAll(s_InitializingLock);
                }
            }
        }

        /// <summary>
        /// Determines the correct Auto Send Consent scope for the current configuration
        /// </summary>
        /// <param name="productName"></param>
        /// <param name="applicationName"></param>
        private static void GetConsentScope(out string productName, out string applicationName)
        {
            bool restrictToApplication = true;

            //if the user has configured auto send to work across all applications then we don't restrict to the application.
            ServerConfiguration server = Configuration.Server;
            if (server.Enabled && server.AutoSendSessions && server.SendAllApplications)
            {
                restrictToApplication = false;
            }

            productName = SessionSummary.Product;
            applicationName = restrictToApplication ? SessionSummary.Application : null;
        }

        /// <summary>
        /// If the configuration allows publishing then starts our one publish engine, creating it if necessary
        /// </summary>
        private static void StartPublishEngine()
        {
            try
            {
                //we try to keep the lock for the shortest time period we can.
                RepositoryPublishEngine publishEngine = null;
                lock (s_SyncObject)
                {
                    if ((s_RunningConfiguration.Server.Enabled) && (s_RunningConfiguration.Server.AutoSendSessions))
                    {
                        if (s_PublishEngine == null)
                        {
                            s_PublishEngine = new RepositoryPublishEngine(s_Publisher, s_RunningConfiguration);
                        }

                        publishEngine = s_PublishEngine;

                        if (s_RunningConfiguration.Server.AutoSendOnError)
                        {
                            var notifier = MessageAlertNotifier; //poking this creates our background threads.
                        }
                    }

                    System.Threading.Monitor.PulseAll(s_SyncObject);
                }

                if (publishEngine != null)
                {
                    publishEngine.Start();
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                DebugBreak();
#endif

            }
        }

        /// <summary>
        /// Shutdown the publish engine if it exists without waiting for it to complete.
        /// </summary>
        private static void ShutdownPublishEngine()
        {
            RepositoryPublishEngine publishEngine;
            lock (s_SyncObject)
            {
                publishEngine = s_PublishEngine;
                System.Threading.Monitor.PulseAll(s_SyncObject);
            }

            if (publishEngine != null)
            {
                try
                {
                    publishEngine.Stop(false);
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
#if DEBUG
                    DebugBreak();
#endif

                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Used to get rid of our active packager handle when it's done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void Packager_EndSend(object sender, PackageSendEventArgs args)
        {
            lock(s_SyncObject)
            {
                Packager caller = (Packager)sender;

                if (ReferenceEquals(caller, s_ActivePackager))
                {
                    //outstanding! we can clear our pointer now.
                    s_ActivePackager.EndSend -= Packager_EndSend;
                    s_ActivePackager = null;
                }

                System.Threading.Monitor.PulseAll(s_SyncObject);
            }
        }

        #endregion
    }
}
