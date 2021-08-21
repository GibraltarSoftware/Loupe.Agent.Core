using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Gibraltar.Data;
using Gibraltar.Serialization;
using Loupe.Extensibility.Data;
#pragma warning disable 1591

namespace Gibraltar.Monitor.Serialization
{
    [DebuggerDisplay("{Caption} ({ID})")]
    public class SessionSummaryPacket : GibraltarCachedPacket, IPacket, IEquatable<SessionSummaryPacket>
    {
        //Stuff that aligns with SESSION table in index
        private Guid m_ComputerId;

        private string m_ProductName;
        private string m_ApplicationName;
        private string m_EnvironmentName;
        private string m_PromotionLevelName;
        private Version m_ApplicationVersion;
        private ApplicationType m_ApplicationType;
        private string m_ApplicationDescription;
        private string m_Caption;
        private string m_TimeZoneCaption;
        private DateTimeOffset m_EndDateTime;
        private Version m_AgentVersion;
        private string m_UserName;
        private string m_UserDomainName;
        private string m_HostName;
        private string m_DnsDomainName;

        //Stuff that aligns with SESSION_DETAILS table in index
        private int m_OSPlatformCode;

        private Version m_OSVersion;
        private string m_OSServicePack;
        private string m_OSCultureName;
        private ProcessorArchitecture m_OSArchitecture;
        private OSBootMode m_OSBootMode;
        private int m_OSSuiteMaskCode;
        private int m_OSProductTypeCode;
        private Version m_RuntimeVersion;
        private ProcessorArchitecture m_RuntimeArchitecture;
        private string m_CurrentCultureName;
        private string m_CurrentUICultureName;
        private int m_MemoryMB;
        private int m_Processors;
        private int m_ProcessorCores;
        private bool m_UserInteractive;
        private bool m_TerminalServer;
        private int m_ScreenWidth;
        private int m_ScreenHeight;
        private int m_ColorDepth;
        private string m_CommandLine;

        //App.Config properties
        private readonly Dictionary<string, string> m_Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        //CALCULATED value
        private string m_FullyQualifiedUserName;

        public SessionSummaryPacket()
            : base(true)
        {
        }

        /// <summary>
        /// Create a session summary packet from the provided session header
        /// </summary>
        /// <param name="sessionHeader"></param>
        public SessionSummaryPacket(SessionHeader sessionHeader)
            : base(sessionHeader.Id, true)
        {
            //Stuff that aligns with SESSION table in index
            m_ComputerId = sessionHeader.ComputerId.GetValueOrDefault();
            Timestamp = sessionHeader.StartDateTime;
            m_EndDateTime = sessionHeader.EndDateTime;
            m_Caption = sessionHeader.Caption;
            m_TimeZoneCaption = sessionHeader.TimeZoneCaption;
            m_ProductName = sessionHeader.Product;
            m_ApplicationName = sessionHeader.Application;
            m_EnvironmentName = sessionHeader.Environment;
            m_PromotionLevelName = sessionHeader.PromotionLevel;
            m_ApplicationType =
                (ApplicationType) Enum.Parse(typeof(ApplicationType), sessionHeader.ApplicationTypeName, true);
            m_ApplicationDescription = sessionHeader.ApplicationDescription;
            m_ApplicationVersion = sessionHeader.ApplicationVersion;
            m_AgentVersion = sessionHeader.AgentVersion;
            m_UserName = sessionHeader.UserName;
            m_UserDomainName = sessionHeader.UserDomainName;
            m_HostName = sessionHeader.HostName;
            m_DnsDomainName = sessionHeader.DnsDomainName;

            //Stuff that aligns with SESSION_DETAILS table in index
            m_OSPlatformCode = sessionHeader.OSPlatformCode;
            m_OSVersion = sessionHeader.OSVersion;
            m_OSServicePack = sessionHeader.OSServicePack;
            m_OSCultureName = sessionHeader.OSCultureName;
            m_OSArchitecture = sessionHeader.OSArchitecture;
            m_OSBootMode = sessionHeader.OSBootMode;
            m_OSSuiteMaskCode = sessionHeader.OSSuiteMask;
            m_OSProductTypeCode = sessionHeader.OSProductType;
            m_RuntimeVersion = sessionHeader.RuntimeVersion;
            m_RuntimeArchitecture = sessionHeader.RuntimeArchitecture;
            m_CurrentCultureName = sessionHeader.CurrentCultureName;
            m_CurrentUICultureName = sessionHeader.CurrentUICultureName;
            m_MemoryMB = sessionHeader.MemoryMB;
            m_Processors = sessionHeader.Processors;
            m_ProcessorCores = sessionHeader.ProcessorCores;
            m_UserInteractive = sessionHeader.UserInteractive;
            m_TerminalServer = sessionHeader.TerminalServer;
            m_ScreenWidth = sessionHeader.ScreenWidth;
            m_ScreenHeight = sessionHeader.ScreenHeight;
            m_ColorDepth = sessionHeader.ColorDepth;
            m_CommandLine = sessionHeader.CommandLine;

            //and app.config properties.
            m_Properties = new Dictionary<string, string>(sessionHeader.Properties);

            //finally calculated value.
            CalculateFullyQualifiedUserName();
        }

