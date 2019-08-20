using System;



#pragma warning disable 1591
namespace Loupe.Core.Serialization
{
    public class SerializedPacket
    {
        private readonly PacketDefinition m_Definition;
        private readonly object[] m_Values;
        private readonly bool m_ReadOnly;

        /// <summary>
        /// Create a new serialized packet for serialization
        /// </summary>
        /// <param name="definition"></param>
        internal SerializedPacket(PacketDefinition definition)
        {
            m_Definition = definition;
            m_Values = new object[definition.Fields.Count];
            m_ReadOnly = false;
        }

        /// <summary>
        /// Create a new serailized packet for deserialization
        /// </summary>
        /// <param name="definition"></param>
        /// <param name="values"></param>
        internal SerializedPacket(PacketDefinition definition, object[] values)
        {
            m_Definition = definition;
            m_Values = values;
            m_ReadOnly = true;
        }

        public void GetField(string fieldName, out string value)
        {
            value = (string)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out string value)
        {
            value = (string) m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out string[] value)
        {
            value = (string[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out string[] value)
        {
            value = (string[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out double value)
        {
            value = (double)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out double value)
        {
            value = (double)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out double[] value)
        {
            value = (double[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out double[] value)
        {
            value = (double[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out bool value)
        {
            value = (bool)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out bool value)
        {
            value = (bool)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out bool[] value)
        {
            value = (bool[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out bool[] value)
        {
            value = (bool[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out Int32 value)
        {
            value = (Int32)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out Int32 value)
        {
            value = (Int32)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out Int32[] value)
        {
            value = (Int32[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out Int32[] value)
        {
            value = (Int32[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out UInt32 value)
        {
            value = (UInt32)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out UInt32 value)
        {
            value = (UInt32)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out UInt32[] value)
        {
            value = (UInt32[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out UInt32[] value)
        {
            value = (UInt32[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out Int64 value)
        {
            value = (Int64)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out Int64 value)
        {
            value = (Int64)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out Int64[] value)
        {
            value = (Int64[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out Int64[] value)
        {
            value = (Int64[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out UInt64 value)
        {
            value = (UInt64)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out UInt64 value)
        {
            value = (UInt64)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out UInt64[] value)
        {
            value = (UInt64[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out UInt64[] value)
        {
            value = (UInt64[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out TimeSpan value)
        {
            value = (TimeSpan)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out TimeSpan value)
        {
            value = (TimeSpan)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out TimeSpan[] value)
        {
            value = (TimeSpan[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out TimeSpan[] value)
        {
            value = (TimeSpan[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out DateTime value)
        {
            value = (DateTime)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out DateTime value)
        {
            value = (DateTime)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out DateTime[] value)
        {
            value = (DateTime[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out DateTime[] value)
        {
            value = (DateTime[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out DateTimeOffset value)
        {
            value = (DateTimeOffset)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out DateTimeOffset value)
        {
            value = (DateTimeOffset)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out DateTimeOffset[] value)
        {
            value = (DateTimeOffset[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out DateTimeOffset[] value)
        {
            value = (DateTimeOffset[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out Guid value)
        {
            value = (Guid)GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out Guid value)
        {
            value = (Guid)m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out Guid[] value)
        {
            value = (Guid[])GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out Guid[] value)
        {
            value = (Guid[])m_Values[fieldIndex];
        }

        public void GetField(string fieldName, out object value)
        {
            value = GetFieldValue(fieldName);
        }

        public void GetField(int fieldIndex, out object value)
        {
            value = m_Values[fieldIndex];
        }

        public void SetField(string fieldName, object value)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            if (m_ReadOnly)
            {
                throw new InvalidOperationException("The packet has been deserialized and is read-only");
            }

            //find the field index for the field name
            FieldDefinition fieldDefinition = m_Definition.Fields[fieldName];
            int fieldIndex = m_Definition.Fields.IndexOf(fieldDefinition);

            //now we need to check the value for sanity.
            if (value != null)
            {
                FieldType valueType = PacketDefinition.GetSerializableType(value.GetType());
                if (fieldDefinition.IsCompatible(valueType) == false)
                {
                    throw new ArgumentException("The provided value's type doesn't match the definition for the field.", nameof(value));
                }                
            }
            
            //now that we know we're of the right type, store it away.
            m_Values[fieldIndex] = value;
        }

        internal object [] Values
        {
            get { return m_Values; }
        }

        internal object GetFieldValue(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            //find the field index from the field name.
            int fieldIndex = m_Definition.Fields.IndexOf(fieldName);

            return m_Values[fieldIndex];            
        }
    }
}
