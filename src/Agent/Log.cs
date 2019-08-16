using System;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Gibraltar.Agent.Internal;
using Gibraltar.Monitor;
using Gibraltar.Server.Client;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using IServerAuthenticationProvider = Gibraltar.Agent.Net.IServerAuthenticationProvider;
using MessageSourceProvider=Gibraltar.Agent.Internal.MessageSourceProvider;
using MetricDefinitionCollection=Gibraltar.Agent.Metrics.Internal.MetricDefinitionCollection;

namespace Gibraltar.Agent
{
    /// <summary>This static class is the primary API for logging with the Loupe Agent.</summary>
    /// <remarks>
    /// 	<para>
    ///         This Log class provides the API for
    ///         directly logging to Loupe and for receiving log messages from logging
    ///         systems such as log4net. Messages sent directly to Loupe will not go
    ///         through System.Diagnostics.Trace and will not be seen by trace listeners or by
    ///         other logging systems, but can take direct advantage of Loupe's logging
    ///         features.
    ///     </para>
    /// 	<para>The logging API provides different groups of methods for different levels of
    ///     simplicity verses flexibility.</para>
    /// 	<list type="bullet">
    /// 		<item><strong>Trace Methods:</strong> Designed to mirror the Trace class built
    ///         into .NET, these provide the simplest API and are a direct substitute for
    ///         existing calls that use the Trace method (simply change the class name from
    ///         System.Diagnostics.Trace to Gibraltar.Agent.Log)</item>
    /// 		<item><strong>Severity Methods:</strong> A method for each Loupe severity
    ///         from Critical (the most severe) to Verbose (the least). These provide a full
    ///         featured API for logging directly to Loupe as part of your
    ///         application.</item>
    /// 		<item><strong>Write Methods:</strong> Used to forward log messages into the
    ///         Loupe Agent from an external logging system or logging aggregation class.
    ///         These expose the most capability but are generally unnecessary outside of the
    ///         message forwarding scenario.</item>
    /// 	</list>
    /// 	<para><strong>Trace Methods</strong></para>
    /// 	<para>
    ///         The various Trace methods provide a quick way to record a log message at a
    ///         chosen severity level with the fewest arguments to manage. These methods
    ///         include direct replacements for Trace.TraceInformation,
    ///         Trace.TraceWarning, and Trace.TraceError,
    ///         as well as a Trace() call (replacing the use of Trace.Write and Trace.WriteLine for logging
    ///         Verbose messages).
    ///     </para>
    /// 	<para>
    ///         In addition to the direct replacement calls for the Trace API an additional
    ///         <see cref="TraceCritical(string, object[])">TraceCritical Method</see> method
    ///         was added for logging fatal errors.
    ///     </para>
    /// 	<para>Each of these methods also provides an overload which accepts an Exception
    ///     object as the first parameter. By providing the exception object with the method,
    ///     extended information about the exception is recorded which can significantly
    ///     improve the utility of the log information without requiring it to be included in
    ///     the message.</para>
    /// 	<para>
    ///         When using Trace exclusively, it's recommended that you include a call to
    ///         Trace.Close at the very end of your application's execution. This will ensure
    ///         that all Trace Listeners are shut down correctly, and the Agent will use this
    ///         to record that the session closed normally and start its shutdown procedure by
    ///         automatically calling <see cref="EndSession()">Log.EndSession</see>.
    ///     </para>
    /// 	<para>For more information, see <a href="Logging_Trace.html">Developer's Reference
    ///     - Logging - Using with Trace</a>.</para>
    /// 	<para><strong>Severity Methods</strong></para>
    /// 	<para>The Severity Methods (named after each severity level) provide the most
    ///     commonly-needed features of Loupe's logging capability. In order from most to
    ///     least severe, these are:</para>
    /// 	<list type="bullet">
    /// 		<item>
    /// 			<see cref="Critical(string, string, string, object[])">Log.Critical</see>
    /// 		</item>
    /// 		<item>
    /// 			<see cref="Error(string, string, string, object[])">Log.Error</see>
    /// 		</item>
    /// 		<item>
    /// 			<see cref="Warning(string, string, string, object[])">Log.Warning</see>
    /// 		</item>
    /// 		<item>
    /// 			<see cref="Information(string, string, string, object[])">Log.Information</see>
    /// 		</item>
    /// 		<item>
    /// 			<see cref="Verbose(string, string, string, object[])">Log.Verbose</see>
    /// 		</item>
    /// 	</list>
    /// 	<para>Each of these methods in their simplest form takes Category, Caption, and
    ///     Description instead of just a single Message to take best advantage of Loupe's
    ///     ability to group similar messages for analysis and reporting. Additional overloads
    ///     allow an Exception object to be specified (regardless of severity) and allow the
    ///     message to be committed to disk in the session file before the thread's execution
    ///     continues.</para>
    /// 	<para>For more advanced usage, each Severity method has a corresponding Detail
    ///     method that supports recording an XML document string with details for more
    ///     sophisticated examination. This information can be formatted in the Loupe
    ///     Analyst to provide end users with extended drill-in data about a particular
    ///     situation. Because the logging data is highly compressed (typically 80 percent or
    ///     more for strings over 5kb), it's safe to record XML documents without overwhelming
    ///     the session files.</para>
    /// 	<para>For more information, see <a href="Logging_DirectLogging.html">Developer's
    ///     Reference - Logging Directly to Loupe</a>.</para>
    /// 	<para><strong>Write Method</strong></para>
    /// 	<para>If you are already using a different logging system than Trace or the
    ///     Loupe Agent you can forward messages from it into the Agent by using the Write
    ///     method. The two overloads of the Write method are designed to support both full
    ///     featured external log systems that can capture extended information, origin
    ///     information for the log message, and even override the user identity.</para>
    /// 	<para>
    ///         Another common scenario supported by Write is an existing application with a
    ///         central class that all logging is being routed through. The <see cref="Write(LogMessageSeverity, string, int, Exception, LogWriteMode, string, string, string, string, object[])">
    ///         Log.Write</see> method is designed to support this easily while still allowing
    ///         you to take advantage of the safe formatting and origin determination
    ///         capabilities of the Loupe Agent.
    ///     </para>
    /// 	<para>For more information, see <a href="Logging_ExternalLogSystems.html">Developer's Reference - Logging - Using with
    ///     External Log Systems</a>.</para>
    /// 	<para><strong>Starting a Session</strong></para>
    /// 	<para>
    ///         The Log object will attempt to start the first time it is used, or any time a
    ///         call is made to <see cref="StartSession()">StartSession</see>. When it starts, it
    ///         will raise its <see cref="Initializing">Log.Initializing</see> event to allow
    ///         for configuration overrides to be done in code and for the startup sequence to
    ///         be canceled. If the startup is canceled, all API functions continue to work but
    ///         no Agent functionality is available. This is a high speed mode that allows any
    ///         agent overhead to be removed from the process without altering the control flow
    ///         or recompiling the application.
    ///     </para>
    /// 	<para><strong>Ending a Session</strong></para>
    /// 	<para>
    ///         It's a best practice at the end of your application's normal execution path to
    ///         include a call to <see cref="EndSession()">Log.EndSession</see>. This performs
    ///         several functions:
    ///     </para>
    /// 	<list type="number">
    /// 		<item>It marks the session as ending normally. Regardless of how the process
    ///         exits after EndSession is called, it will not be considered crashed.</item>
    /// 		<item>All queued information is flushed to disk and all subsequent write
    ///         requests are handled as WaitForCommit requests to ensure that no messages are
    ///         lost.</item>
    /// 		<item>Various internal changes are made to ensure that the process will exit
    ///         quickly. If no EndSession call is made, the Agent may keep the process alive
    ///         even if it normally would have exited.</item>
    /// 	</list>
    /// 	<para>You can safely call EndSession multiple times.</para>
    /// 	<para><strong>Configuring the Agent</strong></para>
    /// 	<para>
    ///         The agent can be configured in the application configuration file, through
    ///         code, or both. To configure the agent in code you must subscribe to the
    ///         <see cref="Initializing">Log.Initializing</see> event before the agent is
    ///         started and then manipulate the <see cref="AgentConfiguration">Agent</see> configuration
    ///         object and its child objects. If any configuration was supplied in the
    ///         application configuration file that will have already been loaded into the
    ///         configuration objects when the event is raised.
    ///     </para>
    /// </remarks>
    /// <seealso cref="!:Logging_Trace.html" cat="Developer's Reference">Logging - Using with Trace</seealso>
    /// <seealso cref="!:Logging_ExternalLogSystems.html" cat="Developer's Reference">Logging - Using with External Log Systems</seealso>
    /// <seealso cref="!:Logging_DirectLogging.html" cat="Developer's Reference">Logging - Using Loupe as a Log System</seealso>
    public static class Log
    {
        /// <summary>The file extension (without period) for a Loupe Package File.</summary>
        public const string PackageExtension = Monitor.Log.PackageExtension;

