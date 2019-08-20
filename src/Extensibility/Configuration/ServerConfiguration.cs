using System;

namespace Loupe.Configuration
{
    /// <summary>
    /// The application configuration information for sending session data to a Loupe Server
    /// </summary>
    /// <remarks>
    /// 	<para>To configure integration with the Loupe Server you need to either:</para>
    /// 	<list type="bullet">
    /// 		<item>
    ///             Set the <see cref="UseGibraltarService">UseGibraltarService</see> option to
    ///             True and specify a <see cref="CustomerName">CustomerName</see>
    /// 		</item>
    /// 		<item>
    ///             Set the <see cref="UseGibraltarService">UseGibraltarService</see> option to
    ///             False and specify a <see cref="Server">Server</see>
    /// 		</item>
    /// 	</list>
    /// 	<para><strong>Using the Loupe Cloud-Hosted Service</strong></para>
    /// 	<para>Just set the UseGibraltarService option to True and specify your Account name
    ///     which you can find at <a href="http://www.GibraltarSoftware.com">www.GibraltarSoftware.com</a>.</para>
    /// 	<para><strong>Using a Private Loupe Server</strong></para>
    /// 	<para>You will need to know the connection information from when the server was
    ///     installed to correctly configure some or all of the following values:</para>
    /// 	<list type="bullet">
    /// 		<item><strong>Server (required):</strong> The full DNS name of the computer
    ///         hosting the Loupe Server.</item>
    /// 		<item><strong>ApplicationBaseDirectory (optional):</strong> The virtual
    ///         directory on the server where the Loupe Server is installed. It may be empty,
    ///         indicating the server is installed in the root of its web site.</item>
    /// 		<item><strong>UseSsl (optional):</strong> Defaults to false; indicates if the
    ///         connection with the server should be encrypted using Secure Sockets Layer
    ///         (SSL). If no port is specified then port 443 (the standard for SSL) will be
    ///         used.</item>
    /// 		<item><strong>Port (optional):</strong> Used to specify a nonstandard port to
    ///         communicate with the server.</item>
    /// 	</list>
    /// 	<para><strong>Automatically Sending Sessions</strong></para>
    /// 	<para>The server configuration is used both to establish the connection information
    ///     for the Loupe Server for on-demand transfer (such as with the Packager Dialog or
    ///     Packager class) and also for automatic background session transfer. To enable
    ///     automatic background transfer of all sessions:</para>
    /// 	<list type="bullet">
    /// 		<item>
    ///             Set the <see cref="AutoSendSessions">AutoSendSessions</see> option to True
    ///         </item>
    /// 		<item>
    ///             Optionally set the <see cref="SendAllApplications">SendAllApplications</see> option to send session data
    ///             for any application that shares the same Product name as the current
    ///             application.
    ///         </item>
    /// 		<item>
    ///             Optionally set the <see cref="PurgeSentSessions">PurgeSentSessions</see>
    ///             option to True to remove sessions from the local computer once they have
    ///             been successfully sent to the Loupe Server.
    ///         </item>
    /// 	</list>
    /// 	<para><strong>Testing your Configuration</strong></para>
    /// 	<para>
    ///         You can validate the current configuration at runtime by calling the <see cref="Validate">Validate</see> method. If there are any problems with the
    ///         configuration data an exception will be thrown describing the issue. Some
    ///         problems (like an invalid customer name or server name) may not be fully
    ///         detectable in this routine.
    ///     </para>
    /// 	<para>
    ///         If the configuration is not valid the <see cref="Enabled">Enabled</see>
    ///         property is automatically set to False at the end of the Log.Initializing event. This isn't
    ///         checked until the end of the initialization cycle so properties can be set in
    ///         any order.
    ///     </para>
    /// </remarks>
    /// <example>
    /// 	<code lang="CS" title="Server Programmatic Configuration" description="You can configure the entire Loupe Server connection in the Log Initializing event of your application (not recommended for ASP.NET)">
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
    ///         Log.StartSession("Starting Loupe Desktop");
    ///  
    ///         //here you actually start up your application
    ///  
    ///         //and if we got to this point, we done good and can mark the session as being not crashed :)
    ///         Log.EndSession("Exiting Loupe Desktop");
    ///     }
    ///  
    ///     static void Log_Initializing(object sender, LogInitializingEventArgs e)
    ///     {
    ///         //and configure Loupe Server Connection
    ///         ServerConfiguration server = e.Configuration.Server;
    ///         server.UseGibraltarService = true;
    ///         server.CustomerName = "yOUR_SERVICE_NAME";
    ///         server.AutoSendSessions = true;
    ///         server.SendAllApplications = true;
    ///         server.PurgeSentSessions = true;
    ///     }
    /// }]]>
    /// 	</code>
    /// </example>
    public class ServerConfiguration
    {
        /// <summary>
        /// Initialize the server configuration from the application configuration
        /// </summary>
        public ServerConfiguration()
        {
            Enabled = true;
            AutoSendSessions = false;
            AutoSendOnError = true;
            SendAllApplications = false;
            PurgeSentSessions = false;
            UseGibraltarService = false;
            UseSsl = false;
            Port = 0;
        }

        /// <summary>
        /// True by default, disables server communication when false.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Indicates whether to automatically send session data to the server in the background.
        /// </summary>
        /// <remarks>Defaults to false, indicating data will only be sent on request via packager.</remarks>
        public bool AutoSendSessions { get; set; }

