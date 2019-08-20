using System;
using System.Diagnostics;
using System.Net.Sockets;
using Loupe.Data;
using Loupe.Messaging.Net;
using Loupe.Monitor;
using Loupe.Monitor.Serialization;
using Loupe.Server.Client;
using Loupe.Extensibility.Data;



namespace Loupe.Messaging
{
    /// <summary>
    /// Used by the agent to write session data to a network socket
    /// </summary>
    [DebuggerDisplay("Connected: {IsConnected} Failed: {IsFailed} Closed: {IsClosed}")]
    internal class NetworkWriter: NetworkClient
    {
        private readonly object m_Lock = new object();
        private readonly NetworkMessenger m_Messenger;
        private readonly long m_SequenceOffset;
        private readonly Guid m_ChannelId;
        private readonly Guid m_RepositoryId;

        /// <summary>
        /// Create a new network writer for a remote server
        /// </summary>
        public NetworkWriter(NetworkMessenger messenger, NetworkConnectionOptions options, Guid repositoryId, Guid channelId, long sequenceOffset = 0)
            : this(messenger, options, repositoryId, channelId, sequenceOffset, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
        }

        /// <summary>
        /// Create a new network writer for a remote server
        /// </summary>
        public NetworkWriter(NetworkMessenger messenger, NetworkConnectionOptions options, Guid repositoryId, Guid channelId, long sequenceOffset, int majorVersion, int minorVersion)
            : base(options, false, majorVersion, minorVersion)
        {
            if (channelId == Guid.Empty)
                throw new ArgumentNullException(nameof(channelId));

            lock(m_Lock) //since we promptly access these variables from another thread, I'm adding this as paranoia to ensure they get synchronized.
            {
                m_Messenger = messenger;
                m_RepositoryId = repositoryId;
                m_ChannelId = channelId;
                m_SequenceOffset = sequenceOffset;
            }
        }

        /// <summary>
        /// Create a new network writer for a connected socket
        /// </summary>
        public NetworkWriter(NetworkMessenger messenger, TcpClient tcpClient, Guid repositoryId, Guid channelId, long sequenceOffset = 0)
            : this(messenger, tcpClient, repositoryId, channelId, sequenceOffset, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
        }

        /// <summary>
        /// Create a new network writer for a connected socket
        /// </summary>
        public NetworkWriter(NetworkMessenger messenger, TcpClient tcpClient, Guid repositoryId, Guid channelId, long sequenceOffset, int majorVersion, int minorVersion)
            : base(tcpClient, majorVersion, minorVersion)
        {
            if (channelId == Guid.Empty)
                throw new ArgumentNullException(nameof(channelId));

            lock(m_Lock) //since we promptly access these variables from another thread, I'm adding this as paranoia to ensure they get synchronized.
            {
                m_Messenger = messenger;
                m_RepositoryId = repositoryId;
                m_ChannelId = channelId;
                m_SequenceOffset = sequenceOffset;
            }
        }

        #region Public Properties and Methods

        /// <summary>
        /// Write the provided packet to the client stream (synchronously)
        /// </summary>
        /// <param name="packets"></param>
        /// <remarks>Throws exceptions if there is a connection failure.</remarks>
        public void Write(IMessengerPacket[] packets)
        {
            lock (m_Lock)
            {
                if ((IsFailed) || (IsClosed))
                    return;

                foreach (IMessengerPacket packet in packets)
                {
                    Write(packet);
                }
            }
        }

        /// <summary>
        /// Write the provided packet to the client stream (synchronously)
        /// </summary>
        /// <param name="packet"></param>
        public void Write(IMessengerPacket packet)
        {
            lock (m_Lock)
            {
                if ((IsFailed) || (IsClosed))
                    return;

                //we don't send across all types - just a few we understand.
                if (CanWritePacket(packet))
                {
                    SendPacket(packet);
                }
            }
        }

        /// <summary>
        /// Indicates if we can write the specified packet.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static bool CanWritePacket(IMessengerPacket packet)
        {
                //we don't send across all types - just a few we understand.
            if ((packet is LogMessagePacket)
                || (packet is ApplicationUserPacket)
                || (packet is ExceptionInfoPacket)
                || (packet is ThreadInfoPacket)
                || (packet is SessionSummaryPacket))
            {
#if DEBUG
                //BUG: This is a check to see why we're getting one byte session summaries.
                if (packet is SessionSummaryPacket summaryPacket)
                {
                    if ((string.IsNullOrEmpty(summaryPacket.ProductName)) && (Debugger.IsAttached))
                        Debugger.Break(); // Stop in debugger, ignore in production.
                }
#endif

                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Protected Properties and methods

        /// <summary>
        /// Implemented to complete the protocol connection
        /// </summary>
        /// <returns>True if a connection was successfully established, false otherwise.</returns>
        protected override bool Connect()
        {
            //because we're used inside of the writing side of a messenger we have to be sure our thread doesn't block.
            Publisher.ThreadMustNotBlock();

            bool connected = false;

            //tell the other end who we are to start the conversation
            SendMessage(new LiveViewStartCommandMessage(m_RepositoryId, Log.SessionSummary.Id, m_ChannelId));

            //and then wait to hear that we should start our packet stream.
            NetworkMessage nextPacket;
            do
            {
                nextPacket = ReadNetworkMessage();
                if (nextPacket != null)
                {
                    if (nextPacket is LiveViewStopCommandMessage)
                    {
                        //we are going to shut down the connection
                        RemoteClose();
                    }
                    else if (nextPacket is PacketStreamStartCommandMessage)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Information, LogCategory, "Packet Stream start command received from server", "Received the start command, now we will switch over to the session stream data.\r\n{0}", this);

                        //initialization is tetchy - we have to get the header, the cache, and be added to the list in one blow
                        //to be sure we get all of the packets we should.
                        m_Messenger.ActivateWriter(this, m_SequenceOffset); 
                        connected = true;
                    }
                }
            } while ((nextPacket != null) && (connected == false));

            return connected;
        }

        /// <summary>
        /// Implemented to transfer data on an established connection
        /// </summary>
        protected override void TransferData()
        {
            NetworkMessage nextPacket;
            do
            {
                nextPacket = ReadNetworkMessage();

                if (nextPacket != null)
                {
                    if (nextPacket is LiveViewStopCommandMessage)
                    {
                        //time to end...
                        RemoteClose();
                    }
                    else if (nextPacket is SendSessionCommandMessage packet)
                    {
                        //send to server baby!
#pragma warning disable 4014
                        m_Messenger.SendToServer(packet);
#pragma warning restore 4014
                    }
                }
            } while (nextPacket != null);
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for NetworkWriter events
    /// </summary>
    public class NetworkEventArgs: EventArgs
    {
        /// <summary>
        /// Create a new network event arguments object
        /// </summary>
        /// <param name="description"></param>
        public NetworkEventArgs(string description)
        {
            Description = description;    
        }

        /// <summary>
        /// An extended description of the cause of the event
        /// </summary>
        public string Description { get; private set; }
    }

    /// <summary>
    /// Delegate for handling NetworkWriter events
    /// </summary>
    public delegate void NetworkEventHandler(object sender, NetworkEventArgs e);
}
