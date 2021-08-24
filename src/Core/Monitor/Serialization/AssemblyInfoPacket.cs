using System;
using System.Reflection;
using Gibraltar.Monitor.Serialization;
using Gibraltar.Serialization;

namespace Gibraltar.Monitor.Serialization
{
    internal class AssemblyInfoPacket : GibraltarCachedPacket, IPacket, IEquatable<AssemblyInfoPacket>
    {
        private string m_CultureName;
        private string m_FullName;
        private string m_ImageRuntimeVersion;
        private bool m_GlobalAssemblyCache;
        private string m_Location;
        private string m_Name;
        private ProcessorArchitecture m_ProcessorArchitecture;
        private string m_Version;
        private string m_FileVersion;

        public AssemblyInfoPacket()
            : base(true)
        {
        }

        #region Public Properties and Methods

        public string CultureName { get => m_CultureName;
            set => m_CultureName = value;
        }

        public string FullName { get => m_FullName;
            set => m_FullName = value;
        }

        public string ImageRuntimeVersion { get => m_ImageRuntimeVersion;
            set => m_ImageRuntimeVersion = value;
        }

        public string Location { get => m_Location;
            set => m_Location = value;
        }

        public string Name { get => m_Name;
            set => m_Name = value;
        }

        public ProcessorArchitecture ProcessorArchitecture { get => m_ProcessorArchitecture;
            set => m_ProcessorArchitecture = value;
        }

        public bool GlobalAssemblyCache { get => m_GlobalAssemblyCache;
            set => m_GlobalAssemblyCache = value;
        }

        public string Version { get => m_Version;
            set => m_Version = value;
        }

        public string FileVersion { get => m_FileVersion;
            set => m_FileVersion = value;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public override bool Equals(object other)
        {
            //use our type-specific override
            return Equals(other as AssemblyInfoPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(AssemblyInfoPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((CultureName == other.CultureName)
                && (FullName == other.FullName)
                && (ImageRuntimeVersion == other.ImageRuntimeVersion)
                && (GlobalAssemblyCache == other.GlobalAssemblyCache)
                && (Location == other.Location)
                && (Name == other.Name)
                && (ProcessorArchitecture == other.ProcessorArchitecture)
                && (Version == other.Version)
                && (FileVersion == other.FileVersion)
                && base.Equals(other));
        }

        /// <summary>
        /// Provides a representative hash code for objects of this type to spread out distribution
        /// in hash tables.
        /// </summary>
        /// <remarks>Objects which consider themselves to be Equal (a.Equals(b) returns true) are
        /// expected to have the same hash code.  Objects which are not Equal may have the same
        /// hash code, but minimizing such overlaps helps with efficient operation of hash tables.
        /// </remarks>
        /// <returns>
        /// an int representing the hash code calculated for the contents of this object
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = base.GetHashCode(); // Fold in hash code for inherited base type

            myHash ^= m_FullName.GetHashCode();

            return myHash;
        }

        #endregion

        #region IPacket Implementation

        private const int SerializationVersion = 1;

        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            //we have no dependencies.  Things depend on US!
            return null;
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            string typeName = MethodBase.GetCurrentMethod().DeclaringType.Name;
            PacketDefinition definition = new PacketDefinition(typeName, SerializationVersion, false);
            definition.Fields.Add("FullName", FieldType.String);
            definition.Fields.Add("Name", FieldType.String);
            definition.Fields.Add("Version", FieldType.String);
            definition.Fields.Add("CultureName", FieldType.String);
            definition.Fields.Add("ProcessorArchitecture", FieldType.Int32);
            definition.Fields.Add("GlobalAssemblyCache", FieldType.Bool);
            definition.Fields.Add("Location", FieldType.String);
            definition.Fields.Add("FileVersion", FieldType.String);
            definition.Fields.Add("ImageRuntimeVersion", FieldType.String);
            return definition;
        }


        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("FullName", m_FullName);
            packet.SetField("Name", m_Name);
            packet.SetField("Version", m_Version);
            packet.SetField("CultureName", m_CultureName);
            packet.SetField("ProcessorArchitecture", m_ProcessorArchitecture);
            packet.SetField("GlobalAssemblyCache", m_GlobalAssemblyCache);
            packet.SetField("Location", m_Location);
            packet.SetField("FileVersion", m_FileVersion);
            packet.SetField("ImageRuntimeVersion", m_ImageRuntimeVersion);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("FullName", out m_FullName);
                    packet.GetField("Name", out m_Name);
                    packet.GetField("Version", out m_Version);
                    packet.GetField("CultureName", out m_CultureName);

                    packet.GetField("ProcessorArchitecture", out int processorArchitectureRaw);
                    m_ProcessorArchitecture = (ProcessorArchitecture)processorArchitectureRaw;

                    packet.GetField("GlobalAssemblyCache", out m_GlobalAssemblyCache);
                    packet.GetField("Location", out m_Location);
                    packet.GetField("FileVersion", out m_FileVersion);
                    packet.GetField("ImageRuntimeVersion", out m_ImageRuntimeVersion);
                    break;
            }
        }

        #endregion

    }
}
