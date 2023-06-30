using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Loupe.Configuration
{
    /// <summary>
    /// The top level configuration class for configuring the Loupe Agent's built-in features.
    /// </summary>
    /// <remarks>
    /// <para>You can provide configuration using the normal .NET configuration sources (like appsettings.json, environment variables, etc.)
    /// and those values will be loaded first.  Then, you can modify the configuration using a builder expression provided to the AddLoupe
    /// method as shown in the example below.</para>
    /// <para>For more information, see the <a href="https://doc.onloupe.com">Loupe Documentation</a></para>
    /// </remarks>
    /// <example>
    /// 	<code lang="CS" title="Programmatic Configuration" description="You can supply some or all of your configuration information when setting up your host, just use a lambda expression in the AddLoupe call to provide a configuration builder expression.">
    /// 		<![CDATA[
    ///public static IHostBuilder CreateHostBuilder(string[] args) =>
    ///    Host.CreateDefaultBuilder(args)
    ///        .AddLoupe(builder => builder.AddAspNetCoreDiagnostics()
    ///            .AddClientLogging() //The Loupe endpoint for client logging
    ///            .AddEntityFrameworkCoreDiagnostics() //EF Core monitoring
    ///            .AddPerformanceCounters()) //Windows Perf Counter monitoring
    ///        .AddLoupeLogging();
    /// ]]>
    /// 	</code>
    /// </example>
    /// 
    public sealed class AgentConfiguration
    {
        /// <summary>
        /// Create a new agent configuration, starting with the application's configuration file data.
        /// </summary>
        public AgentConfiguration()
        {
            AspNet = new AspNetConfiguration();
            ExportFile = new ExportFileConfiguration();
            Listener = new ListenerConfiguration();
            NetworkViewer = new NetworkViewerConfiguration();
            Packager = new PackagerConfiguration();
            Performance = new PerformanceConfiguration();
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
            AspNet = configuration.AspNet;
            ExportFile = configuration.ExportFile;
            Listener = configuration.Listener;
            NetworkViewer = configuration.NetworkViewer;
            Packager = configuration.Packager;
            Performance = configuration.Performance;
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
        /// The text log file configuration 
        /// </summary>
        public ExportFileConfiguration ExportFile { get; set; }

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
        /// Configures the ASP.NET Agent
        /// </summary>
        public AspNetConfiguration AspNet { get; set; }

        /// <summary>
        /// Performance monitoring options
        /// </summary>
        public PerformanceConfiguration Performance { get; set; }

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
            ExportFile ??= new ExportFileConfiguration();
            NetworkViewer ??= new NetworkViewerConfiguration();
            Packager ??= new PackagerConfiguration();
            Publisher ??= new PublisherConfiguration();
            SessionFile ??= new SessionFileConfiguration();
            Server ??= new ServerConfiguration();

            ExportFile?.Sanitize();
            NetworkViewer?.Sanitize();
            Packager?.Sanitize();
            Publisher?.Sanitize();
            SessionFile?.Sanitize();
            Server?.Sanitize();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("Publisher:\r\n{0}\r\n", Publisher);
            stringBuilder.AppendFormat("Listener:\r\n{0}\r\n", Listener);
            stringBuilder.AppendFormat("Performance:\r\n{0}\r\n", Performance);
            stringBuilder.AppendFormat("Session File:\r\n{0}\r\n", SessionFile);
            stringBuilder.AppendFormat("Export File:\r\n{0}\r\n", ExportFile);
            stringBuilder.AppendFormat("Server:\r\n{0}\r\n", Server);
            stringBuilder.AppendFormat("Network Viewer:\r\n{0}\r\n", NetworkViewer);

            if (AspNet?.Enabled == true)
            {
                stringBuilder.AppendFormat("Asp.NET:\r\n{0}\r\n", AspNet);
            }

            if (Properties?.Count > 0)
            {
                stringBuilder.AppendLine("\r\nProperties:");
                foreach (var property in Properties)
                {
                    stringBuilder.AppendFormat("{0}: {1}", property.Key, property.Value);
                }
            }

            return stringBuilder.ToString();
        }
    }
}
