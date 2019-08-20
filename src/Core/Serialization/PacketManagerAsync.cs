using System.IO;

namespace Loupe.Serialization
{
    /// <summary>
    /// Efficiently manage the deserialization of log packets with asynchronous reading of buffers
    /// </summary>
    public class PacketManagerAsync : PacketManagerBase
    {
        private readonly Stream m_Stream;

        /// <summary>
        /// TBD: Need to integrate optimized version of PipeStream.
        /// </summary>
        /// <param name="stream"></param>
        public PacketManagerAsync(Stream stream)
        {
            m_Stream = stream;
        }

        /// <summary>
        /// Get the next packet from the stream
        /// </summary>
        /// <returns>Returns a Packet or null if a Packet is not available</returns>
        public override MemoryStream GetNextPacket()
        {
            return null;
        }
    }
}
