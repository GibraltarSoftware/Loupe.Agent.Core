using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Loupe.Core.Monitor;
using Loupe.Core.Server.Client;
using Loupe.Configuration;
using Loupe.Core.IO.Messaging;
using Loupe.Core.IO.Serialization;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Loupe.Core.Messaging
{
    internal class NetworkMessenger : MessengerBase
    {
        /// <summary>
        /// The log category to use for messages in this messenger
        /// </summary>
        public new const string LogCategory = "Loupe.Network.Messenger";

        private const int DefaultBufferSize = 1000;
        private int m_BufferSize = DefaultBufferSize;

        private readonly object m_Lock = new object();

        //NOTE TO MAINTAINERS:  Locking is granular based on the connection role on the following collections, be warned!
        private readonly Queue<IMessengerPacket> m_Buffer = new Queue<IMessengerPacket>(); // LOCKED BY ACTIVE CLIENTS
        private readonly List<NetworkWriter> m_PendingClients = new List<NetworkWriter>();
        private readonly List<NetworkWriter> m_ActiveClients = new List<NetworkWriter>();
        private readonly List<NetworkWriter> m_DeadClients = new List<NetworkWriter>();

        //if we allow local connections this is the index of the ones we're currently tracking.
        private readonly Dictionary<string, LiveSessionPublisher> m_LocalProxyConnections = new Dictionary<string, LiveSessionPublisher>(StringComparer.OrdinalIgnoreCase); //lock this for data integrity

        private LocalServerDiscoveryFileMonitor m_DiscoveryFileMonitor;

        private volatile bool m_ActiveRemoteConnectionAttempt; //for making sure we only do one server connection attempt at a time
        private HubConnection m_HubConnection; //our one and only server connection, if enabled
        private LiveSessionPublisher m_Client;
        private bool m_EnableOutbound;
        private NetworkConnectionOptions m_ConnectionOptions;
        private bool m_IsClosed;

        private DateTimeOffset m_HubConfigurationExpiration; // LOCKED BY LOCK

        /// <summary>
        /// Create a new network messenger
        /// </summary>
        public NetworkMessenger()
            : base("Network", false)
        {
        }

        #region Public Properties and Methods

        /// <summary>
        /// The list of cached packets that should be in every stream before any other packet.
        /// </summary>
        public ICachedMessengerPacket[] HeaderPackets { get { return Publisher.HeaderPackets; } }

        /// <summary>
        /// Create a new outbound live viewer to the default server
        /// </summary>
        public void StartLiveView(Guid repositoryId, Guid channelId, long sequenceOffset = 0)
        {
            if (m_ConnectionOptions != null)
            {
                StartLiveView(m_ConnectionOptions, repositoryId, channelId, sequenceOffset);
            }
        }

        /// <summary>
        /// Create a new outbound live viewer to the default server
        /// </summary>
        public void StartLiveView(NetworkConnectionOptions options, Guid repositoryId, Guid channelId, long sequenceOffset = 0)
        {
            if (channelId == Guid.Empty)
                throw new ArgumentNullException(nameof(channelId));

            //open an outbound pending connection.
            var newWriter = new NetworkWriter(this, options, repositoryId, channelId, sequenceOffset);
            newWriter.Start();
            RegisterWriter(newWriter);
        }

        /// <summary>
        /// Send the matching sessions to the server
        /// </summary>
        /// <param name="sendSessionCommand"></param>
        public async Task SendToServer(SendSessionCommandMessage sendSessionCommand)
        {
            try
            {
                if (await Log.SendSessions(sendSessionCommand.Criteria, null, true).ConfigureAwait(false) == false) //we love async!
                {
                    //since we can't send sessions to the server we'll just roll over the log file for local access.
                    Log.EndFile("Remote Live Sessions client requested the log file be rolled over");
                }
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory,
                              "Failed to process send to server command", "An exception was thrown that prevents us from completing the command:\r\n{0}", ex.Message);
            }
        }

        /// <summary>
        /// Send the latest summary to the server
        /// </summary>
        public void SendSummary()
        {
            //We don't want to use locks so we need to do a point-in-time copy and then iterate through that copy.
            var client = m_Client;
            if (client != null)
                SendSummary(client);

            LiveSessionPublisher[] registeredClients;
            lock(m_LocalProxyConnections)
            {
                registeredClients = new LiveSessionPublisher[ m_LocalProxyConnections.Count ];
                m_LocalProxyConnections.Values.CopyTo(registeredClients, 0);
            }

            foreach (var liveSessionPublisher in registeredClients)
            {
                SendSummary(liveSessionPublisher);
            }
        }

        /// <summary>
        /// Send the latest summary to the specified publisher
        /// </summary>
        private void SendSummary(LiveSessionPublisher publisher)
        {
            try
            {
                if (publisher.IsConnected)
                {
                    publisher.SendSummary();
                }
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory,
                              "Failed to send summary to the server or other network proxy", "An exception was thrown that prevents us from sending the latest session summary to the server:\r\n" +
                                                                                             "Server or Proxy: {0}\r\n{1} Exception:\r\n{2}", publisher, ex.GetType(), ex.Message);
            }
        }


        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Register the network writer to receive information and get it in sync with the current packet stream
        /// </summary>
        /// <remarks>If the network writer was previously activated then it will be re-activated.</remarks>
        internal void ActivateWriter(NetworkWriter writer, long sequenceOffset = 0)
        {
            //dump the queue to it....
            try
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Verbose, LogCategory, "New remote network viewer connection starting", "We will process the connection attempt and feed it our buffered data.\r\nRemote Endpoint: {0}\r\nSequence Offset: {1:N0}", writer, sequenceOffset);
                lock(m_ActiveClients) //we can't have a gap between starting to dump the buffer and the buffer changing.
                {
                    //write out every header packet to the stream
                    ICachedMessengerPacket[] headerPackets = HeaderPackets;
                    if (headerPackets != null)
                    {
                        writer.Write(headerPackets);
                    }

                    var bufferContents = m_Buffer.ToArray();

                    if ((sequenceOffset > 0) && (bufferContents.Length > 0))
                    {
                        //they have all the packets up through the sequence offset so only later packets
                        if (bufferContents[0].Sequence > sequenceOffset)
                        {
                            //All of our packets qualify because even the first one is after our offset. So we just use bufferContents unmodified.
                        }
                        else if (bufferContents[bufferContents.Length - 1].Sequence <= sequenceOffset)
                        {
                            //*none* of our packets qualify, it's at the end of our buffer, so just clear it.
                            bufferContents = new IMessengerPacket[ 0 ];
                        }
                        else
                        {
                            //figure out exactly where in the buffer we should be.
                            int firstPacketOffset = 0; //we know the zeroth packet should not be included because we checked that above.
                            for (int packetBufferIndex = bufferContents.Length - 2; packetBufferIndex >= 0; packetBufferIndex--) //we iterate backwards because if they have any offset they're likely close to current.
                            {
                                if (bufferContents[packetBufferIndex].Sequence <= sequenceOffset)
                                {
                                    //This is the first packet we should *skip* so the first offset to take is up one.
                                    firstPacketOffset = packetBufferIndex + 1;
                                }
                            }

                            var offsetBuffer = new IMessengerPacket[ bufferContents.Length - firstPacketOffset ]; //inclusive

                            //we've been trying unsuccessfully to isolate why we're getting an exception that the destination array isn't long enough.
                            try
                            {
                                Array.Copy(bufferContents, firstPacketOffset, offsetBuffer, 0, bufferContents.Length - firstPacketOffset);
                                bufferContents = offsetBuffer;
                            }
                            catch (ArgumentException ex)
                            {
                                Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, false, LogCategory,
                                          "Unable to create offset buffer due to " + ex.GetType(),
                                          "Original Buffer Length: {0}\r\nFirst Packet Offset: {1}\r\nOffset Buffer Length: {2}", bufferContents.Length, firstPacketOffset, offsetBuffer.Length);
                            }
                        }
                    }

                    if (bufferContents.Length > 0)
                        writer.Write(bufferContents);

                    //and mark it active if that succeeded
                    if (writer.IsFailed == false)
                    {
                        //note that it may have been previously registered so we need to be cautious about this.
                        if (m_ActiveClients.Contains(writer) == false)
                            m_ActiveClients.Add(writer);
                    }
                    //if it didn't succeed it should raise its failed event, and in turn we will eventually dispose it in due course.
                }
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                    Log.RecordException(0, ex, null, LogCategory, true);
            }
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Inheritors should override this method to implement custom initialize functionality.
        /// </summary>
        /// <remarks>This method will be called exactly once before any call to OnFlush or OnWrite is made.  
        /// Code in this method is protected by a Thread Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnInitialize(IMessengerConfiguration configuration)
        {
            //do our first time initialization
            Caption = "Network Viewer Messenger";
            Description = "Messenger implementation that sends session data live over a TCP connection.";
            OverflowMode = OverflowMode.Drop; //important so we don't block in this mode.

            //try to up cast the configuration to our specific configuration type
            var messengerConfiguration = (NetworkViewerConfiguration)configuration;

            if (messengerConfiguration.AllowLocalClients)
            {
                //set up our local discovery file monitor so we will connect with local proxies.
                m_DiscoveryFileMonitor = new LocalServerDiscoveryFileMonitor();
                m_DiscoveryFileMonitor.FileChanged += DiscoveryFileMonitorOnFileChanged;
                m_DiscoveryFileMonitor.FileDeleted += DiscoveryFileMonitorOnFileDeleted;
                m_DiscoveryFileMonitor.Start();
            }

            if (messengerConfiguration.AllowRemoteClients)
            {
                //we need to monitor & keep a server configuration going.
                var server = Log.Configuration.Server;

                if (server.Enabled)
                {
                    m_HubConnection = new HubConnection(server);
                    m_EnableOutbound = true;

                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Verbose, LogCategory, "Remote live view enabled, will be available once connected to server", "Server Configuration:\r\n{0}", server);
                }
            }

            AutoFlush = true;
            AutoFlushInterval = 5;
        }

        /// <summary>
        /// Inheritors should override this method to implement custom Close functionality
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnClose()
        {
            if (m_IsClosed)
                return;

            if (m_DiscoveryFileMonitor != null)
            {
                try
                {
                    m_DiscoveryFileMonitor.FileChanged -= DiscoveryFileMonitorOnFileChanged;
                    m_DiscoveryFileMonitor.FileDeleted -= DiscoveryFileMonitorOnFileDeleted;
                    m_DiscoveryFileMonitor.Stop();
                }
                catch
                {
                }
            }

            //move everything to the dead collection and then we unregister that guy (this is what the iterators expect)
            lock(m_DeadClients)
            {
                lock(m_ActiveClients)
                {
                    foreach (NetworkWriter activeClient in m_ActiveClients)
                    {
                        if (m_DeadClients.Contains(activeClient) == false)
                            m_DeadClients.Add(activeClient);
                    }
                    m_ActiveClients.Clear();
                    m_Buffer.Clear();
                }

                lock(m_PendingClients)
                {
                    foreach (NetworkWriter pendingClient in m_PendingClients)
                    {
                        if (m_DeadClients.Contains(pendingClient) == false)
                            m_DeadClients.Add(pendingClient);
                    }
                    m_PendingClients.Clear();
                }
            }

            //now we can kill them all
            DropDeadConnections();

            //Now ditch our local proxies.  We've already stopped the file monitor so new ones shouldn't be showing up.
            //Despite that we don't like holding locks if we don't have to.
            List<LiveSessionPublisher> registeredProxies = null;
            lock(m_LocalProxyConnections)
            {
                if (m_LocalProxyConnections.Count > 0)
                {
                    registeredProxies = new List<LiveSessionPublisher>(m_LocalProxyConnections.Values);
                    m_LocalProxyConnections.Clear();
                }
            }

            if (registeredProxies != null)
            {
                foreach (var localProxyConnection in registeredProxies)
                {
                    localProxyConnection.Close();
                }
            }

            if (m_Client != null)
            {
                m_Client.Dispose();
                m_Client = null;
            }

            if (m_HubConnection != null)
            {
                m_HubConnection.Dispose();
                m_HubConnection = null;
            }

            m_IsClosed = true;
        }

        protected override void OnCommand(MessagingCommand command, object state, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested)
        {
            switch (command)
            {
                case MessagingCommand.OpenRemoteViewer:
                    //open an outbound pending connection.
                    var options = (NetworkConnectionOptions)state;
                    StartLiveView(options, Guid.Empty, Guid.NewGuid()); //this is a new channel we're opening.
                    break;
                case MessagingCommand.ExitMode:
                    //close all of our outbound connections.
                    OnClose();
                    break;
            }
        }

        /// <summary>
        /// Inheritors must override this method to implement their custom message writing functionality.
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnWrite(IMessengerPacket packet, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested)
        {
            if (m_IsClosed) //we act like we're closed as soon as we receive exit mode, so we will still get writes after that.
                return;

            if (NetworkWriter.CanWritePacket(packet))
            {
                lock(m_ActiveClients) //between caching and writing to the active clients we need to be consistent.
                {
                    //queue it for later clients
                    CachePacket(packet);

                    //send the packet to all our clients
                    foreach (var activeClient in m_ActiveClients)
                    {
                        try
                        {
                            //if we run into a failed active client it's because it hasn't yet been pruned from the active list, 
                            //so we need to go into maintenance
                            if ((activeClient.IsFailed) || (activeClient.IsClosed))
                            {
                                maintenanceRequested = MaintenanceModeRequest.Regular;
                            }
                            else
                            {
                                activeClient.Write(packet);
                            }
                        }
                        catch (Exception ex)
                        {
                            GC.KeepAlive(ex);
                        }
                    }
                }
            }
        }

        protected override void OnFlush(ref MaintenanceModeRequest maintenanceRequested)
        {
            AttemptRemoteConnectionAsync();

            SendSummary();
        }

        protected override void OnMaintenance()
        {
            DropDeadConnections();

            AttemptRemoteConnectionAsync();
        }

        #endregion

        #region Private Properties and Methods

        private void CachePacket(IMessengerPacket packet)
        {
            // Make sure this is actually a message, not null.
            if (packet == null)
            {
                Log.DebugBreak(); // This shouldn't happen, and we'd like to know if it is, so stop here if debugging.

                return; // Otherwise, just return; we don't want to throw exceptions.
            }

            lock(m_ActiveClients) //we are kept in sync with active client activity.
            {
                if (m_BufferSize > 0)
                    m_Buffer.Enqueue(packet);

                while (m_Buffer.Count > m_BufferSize)
                    m_Buffer.Dequeue(); //discard older excess.
            }
        }

        /// <summary>
        /// Asynchronously verify that we are connected to a remote proxy if we should be.
        /// </summary>
        private void AttemptRemoteConnectionAsync()
        {
            if (m_EnableOutbound == false)
                return;

            //If we already have a thread doing the remote connection attempt, don't start another.
            if (m_ActiveRemoteConnectionAttempt)
                return;

            m_ActiveRemoteConnectionAttempt = true;
            Task.Run(AsyncEnsureRemoteConnection);
        }

        /// <summary>
        /// Make sure we have an outbound proxy connection.
        /// </summary>
        /// <remarks>Intended for asynchronous execution from the thread pool.</remarks>
        private async Task AsyncEnsureRemoteConnection()
        {
            try
            {
                try
                {
                    DateTimeOffset hubConfigurationExpiration;
                    lock(m_HubConnection)
                    {
                        hubConfigurationExpiration = m_HubConfigurationExpiration;
                    }

                    NetworkConnectionOptions newLiveStreamOptions = null;
                    if ((m_HubConnection.IsConnected == false) || (hubConfigurationExpiration < DateTimeOffset.Now))
                    {
                        await m_HubConnection.Reconnect().ConfigureAwait(false);
                        DateTimeOffset connectionAttemptTime = DateTimeOffset.Now;

                        var status = await m_HubConnection.GetStatus().ConfigureAwait(false);

                        if (status.Status == HubStatus.Expired)
                        {
                            //if it's expired we're not going to check again for a while.
                            if (status.Repository?.ExpirationDt == null)
                            {
                                //should never happen, but treat as our long term case.
                                hubConfigurationExpiration = connectionAttemptTime.AddDays(1);
                            }
                            else
                            {
                                TimeSpan expiredTimeframe = connectionAttemptTime - status.Repository.ExpirationDt.Value;
                                if (expiredTimeframe.TotalHours < 24)
                                {
                                    hubConfigurationExpiration = connectionAttemptTime.AddMinutes(15); //we'll check pretty fast for that first day.
                                }
                                else if (expiredTimeframe.TotalDays < 4)
                                {
                                    hubConfigurationExpiration = connectionAttemptTime.AddHours(6);
                                }
                                else
                                {
                                    hubConfigurationExpiration = connectionAttemptTime.AddDays(1);
                                }
                            }

                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Verbose, LogCategory, "Loupe server status is expired so no remote live view possible.", "Will check the server configuration again at {0}", hubConfigurationExpiration);
                        }
                        else
                        {
                            //we always want to periodically recheck the configuration in case it has changed anyway.
                            //we want to coordinate to an exact hour point to provide some consistency to worried users wondering when things will reconnect.
                            hubConfigurationExpiration = connectionAttemptTime.AddMinutes(60 - connectionAttemptTime.Minute); //so we go to a flush hour.
                            newLiveStreamOptions = status.Repository?.AgentLiveStreamOptions;

                            if ((newLiveStreamOptions == null) && (!Log.SilentMode))
                                Log.Write(LogMessageSeverity.Information, LogCategory, "Remote live view not available due to server configuration", "The server is configured to have live view disabled so even though we have it enabled there is no live view.");
                        }

                        lock(m_HubConnection)
                        {
                            m_HubConfigurationExpiration = hubConfigurationExpiration;
                        }

                        //if we got different options then we're going to have to drop & recreate the client.
                        if (newLiveStreamOptions != m_ConnectionOptions)
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Verbose, LogCategory, "Loupe server live view options are different than our running configuration so we will close the client.", "New configuration:\r\n{0}", newLiveStreamOptions);
                            m_ConnectionOptions = newLiveStreamOptions;
                            CloseClient(m_Client);
                            m_Client = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Remote viewer connection attempt failed",
                                  "While attempting to open our outbound connection to the proxy server an exception was thrown.  We will retry again later.\r\nException: {0}", ex.Message);
                }

                try
                {
                    if ((m_Client == null) && (m_ConnectionOptions != null))
                    {
                        LiveSessionPublisher newClient = new LiveSessionPublisher(this, m_ConnectionOptions);
                        newClient.Start();
                        m_Client = newClient;
                        SendSummary(m_Client); //since we just connected, we want to immediately tell it about us.
                    }
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Remote viewer connection attempt failed",
                                  "While attempting to open our outbound connection to the proxy server an exception was thrown.  We will retry again later.\r\nException: {0}", ex.Message);
                }

            }
            finally
            {

                m_ActiveRemoteConnectionAttempt = false;
            }
        }

        /// <summary>
        /// Closes all outbound connections related to the current live agent client
        /// </summary>
        private void CloseClient(LiveSessionPublisher deadClient)
        {
            if (deadClient == null)
                return;

            //we are going to release every connection related to this client.  They might not all be - some might be local.
            var deadClientOptions = deadClient.GetOptions();
            if (deadClientOptions != null)
            {
                lock(m_DeadClients)
                {
                    //move everything to the dead collection and then we unregister that guy (this is what the iterators expect)
                    lock(m_ActiveClients)
                    {
                        foreach (var activeClient in m_ActiveClients)
                        {
                            if ((activeClient.GetOptions().Equals(deadClientOptions))
                                && (m_DeadClients.Contains(activeClient) == false))
                                m_DeadClients.Add(activeClient);
                        }
                    }

                    lock(m_PendingClients)
                    {
                        foreach (var pendingClient in m_PendingClients)
                        {
                            if ((pendingClient.GetOptions().Equals(deadClientOptions))
                                && (m_DeadClients.Contains(pendingClient) == false))
                                m_DeadClients.Add(pendingClient);
                        }
                        m_PendingClients.Clear();
                    }
                }

                //now we can kill them all
                DropDeadConnections();
            }

            deadClient.Dispose();
        }

        /// <summary>
        /// Dispose any connections that we discovered are no longer valid.
        /// </summary>
        private void DropDeadConnections()
        {
            NetworkWriter[] deadClients = null;
            lock(m_DeadClients)
            {
                if (m_DeadClients.Count > 0)
                {
                    deadClients = new NetworkWriter[m_DeadClients.Count];
                    m_DeadClients.CopyTo(deadClients);
                }

                m_DeadClients.Clear();
            }

            //now we start clearing them - outside of the lock since they may check that themselves.
            if (deadClients != null)
            {
                foreach (var networkWriter in deadClients)
                {
                    UnregisterWriter(networkWriter);
                }
            }
        }

        /// <summary>
        /// Register a new writer for all events
        /// </summary>
        /// <param name="newWriter"></param>
        private void RegisterWriter(NetworkWriter newWriter)
        {
            newWriter.Closed += NetworkWriter_Closed;
            newWriter.Connected += NetworkWriter_Connected;
            newWriter.Failed += NetworkWriter_Failed;

            lock (m_PendingClients)
            {
                m_PendingClients.Add(newWriter);
            }
        }

        /// <summary>
        /// Unregister the writer from all events and dispose it
        /// </summary>
        /// <param name="writer"></param>
        private void UnregisterWriter(NetworkWriter writer)
        {
            writer.Closed -= NetworkWriter_Closed;
            writer.Connected -= NetworkWriter_Connected;
            writer.Failed -= NetworkWriter_Failed;

            lock (m_PendingClients)
            {
                m_PendingClients.Remove(writer);
            }

            lock (m_ActiveClients)
            {
                m_ActiveClients.Remove(writer);
            }

            //we are deliberately NOT removing it from the dead connection since that's
            //what we're iterating outside of here...

            writer.Dispose();
        }

        #endregion

        #region Event Handlers

        private void DiscoveryFileMonitorOnFileChanged(object sender, LocalServerDiscoveryFileEventArgs e)
        {
            //this event *should* mean that we have a new proxy to connect to...
            lock (m_LocalProxyConnections)
            {
                if (!m_LocalProxyConnections.TryGetValue(e.File.FileNamePath, out var target))
                {
                    if (e.File.IsAlive)
                    {
                        target = new LiveSessionPublisher(this, e.File);
                        target.Start();
                        m_LocalProxyConnections.Add(e.File.FileNamePath, target);
                    }
                }
            }
        }

        private void DiscoveryFileMonitorOnFileDeleted(object sender, LocalServerDiscoveryFileEventArgs e)
        {
            LiveSessionPublisher victim;

            //this event *should* mean that we have to dump a proxy we were connected to...
            lock (m_LocalProxyConnections)
            {
                if (m_LocalProxyConnections.TryGetValue(e.File.FileNamePath, out victim))
                {
                    if (victim != null)
                    {
                        m_LocalProxyConnections.Remove(e.File.FileNamePath);
                    }
                }
            }

            CloseClient(victim);
        }

        private void NetworkWriter_Closed(object sender, EventArgs e)
        {
            var writer = (NetworkWriter)sender;
            if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, LogCategory, "Remote network viewer connection closed", "Remote Endpoint: {0}", writer);

            //we can't afford to change the active clients collection since that's too pivotal to performance.
            lock (m_DeadClients)
            {
                if (m_DeadClients.Contains(writer) == false)
                {
                    m_DeadClients.Add(writer);
                }
            }
        }

        private void NetworkWriter_Connected(object sender, EventArgs e)
        {
        }

        private void NetworkWriter_Failed(object sender, NetworkEventArgs e)
        {
            NetworkWriter writer = (NetworkWriter)sender;
            if (!Log.SilentMode) Log.Write(LogMessageSeverity.Information, LogCategory, "Remote network viewer connection failed", "We will add it to our dead writers collection and it will get permanently removed in the next maintenance cycle.\r\nRemote Endpoint: {0}", writer);

            lock (m_DeadClients)
            {
                if (m_DeadClients.Contains(writer) == false)
                {
                    m_DeadClients.Add(writer);
                }                
            }
        }

        #endregion
    }
}