        #region Public Properties and Methods

        /// <summary>
        /// The unique Id of the local computer.
        /// </summary>
        public Guid ComputerId
        {
            get => m_ComputerId;
            set => m_ComputerId = value;
        }

        public DateTimeOffset EndDateTime
        {
            get => m_EndDateTime;
            set => m_EndDateTime = value;
        }

        public string Caption
        {
            get => m_Caption;
            set => m_Caption = SetSafeStringValue(value, 1024);
        }

        public string TimeZoneCaption
        {
            get => m_TimeZoneCaption;
            set => m_TimeZoneCaption = SetSafeStringValue(value, 120);
        }

        public string ProductName
        {
            get => m_ProductName;
            set => m_ProductName = SetSafeStringValue(value, 120);
        }

        public string ApplicationName
        {
            get => m_ApplicationName;
            set => m_ApplicationName = SetSafeStringValue(value, 120);
        }

        public string EnvironmentName
        {
            get => m_EnvironmentName;
            set => m_EnvironmentName = SetSafeStringValue(value, 120);
        }

        public string PromotionLevelName
        {
            get => m_PromotionLevelName;
            set => m_PromotionLevelName = SetSafeStringValue(value, 120);
        }

        public ApplicationType ApplicationType
        {
            get => m_ApplicationType;
            set => m_ApplicationType = value;
        }

        public string ApplicationDescription
        {
            get => m_ApplicationDescription;
            set => m_ApplicationDescription = SetSafeStringValue(value, 1024);
        }

        /// <summary>
        /// The version of the application that recorded the session
        /// </summary>
        public Version ApplicationVersion
        {
            get => m_ApplicationVersion;
            set => m_ApplicationVersion = value;
        }

        public Version AgentVersion
        {
            get => m_AgentVersion;
            set => m_AgentVersion = value;
        }

        public string HostName
        {
            get => m_HostName;
            set => m_HostName = SetSafeStringValue(value, 120);
        }

        public string DnsDomainName
        {
            get => m_DnsDomainName;
            set => m_DnsDomainName = SetSafeStringValue(value, 512);
        }

        public string UserName
        {
            get => m_UserName;
            set
            {
                m_UserName = SetSafeStringValue(value, 120);
                CalculateFullyQualifiedUserName();
            }
        }

        public string UserDomainName
        {
            get => m_UserDomainName;
            set
            {
                m_UserDomainName = SetSafeStringValue(value, 50);
                CalculateFullyQualifiedUserName();
            }
        }

        public int OSPlatformCode
        {
            get => m_OSPlatformCode;
            set => m_OSPlatformCode = value;
        }

        public Version OSVersion
        {
            get => m_OSVersion;
            set => m_OSVersion = value;
        }

        public string OSServicePack
        {
            get => m_OSServicePack;
            set => m_OSServicePack = SetSafeStringValue(value, 50);
        }

        public string OSCultureName
        {
            get => m_OSCultureName;
            set => m_OSCultureName = SetSafeStringValue(value, 50);
        }

