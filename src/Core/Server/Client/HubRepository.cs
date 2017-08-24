using System;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Information about the capabilities and status of a server repository
    /// </summary>
    public class HubRepository
    {
        internal HubRepository(DateTimeOffset? expirationDt, Guid? serverRepositoryId, Version protocolVersion, string publicKey, 
            NetworkConnectionOptions agentOptions, NetworkConnectionOptions clientOptions)
        {
            ExpirationDt = expirationDt;
            ServerRepositoryId = serverRepositoryId;
            ProtocolVersion = protocolVersion;
            PublicKey = publicKey;
            AgentLiveStreamOptions = agentOptions;
            ClientLiveStreamOptions = clientOptions;
        }

        public DateTimeOffset? ExpirationDt { get; private set; }

        public Guid? ServerRepositoryId { get; private set; }

        public Version ProtocolVersion { get; private set; }

        public string PublicKey { get; private set; }

        public NetworkConnectionOptions AgentLiveStreamOptions { get; private set; }

        public NetworkConnectionOptions ClientLiveStreamOptions { get; private set; }

        /// <summary>
        /// Indicates if the server supports file fragments or just a single stream per session
        /// </summary>
        public bool SupportsFileFragments
        {
            get
            {
                if (ProtocolVersion >= HubConnection.Hub30ProtocolVersion) //we introduced file fragments in 1.2
                    return true;
                return false;
            }
        }

        /// <summary>
        /// Indicates if the server supports the API for log events, etc.
        /// </summary>
        public bool SupportsServerApi
        {
            get
            {
                if (ProtocolVersion >= HubConnection.Hub38ProtocolVersion) //we introduced file fragments in 1.3
                    return true;
                return false;
            }
        }
    }
}
