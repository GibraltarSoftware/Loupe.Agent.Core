using System;
using System.Diagnostics;
using System.IO;

namespace Loupe.Core.IO
{
    /// <summary>
    /// Generates and parses the binary header used at the start of a Loupe Log File
    /// </summary>
    public class FileHeader
    {
        /// <summary>
        /// The number of bytes used by the header
        /// </summary>
        public const int HeaderSize = 20;

        /// <summary>
        /// The unique sequence of bytes at the start of the header that identify a binary file as a GLF
        /// </summary>
        public const long GLFTypeCode = 0x79474c460d0a1a0a; //modeled after PNG, except "GLF" substituted for "PNG"

        /// <summary>
        /// Default value for serialization protocol major version
        /// </summary>
        /// <remarks>
        /// Normally, you'd expect this to be a constant.  However, for testing purposes
        /// it's convenient to be able to change it back and forth.
        /// </remarks>
        public static short DefaultMajorVersion = 2;

        /// <summary>
        /// Default value for serialization protocol minor version
        /// </summary>
        public static short DefaultMinorVersion = 2;

        private long m_TypeCode;
        private short m_MajorVersion;
        private short m_MinorVersion;
        private int m_DataOffset;
        private int m_DataChecksum;

        /// <summary>
        /// Create a new empty file header
        /// </summary>
        public FileHeader()
            : this(DefaultMajorVersion, DefaultMinorVersion)
        {
        }

        /// <summary>
        /// Create a new empty file header
        /// </summary>
        public FileHeader(int majorVersion, int minorVersion)
        {
            //set our defaults to proper GLF format
            m_TypeCode = GLFTypeCode;
            m_MajorVersion = (short)majorVersion;
            m_MinorVersion = (short)minorVersion;
        }
        /// <summary>
        /// Create a new header from the provided byte array
        /// </summary>
        /// <param name="data"></param>
        /// <remarks>The byte array must have at least as many bytes as indicated by the Header Size.</remarks>
        public FileHeader(byte[] data)
        {
            //we need the input data buffer to be the right size to interpret.
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length < HeaderSize)
            {
                throw new ArgumentException("The provided header buffer is too short to be a valid header.", nameof(data));
            }

            MemoryStream rawData = new MemoryStream(data);

            BinarySerializer.DeserializeValue(rawData, out m_TypeCode);

            BinarySerializer.DeserializeValue(rawData, out m_MajorVersion);
            
            BinarySerializer.DeserializeValue(rawData, out m_MinorVersion);
            
            BinarySerializer.DeserializeValue(rawData, out m_DataOffset);
            
            BinarySerializer.DeserializeValue(rawData, out m_DataChecksum);
        }

        /// <summary>
        /// Export the file header into a raw data array
        /// </summary>
        /// <returns></returns>
        public byte[] RawData()
        {
            byte[] rawData = new byte[HeaderSize];

            int curByteIndex = 0;

            byte[] curValue = BinarySerializer.SerializeValue(m_TypeCode);

            //we always nibble around so we can ensure big endian/little endian behavior.
            foreach (Byte curByte in curValue)
            {
                rawData[curByteIndex++] = curByte;
            }

            curValue = BinarySerializer.SerializeValue(m_MajorVersion);

            //we always nibble around so we can ensure big endian/little endian behavior.
            foreach (Byte curByte in curValue)
            {
                rawData[curByteIndex++] = curByte;
            }

            curValue = BinarySerializer.SerializeValue(m_MinorVersion);

            //we always nibble around so we can ensure big endian/little endian behavior.
            foreach (Byte curByte in curValue)
            {
                rawData[curByteIndex++] = curByte;
            }

            curValue = BinarySerializer.SerializeValue(m_DataOffset);

            //we always nibble around so we can ensure big endian/little endian behavior.
            foreach (Byte curByte in curValue)
            {
                rawData[curByteIndex++] = curByte;
            }

            curValue = BinarySerializer.SerializeValue(m_DataChecksum);

            //we always nibble around so we can ensure big endian/little endian behavior.
            foreach (Byte curByte in curValue)
            {
                rawData[curByteIndex++] = curByte;
            }

            //we should have exactly filled our header to size.
#if DEBUG
            Debug.Assert(curByteIndex == HeaderSize);
#endif
            return rawData;
        }

        /// <summary>
        /// The type code set in the file
        /// </summary>
        public long TypeCode { get { return m_TypeCode; } set { m_TypeCode = value; } }

        /// <summary>
        /// The major version of the file
        /// </summary>
        public short MajorVersion { get { return m_MajorVersion; } set { m_MajorVersion = value; } }

        /// <summary>
        /// The minor version of the file
        /// </summary>
        public short MinorVersion { get { return m_MinorVersion; } set { m_MinorVersion = value; } }

        /// <summary>
        /// The offset in the stream from the start of the file header to the start of the data section
        /// </summary>
        public int DataOffset { get { return m_DataOffset; } set { m_DataOffset = value; } }

        /// <summary>
        /// A checksum of the file header
        /// </summary>
        public int DataChecksum { get { return m_DataChecksum; } set { m_DataChecksum = value; } }

        /// <summary>
        /// True if the header is valid.  Always returns true.
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            return true; //yeah, until we implement checksums we won't do it        
        }

        /// <summary>
        /// Indicates if the supplied file version supports the Computer Id field.
        /// </summary>
        public static bool SupportsComputerId(int majorVersion, int minorVersion)
        {
            return ((majorVersion > 2) || ((majorVersion == 2) && (minorVersion > 0)));
        }

        /// <summary>
        /// Indicates if the supplied file version supports the Environment and Promotion fields.
        /// </summary>
        public static bool SupportsEnvironmentAndPromotion(int majorVersion, int minorVersion)
        {
            return (majorVersion > 1);
        }

        /// <summary>
        /// Indicates if the binary stream supports fragments or only single-stream transfer (the pre-3.0 format)
        /// </summary>
        public static bool SupportsFragments(int majorVersion, int minorVersion)
        {
            return (majorVersion > 1);
        }
    }
}
