using System.Net;
using Gibraltar.Data;
using Gibraltar.Messaging;
using Gibraltar.Messaging.Net;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Communicates between an Agent and a Loupe Server
    /// </summary>
    internal class LiveSessionPublisher : NetworkClient
    {
        private readonly NetworkMessenger m_Messenger;
        private readonly LocalServerDiscoveryFile m_DiscoveryFile;

        private readonly object m_Lock = new object();

        /// <summary>
        /// Create a new connection with the specified options
        /// </summary>
        public LiveSessionPublisher(NetworkMessenger messenger, NetworkConnectionOptions options)
            : this(messenger, options, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
        }
        
        /// <summary>
        /// Create a new connection with the specified options
        /// </summary>
        public LiveSessionPublisher(NetworkMessenger messenger, NetworkConnectionOptions options, int majorVersion, int minorVersion)
            : base(options, true, majorVersion, minorVersion)
        {
            if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "New live sessions publisher being created", "Configuration:\r\n{0}", options);

            lock(m_Lock) //since we promptly access these variables from another thread, I'm adding this as paranoia to ensure they get synchronized.
            {
                m_Messenger = messenger;
            }
        }

        public LiveSessionPublisher(NetworkMessenger messenger, LocalServerDiscoveryFile discoveryFile)
            : this(messenger, new NetworkConnectionOptions()
                                  {
                                      HostName = IPAddress.Loopback.ToString(),
                                      Port = discoveryFile.PublisherPort
                                  })
        {
            m_DiscoveryFile = discoveryFile;
        }

        #region Public Properties and methods

        /// <summary>
        /// Send a copy of the latest session summary information to the server
        /// </summary>
        public void SendSummary()
        {
            var headerPacket = new SessionHeaderMessage(new SessionHeader(Log.SessionSummary));
            SendMessage(headerPacket);            
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Implemented to complete the protocol connection
        /// </summary>
        /// <returns>True if a connection was successfully established, false otherwise.</returns>
        protected override bool Connect()
        {
            //identify ourselves as a control channel
            var startCommandPacket = new RegisterAgentCommandMessage(Log.SessionSummary.Id);
            SendMessage(startCommandPacket);

            SendSummary();

            return true;
        }

        /// <summary>
        /// Implemented to transfer data on an established connection
        /// </summary>
        protected override void TransferData()
        {
            //we only ever use the network serializer.
            NetworkMessage nextPacket = null;
            do
            {
                nextPacket = ReadNetworkMessage();

                if (nextPacket != null)
                {
                    var viewStartCommand = nextPacket as LiveViewStartCommandMessage;
                    var sendSessionCommand = nextPacket as SendSessionCommandMessage;

                    if (viewStartCommand != null)
                    {
                        viewStartCommand.Validate();
                        //we need to initiate an outbound viewer to the same destination we point to.
                        m_Messenger.StartLiveView(GetOptions(), viewStartCommand.RepositoryId, viewStartCommand.ChannelId, viewStartCommand.SequenceOffset);
                    }
                    else if (sendSessionCommand != null)
                    {
                        //send to server baby!
#pragma warning disable 4014
                        m_Messenger.SendToServer((SendSessionCommandMessage)nextPacket);
#pragma warning restore 4014
                    }
                }

            } while (nextPacket != null); //it will go to null when the connection closes
        }

        /// <summary>
        /// Allows a derived class to implement its own retry delay strategy
        /// </summary>
        /// <param name="defaultDelayMs">The number of Milliseconds to wait before retrying</param>
        /// <returns>true if any retry should be attempted</returns>
        protected override bool RetryDelay(ref int defaultDelayMs)
        {
            //make sure we're still alive..
            if (m_DiscoveryFile != null)
            {
                return m_DiscoveryFile.IsAlive;
            }

            return true;
        }

        #endregion

        #region Private Properties and Methods


        #endregion
    }
}
