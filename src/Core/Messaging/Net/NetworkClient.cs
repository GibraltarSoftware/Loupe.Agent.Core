using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Gibraltar.Monitor;
using Gibraltar.Serialization;
using Gibraltar.Server.Client;
using Loupe.Extensibility.Data;

namespace Gibraltar.Messaging.Net
{
    /// <summary>
    /// The base class for different network clients that talk to a server.
    /// </summary>
    [DebuggerDisplay("Endpoint: {Options} Connected: {IsConnected}")]
    public abstract class NetworkClient : IDisposable
    {
        /// <summary>
        /// The log category used for network client operations
        /// </summary>
        public const string LogCategory = "Loupe.Network.Client";
        private const int NetworkReadBufferLength = 10240;

        private readonly NetworkConnectionOptions m_Options;
        private bool m_RetryConnections;
        private readonly int m_MajorVersion;
        private readonly int m_MinorVersion;
        private readonly object m_Lock = new object();

        private Task m_BackgroundReader;
        private bool m_SingleSocket;
        private bool m_Connected; //PROTECTED BY LOCK
        private bool m_ConnectionFailed; //PROTECTED BY LOCK
        private volatile bool m_Closed;  //PROTECTED BY LOCK //Volatile so we can peek at it.
        private bool m_HasCorruptData;
        private int m_PacketsLostCount;
        private string m_StatusString;

        //these serializers are conversation-specific and have to be replaced every time we connect.
        private NetworkSerializer m_NetworkSerializer;
        private Stream m_NetworkStream;
        private PipeStream m_PacketStream;
        private PacketReader m_PacketReader;
        private PacketWriter m_PacketWriter;
        private TcpClient m_TcpClient; //only used when we were given a specific TCP client, not when we connect ourselves.

        /// <summary>
        /// Event raised whenever the client establishes a connection to a server
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// Event raised whenever the client looses contact with the server
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Raised when a connection closes normally
        /// </summary>
        public event EventHandler Closed;

        /// <summary>
        /// Raised when a connection that did open fails
        /// </summary>
        public event NetworkEventHandler Failed;

        /// <summary>
        /// Create a new network client to the specified endpoint
        /// </summary>
        /// <param name="options"></param>
        /// <param name="retryConnections">If true then network connections will automatically be retried (instead of the client being considered failed)</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        protected NetworkClient(NetworkConnectionOptions options, bool retryConnections, int majorVersion, int minorVersion)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            lock(m_Lock) //since we promptly access these variables from another thread, I'm adding this as paranoia to ensure they get synchronized.
            {
                m_Options = options;
                m_RetryConnections = retryConnections;
                m_MajorVersion = majorVersion;
                m_MinorVersion = minorVersion;
            }

            CalculateStateMessage(null); //initializes to default
        }

        /// <summary>
        /// Create a new network client using the existing socket.
        /// </summary>
        /// <param name="tcpClient">Already connected TCP Socket</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        protected NetworkClient(TcpClient tcpClient, int majorVersion, int minorVersion)
        {
            lock(m_Lock) //since we promptly access these variables from another thread, I'm adding this as paranoia to ensure they get synchronized.
            {
                m_SingleSocket = true;
                m_TcpClient = tcpClient;
                m_RetryConnections = false; //when we're given the client, we can't retry connections.
                m_MajorVersion = majorVersion;
                m_MinorVersion = minorVersion;
            }

            CalculateStateMessage(null); //initializes to default
        }

        /// <summary>
        /// Start the network client
        /// </summary>
        public void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Stop reading from the network and prepare to exit
        /// </summary>
        public void Close()
        {
            try
            {
                ActionOnClosed();
            }
            catch (Exception ex)
            {
                Log.RecordException(0, ex, null, LogCategory, true);
            }
        }

        /// <summary>
        /// Indicates if the remote viewer is currently connected.
        /// </summary>
        public bool IsConnected { get { return m_Connected; } }

        /// <summary>
        /// Indicates if the writer experienced a network failure
        /// </summary>
        public bool IsFailed { get { return m_ConnectionFailed; } }

        /// <summary>
        /// Indicates if the writer was explicitly closed.
        /// </summary>
        public bool IsClosed { get { return m_Closed; } }