        private const string ThisLogSystem = Monitor.Log.ThisLogSystem;
        private const string Category = Monitor.Log.Category;
        private const string ExceptionCategory = Monitor.Log.ExceptionCategory;
        private const Monitor.LogWriteMode Queued = Monitor.LogWriteMode.Queued;

        private static readonly object s_SyncLock = new object();

        // Create a wrapped session summary so it's available to users of the agent.
        private static SessionSummary s_SessionSummary;
        private static readonly MetricDefinitionCollection s_MetricDefinitions = new MetricDefinitionCollection(Monitor.Log.Metrics);

        private static event MessageEventHandler s_MessageEvent;
        private static event MessageAlertEventHandler s_MessageAlertEvent;
        private static event MessageFilterEventHandler s_MessageFilterEvent;
        private static readonly object s_MessageEventLock = new object(); // Locks add/remove of Message event subscriptions.

        // Disable the never-used warning
#pragma warning disable 169
        // Initialize the real log object (this causes listeners to register, all kinds of stuff).
        private static bool s_InitializedComplete;
#pragma warning restore 169

        private static IServerAuthenticationProvider s_ServerAuthenticationProvider;

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

        /// <summary>
        /// Handler type for a message event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void MessageEventHandler(object sender, LogMessageEventArgs e);

        /// <summary>
        /// Handler type for a message filter event.
        /// </summary>
        /// <param name="sender">The sender of this event (the LiveLogViewer control instance or null for the main Loupe Live Viewer)</param>
        /// <param name="e">The Message Filter Event Args.</param>
        public delegate void MessageFilterEventHandler(object sender, LogMessageFilterEventArgs e);

        /// <summary>
        /// Handler type for a message alert event.
        /// </summary>
        /// <param name="sender">The sender of this event.</param>
        /// <param name="e">The Message Alert Event Args.</param>
        public delegate void MessageAlertEventHandler(object sender, LogMessageAlertEventArgs e);

        /// <summary>
        /// Raised to alert subscribers to any Warning, Error, or Critical log messages, with optional throttling.
        /// </summary>
        /// <remarks>This event can be raised after a log message of Warning, Error, or Critical severity has been committed.
        /// This allows the client to take action such as to set the <see cref="SendSessionsOnExit">SendSessionsOnExit</see>
        /// flag or request that the active session be sent immediately.  Setting the
        /// <see cref="LogMessageAlertEventArgs.MinimumDelay">MinimumDelay</see>
        /// property in the event args will specify a minimum time before the event may be raised again (only the last value
        /// set by any subscriber takes effect).  Any new qualifying log messages received during a required wait period will
        /// be queued and included as a batch in the next event, unless there is an excessive number in which case later
        /// ones will be ignored.  A number of handy read-only properties provide quick summaries for simple filtering
        /// (e.g. if the client only cares about Error and Critical messages, not Warnings).
        /// </remarks>
        public static event MessageAlertEventHandler MessageAlert
        {
            add
            {
                if (value == null)
                    return;

                lock (s_MessageEventLock)
                {
                    if (s_MessageAlertEvent == null)
                    {
                        Monitor.Log.MessageAlertNotifier.NotificationEvent += MessageAlertNotifierOnNotificationEvent;
                    }

                    s_MessageAlertEvent += value;
                }
            }
            remove
            {
                if (value == null)
                    return;

                lock (s_MessageEventLock)
                {
                    if (s_MessageAlertEvent == null)
                        return; // Already empty, no subscriptions to remove.

                    s_MessageAlertEvent -= value;

                    if (s_MessageAlertEvent == null)
                    {
                        Monitor.Log.MessageAlertNotifier.NotificationEvent -= MessageAlertNotifierOnNotificationEvent;
                    }
                }
            }
        }

        /// <summary>
        /// Raised to publish log messages as they are committed
        /// </summary>
        /// <remarks>This event is raised after a log message is committed.  Any new qualifying log messages received during a required wait period will
        /// be queued and included as a batch in the next event, unless there is an excessive number in which case later
        /// ones will be ignored. </remarks>
        public static event MessageEventHandler MessagePublished
        {
            add
            {
                if (value == null)
                    return;

                lock (s_MessageEventLock)
                {
                    if (s_MessageEvent == null)
                    {
                        Monitor.Log.MessageNotifier.NotificationEvent += MessageNotifierOnNotificationEvent;
                    }

                    s_MessageEvent += value;
                }
            }
            remove
            {
                if (value == null)
                    return;

                lock (s_MessageEventLock)
                {
                    if (s_MessageEvent == null)
                        return; // Already empty, no subscriptions to remove.

                    s_MessageEvent -= value;

                    if (s_MessageEvent == null)
                    {
                        Monitor.Log.MessageNotifier.NotificationEvent -= MessageNotifierOnNotificationEvent;
                    }
                }
            }
        }

        static Log()
        {
            //we have to bind to the internal log event and create the object.
            Monitor.Log.Initializing += LogOnInitializing;
            CachedCredentialsManager.CredentialsRequired += CachedCredentialsManagerOnCredentialsRequired;

            //make sure that we put logging in silent mode - we're the agent!
            Monitor.Log.SilentMode = true;
        }

        #region Public Properties and Methods

        /// <summary>
        /// The common information about the active log session.
        /// </summary>
        public static SessionSummary SessionSummary
        {
            get
            {
                EnsureSummaryIsAvailable();
                
                return s_SessionSummary;
            }
        }

