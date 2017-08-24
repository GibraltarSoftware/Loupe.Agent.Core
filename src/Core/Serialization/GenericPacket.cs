using System;
using System.Diagnostics;

#pragma warning disable 1591

namespace Gibraltar.Serialization
{
    /// <summary>
    /// This is the class returned by PacketReader when an unknown packet type
    /// is read from the input stream.  </summary>
    /// <remarks>This class is designed to allow the
    /// underlying data to be serialized back out just as it was read.  This
    /// handles the use case of an old 
    /// </remarks>
    public sealed class GenericPacket : IPacket
    {
        private readonly PacketDefinition m_Definition;
        private readonly object[] m_FieldValues;
        private readonly GenericPacket m_BasePacket;

        /// <summary>
        /// Read any packet based solely on its PacketDefinition
        /// </summary>
        /// <param name="definition">PacketDefinition describing the next packet in the stream</param>
        /// <param name="reader">Data stream to be read</param>
        public GenericPacket(PacketDefinition definition, IFieldReader reader)
        {
            if ( definition.BasePacket != null )
            {
                m_BasePacket = new GenericPacket(definition.BasePacket, reader);
            }

            m_Definition = definition;
            m_FieldValues = new object[definition.Fields.Count];

            for (int index = 0; index < definition.Fields.Count; index++)
            {
                switch (definition.Fields[index].FieldType)
                {
                    case FieldType.Bool:
                        m_FieldValues[index] = reader.ReadBool();
                        break;
                    case FieldType.BoolArray:
                        m_FieldValues[index] = reader.ReadBoolArray();
                        break;
                    case FieldType.String:
                        m_FieldValues[index] = reader.ReadString();
                        break;
                    case FieldType.StringArray:
                        m_FieldValues[index] = reader.ReadStringArray();
                        break;
                    case FieldType.Int32:
                        m_FieldValues[index] = reader.ReadInt32();
                        break;
                    case FieldType.Int32Array:
                        m_FieldValues[index] = reader.ReadInt32Array();
                        break;
                    case FieldType.Int64:
                        m_FieldValues[index] = reader.ReadInt64();
                        break;
                    case FieldType.Int64Array:
                        m_FieldValues[index] = reader.ReadInt64Array();
                        break;
                    case FieldType.UInt32:
                        m_FieldValues[index] = reader.ReadUInt32();
                        break;
                    case FieldType.UInt32Array:
                        m_FieldValues[index] = reader.ReadUInt32Array();
                        break;
                    case FieldType.UInt64:
                        m_FieldValues[index] = reader.ReadUInt64();
                        break;
                    case FieldType.UInt64Array:
                        m_FieldValues[index] = reader.ReadUInt64Array();
                        break;
                    case FieldType.Double:
                        m_FieldValues[index] = reader.ReadDouble();
                        break;
                    case FieldType.DoubleArray:
                        m_FieldValues[index] = reader.ReadDoubleArray();
                        break;
                    case FieldType.TimeSpan:
                        m_FieldValues[index] = reader.ReadTimeSpan();
                        break;
                    case FieldType.TimeSpanArray:
                        m_FieldValues[index] = reader.ReadTimeSpanArray();
                        break;
                    case FieldType.DateTime:
                        m_FieldValues[index] = reader.ReadDateTime();
                        break;
                    case FieldType.DateTimeArray:
                        m_FieldValues[index] = reader.ReadDateTimeArray();
                        break;
                    case FieldType.Guid:
                        m_FieldValues[index] = reader.ReadGuid();
                        break;
                    case FieldType.GuidArray:
                        m_FieldValues[index] = reader.ReadGuidArray();
                        break;
                    case FieldType.DateTimeOffset:
                        m_FieldValues[index] = reader.ReadDateTimeOffset();
                        break;
                    case FieldType.DateTimeOffsetArray:
                        m_FieldValues[index] = reader.ReadDateTimeOffsetArray();
                        break;
                    default:
#if DEBUG
                        if (Debugger.IsAttached)
                            Debugger.Break();
#endif
                        throw new InvalidOperationException(string.Format("The field type {0} is unknown so we can't deserialize the packet ", definition.Fields[index].FieldType));
                }
            }
        }

        public int Version { get { return m_Definition.Version; } }

        public int FieldCount { get { return m_Definition.Fields.Count; } }

        public int IndexOf(string fieldName)
        {
            return m_Definition.Fields.IndexOf(fieldName);
        }

        public string GetFieldName(int index)
        {
            return m_Definition.Fields[index].Name;
        }

        public FieldType GetFieldType(int index)
        {
            return m_Definition.Fields[index].FieldType;
        }

