using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Loupe.Data;
using Loupe.Serialization.Internal;

#pragma warning disable 1591
namespace Loupe.Serialization
{
    /// <summary>
    /// Reads a packet data stream, recreating the packets it contains
    /// </summary>
    public class PacketReader : IPacketReader, IDisposable
    {
        private readonly Stream m_Stream;
        private readonly bool m_InputIsReadOnly;
        private readonly FieldReader m_Reader;
        private readonly PacketDefinitionList m_cachedTypes;
        private readonly PacketFactory m_PacketFactory;
        private bool m_ReleaseStream; // indicate whether we need to release m_Stream upon Dispose()
        private bool m_WeAreDisposed;
        private long m_StreamLength; //if we are read only then we cache the stream length to avoid expensive file calls
        private readonly int m_MajorVersion;
        private readonly int m_MinorVersion;

        /// <summary>
        /// Initialize a PacketReader to read the specified stream using
        /// the provided encoding for strings.
        /// </summary>
        /// <param name="stream">Data to be read</param>
        /// <param name="inputIsReadOnly">Indicates if the input can be assumed fixed in length</param>
        public PacketReader(Stream stream, bool inputIsReadOnly)
            : this(stream, inputIsReadOnly, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
         {
         }

        /// <summary>
        /// Initialize a PacketReader to read the specified stream using
        /// the provided encoding for strings.
        /// </summary>
        /// <param name="stream">Data to be read</param>
        /// <param name="inputIsReadOnly">Indicates if the input can be assumed fixed in length</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        public PacketReader(Stream stream, bool inputIsReadOnly, int majorVersion, int minorVersion)
        {
            m_Stream = stream;
            m_InputIsReadOnly = inputIsReadOnly;
            m_MajorVersion = majorVersion;
            m_MinorVersion = minorVersion;

            // m_ReleaseStream = false; // m_Stream was passed in to us, don't release it upon Dispose()!
            // (false by default) If we were invoked from another constructor, they will overwrite m_ReleaseStream correctly
            m_Reader = new FieldReader(stream, new UniqueStringList(), majorVersion, minorVersion);
            m_cachedTypes = new PacketDefinitionList();
            m_PacketFactory = new PacketFactory();

            if ((inputIsReadOnly) && (m_Stream != null))
                m_StreamLength = m_Stream.Length;
        }

        /// <summary>
        /// Initialize a PacketReader to read the specified data using
        /// the default encoding for strings.
        /// </summary>
        /// <param name="data">Data to be read</param>
        public PacketReader(byte[] data)
            : this(new MemoryStream(data), true)
        {
            m_ReleaseStream = true; // we created a new MemoryStream to be m_Stream, so we must release it upon Dispose()
        }

        /// <summary>
        /// Indicates if there are any more packets available on the current stream.
        /// </summary>
        public bool DataAvailable
        {
            get
            {
                long streamLength = m_InputIsReadOnly ? m_StreamLength : m_Stream.Length;

#if ADD_GUARD_BYTES
                if (m_Stream.Position + 5 >= streamLength)
                    return false;

                long originalPosition = m_Stream.Position;
                m_Stream.Position = m_Stream.Position + 4; //go beyond the guard bytes
#else
                if (m_Stream.Position + 1 >= streamLength)
                    return false; // we're EOF so 
#endif

                try
                {
                    int packetSize = (int)m_Reader.PeekUInt64();

#if DEBUG
                    if ((packetSize == 0) && Debugger.IsAttached)
                        Debugger.Break();
#endif

#if ADD_GUARD_BYTES
                    //we have to allow for the post-amble
                    packetSize += 4;
#endif
                    //there is only data available if our stream is long enough to contain a whole packet
                    return ((streamLength - m_Stream.Position) >= packetSize);
                }
                catch (Exception)
                {
                    return false;
                }
#if ADD_GUARD_BYTES
                finally
                {
                    m_Stream.Position = originalPosition;
                }
#endif
            }
        }

        /// <summary>
        /// Returns the current position within the stream.
        /// </summary>
        public long Position { get { return m_Stream.Position; } }

        /// <summary>
        /// Returns the length of the stream.
        /// </summary>
        public long Length { get { return m_InputIsReadOnly ? m_StreamLength : m_Stream.Length; } }

        /// <summary>
        /// Read and return the next IPacket from the stream
        /// </summary>
        public IPacket Read()
        {
#if ADD_GUARD_BYTES
            byte[] preamble = new byte[ 4 ];
            m_Stream.Read(preamble, 0, 4);

            //now compare it against the preamble.
            CompareArrays(preamble, PacketWriter.s_PreambleGuardPattern);
#endif

            int packetSize = (int)m_Reader.ReadUInt64();

            //if the packet size is less than one, that's obviously wrong
            if (packetSize < 1)
            {
                throw new GibraltarSerializationException("The size of the next packet is smaller than 1 byte or negative, which can't be correct.  The packet stream is corrupted.", true);
            }

            // TODO: There's got to be a more efficient way to get this done
            byte[] buffer = new byte[packetSize];
            m_Stream.Read(buffer, 0, packetSize);
            IFieldReader bufferReader = new FieldReader(new MemoryStream(buffer), m_Reader.Strings, m_MajorVersion, m_MinorVersion);

            PacketDefinition definition;
            int typeIndex = (int)bufferReader.ReadUInt32();
            if (typeIndex >= m_cachedTypes.Count)
            {
                definition = PacketDefinition.ReadPacketDefinition(bufferReader);
                if (string.IsNullOrEmpty(definition.TypeName))
                {
                    //we're hosed...  we won't be able to parse this packet.
                    throw new GibraltarSerializationException("The type name of the definition is null, which can't be correct.  The packet stream is corrupted.", true);
                }

                m_cachedTypes.Add(definition);
                m_cachedTypes.Commit();
            }
            else
            {
                definition = m_cachedTypes[typeIndex];
            }

            IPacketFactory factory = m_PacketFactory.GetPacketFactory(definition.TypeName);
            IPacket packet = factory.CreatePacket(definition, bufferReader);

            //we used to populate a packet cache here, but a cached packet should be read just once - it shouldn't be in the stream
            //(and I changed PacketWriter to enforce that)

#if ADD_GUARD_BYTES
            byte[] postamble = new byte[4];
            m_Stream.Read(postamble, 0, 4);

            //now compare it against the preamble.
            CompareArrays(postamble, PacketWriter.s_PostambleGuardPattern);
#endif

            return packet;
        }

        /// <summary>
        /// Read and return the next IPacket from the stream
        /// </summary>
        public IPacket ReadPacket(Stream packetStream)
        {
            m_Reader.ReplaceStream(packetStream);

            PacketDefinition definition;
            int typeIndex = (int)m_Reader.ReadUInt32();
            if (typeIndex >= m_cachedTypes.Count)
            {
                definition = PacketDefinition.ReadPacketDefinition(m_Reader);
                if (string.IsNullOrEmpty(definition.TypeName))
                {
                    //we're hosed...  we won't be able to parse this packet.
                    throw new GibraltarSerializationException("The type name of the definition is null, which can't be correct.  The packet stream is corrupted.", true);
                }

                m_cachedTypes.Add(definition);
                m_cachedTypes.Commit();
            }
            else
            {
                definition = m_cachedTypes[typeIndex];
            }
            definition.PacketCount++;
            definition.PacketSize += packetStream.Length;

            IPacketFactory factory = m_PacketFactory.GetPacketFactory(definition.TypeName);
            IPacket packet = factory.CreatePacket(definition, m_Reader);

            return packet;
        }

        /// <summary>
        /// Returns a summary of packet count and size for each packet type
        /// </summary>
        /// <remarks>
        /// The returned list is sorted using the default sort implied by the
        /// PacketTypeStorageSummary.CompareTo method.
        /// </remarks>
        public List<PacketTypeStorageSummary> GetStorageSummary()
        {
            var summary = new List<PacketTypeStorageSummary>();
            foreach (PacketDefinition cachedType in m_cachedTypes)
            {
                var packetSummary = new PacketTypeStorageSummary(cachedType);
                summary.Add(packetSummary);
            }

            return summary;
        }

#if ADD_GUARD_BYTES
        private void CompareArrays(byte[] original, byte[] check)
        {
            for(int curIndex = 0; curIndex< original.Length; curIndex++)
            {
                if (original[curIndex] != check[curIndex])
                {
                    Debugger.Break();
                    throw new GibraltarSerializationException(string.Format("Guard bytes don't match: Expected {0:x} and got {1:x}", check[curIndex], original[curIndex]));
                }
            }
        }
#endif

        public void RegisterType(Type type)
        {
            m_PacketFactory.RegisterType(type);
        }

        public void RegisterFactory(string typeName, IPacketFactory factory)
        {
            m_PacketFactory.RegisterFactory(typeName, factory);
        }

        public void RegisterAssembly(Assembly assembly)
        {
            m_PacketFactory.RegisterAssembly(assembly);
        }

        #region IDisposable Members

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // and SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (!m_WeAreDisposed)
            {
                m_WeAreDisposed = true; // Only Dispose stuff once

                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case

                    if (m_ReleaseStream)
                    {
                        if (m_Stream != null) m_Stream.Dispose();
                        m_ReleaseStream = false;
                    }
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here
            }
        }

        #endregion
    }
}