        /// <summary>
        /// Indicates whether a session had errors during rehydration and has lost some packets.
        /// </summary>
        public bool HasCorruptData { get { return m_HasCorruptData; } }

        /// <summary>
        /// Indicates how many packets were lost due to errors in rehydration.
        /// </summary>
        public int PacketsLostCount { get { return m_PacketsLostCount; } }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Get a copy of the network connection options used by this client
        /// </summary>
        /// <returns></returns>
        public NetworkConnectionOptions GetOptions()
        {
            return m_Options.Clone();
        }


        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return m_StatusString;
        }

        #region Protected Properties and Methods

        /// <summary>
        /// Dispose managed objects
        /// </summary>
        /// <param name="releaseManaged"></param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (releaseManaged)
            {
                Close();

                DisposeMembers();
            }
        }

        /// <summary>
        /// Implemented to complete the protocol connection
        /// </summary>
        /// <returns>True if a connection was successfully established, false otherwise.</returns>
        protected abstract bool Connect();

        /// <summary>
        /// Implemented to transfer data on an established connection
        /// </summary>
        protected abstract void TransferData();

        /// <summary>
        /// Called when a valid connection is being administratively closed
        /// </summary>
        protected virtual void OnClose()
        {
        }

        /// <summary>
        /// Called to raise the connected event when a connection has been established
        /// </summary>
        protected virtual void OnConnected()
        {
            EventHandler tempEvent = Connected;

            if (tempEvent != null)
            {
                try
                {
                    tempEvent.Invoke(this, new EventArgs());
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Client exception thrown during connected event", ex.Message);
                }
            }
        }

        /// <summary>
        /// Called to raise the disconnected event when a connection has been lost
        /// </summary>
        protected virtual void OnDisconnected()
        {
            EventHandler tempEvent = Disconnected;

            if (tempEvent != null)
            {
                try
                {
                    tempEvent.Invoke(this, new EventArgs());
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Client exception thrown during disconnected event", ex.Message);
                }
            }
        }

        /// <summary>
        /// Raise the closed event
        /// </summary>
        protected virtual void OnClosed()
        {
            EventHandler tempEvent = Closed;

            if (tempEvent != null)
            {
                try
                {
                    tempEvent(this, new EventArgs());
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Client exception thrown during closed event", ex.Message);
                }
            }
        }

        /// <summary>
        /// Raise the failed event
        /// </summary>
        protected virtual void OnFailed(string message)
        {
            NetworkEventHandler tempEvent = Failed;

            if (tempEvent != null)
            {
                try
                {
                    tempEvent(this, new NetworkEventArgs(message));
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Client exception thrown during failed event", ex.Message);
                }
            }
        }

        /// <summary>
        /// Allows derived classes to register all of the packet factories they need when creating a new packet reader.
        /// </summary>
        /// <param name="packetReader"></param>
        protected virtual void OnPacketFactoryRegister(PacketReader packetReader)
        {
        }

        /// <summary>
        /// The network connection options used to connect to the server.
        /// </summary>
        protected NetworkConnectionOptions Options { get { return m_Options; } }

        /// <summary>
        /// Transmit the provided network message to the server.
        /// </summary>
        /// <param name="message"></param>
        protected void SendMessage(NetworkMessage message)
        {
            lock (m_Lock)
            {
                bool commandSent = false;
                Exception sendException = null;
                if (m_NetworkStream != null)
                {
                    try
                    {
                        message.Write(m_NetworkStream);
                        commandSent = true;
                    }
                    catch (Exception ex)
                    {
                        sendException = ex;
                    }
                }

                if (commandSent == false)
                {
                    throw new GibraltarNetworkException("Unable to send message, usually because the network connection was lost.", sendException);
                }
            }
        }


#if GUARDBYTESONLY
        private long m_BytesSent;
        private long m_BytesReceived;
        private readonly Random m_Rnd = new Random();
        private readonly byte[] m_GuardPattern = new byte[] {0x00, 0x0F, 0xF0, 0xFF};

        /// <summary>
        /// Used when built in guard byte mode to hold the pattern of bytes we end across the wire.
        /// </summary>
        internal byte[] GuardPattern { get { return m_GuardPattern; } }