        public object GetFieldValue(int index)
        {
            return m_FieldValues[index];
        }

        #region IPacket Members


        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            //the majority of packets have no dependencies
            return null;
        }

        /// <summary>
        /// The key idea of a GenericPacket is that it allows an unknown packet type to be read and rewritten
        /// such that it can subsequently be read properly when the appropriate IPacketFactory is registered.
        /// </summary>
        /// <returns>The original PacketDefinition read from the input stream</returns>
        PacketDefinition IPacket.GetPacketDefinition()
        {
            return m_Definition;
        }

        /// <summary>
        /// Write out all of the fields for the current packet
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to populate with data</param>
        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            if (m_BasePacket != null)
                ((IPacket)m_BasePacket).WriteFields(definition, packet);

            for (int index = 0; index < m_Definition.Fields.Count; index++)
            {
                FieldDefinition fieldDefinition = m_Definition.Fields[index];
                switch (fieldDefinition.FieldType)
                {
                    case FieldType.Bool:
                        packet.SetField(fieldDefinition.Name, (bool)m_FieldValues[index]);
                        break;
                    case FieldType.BoolArray:
                        packet.SetField(fieldDefinition.Name, (bool[])m_FieldValues[index]);
                        break;
                    case FieldType.String:
                        packet.SetField(fieldDefinition.Name, (string)m_FieldValues[index]);
                        break;
                    case FieldType.StringArray:
                        packet.SetField(fieldDefinition.Name, (string[])m_FieldValues[index]);
                        break;
                    case FieldType.Int32:
                        packet.SetField(fieldDefinition.Name, (Int32)m_FieldValues[index]);
                        break;
                    case FieldType.Int32Array:
                        packet.SetField(fieldDefinition.Name, (Int32[])m_FieldValues[index]);
                        break;
                    case FieldType.Int64:
                        packet.SetField(fieldDefinition.Name, (Int64)m_FieldValues[index]);
                        break;
                    case FieldType.Int64Array:
                        packet.SetField(fieldDefinition.Name, (Int64[])m_FieldValues[index]);
                        break;
                    case FieldType.UInt32:
                        packet.SetField(fieldDefinition.Name, (UInt32)m_FieldValues[index]);
                        break;
                    case FieldType.UInt32Array:
                        packet.SetField(fieldDefinition.Name, (UInt32[])m_FieldValues[index]);
                        break;
                    case FieldType.UInt64:
                        packet.SetField(fieldDefinition.Name, (UInt64)m_FieldValues[index]);
                        break;
                    case FieldType.UInt64Array:
                        packet.SetField(fieldDefinition.Name, (UInt64[])m_FieldValues[index]);
                        break;
                    case FieldType.Double:
                        packet.SetField(fieldDefinition.Name, (double)m_FieldValues[index]);
                        break;
                    case FieldType.DoubleArray:
                        packet.SetField(fieldDefinition.Name, (double[])m_FieldValues[index]);
                        break;
                    case FieldType.TimeSpan:
                        packet.SetField(fieldDefinition.Name, (TimeSpan)m_FieldValues[index]);
                        break;
                    case FieldType.TimeSpanArray:
                        packet.SetField(fieldDefinition.Name, (TimeSpan[])m_FieldValues[index]);
                        break;
                    case FieldType.DateTime:
                        packet.SetField(fieldDefinition.Name, (DateTime)m_FieldValues[index]);
                        break;
                    case FieldType.DateTimeArray:
                        packet.SetField(fieldDefinition.Name, (DateTime[])m_FieldValues[index]);
                        break;
                    case FieldType.Guid:
                        packet.SetField(fieldDefinition.Name, (Guid)m_FieldValues[index]);
                        break;
                    case FieldType.GuidArray:
                        packet.SetField(fieldDefinition.Name, (Guid[])m_FieldValues[index]);
                        break;
                    case FieldType.DateTimeOffset:
                        packet.SetField(fieldDefinition.Name, (DateTimeOffset)m_FieldValues[index]);
                        break;
                    case FieldType.DateTimeOffsetArray:
                        packet.SetField(fieldDefinition.Name, (DateTimeOffset[])m_FieldValues[index]);
                        break;
                    default:
#if DEBUG
                        if (Debugger.IsAttached)
                            Debugger.Break();
#endif
                        throw new InvalidOperationException(string.Format("The field type {0} is unknown so we can't serialize the packet ", definition.Fields[index].FieldType));
                }
            }
        }

        /// <summary>
        /// Read back the field values for the current packet.
        /// </summary>
        /// <param name="definition">The definition that was used to persist the packet.</param>
        /// <param name="packet">The serialized packet to read data from</param>
        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}