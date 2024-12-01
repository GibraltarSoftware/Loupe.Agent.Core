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
    [DebuggerDisplay("{Product} {Application} ({Id})")]
    public sealed class SessionHeader : ISessionSummary, IEquatable<SessionHeader>
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
        private int m_MessageCount; //change these to long on next breaking change
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
        private long m_OffsetSessionEndDateTime;
        private long m_OffsetMessageCount;
        private long m_OffsetCriticalCount;
        private long m_OffsetErrorCount;
        private long m_OffsetWarningCount;

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
            : this(sessionSummary.Properties)
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
            lock (m_Lock)
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
                    m_OffsetSessionEndDateTime = rawData.Position;
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
                    m_OffsetMessageCount = rawData.Position;
                    curValue = BinarySerializer.SerializeValue(m_MessageCount);
                    rawData.Write(curValue, 0, curValue.Length);

                    m_OffsetCriticalCount = rawData.Position;
                    curValue = BinarySerializer.SerializeValue(m_CriticalCount);
                    rawData.Write(curValue, 0, curValue.Length);

                    m_OffsetErrorCount = rawData.Position;
                    curValue = BinarySerializer.SerializeValue(m_ErrorCount);
                    rawData.Write(curValue, 0, curValue.Length);

                    m_OffsetWarningCount = rawData.Position;
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public Uri Uri
        {
            get { throw new NotSupportedException("Links are not supported in this context"); }
        }

        /// <inheritdoc />
        public bool IsComplete { get { return m_HasFileInfo ? m_IsLastFile : false; } }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public Guid? ApplicationEnvironmentId { get; }

        /// <inheritdoc />
        public string ApplicationEnvironmentCaption { get; }

        /// <inheritdoc />
        public Guid? ApplicationEnvironmentServiceId { get; }

        /// <inheritdoc />
        public string ApplicationEnvironmentServiceCaption { get; }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public DateTimeOffset DisplayStartDateTime
        {
            get { return StartDateTime; }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public DateTimeOffset DisplayEndDateTime
        {
            get { return EndDateTime; }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public SessionStatus Status { get { return m_SessionStatus; } }

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

        /// <inheritdoc />
        public long MessageCount
        {
            get { return m_MessageCount; }
            set
            {
                lock (m_Lock)
                {
                    m_MessageCount = value < int.MaxValue ? (int)value : int.MaxValue;

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
                            byte[] binaryValue = BinarySerializer.SerializeValue(m_MessageCount);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetMessageCount);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public long CriticalCount
        {
            get { return m_CriticalCount; }
            set
            {
                lock (m_Lock)
                {
                    m_CriticalCount = value < int.MaxValue ? (int)value : int.MaxValue;

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
                            byte[] binaryValue = BinarySerializer.SerializeValue(m_CriticalCount);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetCriticalCount);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public long ErrorCount
        {
            get { return m_ErrorCount; }
            set
            {
                lock (m_Lock)
                {
                    m_ErrorCount = value < int.MaxValue ? (int)value : int.MaxValue;

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
                            byte[] binaryValue = BinarySerializer.SerializeValue(m_ErrorCount);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetErrorCount);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public long WarningCount
        {
            get { return m_WarningCount; }
            set
            {
                lock (m_Lock)
                {
                    m_WarningCount = value < int.MaxValue ? (int)value : int.MaxValue;

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
                            byte[] binaryValue = BinarySerializer.SerializeValue(m_WarningCount);
                            binaryValue.CopyTo(m_LastRawData, m_OffsetWarningCount);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public string OSFamilyName
        {
            get
            {
                return null; // we don't have a cross-platform way of doing this yet.
            }
        }

        /// <inheritdoc />
        public string OSEditionName
        {
            get
            {
                return null; // we don't have a cross-platform way of doing this yet.
            }
        }

        /// <inheritdoc />
        public string OSFullName
        {
            get
            {
                return null; // we don't have a cross-platform way of doing this yet.
            }
        }

        /// <inheritdoc />
        public string OSFullNameWithServicePack
        {
            get
            {
                return null; // we don't have a cross-platform way of doing this yet.
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public IDictionary<string, string> Properties { get { return m_Properties; } }

        /// <inheritdoc />
        public DateTimeOffset AddedDateTime { get { return m_SessionStartDateTime; } }

        /// <inheritdoc />
        public DateTimeOffset DisplayAddedDateTime { get { return m_SessionStartDateTime; } }

        /// <inheritdoc />
        public DateTimeOffset UpdatedDateTime { get { return m_FileEndDateTime; } }

        /// <inheritdoc />
        public DateTimeOffset DisplayUpdatedDateTime { get { return m_FileEndDateTime; } }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public bool IsNew { get; set; }

        /// <inheritdoc />
        public bool IsLive { get; set; }

        /// <inheritdoc />
        public bool HasData { get; set; }

        /// <summary>
        /// True if the session header contains the extended file information
        /// </summary>
        public bool HasFileInfo { get { return m_HasFileInfo; } }

        /// <summary>
        /// Indicates if the binary stream supports fragments or only single-stream transfer (the pre-3.0 format)
        /// </summary>
        public bool SupportsFragments { get { return FileHeader.SupportsFragments(m_MajorVersion, m_MinorVersion); } }

        /// <inheritdoc />
        public Framework Framework => Framework.DotNet;

        /// <inheritdoc />
        public bool Equals(SessionHeader other)
        {
            // Careful, it could be null; check it without recursion
            if (ReferenceEquals(other, null))
            {
                return false; // Since we're a live object we can't be equal to a null instance.
            }

            return m_SessionId.Equals(other.Id);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as SessionHeader);
        }

        /// <inheritdoc />
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

            //now we know they have the same number of elements and neither are null.  we can compare individual elements.
            for (int curItemIndex = 0; curItemIndex < source.Length; curItemIndex++)
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
            length = (length == 0) ? (int)(rawData.Length - rawData.Position) : length;

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
                BinarySerializer.DeserializeValue(rawData, out Guid rawValue);
                m_ComputerId = rawValue;
            }

            BinarySerializer.DeserializeValue(rawData, out m_ProductName);
            BinarySerializer.DeserializeValue(rawData, out m_ApplicationName);

            if (FileHeader.SupportsEnvironmentAndPromotion(m_MajorVersion, m_MinorVersion))
            {
                BinarySerializer.DeserializeValue(rawData, out m_EnvironmentName);
                BinarySerializer.DeserializeValue(rawData, out m_PromotionLevelName);
            }

            BinarySerializer.DeserializeValue(rawData, out string applicationVersionRaw);
            m_ApplicationVersion = new Version(applicationVersionRaw);

            BinarySerializer.DeserializeValue(rawData, out m_ApplicationTypeName);
            BinarySerializer.DeserializeValue(rawData, out m_ApplicationDescription);
            BinarySerializer.DeserializeValue(rawData, out m_Caption);
            BinarySerializer.DeserializeValue(rawData, out m_TimeZoneCaption);
            BinarySerializer.DeserializeValue(rawData, out m_SessionStatusName);
            m_SessionStatus = StatusNameToStatus(m_SessionStatusName);

            BinarySerializer.DeserializeValue(rawData, out m_SessionStartDateTime);
            BinarySerializer.DeserializeValue(rawData, out m_SessionEndDateTime);

            BinarySerializer.DeserializeValue(rawData, out string agentVersionRaw);
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
            BinarySerializer.DeserializeValue(rawData, out string rawOSVersion);
            m_OSVersion = new Version(rawOSVersion);

            BinarySerializer.DeserializeValue(rawData, out m_OSServicePack);
            BinarySerializer.DeserializeValue(rawData, out m_OSCultureName);

            BinarySerializer.DeserializeValue(rawData, out string osArchitectureRaw);
            m_OSArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), osArchitectureRaw, true);

            BinarySerializer.DeserializeValue(rawData, out string osBootModeRaw);
            m_OSBootMode = (OSBootMode)Enum.Parse(typeof(OSBootMode), osBootModeRaw, true);

            BinarySerializer.DeserializeValue(rawData, out m_OSSuiteMaskCode);
            BinarySerializer.DeserializeValue(rawData, out m_OSProductTypeCode);

            BinarySerializer.DeserializeValue(rawData, out string rawRuntimeVersion);
            m_RuntimeVersion = new Version(rawRuntimeVersion);

            BinarySerializer.DeserializeValue(rawData, out string runtimeArchitectureRaw);
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
            BinarySerializer.DeserializeValue(rawData, out int numberOfProperties);

            for (int curProperty = 0; curProperty < numberOfProperties; curProperty++)
            {
                BinarySerializer.DeserializeValue(rawData, out string name);
                BinarySerializer.DeserializeValue(rawData, out string value);
                m_Properties.Add(name, value);
            }

            //not all headers have file information.  If it doesn't, it'll just have the CRC.
            if ((rawData.Position - startingPosition) < length - 4) //The -4 is for the size of the CRC.
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

#if DEBUG
            if ((dataLength > length - 4) && (Debugger.IsAttached))
            {
                //we read more bytes than our stream expected.  Something is off!
                Debugger.Break();
            }
#endif

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