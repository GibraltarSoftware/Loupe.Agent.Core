
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;



#pragma warning disable 1591
namespace Loupe.Core.Serialization
{
    /// <summary>
    /// Holds the metadata needed to correctly interpret the stream of fields associated with a serialized packet
    /// </summary>
    public sealed class PacketDefinition : IEnumerable<FieldDefinition>, IEquatable<PacketDefinition>
    {
        #region Static members

        /// <summary>
        /// Create a PacketDefinition describing the fields and serialization version information for the
        /// IPacket object passed by the caller.
        /// </summary>
        /// <param name="packet">IPacket object to generate a PacketDefinition for</param>
        /// <returns>PacketDefinition describing fields to be serialized, including nested types</returns>
        public static PacketDefinition CreatePacketDefinition(IPacket packet)
        {
            // These flags are used in the GetMethod call below.  The key flag is DeclaredOnly
            // which is needed because we want to support object hierarchies that implement
            // IPacket at multiple levels.  We accomplish this by called GetPacketDefinition
            // at each level in the hierarchy that implements IPacket.
            const BindingFlags flags = BindingFlags.DeclaredOnly |
                                       BindingFlags.Instance |
                                       BindingFlags.InvokeMethod |
                                       BindingFlags.NonPublic;

            // We iterate from the type we are passed down object hierarchy looking for
            // IPacket implementations.  Then, on the way back up, we link together
            // the m_BasePacket fields to that the PacketDefinition we return includes
            // a description of all the nested types.
            Stack<PacketDefinition> stack = new Stack<PacketDefinition>();
            Type type = packet.GetType();

            // walk down the hierarchy till we get to a base object that no longer implements IPacket
            while (typeof(IPacket).IsAssignableFrom(type))
            {
                // We push one PacketDefinition on the stack for each level in the hierarchy
                PacketDefinition definition;

                // Even though the current type implements IPacket, it may not have a GetPacketDefinition at this level
                MethodInfo method = GetIPacketMethod(type, "GetPacketDefinition", flags, Type.EmptyTypes);
                if (method != null)
                {
                    definition = (PacketDefinition)method.Invoke(packet, new object[0]);
                    definition.m_WriteMethod = GetIPacketMethod(type, "WriteFields", flags, new Type[] { typeof(PacketDefinition), typeof(SerializedPacket) });
                    if (definition.m_WriteMethod == null)
                    {
                        throw new LoupeSerializationException("The current packet implements part but not all of the IPacket interface.  No Write Method could be found.  Did you implement IPacket explicitly?");
                    }

                    if (definition.CanHaveRequiredPackets)
                    {
                        definition.m_GetRequiredPacketsMethod = GetIPacketMethod(type, "GetRequiredPackets", flags, Type.EmptyTypes);
                        if (definition.m_GetRequiredPacketsMethod == null)
                        {
                            throw new LoupeSerializationException("The current packet implements part but not all of the IPacket interface.  No GetRequiredPackets Method could be found.  Did you implement IPacket explicitly?");
                        }
                    }
                }
                else
                {
                    // If GetPacketDefinition isn't defined at this level,
                    // push an empty PacketDefinition on the stack as a placeholder
                    definition = new PacketDefinition(type.Name, -1, false);
                }

                // Push the PacketDefinition for this level on the stack
                // then iterate down to the next deeper level in the object hierarchy
                stack.Push(definition);
                type = type.GetTypeInfo().BaseType;
            }

            // At this point the top of the stack contains the mostly deeply nested base type.
            // While there are 2 or more elements on the stack, the deeper of the two
            // should reference the top element as a base type
            while (stack.Count >= 2)
            {
                // Pop off the deepest base type still in the stack
                PacketDefinition basePacket = stack.Pop();

                // The next element is now visible, so let's peek at it
                PacketDefinition derivedPacket = stack.Peek();

                // link the base type with its derived class
                derivedPacket.m_BasePacket = basePacket;
            }

            // At this point there should be exactly one element in the stack
            // which contains the return value for this method.
            PacketDefinition packetDefinition = stack.Pop();

            // Check if this is a DynamicPacket.  If so, it should have a unique dynamic type.
            // If the DynamicType field has not been assigned, assign a unique string.
            IDynamicPacket dynamicPacket = packet as IDynamicPacket;
            if (dynamicPacket != null)
            {
                if (dynamicPacket.DynamicTypeName == null)
                    dynamicPacket.DynamicTypeName = Guid.NewGuid().ToString();
                packetDefinition.m_DynamicTypeName = dynamicPacket.DynamicTypeName;
            }

            // Record whether or not this is a cachable packet.
            packetDefinition.m_IsCachable = packet is ICachedPacket;

            return packetDefinition;
        }

