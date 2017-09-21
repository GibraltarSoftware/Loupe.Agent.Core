using System;
using System.Collections.Generic;

namespace Loupe.Configuration
{
    /// <summary>
    /// The top level configuration class for all Agent configuration. Supplied during a
    /// Log.Initializing event.
    /// </summary>
    /// <remarks>
    ///     This object is created by the agent and supplied to user code during the <see cref="Gibraltar.Agent.Log.Initializing">Log.Initializing</see> event to allow for
    ///     configuration to be determined in code at runtime. This configuration is applied
    ///     over whatever has been configured in the application configuration file.
    /// </remarks>
    /// <example>
    /// 	<code lang="CS" title="Programmatic Configuration" description="You can supply some or all of your configuration information during the Log.Initializing event. In this example, the Loupe Server configuration is being done at runtime during this event.">
    /// 		<![CDATA[
    /// /// <summary>
    /// /// The primary program entry point.
    /// /// </summary>
    /// static class Program
    /// {
    ///     /// <summary>
    ///     /// The main entry point for the application.
    ///     /// </summary>
    ///     [STAThread]
    ///     public static void Main()
    ///     {
    ///         Log.Initializing += Log_Initializing;
    ///  
    ///         Application.EnableVisualStyles();
    ///         Application.SetCompatibleTextRenderingDefault(false);
    ///         Thread.CurrentThread.Name = "User Interface Main";  //set the thread name before our first call that logs on this thread.
    ///  
    ///         Log.StartSession("Starting Gibraltar Analyst");
    ///  
    ///         //here you actual start up your application
    ///  
    ///         //and if we got to this point, we done good and can mark the session as being not crashed :)
    ///         Log.EndSession("Exiting Gibraltar Analyst");
    ///     }
    ///  
    ///     static void Log_Initializing(object sender, LogInitializingEventArgs e)
    ///     {
    ///         //and configure Loupe Server Connection
    ///         ServerConfiguration server = e.Configuration.Server;
    ///         server.UseGibraltarService = true;
    ///         server.CustomerName = "Gibraltar Software";
    ///         server.AutoSendSessions = true;
    ///         server.SendAllApplications = true;
    ///         server.PurgeSentSessions = true;
    ///     }
    /// }]]>
    /// 	</code>
    /// </example>
    public sealed class AgentConfiguration
    {
        /// <summary>
        /// Create a new agent configuration, starting with the application's configuration file data.
        /// </summary>
        public AgentConfiguration()
        {
            // MR: We've traditionally created these by default so the app can just walk properties to get defaults, but 
            // is that conventional for .NET Core?  Or should we make them create them.
            Listener = new ListenerConfiguration();
            NetworkViewer = new NetworkViewerConfiguration();
            Packager = new PackagerConfiguration();
            Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Publisher = new PublisherConfiguration();
            Server = new ServerConfiguration();
            SessionFile = new SessionFileConfiguration();
        }

        /// <summary>
        /// Create a new agent configuration, copying properties from existing one.
        /// </summary>
        /// <param name="configuration">An existing agent configuration.</param>
        /// <remarks>Inferring this from usage in <c>Gibraltar.Agent.Log</c>.</remarks>
        public AgentConfiguration(AgentConfiguration configuration)
        {
            Listener = configuration.Listener;
            NetworkViewer = configuration.NetworkViewer;
            Packager = configuration.Packager;
            Properties = configuration.Properties;
            Publisher = configuration.Publisher;
            Server = configuration.Server;
            SessionFile = configuration.SessionFile;
        }

        /// <summary>
        /// The listener configuration
        /// </summary>
        public ListenerConfiguration Listener { get; set; }


        /// <summary>The session data file configuration</summary>
        public SessionFileConfiguration SessionFile { get; set; }

        /// <summary>
        /// The packager configuration
        /// </summary>
        public PackagerConfiguration Packager { get; set; }

        /// <summary>
        /// The publisher configuration
        /// </summary>
        public PublisherConfiguration Publisher { get; set; }

        /// <summary>
        /// The central server configuration
        /// </summary>
        public ServerConfiguration Server { get; set; }

        /// <summary>
        /// Configures real-time network log streaming
        /// </summary>
        public NetworkViewerConfiguration NetworkViewer { get; set; }

        /// <summary>
        /// Application defined properties
        /// </summary>
        public Dictionary<string, string> Properties { get; set; }


        /// <summary>
        /// Normalize configuration values
        /// </summary>
        public void Sanitize()
        {
            //we want to force everyone to load and sanitize so we know it's completed.
            NetworkViewer.Sanitize();
            Packager.Sanitize();
            Publisher.Sanitize();
            SessionFile.Sanitize();
            Server.Sanitize();
        }
    }
}
