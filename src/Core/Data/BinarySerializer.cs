using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;



namespace Loupe.Data
{
    /// <summary>
    /// Provides basic binary serialization for platform independent simple serialization
    /// </summary>
    public static class BinarySerializer
    {
        private static readonly Encoding s_Encoding = new UTF8Encoding();
        private static readonly bool s_MonoRuntime;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static BinarySerializer()
        {
            Type monoRuntime = Type.GetType("Mono.Runtime"); // Detect if we're running under Mono runtime.
            if (monoRuntime == null)
            {
                s_MonoRuntime = false; // Not Mono, we can use the built-in "o" ISO format for DTO serialization.
            }
            else
            {
                s_MonoRuntime = true; // It's Mono, we need to do our own parsing of DTO serialization.
            }
        }

        /// <summary>
        /// Write the host value to the stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue"></param>
        public static void SerializeValue(Stream stream, bool hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Serialize a boolean value to a byte array with a single byte
        /// </summary>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        public static byte[] SerializeValue(bool hostValue)
        {
            if (hostValue)
            {
                return new byte[] {0x1};
            }
            else
            {
                return new byte[] {0x0};
            }
        }

        /// <summary>
        /// Serialize a GUID to a stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        public static void SerializeValue(Stream stream, Guid hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Serialize a GUID to a 16 byte array
        /// </summary>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        public static byte[] SerializeValue(Guid hostValue)
        {
            return hostValue.ToByteArray();            
        }

        /// <summary>
        /// Serialize a string to a byte array
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue"></param>
        /// <remarks>Serializes the length in the first byte then each character with one byte character encoding</remarks>
        public static void SerializeValue(Stream stream, string hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Serialize a string to a byte array
        /// </summary>
        /// <param name="hostValue"></param>
        /// <returns>The byte array for the string</returns>
        /// <remarks>Serializes the length in the first byte then each character with one byte character encoding</remarks>
        public static byte[] SerializeValue(string hostValue)
        {
            //short circuit handling for nullstring
            if (hostValue == null)
            {
                return SerializeValue((int)-1); //-1 magic value for null length string in 4 byte encoding
            }
            
            if  (string.IsNullOrEmpty(hostValue))
            {
                return SerializeValue((int)0); //zero length string in 4 byte encoding
            }

            //Get the UTF-8 encoded string
            byte[] rawValue = s_Encoding.GetBytes(hostValue);

            //but return it with the length as the first byte.
            byte[] networkValue = new byte[4 + rawValue.Length];
            Array.Copy(SerializeValue(rawValue.Length), networkValue, 4);
            Array.Copy(rawValue, 0, networkValue, 4, rawValue.Length);
            return networkValue;
        }

        /// <summary>
        /// Serialize a date time to a stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        /// <remarks>Uses the date time offset encoding with the local time zone.</remarks>
        public static void SerializeValue(Stream stream, DateTime hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Serialize a date time to a byte array
        /// </summary>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        /// <remarks>Uses the date time offset encoding with the local time zone.</remarks>
        public static byte[] SerializeValue(DateTime hostValue)
        {
            //The constructor for DateTimeOffset will assume that a DateTIme of kind Local = current time zone offset and figure it out.
            return SerializeValue(new DateTimeOffset(hostValue));
        }

        /// <summary>
        /// Serialize a date time and offset
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        /// <remarks>Encodes the date time offset as a string in ISO 8601 standard formatting</remarks>
        public static void SerializeValue(Stream stream, DateTimeOffset hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Serialize a date time and offset to a byte array.
        /// </summary>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        /// <remarks>Encodes the date time offset as a string in ISO 8601 standard formatting</remarks>
        public static byte[] SerializeValue(DateTimeOffset hostValue)
        {
            string standardTime = hostValue.ToString("o", CultureInfo.InvariantCulture); //this is the ISO 8601 Standard format for date to string.
            return SerializeValue(standardTime);
        }

        /// <summary>
        /// Serialize a timespan to a byte array
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        public static void SerializeValue(Stream stream, TimeSpan hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue.Ticks);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Serialize a timespan to a byte array
        /// </summary>
        /// <param name="hostValue"></param>
        /// <returns></returns>
        public static byte[] SerializeValue(TimeSpan hostValue)
        {
            return SerializeValue(hostValue.Ticks);
        }

        /// <summary>
        /// Write the host value to the provided stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static void SerializeValue(Stream stream, long hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Create a network-byte-order array of the host value
        /// </summary>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static byte[] SerializeValue(long hostValue)
        {
            long networkValue = IPAddress.HostToNetworkOrder(hostValue);
            byte[] rawValue = BitConverter.GetBytes(networkValue);

            return rawValue;
        }

#pragma warning disable 3001
        /// <summary>
        /// Create a network-byte-order array of the host value
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static void SerializeValue(Stream stream, ulong hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Create a network-byte-order array of the host value
        /// </summary>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static byte[] SerializeValue(ulong hostValue)
        {
            long networkValue = IPAddress.HostToNetworkOrder((long)hostValue);
            byte[] rawValue = BitConverter.GetBytes(networkValue);

            return rawValue;
        }
#pragma warning restore 3001

        /// <summary>
        /// Write the host value to the stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static void SerializeValue(Stream stream, int hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Create a network-byte-order array of the host value
        /// </summary>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static byte[] SerializeValue(int hostValue)
        {
            int networkValue = IPAddress.HostToNetworkOrder(hostValue);
            byte[] rawValue = BitConverter.GetBytes(networkValue);

            return rawValue;
        }

#pragma warning disable 3001
        /// <summary>
        /// Write the host value to the stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static void SerializeValue(Stream stream, uint hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Create a network-byte-order array of the host value
        /// </summary>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static byte[] SerializeValue(uint hostValue)
        {
            int networkValue = IPAddress.HostToNetworkOrder((int)hostValue);
            byte[] rawValue = BitConverter.GetBytes(networkValue);

            return rawValue;
        }
#pragma warning restore 3001

        /// <summary>
        /// Write the host value to the stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue">The host value to be serialized</param>
        public static void SerializeValue(Stream stream, short hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Create a network-byte-order array of the host value
        /// </summary>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static byte[] SerializeValue(short hostValue)
        {
            short networkValue = IPAddress.HostToNetworkOrder(hostValue);
            byte[] rawValue = BitConverter.GetBytes(networkValue);

            return rawValue;
        }

#pragma warning disable 3001
        /// <summary>
        /// Write the host value to the stream
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="hostValue">The host value to be serialized</param>
        public static void SerializeValue(Stream stream, ushort hostValue)
        {
            byte[] rawValue = SerializeValue(hostValue);
            stream.Write(rawValue, 0, rawValue.Length);
        }

        /// <summary>
        /// Create a network-byte-order array of the host value
        /// </summary>
        /// <param name="hostValue">The host value to be serialized</param>
        /// <returns>A byte array of each byte of the value in network byte order</returns>
        public static byte[] SerializeValue(ushort hostValue)
        {
            short networkValue = IPAddress.HostToNetworkOrder((short)hostValue);
            byte[] rawValue = BitConverter.GetBytes(networkValue);

            return rawValue;
        }
#pragma warning restore 3001

        /// <summary>
        /// Convert a network byte order array to host value
        /// </summary>
        /// <param name="networkBytes">Network order byte array of the long</param>
        /// <param name="hostValue">The converted long from the network byte array</param>
        /// <returns></returns>
        public static void DeserializeValue(Stream networkBytes, out long hostValue)
        {
            byte[] curValue = new byte[8];
            networkBytes.Read(curValue, 0, curValue.Length);
            long networkValue = BitConverter.ToInt64(curValue, 0);
            hostValue = IPAddress.NetworkToHostOrder(networkValue);
        }

#pragma warning disable 3001
        /// <summary>
        /// Convert a network byte order array to host value
        /// </summary>
        /// <param name="networkBytes">Network order byte array of the long</param>
        /// <param name="hostValue">The converted long from the network byte array</param>
        /// <returns></returns>
        public static void DeserializeValue(Stream networkBytes, out ulong hostValue)
        {
            byte[] curValue = new byte[8];
            networkBytes.Read(curValue, 0, curValue.Length);
            long networkValue = BitConverter.ToInt64(curValue, 0);
            hostValue = (ulong)IPAddress.NetworkToHostOrder(networkValue);
        }
#pragma warning restore 3001

        /// <summary>
        /// Convert a network byte order array to host value
        /// </summary>
        /// <param name="networkBytes">Network order byte array of the long</param>
        /// <param name="hostValue">The converted long from the network byte array</param>
        /// <returns></returns>
        public static void DeserializeValue(Stream networkBytes, out int hostValue)
        {
            byte[] curValue = new byte[4];
            networkBytes.Read(curValue, 0, curValue.Length);
            int networkValue = BitConverter.ToInt32(curValue, 0);
            hostValue = IPAddress.NetworkToHostOrder(networkValue);
        }

#pragma warning disable 3001
        /// <summary>
        /// Convert a network byte order array to host value
        /// </summary>
        /// <param name="networkBytes">Network order byte array of the long</param>
        /// <param name="hostValue">The converted long from the network byte array</param>
        /// <returns></returns>
        public static void DeserializeValue(Stream networkBytes, out uint hostValue)
        {
            byte[] curValue = new byte[4];
            networkBytes.Read(curValue, 0, curValue.Length);
            int networkValue = BitConverter.ToInt32(curValue, 0);
            hostValue = (uint)IPAddress.NetworkToHostOrder(networkValue);
        }
#pragma warning restore 3001

        /// <summary>
        /// Convert a network byte order array to host value
        /// </summary>
        /// <param name="networkBytes">Network order byte array of the long</param>
        /// <param name="hostValue">The converted long from the network byte array</param>
        /// <returns></returns>
        public static void DeserializeValue(Stream networkBytes, out short hostValue)
        {
            byte[] curValue = new byte[2];
            networkBytes.Read(curValue, 0, curValue.Length);
            short networkValue = BitConverter.ToInt16(curValue, 0);
            hostValue = IPAddress.NetworkToHostOrder(networkValue);
        }

#pragma warning disable 3001
        /// <summary>
        /// Convert a network byte order array to host value
        /// </summary>
        /// <param name="networkBytes">Network order byte array of the long</param>
        /// <param name="hostValue">The converted long from the network byte array</param>
        /// <returns></returns>
        public static void DeserializeValue(Stream networkBytes, out ushort hostValue)
        {
            byte[] curValue = new byte[2];
            networkBytes.Read(curValue, 0, curValue.Length);
            short networkValue = BitConverter.ToInt16(curValue, 0);
            hostValue = (ushort)IPAddress.NetworkToHostOrder(networkValue);
        }
#pragma warning restore 3001

        /// <summary>
        /// Deserialize a timespan value from the provided stream
        /// </summary>
        /// <param name="networkBytes"></param>
        /// <param name="hostValue"></param>
        public static void DeserializeValue(Stream networkBytes, out TimeSpan hostValue)
        {
            DeserializeValue(networkBytes, out long rawTicks);
            hostValue = TimeSpan.FromTicks(rawTicks);
        }

        /// <summary>
        /// Deserialize a date and time value from the provided stream
        /// </summary>
        /// <param name="networkBytes"></param>
        /// <param name="hostValue"></param>
        public static void DeserializeValue(Stream networkBytes, out DateTime hostValue)
        {
            DeserializeValue(networkBytes, out DateTimeOffset serializedValue);
            hostValue = serializedValue.DateTime;
        }

        /// <summary>
        /// Deserialize a date time offset value from the provided stream
        /// </summary>
        /// <param name="networkBytes"></param>
        /// <param name="hostValue"></param>
        public static void DeserializeValue(Stream networkBytes, out DateTimeOffset hostValue)
        {
            DeserializeValue(networkBytes, out string serializedValue);
            if (s_MonoRuntime == false)
            {
                hostValue = DateTimeOffset.ParseExact(serializedValue, "o", null);
            }
            else
            {
                // Mono doesn't support the parsing we need!  See if we can fake it:  Parse each value explicitly.
                // 0         1         2         3   
                // 0123456789012345678901234567890123
                // 2010-04-22T19:20:11.1336570+00:00
                if (serializedValue.Length != 33 || serializedValue[4] != '-' || serializedValue[7] != '-' ||
                    serializedValue[10] != 'T' || serializedValue[13] != ':' || serializedValue[16] != ':' ||
                    serializedValue[19] != '.' || serializedValue[30] != ':')
                    throw new FormatException(string.Format("Unrecognized format for DateTimeOffset deserialization: \"{0}\"",
                                                            serializedValue));

                string yearString = serializedValue.Substring(0, 4);
                int year = int.Parse(yearString, NumberStyles.None);
                string monthString = serializedValue.Substring(5, 2);
                int month = int.Parse(monthString, NumberStyles.None);
                string dayString = serializedValue.Substring(8, 2);
                int day = int.Parse(dayString, NumberStyles.None);
                string hourString = serializedValue.Substring(11, 2);
                int hour = int.Parse(hourString, NumberStyles.None);
                string minuteString = serializedValue.Substring(14, 2);
                int minute = int.Parse(minuteString, NumberStyles.None);
                string secondString = serializedValue.Substring(17, 2);
                int second = int.Parse(secondString, NumberStyles.None);
                string ticksString = serializedValue.Substring(20, 7);
                int ticks = int.Parse(ticksString, NumberStyles.None); // Only 7 digits can't exceed an int.

                string timeZoneHourString = serializedValue.Substring(28, 2);
                int timeZoneHour = int.Parse(timeZoneHourString, NumberStyles.None);
                string timeZoneMinuteString = serializedValue.Substring(31, 2);
                int timeZoneMinute = int.Parse(timeZoneMinuteString, NumberStyles.None);
                TimeSpan timeZoneOffset = new TimeSpan(timeZoneHour, timeZoneMinute, 0);
                char timeZoneOffsetSign = serializedValue[27];
                if (timeZoneOffsetSign == '-')
                {
                    timeZoneOffset = timeZoneOffset.Negate();
                }
                else if (timeZoneOffsetSign != '+')
                {
                    throw new FormatException(string.Format("Unrecognized character for time zone offset sign: '{0}'",
                                                            timeZoneOffsetSign));
                }

                hostValue = new DateTimeOffset(year, month, day, hour, minute, second, timeZoneOffset).AddTicks(ticks);
            }
        }

        /// <summary>
        /// Deserialize a GUID value from the provided stream
        /// </summary>
        /// <param name="networkBytes"></param>
        /// <param name="hostValue"></param>
        public static void DeserializeValue(Stream networkBytes, out Guid hostValue)
        {
            byte[] curValue = new byte[16];
            networkBytes.Read(curValue, 0, curValue.Length);
            hostValue = new Guid(curValue);
        }

        /// <summary>
        /// Deserialize a boolean value from the provided stream
        /// </summary>
        /// <param name="networkBytes"></param>
        /// <param name="hostValue"></param>
        public static void DeserializeValue(Stream networkBytes, out bool hostValue)
        {
            int curValue = networkBytes.ReadByte();

            if (curValue == 0)
            {
                hostValue = false;
            }
            else
            {
                hostValue = true;
            }
        }

        /// <summary>
        /// Deserialize a string value from the provided stream
        /// </summary>
        /// <param name="networkBytes"></param>
        /// <param name="hostValue"></param>
        public static void DeserializeValue(Stream networkBytes, out String hostValue)
        {
            //first get the length of the string
            DeserializeValue(networkBytes, out int length);

            //now get the string, based on that length.
            if (length > 0)
            {
                byte[] curValue = new byte[length];
                networkBytes.Read(curValue, 0, curValue.Length);

                hostValue = s_Encoding.GetString(curValue);
            }
            else if (length == 0)
            {
                hostValue = string.Empty;
            }
            else
            {
                hostValue = null;
            }
        }

        /// <summary>
        /// Calculate a CRC for the provided byte array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns>A 4 byte CRC value created by calculating an MD5 hash of the provided byte array</returns>
        public static byte[] CalculateCRC(byte[] data, int length)
        {
            if (length < data.Length)
            {
                byte[] shortData = new byte[length];
                Array.Copy(data, shortData, length);
                data = shortData;
            }

            //to be FIPS compliant we have to not use the Crypto-API version of MD5.
            byte[] crc = MD5Core.GetHash(data);

            //This is a big bogus - I'm just going to take the first four bytes.  We really should
            //go find the true CRC32 algorithm
            return new byte[4]{crc[0], crc[1], crc[2], crc[3]};
        }
    }
}