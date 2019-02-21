using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;

namespace Gibraltar.Data
{
    /// <summary>
    /// Used at the start of a data stream to contain the session summary
    /// </summary>
    /// <remarks>The session header subsumes a SessionStartInfoPacket, but both should be included
    /// in a stream because the SessionHeader is really a cache of the session start info packet that
    /// is easy to access.</remarks>
    public sealed class SessionHeader: ISessionSummary, IEquatable<SessionHeader>
    {
        private readonly object m_Lock = new object();

        //You just can't do a binary format without recording version information
        private int m_MajorVersion;
        private int m_MinorVersion;

        //Stuff that aligns with SESSION table in index
        private Guid? m_ComputerId;
        private Guid m_SessionId;
        private DateTimeOffset m_SessionStartDateTime;
        private DateTimeOffset m_SessionEndDateTime;
        private string m_Caption;
        private string m_ProductName;
        private string m_ApplicationName;
        private string m_EnvironmentName;
        private string m_PromotionLevelName;
        private Version m_ApplicationVersion;
        private string m_ApplicationTypeName;
        private string m_ApplicationDescription;
        private string m_TimeZoneCaption;
        private Version m_AgentVersion;
        private string m_UserName;
        private string m_UserDomainName;
        private string m_HostName;
        private string m_DnsDomainName;
        private string m_SessionStatusName;
        private int m_MessageCount;
        private int m_CriticalCount;
        private int m_ErrorCount;
        private int m_WarningCount;

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

        //file specific information (for this file)
        private bool m_HasFileInfo;
        private Guid m_FileID;
        private DateTimeOffset m_FileStartDateTime;
        private DateTimeOffset m_FileEndDateTime;
        private readonly bool m_Valid;
        private bool m_IsLastFile;
        private int m_FileSequence;
        private int m_OffsetSessionEndDateTime;
        private int m_OffsetMessageCount;
        private int m_OffsetCriticalCount;
        private int m_OffsetErrorCount;
        private int m_OffsetWarningCount;

        //cached serialized data (for when we're in a fixed representation and want performance)
        private byte[] m_LastRawData; //raw data is JUST the session header stuff, not file or CRC, so you can't return just it.

        private string m_FullyQualifiedUserName;
        private int m_HashCode;
        private SessionStatus m_SessionStatus;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Create a new header from the provided session summary information
        /// </summary>
        /// <param name="sessionSummary"></param>
        public SessionHeader(SessionSummary sessionSummary)
            :this(sessionSummary.Properties)
        {
            //copy the values from the session start info.  We make a copy because it'd be deadly if any of this changed
            //while we were alive - and while none of it should, lets just not count on that,OK?
            //SESSION index information
            Id = sessionSummary.Id;
            ComputerId = sessionSummary.ComputerId;
            Product = sessionSummary.Product;
            Application = sessionSummary.Application;
            Environment = sessionSummary.Environment;
            PromotionLevel = sessionSummary.PromotionLevel;
            ApplicationVersion = sessionSummary.ApplicationVersion;
            ApplicationTypeName = sessionSummary.ApplicationType.ToString();
            ApplicationDescription = sessionSummary.ApplicationDescription;
            Caption = sessionSummary.Caption;
            StatusName = sessionSummary.Status.ToString();
            TimeZoneCaption = sessionSummary.TimeZoneCaption;
            StartDateTime = sessionSummary.StartDateTime;
            EndDateTime = sessionSummary.EndDateTime;
            AgentVersion = sessionSummary.AgentVersion;
            UserName = sessionSummary.UserName;
            UserDomainName = sessionSummary.UserDomainName;
            HostName = sessionSummary.HostName;
            DnsDomainName = sessionSummary.DnsDomainName;
            MessageCount = sessionSummary.MessageCount;
            CriticalCount = sessionSummary.CriticalCount;
            ErrorCount = sessionSummary.ErrorCount;
            WarningCount = sessionSummary.WarningCount;

            //SESSION DETAIL index information
            OSPlatformCode = sessionSummary.OSPlatformCode;
            OSVersion = sessionSummary.OSVersion;
            OSServicePack = sessionSummary.OSServicePack;
            OSCultureName = sessionSummary.OSCultureName;
            OSArchitecture = sessionSummary.OSArchitecture;
            OSBootMode = sessionSummary.OSBootMode;
            OSSuiteMask = sessionSummary.OSSuiteMask;
            OSProductType = sessionSummary.OSProductType;
            RuntimeVersion = sessionSummary.RuntimeVersion;
            RuntimeArchitecture = sessionSummary.RuntimeArchitecture;
            CurrentCultureName = sessionSummary.CurrentCultureName;
            CurrentUICultureName = sessionSummary.CurrentUICultureName;
            MemoryMB = sessionSummary.MemoryMB;
            Processors = sessionSummary.Processors;
            ProcessorCores = sessionSummary.ProcessorCores;
            UserInteractive = sessionSummary.UserInteractive;
            TerminalServer = sessionSummary.TerminalServer;
            ScreenWidth = sessionSummary.ScreenWidth;
            ScreenHeight = sessionSummary.ScreenHeight;
            ColorDepth = sessionSummary.ColorDepth;
            CommandLine = sessionSummary.CommandLine;
        }

