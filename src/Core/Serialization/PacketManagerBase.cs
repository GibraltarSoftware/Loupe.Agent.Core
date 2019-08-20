using System.IO;

namespace Loupe.Core.Serialization
{
    /// <summary>
    /// Efficiently manage the deserialization of log packets from a sequence of buffers.
    /// </summary>
    /// <remarks>
    /// Loupe session files are large and complex.  Reading them fast is important to user satisfaction
    /// -- particularly the time to open a session in Loupe Desktop. Sessions are read from a stream in
    /// a series of buffers.  Each buffer contains some number of packets. This base class provides common
    /// infrastructure to support higher-level concrete classes to support synchronous and asynchronous 
    /// reading of buffers.
    /// </remarks>
    public abstract class PacketManagerBase
    {
        /// <summary>
        /// Helper class for PacketManager to provide needed operations on a single buffer
        /// </summary>
        /// <remarks>
        /// This class addresses the three key variants that can occur when trying to read a sequence
        /// of variable length packets from a sequence of buffers:
        /// 
        /// <para><pre>
        /// 1. Packet is contained entirely in one buffer (simple, nominal case)
        /// 2. Packet length is within current buffer but data extends into one or more subsequent buffers
        /// 3. The packet length itself extends into the next buffer
        /// </pre></para>
        /// </remarks>
        protected class Buffer
        {
            /// <summary>
            /// Initial size of buffer, which may be expended in multiples of this size
            /// </summary>
            private const int BufferSize = 1024 * 128;

            /// <summary>
            /// Actual byte array holding buffer data
            /// </summary>
            public byte[] DataBytes { get; private set; }

            /// <summary>
            /// Logical length of buffer (may be less than physical length of DataBytes)
            /// </summary>
            public int Length { get; set; }

            /// <summary>
            /// Current index position within DataBytes
            /// </summary>
            public int Position { get; set; }

            /// <summary>
            /// Initializes a new, empty Buffer of the appropriate default size
            /// </summary>
            public Buffer()
            {
                DataBytes = new byte[BufferSize];
            }

            /// <summary>
            /// Resets this Buffer to be logically empty
            /// </summary>
            public void Reset()
            {
                Length = 0;
                Position = 0;
                PrependToNextBuffer = false;
                NeededFromNextBuffer = 0;
            }

            /// <summary>
            /// Destructively expands the size of this buffer.
            /// Note that existing data is lost if the buffer is expanded
            /// </summary>
            /// <remarks>
            /// If this was a generic class, we'd include an option to retain
            /// the existing data. But, in fact, this is a very specialized
            /// class and this method is only used in the specific circumstance
            /// of preparing a Buffer to receive packet data copied from other 
            /// buffers. Therefore, we have no need for retaining existing data.
            /// </remarks>
            public void ExpandIfNeeded(int newLength)
            {
                if (DataBytes.Length >= newLength)
                    return;

                // If we're going to expand the size of our buffer,
                // expand it by a multiple of BufferSize

                var length = DataBytes.Length;
                while (length < newLength)
                    length += BufferSize;

                DataBytes = new byte[length];
            }

            /// <summary>
            /// Boolean indicating whether this Buffer is logically empty
            /// </summary>
            public bool IsEmpty()
            {
                return Position >= Length;
            }

            /// <summary>
            /// Returns the number of number of remaining bytes in the buffer.
            /// </summary>
            public int BytesAvailable()
            {
                return Length - Position;
            }

            /// <summary>
            /// Boolean that is only true if we have a few stray bytes at the end of
            /// this Buffer that must be prepended to the next logical buffer to be
            /// able to even deserialize the length of the next packet.
            /// </summary>
            public bool PrependToNextBuffer { get; private set; }

            /// <summary>
            /// Integer value indicating the number of bytes needed from the next Buffer
            /// to complete the next Packet.
            /// </summary>
            public int NeededFromNextBuffer { get; private set; }