        /// <summary>
        /// Returns a PacketDefinition from the stream (including nested PacketDefinition
        /// objects for cases in which an IPacket is subclassed and has serialized state
        /// at multiple levels).
        /// </summary>
        /// <param name="reader">Stream to read data from</param>
        /// <returns>PacketDefinition (including nested definitions for subclassed packets)</returns>
        public static PacketDefinition ReadPacketDefinition(IFieldReader reader)
        {
            bool cachedPacket = reader.ReadBool();
            int nestingDepth = reader.ReadInt32();
            if (nestingDepth < 1)
            {
                throw new LoupeException(string.Format(CultureInfo.InvariantCulture, "While reading the definition of the next packet, the number of types in the definition was read as {0} which is less than 1.", nestingDepth));
            }


            string dynamicTypeName = reader.ReadString();
            PacketDefinition[] definitions = new PacketDefinition[nestingDepth];
            for (int i = 0; i < nestingDepth; i++)
            {
                definitions[i] = new PacketDefinition(reader);
                if (i > 0)
                    definitions[i].m_BasePacket = definitions[i - 1];
            }

            PacketDefinition topLevelDefinition = definitions[nestingDepth - 1];
            topLevelDefinition.m_IsCachable = cachedPacket;
            topLevelDefinition.m_DynamicTypeName = dynamicTypeName;
            return topLevelDefinition;
        }

        #endregion

        private bool m_CanHaveRequiredPackets;
        private bool m_IsCachable;
        private readonly string m_TypeName;
        private readonly int m_Version;
        private string m_DynamicTypeName;
        private readonly FieldDefinitionCollection m_Fields = new FieldDefinitionCollection();
        private PacketDefinition m_BasePacket;
        private MethodInfo m_WriteMethod;
        private MethodInfo m_ReadMethod;
        private MethodInfo m_GetRequiredPacketsMethod;
        private bool m_ReadMethodAssigned;
        private readonly List<PacketDefinition> m_SubPackets;
        public int PacketCount { get; set; }
        public long PacketSize { get; set; }

        private PacketDefinition(IFieldReader reader)
        {
            m_TypeName = reader.ReadString();
            m_Version = reader.ReadInt32();
            int fieldCount = reader.ReadInt32();
            for (int i = 0; i < fieldCount; i++)
            {
                string fieldName = reader.ReadString();
                FieldType fieldType = (FieldType)reader.ReadInt32();
                m_Fields.Add(new FieldDefinition(fieldName, fieldType));
            }

            // Handle the possiblity that a Packet aggregates lower level packets
            int subPacketCount = reader.ReadInt32();
            m_SubPackets = new List<PacketDefinition>();
            for ( int i = 0; i < subPacketCount; i++)
            {
                // We need to call the static ReadPacketDefinition(reader) in order to
                // read and process the cacheable, version, and dynamic name fields which
                // also exist for each subPacket definition.  Fixed as part of Case #165
                PacketDefinition subPacket = ReadPacketDefinition(reader);
                m_SubPackets.Add(subPacket);
            }
        }

        public PacketDefinition(string typeName, int version, bool canHaveRequiredPackets)
        {
            m_TypeName = typeName;
            m_Version = version;
            m_BasePacket = null;
            m_CanHaveRequiredPackets = canHaveRequiredPackets;
            m_SubPackets = new List<PacketDefinition>();
        }

        #region Public Properties and Methods

        /// <summary>
        /// Indicates if this level of the definition can have required packets.
        /// </summary>
        public bool CanHaveRequiredPackets { get { return m_CanHaveRequiredPackets; } }

        public bool IsCachable { get { return m_IsCachable; } }

        public static bool IsSerializeableType(Type candidateType)
        {
            return TryGetSerializableType(candidateType, out var throwAwayMapping);
        }

        public string TypeName { get { return m_TypeName; } }

        public int Version { get { return m_Version; } }

        public string DynamicTypeName { get { return m_DynamicTypeName; } }

        public string QualifiedTypeName
        {
            get
            {
                if (m_DynamicTypeName == null)
                    return m_TypeName;
                else
                    return m_TypeName + "+" + m_DynamicTypeName;
            }
        }

        public FieldDefinitionCollection Fields { get { return m_Fields; } }

            /// <summary>
        /// This list allows for the possiblity of a Packet that aggregates other sub-packets
        /// </summary>
        public List<PacketDefinition> SubPackets { get { return m_SubPackets; } }

