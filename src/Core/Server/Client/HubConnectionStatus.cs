using Loupe.Configuration;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Loupe.Core.Server.Client
{
    public class HubConnectionStatus
    {
        internal HubConnectionStatus(ServerConfiguration configuration, bool isValid, HubStatus status, string message)
            : this(configuration, null, null, isValid, status, message)
        {
        }

        internal HubConnectionStatus(ServerConfiguration configuration, WebChannel channel, HubRepository repository, bool isValid, HubStatus status, string message)
        {
            Configuration = configuration;
            Channel = channel;
            Repository = repository;
            Status = status;
            Message = message;
            IsValid = isValid;
        }

        public ServerConfiguration Configuration { get; private set; }

        public HubRepository Repository { get; private set; }

        /// <summary>
        /// The hub status of the final hub connected to.
        /// </summary>
        public HubStatus Status { get; private set; }

        /// <summary>
        /// An end-user display message providing feedback on why a connection is not available
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// True if the configuration is valid and the server is available, false otherwise.
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// The channel that was connected
        /// </summary>
        public WebChannel Channel { get; private set; }
    }
}
