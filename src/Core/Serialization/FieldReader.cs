using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Loupe.Core.Data;
using Loupe.Core.IO;
using Loupe.Core.Serialization.Internal;

namespace Loupe.Core.Serialization
{
    /// <summary>
    /// Provides low-level decompression of the basic data types we pass over the wire.
    /// 
    /// This class consumes a compressed stream of bytes to be produced by FieldWriter
    /// to reinstate the original stream of basic data types passed to FiedWriter.
    /// </summary>
    public class FieldReader : IFieldReader
    {
        private Stream m_Stream;
        private readonly Encoding m_Encoding;
        private readonly UniqueStringList m_Strings;
        private readonly int m_MajorVersion;
        private readonly int m_MinorVersion;

        private static readonly ArrayEncoder<string> StringArrayReader = new ArrayEncoder<string>();
        private static readonly ArrayEncoder<int> Int32ArrayReader = new ArrayEncoder<int>();
        private static readonly ArrayEncoder<long> Int64ArrayReader = new ArrayEncoder<long>();
        private static readonly ArrayEncoder<uint> UInt32ArrayReader = new ArrayEncoder<uint>();
        private static readonly ArrayEncoder<ulong> UInt64ArrayReader = new ArrayEncoder<ulong>();
        private static readonly ArrayEncoder<double> DoubleArrayReader = new ArrayEncoder<double>();
        private static readonly ArrayEncoder<TimeSpan> TimeSpanArrayReader = new ArrayEncoder<TimeSpan>();
        private static readonly ArrayEncoder<DateTime> DateTimeArrayReader = new ArrayEncoder<DateTime>();
        private static readonly ArrayEncoder<DateTimeOffset> DateTimeOffsetArrayReader = new ArrayEncoder<DateTimeOffset>();
        private static readonly ArrayEncoder<Guid> GuidArrayReader = new ArrayEncoder<Guid>();

        /// <summary>
        /// Initialize a FieldReader to read the specified stream using
        /// the provided encoding for strings.  Also, share state with the
        /// specified parent reader.
        /// </summary>
        /// <param name="stream">Data to be read</param>
        /// <param name="stringList">The cache of unique strings that have been previously read</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        public FieldReader(Stream stream, UniqueStringList stringList, int majorVersion, int minorVersion)
        {
            m_Stream = stream;
            m_Encoding = new UTF8Encoding();
            m_Strings = stringList;
            m_MajorVersion = majorVersion;
            m_MinorVersion = minorVersion;
        }

        /// <summary>
        /// Initialize a FieldReader to read the specified stream using
        /// the provided encoding for strings.
        /// </summary>
        /// <param name="stream">Data to be read</param>
        public FieldReader(Stream stream) : this(stream, new UniqueStringList(), FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
        }

        /// <summary>
        /// Initialize a FieldReader to read the specified data using
        /// the default encoding for strings.
        /// </summary>
        /// <param name="data">Data to be read</param>
        public FieldReader(byte[] data)
            : this(new MemoryStream(data))
        {
        }

        /// <summary>
        /// Initialize a FieldReader to read the specified data using
        /// the default encoding for strings.  Also, share state with the
        /// specified parent reader.
        /// </summary>
        /// <param name="data">Data to be read</param>
        /// <param name="stringList">The cache of unique strings that have been previously read</param>
        public FieldReader(byte[] data, UniqueStringList stringList)
            : this(new MemoryStream(data), stringList, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
        }

        /// <summary>
        /// Allows the stream being read by a FieldReader to be replaced without having to re-instance a new object.
        /// </summary>
        public void ReplaceStream(Stream newStream)
        {
            m_Stream = newStream;
        }

        #region IFieldReader Members

        /// <summary>
        /// Returns the cache of unique string values that have been written
        /// </summary>
        public UniqueStringList Strings { get { return m_Strings; } }


        /// <summary>
        /// Returns a UInt64 value from the stream without repositioning the stream
        /// </summary>
        /// <returns>A UInt64 value.</returns>
        public UInt64 PeekUInt64()
        {
            long originalPosition = m_Stream.Position;
            try
            {
                return ReadUInt64();
            }
            finally 
            {
                m_Stream.Position = originalPosition;
            }
        }

        /// <summary>
        /// Returns a bool value from the stream.
        /// </summary>
        /// <returns>A bool value.</returns>
        public bool ReadBool()
        {
            return ReadByte() != 0;
        }