#endif

        /// <summary>
        /// Transmit the provided serialized packet to the server.
        /// </summary>
        /// <param name="packet"></param>
        protected void SendPacket(IPacket packet)
        {
            lock (m_Lock)
            {
                bool packetSent = false;
                Exception sendException = null;
                if (m_NetworkStream != null)
                {
                    //make sure we have a packet serializer, if not hopefully this is the first time :)
                    if (m_PacketWriter == null)
                    {
                        m_PacketWriter = new PacketWriter(m_NetworkStream, m_MajorVersion, m_MinorVersion);
                    }

                    //since the packet writer goes directly to the network stream we have to do our full network exception handling.
                    try
                    {
#if GUARDBYTESONLY //used for testing
                        int bytesToSend = m_Rnd.Next(1, 4096);

                        for (int bytesSent = 0; bytesSent < bytesToSend; bytesSent++ )
                        {
                            m_NetworkStream.WriteByte(m_GuardPattern[m_BytesSent % 4]);
                            m_BytesSent++;
                        }
#else
                        m_PacketWriter.Write(packet);
#endif
                        packetSent = true;
                    }
                    catch (SocketException ex)
                    {
                        //most likely the socket is no good any more.
                        ActionSocketFailed(ex); //throws an exception
                    }
                    catch (IOException ex)
                    {
                        //the doc indicates you'll often get an IO exception that WRAPS a socket exception.
                        if ((ex.InnerException != null) && (ex.InnerException is SocketException))
                        {
                            //most likely the socket is no good any more.
                            ActionSocketFailed(ex); //throws an exception
                        }
                        else
                        {
                            sendException = ex;
                        }
                    }
                    catch (Exception ex)
                    {
                        sendException = ex;
                    }
                }

                if (packetSent == false)
                {
                    throw new GibraltarNetworkException("Unable to send packet, usually because the network connection was lost.", sendException);
                }
            }
        }
        /// <summary>
        /// Read the next network packet from the pipe.  Blocks until a packet is detected or the connection fails.
        /// </summary>
        /// <returns></returns>
        protected NetworkMessage ReadNetworkMessage()
        {
            if (m_NetworkSerializer == null)
                m_NetworkSerializer = new NetworkSerializer();

            var socketClosed = false;
            NetworkMessage nextPacket;
           
            do
            {
                //see if we have another packet in the buffer...
                nextPacket = m_NetworkSerializer.ReadNext();

                if (nextPacket == null)
                {
                    //go into a blocking wait on the socket..  we'll loop until we get the whole buffer into the stream.
                    var newDataLength = ReadSocket(out var buffer);

                    if (newDataLength == 0)
                    {
                        //this is the signal that the other end shut down the pipe.
                        socketClosed = true;
                    }
                    else
                    {
                        m_NetworkSerializer.AppendData(buffer, newDataLength);
                    }
                }
            } while ((socketClosed == false) && (nextPacket == null));

            return nextPacket;            
        }

        /// <summary>
        /// Read the next serialized packet from the pipe.  Blocks until a packet is detected or the connection fails.
        /// </summary>
        /// <returns>Null if the connection fails or an IPacket that is the next available packet</returns>
        /// <remarks>This will automatically handle transitions from the network reader to the packet reader, but you can't transition the other way.</remarks>
        protected IPacket ReadSerializedPacket()
        {
            if (m_PacketReader == null)
            {
                //we haven't set up our packet reader - we're switching to serialized packet reading, so set everything up.
                m_PacketStream = new PipeStream(NetworkReadBufferLength);
                m_PacketReader = new PacketReader(m_PacketStream, false, m_MajorVersion, m_MinorVersion); //we keep writing to that memory stream, so its length will constantly change

                //register our packet factories we need
                OnPacketFactoryRegister(m_PacketReader);

                //before we jump in we need to transfer any buffeNetworkClientr remnant from the network serializer that
                //represents data the writer has already put into the pipeline
                if (m_NetworkSerializer != null)
                {
                    byte[] bufferTail = m_NetworkSerializer.UnusedData;

                    if ((bufferTail != null) && (bufferTail.Length > 0))
                    {
                        AppendToStream(m_PacketStream, bufferTail, bufferTail.Length);
                    }
                }
            }

            bool socketClosed = false;
            IPacket nextPacket = null;

#if GUARDBYTESONLY
            do
            {
                //instead of the packet reader we have to just compare against guardbytes.
                while (m_PacketStream.Position < m_PacketStream.Length)
                {
                    byte providedByte = (byte)m_PacketStream.ReadByte();
                    byte guardByte = m_GuardPattern[m_BytesReceived % 4];
                    if (providedByte != guardByte)
                    {
                        Console.WriteLine("LiveStream: Read next byte as {0:x} but expected {1:x}", providedByte, guardByte);
                        Debugger.Break();
                    }
                    m_BytesReceived++;
                }

                //go into a blocking wait on the socket..  we'll loop until we get the whole buffer into the stream.
                byte[] buffer;
                int newDataLength = ReadSocket(out buffer);

                if (newDataLength == 0)
                {
                    //this is the signal that the other end shut down the pipe.
                    socketClosed = true;
                }
                else
                {
                    AppendToStream(m_PacketStream, buffer, newDataLength);
                }
            } while ((socketClosed == false) && (nextPacket == null));

#else
            do
            {

                //see if we have another packet in the buffer...
                while ((m_PacketReader.DataAvailable) && (nextPacket == null)) //this has us loop over all bad packets in the buffer...
                {
                    try
                    {
                        nextPacket = m_PacketReader.Read();
                    }
                    catch (Exception ex)
                    {
                        m_HasCorruptData = true;
                        m_PacketsLostCount++;

                        GibraltarSerializationException serializationException = ex as GibraltarSerializationException;
                        if ((serializationException != null) //and really this should always be the case...
                            && (serializationException.StreamFailed))
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Exception during packet stream read, unable to continue deserializing data",
                                          "While attempting to deserialize a packet an exception was reported. This has failed the stream so serialization will stop.\r\nException: {0}", ex.Message);

                            throw; //rethrow because this is terminal
                        }
                        else
                        {
                            if (!Log.SilentMode) Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Exception during packet read, discarding packet and continuing",
                                      "While attempting to deserialize a packet an exception was reported.  Since this may be a problem with just this one packet, we'll continue deserializing.\r\nException: {0}", ex.Message);
                        }
                    }
                }

                if (nextPacket == null) //at this point we know we've exhausted the read buffer and haven't found a valid packet.
                {
                    //go into a blocking wait on the socket..  we'll loop until we get the whole buffer into the stream.
                    int newDataLength = ReadSocket(out var buffer);

                    if (newDataLength == 0)
                    {
                        //this is the signal that the other end shut down the pipe.
                        socketClosed = true;
                    }
                    else
                    {
                        AppendToStream(m_PacketStream, buffer, newDataLength);
                    }
                }
            } while ((socketClosed == false) && (nextPacket == null));

            m_PacketStream.Trim(); //we need to periodically let it release memory.
