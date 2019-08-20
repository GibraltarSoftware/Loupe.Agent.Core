
using System;
using System.IO;
using System.Text;
using Loupe.Data;
using Loupe.Serialization.Internal;



namespace Loupe.Serialization
{
    /// <summary>
    /// Provides low-level compression of the basic data types we pass over the wire.
    /// 
    /// This class produces a compressed stream of bytes to be consumed by FieldReader
    /// which will reinstate the original stream of basic data types.
    /// </summary>
    public class FieldWriter : IFieldWriter
    {
        private readonly Stream m_Stream;
        private readonly Encoding m_Encoding;
        private readonly UniqueStringList m_Strings;
        private readonly int m_MajorVersion;
        private readonly int m_MinorVersion;

        private static readonly ArrayEncoder<string> StringArrayWriter = new ArrayEncoder<string>();
        private static readonly ArrayEncoder<int> Int32ArrayWriter = new ArrayEncoder<int>();
        private static readonly ArrayEncoder<long> Int64ArrayWriter = new ArrayEncoder<long>();
        private static readonly ArrayEncoder<uint> UInt32ArrayWriter = new ArrayEncoder<uint>();
        private static readonly ArrayEncoder<ulong> UInt64ArrayWriter = new ArrayEncoder<ulong>();
        private static readonly ArrayEncoder<double> DoubleArrayWriter = new ArrayEncoder<double>();
        private static readonly ArrayEncoder<TimeSpan> TimeSpanArrayWriter = new ArrayEncoder<TimeSpan>();
        private static readonly ArrayEncoder<DateTime> DateTimeArrayWriter = new ArrayEncoder<DateTime>();
        private static readonly ArrayEncoder<DateTimeOffset> DateTimeOffsetArrayWriter = new ArrayEncoder<DateTimeOffset>();
        private static readonly ArrayEncoder<Guid> GuidArrayWriter = new ArrayEncoder<Guid>();

        /// <summary>
        /// Initialize a FieldWriter to write to the specified stream using
        /// the provided encoding for strings.
        /// </summary>
        /// <param name="stream">Stream to write data into</param>
        /// <param name="stringList">The cache of unique strings that have been previously written</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        internal FieldWriter(Stream stream, UniqueStringList stringList, int majorVersion, int minorVersion)
        {
            m_Stream = stream;
            m_Encoding = new UTF8Encoding();
            m_Strings = stringList;
            m_MajorVersion = majorVersion;
            m_MinorVersion = minorVersion;
        }

        /// <summary>
        /// Initialize a FieldWriter to write to the specified stream using
        /// the provided encoding for strings.
        /// </summary>
        /// <param name="stream">Stream to write data into</param>
        public FieldWriter(Stream stream) : this(stream, new UniqueStringList(), FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
            m_Stream = stream;
            m_Encoding = new UTF8Encoding();
            m_Strings = new UniqueStringList();
        }

        /// <summary>
        /// Initialize a FieldWriter to write to a memory stream using
        /// the default encoding for strings.
        /// </summary>
        public FieldWriter()
            : this(new MemoryStream())
        {
        }

        /// <summary>
        /// Returns the current position within the stream.
        /// </summary>
        public long Position { get { return m_Stream.Position; } }

        /// <summary>
        /// Returns the length of the stream.
        /// </summary>
        public long Length { get { return m_Stream.Length; } }

        /// <summary>
        /// Returns the cache of unique string values that have been written
        /// </summary>
        public UniqueStringList Strings { get { return m_Strings; } }

        /// <summary>
        /// Ensure that all pending state changes are committed.
        /// </summary>
        public void Commit()
        {
            m_Strings.Commit();
        }

        /// <summary>
        /// Rollback any pending state changes that have not been committed
        /// </summary>
        public void Rollback()
        {
            m_Strings.Abort();
        }

        #region Write() overloads

        /// <summary>
        /// Write an object to the stream as its serializable type
        /// </summary>
        /// <param name="value">The object (or boxed integral value) to write.</param>
        public void Write(object value)
        {
            //but what type are we going to use?
            FieldType serializedType = PacketDefinition.GetSerializableType(value.GetType());
            Write((Int32)serializedType); // cast to Int32 to match ReadFieldType()

            Write(value, serializedType);
        }