        /// <summary>
        /// Returns an array of bool values from the stream.
        /// </summary>
        /// <remarks>
        /// The idea here is to compress the bool[] down to a bit vector which is then further
        /// compressed as an array of Int32 words.
        /// </remarks>
        /// <returns>An array bool values.</returns>
        public bool[] ReadBoolArray()
        {
            // first we read th number of bools encoded
            int bitCount = (int)ReadUInt32();
            bool[] array = new bool[bitCount];
            // then we read the Int32[] containing the bits
            int[] words = ReadInt32Array();

            // Decompress bits back into bool array
            int bitIndex = 0;
            int highOrderBit = 1 << 31;
            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                int word = words[wordIndex];
                // the last word may be only partially filled
                if (bitCount - bitIndex < 32)
                {
                    // handle a partial last word by skipping over the unused bits
                    int unusedBits = 32 - bitCount % 32;
                    word <<= unusedBits;
                }
                //
                for (int i = 0; i < 32 & bitIndex < bitCount; i++)
                {
                    array[bitIndex] = (word & highOrderBit) == highOrderBit;
                    bitIndex++;
                    word <<= 1;
                }
            }
            return array;
        }

        /// <summary>
        /// Read a string from the stream.
        /// <remarks>
        /// We optimize strings by maintaining a hash table of each unique string 
        /// we have seen.  Each string is sent with as an integer index into the table.
        /// When a new string is encountered, it's index is followed by the string value.
        /// </remarks>
        /// </summary>
        /// <returns>Returns the string</returns>
        public string ReadString()
        {
            // Dropping string tables is pretty easy!
            if (m_MajorVersion > 1)
                return ReadStringDirect();

            /*
             * The rest of this method is for protocol version 1.0
             */
            string stringValue; // string to be returned

            var index = (int)ReadUInt32();
            if (index == 0)
                return null;

            // check if this is a token we've never seen before.
            // If so, the value follows and we should add it to the token list.
            if (--index == m_Strings.Count)
            {
                stringValue = ReadStringDirect();
                m_Strings.AddOrGet(stringValue);
            }
            else
            {
                stringValue = m_Strings[index];
                CompensateForPossibleSerializationError(stringValue);
            }

            return stringValue;
        }

