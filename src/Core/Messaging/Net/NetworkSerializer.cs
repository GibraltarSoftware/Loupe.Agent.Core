using System;
using System.Diagnostics;
using System.IO;
using Gibraltar.Monitor;
using Gibraltar.Server.Client;
using Loupe.Extensibility.Data;

namespace Gibraltar.Messaging.Net
{
    /// <summary>
    /// Used to serialize network packets across a TCP socket
    /// </summary>
    public class NetworkSerializer : IDisposable
    {
        private const string LogCategory = NetworkClient.LogCategory;
        private readonly object m_Lock = new object();
        private MemoryStream m_Stream = new MemoryStream();
        private const int MemoryReclaimThreshold = 1024 * 16; //16KB
        private int m_BytesRequired;

        /// <summary>
        /// The unused data that has been provided to the serializer
        /// </summary>
        public byte[] UnusedData
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_Stream.Position == 0)
                    {
                        //we can just dump the whole stream back
                        return m_Stream.ToArray();
                    }
                    else
                    {
                        long bytesRemaining = m_Stream.Length - m_Stream.Position;
                        byte[] output = null;
                        if (bytesRemaining > 0)
                        {
                            output = new byte[bytesRemaining];
                            long originalPosition = m_Stream.Position;
                            m_Stream.Read(output, 0, output.Length);
                            m_Stream.Position = originalPosition; //set it back so people can get the buffer again.
                        }
                        return output;
                    }
                }
            }
        }

        /// <summary>
        /// Indicates if there is any unused data in the network serializer
        /// </summary>
        public bool HaveUnusedData
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Stream.Length > 0;
                }
            }
        }

        /// <summary>
        /// The number of additional bytes required to create the next packet.
        /// </summary>
        public int BytesRequired { get { return m_BytesRequired; } }


        /// <summary>
        /// Add more information to the serializer stream
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        public void AppendData(byte[] buffer, int length)
        {
            lock (m_Lock)
            {
                long readPosition = m_Stream.Position;
                m_Stream.Position = m_Stream.Length;
                m_Stream.Write(buffer, 0, length);
                m_Stream.Position = readPosition;
            }
        }

        /// <summary>
        /// Read the next network packets in the buffer
        /// </summary>
        /// <returns>A complete packet or null if there isn't enough data to make a packet</returns>
        /// <remarks>Since packets may be spread across multiple packets the serializer
        /// keeps a buffer of any unused bytes for the next read.  This means a Read call
        /// may return zero or one network packets</remarks>
        public NetworkMessage ReadNext()
        {
            lock (m_Lock)
            {
                //now lets figure out if we have one or more packets    
                NetworkMessage nextPacket = null;
                if ((NetworkMessage.ReadHeader(m_Stream, out var packetLength, out var typeCode, out var version))
                    && ((m_Stream.Length - m_Stream.Position) >= packetLength))
                {
                    //we have enough data to read a packet
                    nextPacket = NetworkMessage.Read(m_Stream);
                }
                else if (packetLength > NetworkMessage.MaxMessageLength)
                {
#if DEBUG
                    if (Debugger.IsAttached)
                        Debugger.Break();
#endif                    
                    //something has gone horribly awry: The length the user is specifying is just
                    //not reasonable.
                    Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, null, 0,
                              LogCategory, "The network message indicates it's longer than our maximum supported message, will abort.",
                              "Message Length: {0:N0}\r\n" +
                              "Maximum Length: {1:N0}", packetLength, NetworkMessage.MaxMessageLength);
                    throw new GibraltarNetworkException("The network message indicates it's longer than supported.");
                }

                if (m_Stream.Length != m_Stream.Position)
                {
                    //we have some unused data - record how many bytes we still need.
                    m_BytesRequired = packetLength - (int)(m_Stream.Length - m_Stream.Position);

                    //but be wary of keeping too much memory tied up in the stream.
                    if (m_Stream.Position > MemoryReclaimThreshold) //we've read more than our reclaim threshold
                    {
                        //to be sure the buffer gets sized back *down* we don't just move to the start, 
                        //we ditch the stream and restart.
                        var newStream = new MemoryStream();
                        m_Stream.WriteTo(newStream);
                        m_Stream.Dispose();
                        m_Stream = newStream;
                    }
                }
                else
                {
                    //clear things, we ended on a packet boundary.
                    //but be wary of keeping too much memory tied up in the stream.
                    if (m_Stream.Position > MemoryReclaimThreshold)
                    {
                        m_Stream.Dispose();
                        m_Stream = new MemoryStream();
                    }
                    else
                    {
                        m_Stream.Position = 0;
                        m_Stream.SetLength(0);
                    }

                    m_BytesRequired = 0;
                }

                return nextPacket;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            m_Stream?.Dispose();
        }
    }
}