        public void WriteDefinition(IFieldWriter writer)
        {
            writer.Write(m_IsCachable);
            int nestingDepth = GetNestingDepth();
            writer.Write(nestingDepth);
            writer.Write(m_DynamicTypeName);
            WriteDefinitionForThisLevel(writer);
        }

        #endregion

        internal int GetNestingDepth()
        {
            if (m_BasePacket == null)
                return 1;
            else
                return 1 + m_BasePacket.GetNestingDepth();
        }

        internal PacketDefinition BasePacket { get { return m_BasePacket; } }

        private void WriteDefinitionForThisLevel(IFieldWriter writer)
        {
            if (m_BasePacket != null)
                m_BasePacket.WriteDefinitionForThisLevel(writer);

            writer.Write(m_TypeName);
            writer.Write(m_Version);
            writer.Write(m_Fields.Count);

            foreach (FieldDefinition fieldDefinition in m_Fields)
            {
                writer.Write(fieldDefinition.Name);
                writer.Write((int)fieldDefinition.FieldType);
            }

            // Writer out any associated sub-packets
            writer.Write(m_SubPackets.Count);
            for( int i = 0; i < m_SubPackets.Count; i++)
                m_SubPackets[i].WriteDefinition(writer);
        }

        /// <summary>
        /// Get the lossless equivalent type for serialization
        /// </summary>
        /// <param name="type">A .NET type to serialize</param>
        /// <returns>The Field Type that will provide lossless serialization</returns>
        /// <remarks>If no lossless type is found, an exception will be thrown.</remarks>
        public static FieldType GetSerializableType(Type type)
        {
            if (TryGetSerializableType(type, out var bestType) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(type), type.FullName, "The provided type isn't supported for lossless serialization.");
            }

            return bestType;
        }

        /// <summary>
        /// Get the lossless equivalent type for serialization
        /// </summary>
        /// <param name="type">A .NET type to serialize</param>
        /// <param name="bestType">The optimal field type for the provided .NET type, or 0 if none can be determined.</param>
        /// <returns>A boolean indicating that no matching type was found.</returns>
        /// <remarks>No exception is raised if no matching type can be found, instead the best type is set to zero (invalid)
        /// and false is returned.</remarks>
        public static bool TryGetSerializableType(Type type, out FieldType bestType)
        {
            bestType = 0; //0 is bad...

            if (type == typeof(DateTime))
            {
                bestType = FieldType.DateTime;
            }
            else if (type == typeof(DateTime[]))
            {
                bestType = FieldType.DateTimeArray;
            }
            else if (type == typeof(DateTimeOffset))
            {
                bestType = FieldType.DateTimeOffset;
            }
            else if (type == typeof(DateTimeOffset[]))
            {
                bestType = FieldType.DateTimeOffsetArray;
            }
            else if (type == typeof(TimeSpan))
            {
                bestType = FieldType.TimeSpan;
            }
            else if (type == typeof(TimeSpan[]))
            {
                bestType = FieldType.TimeSpanArray;
            }
            else if (type == typeof(String))
            {
                bestType = FieldType.String;
            }
            else if (type == typeof(String[]))
            {
                bestType = FieldType.StringArray;
            }
            else if (type == typeof(Int64))
            {
                bestType = FieldType.Int64;
            }
            else if (type == typeof(Int64[]))
            {
                bestType = FieldType.Int64Array;
            }
            else if (type == typeof(UInt64))
            {
                bestType = FieldType.UInt64;
            }
            else if (type == typeof(UInt64[]))
            {
                bestType = FieldType.UInt64Array;
            }
            else if (type == typeof(Int32))
            {
                bestType = FieldType.Int32;
            }
            else if (type == typeof(Int32[]))
            {
                bestType = FieldType.Int32Array;
            }
            else if (type == typeof(UInt32))
            {
                bestType = FieldType.UInt32;
            }
            else if (type == typeof(UInt32[]))
            {
                bestType = FieldType.UInt32Array;
            }
            else if (type == typeof(Int16))
            {
                bestType = FieldType.Int32;
            }
            else if (type == typeof(Int16[]))
            {
                bestType = FieldType.Int32Array;
            }
            else if (type == typeof(UInt16))
            {
                bestType = FieldType.UInt32;
            }
            else if (type == typeof(UInt16[]))
            {
                bestType = FieldType.UInt32Array;
            }
            else if (type == typeof(Double))
            {
                bestType = FieldType.Double;
            }
            else if (type == typeof(Double[]))
            {
                bestType = FieldType.DoubleArray;
            }
            else if (type == typeof(Decimal)) // Note: Does this cast to a double without loss?
            {
                bestType = FieldType.Double;
            }
            else if (type == typeof(Decimal[]))
            {
                bestType = FieldType.DoubleArray;
            }
            else if (type == typeof(Single))
            {
                bestType = FieldType.Double;
            }
            else if (type == typeof(Single[]))
            {
                bestType = FieldType.DoubleArray;
            }
            else if (type == typeof(Boolean))
            {
                bestType = FieldType.Bool;
            }
            else if (type == typeof(Boolean[]))
            {
                bestType = FieldType.BoolArray;
            }
            else if (type == typeof(Guid))
            {
                bestType = FieldType.Guid;
            }
            else if (type == typeof(Guid[]))
            {
                bestType = FieldType.GuidArray;
            }
            else
            {
                var baseType = type.GetTypeInfo().BaseType;
                if (baseType == typeof(Enum))
                {
                    Type underlyingType = Enum.GetUnderlyingType(type);
                    return TryGetSerializableType(underlyingType, out bestType);
                }
                else if (baseType == typeof(Enum[]))
                {
                    Type underlyingType = Enum.GetUnderlyingType(type);
                    return TryGetSerializableType(underlyingType, out bestType);
                }
            }

            if (bestType == 0)
            {
                //we didn't find a matching type
                return false;
            }

            return true;
        }

