
using System;
using System.Collections.Generic;
using System.IO;
using Loupe.Core.Data;
using Loupe.Core.IO;


#pragma warning disable 1591
namespace Loupe.Core.Serialization
{
    public class PacketWriter : IPacketWriter, IDisposable
    {
#if ADD_GUARD_BYTES
        internal static readonly byte[] s_PreambleGuardPattern = new byte[] { 0xAB, 0xAC, 0xAD, 0xAE };
        internal static readonly byte[] s_PostambleGuardPattern = new byte[] { 0xED, 0xEC, 0xEB, 0xEA };
#endif
        private readonly Stream m_Stream;
        private readonly IFieldWriter m_Writer;
        private readonly MemoryStream m_Buffer;
        private readonly IFieldWriter m_BufferWriter;
        private readonly PacketDefinitionList m_CachedTypes;
        private readonly PacketCache m_PacketCache;
        private bool m_WeAreDisposed;

        /// <summary>
        /// Initialize a PacketWriter to read the specified stream using
        /// the provided encoding for strings.
        /// </summary>
        /// <param name="stream">Destination for data written</param>
        public PacketWriter(Stream stream)
            : this(stream, FileHeader.DefaultMajorVersion, FileHeader.DefaultMinorVersion)
        {
        }

        /// <summary>
        /// Initialize a PacketWriter to read the specified stream using
        /// the provided encoding for strings.
        /// </summary>
        /// <param name="stream">Destination for data written</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        public PacketWriter(Stream stream, int majorVersion, int minorVersion)
        {
            m_Stream = stream;
            m_Writer = new FieldWriter(stream, new UniqueStringList(), majorVersion, minorVersion);
            m_Buffer = new MemoryStream();
            m_BufferWriter = new FieldWriter(m_Buffer, m_Writer.Strings, majorVersion, minorVersion);
            m_CachedTypes = new PacketDefinitionList();
            m_PacketCache = new PacketCache();
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
        /// Exposes the PacketCache
        /// </summary>
        public PacketCache PacketCache { get { return m_PacketCache; } }

        /// <summary>
        /// Write the data needed to serialize the state of the packet
        /// </summary>
        /// <param name="packet">Object to be serialized, must implement IPacket</param>
        public void Write(IPacket packet)
        {
            //Before we do anything - is this a cached packet that's already been written out?
            ICachedPacket cachedPacket = packet as ICachedPacket;
            if (cachedPacket != null)
            {
                //it is cacheable - is it in the cache?
                if (m_PacketCache.Contains(cachedPacket))
                {
                    //good to go, we're done.
                    return;
                }
            }

            //First, we need to find out if there are any packets this guy depends on.  If there are,
            //they have to be serialized out first.  They may have been - they could be cached.
            //to do this, we'll need to get the definition.
            PacketDefinition previewDefinition;
            int previewTypeIndex = m_CachedTypes.IndexOf(packet);
            if (previewTypeIndex < 0)
            {
                //we're going to get the definition, BUT we're not going to cache it yet.  
                //This is because we recurse on our self if there are required packets, and if one of those
                //packets is our same type, IT has to write out the definition so that it's on the stream
                //before the packet itself.
                previewDefinition = PacketDefinition.CreatePacketDefinition(packet);
            }
            else
            {
                previewDefinition = m_CachedTypes[previewTypeIndex];
            }

            Dictionary<IPacket, IPacket> requiredPackets = previewDefinition.GetRequiredPackets(packet);

            foreach (IPacket requiredPacket in requiredPackets.Values)
            {
                Write(requiredPacket); //this will handle if it's a cached packet and shouldn't be written out.
            }

            //Begin our "transactional" phase
            try
            {
                // This routine is written to either write a complete packet, or to write nothing.
                // As we build up the packet, we will write to a MemoryStream.  Only after we've
                // built up the complete packet will we write it to the actual stream.
                m_Buffer.SetLength(0);
                m_Buffer.Position = 0;

                // The first time a packet type is written, we send along a packet definition
                PacketDefinition definition;
                int typeIndex = m_CachedTypes.IndexOf(packet);
                if (typeIndex < 0)
                {
                    // Record that we've seen this type so we don't bother sending the PacketDefinition again
                    definition = PacketDefinition.CreatePacketDefinition(packet);
                    typeIndex = m_CachedTypes.Count;
                    m_CachedTypes.Add(definition);

                    // Each packet always starts with a packet type index.  And the first time a new
                    // index is used, it is followed by the packet definition.
                    m_BufferWriter.Write((UInt32)typeIndex);
                    definition.WriteDefinition(m_BufferWriter);
                }
                else
                {
                    // If this type has been written before, just send the type index
                    m_BufferWriter.Write((UInt32)typeIndex);
                    definition = m_CachedTypes[typeIndex];
                }

                // if it's cacheable then we need to add it to our packet cache before we write it out
                if (definition.IsCachable)
                {
                    // In the case of an ICachedPacket, we need to add it to the cache.
                    // we'd have already bailed if it was in there.
                    // Note: Use previous cast for efficiency, but we must recast here *if* packet gets reassigned above
                    // Currently it does not get reassigned, so cachedPacket which we cast packet into above is still valid.
                    m_PacketCache.AddOrGet(cachedPacket);
                }

                //Finally, and it really is a long journey, we ask the definition to write out
                //the individual fields for the packet
                definition.WriteFields(packet, m_BufferWriter);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                Rollback();
                throw;
            }

#if ADD_GUARD_BYTES
            m_Stream.Write(s_PreambleGuardPattern, 0, s_PreambleGuardPattern.Length);
#endif

            // Write the data to the stream preceded by the length of this packet
            // NOTE: The logic below is careful to ensure that the length and payload is written in one call
            //       This is necessary to ensure that the GZipStream writes the whole packet in edge cases 
            //       of writing the very last packet as an application is exiting.

            var payloadLength = (int)m_Buffer.Position; // get the actual length of the payload
            MemoryStream encodedLength = FieldWriter.WriteLength(payloadLength);
            var lengthLength = (int)encodedLength.Length;

            var packetBytes = new byte[lengthLength + payloadLength];

            encodedLength.Position = 0; // reset the position in preparation to read the data back
            encodedLength.Read(packetBytes, 0, lengthLength);

            m_Buffer.Position = 0; // reset the position in preparation to read the data back
            m_Buffer.Read(packetBytes, lengthLength, payloadLength);

            m_Stream.Write(packetBytes, 0, packetBytes.Length);

#if ADD_GUARD_BYTES
            m_Stream.Write(s_PostambleGuardPattern, 0, s_PostambleGuardPattern.Length);
#endif

            Commit();
        }

        private void Commit()
        {
            m_CachedTypes.Commit();
            m_Writer.Commit();
        }

        private void Rollback()
        {
            m_CachedTypes.Rollback();
            m_Writer.Rollback();
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

                    m_Buffer.Dispose(); // We always own our own MemoryStream, so we need to release it here
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here
            }
        }

        #endregion
    }
}