        public ProcessorArchitecture OSArchitecture
        {
            get => m_OSArchitecture;
            set => m_OSArchitecture = value;
        }

        public OSBootMode OSBootMode
        {
            get => m_OSBootMode;
            set => m_OSBootMode = value;
        }

        public int OSSuiteMask
        {
            get => m_OSSuiteMaskCode;
            set => m_OSSuiteMaskCode = value;
        }

        public int OSProductType
        {
            get => m_OSProductTypeCode;
            set => m_OSProductTypeCode = value;
        }

        public Version RuntimeVersion
        {
            get => m_RuntimeVersion;
            set => m_RuntimeVersion = value;
        }

        public ProcessorArchitecture RuntimeArchitecture
        {
            get => m_RuntimeArchitecture;
            set => m_RuntimeArchitecture = value;
        }

        public string CurrentCultureName
        {
            get => m_CurrentCultureName;
            set => m_CurrentCultureName = SetSafeStringValue(value, 50);
        }

        public string CurrentUICultureName
        {
            get => m_CurrentUICultureName;
            set => m_CurrentUICultureName = SetSafeStringValue(value, 50);
        }

        public int MemoryMB
        {
            get => m_MemoryMB;
            set => m_MemoryMB = value;
        }

        public int Processors
        {
            get => m_Processors;
            set => m_Processors = value;
        }

        public int ProcessorCores
        {
            get => m_ProcessorCores;
            set => m_ProcessorCores = value;
        }

        public bool UserInteractive
        {
            get => m_UserInteractive;
            set => m_UserInteractive = value;
        }

        public bool TerminalServer
        {
            get => m_TerminalServer;
            set => m_TerminalServer = value;
        }

        public int ScreenWidth
        {
            get => m_ScreenWidth;
            set => m_ScreenWidth = value;
        }

        public int ScreenHeight
        {
            get => m_ScreenHeight;
            set => m_ScreenHeight = value;
        }

        public int ColorDepth
        {
            get => m_ColorDepth;
            set => m_ColorDepth = value;
        }

        public string CommandLine
        {
            get => m_CommandLine;
            set => m_CommandLine = SetSafeStringValue(value, 2048);
        }

        /// <summary>
        /// The fully qualified user name of the user the application was run as.
        /// </summary>
        public string FullyQualifiedUserName => m_FullyQualifiedUserName;

        /// <summary>
        /// Application provided properties 
        /// </summary>
        public Dictionary<string, string> Properties => m_Properties;

        #endregion

        #region IPacket Members

        private const int SerializationVersion = 3;

        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            //the majority of packets have no dependencies
            return null;
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(SessionSummaryPacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, false);

            //Session index information
            definition.Fields.Add("ComputerId", FieldType.Guid);
            definition.Fields.Add("ProductName", FieldType.String);
            definition.Fields.Add("ApplicationName", FieldType.String);
            definition.Fields.Add("EnvironmentName", FieldType.String);
            definition.Fields.Add("PromotionLevelName", FieldType.String);
            definition.Fields.Add("ApplicationType", FieldType.Int32);
            definition.Fields.Add("ApplicationVersion", FieldType.String);
            definition.Fields.Add("ApplicationDescription", FieldType.String);
            definition.Fields.Add("Caption", FieldType.String);
            definition.Fields.Add("TimeZoneCaption", FieldType.String);
            definition.Fields.Add("EndDateTime", FieldType.DateTimeOffset);
            definition.Fields.Add("AgentVersion", FieldType.String);
            definition.Fields.Add("UserName", FieldType.String);
            definition.Fields.Add("UserDomainName", FieldType.String);
            definition.Fields.Add("HostName", FieldType.String);
            definition.Fields.Add("DNSDomainName", FieldType.String);