        /// <summary>
        /// Request the packet object write out all of its fields.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="writer"></param>
        internal void WriteFields(IPacket packet, IFieldWriter writer)
        {
            if (packet is GenericPacket)
            {
                //TODO: Update generic packet handling
                //packet.WriteFields(writer);
            }
            else
            {
                //We need all of our base classes to write out before us
                if (m_BasePacket != null)
                    m_BasePacket.WriteFields(packet, writer);

                //and finally write out our information, if we have any.
                if (m_WriteMethod != null && m_Fields.Count > 0)
                {
                    //we need to create a serialized packet for this new level so the write method can store information
                    SerializedPacket serializedPacket = new SerializedPacket(this);
                    m_WriteMethod.Invoke(packet, new object[] {this, serializedPacket});
                    
                    //and now write out the fields to serialization.
                    for(int curFieldIndex = 0; curFieldIndex < Fields.Count; curFieldIndex++)
                    {
                        FieldDefinition fieldDefinition = Fields[curFieldIndex];
                        writer.Write(serializedPacket.Values[curFieldIndex], fieldDefinition.FieldType);
                    }
                }
            }
        }

        public Dictionary<IPacket, IPacket> GetRequiredPackets(IPacket packet)
        {
            Dictionary<IPacket, IPacket> requiredPackets;

            //go up the stack first to get the required packets of our children.
            if (m_BasePacket != null)
            {
                requiredPackets = m_BasePacket.GetRequiredPackets(packet);
            }
            else
            {
                //we're the base - create the dictionary all of our children will use for their required packets
                requiredPackets = new Dictionary<IPacket, IPacket>();
            }

            if (m_GetRequiredPacketsMethod != null)
            {
                IPacket[] currentRequiredPackets = (IPacket[])m_GetRequiredPacketsMethod.Invoke(packet, new object[0]);

                //I just don't trust user provided code.  I mean - that guy, who knows what he might do! :)
                if ((currentRequiredPackets != null) && (currentRequiredPackets.Length > 0))
                {
                    //there should be at least one in there!
                    foreach (IPacket requiredPacket in currentRequiredPackets)
                    {
                        if (requiredPacket == null)
                        {
                            //we can't process this - but insetad of up and failing, lets write out  a debug warn
                            Debug.WriteLine("A required packet was specified, but was null.  This shouldn't happen.");
                        }
                        else
                        {
                            //add it to our overall dictionary of packets.
                            if (requiredPackets.ContainsKey(requiredPacket) == false)
                            {
                                requiredPackets.Add(requiredPacket, requiredPacket);
                            }
                        }
                    }
                }
            }

            return requiredPackets;
        }

        public void ReadFields(IPacket packet, IFieldReader reader)
        {
            IDynamicPacket dynamicPacket = packet as IDynamicPacket;
            if (dynamicPacket != null)
                dynamicPacket.DynamicTypeName = m_DynamicTypeName;
            ReadFields(packet.GetType(), packet, reader);
        }

