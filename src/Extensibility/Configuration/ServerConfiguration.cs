using System;
using System.Text;

namespace Loupe.Configuration
{
    /// <summary>
    /// The application configuration information for sending session data to a Loupe Server
    /// </summary>
    /// <remarks>
    /// For more details on how to configure Loupe, see the <a href="https://doc.onloupe.com">Loupe Documentation</a>
    /// </remarks>
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
        public string? ApplicationKey { get; set; }

        /// <summary>
        /// The unique customer name when using the Gibraltar Loupe Service
        /// </summary>
        public string? CustomerName { get; set; }

        /// <summary>
        /// Indicates if the Gibraltar Loupe Service should be used instead of a private Loupe Server
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
        public string? Server { get; set; }

        /// <summary>
        ///  An optional port number override for the server
        /// </summary>
        /// <remarks>Not required if the port is the traditional port (80 or 443).  Only applies to a private Loupe Server.</remarks>
        public int Port { get; set; }

        /// <summary>
        /// The virtual directory on the host for the private Loupe Server
        /// </summary>
        /// <remarks>Only applies to a private Loupe Server.</remarks>
        public string? ApplicationBaseDirectory { get; set; }

        /// <summary>
        /// The specific repository on the server to send the session to
        /// </summary>
        /// <remarks>Only applies to a private Loupe Server running Enterprise Edition.</remarks>
        public string? Repository { get; set; }

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
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("\tEnabled: {0}\r\n", Enabled);

            if (Enabled)
            {
                if (UseGibraltarService)
                {
                    stringBuilder.AppendFormat("\tLoupe Cloud-Hosted Subscription '{0}'\r\n", CustomerName);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(Repository))
                    {
                        stringBuilder.AppendFormat("\tLoupe Server '{0}'", Server);
                    }
                    else
                    {
                        stringBuilder.AppendFormat("\tLoupe Server '{0}' repository '{1}'", Server, Repository);
                    }

                    stringBuilder.AppendFormat("\tUse Ssl: {0}\r\n", UseSsl);

                    if (Port != 0)
                        stringBuilder.AppendFormat("\tPort: {0}\r\n", Port);
                }

                if (string.IsNullOrEmpty(ApplicationKey) == false)
                    stringBuilder.AppendFormat("\tApplication Key: {0}\r\n", ApplicationKey);

                stringBuilder.AppendFormat("\tAuto Send Sessions: {0}\r\n", AutoSendSessions);
                stringBuilder.AppendFormat("\tSend All Applications: {0}\r\n", SendAllApplications);
                stringBuilder.AppendFormat("\tPurge Sent Sessions: {0}\r\n", PurgeSentSessions);

                stringBuilder.AppendFormat("\tAuto Send On Error: {0}\r\n", AutoSendOnError);
            }

            return stringBuilder.ToString();
        }
    }
}