        /// <summary>
        /// Indicates whether to automatically send data to the server when error or critical messages are logged.
        /// </summary>
        /// <remarks>Defaults to true, indicating if the Auto Send Sessions option is also enabled data will be sent
        /// to the server after an error occurs (unless overridden by the MessageAlert event).</remarks>
        public bool AutoSendOnError { get; set; }

        /// <summary>
        /// Indicates whether to send data about all applications for this product to the server or just this application (the default)
        /// </summary>
        /// <remarks>Defaults to false, indicating just the current applications data will be sent.  Requires that AutoSendSessions is enabled.</remarks>
        public bool SendAllApplications { get; set; }

        /// <summary>
        /// Indicates whether to remove sessions that have been sent from the local repository once confirmed by the server.
        /// </summary>
        /// <remarks>Defaults to false.  Requires that AutoSendSessions is enabled.</remarks>
        public bool PurgeSentSessions { get; set; }

        /// <summary>
        /// The application key to use to communicate with the Loupe Server
        /// </summary>
        /// <remarks>Application keys identify the specific repository and optionally an application environment service
        /// for this session's data to be associated with.  The server administrator can determine by application key
        /// whether to accept the session data or not.</remarks>
        public string ApplicationKey { get; set; }

        /// <summary>
        /// The unique customer name when using the Loupe Cloud-Hosted Service
        /// </summary>
        public string CustomerName { get; set; }

        /// <summary>
        /// Indicates if the Loupe Cloud-Hosted Service should be used instead of a private Loupe Server
        /// </summary>
        /// <remarks>If true then the customer name must be specified.</remarks>
        public bool UseGibraltarService { get; set; }

        /// <summary>
        /// Indicates if the connection to the Loupe Server should be encrypted with Ssl. 
        /// </summary>
        /// <remarks>Only applies to a private Loupe Server.</remarks>
        public bool UseSsl { get; set; }

        /// <summary>
        /// The full DNS name of the server where the Loupe Server is located
        /// </summary>
        /// <remarks>Only applies to a private Loupe Server.</remarks>
        public string Server { get; set; }

        /// <summary>
        ///  An optional port number override for the server
        /// </summary>
        /// <remarks>Not required if the port is the traditional port (80 or 443).  Only applies to a private Loupe Server.</remarks>
        public int Port { get; set; }

        /// <summary>
        /// The virtual directory on the host for the private Loupe Server
        /// </summary>
        /// <remarks>Only applies to a private Loupe Server.</remarks>
        public string ApplicationBaseDirectory { get; set; }

        /// <summary>
        /// The specific repository on the server to send the session to
        /// </summary>
        /// <remarks>Only applies to a private Loupe Server running Enterprise Edition.</remarks>
        public string Repository { get; set; }

        /// <summary>
        /// Check the current configuration information to see if it's valid for a connection, throwing relevant exceptions if not.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid with the specific problem indicated in the message</exception>
        public void Validate()
        {
            //check a special case:  There is NO configuration information to speak of.
            if ((UseGibraltarService == false)
                && string.IsNullOrEmpty(ApplicationKey)
                && string.IsNullOrEmpty(CustomerName)
                && string.IsNullOrEmpty(Server))
            {
                //no way you even tried to configure the SDS.  lets use a different message.
                throw new InvalidOperationException("No server connection configuration could be found");
            }

            if (UseGibraltarService)
            {
                if (string.IsNullOrEmpty(ApplicationKey)
                    && string.IsNullOrEmpty(CustomerName))
                    throw new InvalidOperationException("An application key or service name is required to use the Loupe Service,");
            }
            else
            {
                if (string.IsNullOrEmpty(Server))
                    throw new InvalidOperationException("When using a self-hosted Loupe server a full server name is required");

                if (Port < 0)
                    throw new InvalidOperationException("When overriding the connection port, a positive number must be specified.  Use zero to accept the default port.");
            }
        }


        /// <summary>
        /// Normalize the configuration data
        /// </summary>
        public void Sanitize()
        {
            if (string.IsNullOrEmpty(Server))
            {
                Server = null;
            }
            else
            {
                Server = Server.Trim();
                if (Server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Server = Server.Substring(8);
                }
                else if (Server.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    Server = Server.Substring(7);
                }

                if (Server.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    Server = Server.Substring(0, Server.Length - 1);
                }
            }

            if (string.IsNullOrEmpty(CustomerName))
            {
                CustomerName = null;
            }
            else
            {
                CustomerName = CustomerName.Trim();
            }

            if (string.IsNullOrEmpty(ApplicationBaseDirectory))
            {
                ApplicationBaseDirectory = null;
            }
            else
            {
                ApplicationBaseDirectory = ApplicationBaseDirectory.Trim();

                if (ApplicationBaseDirectory.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    ApplicationBaseDirectory = ApplicationBaseDirectory.Substring(1);
                }

                if (ApplicationBaseDirectory.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    ApplicationBaseDirectory = ApplicationBaseDirectory.Substring(0, ApplicationBaseDirectory.Length - 1);
                }
            }

            if (Port < 0)
                Port = 0;

            if ((UseGibraltarService && string.IsNullOrEmpty(CustomerName))
                || (!UseGibraltarService && string.IsNullOrEmpty(Server)))
                Enabled = false; //we can't be enabled because we aren't plausibly configured.
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (UseGibraltarService)
                return string.Format("Loupe Cloud-Hosted Subscription '{0}'", CustomerName);

            if (string.IsNullOrWhiteSpace(Repository))
                return string.Format("Loupe Server '{0}'", Server);

            return string.Format("Loupe Server '{0}' repository '{1}'", Server, Repository);
        }
    }
}