        /// <summary>
        /// An implementation of IApplicationUserProvider to capture Application User details from an IPrinciple
        /// </summary>
        public static IApplicationUserProvider ApplicationUserProvider
        {
            get => Monitor.Log.ApplicationUserProvider;
            set => Monitor.Log.ApplicationUserProvider = value;
        }

        /// <summary>
        /// An implementation of IPrincipalResolver to determine the IPrinciple for each log message and metric sample
        /// </summary>
        public static IPrincipalResolver PrincipalResolver
        {
            get => Monitor.Log.PrincipalResolver;
            set => Monitor.Log.PrincipalResolver = value;
        }

        /// <summary>
        /// Indicates if the agent should package &amp; send sessions for the current application after this session exits.
        /// </summary>
        /// <remarks>
        /// When true the system will automatically spawn the packager to send all unsent
        /// sessions for the current application. This is only supported if the packager is enabled
        /// and configured to submit sessions via Loupe Server and/or to send packages via email.
        /// Loupe Server will be used by preference if available, but email can be used as a fall-back
        /// option.  If sessions can't be sent on exit, the property can still be set but will stay false.
        /// No exception will be thrown.
        /// </remarks>
        public static bool SendSessionsOnExit
        {
            get { return Monitor.Log.SendSessionsOnExit; }
            [MethodImplAttribute(MethodImplOptions.NoInlining)] 
            set
            {
                //don't do jack if we aren't initialized.
                if (Monitor.Log.IsLoggingActive() == false)
                    return;

                Monitor.Log.SetSendSessionsOnExit(value); // Jump to the method (not property) for correct source attribution.
            }
        }

        /// <summary>
        /// The version information for the Loupe Agent.
        /// </summary>
        public static Version AgentVersion
        {
            get { return Monitor.Log.AgentVersion; }
        }

        //
        // VERBOSE
        //

