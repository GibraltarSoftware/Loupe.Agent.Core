using System;
using System.Diagnostics;
using System.IO;
using Gibraltar.Data;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;


namespace Gibraltar.Messaging.Net
{
    /// <summary>
    /// A packet of data that can be serialized across the network
    /// </summary>
    public abstract class NetworkMessage
    {
        private const int BasePacketLength = 16; //our fixed size when serialized

        /// <summary>
        /// The maximum length of any network message, anything longer is invalid.
        /// </summary>
        /// <remarks>The only packet of any real size is a SessionHeader message.
        /// They're generally around 3KB plus name/value pairs but lets assume no one goes
        /// crazy so the biggest is 1MB.</remarks>
        public const int MaxMessageLength = 1024 * 1024;

        private readonly object m_Lock = new object();

        private Version m_Version;
        private NetworkMessageTypeCode m_TypeCode;
        private int m_Length;

        /// <summary>
        /// The protocol version
        /// </summary>
        public Version Version
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Version;
                }
            }
            set
            {
                lock (m_Lock)
                {
                    m_Version = value;
                }
            }
        }

        /// <summary>
        /// The specific packet type code
        /// </summary>
        public NetworkMessageTypeCode TypeCode
        {
            get
            {
                lock (m_Lock)
                {
                    return m_TypeCode;
                }
            }
            set
            {
                lock (m_Lock)
                {
                    m_TypeCode = value;
                }
            }
        }

        /// <summary>
        /// The number of bytes for the packet.
        /// </summary>
        public int Length
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_Length == 0)
                    {
                        //we haven't calculated it yet... we have to serialize to find out.
                        Stream test = new MemoryStream();
                        Write(test);
                        return (int)test.Length;
                    }

                    return m_Length;
                }
            }
        }

        /// <summary>
        /// Peek at the byte data and see if there's a full packet header
        /// </summary>
        /// <param name="stream">The raw data</param>
        /// <param name="packetLength">The length of the packet</param>
        /// <param name="typeCode">The type of the packet</param>
        /// <param name="version">The schema of the packet</param>
        /// <returns>True if the header could be read, false if there wasn't enough data</returns>
        public static bool ReadHeader(Stream stream, out int packetLength, out NetworkMessageTypeCode typeCode, out Version version)
        {
            if ((stream.Length - stream.Position) >= BasePacketLength)
            {
                long originalPosition = stream.Position;
                ReadPacket(stream, out typeCode, out version, out packetLength);
                stream.Position = originalPosition;
                return true;
            }
            else
            {
                packetLength = 0;
                typeCode = 0;
                version = null;
                return false;
            }
        }

        /// <summary>
        /// Read the provided stream to create the packet
        /// </summary>
        /// <returns>The length of the packet, which may be less or greater than the buffer</returns>
        public static NetworkMessage Read(Stream stream)
        {
            NetworkMessage newPacket;

            var initialStreamPosition = stream.Position;

            //read out the header, this gives us what we need to know what type of packet to make.
            ReadPacket(stream, out var typeCode, out var version, out var packetLength);
            switch (typeCode)
            {
                case NetworkMessageTypeCode.LiveViewStartCommand:
                    newPacket = new LiveViewStartCommandMessage();
                    break;
                case NetworkMessageTypeCode.LiveViewStopCommand:
                    newPacket = new LiveViewStopCommandMessage();
                    break;
                case NetworkMessageTypeCode.SendSession:
                    newPacket = new SendSessionCommandMessage();
                    break;
                case NetworkMessageTypeCode.GetSessionHeaders:
                    newPacket = new GetSessionHeadersCommandMessage();
                    break;
                case NetworkMessageTypeCode.RegisterAgentCommand:
                    newPacket = new RegisterAgentCommandMessage();
                    break;
                case NetworkMessageTypeCode.RegisterAnalystCommand:
                    newPacket = new RegisterAnalystCommandMessage();
                    break;
                case NetworkMessageTypeCode.SessionClosed:
                    newPacket = new SessionClosedMessage();
                    break;
                case NetworkMessageTypeCode.SessionHeader:
                    newPacket = new SessionHeaderMessage();
                    break;
                case NetworkMessageTypeCode.PacketStreamStartCommand:
                    newPacket = new PacketStreamStartCommandMessage();
                    break;
                case NetworkMessageTypeCode.ClockDrift:
                    newPacket = new ClockDriftMessage();
                    break;
                case NetworkMessageTypeCode.PauseSessionHeaders:
                    newPacket = new PauseSessionHeadersCommandMessage();
                    break;
                case NetworkMessageTypeCode.ResumeSessionHeaders:
                    newPacket = new ResumeSessionHeadersCommandMessage();
                    break;
                default:
                    throw new InvalidOperationException("Unable to create network packet because it uses a type code that is unknown: " + typeCode);
            }

            newPacket.m_Length = packetLength;
            newPacket.m_TypeCode = typeCode;
            newPacket.m_Version = version;

            newPacket.OnRead(stream);

            //force the stream to be at the end of our length in case a derived class messed up.
            if (stream.Position - initialStreamPosition != packetLength)
            {
                Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, null, 0, NetworkClient.LogCategory,
                    "Network stream is not in correct position after packet read",
                    "After reading a packet {0:N0} long from a stream starting at position {1:N0} the stream is at position {2:N0}.  This stream will likely fail on a subsequent read.",
                    packetLength, initialStreamPosition, stream.Position);

#if DEBUG && DEBUG_LIVE
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif
                stream.Position = initialStreamPosition + packetLength;
            }

            return newPacket;
        }

        /// <summary>
        /// Write the packet to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        public void Write(Stream stream)
        {
            //we can't depend on the stream being seekable so we have to stage our serialization.
            lock (m_Lock)
            {
                //serialize our derived packet so we can know our length below
                using (var ourPacketStream = new MemoryStream())
                {
                    OnWrite(ourPacketStream);
                    ourPacketStream.Position = 0;

                    BinarySerializer.SerializeValue(stream, (int)m_TypeCode);
                    BinarySerializer.SerializeValue(stream, m_Version.Major);
                    BinarySerializer.SerializeValue(stream, m_Version.Minor);
                    BinarySerializer.SerializeValue(stream, (int)(ourPacketStream.Length + BasePacketLength));
                    ourPacketStream.WriteTo(stream);
                }
            }
        }

        #region Protected Properties and Methods

        /// <summary>
        /// Write the packet to the stream
        /// </summary>
        protected abstract void OnWrite(Stream stream);

        /// <summary>
        /// Read packet data from the stream
        /// </summary>
        protected abstract void OnRead(Stream stream);

        /// <summary>
        /// Inheritors must implement this to reflect their packet length as they read a packet plus the base length that came before.
        /// </summary>
        /// <remarks>At any time the remaining length is the Length property minus the BaseLength property.</remarks>
        protected virtual int BaseLength
        {
            get
            {
                return BasePacketLength;
            }
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Read just this level from the stream.
        /// </summary>
        private static void ReadPacket(Stream stream, out NetworkMessageTypeCode typeCode, out Version version, out int length)
        {
            BinarySerializer.DeserializeValue(stream, out int rawTypeCode);
            typeCode = (NetworkMessageTypeCode)rawTypeCode;

            //we serialize version as major/minor
            BinarySerializer.DeserializeValue(stream, out int majorVer);
            BinarySerializer.DeserializeValue(stream, out int minorVer);
            version = new Version(majorVer, minorVer);

            BinarySerializer.DeserializeValue(stream, out length);
#if DEBUG
            if (length > MaxMessageLength && Debugger.IsAttached)
                Debugger.Break();
#endif
        }

        #endregion
    }
}
