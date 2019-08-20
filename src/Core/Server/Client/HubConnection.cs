using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Loupe.Server.Client.Data;
using Loupe.Server.Client.Internal;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Loupe.Server.Client
{
    /// <summary>
    /// A web channel specifically designed to work with the Loupe Server.
    /// </summary>
    [DebuggerDisplay("{EndUserTestUrl} Status: {Status}")]
    public class HubConnection : IDisposable
    {
        /// <summary>
        /// The web request header to add for our hash
        /// </summary>
        public const string SHA1HashHeader = "X-Gibraltar-Hash";

        internal const string LogCategory = "Loupe.Repository.Hub";
        internal const string LoupeServiceServerName = "hub.gibraltarsoftware.com";
        private const string LoupeServiceEntryPath = "Customers/{0}";
        private const string ApplicationKeyEntryPath = "Agent/{0}/";

        /// <summary>
        /// The version number for the new Loupe 3.0 features
        /// </summary>
        public static readonly Version Hub30ProtocolVersion = new Version(1, 2);

        /// <summary>
        /// The version number for the new Loupe 3.8 features
        /// </summary>
        public static readonly Version Hub38ProtocolVersion = new Version(1, 4);

        /// <summary>
        /// The latest version of the protocol we understand
        /// </summary>
        public static readonly Version ClientProtocolVersion = Hub38ProtocolVersion;

        private readonly object m_Lock = new object();
        private readonly object m_ChannelLock = new object();

        //these are the root connection parameters from the configuration.
        private readonly ServerConfiguration m_RootConfiguration;

        private string m_TestUrl;
        private WebChannel m_CurrentChannel;  //the current hub we're connected to. //PROTECTED BY CHANNELLOCK
        private bool m_EnableLogging; //PROTECTED BY LOCK

        //status information
        private readonly object m_StatusLock = new object();
        private volatile bool m_HaveTriedToConnect; //volatile instead of lock to avoid locks in locks
        private HubRepository m_HubRepository; //PROTECTED BY STATUSLOCK
        private HubConnectionStatus m_HubStatus; //PROTECTED BY STATUSLOCK

        //Security information.  if SupplyCredentials is set, then the other three items must be set.
        private bool m_UseCredentials; //PROTECTED BY LOCK
        private bool m_UseRepositoryCredentials; //PROTECTED BY LOCK
        private Guid m_ClientRepositoryId; //PROTECTED BY LOCK
        private string m_KeyContainerName; //PROTECTED BY LOCK
        private bool m_UseMachineStore; //PROTECTED BY LOCK

        /// <summary>
        /// Raised whenever the connection state changes.
        /// </summary>
        public event ChannelConnectionStateChangedEventHandler ConnectionStateChanged;

        /// <summary>
        /// Create a new server connection using the provided configuration
        /// </summary>
        /// <param name="configuration"></param>
        public HubConnection(ServerConfiguration configuration)
        {
            m_RootConfiguration = configuration;
        }

        /// <summary>
        /// The logger to use in this process
        /// </summary>
        public static IClientLogger Logger { get; set; }

        #region Public Properties and Methods

        /// <summary>
        /// Indicates if logging for events on the web channel is enabled or not.
        /// </summary>
        public bool EnableLogging
        {
            get { return m_EnableLogging; }
            set
            {
                lock (m_Lock)
                {
                    if (value != m_EnableLogging)
                    {
                        m_EnableLogging = value;

                        //update the existing channel, if necessary.
                        lock (m_ChannelLock)
                        {
                            if (m_CurrentChannel != null)
                            {
                                m_CurrentChannel.EnableLogging = m_EnableLogging;
                            }

                            System.Threading.Monitor.PulseAll(m_ChannelLock);
                        }
                    }

                    System.Threading.Monitor.PulseAll(m_Lock);
                }
            }
        }

        /// <summary>
        /// Identify our relationship Id and credential configuration for communicating with the server.
        /// </summary>
        public void SetCredentials(Guid clientRepositoryId, bool useApiKey, string keyContainerName, bool useMachineStore)
        {
            if (clientRepositoryId.Equals(Guid.Empty))
                throw new ArgumentNullException(nameof(clientRepositoryId));

            if (String.IsNullOrEmpty(keyContainerName))
                throw new ArgumentNullException(nameof(keyContainerName));

            lock (m_Lock)
            {
                m_UseCredentials = true;
                m_UseRepositoryCredentials = useApiKey;
                m_ClientRepositoryId = clientRepositoryId;
                m_KeyContainerName = keyContainerName;
                m_UseMachineStore = useMachineStore;

                System.Threading.Monitor.PulseAll(m_Lock);
            }
        }

        /// <summary>
        /// Attempts to connect to the server and returns information about the connection status.
        /// </summary>
        /// <remarks>This method will keep the connection if it is made, improving efficiency if you are then going to use the connection.</remarks>
        /// <returns>True if the configuration is valid and the server is available, false otherwise.</returns>
        public async Task<HubConnectionStatus> CanConnect()
        {
            WebChannel currentChannel;
            lock (m_ChannelLock)
            {
                currentChannel = m_CurrentChannel;
                System.Threading.Monitor.PulseAll(m_ChannelLock);
            }

            //if we have a current connection we'll need to see if we can keep using it
            if (currentChannel != null)
            {
                var status = HubStatus.Maintenance;
                try
                {
                    var configurationGetRequest = new HubConfigurationGetRequest();
                    await currentChannel.ExecuteRequest(configurationGetRequest, 1).ConfigureAwait(false); //we'd like it to succeed, so we'll give it one retry 

                    //now, if we got back a redirect we need to go THERE to get the status.
                    HubConfigurationXml configurationXml = configurationGetRequest.Configuration;
                    if ((configurationXml.redirectRequested == false) && (configurationXml.status == HubStatusXml.available))
                    {
                        //we can just keep using this connection, so lets do that.
                        return new HubConnectionStatus(null, true, HubStatus.Available, null);
                    }

                    status = (HubStatus)configurationXml.status; //we define these to be equal.
                }
                catch (Exception ex)
                {
                    if (!Logger.SilentMode)
                        Logger.Write(LogMessageSeverity.Information, ex, false, LogCategory, "Unable to get server configuration, connection will be assumed unavailable.", "Due to an exception we were unable to retrieve the server configuration.  We'll assume the server is in maintenance until we can succeed.  Exception: {0}\r\n", ex.Message);
                }

                //drop the connection - we might do better, unless we're already at the root.
                if (IsRootHub(currentChannel.HostName, currentChannel.Port, currentChannel.UseSsl, currentChannel.ApplicationBaseDirectory))
                {
                    //we are the root - what we are is the best we are.
                    string message = null;
                    switch (status)
                    {
                        case HubStatus.Expired:
                            message = "your subscription is expired";
                            break;
                        case HubStatus.Maintenance:
                            message = "the repository is in maintenance";
                            break;
                    }
                    return new HubConnectionStatus(null, false, status, message);
                }
            }

            //if we don't have a connection (either we didn't before or we just invalidated the current connection) get a new one.
            var connectionStatus = await Connect().ConfigureAwait(false);
            SetCurrentChannel(connectionStatus.Channel);

            //before we return, lets set our status to track what we just calculated.
            lock (m_StatusLock)
            {
                //make a copy of the connection status so the caller does NOT get our connection object.
                m_HubStatus = new HubConnectionStatus(connectionStatus.Configuration, null, connectionStatus.Repository, connectionStatus.IsValid, connectionStatus.Status, connectionStatus.Message);
                m_HubRepository = connectionStatus.Repository;
            }

            return connectionStatus;
        }

        /// <summary>
        /// Attempts to connected to the specified hub and returns information about the connection status.  The connection is then dropped.
        /// </summary>
        /// <param name="configuration">The configuration to test</param>
        /// <returns>The connection status information</returns>
        public static async Task<HubConnectionStatus> CanConnect(ServerConfiguration configuration)
        {
            var connectionStatus = await Connect(configuration).ConfigureAwait(false);

            if (connectionStatus.Status == HubStatus.Available)
            {
                //wait, one last check - what about protocol?
                if (connectionStatus.Repository.ProtocolVersion < Hub30ProtocolVersion)
                {
                    return new HubConnectionStatus(configuration, false, HubStatus.Maintenance, "The server is implementing an older, incompatible version of the hub protocol.");
                }
            }

            if (connectionStatus.Channel != null)
            {
                connectionStatus.Channel.Dispose();
            }

            //we don't want to return the status we got because it has a real channel on it.
            return new HubConnectionStatus(configuration, null, connectionStatus.Repository, connectionStatus.IsValid, connectionStatus.Status, connectionStatus.Message);
        }

        /// <summary>
        /// Execute the provided request.
        /// </summary>
        /// <param name="newRequest"></param>
        /// <param name="maxRetries">The maximum number of times to retry the connection.  Use -1 to retry indefinitely.</param>
        public async Task ExecuteRequest(IWebRequest newRequest, int maxRetries)
        {
            //make sure we have a channel
            WebChannel channel = await GetCurrentChannel().ConfigureAwait(false); //this throws exceptions when it can't connect and is thread safe.

            //if we have a channel and NOW get an exception, here is where we would recheck the status of our connection.
            bool retryAuthentication = false;
            bool resetAndRetry = false;
            try
            {
                await channel.ExecuteRequest(newRequest, maxRetries).ConfigureAwait(false);
            }
            catch (WebChannelAuthorizationException ex)
            {
                //request better credentials..
                Logger.Write(LogMessageSeverity.Warning, ex, true, LogCategory,
                    "Requesting updated credentials for the server connection due to " + ex.GetType(),
                    "We're going to assume the user can provide current credentials.\r\nDetails: {0}", ex.Message);
                if (CachedCredentialsManager.UpdateCredentials(channel, m_ClientRepositoryId, false))
                {
                    //they entered new creds.. lets give them a go.
                    retryAuthentication = true;
                }
                else
                {
                    //they canceled, lets call it.
                    throw;
                }
            }
            catch (WebChannelConnectFailureException)
            {
                //clear our current channel and try again if we're on a child server.
                if (IsRootHub(channel.HostName, channel.Port, channel.UseSsl, channel.ApplicationBaseDirectory) == false)
                {
                    resetAndRetry = true;
                }
            }

            if (retryAuthentication)
            {
                await ExecuteRequest(newRequest, maxRetries).ConfigureAwait(false);
            }
            else if (resetAndRetry)
            {
                await ResetChannel().ConfigureAwait(false); //safely clears the current channel and gets a fresh one if possible
            }
        }

        /// <summary>
        /// Create a new subscription to this hub for the supplied repository information and shared secret.
        /// </summary>
        /// <param name="repositoryXml"></param>
        /// <remarks></remarks>
        /// <returns>The client repository information retrieved from the server.</returns>
        public async Task<ClientRepositoryXml> CreateSubscription(ClientRepositoryXml repositoryXml)
        {
            var request = new ClientRepositoryUploadRequest(repositoryXml);

            //we have to use distinct credentials for this so we have to swap the credentials on the connection.
            var channel = await GetCurrentChannel().ConfigureAwait(false);

            bool retry;
            do
            {
                retry = false; //so we'll exit by default
                try
                {
                    await channel.ExecuteRequest(request, 1).ConfigureAwait(false);
                }
                catch (WebChannelAuthorizationException ex)
                {
                    //request better credentials..
                    Logger.Write(LogMessageSeverity.Warning, ex, true, LogCategory,
                              "Requesting updated credentials for the server connection due to " + ex.GetType(),
                              "We're going to assume the user can provide current credentials.\r\nDetails: {0}", ex.Message);
                    retry = CachedCredentialsManager.UpdateCredentials(channel, m_ClientRepositoryId, true);

                    if (retry == false)
                        throw;
                }
            } while (retry);

            return request.ResponseRepository;
        }
        
        /// <summary>
        /// Authenticate now (instead of waiting for a request to fail)
        /// </summary>
        public async Task Authenticate()
        {
            //get the current connection and authenticate it
            var channel = await GetCurrentChannel().ConfigureAwait(false);
            await channel.Authenticate().ConfigureAwait(false);
        }

        /// <summary>
        /// Indicates if the connection is currently authenticated.
        /// </summary>
        /// <value>False if no connection, connection doesn't support authentication, or connection is not authenticated.</value>
        public bool IsAuthenticated
        {
            get
            {
                bool isAuthenticated = false;

                lock (m_ChannelLock)
                {
                    if ((m_CurrentChannel != null) && (m_CurrentChannel.AuthenticationProvider != null))
                    {
                        isAuthenticated = m_CurrentChannel.AuthenticationProvider.IsAuthenticated;
                    }

                    System.Threading.Monitor.PulseAll(m_ChannelLock);
                }

                return isAuthenticated;
            }
        }

        /// <summary>
        /// Indicates if the connection is currently connected without attempting a new connection
        /// </summary>
        /// <value>False if no connection.  Connection may fail at any time.</value>
        public bool IsConnected
        {
            get
            {
                bool isConnected = false;

                lock (m_ChannelLock)
                {
                    if (m_CurrentChannel != null)
                    {
                        switch (m_CurrentChannel.ConnectionState)
                        {
                            case ChannelConnectionState.Connected:
                            case ChannelConnectionState.TransferingData:
                                isConnected = true;
                                break;
                        }
                    }

                    System.Threading.Monitor.PulseAll(m_ChannelLock);
                }

                return isConnected;
            }
        }

        /// <summary>
        /// Information about the remote repository
        /// </summary>
        /// <remarks>Returns null when no server can be contacted</remarks>
        /// <returns></returns>
        public async Task<HubRepository> GetRepository()
        {
            await EnsureConnectAttempted().ConfigureAwait(false);
            lock(m_StatusLock)
            {
                return m_HubRepository;
            }
        }

        /// <summary>
        /// The current connection status
        /// </summary>
        /// <returns></returns>
        public async Task<HubConnectionStatus> GetStatus()
        {
            await EnsureConnectAttempted().ConfigureAwait(false);
            lock(m_StatusLock)
            {
                return m_HubStatus;
            }
        }

        /// <summary>
        /// Reset the current connection and re-establish it, getting the latest hub configuration.
        /// </summary>
        public async Task Reconnect()
        {
            await ResetChannel().ConfigureAwait(false);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Dispose managed resources
        /// </summary>
        /// <param name="releaseManaged"></param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (releaseManaged)
                SetCurrentChannel(null);
        }

        /// <summary>
        /// Raises the ConnectionStateChanged event
        /// </summary>
        /// <param name="state">The new connection state</param>
        /// <remarks>Note to inheritors:  be sure to call the base implementation to ensure the event is raised.</remarks>
        protected virtual void OnConnectionStateChanged(ChannelConnectionState state)
        {
            ChannelConnectionStateChangedEventHandler tempEvent = ConnectionStateChanged;

            if (tempEvent != null)
            {
                ChannelConnectionStateChangedEventArgs e = new ChannelConnectionStateChangedEventArgs(state);
                tempEvent.Invoke(this, e);
            }
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Make sure we've at least tried to connect to the hub.
        /// </summary>
        private async Task EnsureConnectAttempted()
        {
            if (m_HaveTriedToConnect == false)
            {
                await GetCurrentChannel().ConfigureAwait(false); //this will try to connect.
            }
        }

        private async Task<WebChannel> GetCurrentChannel()
        {
            WebChannel currentChannel;
            lock (m_ChannelLock)
            {
                currentChannel = m_CurrentChannel;
                System.Threading.Monitor.PulseAll(m_ChannelLock);
            }

            //CAREFUL:  Between here and the end of this if do not access a property that
            //checks m_HaveTriedToConnect because if so we get into a big bad loop.
            if (currentChannel == null)
            {
                try
                {
                    //try to connect.
                    var connectionStatus = await Connect().ConfigureAwait(false);

                    WebChannel newChannel = connectionStatus.Channel;

                    //before we return, lets set our status to track what we just calculated.
                    lock (m_StatusLock)
                    {
                        //make a copy of the connection status without the connection so they can't accidentally call it.
                        m_HubStatus = new HubConnectionStatus(connectionStatus.Configuration, null, connectionStatus.Repository, connectionStatus.IsValid, connectionStatus.Status, connectionStatus.Message);
                        m_HubRepository = connectionStatus.Repository;
                    }

                    //if we couldn't connect we'll have a null channel (connect returns null)
                    if (newChannel == null)
                    {
                        throw new WebChannelConnectFailureException(connectionStatus.Message);
                    }

                    //otherwise we need to bind up our events and release the existing - use our setter for that
                    SetCurrentChannel(newChannel);
                    currentChannel = newChannel;
                }
                finally
                {
                    //whether we succeeded or failed, we tried.
                    m_HaveTriedToConnect = true;
                }
            }

            return currentChannel;
        }

        private void SetCurrentChannel(WebChannel channel)
        {
            lock (m_ChannelLock)
            {
                //are they the SAME? if so nothing to do
                if (ReferenceEquals(channel, m_CurrentChannel))
                    return;

                //otherwise, release any existing connection...
                if (m_CurrentChannel != null)
                {
                    m_CurrentChannel.Dispose();
                    m_CurrentChannel.ConnectionStateChanged -= CurrentChannel_ConnectionStateChanged;
                    m_CurrentChannel = null;

                    m_HaveTriedToConnect = false;
                }

                //and establish the new connection.
                if (channel != null)
                {
                    m_CurrentChannel = channel;
                    m_CurrentChannel.ConnectionStateChanged += CurrentChannel_ConnectionStateChanged;
                }

                System.Threading.Monitor.PulseAll(m_ChannelLock);
            }
        }

        /// <summary>
        /// Get a test URL to access through a web browser.
        /// </summary>
        public string EndUserTestUrl
        {
            get
            {
                if (String.IsNullOrEmpty(m_TestUrl))
                {
                    string fullHubUrl;
                    WebChannel channel = null;
                    try
                    {
                        //first try to resolve it through a real connection to determine the effective server based on redirection
                        var connectionStatusTask = Connect(m_RootConfiguration);
                        connectionStatusTask.Wait(new TimeSpan(0, 0, 15));

                        //if we weren't able to connect fully we will have gotten a null channel; create just a configured channel with the parameters.
                        if ((connectionStatusTask.Result == null) || (connectionStatusTask.Result.Channel == null))
                        {
                            channel = CreateChannel(m_RootConfiguration);
                        }
                        else
                        {
                            channel = connectionStatusTask.Result.Channel;
                        }

                        fullHubUrl = channel.EntryUri;
                    }
                    finally
                    {
                        if (channel != null)
                            channel.Dispose();
                    }

                    //if this is a hub URL we need to pull off the HUB suffix to make it a valid HTML URL.
                    if (String.IsNullOrEmpty(fullHubUrl) == false)
                    {
                        if (fullHubUrl.EndsWith("HUB", StringComparison.OrdinalIgnoreCase))
                        {
                            fullHubUrl = fullHubUrl.Remove(fullHubUrl.Length - 4); //-3 for HUB, -1 to offset length to start position                        
                        }
                        else if (fullHubUrl.EndsWith("HUB/", StringComparison.OrdinalIgnoreCase))
                        {
                            fullHubUrl = fullHubUrl.Remove(fullHubUrl.Length - 4); //-3 for HUB/, -1 to offset length to start position                        
                        }
                    }
                    m_TestUrl = fullHubUrl;
                }

                return m_TestUrl;
            }
        }

        /// <summary>
        /// The URL to the server's version info structure.
        /// </summary>
        public string UpdateUrl
        {
            get 
            { 
                var testUrl = EndUserTestUrl; //we are relying on the implementation of this pointing to the base of the tenant.
                return testUrl + "Hub/VersionInfo.ini";
            }
        }

        /// <summary>
        /// Reset the stored channel and reconnect.
        /// </summary>
        /// <returns></returns>
        private async Task<WebChannel> ResetChannel()
        {
            //force the channel to drop..
            SetCurrentChannel(null);

            //and get a fresh one...
            var newChannel = await GetCurrentChannel().ConfigureAwait(false);

            return newChannel;
        }

        /// <summary>
        /// Connect to the hub (or another hub if the configured hub is redirecting)
        /// </summary>
        /// <returns>The last web channel it was able to connect to after processing redirections, if that channel is available.</returns>
        private async Task<HubConnectionStatus> Connect()
        {
            var connectionStatus = await Connect(m_RootConfiguration).ConfigureAwait(false);
            if (connectionStatus.Channel != null)
            {
                //copy our current settings into it.
                lock (m_Lock)
                {
                    connectionStatus.Channel.EnableLogging = m_EnableLogging;

                    if (m_UseCredentials)
                    {
                        connectionStatus.Channel.AuthenticationProvider = CachedCredentialsManager.GetCredentials(connectionStatus.Channel, m_UseRepositoryCredentials, m_ClientRepositoryId, m_KeyContainerName, m_UseMachineStore);
                    }

                    System.Threading.Monitor.PulseAll(m_Lock);
                }
            }

            return connectionStatus;
        }

        /// <summary>
        /// Connects to the specified hub (or another hub if this hub is redirecting)
        /// </summary>
        /// <returns>The last web channel it was able to connect to after processing redirections.</returns>
        private static async Task<HubConnectionStatus> Connect(ServerConfiguration configuration)
        {
            WebChannel channel = null;
            bool canConnect = true;
            HubStatus status = HubStatus.Maintenance; //a reasonable default.
            string statusMessage = null;
            Guid? serverRepositoryId = null;
            DateTimeOffset? expirationDt = null;
            Version protocolVersion = new Version(0,0);
            NetworkConnectionOptions agentLiveStream = null;
            NetworkConnectionOptions clientLiveStream = null;

            //first, is it a valid config?  No point in trying to connect if it's a bum config.
            HubConnectionStatus connectionStatus;
            try
            {
                configuration.Validate();
            }
            catch (Exception ex)
            {
                canConnect = false;
                statusMessage = "Invalid configuration: " + ex.Message;
                connectionStatus = new HubConnectionStatus(configuration, false, status, statusMessage);
                return connectionStatus;
            }

            //and now try to connect to the server
            try
            {
                channel = CreateChannel(configuration);
                var configurationGetRequest = new HubConfigurationGetRequest();
                await channel.ExecuteRequest(configurationGetRequest, 1).ConfigureAwait(false); //we'd like it to succeed, so we'll give it one retry 

                var configurationXml = configurationGetRequest.Configuration;

                //now, if we got back a redirect we need to go THERE to get the status.
                if (configurationXml.redirectRequested)
                {
                    //recursively try again.
                    channel.Dispose();
                    connectionStatus = await Connect(configurationXml.ToServerConfiguration()).ConfigureAwait(false);
                }
                else
                {
                    //set the right status message
                    status = (HubStatus)configurationXml.status;

                    switch (status)
                    {
                        case HubStatus.Available:
                            break;
                        case HubStatus.Expired:
                            statusMessage = "The Server's license has expired.  " + (configuration.UseGibraltarService ? "You can reactivate your license in seconds at www.GibraltarSoftware.com." : "To renew your license, run the Administration tool on the Loupe Server.");
                            break;
                        case HubStatus.Maintenance:
                            statusMessage = "The Server is currently undergoing maintenance and can't process requests.";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("status");
                    }

                    if (configurationXml.id != null)
                    {
                        serverRepositoryId = new Guid(configurationXml.id);
                    }

                    if (configurationXml.expirationDt != null)
                    {
                        expirationDt = DataConverter.FromDateTimeOffsetXml(configurationXml.expirationDt);
                    }

                    string publicKey = configurationXml.publicKey;

                    if (String.IsNullOrEmpty(configurationXml.protocolVersion) == false)
                    {
                        protocolVersion = new Version(configurationXml.protocolVersion);
                    }

                    LiveStreamServerXml liveStreamConfig = configurationXml.liveStream;
                    if (liveStreamConfig != null)
                    {
                        agentLiveStream = new NetworkConnectionOptions { HostName = channel.HostName, Port = liveStreamConfig.agentPort, UseSsl = liveStreamConfig.useSsl };
                        clientLiveStream = new NetworkConnectionOptions { HostName = channel.HostName, Port = liveStreamConfig.clientPort, UseSsl = liveStreamConfig.useSsl };
                    }

                    //We've connected for sure, time to set up our connection status to return to our caller with the full connection info
                    connectionStatus = new HubConnectionStatus(configuration, channel, new HubRepository(expirationDt, serverRepositoryId, protocolVersion, publicKey, agentLiveStream, clientLiveStream),
                                                   true, status, statusMessage);
                }
            }
            catch (WebChannelFileNotFoundException)
            {
                canConnect = false;
                if (configuration.UseGibraltarService)
                {
                    //we'll treat file not found (e.g. customer never existed) as expired to get the right UI behavior.
                    status = HubStatus.Expired;
                    statusMessage = "The specified customer name is not valid";
                }
                else
                {
                    statusMessage = "The server does not support this service or the specified directory is not valid";
                }

                connectionStatus = new HubConnectionStatus(configuration, false, status, statusMessage);
            }
            catch (WebException ex)
            {
                canConnect = false;
                HttpWebResponse response = (HttpWebResponse)ex.Response;
                statusMessage = response.StatusDescription; //by default we'll use the detailed description we got back from the web server.

                //we want to be somewhat more intelligent in our responses to decode what these might MEAN.
                if (configuration.UseGibraltarService)
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.NotFound:
                        case HttpStatusCode.BadRequest:
                            status = HubStatus.Expired;
                            statusMessage = "The specified customer name is not valid";
                            break;
                    }
                }
                else
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.NotFound:
                            statusMessage = "No service could be found with the provided information";
                            break;
                        case HttpStatusCode.BadRequest:
                            statusMessage = "The server does not support this service or the specified directory is not valid";
                            break;
                    }
                }

                connectionStatus = new HubConnectionStatus(configuration, false, status, statusMessage);
            }
            catch (Exception ex)
            {
                canConnect = false;
                statusMessage = ex.Message;

                connectionStatus = new HubConnectionStatus(configuration, false, status, statusMessage);
            }

            //before we return make sure we clean up an errant channel if we don't need it.
            if ((canConnect == false) && (channel != null))
            {
                channel.Dispose();
                channel = null;
            }

            return connectionStatus;
        }

        /// <summary>
        /// Create a web channel to the specified server configuration.  Low level primitive that does no redirection.
        /// </summary>
        private static WebChannel CreateChannel(ServerConfiguration configuration)
        {
            WebChannel channel;

            if (configuration.UseGibraltarService)
            {
                string entryPath = string.IsNullOrEmpty(configuration.ApplicationKey)
                    ? string.Format(LoupeServiceEntryPath, configuration.CustomerName)
                    : string.Format(ApplicationKeyEntryPath, configuration.ApplicationKey);


                channel = new WebChannel(Logger, true, LoupeServiceServerName, entryPath, ClientProtocolVersion);
            }
            else
            {
                //we need to create the right application base directory to get into Hub.
                string entryPath = EffectiveApplicationBaseDirectory(configuration.ApplicationKey, configuration.ApplicationBaseDirectory, configuration.Repository);

                //and now we can actually create the channel!  Yay!
                channel = new WebChannel(Logger, configuration.UseSsl, configuration.Server, configuration.Port, entryPath, ClientProtocolVersion);
            }

            return channel;
        }

        /// <summary>
        /// Combines application base directory (if not null) and repository (if not null) into one merged path.
        /// </summary>
        private static string EffectiveApplicationBaseDirectory(string applicationKey, string applicationBaseDirectory, string repository)
        {
            string effectivePath = applicationBaseDirectory ?? string.Empty;

            if (string.IsNullOrEmpty(effectivePath) == false)
            {
                //check for whether we need to Extension a slash.
                if (effectivePath.EndsWith("/") == false)
                {
                    effectivePath += "/";
                }
            }

            if (string.IsNullOrEmpty(applicationKey) == false)
            {
                //we have an app key for that effective path which gets priority over any repository.
                effectivePath += string.Format(ApplicationKeyEntryPath, applicationKey);
            }
            else if (string.IsNullOrEmpty(repository) == false)
            {
                //we want a specific repository - which was created for Loupe Service so it assumes everyone's a "customer".  Oh well.
                effectivePath += string.Format(LoupeServiceEntryPath, repository);
            }

            return effectivePath;
        }


        /// <summary>
        /// Indicates if we're on the original configured server (the "root") or have been redirected.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="port"></param>
        /// <param name="useSsl"></param>
        /// <param name="applicationBaseDirectory"></param>
        /// <returns></returns>
        private bool IsRootHub(string hostName, int port, bool useSsl, string applicationBaseDirectory)
        {
            bool isSameHub = true;

            if (String.IsNullOrEmpty(hostName))
            {
                //can't be the same - invalid host
                isSameHub = false;
            }
            else
            {
                if (m_RootConfiguration.UseGibraltarService)
                {
                    if (hostName.Equals(LoupeServiceServerName, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        //it's the wrong server.
                        isSameHub = false;
                    }

                    string entryPath = String.Format(LoupeServiceEntryPath, m_RootConfiguration.CustomerName);

                    if (String.Equals(entryPath, applicationBaseDirectory) == false)
                    {
                        //it isn't the same customer
                        isSameHub = false;
                    }
                }
                else
                {
                    //simpler - we're looking for an exact match on each item.
                    if ((hostName.Equals(m_RootConfiguration.Server, StringComparison.OrdinalIgnoreCase) == false)
                        || (m_RootConfiguration.Port != port)
                        || (m_RootConfiguration.UseSsl != useSsl))
                    {
                        //it's the wrong server.
                        isSameHub = false;
                    }
                    else
                    {
                        //application base directory is more complicated - we have to take into account if we have a repository set or not.
                        var entryPath = EffectiveApplicationBaseDirectory(m_RootConfiguration.ApplicationKey, m_RootConfiguration.ApplicationBaseDirectory, m_RootConfiguration.Repository);

                        if (String.Equals(entryPath, applicationBaseDirectory) == false)
                        {
                            //it isn't the same repository
                            isSameHub = false;
                        }
                    }
                }
            }

            return isSameHub;
        }


        #endregion

        #region Event Handlers

        private void CurrentChannel_ConnectionStateChanged(object sender, ChannelConnectionStateChangedEventArgs e)
        {
            OnConnectionStateChanged(e.State);
        }

        #endregion
    }
}