        /// <summary>
        /// Write an object to the stream as its serializable type
        /// </summary>
        /// <param name="value">The object (or boxed integral value) to write.</param>
        /// <param name="fieldType">The field type to write the value out as.</param>
        public void Write(object value, FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.Bool:
                    Write((bool)value);
                    break;
                case FieldType.BoolArray:
                    Write((bool[])value);
                    break;
                case FieldType.String:
                    Write((string)value);
                    break;
                case FieldType.StringArray:
                    Write((string[])value);
                    break;
                case FieldType.Int32:
                    Write(Convert.ToInt32(value));
                    break;
                case FieldType.Int32Array:
                    Write((int[])value);
                    break;
                case FieldType.Int64:
                    Write(Convert.ToInt64(value));
                    break;
                case FieldType.Int64Array:
                    Write((long[])value);
                    break;
                case FieldType.UInt32:
                    Write(Convert.ToUInt32(value));
                    break;
                case FieldType.UInt32Array:
                    Write((uint[])value);
                    break;
                case FieldType.UInt64:
                    Write(Convert.ToUInt64(value));
                    break;
                case FieldType.UInt64Array:
                    Write((ulong[])value);
                    break;
                case FieldType.Double:
                    Write(Convert.ToDouble(value));
                    break;
                case FieldType.DoubleArray:
                    Write((double[])value);
                    break;
                case FieldType.TimeSpan:
                    Write((TimeSpan)value);
                    break;
                case FieldType.TimeSpanArray:
                    Write((TimeSpan[])value);
                    break;
                case FieldType.DateTime:
                    Write((DateTime)value);
                    break;
                case FieldType.DateTimeArray:
                    Write((DateTime[])value);
                    break;
                case FieldType.DateTimeOffset:
                    Write((DateTimeOffset)value);
                    break;
                case FieldType.DateTimeOffsetArray:
                    Write((DateTimeOffset[])value);
                    break;
                case FieldType.Guid:
                    Write((Guid)value);
                    break;
                case FieldType.GuidArray:
                    Write((Guid[])value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>
        /// Write a bool to the stream.
        /// </summary>
        /// <returns>A bool value.</returns>
        public void Write(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Write an array of bool to the stream.
        /// </summary>
        /// <returns>An array of bool values.</returns>
        public void Write(bool[] array)
        {
            // Compress each bool down to a single bit
            int[] words = new int[(array.Length + 31)/32];
            int bitIndex = 0;
            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++ )
            {
                for (int i = 0; i < 32 && bitIndex < array.Length; i++)
                {
                    words[wordIndex] <<= 1;
                    if (array[bitIndex])
                        words[wordIndex] |= 1;
                    bitIndex++;
                }
            }

            Write((UInt32)array.Length);
            Write(words);
        }

        /// <summary>
        /// Write a string to the stream.
        /// <remarks>
        /// We optimize strings by maintaining a hash table of each unique string 
        /// we have seen.  Each string is sent with as an integer index into the table.
        /// When a new string is encountered, it's index is followed by the string value.
        /// </remarks>
        /// </summary>
        public void Write(string value)
        {
            if (m_MajorVersion > 1)
            {
                WriteString(value);
                return;
            }

            if (value == null)
            {
                WriteByte(0);
                return;
            }

            // The AddOrGet method first checks if the string is already in the table.
            // If so, it just returns the existing index.  Otherwise, it adds the
            // string to the end of the table and returns the index.  In that case,
            // we can detect that this is the first time that a string has been
            // seen by checking if the returned index is too large relative to the
            // Count property before the AddOrGet. 
            int newStringIndex = m_Strings.Count + 1; // this line must be called before AddOrGet
            int stringIndex = m_Strings.AddOrGet(value) + 1;
            Write((UInt32)stringIndex);

            // If we've just added a new string to the table, the index should be equal to
            // the previous count of strings. We have added 1 to the index values because we
            // use zero-based indexing in the string table but 1-based indexing over the wire.
            if (stringIndex == newStringIndex)
            {
                WriteString(value);
            }
            else if (stringIndex > newStringIndex)
            {
                throw new LoupeException(
                    "Something went wrong with string serialization, we got a bogus index from our string table: " + stringIndex);
            }
        }

        /// <summary>
        /// Write an array of string to the stream.
        /// </summary>
        /// <returns>An array of string values.</returns>
        public void Write(string[] array)
        {
            StringArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a 32-bit signed value into the stream using 7-bit encoding.
        /// <remarks>
        /// The value is written 7 bits at a time (starting with the least-significant bits) until there are no more bits to write.
        /// The eighth bit of each byte stored is used to indicate whether there are more bytes following this one.
        /// </remarks>
        /// </summary>
        public void Write(Int32 value)
        {
            byte firstByte;
            if (value < 0)
            {
                // If the value is negative, set the sign bit and negate the value
                // to optimize 7-bit encoding
                firstByte = 0x80;
                value = -value;
            }
            else
                firstByte = 0;

            UInt32 unsignedValue = unchecked((UInt32)value);
            // include the first 6 bits of the value in the first byte 
            firstByte |= (byte)(unsignedValue & 0x3f);
            unsignedValue >>= 6;
            if (unsignedValue == 0)
            {
                // if the value is in the range [-63..63] we only need to write one byte
                WriteByte(firstByte);
            }
            else
            {
                // In this case we need to write at least 2 bytes.  The second bit of
                // the first byte is used to indicate that more bytes follow.
                firstByte |= 0x40;
                WriteByte(firstByte);

                while (unsignedValue >= 0x80)
                {
                    WriteByte((byte)(unsignedValue | 0x80));
                    unsignedValue >>= 7;
                }
                WriteByte((byte)unsignedValue);
            }
        }

        /// <summary>
        /// Write an array of Int32 to the stream.
        /// </summary>
        /// <returns>An array of Int32 values.</returns>
        public void Write(Int32[] array)
        {
            Int32ArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a 64-bit signed value into the stream using 7-bit encoding.
        /// <remarks>
        /// The value is written 7 bits at a time (starting with the least-significant bits) until there are no more bits to write.
        /// The eighth bit of each byte stored is used to indicate whether there are more bytes following this one.
        /// </remarks>
        /// </summary>
        /// <param name="value">The Int64 value to encode.</param>
        public void Write(Int64 value)
        {
            byte firstByte;
            if (value < 0)
            {
                // If the value is negative, set the sign bit and negate the value
                // to optimize 7-bit encoding
                firstByte = 0x80;
                value = -value;
            }
            else
                firstByte = 0;

            UInt64 unsignedValue = unchecked((UInt64)value);
            // include the first 6 bits of the value in the first byte 
            firstByte |= (byte)(unsignedValue & 0x3f);
            unsignedValue >>= 6;
            if (unsignedValue == 0)
            {
                // if the value is in the range [-32..31] we only need to write one byte
                WriteByte(firstByte);
            }
            else
            {
                // In this case we need to write at least 2 bytes.  The second bit of
                // the first byte is used to indicate that more bytes follow.
                firstByte |= 0x40;
                WriteByte(firstByte);

                while (unsignedValue >= 0x80)
                {
                    WriteByte((byte)(unsignedValue | 0x80));
                    unsignedValue >>= 7;
                }
                WriteByte((byte)unsignedValue);
            }
        }

        /// <summary>
        /// Write an array of Int64 to the stream.
        /// </summary>
        /// <returns>An array of Int64 values.</returns>
        public void Write(Int64[] array)
        {
            Int64ArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a 32-bit unsigned value into the stream using 7-bit encoding.
        /// <remarks>
        /// The value is written 7 bits at a time (starting with the least-significant bits) until there are no more bits to write.
        /// The eighth bit of each byte stored is used to indicate whether there are more bytes following this one.
        /// </remarks>
        /// </summary>
        /// <param name="value">The UInt32 value to encode.</param>
        public void Write(UInt32 value)
        {
            while (value >= 0x80)
            {
                WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            WriteByte((byte)value);
        }

        /// <summary>
        /// Write an array of UInt32 to the stream.
        /// </summary>
        /// <returns>An array of UInt32 values.</returns>
        public void Write(UInt32[] array)
        {
            UInt32ArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a 64-bit unsigned value into the stream using 7-bit encoding.
        /// <remarks>
        /// The value is written 7 bits at a time (starting with the least-significant bits) until there are no more bits to write.
        /// The eighth bit of each byte stored is used to indicate whether there are more bytes following this one.
        /// 
        /// There is a special optimization for UInt64 because after passing 8 7-bit values we know that
        /// there can only be 8 bits left (since the original data was 64 bits long).  So, for that last byte,
        /// we can use all 8 bits.  This means that the worst case size for a double is 9 bytes versus
        /// 10 which would otherwise sometimes be necessary to pass that very last bit.
        /// </remarks>
        /// </summary>
        /// <param name="value">The UInt64 value to encode.</param>
        public void Write(UInt64 value)
        {
            for (int byteCount = 0; byteCount < 8 && value >= 0x80; byteCount++)
            {
                WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            WriteByte((byte)value);
        }

        /// <summary>
        /// Efficiently encodes a packet length as a variable length byte array using 7-bit encoding
        /// </summary>
        /// <param name="length">Packet length to be encoded</param>
        /// <returns>Returns a MemoryStream containing the encoded length</returns>
        public static MemoryStream WriteLength(long length)
        {
            var bytes = new byte[9];

            int byteCount;
            for (byteCount = 0; byteCount < 8 && length >= 0x80; byteCount++)
            {
                bytes[byteCount] = (byte)(length | 0x80);
                length >>= 7;
            }

            bytes[byteCount++] = (byte)length;

            return new MemoryStream(bytes, 0, byteCount);
        }

        /// <summary>
        /// Write an array of UInt64 to the stream.
        /// </summary>
        /// <returns>An array of UInt64 values.</returns>
        public void Write(UInt64[] array)
        {
            UInt64ArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a 64-bit double value int the stream in the fewest bytes possible.
        /// <remarks>
        /// For many common numbers the bit representation of a double includes lots of
        /// trailing zeros.  This creates an opportunity to optimize these values in a
        /// similar way to how we optimize UInt64.  The difference is just that in this case
        /// we are interested in the high-order bits whereas with UInt64 we are interested
        /// in the low order bits.
        /// </remarks>
        /// </summary>
        public void Write(double value)
        {
            // First off, convert to bits to make bit-twiddling possible
            UInt64 bits = (UInt64)BitConverter.DoubleToInt64Bits(value);

            // For zero, we only need to send a single byte
            if (bits == 0)
                WriteByte(0);

            else
            {
                // We're done if either their are no more bits to send or we've written 8 bytes
                for (int byteCount = 0; byteCount < 8 && bits != 0; byteCount++)
                {
                    // Grab the leftmost 7 bits
                    UInt64 maskedBits = bits & 0xFE00000000000000U;
                    byte nextByte = (byte)(maskedBits >> 57);
                    bits <<= 7;

                    // set the high order bit within the byte if more bits still to process
                    if (bits != 0)
                        nextByte |= 0x80;

                    WriteByte(nextByte);
                }
                // After writing 8 7-bit values, we've written 56 bits.  So, if
                // we have bits left, we have 8 bits at most, so let's write them.
                if (bits != 0)
                    WriteByte((byte)(bits >> 56));
            }
        }

        /// <summary>
        /// Write an array of double to the stream.
        /// </summary>
        /// <returns>An array of double values.</returns>
        public void Write(double[] array)
        {
            DoubleArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a TimeSpan value to the stream
        /// </summary>
        public void Write(TimeSpan value)
        {
            Write((UInt64)value.Ticks);
        }

        /// <summary>
        /// Write an array of TimeSpan to the stream.
        /// </summary>
        /// <returns>An array of TimeSpan values.</returns>
        public void Write(TimeSpan[] array)
        {
            TimeSpanArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a DateTime value to the stream
        /// </summary>
        public void Write(DateTime value)
        {
            //write it out as a date time offset so we get time offset information
            Write(new DateTimeOffset(value));
        }
        /// <summary>
        /// Stores a DateTime value to the stream
        /// </summary>
        public void Write(DateTimeOffset value)
        {
            //write out the time zone offset for this date in minutes (because there are some partial hour time zones)
            Write((int)value.Offset.TotalMinutes);

            // On first write, we store the reference time, thereafter,
            // we store DateTime as offset to the reference time
            if (m_Strings.ReferenceTime == DateTime.MinValue)
            {
                m_Strings.ReferenceTime = value.DateTime;
                WriteByte((byte)DateTimeEncoding.NewReference); // Tell it to set ReferenceTime from this
                Write((UInt64)value.Ticks);
            }
            else
            {
                TimeSpan delta = value.DateTime - m_Strings.ReferenceTime;
                if (delta.Ticks < 0)
                {
                    WriteByte((byte)DateTimeEncoding.EarlierTicksNet); // earlier than ReferenceTime
                    Write((UInt64)(-delta.Ticks)); // convert negative to absolute value and cast unsigned
                }
                else
                {
                    WriteByte((byte)DateTimeEncoding.LaterTicksNet); // later than ReferenceTime
                    Write((UInt64)delta.Ticks); // confirmed to be non-negative, safe to cast unsigned
                }
            }
        }

        /// <summary>
        /// Write an array of DateTime to the stream.
        /// </summary>
        /// <returns>An array of DateTime values.</returns>
        public void Write(DateTime[] array)
        {
            DateTimeArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Write an array of DateTimeOffset to the stream.
        /// </summary>
        /// <returns>An array of DateTimeOffset values.</returns>
        public void Write(DateTimeOffset[] array)
        {
            DateTimeOffsetArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a DateTime value to the stream
        /// </summary>
        /// <remarks></remarks>
        public void WriteTimestamp(DateTime value) // change to Timestamp value if we make it?
        {
            // We always use UTC time for consistency
            DateTime timestamp = value.ToUniversalTime();

            // On first write, we store the reference time, thereafter,
            // we store DateTime as offset to the reference time
            if (m_Strings.ReferenceTime == DateTime.MinValue)
            {
                m_Strings.ReferenceTime = timestamp;
                Write(TimeSpan.Zero.Ticks);
                Write(timestamp.Ticks);
            }
            else
            {
                TimeSpan delta = timestamp - m_Strings.ReferenceTime;
                Write(delta.Ticks);

                //if we got no delta then we have to gratuitously write out a zero to make sure that 
                //we read this back on the other side, because it's going to think we just specified
                //a new reference.
                if (delta.Ticks == 0)
                {
                    Write((Int64)0);
                }
            }
        }

        /// <summary>
        /// Write an array of Timestamp to the stream.
        /// </summary>
        /// <returns>An array of DateTime values.</returns>
        public void WriteTimestamp(DateTime[] array) // change to Timestamp[] array if we make it?
        {
            DateTimeArrayWriter.Write(array, this);
        }

        /// <summary>
        /// Stores a 128-bit Guid value to the stream
        /// </summary>
        public void Write(Guid value)
        {
            byte[] array = value.ToByteArray();
            WriteBytes(array);
        }

        /// <summary>
        /// Write an array of Guid to the stream.
        /// </summary>
        /// <returns>An array of Guid values.</returns>
        public void Write(Guid[] array)
        {
            GuidArrayWriter.Write(array, this);
        }

        #endregion

        /// <summary>
        /// This is a helper method for unit testing.  It only works for the case
        /// the underlying stream is a MemoryStream.
        /// </summary>
        /// <returns>The stream data as a byte array</returns>
        public byte[] ToArray()
        {
            MemoryStream memory = m_Stream as MemoryStream;
            if (memory != null)
            {
                return memory.ToArray();
            }

            return null;
        }

        /// <summary>
        /// Helper method to write a single byte to the underlying stream.
        /// </summary>
        /// <param name="value">byte to be written</param>
        private void WriteByte(byte value)
        {
            m_Stream.WriteByte(value);
        }

        /// <summary>
        /// Helper method to write a single byte to the underlying stream.
        /// </summary>
        /// <param name="values">byte array to be written</param>
        private void WriteBytes(byte[] values)
        {
            m_Stream.Write(values, 0, values.Length);
        }

        /// <summary>
        /// Helper method to write a string to the underlying stream.
        /// </summary>
        /// <param name="value">String to be written</param>
        private void WriteString(string value)
        {
            // Under ProtocolVersion > 0 we pass null as a special 2-byte sequence
            if (value == null)
            {
                WriteByte(1);
                WriteByte(0);
            }
            else if (value.Length == 0)
            {
                WriteByte(0);
            }
            else
            {
                byte[] bytes = m_Encoding.GetBytes(value);
                Write((UInt32)bytes.Length);
                m_Stream.Write(bytes, 0, bytes.Length);
            }

        }
    }
}