        /// <summary>
        /// Write a categorized Verbose message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Verbose(string category, string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Verbose message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Verbose(Exception exception, string category, string caption, string description,
                                   params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                Queued, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Verbose message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Verbose(LogWriteMode writeMode, string category, string caption, string description,
                                   params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Verbose message directly to the Loupe log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Verbose(Exception exception, LogWriteMode writeMode, string category, string caption,
                                   string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Verbose message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.  Also see the Write() method for when XML details
        /// are not needed.  This method is otherwise similar to Write().</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void VerboseDetail(string detailsXml, string category, string caption, string description,
                                         params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Verbose message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void VerboseDetail(Exception exception, string detailsXml, string category, string caption,
                                         string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                Queued, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Verbose message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void VerboseDetail(LogWriteMode writeMode, string detailsXml, string category, string caption,
                                         string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Verbose message directly to the Loupe log with an optional attached Exception and
        /// specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void VerboseDetail(Exception exception, LogWriteMode writeMode, string detailsXml, string category,
                                         string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        //
        // INFORMATION
        //

        /// <summary>
        /// Write a categorized Information message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Information(string category, string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Information message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Information(Exception exception, string category, string caption, string description,
                                       params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                Queued, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Information message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Information(LogWriteMode writeMode, string category, string caption, string description,
                                       params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Information message directly to the Loupe log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Information(Exception exception, LogWriteMode writeMode, string category, string caption,
                                       string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Information message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.  Also see the Write() method for when XML details
        /// are not needed.  This method is otherwise similar to Write().</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void InformationDetail(string detailsXml, string category, string caption, string description,
                                             params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Information message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void InformationDetail(Exception exception, string detailsXml, string category, string caption,
                                             string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                Queued, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Information message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void InformationDetail(LogWriteMode writeMode, string detailsXml, string category, string caption,
                                             string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Information message directly to the Loupe log with an optional attached Exception and
        /// specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void InformationDetail(Exception exception, LogWriteMode writeMode, string detailsXml, string category,
                                             string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        //
        // WARNING
        //

        /// <summary>
        /// Write a categorized Warning message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Warning(string category, string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Warning message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Warning(Exception exception, string category, string caption, string description,
                                   params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                Queued, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Warning message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Warning(LogWriteMode writeMode, string category, string caption, string description,
                                   params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Warning message directly to the Loupe log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Warning(Exception exception, LogWriteMode writeMode, string category, string caption,
                                   string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Warning message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.  Also see the Write() method for when XML details
        /// are not needed.  This method is otherwise similar to Write().</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void WarningDetail(string detailsXml, string category, string caption, string description,
                                         params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Warning message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void WarningDetail(Exception exception, string detailsXml, string category, string caption,
                                         string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                Queued, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Warning message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void WarningDetail(LogWriteMode writeMode, string detailsXml, string category, string caption,
                                         string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Warning message directly to the Loupe log with an optional attached Exception and
        /// specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void WarningDetail(Exception exception, LogWriteMode writeMode, string detailsXml, string category,
                                         string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        //
        // ERROR
        //

        /// <summary>
        /// Write a categorized Error message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Error(string category, string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Error message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Error(Exception exception, string category, string caption, string description,
                                 params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                Queued, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Error message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Error(Exception exception, bool attributeToException, string category, string caption, string description,
                                 params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                Queued, ThisLogSystem, category, 1, exception, attributeToException, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Error message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Error(LogWriteMode writeMode, string category, string caption, string description,
                                 params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Error message directly to the Loupe log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Error(Exception exception, LogWriteMode writeMode, string category, string caption,
                                 string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Error message directly to the Loupe log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Error(Exception exception, bool attributeToException, LogWriteMode writeMode, string category, string caption,
                                 string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, attributeToException, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Error message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.  Also see the Write() method for when XML details
        /// are not needed.  This method is otherwise similar to Write().</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void ErrorDetail(string detailsXml, string category, string caption, string description,
                                       params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Error message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void ErrorDetail(Exception exception, string detailsXml, string category, string caption,
                                       string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                Queued, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Error message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void ErrorDetail(Exception exception, bool attributeToException, string detailsXml, string category, string caption,
                                       string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                Queued, ThisLogSystem, category, 1, exception, attributeToException, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Error message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void ErrorDetail(LogWriteMode writeMode, string detailsXml, string category, string caption,
                                       string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Error message directly to the Loupe log with an optional attached Exception and
        /// specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void ErrorDetail(Exception exception, LogWriteMode writeMode, string detailsXml, string category,
                                       string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Error message directly to the Loupe log with an optional attached Exception and
        /// specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void ErrorDetail(Exception exception, bool attributeToException, LogWriteMode writeMode, string detailsXml, string category,
                                       string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, attributeToException, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        //
        // CRITICAL
        //

        /// <summary>
        /// Write a categorized Critical message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Critical(string category, string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Critical message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Critical(Exception exception, string category, string caption, string description,
                                    params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                Queued, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Critical message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Critical(Exception exception, bool attributeToException, string category, string caption, string description,
                                    params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                Queued, ThisLogSystem, category, 1, exception, attributeToException, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Critical message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Critical(LogWriteMode writeMode, string category, string caption, string description,
                                    params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Critical message directly to the Loupe log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead
        /// call the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Critical(Exception exception, LogWriteMode writeMode, string category, string caption,
                                    string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a categorized Critical message directly to the Loupe log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides basic Loupe logging features for typical use.  Loupe supports
        /// a separate caption and description in log messages in order to provide better analysis capability.  Log
        /// messages can be grouped by their captions even while their full descriptions differ, so for more useful
        /// matching we don't provide format processing on the caption argument, only on the description argument.</para>
        /// <para>The caption and description arguments tolerate null and empty strings (e.g. a simple one-line message
        /// caption with no further description needed).  A null caption will cause the message caption to be extracted
        /// from the description after format processing (comparable to using the Trace...() methods which don't take a
        /// separate caption argument).  A valid string caption argument, including an empty string, will be taken as the
        /// intended caption; an empty caption string is thus possible, but not recommended.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para>
        /// <para>The writeMode argument allows the caller to specify WaitForCommit behavior, which will not return
        /// until the message has been committed to the session file on disk.  The Queued behavior used by default with
        /// other overloads of this method places the message on Loupe's central queue and then returns, allowing
        /// the current thread execution to continue while Loupe processes the queue on a separate thread.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Critical(Exception exception, bool attributeToException, LogWriteMode writeMode, string category, string caption,
                                    string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.BasicLogMessage logMessage = new Monitor.BasicLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, attributeToException, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Critical message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.  Also see the Write() method for when XML details
        /// are not needed.  This method is otherwise similar to Write().</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void CriticalDetail(string detailsXml, string category, string caption, string description,
                                          params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Critical message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void CriticalDetail(Exception exception, string detailsXml, string category, string caption,
                                          string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                Queued, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Critical message directly to the Loupe log with an attached Exception.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void CriticalDetail(Exception exception, bool attributeToException, string detailsXml, string category, string caption,
                                          string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                Queued, ThisLogSystem, category, 1, exception, attributeToException, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Critical message directly to the Loupe log, specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para></remarks>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void CriticalDetail(LogWriteMode writeMode, string detailsXml, string category, string caption,
                                          string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Critical message directly to the Loupe log with an optional attached Exception and
        /// specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>The log message will be attributed to the caller of this method.  Wrapper methods should instead call
        /// the Write() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void CriticalDetail(Exception exception, LogWriteMode writeMode, string detailsXml, string category,
                                          string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, false, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a detailed Critical message directly to the Loupe log with an optional attached Exception and
        /// specifying Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages to include an XML document
        /// (as a string) containing extended details about the message.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void CriticalDetail(Exception exception, bool attributeToException, LogWriteMode writeMode, string detailsXml, string category,
                                          string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                (Monitor.LogWriteMode)writeMode, ThisLogSystem, category, 1, exception, attributeToException, detailsXml, caption, description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a complete log message directly to the Loupe log from a wrapper method or bridging logic,
        /// attributing the source of the message farther up the call-stack.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages for use in wrapper methods and
        /// for bridging simple logging systems into Loupe.  Also see
        /// <see cref="Verbose(Exception,LogWriteMode,string,string,string,object[])">Verbose</see> and
        /// <see cref="VerboseDetail(Exception,LogWriteMode,string,string,string,string,object[])">VerboseDetail</see>
        /// and their other overloads and related methods for simpler usage of XML details when the other advanced hooks
        /// are not needed.</para>
        /// <para>This overload of Write() is provided as an API hook for simple wrapping methods which need to attribute a
        /// log message to their own outer callers rather than to the direct caller of this method.  Passing a skipFrames
        /// of 0 would designate the caller of this method as the originator; a skipFrames of 1 would designate the caller
        /// of the caller of this method as the originator, and so on.  It will then extract information about
        /// the originator automatically based on the indicated stack frame.  Bridge logic adapting from a logging
        /// system which already determines and provides information about the originator (such as log4net) into
        /// Loupe should use the other overload of
        /// <see cref="Write(LogMessageSeverity,string,IMessageSourceProvider,IPrincipal,Exception,LogWriteMode,string,string,string,string,object[])">Write</see>,
        /// passing a customized IMessageSourceProvider.</para>
        /// <para>This method also requires explicitly selecting the LogWriteMode between Queued (the normal default,
        /// for optimal performance) and WaitForCommit (to help ensure critical information makes it to disk, e.g. before
        /// exiting the application upon return from this call).  See the <see cref="LogWriteMode">LogWriteMode</see> enum
        /// for more information.</para>
        /// <para>This method also allows an optional Exception object to be attached to the log message (null for none).
        /// It can also include an optional XML document (as a string, or null for none) containing extended details about
        /// the message.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="logSystem">The name of the originating log system (e.g. "Log4Net").</param>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.
        /// (0 means the immediate caller of this method; 1 means their immediate caller, and so on.)</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message. (May be null.)</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Write(LogMessageSeverity severity, string logSystem, int skipFrames, Exception exception,
                                 LogWriteMode writeMode, string detailsXml, string category, string caption, string description,
                                 params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            if (skipFrames < 0)
                skipFrames = 0; // Make sure they don't pass us a negative, it would attribute it here to us.

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage((Loupe.Extensibility.Data.LogMessageSeverity)severity,
                (Monitor.LogWriteMode)writeMode, logSystem, category, skipFrames + 1, exception, false, detailsXml, caption,
                description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a complete log message directly to the Loupe log from a wrapper method or bridging logic,
        /// attributing the source of the message farther up the call-stack.
        /// </summary>
        /// <remarks><para>This method provides an advanced use of Loupe log messages for use in wrapper methods and
        /// for bridging simple logging systems into Loupe.  Also see
        /// <see cref="Verbose(Exception,LogWriteMode,string,string,string,object[])">Verbose</see> and
        /// <see cref="VerboseDetail(Exception,LogWriteMode,string,string,string,string,object[])">VerboseDetail</see>
        /// and their other overloads and related methods for simpler usage of XML details when the other advanced hooks
        /// are not needed.</para>
        /// <para>This overload of Write() is provided as an API hook for simple wrapping methods which need to attribute a
        /// log message to their own outer callers rather than to the direct caller of this method.  Passing a skipFrames
        /// of 0 would designate the caller of this method as the originator; a skipFrames of 1 would designate the caller
        /// of the caller of this method as the originator, and so on.  It will then extract information about
        /// the originator automatically based on the indicated stack frame.  Bridge logic adapting from a logging
        /// system which already determines and provides information about the originator (such as log4net) into
        /// Loupe should use the other overload of
        /// <see cref="Write(LogMessageSeverity,string,IMessageSourceProvider,IPrincipal,Exception,LogWriteMode,string,string,string,string,object[])">Write</see>,
        /// passing a customized IMessageSourceProvider.</para>
        /// <para>This method also requires explicitly selecting the LogWriteMode between Queued (the normal default,
        /// for optimal performance) and WaitForCommit (to help ensure critical information makes it to disk, e.g. before
        /// exiting the application upon return from this call).  See the <see cref="LogWriteMode">LogWriteMode</see> enum
        /// for more information.</para>
        /// <para>This method also allows an optional Exception object to be attached to the log message (null for none).
        /// It can also include an optional XML document (as a string, or null for none) containing extended details about
        /// the message.</para>
        /// <para>If attributeToException is set to true the log message will be attributed to the location where the 
        /// provided exception was thrown from instead of the caller of this method.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="logSystem">The name of the originating log system (e.g. "Log4Net").</param>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.
        /// (0 means the immediate caller of this method; 1 means their immediate caller, and so on.)</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True to record this log message based on where the exception was thrown, not where this method was called</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message. (May be null.)</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Write(LogMessageSeverity severity, string logSystem, int skipFrames, Exception exception, bool attributeToException,
                                 LogWriteMode writeMode, string detailsXml, string category, string caption, string description,
                                 params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            if (skipFrames < 0)
                skipFrames = 0; // Make sure they don't pass us a negative, it would attribute it here to us.

            Monitor.DetailLogMessage logMessage = new Monitor.DetailLogMessage((Loupe.Extensibility.Data.LogMessageSeverity)severity,
                (Monitor.LogWriteMode)writeMode, logSystem, category, skipFrames + 1, exception, attributeToException, detailsXml, caption,
                description, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a complete log message to the Loupe central log, optionally extended with XML details and broadest API features.
        /// </summary>
        /// <remarks><para>This method provides the most complete API for issuing messages into Loupe, allowing
        /// log system, user name, and a customized message source provider to be specified.  These parameters allow
        /// bridge logic to support interfacing to Loupe from third-party logging systems like log4net,
        /// including from your own in-house system.</para>
        /// <para>The <see cref="IMessageSourceProvider">sourceProvider</see> is expected to remain valid for this message
        /// until this call returns, after which the values have been read and copied.  Loupe does not keep a reference
        /// to the sourceProvider object after this call returns, so it may then be discarded, reused, or altered however
        /// your implementation requires.</para></remarks>
        /// <param name="severity">The severity enum value of the log message.</param>
        /// <param name="logSystem">The name of the originating log system (e.g. "Log4Net").</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <param name="principal">The effective user principal associated with the execution task which issued the log message.
        /// (If null, Loupe will determine the user name automatically.)</param>
        /// <param name="exception">An Exception object attached to this log message, or null if none.</param>
        /// <param name="writeMode">A LogWriteMode enum value indicating whether to simply queue the log message
        /// and return quickly, or to wait for the log message to be committed to disk before returning.</param>
        /// <param name="detailsXml">An XML document (as a string) with extended details about the message.</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which supports a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        public static void Write(LogMessageSeverity severity, string logSystem, IMessageSourceProvider sourceProvider,
                                 IPrincipal principal, Exception exception, LogWriteMode writeMode, string detailsXml,
                                 string category, string caption, string description, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.Log.WriteMessage((Loupe.Extensibility.Data.LogMessageSeverity)severity, (Monitor.LogWriteMode)writeMode, logSystem,
                category, (sourceProvider == null) ? null : new MessageSourceProvider(sourceProvider), principal, exception,
                detailsXml, caption, description, args);
        }

        /// <summary>
        /// Record an unexpected exception to the log without displaying a user
        /// prompt.
        /// </summary>
        /// <remarks>
        /// 	<para>This method provides an easy way to record an exception as a separate message
        ///     which will be attributed to the code location which threw the exception rather than
        ///     where this method was called from. The category will default to "Exception" if
        ///     null, and the message will be formatted automatically based on the exception. The
        ///     severity will be determined by the canContinue parameter: Critical for fatal errors
        ///     (canContinue is false), Error for non-fatal errors (canContinue is true).</para>
        /// 	<para>
        ///         This method is intended for use with top-level exception catching for errors
        ///         not anticipated in a specific operation, but when it is not appropriate to
        ///         alert the user because the error does not impact their work flow or will be
        ///         otherwise handled gracefully within the application.
        ///     </para>
        /// 	<para>
        ///         For localized exception catching (e.g. anticipating exceptions when opening a
        ///         file) we recommend logging an appropriate, specific log message with the
        ///         exception attached. (See <see cref="TraceError(Exception, string, object[])">TraceError</see>, <see cref="Error(Exception, LogWriteMode, string, string, string, object[])">Error</see>, and <see cref="Write(LogMessageSeverity, string, int, Exception, LogWriteMode, string, string, string, string, object[])">Write</see> and other such methods;
        ///         the message need not be of Error severity.)
        ///     </para>
        /// </remarks>
        /// <example>
        /// 	<code lang="CS" title="Record and Report exception" description="Shows an example of both the record and report exception commands. Only one needs to be used for any single exception.">
        /// 		<![CDATA[
        /// //this option records the exception but does not display any user interface.  
        /// Log.RecordException(ex, "Exceptions", true);
        ///  
        /// //this option records the exception and displays a user interface, optionally waiting for the user 
        /// //to decide to continue or exit before returning.
        /// Log.ReportException(ex, "Exceptions", true, true);]]>
        /// 	</code>
        /// </example>
        /// <param name="exception">
        /// An exception object to record as a log message. This call is ignored if
        /// null.
        /// </param>
        /// <param name="category">The application subsystem or logging category that the message will be associated with.</param>
        /// <param name="canContinue">
        /// True if the application can continue after this call, false if this is a fatal
        /// error and the application should not continue.
        /// </param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void RecordException(Exception exception, string category, bool canContinue = true)
        {
            if (exception != null)
                Monitor.Log.RecordException(1, exception, null, category, canContinue);
        }

        /// <summary>
        /// Write a Verbose trace message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para></remarks>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceVerbose(string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                ThisLogSystem, Category, 1, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a Verbose trace message directly to the Loupe log, with an attached Exception.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceVerbose(Exception exception, string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Verbose,
                Queued, ThisLogSystem, Category, 1, exception, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write an Information trace message directly to the Loupe log.
        /// </summary>
        /// <remarks>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para></remarks>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceInformation(string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                ThisLogSystem, Category, 1, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write an Information trace message directly to the Loupe log, with an attached Exception.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceInformation(Exception exception, string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Information,
                Queued, ThisLogSystem, Category, 1, exception, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a Warning trace message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para></remarks>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceWarning(string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                ThisLogSystem, Category, 1, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a Warning trace message directly to the Loupe log, with an attached Exception.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceWarning(Exception exception, string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Warning,
                Queued, ThisLogSystem, Category, 1, exception, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write an Error trace message directly to the Loupe  log.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para></remarks>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceError(string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                ThisLogSystem, Category, 1, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write an Error trace message directly to the Loupe log, with an attached Exception.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceError(Exception exception, string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Error,
                Queued, ThisLogSystem, Category, 1, exception, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a Critical trace message directly to the Loupe log.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para></remarks>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceCritical(string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                ThisLogSystem, Category, 1, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// Write a Critical trace message directly to the Loupe log, with an attached Exception.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations should
        /// instead use the Log.Write(...) overloads in order to attribute the log message to their own outer callers.</para>
        /// <para>The message will not be sent through System.Diagnostics.Trace and will not be seen by other trace
        /// listeners.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        public static void TraceCritical(Exception exception, string format, params object[] args)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return;

            Monitor.SimpleLogMessage logMessage = new Monitor.SimpleLogMessage(Loupe.Extensibility.Data.LogMessageSeverity.Critical,
                Queued, ThisLogSystem, Category, 1, exception, format, args);

            logMessage.PublishToLog();
        }

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file (instead of waiting
        /// for an automatic maintenance roll-over) in order to allow the logs of an ongoing session up to that point
        /// to be collected and submitted for analysis without shutting down the subject application.</remarks>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndFile()
        {
            Monitor.Log.EndFile(1, string.Empty); // No reason declared, attribute it to our immediate caller.
        }

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging,
        /// specifying an optional reason.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file (instead of waiting
        /// for an automatic maintenance roll-over) in order to allow the logs of an ongoing session up to that point
        /// to be collected and submitted for analysis without shutting down the subject application.</remarks>
        /// <param name="reason">An optionally-declared reason for invoking this operation (may be null or empty).</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndFile(string reason)
        {
            Monitor.Log.EndFile(1, reason); // Pass on reason, attribute it to our immediate caller.
        }

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging,
        /// specifying an optional reason and attributing the request farther back in the call stack.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file (instead of waiting
        /// for an automatic maintenance roll-over) in order to allow the logs of an ongoing session up to that point
        /// to be collected and submitted for analysis without shutting down the subject application.</remarks>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.
        /// (0 means the immediate caller of this method; 1 means their immediate caller, and so on.)</param>
        /// <param name="reason">An optionally-declared reason for invoking this operation (may be null or empty).</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndFile(int skipFrames, string reason)
        {
            if (skipFrames < 0)
                skipFrames = 0;

            Monitor.Log.EndFile(skipFrames + 1, reason); // Pass on reason, attribute it farther back as specified.
        }

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally or explicitly crashed.
        /// </summary>
        /// <remarks>
        /// 	<para>This will put the Loupe Agent into an ending state in which it will flush
        ///     everything still in its queue and then switch to a background thread to process any
        ///     further messages. All messages submitted after this call will block the submitting
        ///     thread until they are committed to disk, so that any foreground thread still
        ///     recording final items will be sure to get them through before they exit.</para>
        /// 	<para>In WinForms applications this method is called automatically when an
        ///     ApplicationExit event is received. It is also called automatically when the Agent
        ///     is registered as a Trace Listener and Trace.Close is called.</para>
        /// 	<para>If EndSession is never called, the log will reflect that the session must
        ///     have crashed.</para>
        /// </remarks>
        /// <overloads>
        /// Used to explicitly set the session state for the current session and provide a
        /// reason.
        /// </overloads>
        /// <param name="endingStatus">The explicit ending status to declare for this session, <see cref="SessionStatus.Normal">Normal</see>
        /// or <see cref="SessionStatus.Crashed">Crashed</see>.</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <param name="reason">A simple reason to declare why the application is ending as Normal or as Crashed, or may be null.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndSession(SessionStatus endingStatus, IMessageSourceProvider sourceProvider, string reason)
        {
            Monitor.Log.EndSession((Loupe.Extensibility.Data.SessionStatus)endingStatus,
                (sourceProvider == null) ? null : new MessageSourceProvider(sourceProvider), reason);
        }

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally or explicitly crashed.
        /// </summary>
        /// <remarks>
        /// 	<para>This will put the Loupe Agent into an ending state in which it will flush
        ///     everything still in its queue and then switch to a background thread to process any
        ///     further messages. All messages submitted after this call will block the submitting
        ///     thread until they are committed to disk, so that any foreground thread still
        ///     recording final items will be sure to get them through before they exit.</para>
        /// 	<para>In WinForms applications this method is called automatically when an
        ///     ApplicationExit event is received. It is also called automatically when the Agent
        ///     is registered as a Trace Listener and Trace.Close is called.</para>
        /// 	<para>If EndSession is never called, the log will reflect that the session must
        ///     have crashed.</para>
        /// </remarks>
        /// <overloads>
        /// Used to explicitly set the session state for the current session and provide a
        /// reason.
        /// </overloads>
        /// <param name="endingStatus">The explicit ending status to declare for this session, <see cref="SessionStatus.Normal">Normal</see>
        /// or <see cref="SessionStatus.Crashed">Crashed</see>.</param>
        /// <param name="skipFrames">The number of stack frames to skip out to find the original caller.</param>
        /// <param name="reason">A simple reason to declare why the application is ending as Normal or as Crashed, or may be null.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndSession(SessionStatus endingStatus, int skipFrames, string reason)
        {
            Monitor.Log.EndSession((Loupe.Extensibility.Data.SessionStatus)endingStatus, skipFrames + 1, reason);
        }

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally or explicitly crashed.
        /// </summary>
        /// <remarks>
        /// 	<para>This will put the Loupe Agent into an ending state in which it will flush
        ///     everything still in its queue and then switch to a background thread to process any
        ///     further messages. All messages submitted after this call will block the submitting
        ///     thread until they are committed to disk, so that any foreground thread still
        ///     recording final items will be sure to get them through before they exit.</para>
        /// 	<para>In WinForms applications this method is called automatically when an
        ///     ApplicationExit event is received. It is also called automatically when the Agent
        ///     is registered as a Trace Listener and Trace.Close is called.</para>
        /// 	<para>If EndSession is never called, the log will reflect that the session must
        ///     have crashed.</para>
        /// </remarks>
        /// <overloads>
        /// Used to explicitly set the session state for the current session and provide a
        /// reason.
        /// </overloads>
        /// <param name="endingStatus">The explicit ending status to declare for this session, <see cref="SessionStatus.Normal">Normal</see>
        /// or <see cref="SessionStatus.Crashed">Crashed</see>.</param>
        /// <param name="reason">A simple reason to declare why the application is ending as Normal or as Crashed, or may be null.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndSession(SessionStatus endingStatus, string reason)
        {
            // A specified exit status attributed to our immediate caller with a specified reason.
            Monitor.Log.EndSession((Loupe.Extensibility.Data.SessionStatus)endingStatus, 1, reason);
        }

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally or explicitly crashed.
        /// </summary>
        /// <remarks>
        /// 	<para>This will put the Loupe Agent into an ending state in which it will flush
        ///     everything still in its queue and then switch to a background thread to process any
        ///     further messages. All messages submitted after this call will block the submitting
        ///     thread until they are committed to disk, so that any foreground thread still
        ///     recording final items will be sure to get them through before they exit.</para>
        /// 	<para>In WinForms applications this method is called automatically when an
        ///     ApplicationExit event is received. It is also called automatically when the Agent
        ///     is registered as a Trace Listener and Trace.Close is called.</para>
        /// 	<para>If EndSession is never called, the log will reflect that the session must
        ///     have crashed.</para>
        /// </remarks>
        /// <overloads>
        /// Used to explicitly set the session state for the current session and provide a
        /// reason.
        /// </overloads>
        /// <param name="reason">A simple reason to declare why the application is ending as Normal, or may be null.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndSession(string reason)
        {
            // A specified exit status attributed to our immediate caller with a specified reason.
            Monitor.Log.EndSession(Loupe.Extensibility.Data.SessionStatus.Normal, 1, reason);
        }

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally.
        /// </summary>
        /// <remarks>
        /// 	<para>This will put the Loupe Agent into an ending state in which it will flush
        ///     everything still in its queue and then switch to a background thread to process any
        ///     further messages. All messages submitted after this call will block the submitting
        ///     thread until they are committed to disk, so that any foreground thread still
        ///     recording final items will be sure to get them through before they exit.</para>
        /// 	<para>In WinForms applications this method is called automatically when an
        ///     ApplicationExit event is received. It is also called automatically when the Agent
        ///     is registered as a Trace Listener and Trace.Close is called.</para>
        /// 	<para>If EndSession is never called, the log will reflect that the session must
        ///     have crashed.</para>
        /// </remarks>
        /// <overloads>
        ///     This overload will declare a <see cref="SessionStatus.Normal">Normal</see> ending
        ///     state with no explicit reason.
        /// </overloads>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void EndSession()
        {
            // A normal exit attributed to our immediate caller with no explicit reason.
            Monitor.Log.EndSession(Loupe.Extensibility.Data.SessionStatus.Normal, 1, null);
        }

        /// <summary>
        /// Attempt to activate the agent.
        /// </summary>
        /// <remarks>If the agent is already active this call has no effect.  When starting, the agent
        /// will raise an Initializing event which can be canceled.  If it cancels then the session has not been
        /// started.  All calls to the agent are safe whether it has been activated or not.</remarks>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void StartSession()
        {
            Monitor.Log.StartSession(null, 1, null);
        }

        /// <summary>
        /// Attempt to activate the agent.
        /// </summary>
        /// <param name="reason">A caption for the reason the session is starting, or null.</param>
        /// <remarks>If the agent is already active this call has no effect.  When starting, the agent
        /// will raise an Initializing event which can be canceled.  If it cancels then the session has not been
        /// started.  All calls to the agent are safe whether it has been activated or not.</remarks>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void StartSession(string reason)
        {
            Monitor.Log.StartSession(null, 1, reason);
        }

        /// <summary>
        /// Attempt to activate the agent.
        /// </summary>
        /// <param name="skipFrames">The number of stack frames to skip out to find the original caller.</param>
        /// <param name="reason">A caption for the reason the session is starting, or null.</param>
        /// <remarks>If the agent is already active this call has no effect.  When starting, the agent
        /// will raise an Initializing event which can be canceled.  If it cancels then the session has not been
        /// started.  All calls to the agent are safe whether it has been activated or not.</remarks>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void StartSession(int skipFrames, string reason)
        {
            Monitor.Log.StartSession(null, skipFrames + 1, reason);
        }

        /// <summary>
        /// Attempt to activate the agent.
        /// </summary>
        /// <param name="reason">A caption for the reason the session is starting, or null.</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <remarks>If the agent is already active this call has no effect.  When starting, the agent
        /// will raise an Initializing event which can be canceled.  If it cancels then the session has not been
        /// started.  All calls to the agent are safe whether it has been activated or not.</remarks>
        public static void StartSession(IMessageSourceProvider sourceProvider, string reason)
        {
            Monitor.Log.StartSession(null, (sourceProvider == null) ? null : new MessageSourceProvider(sourceProvider), reason);
        }

        /// <summary>
        /// Attempt to activate the agent.
        /// </summary>
        /// <param name="configuration">The Agent configuration to use instead of any configuration in the app.config file.</param>
        /// <remarks>If the agent is already active this call has no effect.  When starting, the agent
        /// will raise an Initializing event which can be canceled.  If it cancels then the session has not been
        /// started.  All calls to the agent are safe whether it has been activated or not.</remarks>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void StartSession(AgentConfiguration configuration)
        {
            if (ReferenceEquals(configuration, null))
                throw new ArgumentNullException(nameof(configuration));

            Monitor.Log.StartSession(configuration, 1, null);
        }

        /// <summary>
        /// Attempt to activate the agent.
        /// </summary>
        /// <param name="configuration">The Agent configuration to use instead of any configuration in the app.config file.</param>
        /// <param name="reason">A caption for the reason the session is starting, or null.</param>
        /// <remarks>If the agent is already active this call has no effect.  When starting, the agent
        /// will raise an Initializing event which can be canceled.  If it cancels then the session has not been
        /// started.  All calls to the agent are safe whether it has been activated or not.</remarks>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void StartSession(AgentConfiguration configuration, string reason)
        {
            if (ReferenceEquals(configuration, null))
                throw new ArgumentNullException(nameof(configuration));

            Monitor.Log.StartSession(configuration, 1, reason);
        }

        /// <summary>
        /// Attempt to activate the agent.
        /// </summary>
        /// <param name="configuration">The Agent configuration to use instead of any configuration in the app.config file.</param>
        /// <param name="skipFrames">The number of stack frames to skip out to find the original caller.</param>
        /// <param name="reason">A caption for the reason the session is starting, or null.</param>
        /// <remarks>If the agent is already active this call has no effect.  When starting, the agent
        /// will raise an Initializing event which can be canceled.  If it cancels then the session has not been
        /// started.  All calls to the agent are safe whether it has been activated or not.</remarks>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static void StartSession(AgentConfiguration configuration, int skipFrames, string reason)
        {
            if (ReferenceEquals(configuration, null))
                throw new ArgumentNullException(nameof(configuration));

            Monitor.Log.StartSession(configuration, skipFrames + 1, reason);
        }

        /// <summary>
        /// Attempt to activate the agent.
        /// </summary>
        /// <param name="configuration">The Agent configuration to use instead of any configuration in the app.config file.</param>
        /// <param name="reason">A caption for the reason the session is starting, or null.</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <remarks>If the agent is already active this call has no effect.  When starting, the agent
        /// will raise an Initializing event which can be canceled.  If it cancels then the session has not been
        /// started.  All calls to the agent are safe whether it has been activated or not.</remarks>
        public static void StartSession(AgentConfiguration configuration, IMessageSourceProvider sourceProvider, string reason)
        {
            if (ReferenceEquals(configuration, null))
                throw new ArgumentNullException(nameof(configuration));

            Monitor.Log.StartSession(configuration, (sourceProvider == null) ? null : new MessageSourceProvider(sourceProvider), reason);
        }

        /// <summary>
        /// Safely send sessions to the Loupe Server or via email.  Only one send request will be processed at a time.
        /// </summary>
        /// <param name="criteria">The criteria to match for the sessions to send</param>
        /// <returns>True if the send was processed, false if it was not due to configuration or another active send</returns>
        /// <remarks>
        /// <para>This method uses the same logic to determine how to transport data as SendSessionsOnExit. 
        /// If a Loupe Server connection is configured and the server can be contacted it will be used.
        /// Otherwise if packaging via email is configured the package will be sent that way.</para>
        /// <para>If there is no way to send the information (either due to configuration or the 
        /// server being unreachable) this method will return false.  Otherwise the method will 
        /// return when it completes sending.</para>
        /// <para>If another send attempt is currently being processed this method will complete
        /// immediately and return false.  This prevents multiple simultaneous send attempts from
        /// consuming resources.</para>
        /// </remarks>
        public static async Task<bool> SendSessions(SessionCriteria criteria)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return false;

            return await Monitor.Log.SendSessions(criteria, null, false).ConfigureAwait(false);
        }

        /// <summary>
        /// Safely send sessions to the Loupe Server or via email.  Only one send request will be processed at a time.
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <returns>True if the send was processed, false if it was not due to configuration or another active send</returns>
        /// <remarks>
        /// <para>This method uses the same logic to determine how to transport data as SendSessionsOnExit. 
        /// If a Loupe Server connection is configured and the server can be contacted it will be used.
        /// Otherwise if packaging via email is configured the package will be sent that way.</para>
        /// <para>If there is no way to send the information (either due to configuration or the 
        /// server being unreachable) this method will return false.  Otherwise the method will 
        /// return when it completes sending.</para>
        /// <para>If another send attempt is currently being processed this method will complete
        /// immediately and return false.  This prevents multiple simultaneous send attempts from
        /// consuming resources.</para>
        /// </remarks>
        public static async Task<bool> SendSessions(Predicate<SessionSummary> sessionMatchPredicate)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return false;

            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            return await Monitor.Log.SendSessions(null, predicateAdapter.Predicate, false).ConfigureAwait(false);
        }

        /// <summary>
        /// Safely send sessions asynchronously to the Loupe Server or via email.  Only one send request will be processed at a time.
        /// </summary>
        /// <param name="criteria">The criteria to match for the sessions to send</param>
        /// <returns>True if the send was processed, false if it was not due to configuration or another active send</returns>
        /// <remarks>
        /// <para>This method uses the same logic to determine how to transport data as SendSessionsOnExit. 
        /// If a Loupe Server connection is configured and the server can be contacted it will be used.
        /// Otherwise if packaging via email is configured the package will be sent that way.</para>
        /// <para>If there is no way to send the information (either due to configuration or the 
        /// server being unreachable) this method will return false.  Otherwise the method will
        /// return once it starts the send process, which will complete asynchronously.</para>
        /// <para>If another send attempt is currently being processed this method will complete
        /// immediately and return false.  This prevents multiple simultaneous send attempts from
        /// consuming resources.</para>
        /// </remarks>
        public static async Task<bool> SendSessionsAsync(SessionCriteria criteria)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return false;

            return await Monitor.Log.SendSessions(criteria, null, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Safely send sessions asynchronously to the Loupe Server or via email.  Only one send request will be processed at a time.
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <returns>True if the send was processed, false if it was not due to configuration or another active send</returns>
        /// <remarks>
        /// <para>This method uses the same logic to determine how to transport data as SendSessionsOnExit. 
        /// If a Loupe Server connection is configured and the server can be contacted it will be used.
        /// Otherwise if packaging via email is configured the package will be sent that way.</para>
        /// <para>If there is no way to send the information (either due to configuration or the 
        /// server being unreachable) this method will return false.  Otherwise the method will
        /// return once it starts the send process, which will complete asynchronously.</para>
        /// <para>If another send attempt is currently being processed this method will complete
        /// immediately and return false.  This prevents multiple simultaneous send attempts from
        /// consuming resources.</para>
        /// </remarks>
        public static async Task<bool> SendSessionsAsync(Predicate<SessionSummary> sessionMatchPredicate)
        {
            //don't do jack if we aren't initialized.
            if (Monitor.Log.IsLoggingActive() == false)
                return false;

            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            return await Monitor.Log.SendSessions(null, predicateAdapter.Predicate, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Optional.  A custom authentication provider for communication between the agent and server
        /// </summary>
        /// <remarks><p>Typically communication between the Agent and a Server is done using anonymous
        /// methods (due to the restrictive nature of the API).  If a network device (such as custom
        /// gateway or module added to the server) requires additional authentication to allow communication
        /// then it can be handled by creating a server authentication provider.</p>
        /// <p>Individual providers can perform authentication processes independently of the server
        /// request pipeline and can modify each server request to add additional headers or other data
        /// as required to authenticate the request.  A simple authentication provider for handling HTTP
        /// Basic Authentication is implemented in the <see cref="Gibraltar.Agent.Net.BasicAuthenticationProvider">
        /// BasicAuthenticationProvider</see> class.</p></remarks>
        public static IServerAuthenticationProvider ServerAuthenticationProvider
        {
            get { return s_ServerAuthenticationProvider; }
            set
            {
                s_ServerAuthenticationProvider = value;
            }
        }


        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Our one metric definition collection for capturing metrics in this process
        /// </summary>
        /// <remarks>
        /// For performance reasons, it is important that there is only a single instance of a particular metric
        /// for any given process.  This is managed automatically provided only this metrics collection is used.
        /// If there is a duplicate metric in the data stream, that information will be discarded when the log 
        /// file is read (but there is no affect at runtime).
        /// </remarks>
        internal static MetricDefinitionCollection MetricDefinitions { get { return s_MetricDefinitions; } }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Proxies the inner log initializing event to our clients.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void LogOnInitializing(object sender, Monitor.LogInitializingEventArgs e)
        {
            //see if we CAN initialize.
            InitializingEventHandler tempEvent = Initializing;

            if (tempEvent != null)
            {
                //we need to see if our callers will let us initialize.
                LogInitializingEventArgs eventArgs = new LogInitializingEventArgs(new AgentConfiguration(e.Configuration));
                try
                {
                    tempEvent(null, eventArgs);
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
#if DEBUG
                    Monitor.Log.DebugBreak();
#endif
                }

                //copy back the cancel setting
                e.Cancel = eventArgs.Cancel;
            }
        }

        private static void CachedCredentialsManagerOnCredentialsRequired(object source, CredentialsRequiredEventArgs e)
        {
            var authenticationProvider = s_ServerAuthenticationProvider;
            if (ReferenceEquals(authenticationProvider, null))
                return;

            e.AuthenticationProvider = new WebAuthenticationProvider(authenticationProvider);
        }

        private static void EnsureSummaryIsAvailable()
        {
            //we're going to modify shared objects, so lets lock our state.
            lock (s_SyncLock)
            {
                //lets see if we get the same object both ways.
                if (s_SessionSummary == null)
                {
                    s_SessionSummary = new SessionSummary(Monitor.Log.SessionSummary);                    
                }
                else
                {
                    s_SessionSummary.SyncWrappedObject(Monitor.Log.SessionSummary);
                }

                System.Threading.Monitor.PulseAll(s_SyncLock);
            }
        }

        private static void MessageAlertNotifierOnNotificationEvent(object sender, Messaging.NotificationEventArgs e)
        {
            var eventHandler = s_MessageAlertEvent;

            if (eventHandler != null)
            {
                var eventArgs = new LogMessageAlertEventArgs(e);
                eventHandler(null, eventArgs);
            }
        }

        private static void MessageNotifierOnNotificationEvent(object sender, Messaging.NotificationEventArgs e)
        {
            var eventHandler = s_MessageEvent;

            if (eventHandler != null)
            {
                var eventArgs = new LogMessageEventArgs(e);
                eventHandler(null, eventArgs);
            }
        }

        private static void LiveViewerOnMessageFilter(object sender, Monitor.MessageFilterEventArgs e)
        {
            var eventHandler = s_MessageFilterEvent;

            if (eventHandler != null)
            {
                LogMessageFilterEventArgs eventArgs = new LogMessageFilterEventArgs(e);
                eventHandler(sender, eventArgs);
            }
        }

        #endregion
    }
}