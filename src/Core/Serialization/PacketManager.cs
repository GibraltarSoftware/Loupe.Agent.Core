using System.IO;

namespace Loupe.Serialization
{
    /// <summary>
    /// Efficiently manage the deserialization of log packets.
    /// </summary>
    /// <remarks>
    /// The idea is that PacketManager will extract and deserialize a sequence of packets from an sequence of buffers.
    /// 
    /// This class uses a double-buffering strategy that allows efficient reading of a file with a minimum of
    /// superfluous object allocation or memory copying.
    /// </remarks>
    public class PacketManager : PacketManagerBase
    {
        private readonly Stream m_Stream;
        private Buffer m_CurrentBuffer;
        private Buffer m_OtherBuffer;
        private int m_PacketCount = 0;

        /// <summary>
        /// Bind to a stream to of data from which a sequence of packets can be read.
        /// </summary>
        /// <remarks>
        /// This class is intended for reading session data from a file.  The GetPacketStream method will
        /// "pull" the relevant data from the stream to complete each request.  Use PacketManagerAsync for
        /// reading packets from the network where data must be pushed in from the network.
        /// </remarks>
        public PacketManager(Stream stream)
        {
            m_Stream = stream;
            if (m_CurrentBuffer == null)
                m_CurrentBuffer = new Buffer();
            if (m_OtherBuffer == null)
                m_OtherBuffer = new Buffer();
        }

        /// <summary>
        /// Read another buffer of data to be deserialized into packets
        /// </summary>
        private int FetchBuffer(Buffer buffer)
        {
            var offset = buffer.Position == buffer.Length ? 0 : buffer.Position;
            var count = buffer.DataBytes.Length - offset;
            var bytesRead = m_Stream.Read(buffer.DataBytes, offset, count);
            if (bytesRead > 0)
                buffer.Length = offset + bytesRead;
            else
                buffer.Length = offset;

            return bytesRead;
        }

        /// <summary>
        /// Swap the buffers we use for double-buffering stream data.
        /// </summary>
        private void SwapBuffers()
        {
            Buffer temp = m_CurrentBuffer;
            m_CurrentBuffer = m_OtherBuffer;
            m_OtherBuffer = temp;
        }

        /// <summary>
        /// Get the next packet from the stream
        /// </summary>
        /// <returns>If another packet exists, return a MemoryStream to the packet data.  Return null at end-of-file.</returns>
        public override MemoryStream GetNextPacket()
        {
            for (int iteration = 0; iteration < 2; iteration++)
            {
                MemoryStream nextPacket;

                if (m_CurrentBuffer.PrependToNextBuffer)
                {
                    // This is a rare case that should only happen at the end of a buffer in which
                    // we don't have quite enough bytes to read the length of the next packet.
                    var remainingBytes = m_CurrentBuffer.BytesAvailable();
                    if (remainingBytes <= 0 || remainingBytes >= 5)
                        throw new GibraltarSerializationException("Unexpected number of BytesAvailable: " + remainingBytes +
                                                                  ". The value should be in the range [1..4] because it is a partial length.", true);

                    // Copy these last few bytes from the current buffer into the other buffer
                    m_OtherBuffer.Reset();
                    m_CurrentBuffer.ReadBytes(m_OtherBuffer.DataBytes, 0, remainingBytes);

                    // Fill the rest of the buffer
                    m_OtherBuffer.Position = remainingBytes;
                    var bytesRead = FetchBuffer(m_OtherBuffer);
                    if (bytesRead <= 0)
                        throw new GibraltarSerializationException("Unable to read the rest of a packet length that spans buffers", true);

                    // Reset the position and swap buffers so m_CurrentBuffer will contain the complete packet
                    m_OtherBuffer.Position = 0;
                    SwapBuffers();

                    // At this point, we can now read the packet length and probably get all the data.
                    // One edge case would be if this was a huge packet and we need to expand our buffer to read it all.
                    // Another edge case is if the file has a ragged edge and insufficient data is available
                    nextPacket = m_CurrentBuffer.GetPacketStream();
                }

                else if (m_CurrentBuffer.NeededFromNextBuffer > 0)
                {
                    var neededFromCurrentBuffer = m_CurrentBuffer.BytesAvailable();
                    var neededFromNextBuffer = m_CurrentBuffer.NeededFromNextBuffer;
                    var requiredLength = neededFromCurrentBuffer + neededFromNextBuffer;
                    m_OtherBuffer.Reset();
                    m_OtherBuffer.ExpandIfNeeded(requiredLength);

                    // It's possible that just the length fit at the very end of a packet
                    // and that all the data is in the next packet. In which case, there 
                    // is no data in the current packet to read.
                    if (neededFromCurrentBuffer > 0)
                        m_CurrentBuffer.ReadBytes(m_OtherBuffer.DataBytes, 0, neededFromCurrentBuffer);

                    m_OtherBuffer.Position = neededFromCurrentBuffer;
                    var bytesRead = FetchBuffer(m_OtherBuffer);

                    if (bytesRead < neededFromNextBuffer)
                    {
                        throw new GibraltarSerializationException(string.Format(
                            "Incomplete session file detected after reading {0} packets.  Expected {1} bytes but hit EOF after {2} bytes.",
                            m_PacketCount, requiredLength, neededFromCurrentBuffer + bytesRead), true);
                    }

                    // Reset the position and swap buffers so m_CurrentBuffer will contain the complete packet
                    m_OtherBuffer.Position = 0;
                    SwapBuffers();
                    nextPacket = m_CurrentBuffer.GetPacketStream(requiredLength);
                }

                else
                {
                    // The the current buffer is empty, read more data
                    if (m_CurrentBuffer.IsEmpty())
                    {
                        // This should fill the buffer
                        m_CurrentBuffer.Reset();
                        var bytesRead = FetchBuffer(m_CurrentBuffer);

                        // If the buffer is still empty, it's because there is no more data
                        if (bytesRead <= 0)
                            return null;
                    }

                    // This is the nominal case of reading the next packet from the current buffer
                    nextPacket = m_CurrentBuffer.GetPacketStream();
                }

                // If we have a valid packet, return it.  If it's null, we can iterate one more time to
                // get the rest of the packet.
                if (nextPacket != null)
                {
                    m_PacketCount++;
                    return nextPacket;
                }
            }

            // TODO: We shouldn't get here.  If we do, what should we do about it?
            return null;
        }
    }

}