            //Session Details index information
            definition.Fields.Add("OSPlatformCode", FieldType.Int32);
            definition.Fields.Add("OSVersion", FieldType.String);
            definition.Fields.Add("OSServicePack", FieldType.String);
            definition.Fields.Add("OSCultureName", FieldType.String);
            definition.Fields.Add("OSArchitecture", FieldType.Int32);
            definition.Fields.Add("OSBootMode", FieldType.Int32);
            definition.Fields.Add("OSSuiteMaskCode", FieldType.Int32);
            definition.Fields.Add("OSProductTypeCode", FieldType.Int32);
            definition.Fields.Add("RuntimeVersion", FieldType.String);
            definition.Fields.Add("RuntimeArchitecture", FieldType.Int32);
            definition.Fields.Add("CurrentCultureName", FieldType.String);
            definition.Fields.Add("CurrentUICultureName", FieldType.String);
            definition.Fields.Add("MemoryMB", FieldType.Int32);
            definition.Fields.Add("Processors", FieldType.Int32);
            definition.Fields.Add("ProcessorCores", FieldType.Int32);
            definition.Fields.Add("UserInteractive", FieldType.Bool);
            definition.Fields.Add("TerminalServer", FieldType.Bool);
            definition.Fields.Add("ScreenWidth", FieldType.Int32);
            definition.Fields.Add("ScreenHeight", FieldType.Int32);
            definition.Fields.Add("ColorDepth", FieldType.Int32);
            definition.Fields.Add("CommandLine", FieldType.String);


            //App.Config options
            foreach (KeyValuePair<string, string> property in m_Properties)
                definition.Fields.Add(property.Key, FieldType.String);
            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            //Write out session stuff
            packet.SetField("ComputerId", m_ComputerId);
            packet.SetField("ProductName", m_ProductName);
            packet.SetField("ApplicationName", m_ApplicationName);
            packet.SetField("EnvironmentName", m_EnvironmentName);
            packet.SetField("PromotionLevelName", m_PromotionLevelName);
            packet.SetField("ApplicationVersion", m_ApplicationVersion.ToString());
            packet.SetField("ApplicationType", (int) m_ApplicationType);
            packet.SetField("ApplicationDescription", m_ApplicationDescription);
            packet.SetField("Caption", m_Caption);
            packet.SetField("TimeZoneCaption", m_TimeZoneCaption);
            packet.SetField("EndDateTime", m_EndDateTime);
            packet.SetField("AgentVersion", m_AgentVersion.ToString());
            packet.SetField("UserName", m_UserName);
            packet.SetField("UserDomainName", m_UserDomainName);
            packet.SetField("HostName", m_HostName);
            packet.SetField("DNSDomainName", m_DnsDomainName);

            //write out session details stuff
            packet.SetField("OSPlatformCode", m_OSPlatformCode);
            packet.SetField("OSVersion", m_OSVersion.ToString());
            packet.SetField("OSServicePack", m_OSServicePack);
            packet.SetField("OSCultureName", m_OSCultureName);
            packet.SetField("OSArchitecture", (int) m_OSArchitecture);
            packet.SetField("OSBootMode", (int) m_OSBootMode);
            packet.SetField("OSSuiteMaskCode", m_OSSuiteMaskCode);
            packet.SetField("OSProductTypeCode", m_OSProductTypeCode);
            packet.SetField("RuntimeVersion", m_RuntimeVersion.ToString());
            packet.SetField("RuntimeArchitecture", (int) m_RuntimeArchitecture);
            packet.SetField("CurrentCultureName", m_CurrentCultureName);
            packet.SetField("CurrentUICultureName", m_CurrentUICultureName);
            packet.SetField("MemoryMB", m_MemoryMB);
            packet.SetField("Processors", m_Processors);
            packet.SetField("ProcessorCores", m_ProcessorCores);
            packet.SetField("UserInteractive", m_UserInteractive);
            packet.SetField("TerminalServer", m_TerminalServer);
            packet.SetField("ScreenWidth", m_ScreenWidth);
            packet.SetField("ScreenHeight", m_ScreenHeight);
            packet.SetField("ColorDepth", m_ColorDepth);
            packet.SetField("CommandLine", m_CommandLine);


            //write out application config provided stuff
            foreach (KeyValuePair<string, string> property in m_Properties)
                packet.SetField(property.Key, property.Value);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            //read back session stuff.  Order really doesn't matter a damn, but hey.
            packet.GetField("ProductName", out m_ProductName);
            packet.GetField("ApplicationName", out m_ApplicationName);