        /// <summary>
        /// Create a new session header with the specified properties collection.  All other values are unset.
        /// </summary>
        /// <param name="properties"></param>
        public SessionHeader(IDictionary<string, string> properties)
        {
            m_MajorVersion = FileHeader.DefaultMajorVersion;
            m_MinorVersion = FileHeader.DefaultMinorVersion;

            m_Properties = new Dictionary<string, string>(properties);

            IsNew = true;
        }

        /// <summary>
        /// Create a new session header by reading the provided byte array
        /// </summary>
        /// <param name="data"></param>
        public SessionHeader(byte[] data)
        {
            using (MemoryStream rawData = new MemoryStream(data))
            {
                m_Valid = LoadStream(rawData, data.Length);
            }
        }

        /// <summary>
        /// Create a new session header by reading the provided stream, which must contain ONLY the header
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length">The number of bytes to read from the stream for the header (or zero to read the whole stream)</param>
        public SessionHeader(Stream data, int length)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            m_Valid = LoadStream(data, length);
        }

        #region Public Properties and Methods

        /// <summary>
        /// Export the file header into a raw data array
        /// </summary>
        /// <returns></returns>
        public byte[] RawData()
        {
            lock(m_Lock)
            {
                MemoryStream rawData = new MemoryStream(2048);
                byte[] curValue;

                //two paths for this:  We either already have a cached value or we don't.
                if (m_LastRawData == null)
                {
                    //gotta make the last raw data.
                    curValue = BinarySerializer.SerializeValue(m_MajorVersion);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_MinorVersion);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_SessionId);
                    rawData.Write(curValue, 0, curValue.Length);

                    if (FileHeader.SupportsComputerId(m_MajorVersion, m_MinorVersion))
                    {
                        curValue = BinarySerializer.SerializeValue(m_ComputerId.GetValueOrDefault());
                        rawData.Write(curValue, 0, curValue.Length);
                    }

                    curValue = BinarySerializer.SerializeValue(m_ProductName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_ApplicationName);
                    rawData.Write(curValue, 0, curValue.Length);

                    if (FileHeader.SupportsEnvironmentAndPromotion(m_MajorVersion, m_MinorVersion))
                    {
                        curValue = BinarySerializer.SerializeValue(m_EnvironmentName);
                        rawData.Write(curValue, 0, curValue.Length);

                        curValue = BinarySerializer.SerializeValue(m_PromotionLevelName);
                        rawData.Write(curValue, 0, curValue.Length);
                    }

                    curValue = BinarySerializer.SerializeValue(m_ApplicationVersion.ToString());
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_ApplicationTypeName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_ApplicationDescription);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_Caption);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_TimeZoneCaption);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_SessionStatusName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_SessionStartDateTime);
                    rawData.Write(curValue, 0, curValue.Length);

                    //OK, where we are now is the end date time position which is variable
                    m_OffsetSessionEndDateTime = (int) rawData.Position;
                    curValue = BinarySerializer.SerializeValue(m_SessionEndDateTime);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_AgentVersion.ToString());
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_UserName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_UserDomainName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_HostName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_DnsDomainName);
                    rawData.Write(curValue, 0, curValue.Length);

                    //For each message count we need to record our position
                    m_OffsetMessageCount = (int) rawData.Position;
                    curValue = BinarySerializer.SerializeValue(m_MessageCount);
                    rawData.Write(curValue, 0, curValue.Length);

                    m_OffsetCriticalCount = (int) rawData.Position;
                    curValue = BinarySerializer.SerializeValue(m_CriticalCount);
                    rawData.Write(curValue, 0, curValue.Length);

                    m_OffsetErrorCount = (int) rawData.Position;
                    curValue = BinarySerializer.SerializeValue(m_ErrorCount);
                    rawData.Write(curValue, 0, curValue.Length);

                    m_OffsetWarningCount = (int) rawData.Position;
                    curValue = BinarySerializer.SerializeValue(m_WarningCount);
                    rawData.Write(curValue, 0, curValue.Length);

                    //Stuff that aligns with SESSION_DETAILS table in index
                    curValue = BinarySerializer.SerializeValue(m_OSPlatformCode);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_OSVersion.ToString());
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_OSServicePack);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_OSCultureName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_OSArchitecture.ToString());
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_OSBootMode.ToString());
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_OSSuiteMaskCode);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_OSProductTypeCode);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_RuntimeVersion.ToString());
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_RuntimeArchitecture.ToString());
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_CurrentCultureName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_CurrentUICultureName);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_MemoryMB);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_Processors);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_ProcessorCores);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_UserInteractive);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_TerminalServer);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_ScreenWidth);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_ScreenHeight);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_ColorDepth);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_CommandLine);
                    rawData.Write(curValue, 0, curValue.Length);

                    //Application provided Properties
                    //now write off properties as a set of name/value pairs
                    //we have to write out how many properties there are so we know how many to read back
                    curValue = BinarySerializer.SerializeValue(m_Properties.Count);
                    rawData.Write(curValue, 0, curValue.Length);

                    foreach (KeyValuePair<string, string> property in m_Properties)
                    {
                        curValue = BinarySerializer.SerializeValue(property.Key);
                        rawData.Write(curValue, 0, curValue.Length);

                        curValue = BinarySerializer.SerializeValue(property.Value);
                        rawData.Write(curValue, 0, curValue.Length);
                    }

                    //cache the raw data so we don't have to recalc it every time.
                    m_LastRawData = rawData.ToArray();
                }
                else
                {
                    //copy the last raw data we have into the stream.
                    rawData.Write(m_LastRawData, 0, m_LastRawData.Length);
                }

                //BEGIN FILE INFO (added every time)
                if (m_HasFileInfo)
                {
                    curValue = BinarySerializer.SerializeValue(m_FileID);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_FileSequence);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_FileStartDateTime);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_FileEndDateTime);
                    rawData.Write(curValue, 0, curValue.Length);

                    curValue = BinarySerializer.SerializeValue(m_IsLastFile);
                    rawData.Write(curValue, 0, curValue.Length);
                }

                //CRC CALC
                //Now we need to calculate the header CRC
                curValue = BinarySerializer.CalculateCRC(rawData.ToArray(), (int)rawData.Position);
                rawData.Write(curValue, 0, curValue.Length);
                return rawData.ToArray(); 
            }
        }

        /// <summary>
        /// The major version of the binary format of the session header
        /// </summary>
        public int MajorVersion
        {
            get { return m_MajorVersion; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_MajorVersion = value;
                }
            }
        }

        /// <summary>
        /// The minor version of the binary format of the session header
        /// </summary>
        public int MinorVersion
        {
            get { return m_MinorVersion; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_MinorVersion = value;
                }
            }
        }

        /// <summary>
        /// The unique Id of the session
        /// </summary>
        public Guid Id
        {
            get { return m_SessionId; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_SessionId = value;
                }
            }
        }

        /// <summary>
        /// The link to this item on the server
        /// </summary>
        public Uri Uri
        {
            get { throw new NotSupportedException("Links are not supported in this context"); }
        }

        /// <summary>
        /// Indicates if all of the session data is stored that is expected to be available
        /// </summary>
        public bool IsComplete { get { return m_HasFileInfo ? m_IsLastFile : false; } }

        /// <summary>
        /// The unique Id of the computer
        /// </summary>
        public Guid? ComputerId
        {
            get { return m_ComputerId; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ComputerId = value;
                }
            }
        }

        /// <summary>
        /// A display caption for the session
        /// </summary>
        public string Caption
        {
            get { return m_Caption; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_Caption = value;
                }
            }
        }

        /// <summary>
        /// The product name of the application that recorded the session
        /// </summary>
        public string Product
        {
            get { return m_ProductName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ProductName = value;
                }
            }
        }

        /// <summary>
        /// The title of the application that recorded the session
        /// </summary>
        public string Application
        {
            get { return m_ApplicationName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ApplicationName = value;
                }
            }
        }

        /// <summary>
        /// Optional.  The environment this session is running in.
        /// </summary>
        /// <remarks>Environments are useful for categorizing sessions, for example to 
        /// indicate the hosting environment. If a value is provided it will be 
        /// carried with the session data to upstream servers and clients.  If the 
        /// corresponding entry does not exist it will be automatically created.</remarks>
        public string Environment
        {
            get { return m_EnvironmentName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_EnvironmentName = value;
                }
            }
        }

        /// <summary>
        /// Optional.  The promotion level of the session.
        /// </summary>
        /// <remarks>Promotion levels are useful for categorizing sessions, for example to 
        /// indicate whether it was run in development, staging, or production. 
        /// If a value is provided it will be carried with the session data to upstream servers and clients.  
        /// If the corresponding entry does not exist it will be automatically created.</remarks>
        public string PromotionLevel
        {
            get { return m_PromotionLevelName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_PromotionLevelName = value;
                }
            }
        }

        /// <summary>
        /// The type of process the application ran as.
        /// </summary>
        public ApplicationType ApplicationType
        {
            get { return (ApplicationType)Enum.Parse(typeof(ApplicationType), m_ApplicationTypeName, true); }
        }

        /// <summary>
        /// The type of process the application ran as.
        /// </summary>
        /// <remarks>Not an enumeration because the ApplicationType enum isn't accessible at this level.</remarks>
        public string ApplicationTypeName
        {
            get { return m_ApplicationTypeName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ApplicationTypeName = value;
                }
            }
        }

        /// <summary>
        /// The description of the application from its manifest.
        /// </summary>
        public string ApplicationDescription
        {
            get { return m_ApplicationDescription; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ApplicationDescription = value;
                }
            }
        }

        /// <summary>
        /// The version of the application that recorded the session
        /// </summary>
        public Version ApplicationVersion
        {
            get { return m_ApplicationVersion; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ApplicationVersion = value;
                }
            }
        }

        /// <summary>
        /// The version of the Gibraltar Agent used to monitor the session
        /// </summary>
        public Version AgentVersion
        {
            get { return m_AgentVersion; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_AgentVersion = value;
                }
            }
        }

        /// <summary>
        /// The host name / NetBIOS name of the computer that recorded the session
        /// </summary>
        /// <remarks>Does not include the domain name portion of the fully qualified DNS name.</remarks>
        public string HostName
        {
            get { return m_HostName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_HostName = value;
                }
            }
        }

        /// <summary>
        /// The DNS domain name of the computer that recorded the session.  May be empty.
        /// </summary>
        /// <remarks>Does not include the host name portion of the fully qualified DNS name.</remarks>
        public string DnsDomainName
        {
            get { return m_DnsDomainName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_DnsDomainName = value;
                }
            }
        }

        /// <summary>
        /// The display caption of the time zone where the session was recorded
        /// </summary>
        public string TimeZoneCaption
        {
            get { return m_TimeZoneCaption; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_TimeZoneCaption = value;
                }
            }
        }

        /// <summary>
        /// The user Id that was used to run the session
        /// </summary>
        public string UserName
        {
            get { return m_UserName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_FullyQualifiedUserName = null;
                    m_UserName = value;
                }
            }
        }

        /// <summary>
        /// The domain of the user id that was used to run the session
        /// </summary>
        public string UserDomainName
        {
            get { return m_UserDomainName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_FullyQualifiedUserName = null;
                    m_UserDomainName = value;
                }
            }
        }

        /// <summary>
        /// The fully qualified user name of the user the application was run as.
        /// </summary>
        public string FullyQualifiedUserName
        {
            get
            {
                if (m_FullyQualifiedUserName == null)
                {
                    m_FullyQualifiedUserName = string.IsNullOrEmpty(UserDomainName) ? UserName : UserDomainName + "\\" + UserName;
                }
                return m_FullyQualifiedUserName;
            }
        }

        /// <summary>
        /// The date and time the session started
        /// </summary>
        public DateTimeOffset StartDateTime
        {
            get { return m_SessionStartDateTime; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_SessionStartDateTime = value;
                }
            }
        }

        /// <summary>
        /// The date and time the session started
        /// </summary>
        public DateTimeOffset DisplayStartDateTime
        {
            get { return StartDateTime; }
        }

        /// <summary>
        /// The date and time the session ended or was last confirmed running
        /// </summary>
        public DateTimeOffset EndDateTime
        {
            get { return m_SessionEndDateTime; }
            set
            {
                lock (m_Lock)
                {
                    //this is an updatable field, so if we already have the raw data and can update it, lets do that.
                    if (m_LastRawData != null)
                    {
                        //protect against case that should never happen - we have the raw data, but not the offset to the value.
                        if (m_OffsetSessionEndDateTime == 0)
                        {
                            m_LastRawData = null;
                        }
                        else
                        {
                            //update the date at that point.
                            byte[] binaryValue = BinarySerializer.SerializeValue(value);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetSessionEndDateTime);
                        }
                    }

                    m_SessionEndDateTime = value;
                }
            }
        }

        /// <summary>
        /// The date and time the session ended
        /// </summary>
        public DateTimeOffset DisplayEndDateTime
        {
            get { return EndDateTime; }
        }

        /// <summary>
        /// The duration of the session.  May be zero indicating unknown
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                TimeSpan duration = (m_SessionEndDateTime - m_SessionStartDateTime);

                if (duration.TotalSeconds < 0)
                    duration = new TimeSpan(0);

                return duration;
            }
        }

        /// <summary>
        /// The final status of the session.
        /// </summary>
        public SessionStatus Status { get { return m_SessionStatus;  }}

        /// <summary>
        /// The status of the session (based on the SessionStatus enumeration)
        /// </summary>
        public string StatusName
        {
            get { return m_SessionStatusName; }
            set
            {
                lock (m_Lock)
                {
                    //only do this change if we actually have a change...
                    if (string.CompareOrdinal(m_SessionStatusName, value) != 0)
                    {
                        m_LastRawData = null;
                        m_SessionStatusName = value;

                        m_SessionStatus = StatusNameToStatus(m_SessionStatusName);
                    }
                }
            }
        }

        /// <summary>
        /// The total number of log messages recorded in the session
        /// </summary>
        public int MessageCount
        {
            get { return m_MessageCount; }
            set
            {
                lock (m_Lock)
                {
                    //this is an updatable field, so if we already have the raw data and can update it, lets do that.
                    if (m_LastRawData != null)
                    {
                        //protect against case that should never happen - we have the raw data, but not the offset to the value.
                        if (m_OffsetMessageCount == 0)
                        {
                            m_LastRawData = null;
                        }
                        else
                        {
                            //update the number at that point.
                            byte[] binaryValue = BinarySerializer.SerializeValue(value);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetMessageCount);
                        }
                    }

                    m_MessageCount = value;
                }
            }
        }

        /// <summary>
        /// The total number of critical severity log messages recorded in the session
        /// </summary>
        public int CriticalCount
        {
            get { return m_CriticalCount; }
            set
            {
                lock (m_Lock)
                {
                    //this is an updatable field, so if we already have the raw data and can update it, lets do that.
                    if (m_LastRawData != null)
                    {
                        //protect against case that should never happen - we have the raw data, but not the offset to the value.
                        if (m_OffsetCriticalCount == 0)
                        {
                            m_LastRawData = null;
                        }
                        else
                        {
                            //update the number at that point.
                            byte[] binaryValue = BinarySerializer.SerializeValue(value);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetCriticalCount);
                        }
                    }

                    m_CriticalCount = value;
                }
            }
        }

        /// <summary>
        /// The total number of error severity log messages recorded in the session
        /// </summary>
        public int ErrorCount
        {
            get { return m_ErrorCount; }
            set
            {
                lock (m_Lock)
                {
                    //this is an updatable field, so if we already have the raw data and can update it, lets do that.
                    if (m_LastRawData != null)
                    {
                        //protect against case that should never happen - we have the raw data, but not the offset to the value.
                        if (m_OffsetErrorCount == 0)
                        {
                            m_LastRawData = null;
                        }
                        else
                        {
                            //update the number at that point.
                            byte[] binaryValue = BinarySerializer.SerializeValue(value);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetErrorCount);
                        }
                    }

                    m_ErrorCount = value;
                }
            }
        }

        /// <summary>
        /// The total number of warning severity log messages recorded in the session
        /// </summary>
        public int WarningCount
        {
            get { return m_WarningCount; }
            set
            {
                lock (m_Lock)
                {
                    //this is an updatable field, so if we already have the raw data and can update it, lets do that.
                    if (m_LastRawData != null)
                    {
                        //protect against case that should never happen - we have the raw data, but not the offset to the value.
                        if (m_OffsetWarningCount == 0)
                        {
                            m_LastRawData = null;
                        }
                        else
                        {
                            //update the number at that point.
                            byte[] binaryValue = BinarySerializer.SerializeValue(value);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetWarningCount);
                        }
                    }

                    m_WarningCount = value;
                }
            }
        }

        /// <summary>
        /// The version information of the installed operating system (without service pack or patches)
        /// </summary>
        public Version OSVersion
        {
            get { return m_OSVersion; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_OSVersion = value;
                }
            }
        }

        /// <summary>
        /// The operating system service pack, if any.
        /// </summary>
        public string OSServicePack
        {
            get { return m_OSServicePack; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_OSServicePack = value;
                }
            }
        }

        /// <summary>
        /// The culture name of the underlying operating system installation
        /// </summary>
        public string OSCultureName
        {
            get { return m_OSCultureName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_OSCultureName = value;
                }
            }
        }

        /// <summary>
        /// The OS Platform code, nearly always 1 indicating Windows NT
        /// </summary>
        public int OSPlatformCode
        {
            get { return m_OSPlatformCode; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_OSPlatformCode = value;
                }
            }
        }

        /// <summary>
        /// The OS product type code, used to differentiate specific editions of various operating systems.
        /// </summary>
        public int OSProductType
        {
            get { return m_OSProductTypeCode; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_OSProductTypeCode = value;
                }
            }
        }

        /// <summary>
        /// The OS Suite Mask, used to differentiate specific editions of various operating systems.
        /// </summary>
        public int OSSuiteMask
        {
            get { return m_OSSuiteMaskCode; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_OSSuiteMaskCode = value;
                }
            }
        }

        /// <summary>
        /// The well known operating system family name, like Windows Vista or Windows Server 2003.
        /// </summary>
        public string OSFamilyName
        {
            get
            {
                return string.Empty; // BUG
            }
        }

        /// <summary>
        /// The edition of the operating system without the family name, such as Workstation or Standard Server.
        /// </summary>
        public string OSEditionName
        {
            get
            {
                return string.Empty; // BUG
            }
        }

        /// <summary>
        /// The well known OS name and edition name
        /// </summary>
        public string OSFullName
        {
            get
            {
                return string.Empty; // BUG
            }
        }

        /// <summary>
        /// The well known OS name, edition name, and service pack like Windows XP Professional Service Pack 3
        /// </summary>
        public string OSFullNameWithServicePack
        {
            get
            {
                return string.Empty; // BUG
            }
        }

        /// <summary>
        /// The processor architecture of the operating system.
        /// </summary>
        public ProcessorArchitecture OSArchitecture
        {
            get { return m_OSArchitecture; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_OSArchitecture = value;
                }
            }
        }

        /// <summary>
        /// The boot mode of the operating system.
        /// </summary>
        public OSBootMode OSBootMode
        {
            get { return m_OSBootMode; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_OSBootMode = value;
                }
            }
        }


        /// <summary>
        /// The version of the .NET runtime that the application domain is running as.
        /// </summary>
        public Version RuntimeVersion
        {
            get { return m_RuntimeVersion; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_RuntimeVersion = value;
                }
            }
        }

        /// <summary>
        /// The processor architecture the process is running as.
        /// </summary>
        public ProcessorArchitecture RuntimeArchitecture
        {
            get { return m_RuntimeArchitecture; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_RuntimeArchitecture = value;
                }
            }
        }

        /// <summary>
        /// The current application culture name.
        /// </summary>
        public string CurrentCultureName
        {
            get { return m_CurrentCultureName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_CurrentCultureName = value;
                }
            }
        }

        /// <summary>
        /// The current user interface culture name.
        /// </summary>
        public string CurrentUICultureName
        {
            get { return m_CurrentUICultureName; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_CurrentUICultureName = value;
                }
            }
        }

        /// <summary>
        /// The number of megabytes of installed memory in the host computer.
        /// </summary>
        public int MemoryMB
        {
            get { return m_MemoryMB; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_MemoryMB = value;
                }
            }
        }

        /// <summary>
        /// The number of physical processor sockets in the host computer.
        /// </summary>
        public int Processors
        {
            get { return m_Processors; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_Processors = value;
                }
            }
        }

        /// <summary>
        /// The total number of processor cores in the host computer.
        /// </summary>
        public int ProcessorCores
        {
            get { return m_ProcessorCores; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ProcessorCores = value;
                }
            }
        }

        /// <summary>
        /// Indicates if the session was run in a user interactive mode.
        /// </summary>
        public bool UserInteractive
        {
            get { return m_UserInteractive; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_UserInteractive = value;
                }
            }
        }

        /// <summary>
        /// Indicates if the session was run through terminal server.  Only applies to User Interactive sessions.
        /// </summary>
        public bool TerminalServer
        {
            get { return m_TerminalServer; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_TerminalServer = value;
                }
            }
        }

        /// <summary>
        /// The number of pixels wide of the virtual desktop.
        /// </summary>
        public int ScreenWidth
        {
            get { return m_ScreenWidth; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ScreenWidth = value;
                }
            }
        }

        /// <summary>
        /// The number of pixels tall for the virtual desktop.
        /// </summary>
        public int ScreenHeight
        {
            get { return m_ScreenHeight; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ScreenHeight = value;
                }
            }
        }

        /// <summary>
        /// The number of bits of color depth.
        /// </summary>
        public int ColorDepth
        {
            get { return m_ColorDepth; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_ColorDepth = value;
                }
            }
        }

        /// <summary>
        /// The complete command line used to execute the process including arguments.
        /// </summary>
        public string CommandLine
        {
            get { return m_CommandLine; }
            set
            {
                lock (m_Lock)
                {
                    m_LastRawData = null;
                    m_CommandLine = value;
                }
            }
        }

        /// <summary>
        /// The unique id of the file the session header is associated with
        /// </summary>
        public Guid FileId
        {
            get { return m_FileID; }
            set
            {
                lock (m_Lock)
                {
                    m_FileID = value;
                    m_HasFileInfo = true;
                }
            }
        }

        /// <summary>
        /// The date and time that this file became the active file for the session
        /// </summary>
        public DateTimeOffset FileStartDateTime
        {
            get { return m_FileStartDateTime; }
            set
            {
                lock (m_Lock)
                {
                    m_FileStartDateTime = value;
                }
            }
        }

        /// <summary>
        /// The date and time that this file was no longer the active file for the session.
        /// </summary>
        public DateTimeOffset FileEndDateTime
        {
            get { return m_FileEndDateTime; }
            set
            {
                lock (m_Lock)
                {
                    m_FileEndDateTime = value;
                }
            }
        }

        /// <summary>
        /// The sequence of this file in the set of files for the session
        /// </summary>
        public int FileSequence
        {
            get { return m_FileSequence; }
            set
            {
                lock (m_Lock)
                {
                    m_FileSequence = value;
                }
            }
        }

        /// <summary>
        /// A collection of properties used to provided extended information about the session
        /// </summary>
        public IDictionary<string, string> Properties { get { return m_Properties; } }

        /// <summary>
        /// Optional. Represents the computer that sent the session
        /// </summary>
        public IComputer Computer { get { return null; } }

        /// <summary>
        /// The date and time the session was added to the repository
        /// </summary>
        public DateTimeOffset AddedDateTime { get { return m_SessionStartDateTime; } }

        /// <summary>
        /// The date and time the session was added to the repository
        /// </summary>
        public DateTimeOffset DisplayAddedDateTime { get { return m_SessionStartDateTime; } }

        /// <summary>
        /// The date and time the session was added to the repository
        /// </summary>
        public DateTimeOffset UpdatedDateTime { get { return m_FileEndDateTime; } }

        /// <summary>
        /// The date and time the session was added to the repository
        /// </summary>
        public DateTimeOffset DisplayUpdatedDateTime { get { return m_FileEndDateTime; } }

        /// <summary>
        /// Get a copy of the full session detail this session refers to.  
        /// </summary>
        /// <remarks>Session objects can be large in memory.  This method will return a new object
        /// each time it is called which should be released by the caller as soon as feasible to control memory usage.</remarks>
        ISession ISessionSummary.Session()
        {
            throw new NotSupportedException("Loading a full session from a raw session header isn't supported");
        }

        /// <summary>
        /// True if this is the last file recorded for the session.
        /// </summary>
        public bool IsLastFile
        {
            get { return m_IsLastFile; }
            set
            {
                lock (m_Lock)
                {
                    m_IsLastFile = value;
                }
            }
        }

        /// <summary>
        /// True if the session header is valid (has not been corrupted)
        /// </summary>
        /// <returns></returns>
        public bool IsValid { get { return m_Valid; } }

        /// <summary>
        /// Indicates if the session has ever been viewed or exported
        /// </summary>
        /// <remarks>Changes to this property are not persisted.</remarks>
        public bool IsNew { get; set; }

        /// <summary>
        /// Indicates if the session is currently running and a live stream is available.
        /// </summary>
        public bool IsLive { get; set; }

        /// <summary>
        /// Indicates if session data is available.
        /// </summary>
        /// <remarks>The session summary can be transfered separately from the session details
        /// and isn't subject to pruning so it may be around long before or after the detailed data is.</remarks>
        public bool HasData { get; set; } 

        /// <summary>
        /// True if the session header contains the extended file information
        /// </summary>
        public bool HasFileInfo { get { return m_HasFileInfo; } }

        /// <summary>
        /// Indicates if the binary stream supports fragments or only single-stream transfer (the pre-3.0 format)
        /// </summary>
        public bool SupportsFragments { get { return FileHeader.SupportsFragments(m_MajorVersion, m_MinorVersion); } }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.
        ///                 </param>
        public bool Equals(SessionHeader other)
        {
            // Careful, it could be null; check it without recursion
            if (ReferenceEquals(other, null))
            {
                return false; // Since we're a live object we can't be equal to a null instance.
            }

            return m_SessionId.Equals(other.Id);
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. 
        ///                 </param><exception cref="T:System.NullReferenceException">The <paramref name="obj"/> parameter is null.
        ///                 </exception><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            return Equals(obj as SessionHeader);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            if (m_HashCode == 0)
            {
                CalculateHash();
            }

            return m_HashCode;
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Compare two arrays of an arbitrary object type for equality.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private static bool ArraysAreEqual<T>(T[] source, T[] target)
        {
            if ((source == null) && (target == null))
                return true;

            if ((source == null) || (target == null))
                return false;

            if (source.Length != target.Length)
                return false;

            //now we know they have the same nubmer of elements and neither are null.  we can compare individual elements.
            for(int curItemIndex = 0; curItemIndex < source.Length; curItemIndex++)
            {
                if (source[curItemIndex].Equals(target[curItemIndex]) == false)
                    return false; //it only takes one miss
            }

            return true; //We've checked every element, we're now good to go.
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Performance optimized status converter
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private SessionStatus StatusNameToStatus(string name)
        {
            SessionStatus status = SessionStatus.Unknown;
            //first check using the casing it *should* be
            switch (name)
            {
                case "Running":
                case "running":
                    status = SessionStatus.Running;
                    break;
                case "Normal":
                case "normal":
                    status = SessionStatus.Normal;
                    break;
                case "Crashed":
                case "crashed":
                    status = SessionStatus.Crashed;
                    break;
            }

            //If we got a miss then convert the string and check again.
            if (status == SessionStatus.Unknown)
            {
                switch (name.ToLowerInvariant())
                {
                    case "crashed":
                        status = SessionStatus.Crashed;
                        break;
                    case "normal":
                        status = SessionStatus.Normal;
                        break;
                    case "running":
                        status = SessionStatus.Running;
                        break;
                    case "unknown":
                    default:
                        status = SessionStatus.Unknown;
                        break;
                }
            }

            return status;
        }

        private bool LoadStream(Stream rawData, int length)
        {
            bool isValid = false;
            long startingPosition = rawData.Position;

            //The current file version information.
            BinarySerializer.DeserializeValue(rawData, out m_MajorVersion);

            BinarySerializer.DeserializeValue(rawData, out m_MinorVersion);

            //Now check for compatibility.
            if (m_MajorVersion > FileHeader.DefaultMajorVersion)
            {
                //stop now - we don't know how to decode this.
                return false;
            }

            //Session information
            BinarySerializer.DeserializeValue(rawData, out m_SessionId);

            if (FileHeader.SupportsComputerId(m_MajorVersion, m_MinorVersion))
            {
                Guid rawValue;
                BinarySerializer.DeserializeValue(rawData, out rawValue);
                m_ComputerId = rawValue;
            }

            BinarySerializer.DeserializeValue(rawData, out m_ProductName);
            BinarySerializer.DeserializeValue(rawData, out m_ApplicationName);

            if (FileHeader.SupportsEnvironmentAndPromotion(m_MajorVersion, m_MinorVersion))
            {
                BinarySerializer.DeserializeValue(rawData, out m_EnvironmentName);
                BinarySerializer.DeserializeValue(rawData, out m_PromotionLevelName);
            }

            string applicationVersionRaw;
            BinarySerializer.DeserializeValue(rawData, out applicationVersionRaw);
            m_ApplicationVersion = new Version(applicationVersionRaw);

            BinarySerializer.DeserializeValue(rawData, out m_ApplicationTypeName);
            BinarySerializer.DeserializeValue(rawData, out m_ApplicationDescription);
            BinarySerializer.DeserializeValue(rawData, out m_Caption);
            BinarySerializer.DeserializeValue(rawData, out m_TimeZoneCaption);
            BinarySerializer.DeserializeValue(rawData, out m_SessionStatusName);
            m_SessionStatus = StatusNameToStatus(m_SessionStatusName);

            BinarySerializer.DeserializeValue(rawData, out m_SessionStartDateTime);
            BinarySerializer.DeserializeValue(rawData, out m_SessionEndDateTime);

            string agentVersionRaw;
            BinarySerializer.DeserializeValue(rawData, out agentVersionRaw);
            m_AgentVersion = new Version(agentVersionRaw);

            BinarySerializer.DeserializeValue(rawData, out m_UserName);
            BinarySerializer.DeserializeValue(rawData, out m_UserDomainName);
            BinarySerializer.DeserializeValue(rawData, out m_HostName);
            BinarySerializer.DeserializeValue(rawData, out m_DnsDomainName);
            BinarySerializer.DeserializeValue(rawData, out m_MessageCount);
            BinarySerializer.DeserializeValue(rawData, out m_CriticalCount);
            BinarySerializer.DeserializeValue(rawData, out m_ErrorCount);
            BinarySerializer.DeserializeValue(rawData, out m_WarningCount);

            //The session Details
            BinarySerializer.DeserializeValue(rawData, out m_OSPlatformCode);
            string rawOSVersion;
            BinarySerializer.DeserializeValue(rawData, out rawOSVersion);
            m_OSVersion = new Version(rawOSVersion);

            BinarySerializer.DeserializeValue(rawData, out m_OSServicePack);
            BinarySerializer.DeserializeValue(rawData, out m_OSCultureName);

            string osArchitectureRaw;
            BinarySerializer.DeserializeValue(rawData, out osArchitectureRaw);
            m_OSArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), osArchitectureRaw, true);

            string osBootModeRaw;
            BinarySerializer.DeserializeValue(rawData, out osBootModeRaw);
            m_OSBootMode = (OSBootMode)Enum.Parse(typeof(OSBootMode), osBootModeRaw, true);

            BinarySerializer.DeserializeValue(rawData, out m_OSSuiteMaskCode);
            BinarySerializer.DeserializeValue(rawData, out m_OSProductTypeCode);

            string rawRuntimeVersion;
            BinarySerializer.DeserializeValue(rawData, out rawRuntimeVersion);
            m_RuntimeVersion = new Version(rawRuntimeVersion);

            string runtimeArchitectureRaw;
            BinarySerializer.DeserializeValue(rawData, out runtimeArchitectureRaw);
            m_RuntimeArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), runtimeArchitectureRaw, true);

            BinarySerializer.DeserializeValue(rawData, out m_CurrentCultureName);
            BinarySerializer.DeserializeValue(rawData, out m_CurrentUICultureName);
            BinarySerializer.DeserializeValue(rawData, out m_MemoryMB);
            BinarySerializer.DeserializeValue(rawData, out m_Processors);
            BinarySerializer.DeserializeValue(rawData, out m_ProcessorCores);
            BinarySerializer.DeserializeValue(rawData, out m_UserInteractive);
            BinarySerializer.DeserializeValue(rawData, out m_TerminalServer);
            BinarySerializer.DeserializeValue(rawData, out m_ScreenWidth);
            BinarySerializer.DeserializeValue(rawData, out m_ScreenHeight);
            BinarySerializer.DeserializeValue(rawData, out m_ColorDepth);
            BinarySerializer.DeserializeValue(rawData, out m_CommandLine);

            //now the application properties
            int numberOfProperties;
            BinarySerializer.DeserializeValue(rawData, out numberOfProperties);

            for (int curProperty = 0; curProperty < numberOfProperties; curProperty++)
            {
                string name, value;

                BinarySerializer.DeserializeValue(rawData, out name);
                BinarySerializer.DeserializeValue(rawData, out value);
                m_Properties.Add(name, value);
            }

            //we may have been passed a length or not - if so trust it.
            long remainingLength = (length == 0) ? rawData.Length : length;
            if (rawData.Position < remainingLength - 4)
            {
                m_HasFileInfo = true;

                //BEGIN FILE INFO
                BinarySerializer.DeserializeValue(rawData, out m_FileID);
                BinarySerializer.DeserializeValue(rawData, out m_FileSequence);
                BinarySerializer.DeserializeValue(rawData, out m_FileStartDateTime);
                BinarySerializer.DeserializeValue(rawData, out m_FileEndDateTime);
                BinarySerializer.DeserializeValue(rawData, out m_IsLastFile);
            }

            //now lets get the CRC and check it...
            long dataLength = rawData.Position - startingPosition; //be sure to offset for wherever the stream was when it started

            //make a new copy of the header up to the start of the CRC
            byte[] headerBytes = new byte[dataLength];
            rawData.Position = startingPosition; //...and right here we'll blow up if this isn't a seakable stream.  Be warned.
            rawData.Read(headerBytes, 0, headerBytes.Length);

            //now read the CRC (this will leave the stream in the right position)
            byte[] crcValue = new byte[4];
            rawData.Read(crcValue, 0, crcValue.Length);

            byte[] crcComparision = BinarySerializer.CalculateCRC(headerBytes, headerBytes.Length);

            isValid = ArraysAreEqual(crcComparision, crcValue);

