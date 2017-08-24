using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Gibraltar.Serialization.Internal
{
    /// <summary>
    /// SimplePacketFactory is the IPacketFactory used when an IPacket
    /// implementation knows how to use when a type
    /// </summary>
    internal class SimplePacketFactory : IPacketFactory
    {
        private readonly ConstructorInfo m_Constructor;
        private readonly List<MethodInfo> m_ReadMethods;

        /// <summary>
        /// Creates an IPacketFactory wrappering a type that implements IPacket.
        /// </summary>
        /// <param name="type">The type must implement IPacket and provide a default constructor</param>
        public SimplePacketFactory(Type type)
        {
            if (!(typeof(IPacket).IsAssignableFrom(type)))
            {
                return;
            }

            // the type must provide a default constructor, but this constructor can be
            // private if it should not be called directly (other than during deserialization)
            m_Constructor = type.GetTypeInfo().DeclaredConstructors
                .FirstOrDefault(c => c.GetParameters().Any() == false); 
            if (m_Constructor == null)
            {
                return;
            }

            m_ReadMethods = new List<MethodInfo>();

            // walk down the hierarchy till we get to a base object that no longer implements IPacket
            //KM: It isn't clear to me why we bother doing this, I can't see where we ever use the information....
            while (typeof(IPacket).IsAssignableFrom(type))
            {
                // Even though the current type implements IPacket, it may not have a GetPacketDefinition at this level
                var typeInfo = type.GetTypeInfo();
                var method = typeInfo.GetDeclaredMethod("Gibraltar.Serialization.IPacket.ReadFields");
//               var method = typeInfo.GetMethod("ReadFields", new Type[] {typeof(PacketDefinition), typeof(IFieldReader)}); //original better version that checked arguments
                if (method != null) m_ReadMethods.Add(method);
                type = typeInfo.BaseType;
            }
        }

        /// <summary>
        /// This method is used by caller to detect if the constructor failed.
        /// This is necessary because we suppress exceptions in release builds.
        /// </summary>
        public bool IsValid
        {
            get { return m_Constructor != null && m_ReadMethods != null; }
        }

        #region IPacketFactory Members

        /// <summary>
        /// This is the method that is invoked on an IPacketFactory to create an IPacket
        /// from the data in an IFieldReader given a specified PacketDefinition.
        /// </summary>
        /// <param name="definition">Definition of the fields expected in the next packet</param>
        /// <param name="reader">Data stream to be read</param>
        /// <returns>An IPacket corresponding to the PacketDefinition and the stream data</returns>
        public IPacket CreatePacket(PacketDefinition definition, IFieldReader reader)
        {
            IPacket packet = (IPacket)m_Constructor.Invoke(new object[0]);
            definition.ReadFields(packet, reader);
            return packet;
        }

        #endregion
    }
}