            string applicationVersionRaw;
            packet.GetField("ApplicationVersion", out applicationVersionRaw);
            m_ApplicationVersion = new Version(applicationVersionRaw);

            int applicationTypeRaw;
            packet.GetField("ApplicationType", out applicationTypeRaw);
            m_ApplicationType = (ApplicationType) applicationTypeRaw;

            packet.GetField("ApplicationDescription", out m_ApplicationDescription);
            packet.GetField("Caption", out m_Caption);
            packet.GetField("TimeZoneCaption", out m_TimeZoneCaption);
            packet.GetField("EndDateTime", out m_EndDateTime);

            string agentVersionRaw;
            packet.GetField("AgentVersion", out agentVersionRaw);
            m_AgentVersion = new Version(agentVersionRaw);

            packet.GetField("UserName", out m_UserName);
            packet.GetField("UserDomainName", out m_UserDomainName);
            packet.GetField("HostName", out m_HostName);
            packet.GetField("DNSDomainName", out m_DnsDomainName);

            //Read back session details stuff
            packet.GetField("OSPlatformCode", out m_OSPlatformCode);
            string osVersionRaw;
            packet.GetField("OSVersion", out osVersionRaw);
            m_OSVersion = new Version(osVersionRaw);

            packet.GetField("OSServicePack", out m_OSServicePack);
            packet.GetField("OSCultureName", out m_OSCultureName);

            int osArchitectureRaw;
            packet.GetField("OSArchitecture", out osArchitectureRaw);
            m_OSArchitecture = ((ProcessorArchitecture) osArchitectureRaw);

            int osBootModeRaw;
            packet.GetField("OSBootMode", out osBootModeRaw);
            m_OSBootMode = (OSBootMode) osBootModeRaw;

            packet.GetField("OSSuiteMaskCode", out m_OSSuiteMaskCode);
            packet.GetField("OSProductTypeCode", out m_OSProductTypeCode);

            string runtimeVersionRaw;
            packet.GetField("RuntimeVersion", out runtimeVersionRaw);
            m_RuntimeVersion = new Version(runtimeVersionRaw);

            int runtimeArchitectureRaw;
            packet.GetField("RuntimeArchitecture", out runtimeArchitectureRaw);
            m_RuntimeArchitecture = ((ProcessorArchitecture) runtimeArchitectureRaw);

            packet.GetField("CurrentCultureName", out m_CurrentCultureName);
            packet.GetField("CurrentUICultureName", out m_CurrentUICultureName);
            packet.GetField("MemoryMB", out m_MemoryMB);
            packet.GetField("Processors", out m_Processors);
            packet.GetField("ProcessorCores", out m_ProcessorCores);
            packet.GetField("UserInteractive", out m_UserInteractive);
            packet.GetField("TerminalServer", out m_TerminalServer);
            packet.GetField("ScreenWidth", out m_ScreenWidth);
            packet.GetField("ScreenHeight", out m_ScreenHeight);
            packet.GetField("ColorDepth", out m_ColorDepth);
            packet.GetField("CommandLine", out m_CommandLine);

            //See the 33 below?  That should be the # of reads - 1 we've done before properties.            
            int baseFields = 33;

            if (definition.Version > 2)
            {
                packet.GetField("ComputerId", out m_ComputerId);
                baseFields++;
            }

            if (definition.Version > 1)
            {
                packet.GetField("EnvironmentName", out m_EnvironmentName);
                packet.GetField("PromotionLevelName", out m_PromotionLevelName);
                baseFields += 2; //we have to account for the additional fields beyond the default
            }

            //Application provided properties
            //if this starts blowing up, count the number of fields above first.
            for (int i = baseFields; i < definition.Fields.Count; i++)
            {
                FieldDefinition fieldDefinition = definition.Fields[i];
                string fieldValue;
                packet.GetField(fieldDefinition.Name, out fieldValue);
                m_Properties.Add(fieldDefinition.Name, fieldValue);
            }
        }

