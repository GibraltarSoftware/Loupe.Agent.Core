using System.IO;

namespace Loupe
{
    /// <summary>
    /// A derivative of a MemoryStream primarily for reading which provides additional methods to append data and
    /// periodically truncate (discard already-read data which won't be needed again).  (Currently no safety locking!)
    /// </summary>
    public class PipeStream : MemoryStream
    {
        private readonly object m_Lock = new object();
        private readonly int m_InitialCapacity;

        /// <summary>
        /// Initializes a new PipeStream with an expandable capacity.
        /// </summary>
        public PipeStream()
        {
            m_InitialCapacity = 0;
        }

        /// <summary>
        /// Initializes a new PipeStream with an expandable capacity starting as specified. 
        /// </summary>
        /// <param name="capacity"></param>
        public PipeStream(int capacity)
            : base(capacity)
        {
            m_InitialCapacity = capacity;
        }

        /// <summary>
        /// The number of bytes we haven't read yet in the stream.
        /// </summary>
        public long UnreadLength
        {
            get
            {
                lock(m_Lock)
                {
                    return Length - Position;
                }
            }
        }

        /// <summary>
        /// Appends a block of bytes to the end of the current stream using data read from buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write data from.</param>
        /// <param name="offset">The byte offset in <paramref name="buffer"/> at which to begin writing from.</param>
        /// <param name="count">The maximum number of bytes to write.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support writing. For additional information
        ///     see <see cref="P:System.IO.Stream.CanWrite"/>. -or- The current position is closer than
        ///     <paramref name="count"/> bytes to the end of the stream, and the capacity cannot be modified.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="offset"/> subtracted from the buffer length is less
        ///     than <paramref name="count"/>.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> are
        ///     negative.</exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        /// <exception cref="T:System.ObjectDisposedException">The current stream instance is closed.</exception>
        /// <filterpriority>2</filterpriority>
        public void Append(byte[] buffer, int offset, int count)
        {
            lock (m_Lock)
            {
                long currentPosition = Position; // Remember this!
                try
                {
                    Position = Length; // Position it at the end of the stream.
                    base.Write(buffer, offset, count); // Write the data as given.
                }
                finally
                {
                    Position = currentPosition; // Restore the original (reading) position.
                }
            }
        }

        /// <summary>
        /// Writes a block of bytes to the current stream using data read from buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write data from.</param>
        /// <param name="offset">The byte offset in <paramref name="buffer"/> at which to begin writing from.</param>
        /// <param name="count">The maximum number of bytes to write.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support writing. For additional information
        ///     see <see cref="P:System.IO.Stream.CanWrite"/>. -or- The current position is closer than
        ///     <paramref name="count"/> bytes to the end of the stream, and the capacity cannot be modified.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="offset"/> subtracted from the buffer length is less
        ///     than <paramref name="count"/>.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> are
        ///     negative.</exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        /// <exception cref="T:System.ObjectDisposedException">The current stream instance is closed.</exception>
        /// <filterpriority>2</filterpriority>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Append(buffer, offset, count);
        }

        /// <summary>
        /// Discard already-read data (up to the read Position) from the stream buffer and reset the buffer length to free memory.
        /// </summary>
        /// <returns>The number of bytes discarded from the stream.</returns>
        public long Trim()
        {
            lock (m_Lock)
            {                
                long currentPosition = Position;
                long currentLength = Length;
                long newLength = currentLength - currentPosition;

                //but lets not be wasteful...
                if ((currentPosition == 0) || (currentLength == 0) || ((double)newLength / currentLength > 0.5))
                    return 0;

                var buffer = ToArray();
                SetLength(0); // We have to do this before we reset the Capacity or else it will barf.
                Capacity = 0; // This special case should drop the old buffer and start anew.
                if (m_InitialCapacity > 0)
                    Capacity = m_InitialCapacity; // Start fresh with our initial requested capacity.

                if (newLength > 0)
                {
                    base.Write(buffer, (int)currentPosition, (int)newLength);
                    Position = 0; // Reset the read Position to the start of the unread data.
                }

                return currentPosition; // The number of bytes we discarded from the stream.
            }
        }
    }
}