            /// <summary>
            /// Read the length of the next packet and return a MemoryStream for the next packet, if possible.
            /// Otherwise, return null and set flags to indicate what must happen next.
            /// </summary>
            public MemoryStream GetPacketStream()
            {
                // Start by attempting to read the length of the next packet
                int packetLength = ReadPacketLength();

                // It could be that the length itself spans buffers.  If so, then we will
                // prepend the remainder of this buffer to the next buffer and try again
                if (packetLength == -1) //magic value meaning do this (next stuff)
                {
                    PrependToNextBuffer = true;
                    return null;
                }

                //if the packet size is less than one, that's obviously wrong
                if (packetLength < 1)
                {
                    throw new LoupeSerializationException("The size of the next packet is smaller than 1 byte or negative, which can't be correct.  The packet stream is corrupted.", true);
                }

                // The next possibility is that we read the length, but some of the packet data
                // is in the next buffer. In that case, we'll grab that data from the next buffer.
                if (packetLength > BytesAvailable())
                {
                    NeededFromNextBuffer = packetLength - (Length - Position);
                    return null;
                }

                return GetPacketStream(packetLength);
            }

            /// <summary>
            /// Return a MemoryStream for the next packet assuming a valid packet length has already been read.
            /// </summary>
            public MemoryStream GetPacketStream(int packetLength)
            {
                // Finally, we're down to the nominal case in which all the needed packet data
                // is available in the current buffer. So, all we need do is return a stream
                // referencing that data.
                var stream = new MemoryStream(DataBytes, Position, packetLength);
                Position += packetLength;
                return stream;
            }

            /// <summary>
            /// Copies bytes to the destination array adjusting Position accordingly.
            /// </summary>
            public void ReadBytes(byte[] destination, int offset, int count)
            {
                if (count <= 0)
                    throw new LoupeSerializationException("ReadBytes called with non-positive count: " + count, true);

                if (offset + count > destination.Length)
                    throw new LoupeSerializationException("ReadBytes called with count that would overflow buffer. Count="
                        + count + ", Buffer.Length=" + destination.Length + ", Buffer.Offset=" + offset, true);

                if (count > BytesAvailable())
                    throw new LoupeSerializationException("ReadBytes called to read " + count + " bytes when only " + BytesAvailable() + " bytes are available.", true);

                System.Buffer.BlockCopy(DataBytes, Position, destination, offset, count);
                Position += count;
            }

            /// <summary>
            /// This method is used to read a packet length from this Buffer.
            /// </summary>
            /// <returns>Returns packet length or -1 if the encoding of the packet length extends beyond this Buffer</returns>
            /// <remarks>
            /// This method deserializes using a decoding algorithm taken from FieldReader.ReadUInt64 but adapted slightly
            /// to handle the possibility of the complete value extending into the next Buffer.
            /// </remarks>
            private int ReadPacketLength()
            {
                ulong result = 0;
                int bitCount = 0;

                for (int index = Position; index < Length; index++)
                {
                    // We expect a packet length to fit in an integer.
                    // But since we use 7-bit encoding, the encoded length
                    // may be up to 5 bytes long
                    if (index > Position + 5)
                        throw new LoupeSerializationException("Invalid length detected in ReadPacketLength. Length should not be more that 5 bytes.", true);

                    byte nextByte = DataBytes[index];

                    // Normally, we are reading 7 bits at a time.
                    // But once we've read 8*7=56 bits, if we still
                    // have more bits, there can at most be 8 bits
                    // so we read all 8 bits for that last byte.
                    if (bitCount < 56)
                    {
                        result |= ((ulong) nextByte & 0x7f) << bitCount;
                        bitCount += 7;
                        if ((nextByte & 0x80) == 0)
                        {
                            Position = index + 1;

                            if (result < 0 || result > int.MaxValue)
                                throw new LoupeSerializationException("Illegal packet length: " + result, true);

                            return (int) result;
                        }
                    }
                    else
                    {
                        result |= ((ulong) nextByte & 0xff) << 56;
                        Position = index + 1;

                        if (result < 0 || result > int.MaxValue)
                            throw new LoupeSerializationException("Illegal packet length read: " + result, true); 

                        return (int) result;
                    }
                }
                return -1;
            }
        }

        /// <summary>
        /// Read a Packet from the buffer.
        /// </summary>
        /// <returns>Returns the next Packet from the Buffer, if possible or null if there is 
        /// not data available to read another packet</returns>
        public abstract MemoryStream GetNextPacket();
    }
}
