using System.Text;

namespace Loupe.Configuration
{
    /// <summary>
    /// Network Messenger Configuration
    /// </summary>
    public class NetworkViewerConfiguration : IMessengerConfiguration
    {
        /// <summary>
        /// Initialize the network messenger from the application configuration
        /// </summary>
        public NetworkViewerConfiguration()
        {
            Enabled = true;
            AllowLocalClients = true;
            AllowRemoteClients = false;
            MaxQueueLength = 2000;

        }

        /// <summary>
        /// True by default, enables connecting a viewer on the local computer when true.
        /// </summary>
        public bool AllowLocalClients { get; set; }

        /// <summary>
        /// False by default, enables connecting a viewer from another computer when true.
        /// </summary>
        /// <remarks>Requires a server configuration section</remarks>
        public bool AllowRemoteClients { get; set; }

        /// <summary>
        /// The maximum number of queued messages waiting to be processed by the network messenger
        /// </summary>
        /// <remarks>Once the total number of messages waiting to be processed exceeds the
        /// maximum queue length unsent messages will be dropped.</remarks>
        public int MaxQueueLength { get; set; }

        /// <summary>
        /// False by default. When false, the network messenger is disabled even if otherwise configured.
        /// </summary>
        /// <remarks>This allows for explicit disable/enable without removing the existing configuration
        /// or worrying about the default configuration.</remarks>
        public bool Enabled { get; set; }


        /// <summary>
        /// When true, the session file will treat all write requests as write-through requests.
        /// </summary>
        /// <remarks>This overrides the write through request flag for all published requests, acting
        /// as if they are set true.  This will slow down logging and change the degree of parallelism of 
        /// multithreaded applications since each log message will block until it is committed.</remarks>
        bool IMessengerConfiguration.ForceSynchronous => false;

        /// <summary>
        /// Normalize configuration
        /// </summary>
        public void Sanitize()
        {
            if (MaxQueueLength <= 0)
                MaxQueueLength = 2000;
            else if (MaxQueueLength > 50000)
                MaxQueueLength = 50000;
        }

        /// <inheritdoc />
        string IMessengerConfiguration.MessengerTypeName => "Gibraltar.Messaging.NetworkMessenger";

        /// <inheritdoc />
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("\tEnabled: {0}\r\n", Enabled);
            stringBuilder.AppendFormat("\tAllow Local Clients: {0}\r\n", AllowLocalClients);
            stringBuilder.AppendFormat("\tAllow Remote Clients: {0}\r\n", AllowRemoteClients);
            stringBuilder.AppendFormat("\tMax Queue Length: {0}\r\n", MaxQueueLength);

            return stringBuilder.ToString();
        }
    }
}
