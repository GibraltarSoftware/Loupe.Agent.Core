using System;
using System.Collections.Generic;
using Loupe.Serialization;
using Loupe.Extensibility.Data;
#pragma warning disable 1591

namespace Loupe.Monitor.Serialization
{
    public class ApplicationUserPacket : GibraltarCachedPacket, IPacket, IEquatable<ApplicationUserPacket>, IApplicationUser
    {
        private string m_Key;
        private string m_FullyQualifiedUserName;
        private string m_Caption;
        private string m_Title;
        private string m_Organization;
        private string m_Role;
        private string m_Tenant;
        private string m_TimeZoneCode;
        private string m_EmailAddress;
        private string m_Phone;

        private readonly Dictionary<string, string> m_Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ApplicationUserPacket() 
            : base(false)
        {            
        }

        /// <summary>
        /// Optional. An absolute, unique key for the user to use as a primary match
        /// </summary>
        public string Key
        {
            get { return m_Key; }
            set { m_Key = value; }
        }

        /// <summary>
        /// The fully qualified user name, composed from the Domain and Name as originally specified
        /// </summary>
        public string FullyQualifiedUserName
        {
            get { return m_FullyQualifiedUserName; }
            set { m_FullyQualifiedUserName = value; }
        }

        /// <summary>
        /// A display label for the user (such as their full name)
        /// </summary>
        public string Caption
        {
            get { return m_Caption; }
            set { m_Caption = value; }
        }

        /// <summary>
        /// Optional.  A primary email address for the user
        /// </summary>
        public string EmailAddress
        {
            get { return m_EmailAddress; }
            set { m_EmailAddress = value; }
        }

        /// <summary>
        /// Optional.  A phone number or other telecommunication alias
        /// </summary>
        public string Phone
        {
            get { return m_Phone; }
            set { m_Phone = value; }
        }

        /// <summary>
        /// Optional.  A label for the organization this user is a part of
        /// </summary>
        public string Organization
        {
            get { return m_Organization; }
            set { m_Organization = value; }
        }

        /// <summary>
        /// Optional.  The primary time zone the user is associated with.
        /// </summary>
        public string TimeZoneCode
        {
            get { return m_TimeZoneCode; }
            set { m_TimeZoneCode = value; }
        }

        /// <summary>
        /// Optional.  A title to display for the user
        /// </summary>
        public string Title
        {
            get { return m_Title; }
            set { m_Title = value; }
        }

        /// <summary>
        /// Optional.  A primary role for this user with respect to this application
        /// </summary>
        public string Role
        {
            get { return m_Role; }
            set { m_Role = value; }
        }

        /// <summary>
        /// Optional.  The primary tenant this user is a part of.
        /// </summary>
        public string Tenant
        {
            get { return m_Tenant; }
            set { m_Tenant = value; }
        }

        /// <summary>
        /// Application provided properties 
        /// </summary>
        public Dictionary<string, string> Properties { get { return m_Properties; } }

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
            return Equals(other as ThreadInfoPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ApplicationUserPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            if (String.IsNullOrEmpty(Key) == false && Key.Equals(other.Key, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(FullyQualifiedUserName, other.FullyQualifiedUserName, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
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
            //we're being a bit more strict about hash code to match our equality compare exactly

            if (string.IsNullOrEmpty(Key) == false)
                return Key.GetHashCode();

            if (string.IsNullOrEmpty(FullyQualifiedUserName) == false) 
                return FullyQualifiedUserName.GetHashCode();

            return base.GetHashCode();
        }

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
            const string typeName = nameof(ApplicationUserPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            definition.Fields.Add("Key", FieldType.String);
            definition.Fields.Add("UserName", FieldType.String);
            definition.Fields.Add("Caption", FieldType.String);
            definition.Fields.Add("Title", FieldType.String);
            definition.Fields.Add("Organization", FieldType.String);
            definition.Fields.Add("Role", FieldType.String);
            definition.Fields.Add("Tenant", FieldType.String);
            definition.Fields.Add("TimeZoneCode", FieldType.String);
            definition.Fields.Add("EmailAddress", FieldType.String);
            definition.Fields.Add("Phone", FieldType.String);

            //serialize our name/value pairs as parallel arrays
            definition.Fields.Add("PropertyNames", FieldType.StringArray);
            definition.Fields.Add("PropertyValues", FieldType.StringArray);

            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            packet.SetField("Key", Key);
            packet.SetField("UserName", FullyQualifiedUserName);
            packet.SetField("Caption", Caption);
            packet.SetField("Title", Title);
            packet.SetField("Organization", Organization);
            packet.SetField("Role", Role);
            packet.SetField("Tenant", Tenant);
            packet.SetField("TimeZoneCode", TimeZoneCode);
            packet.SetField("EmailAddress", EmailAddress);
            packet.SetField("Phone", Phone);

            var propertyNames = new string[ m_Properties.Count ];
            var propertyValues = new string[ m_Properties.Count ];

            m_Properties.Keys.CopyTo(propertyNames, 0);
            packet.SetField("PropertyNames", propertyNames);

            m_Properties.Values.CopyTo(propertyValues, 0);
            packet.SetField("PropertyValues", propertyValues);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                    packet.GetField("Key", out m_Key);
                    packet.GetField("UserName", out m_FullyQualifiedUserName);
                    packet.GetField("Caption", out m_Caption);
                    packet.GetField("Title", out m_Title);
                    packet.GetField("Organization", out m_Organization);
                    packet.GetField("Role", out m_Role);
                    packet.GetField("Tenant", out m_Tenant);
                    packet.GetField("TimeZoneCode", out m_TimeZoneCode);
                    packet.GetField("EmailAddress", out m_EmailAddress);
                    packet.GetField("Phone", out m_Phone);

                    packet.GetField("PropertyNames", out string[] propertyNames);

                    packet.GetField("PropertyValues", out string[] propertyValues);

                    for (int index = 0; index < propertyNames.Length; index++)
                    {
                        var propertyName = propertyNames[index];
                        var propertyValue = propertyValues[index];
                        m_Properties.Add(propertyName, propertyValue);
                    }

                    break;
            }
        }

        #endregion

    }
}