        #endregion

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
            return Equals(other as SessionSummaryPacket);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(SessionSummaryPacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            bool isEqual = ((ComputerId == other.ComputerId)
                            && (Caption == other.Caption)
                            && (EndDateTime == other.EndDateTime)
                            && (ProductName == other.ProductName)
                            && (ApplicationName == other.ApplicationName)
                            && (EnvironmentName == other.EnvironmentName)
                            && (PromotionLevelName == other.PromotionLevelName)
                            && (ApplicationType == other.ApplicationType)
                            && (ApplicationDescription == other.ApplicationDescription)
                            && (ApplicationVersion == other.ApplicationVersion)
                            && (HostName == other.HostName)
                            && (DnsDomainName == other.DnsDomainName)
                            && (AgentVersion == other.AgentVersion)
                            && (UserDomainName == other.UserDomainName)
                            && (UserName == other.UserName)
                            && (TimeZoneCaption == other.TimeZoneCaption)
                            && (base.Equals(other)));

            //if we're equal so far, keep digging
            if (isEqual)
            {
                foreach (KeyValuePair<string, string> property in m_Properties)
                {
                    //does the comparable one exist?
                    string otherValue;
                    if (other.Properties.TryGetValue(property.Key, out otherValue))
                    {
                        isEqual = property.Value.Equals(otherValue);
                    }
                    else
                    {
                        //they are clearly not equal
                        isEqual = false;
                    }

                    if (isEqual == false)
                        break;
                }
            }

            return isEqual;
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

            if (m_ComputerId != Guid.Empty)
                myHash ^= m_ComputerId.GetHashCode(); // Fold in hash code for string ProductName

            if (m_ProductName != null)
                myHash ^= m_ProductName.GetHashCode(); // Fold in hash code for string ProductName

            if (m_ApplicationName != null)
                myHash ^= m_ApplicationName.GetHashCode(); // Fold in hash code for string ApplicationName

            if (m_EnvironmentName != null)
                myHash ^= m_EnvironmentName.GetHashCode(); // Fold in hash code for string EnvironmentName

            if (m_PromotionLevelName != null)
                myHash ^= m_PromotionLevelName.GetHashCode(); // Fold in hash code for string PromotionName

            if (m_ApplicationVersion != null)
                myHash ^= m_ApplicationVersion.GetHashCode(); // Fold in hash code for string ApplicationVersion

            myHash ^= m_ApplicationType.GetHashCode(); // Fold in hash code for string ApplicationTypeName

            if (m_ApplicationDescription != null)
                myHash ^= m_ApplicationDescription.GetHashCode(); // Fold in hash code for string ApplicationDescription

            if (m_Caption != null) myHash ^= m_Caption.GetHashCode(); // Fold in hash code for string Caption

            if (m_TimeZoneCaption != null)
                myHash ^= m_TimeZoneCaption.GetHashCode(); // Fold in hash code for string TimeZoneCaption

            myHash ^= m_EndDateTime.GetHashCode(); // Fold in hash code for DateTimeOffset member EndDateTime

            if (m_AgentVersion != null)
                myHash ^= m_AgentVersion.GetHashCode(); // Fold in hash code for string MonitorVersion

            if (m_UserName != null)
                myHash ^= m_UserName.GetHashCode(); // Fold in hash code for string UserName

            if (m_UserDomainName != null)
                myHash ^= m_UserDomainName.GetHashCode(); // Fold in hash code for string UserDomainName

            if (m_HostName != null)
                myHash ^= m_HostName.GetHashCode(); // Fold in hash code for string HostName

            if (m_DnsDomainName != null)
                myHash ^= m_DnsDomainName.GetHashCode(); // Fold in hash code for string DNSDomainName

            // Not bothering with dictionary of Properties

            return myHash;
        }

        /// <summary>
        /// Eliminates nulls and ensures that the string value isn't too long.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        private static string SetSafeStringValue(string value, int maxLength)
        {
            string returnVal = value ?? string.Empty;
            if (returnVal.Length > maxLength)
                returnVal = returnVal.Substring(0, maxLength);

            return returnVal;
        }

        private void CalculateFullyQualifiedUserName()
        {
            m_FullyQualifiedUserName = string.IsNullOrEmpty(m_UserDomainName)
                ? m_UserName
                : StringReference.GetReference(m_UserDomainName + "\\" + m_UserName);
        }
    }
}