#if DEBUG
            if ((isValid == false) && (Debugger.IsAttached))
            {
                Debugger.Break();
            }
#endif
            SwapStringReferences();

            return isValid;
        }

        private void CalculateHash()
        {
            int myHash = m_SessionId.GetHashCode(); //we're base class so we start the hashing.

            m_HashCode = myHash;
        }


        /// <summary>
        /// Exchange our custom strings for the single instance value from the single instance store.
        /// </summary>
        private void SwapStringReferences()
        {
            lock (m_Lock)
            {
                StringReference.SwapReference(ref m_ProductName);
                StringReference.SwapReference(ref m_ApplicationName);
                StringReference.SwapReference(ref m_EnvironmentName);
                StringReference.SwapReference(ref m_PromotionLevelName);
                StringReference.SwapReference(ref m_ApplicationTypeName);
                StringReference.SwapReference(ref m_ApplicationDescription);
                StringReference.SwapReference(ref m_Caption);
                StringReference.SwapReference(ref m_TimeZoneCaption);
                StringReference.SwapReference(ref m_SessionStatusName);
                StringReference.SwapReference(ref m_UserName);
                StringReference.SwapReference(ref m_UserDomainName);
                StringReference.SwapReference(ref m_HostName);
                StringReference.SwapReference(ref m_DnsDomainName);
                StringReference.SwapReference(ref m_OSServicePack);
                StringReference.SwapReference(ref m_OSCultureName);
                StringReference.SwapReference(ref m_CurrentCultureName);
                StringReference.SwapReference(ref m_CurrentUICultureName);
                StringReference.SwapReference(ref m_CommandLine);
            }
        }

        #endregion
    }
}