        /// <summary>
        /// We found that there is an occasional error in the way Agent serializes strings under
        /// protocol version 1.0.  Every once in a while, a string that has already been entered
        /// into the string table has its value passed again as well as its string token.
        /// 
        /// We suspect that this may be do to a threading issue but since we diagnosed the
        /// problem at the time we were already implementing protocol version 2.0, we didn't
        /// spend the time to exactly work out the precise circumstances that would cause the
        /// error to occur. At least, we didn't investigate further than identifying these potentially
        /// un-thread-safe lines in FieldWriter.Write:
        /// <code>
        ///     int newStringIndex = m_Strings.Count + 1;
        ///     int stringIndex = m_Strings.AddOrGet(value) + 1;
        ///     Write((UInt32)stringIndex);
        ///     if (stringIndex == newStringIndex) 
        ///        WriteString(value);
        /// </code>
        /// i.e. The error seen empirically could occur if two threads execute that code snippet concurrently.
        /// </summary>
        /// <param name="stringValue"></param>
        private void CompensateForPossibleSerializationError(string stringValue)
        {
            // Null or empty string are never stored in the string table
            if (!string.IsNullOrEmpty(stringValue))
            {
                var position = m_Stream.Position;
                bool restorePosition = true;
                try
                {
                    // Peek ahead in the buffer looking for the case that the remainder of the
                    // buffer contains the same string just retrieved from the string table
                    var remainingBytes = m_Stream.Length - position;
                    if (stringValue.Length <= remainingBytes)
                    {
                        UInt32 length = ReadUInt32();
                        if (length == stringValue.Length)
                        {
                            var bytes = new byte[length];
                            int readBytes = m_Stream.Read(bytes, 0, (int) length);
                            if (readBytes == length)
                            {
                                var value = m_Encoding.GetString(bytes);
                                if (string.Equals(stringValue, value))
                                {
                                    //Console.WriteLine("BAD STRING: " + value);
                                    restorePosition = false;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
                }
                finally
                {
                    if (restorePosition)
                        m_Stream.Position = position;
                }
            }
        }

        /// <summary>
        /// Read an array of strings from the stream.
        /// </summary>
        /// <returns>Returns an array of string values</returns>
        public string[] ReadStringArray()
        {
            string[] array = StringArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns an Int32 value from the stream.
        /// </summary>
        /// <returns>An Int32 value.</returns>
        public Int32 ReadInt32()
        {
            byte firstByte = ReadByte();
            int result = firstByte & 0x3f;
            int bitShift = 6;
            if ((firstByte & 0x40) != 0)
            {
                while (true)
                {
                    byte nextByte = ReadByte();
                    result |= (nextByte & 0x7f) << bitShift;
                    bitShift += 7;
                    if ((nextByte & 0x80) == 0)
                        break;
                }
            }
            if ((firstByte & 0x80) == 0)
                return result;
            else
                return -result;
        }

        /// <summary>
        /// Returns an array of Int32 values from the stream.
        /// </summary>
        /// <returns>An array of Int32 values.</returns>
        public int[] ReadInt32Array()
        {
            Int32[] array = Int32ArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns an Int64 value from the stream.
        /// </summary>
        /// <returns>An Int64 value.</returns>
        public Int64 ReadInt64()
        {
            byte firstByte = ReadByte();
            ulong result = (ulong)(firstByte & 0x3f);
            int bitShift = 6;
            if ((firstByte & 0x40) != 0)
            {
                while (true)
                {
                    byte nextByte = ReadByte();
                    result |= (ulong)(nextByte & 0x7f) << bitShift;
                    bitShift += 7;
                    if ((nextByte & 0x80) == 0)
                        break;
                }
            }
            if ((firstByte & 0x80) == 0)
                return (long)result;
            else
                return -(long)result;
        }

        /// <summary>
        /// Returns an array of Int64 values from the stream.
        /// </summary>
        /// <returns>An array of Int64 values.</returns>
        public Int64[] ReadInt64Array()
        {
            Int64[] array = Int64ArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns a UInt32 value from the stream.
        /// </summary>
        /// <returns>A UInt32 value.</returns>
        public UInt32 ReadUInt32()
        {
            uint result = 0;
            int bitShift = 0;
            while (true)
            {
                byte nextByte = ReadByte();
                result |= ((uint)nextByte & 0x7f) << bitShift;
                bitShift += 7;
                if ((nextByte & 0x80) == 0)
                    return result;
            }
        }

        /// <summary>
        /// Returns an array of UInt32 values from the stream.
        /// </summary>
        /// <returns>An array of UInt32 values.</returns>
        public UInt32[] ReadUInt32Array()
        {
            UInt32[] array = UInt32ArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns a UInt64 value from the stream.
        /// </summary>
        /// <returns>A UInt64 value.</returns>
        public UInt64 ReadUInt64()
        {
            ulong result = 0;
            int bitCount = 0;
            while (true)
            {
                byte nextByte = ReadByte();
                // Normally, we are reading 7 bits at a time.
                // But once we've read 8*7=56 bits, if we still
                // have more bits, there can at most be 8 bits
                // so we read all 8 bits for that last byte.
                if (bitCount < 56)
                {
                    result |= ((ulong)nextByte & 0x7f) << bitCount;
                    bitCount += 7;
                    if ((nextByte & 0x80) == 0)
                        break;
                }
                else
                {
                    result |= ((ulong)nextByte & 0xff) << 56;
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// Returns an array of UInt64 values from the stream.
        /// </summary>
        /// <returns>An array of UInt64 values.</returns>
        public UInt64[] ReadUInt64Array()
        {
            UInt64[] array = UInt64ArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns a double value from the stream.
        /// </summary>
        /// <returns>A double value.</returns>
        public double ReadDouble()
        {
            ulong bits = 0;
            int bitCount = 0;
            while (true)
            {
                byte nextByte = ReadByte();
                // Normally, we are reading 7 bits at a time.
                // But once we've read 8*7=56 bits, if we still
                // have more bits, there can at most be 8 bits
                // so we read all 8 bits for that last byte.
                if (bitCount < 56)
                {
                    bits = (bits << 7) | ((ulong)nextByte & 0x7f);
                    bitCount += 7;
                    if ((nextByte & 0x80) == 0)
                    {
                        bits <<= 64 - bitCount;
                        break;
                    }
                }
                else
                {
                    bits = (bits << 8) | ((ulong)nextByte & 0xff);
                    break;
                }
            }
            return BitConverter.Int64BitsToDouble((long)bits);
        }

        /// <summary>
        /// Returns an array of double values from the stream.
        /// </summary>
        /// <returns>An array of double values.</returns>
        public double[] ReadDoubleArray()
        {
            Double[] array = DoubleArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns a TimeSpan value from the stream.
        /// </summary>
        /// <returns>A double value.</returns>
        public TimeSpan ReadTimeSpan()
        {
            long ticks = unchecked((long)ReadUInt64());
            TimeSpan timeSpan = new TimeSpan(ticks);
            return timeSpan;
        }

        /// <summary>
        /// Returns an array of TimeSpan values from the stream.
        /// </summary>
        /// <returns>An array of TimeSpan values.</returns>
        public TimeSpan[] ReadTimeSpanArray()
        {
            TimeSpan[] array = TimeSpanArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns a DateTime value from the stream.
        /// </summary>
        /// <returns>A DateTime value.</returns>
        public DateTime ReadDateTime()
        {
            DateTimeOffset trueDateTime = ReadDateTimeOffset();
            return trueDateTime.DateTime;
        }

        /// <summary>
        /// Returns a DateTimeOffset value from the stream.
        /// </summary>
        /// <returns>A DateTimeOffset value.</returns>
        public DateTimeOffset ReadDateTimeOffset()
        {
            const long TicksPerSecond = 10 * 1000 * 1000; // 10,000,000
            const long TicksPer100ms = 1000 * 1000; // 1,000,000
            const long TicksPer16ms = 160000;
            const long TicksPer10ms = 100000;
            const long TicksPer1ms = 10000;
            const long TicksPer100us = 1000;
            const long TicksPer10us = 100;
            const long TicksPer1us = 10;

            DateTimeOffset timestamp;
            DateTimeEncoding encoding;
            Int64 deltaTicks;
            Int64 factor;
            int offsetMinutes = ReadInt32(); //the time zone offset the time was written in
            TimeSpan timeZoneOffset = new TimeSpan(0, offsetMinutes, 0);

            while (true)
            {
                encoding = (DateTimeEncoding)ReadByte();
                deltaTicks = (Int64)ReadUInt64(); // easier to just read the next value in one place

                switch (encoding)
                {
                    case DateTimeEncoding.RawTicks:
                        // timestamp by absolute ticks, but don't reset ReferenceTime
                        timestamp = new DateTimeOffset(deltaTicks, timeZoneOffset);
                        return timestamp; // We're done

                    case DateTimeEncoding.NewReference:
                        // timestamp by absolute ticks, and also set ReferenceTime
                        timestamp = new DateTimeOffset(deltaTicks, timeZoneOffset);
                        m_Strings.ReferenceTime = timestamp.DateTime;
                        return timestamp; // We're done

                    case DateTimeEncoding.SetReference:
                        // set ReferenceTime based on time value truncated to even seconds
                        deltaTicks *= TicksPerSecond; // Convert seconds into Ticks
                        m_Strings.ReferenceTime = new DateTimeOffset(deltaTicks, timeZoneOffset).DateTime;
                        continue; // another DateTime encoding follows, repeat the loop to process

                    case DateTimeEncoding.SetFactor:
                        factor = deltaTicks; // next value wasn't offsetTicks, but the new factor to set
                        m_Strings.GenericFactor = factor;
                        continue; // another DateTime encoding follows, repeat the loop to process
                }

                // Only repeat loop for SetReference and SetFactor cases
                break;
            }

            // At this point encoding must be DateTimeEncoding.LaterTicksNet or higher,
            // so the 1's bit indicates the sign of the offset.  Do the adjustment here.
            if (((uint)encoding & 0x01) != 0)
            {
                deltaTicks = -deltaTicks;
            }

            switch (encoding)
            {
                case DateTimeEncoding.RawTicks:
                case DateTimeEncoding.NewReference:
                case DateTimeEncoding.SetFactor:
                case DateTimeEncoding.SetReference:
                    // Can't happen
                    throw new ArgumentOutOfRangeException();

                case DateTimeEncoding.LaterTicksNet:
                case DateTimeEncoding.EarlierTicksNet:
                    factor = 1;
                    break;
                case DateTimeEncoding.LaterTicksFactor:
                case DateTimeEncoding.EarlierTicksFactor:
                    factor = m_Strings.GenericFactor;
                    break;
                case DateTimeEncoding.LaterTicks1s:
                case DateTimeEncoding.EarlierTicks1s:
                    factor = TicksPerSecond;
                    break;
                case DateTimeEncoding.LaterTicks100ms:
                case DateTimeEncoding.EarlierTicks100ms:
                    factor = TicksPer100ms;
                    break;
                case DateTimeEncoding.LaterTicks16ms:
                case DateTimeEncoding.EarlierTicks16ms:
                    factor = TicksPer16ms;
                    break;
                case DateTimeEncoding.LaterTicks10ms:
                case DateTimeEncoding.EarlierTicks10ms:
                    factor = TicksPer10ms;
                    break;
                case DateTimeEncoding.LaterTicks1ms:
                case DateTimeEncoding.EarlierTicks1ms:
                    factor = TicksPer1ms;
                    break;
                case DateTimeEncoding.LaterTicks100us:
                case DateTimeEncoding.EarlierTicks100us:
                    factor = TicksPer100us;
                    break;
                case DateTimeEncoding.LaterTicks10us:
                case DateTimeEncoding.EarlierTicks10us:
                    factor = TicksPer10us;
                    break;
                case DateTimeEncoding.LaterTicks1us:
                case DateTimeEncoding.EarlierTicks1us:
                    factor = TicksPer1us;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            deltaTicks *= factor; // adjust offset by determined factor

            //create a new timestamp by using our offset from reference ticks and our new time zone value.
            timestamp = new DateTimeOffset(m_Strings.ReferenceTime.Ticks + deltaTicks, timeZoneOffset);

            return timestamp;
        }

        /// <summary>
        /// Returns an array of DateTime values from the stream.
        /// </summary>
        /// <returns>An array of DateTime values.</returns>
        public DateTime[] ReadDateTimeArray()
        {
            DateTime[] array = DateTimeArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns an array of DateTimeOffset values from the stream.
        /// </summary>
        /// <returns>An array of DateTimeOffset values.</returns>
        public DateTimeOffset[] ReadDateTimeOffsetArray()
        {
            DateTimeOffset[] array = DateTimeOffsetArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns a Timestamp value from the stream (seconds only).
        /// </summary>
        /// <remarks>This is a place-holder for a new concept which is not yet implemented.</remarks>
        /// <returns>A DateTime value (with whole seconds only).</returns>
        public DateTime ReadTimestamp() // need to change return type to Timestamp if we make it?
        {
            DateTime timestamp;

            long offsetTicks = ReadInt64();
            if (offsetTicks == 0)
            {
                long referenceTicks = ReadInt64();

                if (referenceTicks == 0)
                {
                    //wait, this was REALLY just a zero offset.
                    timestamp = m_Strings.ReferenceTime;
                }
                else
                {
                    timestamp = m_Strings.ReferenceTime = DateTime.SpecifyKind(new DateTime(referenceTicks), DateTimeKind.Utc);
                }
            }
            else
            {
                timestamp = m_Strings.ReferenceTime.AddTicks(offsetTicks);
            }

            return timestamp;
        }

        /// <summary>
        /// Returns an array of Timestamp values from the stream.
        /// </summary>
        /// <remarks>This is a place-holder for a new concept which is not yet implemented.</remarks>
        /// <returns>An array of DateTime values.</returns>
        public DateTime[] ReadTimestampTimeArray()
        {
            DateTime[] array = DateTimeArrayReader.Read(this); // need to change this?
            return array;
        }

        /// <summary>
        /// Returns a Guid value from the stream.
        /// </summary>
        /// <returns>A Guid value.</returns>
        public Guid ReadGuid()
        {
            byte[] array = ReadBytes(16);
            Guid guid = new Guid(array);
            return guid;
        }

        /// <summary>
        /// Returns an array of Guid values from the stream.
        /// </summary>
        /// <returns>An array of Guid values.</returns>
        public Guid[] ReadGuidArray()
        {
            Guid[] array = GuidArrayReader.Read(this);
            return array;
        }

        /// <summary>
        /// Returns a field value from the stream.
        /// </summary>
        /// <returns>An object value holding a value (see FieldType.</returns>
        public object ReadField()
        {
            FieldType nextFieldType = ReadFieldType();
            return ReadField(nextFieldType);
        }

        /// <summary>
        /// Returns a field value from the stream for the provided field type
        /// </summary>
        /// <param name="fieldType">The field type of the next field in the stream to read</param>
        /// <returns>An object with the value that was read.</returns>
        public object ReadField(FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.Bool:
                    return ReadBool();
                case FieldType.BoolArray:
                    return ReadBoolArray();
                case FieldType.String:
                    return ReadString();
                case FieldType.StringArray:
                    return ReadStringArray();
                case FieldType.Int32:
                    return ReadInt32();
                case FieldType.Int32Array:
                    return ReadInt32Array();
                case FieldType.Int64:
                    return ReadInt64();
                case FieldType.Int64Array:
                    return ReadInt64Array();
                case FieldType.UInt32:
                    return ReadUInt32();
                case FieldType.UInt32Array:
                    return ReadUInt32Array();
                case FieldType.UInt64:
                    return ReadUInt64();
                case FieldType.UInt64Array:
                    return ReadUInt64Array();
                case FieldType.Double:
                    return ReadDouble();
                case FieldType.DoubleArray:
                    return ReadDoubleArray();
                case FieldType.TimeSpan:
                    return ReadTimeSpan();
                case FieldType.TimeSpanArray:
                    return ReadTimeSpanArray();
                case FieldType.DateTime:
                    return ReadDateTime();
                case FieldType.DateTimeArray:
                    return ReadDateTimeArray();
                case FieldType.Guid:
                    return ReadGuid();
                case FieldType.GuidArray:
                    return ReadGuidArray();
                case FieldType.DateTimeOffset:
                    return ReadDateTimeOffset();
                case FieldType.DateTimeOffsetArray:
                    return ReadDateTimeOffsetArray();
                default:
                    throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture,
                                                                 "There is no known field type for {0}, this most likely indicates a corrupt file or serialization defect.",
                                                                 fieldType));
            }
        }

        /// <summary>
        /// Returns an array of field values from the stream.
        /// </summary>
        /// <returns>An array of objects each holding a field value (see FieldType).</returns>
        public object[] ReadFieldArray()
        {
            int length = (int)ReadUInt32();
            object[] array = new object[length];
            for (int i = 0; i < length; i++)
                array[i] = ReadField();
            return array;
        }

        /// <summary>
        /// Returns a FieldType enum value from the stream.
        /// </summary>
        /// <returns>A FieldType enum value</returns>
        public FieldType ReadFieldType()
        {
            return (FieldType)ReadInt32();
        }

        /// <summary>
        /// Read an array of FieldType enum values from the stream.
        /// </summary>
        /// <returns>Returns an array of FieldType enum values</returns>
        public FieldType[] ReadFieldTypeArray()
        {
            int length = (int)ReadUInt32();
            FieldType[] array = new FieldType[length];
            for (int i = 0; i < length; i++)
                array[i] = ReadFieldType();
            return array;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Helper method to read a single byte from the underlying stream
        /// </summary>
        /// <remarks>
        /// NOTE: In DEBUG builds, this method will throw an exception if
        /// the a byte cannot be read (past end-of-file). Otherwise, it returns zero.
        /// </remarks>
        /// <returns>The next byte in the stream.</returns>
        private byte ReadByte()
        {
            int value = m_Stream.ReadByte();
            if (value < 0)
            {
                Exception exception = new IOException("Unable to read beyond the end of the stream.");
#if DEBUG
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif
                throw exception;
            }
            return (byte)value;
        }

        private byte[] ReadBytes(int length)
        {
            byte[] buffer = new byte[length];
            int bytesRead = m_Stream.Read(buffer, 0, length);
            if (bytesRead < length)
            {
                IOException exception = new IOException("Unable to read beyond the end of the stream.");
#if DEBUG
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif
                throw exception;
            }
            return buffer;
        }

        /// <summary>
        /// Helper method to read a string from the underlying stream.
        /// 
        /// NOTE: In DEBUG builds, this method will throw an exception if a
        /// valid string is not read completely.  Otherwise, it returns null.
        /// </summary>
        /// <returns>A string read with the expected encoding</returns>
        private string ReadStringDirect()
        {
            UInt32 length = ReadUInt32();

            // Handle the possibility of an empty string
            if (length == 0)
                return string.Empty;

            var bytes = new byte[length];
            int readBytes = m_Stream.Read(bytes, 0, (int)length);
            if (readBytes != length)
            {
                IOException exception = new IOException("Unable to read the complete string. "
                                                        + length + " bytes expected, " + readBytes +
                                                        " bytes actually read.");
#if DEBUG
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif
                throw exception;
            }

            // Handle the possibility of a null string under m_MajorVersion > 1
            if (length == 1 && bytes[0] == 0)
                return null;

            // The rest of this method handles a non-null, non-empty string to be interned in the StringReference table

            string value;
            try
            {
                value = m_Encoding.GetString(bytes);
            }
            catch (Exception exception)
            {
                GC.KeepAlive(exception);
#if DEBUG
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif
                throw;
            }

            // Make sure we return a unique reference to this string
            return StringReference.GetReference(value);
        }

        #endregion
    }
}