#endif
            return nextPacket;
        }

        /// <summary>
        /// Called to shut down the client due to a command from the server.
        /// </summary>
        protected void RemoteClose()
        {
            lock (m_Lock)
            {
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Information, LogCategory, "Live view session ending at remote request", "We have received an indication from the other end of the connection that it is time to end the live view session.");
                Close();
            }
        }

        /// <summary>
        /// Allows a derived class to implement its own retry delay strategy
        /// </summary>
        /// <param name="defaultDelayMs">The number of Milliseconds to wait before retrying</param>
        /// <returns>true if any retry should be attempted</returns>
        protected virtual bool RetryDelay(ref int defaultDelayMs)
        {
            return true;
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// The main method of the background thread for reading from the stream.
        /// </summary>
        private async void AsyncNetworkStreamMain()
        {
            try
            {
                int previousFailures = 0;
                while (!m_Closed)
                {
                    try
                    {
                        ActionOnDisconnected(); //this is our initial state

                        //Create a new TCP Client.  If we're in single socket mode & this is the second time, it'll fail.
                        using (var tcpClient = await GetTcpClient())
                        {
                            m_TcpClient = tcpClient;

                            CalculateStateMessage(tcpClient);
                            Stream tcpStream;
                            var rawStream = tcpClient.GetStream();
                            if ((m_Options != null) && (m_Options.UseSsl))
                            {
                                try
                                {
                                    var sslStream = new SslStream(rawStream, false);
                                    await sslStream.AuthenticateAsClientAsync(m_Options.HostName);
                                    tcpStream = sslStream;
                                }
                                catch (AuthenticationException)
                                {
                                    //SSL exceptions are completely fatal because they indicate a configuration problem of some type.
                                    Close();
                                    throw;
                                }
                            }
                            else
                            {
                                tcpStream = rawStream;
                            }

                            //Here we should authenticate when we add support for that to Loupe.
                            //TODO BUG:  Add Authentication

                            lock (m_Lock)
                            {
                                m_NetworkStream = tcpStream;
                            }

                            var connected = Connect();
                            CalculateStateMessage(tcpClient);

                            if (connected)
                            {
                                //since we've successfully connected reset the previous failure count.
                                previousFailures = 0;

                                //let our monitors know we're connected
                                ActionOnConnected();

                                //and now we're ready to pass control to our downstream objects.
                                TransferData();
                            }
                        }
                    }
                    catch (GibraltarNetworkException ex)
                    {
                        //in this case we've already handled the critical state issues, we just need to exit the loop and roll around again.
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Verbose, LogWriteMode.Queued, ex, LogCategory, "Recovering from network failure", "The TCP Socket has been disposed and now we will prepare to reconnect.\r\nPrevious Failures: {0:N0}\r\nConnection Info: {1}", previousFailures, m_Options);
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Unexpected exception while working with network stream", "We will clear the current state and attempt to re-establish (if allowed)\r\n{0}\r\nException: {1}", this, ex);
                    }
                    finally
                    {
                        CalculateStateMessage(null);

                        if (m_RetryConnections == false)
                        {
                            //this is going to be fatal, so kill it.
                            Close();
                        }

                        DisposeMembers();
                    }

                    //now we need to stall for a moment while connecting
                    if (!m_Closed)
                    {
                        int retryDelayMs = 0;
                        if (previousFailures == 0)
                        {
                            //we always want to retry immediately on the first failure.                            
                        }
                        else if (previousFailures < 5)
                        {
                            //first five failures just wait half a second.
                            retryDelayMs = 500;
                        }
                        else if (previousFailures < 13) //8 + the previous 5
                        {
                            retryDelayMs = 15000;
                        }
                        else
                        {
                            retryDelayMs = 60000;
                        }

                        //allow our derived class to have an alternate health check
                        bool canRetry = RetryDelay(ref retryDelayMs );

                        if (canRetry == false)
                            Close();

                        else
                        {
                            previousFailures++;
                            Thread.Sleep(retryDelayMs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Background network reader thread failed", "The thread is exiting due to an exception and the client will effectively be dead.\r\nException: {0}", ex.Message);
                ActionOnDisconnected();
            }
            finally
            {
                CalculateStateMessage(null);
                //in this position we're exiting the thread, so we need to release our pointer to it too.
                m_BackgroundReader = null;
            }
        }


        /// <summary>
        /// Release and dispose all of the connection-specific resources
        /// </summary>
        private void DisposeMembers()
        {
            lock (m_Lock)
            {
                SafeDispose(m_PacketWriter);
                m_PacketWriter = null;

                SafeDispose(m_NetworkStream);
                m_NetworkStream = null;

                SafeDispose(m_NetworkSerializer);
                m_NetworkSerializer = null;

                SafeDispose(m_PacketReader);
                m_PacketReader = null;

                SafeDispose(m_PacketStream);
                m_PacketStream = null;
            }            
        }

        private static void SafeDispose(IDisposable disposableObject)
        {
            if (disposableObject != null)
            {
                try
                {
                    disposableObject.Dispose();
                }
                catch (Exception ex) //this is what makes it safe...
                {
                    Log.RecordException(0, ex, null, LogCategory, true);
                }
            }
        }

        private void Initialize()
        {
            lock(m_Lock)
            {
                if (m_BackgroundReader != null)
                    return;

                //and start our reader thread.
                m_BackgroundReader = new Task(AsyncNetworkStreamMain, TaskCreationOptions.LongRunning);
                m_BackgroundReader.Start();
            }
        }

        /// <summary>
        /// Appends to our packet stream the provided buffer of data, preserving stream position
        /// </summary>
        /// <param name="seeekableStream"></param>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        private void AppendToStream(Stream seeekableStream, byte[] buffer, int length)
        {
            if (length <= 0)
                return;

            long originalPosition = seeekableStream.Position;
            seeekableStream.Position = seeekableStream.Length;
            seeekableStream.Write(buffer, 0, length);
            seeekableStream.Position = originalPosition;
        }

        /// <summary>
        /// Handles terminal socket failures
        /// </summary>
        /// <param name="ex"></param>
        private void ActionSocketFailed(Exception ex)
        {
            lock(m_Lock)
            {
                if (m_Connected)
                {
                    //since we were connected we're transitioning to failed.
                    if (!Log.SilentMode)
                    {
                        Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, LogCategory, "Network Socket Failed", "We received an exception from the socket when performing an operation and will assume it has failed.\r\n{0}\r\nException: {1}", m_StatusString, ex.Message );
                    }

                    ActionOnDisconnected();

                    if (m_RetryConnections == false)
                    {
                        ActionOnFailed(ex.Message);
                    }
                }
            }

            //we always throw the exception to force the call stack to unwind.
            throw new GibraltarNetworkException("The network socket has failed", ex);
        }

        /// <summary>
        /// sets our status to connected and fires the appropriate event.
        /// </summary>
        /// <remarks>This is separate from the overrideable OnConnected to ensure our critical state management gets done even if inheritor messes up.</remarks>
        private void ActionOnConnected()
        {
            bool raiseEvent = false;
            lock (m_Lock)
            {
                if (m_Connected == false)
                {
                    m_Connected = true;
                    raiseEvent = true;
                }
            }
            
            if (raiseEvent)
                OnConnected();
        }

        /// <summary>
        /// sets our status to disconnected and fires the appropriate event.
        /// </summary>
        /// <remarks>This is separate from the overrideable OnDisonnected to ensure our critical state management gets done even if inheritor messes up.</remarks>
        private void ActionOnDisconnected()
        {
            bool raiseEvent = false;
            lock (m_Lock)
            {
                if (m_Connected)
                {
                    m_Connected = false;
                    raiseEvent = true;
                }
            }

            if (raiseEvent)
                OnDisconnected();
        }

        /// <summary>
        /// sets our status to disconnected and fires the appropriate event.
        /// </summary>
        /// <remarks>This is separate from the overrideable OnDisconnected to ensure our critical state management gets done even if inheritor messes up.</remarks>
        private void ActionOnClosed()
        {
            bool performClose = false;

            try
            {
                Task backgroundTask = null;
                Stream writerStream = null;

                //we can get into a deadlock here: we have to peek if another thread is already closing
                if (m_Closed == false)
                {
                    lock(m_Lock)
                    {
                        if (m_Closed == false)
                        {
                            performClose = true;
                            m_Closed = true; //so any other thread will know we're closed.
                            writerStream = m_NetworkStream;
                            backgroundTask = m_BackgroundReader;
                        }
                    }
                }

                if (performClose == false)
                    return; //we are already closed, forget about it!

                if (writerStream != null)
                {
                    OnClose();

                    m_RetryConnections = false; //even if it was true, we don't want to do it now..

                    if ((m_TcpClient != null) && (m_TcpClient.Client != null))
                    {
                        m_TcpClient.Client.Shutdown(SocketShutdown.Both);
                        m_TcpClient.Dispose();
                    }
                }

                if (Task.CurrentId.HasValue == false || Task.CurrentId != backgroundTask.Id)
                {
                    //since we aren't on the background thread we want to stall and see if it exits, otherwise we'll have to beat it about the head.

                    //the background thread could be stuck on a long operation...
                    DateTimeOffset waitTimeout = DateTimeOffset.Now.AddSeconds(5);
                    while (backgroundTask != null && (backgroundTask.IsCompleted == false))
                    {
                        //stall for a moment to let it close out right...
                        if (DateTimeOffset.Now < waitTimeout)
                        {
                            Thread.Sleep(16);
                        }

                        lock (m_Lock)
                        {
                            backgroundTask = m_BackgroundReader;
                        }
                    }
                }
            }
            finally
            {
                //lets force these events to be in the right order.
                ActionOnDisconnected();
                OnClosed();
            }
        }

        /// <summary>
        /// sets our status to disconnected and fires the appropriate event.
        /// </summary>
        /// <remarks>This is separate from the overrideable OnDisonnected to ensure our critical state management gets done even if inheritor messes up.</remarks>
        private void ActionOnFailed(string message)
        {
            bool raiseEvent = false;
            lock (m_Lock)
            {
                if (m_ConnectionFailed == false)
                {
                    m_ConnectionFailed = true;
                    raiseEvent = true;
                }
            }

            if (raiseEvent)
                OnFailed(message);
        }

        private void CalculateStateMessage(TcpClient client)
        {
            string state = null;

            //ideally we want to know where we connected exactly
            if (client != null)
            {
                try
                {
                    if (client.Connected)
                    {
                        Socket clientSocket = client.Client;
                        IPEndPoint localEndPoint = (IPEndPoint)clientSocket.LocalEndPoint;
                        IPEndPoint remoteEndPoint = (IPEndPoint)clientSocket.RemoteEndPoint;
                        state = string.Format("{4}Network Client from {0}:{1} to {2}:{3}", localEndPoint.Address, localEndPoint.Port, remoteEndPoint.Address, remoteEndPoint.Port,
                                              m_Options.UseSsl ? "Encrypted " : string.Empty);
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrEmpty(state))
            {
                if (m_Options != null)
                {
                    state = string.Format("{2}Network Client to {0}:{1} (Not connected)", m_Options.HostName, m_Options.Port,
                                             m_Options.UseSsl ? "Encrypted " : string.Empty);
                }
                else
                {
                    state = "Network Client (Not connected)";
                }
            }

            m_StatusString = state;
        }

        /// <summary>
        /// Get a new TCP Client,if possible.
        /// </summary>
        /// <returns></returns>
        private async Task<TcpClient> GetTcpClient()
        {
            TcpClient newClient = null;
            Exception innerException = null;
            if (m_SingleSocket)
            {
                newClient = m_TcpClient;
                m_TcpClient = null; // it can only be used once
            }
            else
            {
                try
                {
                    var potentialClient = new TcpClient();
                    await potentialClient.ConnectAsync(m_Options.HostName, m_Options.Port);
                    if (potentialClient.Connected)
                        newClient = potentialClient;
                }
                catch (SocketException ex)
                {
                    innerException = ex;
                }
                catch (IOException ex)
                {
                    //the doc indicates you'll often get an IO exception that WRAPS a socket exception.
                    if ((ex.InnerException != null) && (ex.InnerException is SocketException))
                    {
                        innerException = ex;
                    }
                    else
                    {
                        throw; // in this case we're going to let the RAW exception get sent out instead of a network exception
                    }
                }
            }

            if (newClient == null)
            {
                throw new GibraltarNetworkException("There is no connection available", innerException);
            }

            return newClient;
        }

        /// <summary>
        /// Performs a blocking read on the network stream.  If the socket is closed, returns zero.  Other failures will throw exceptions.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private int ReadSocket(out byte[] buffer)
        {
            //go into a blocking wait on the socket..  we'll loop until we get the whole buffer into the stream.
            buffer = new byte[NetworkReadBufferLength];
            int newDataLength = 0;
            try
            {
                if (m_NetworkStream.CanRead)
                {
                    newDataLength = m_NetworkStream.Read(buffer, 0, buffer.Length);
                }
            }
            catch (SocketException ex)
            {
                //most likely the socket is no good any more.
                ActionSocketFailed(ex); //throws an exception
            }
            catch (IOException ex)
            {
                //the doc indicates you'll often get an IO exception that WRAPS a socket exception.
                if ((ex.InnerException != null) && (ex.InnerException is SocketException))
                {
                    //most likely the socket is no good any more.
                    ActionSocketFailed(ex); //throws an exception
                }
                else
                {
                    throw;
                }
            }

            return newDataLength;
        }

        #endregion
    }
}