        private void ReadFields(Type type, IPacket packet, IFieldReader reader)
        {
            Exception basePacketException = null;
            if (m_BasePacket != null)
            {
                try
                {
                    m_BasePacket.ReadFields(type.GetTypeInfo().BaseType, packet, reader);
                }
                catch (Exception ex)
                {
                    basePacketException = ex; // Remember this to wrap it in a new exception.
                }
            }

            if (!m_ReadMethodAssigned)
            {
                const BindingFlags flags = BindingFlags.DeclaredOnly |
                                           BindingFlags.Instance |
                                           BindingFlags.InvokeMethod |
                                           BindingFlags.NonPublic;

                if (typeof(IPacket).IsAssignableFrom(type))
                {
                    // Even though the current type implements IPacket, it may not have a ReadFields at this level
                    m_ReadMethod = GetIPacketMethod(type, "ReadFields", flags, new Type[] {typeof(PacketDefinition), typeof(SerializedPacket)});
                }

                m_ReadMethodAssigned = true;
            }

            Exception firstException = null;
            FieldType firstFailedFieldType = FieldType.Unknown;
            string firstFailedFieldName = null;
            if (m_ReadMethod != null)
            {
                //we need to read back everything the definition says should be there into an array and then pass that 
                //to the object for handling.
                object[] values = new object[Fields.Count];

                for (int curFieldIndex = 0; curFieldIndex < Fields.Count; curFieldIndex++)
                {
                    FieldDefinition fieldDefinition = Fields[curFieldIndex];
                    try
                    {
                        values[curFieldIndex] = reader.ReadField(fieldDefinition.FieldType);
                    }
                    catch (Exception ex)
                    {
                        if (basePacketException == null && firstException == null)
                        {
                            firstException = ex; // Only record the first one encountered in this packet.
                            firstFailedFieldType = fieldDefinition.FieldType;
                            firstFailedFieldName = fieldDefinition.Name;
                        }
                    }
                }

                // Now check for exceptions we may have encountered.  We had to finish reading each field of each packet level
                // in order to keep the stream in sync, but now we have to throw a wrapping exception if there was an error.
                string message;
                if (basePacketException != null) // This happened earlier, so it takes precedence over field exceptions.
                {
                    message = string.Format("Error reading base {0} of a {1}", m_BasePacket.QualifiedTypeName, QualifiedTypeName);
                    throw new LoupeSerializationException(message, basePacketException);
                }
                if (firstException != null) // Otherwise we can report our first exception from reading fields.
                {
                    message = string.Format("Error reading ({0}) field \"{1}\" in a {2}",
                                            firstFailedFieldType, firstFailedFieldName, QualifiedTypeName);
                    throw new LoupeSerializationException(message, firstException);
                }

                SerializedPacket serializedPacket = new SerializedPacket(this, values);
                m_ReadMethod.Invoke(packet, new object[] { this, serializedPacket });
            }
        }

        internal static MethodInfo GetIPacketMethod(Type type, string methodName, BindingFlags flags, Type[] methodArgTypes)
        {
            var requestedMethod = type.GetTypeInfo().DeclaredMethods.FirstOrDefault(m => m.Name.Equals("Loupe.Serialization.IPacket." + methodName, StringComparison.Ordinal));
            //            requestedMethod = type.GetMethod("Loupe.Serialization.IPacket." + methodName, flags, null, methodArgTypes, null); //original

            return requestedMethod;
        } 

        #region IEnumerable<FieldDefinition> Members

        IEnumerator<FieldDefinition> IEnumerable<FieldDefinition>.GetEnumerator()
        {
            return m_Fields.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable<FieldDefinition>)this).GetEnumerator();
        }

        #endregion

        /// <summary>
        /// Compare this PacketDefinition to another to verify that they are equivalent
        /// for purposes of order-dependant field deserialization.
        /// </summary>
        public bool Equals(PacketDefinition other)
        {
            if (other == null) return false;

            // Verify that base packets are equivalent
            if (BasePacket == null)
            {
                if (other.BasePacket != null)
                    return false;
            }
            else
            {
                if (other.BasePacket == null)
                    return false;
                if (!BasePacket.Equals(other.BasePacket))
                    return false;
            }

            // Verify that basic characteristics are equivalent
            if (TypeName != other.TypeName)
                return false;
            if (Version != other.Version)
                return false;
            if (Fields.Count != other.Fields.Count)
                return false;

            // Verify that all fields are equivalent
            for (int i = 0; i < Fields.Count; i++)
            {
                if (Fields[i].Name != other.Fields[i].Name)
                    return false;
                if (Fields[i].FieldType != other.Fields[i].FieldType)
                    return false;
            }

            return true;
        }
    }
}
