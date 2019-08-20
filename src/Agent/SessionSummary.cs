using System;
using System.Collections.Generic;
using System.Reflection;
using Loupe.Extensibility.Data;

namespace Loupe.Agent
{
    /// <summary>Summary information about the current session.</summary>
    /// <remarks>
    /// 	<para>The session summary includes all of the information that is available to Loupe
    /// to categorize the session. This includes the product,
    ///     application, and version information that was detected by Loupe (or overridden
    ///     in the application configuration) as well as a range of information about the
    ///     current computing environment (such as Operating System Family and process
    ///     architecture).</para>
    /// 	<para>This information can be referenced at any time by your application.</para>
    /// </remarks>
    [Obsolete("This type is a duplicate and should be removed now that we don't have to isolate Agent and Core.")]
    public sealed class SessionSummary
    {
        private readonly ISessionSummary m_WrappedISessionSummary;
        private Core.Monitor.SessionSummary m_WrappedSummary;
        private readonly Dictionary<string, string> m_Properties;

        /// <summary>
        /// Create a new session summary as the live collection session for the current process
        /// </summary>
        /// <remarks>This constructor figures out all of the summary information when invoked, which can take a moment.</remarks>
        internal SessionSummary(Core.Monitor.SessionSummary summary)
        {
            m_WrappedSummary = summary;
            m_Properties = new Dictionary<string, string>(summary.Properties);
        }

        /// <summary>
        /// Create a new session summary as the live collection session for the current process
        /// </summary>
        /// <remarks>This constructor figures out all of the summary information when invoked, which can take a moment.</remarks>
        internal SessionSummary(ISessionSummary summary)
        {
            m_WrappedISessionSummary = summary;
            m_Properties = new Dictionary<string, string>(summary.Properties);
        }

        #region Public Properties and Methods

        /// <summary>
        /// The unique Id of the session
        /// </summary>
        public Guid Id => m_WrappedSummary != null ? m_WrappedSummary.Id : m_WrappedISessionSummary.Id;

        /// <summary>
        /// The display caption of the time zone where the session was recorded
        /// </summary>
        public string TimeZoneCaption => m_WrappedSummary != null ? m_WrappedSummary.TimeZoneCaption : m_WrappedISessionSummary.TimeZoneCaption;

        /// <summary>
        /// The date and time the session started
        /// </summary>
        public DateTimeOffset StartDateTime => m_WrappedSummary != null ? m_WrappedSummary.StartDateTime : m_WrappedISessionSummary.StartDateTime;

        /// <summary>
        /// The date and time the session ended or was last confirmed running
        /// </summary>
        public DateTimeOffset EndDateTime => m_WrappedSummary != null ? m_WrappedSummary.EndDateTime : m_WrappedISessionSummary.EndDateTime;

        /// <summary>
        /// The time range between the start and end of this session..
        /// </summary>
        public TimeSpan Duration => m_WrappedSummary != null ? m_WrappedSummary.Duration : m_WrappedISessionSummary.Duration;

        /// <summary>
        /// A display caption for the session
        /// </summary>
        public string Caption => m_WrappedSummary != null ? m_WrappedSummary.Caption : m_WrappedISessionSummary.Caption;

        /// <summary>
        /// The product name of the application that recorded the session.
        /// </summary>
        public string Product => m_WrappedSummary != null ? m_WrappedSummary.Product : m_WrappedISessionSummary.Product;

        /// <summary>
        /// The title of the application that recorded the session.
        /// </summary>
        public string Application => m_WrappedSummary != null ? m_WrappedSummary.Application : m_WrappedISessionSummary.Application;

        /// <summary>
        /// Optional.  The environment this session is running in.
        /// </summary>
        /// <remarks>Environments are useful for categorizing sessions, for example to 
        /// indicate the hosting environment. If a value is provided it will be 
        /// carried with the session data to upstream servers and clients.  If the 
        /// corresponding entry does not exist it will be automatically created.</remarks>
        public string Environment => m_WrappedSummary != null ? m_WrappedSummary.Environment : m_WrappedISessionSummary.Environment;

        /// <summary>
        /// Optional.  The promotion level of the session.
        /// </summary>
        /// <remarks>Promotion levels are useful for categorizing sessions, for example to 
        /// indicate whether it was run in development, staging, or production. 
        /// If a value is provided it will be carried with the session data to upstream servers and clients.  
        /// If the corresponding entry does not exist it will be automatically created.</remarks>
        public string PromotionLevel => m_WrappedSummary != null ? m_WrappedSummary.PromotionLevel : m_WrappedISessionSummary.PromotionLevel;

        /// <summary>
        /// The type of process the application ran as.
        /// </summary>
        public ApplicationType ApplicationType => m_WrappedSummary != null ? (ApplicationType)m_WrappedSummary.ApplicationType : (ApplicationType)m_WrappedISessionSummary.ApplicationType;

        /// <summary>
        /// The description of the application from its manifest.
        /// </summary>
        public string ApplicationDescription => m_WrappedSummary != null ? m_WrappedSummary.ApplicationDescription : m_WrappedISessionSummary.ApplicationDescription;

        /// <summary>
        /// The version of the application that recorded the session
        /// </summary>
        public Version ApplicationVersion => m_WrappedSummary != null ? m_WrappedSummary.ApplicationVersion : m_WrappedISessionSummary.ApplicationVersion;

        /// <summary>
        /// The version of the Loupe Agent used to monitor the session
        /// </summary>
        public Version AgentVersion => m_WrappedSummary != null ? m_WrappedSummary.AgentVersion : m_WrappedISessionSummary.AgentVersion;

        /// <summary>
        /// The host name / NetBIOS name of the computer that recorded the session
        /// </summary>
        /// <remarks>Does not include the domain name portion of the fully qualified DNS name.</remarks>
        public string HostName => m_WrappedSummary != null ? m_WrappedSummary.HostName : m_WrappedISessionSummary.HostName;

        /// <summary>
        /// The DNS domain name of the computer that recorded the session.  May be empty.
        /// </summary>
        /// <remarks>Does not include the host name portion of the fully qualified DNS name.</remarks>
        public string DnsDomainName => m_WrappedSummary != null ? m_WrappedSummary.DnsDomainName : m_WrappedISessionSummary.DnsDomainName;

        /// <summary>
        /// The fully qualified user name of the user the application was run as.
        /// </summary>
        public string FullyQualifiedUserName => m_WrappedSummary != null ? m_WrappedSummary.FullyQualifiedUserName : m_WrappedISessionSummary.FullyQualifiedUserName;

        /// <summary>
        /// The user Id that was used to run the session
        /// </summary>
        public string UserName => m_WrappedSummary != null ? m_WrappedSummary.UserName : m_WrappedISessionSummary.UserName;

        /// <summary>
        /// The domain of the user id that was used to run the session
        /// </summary>
        public string UserDomainName => m_WrappedSummary != null ? m_WrappedSummary.UserDomainName : m_WrappedISessionSummary.UserDomainName;

        /// <summary>
        /// The version information of the installed operating system (without service pack or patches)
        /// </summary>
        public Version OSVersion => m_WrappedSummary != null ? m_WrappedSummary.OSVersion : m_WrappedISessionSummary.OSVersion;

        /// <summary>
        /// The operating system service pack, if any.
        /// </summary>
        public string OSServicePack => m_WrappedSummary != null ? m_WrappedSummary.OSServicePack : m_WrappedISessionSummary.OSServicePack;

        /// <summary>
        /// The culture name of the underlying operating system installation
        /// </summary>
        public string OSCultureName => m_WrappedSummary != null ? m_WrappedSummary.OSCultureName : m_WrappedISessionSummary.OSCultureName;

        /// <summary>
        /// The processor architecture of the operating system.
        /// </summary>
        public ProcessorArchitecture OSArchitecture => m_WrappedSummary != null ? m_WrappedSummary.OSArchitecture : m_WrappedISessionSummary.OSArchitecture;

        /// <summary>
        /// The boot mode of the operating system.
        /// </summary>
        public OSBootMode OSBootMode => (OSBootMode) (m_WrappedSummary != null ? m_WrappedSummary.OSBootMode : m_WrappedISessionSummary.OSBootMode);

        /// <summary>
        /// The OS Platform code, nearly always 1 indicating Windows NT
        /// </summary>
        public int OSPlatformCode => m_WrappedSummary != null ? m_WrappedSummary.OSPlatformCode : m_WrappedISessionSummary.OSPlatformCode;

        /// <summary>
        /// The OS product type code, used to differentiate specific editions of various operating systems.
        /// </summary>
        public int OSProductType => m_WrappedSummary != null ? m_WrappedSummary.OSProductType : m_WrappedISessionSummary.OSProductType;

        /// <summary>
        /// The OS Suite Mask, used to differentiate specific editions of various operating systems.
        /// </summary>
        public int OSSuiteMask => m_WrappedSummary != null ? m_WrappedSummary.OSSuiteMask : m_WrappedISessionSummary.OSSuiteMask;

        /// <summary>
        /// The well known operating system family name, like Windows Vista or Windows Server 2003.
        /// </summary>
        public string OSFamilyName => m_WrappedSummary != null ? m_WrappedSummary.OSFamilyName : m_WrappedISessionSummary.OSFamilyName;

        /// <summary>
        /// The edition of the operating system without the family name, such as Workstation or Standard Server.
        /// </summary>
        public string OSEditionName => m_WrappedSummary != null ? m_WrappedSummary.OSEditionName : m_WrappedISessionSummary.OSEditionName;

        /// <summary>
        /// The well known OS name and edition name
        /// </summary>
        public string OSFullName => m_WrappedSummary != null ? m_WrappedSummary.OSFullName : m_WrappedISessionSummary.OSFullName;

        /// <summary>
        /// The well known OS name, edition name, and service pack like Windows XP Professional Service Pack 3
        /// </summary>
        public string OSFullNameWithServicePack => m_WrappedSummary != null ? m_WrappedSummary.OSFullNameWithServicePack : m_WrappedISessionSummary.OSFullNameWithServicePack;

        /// <summary>
        /// The version of the .NET runtime that the application domain is running as.
        /// </summary>
        public Version RuntimeVersion => m_WrappedSummary != null ? m_WrappedSummary.RuntimeVersion : m_WrappedISessionSummary.RuntimeVersion;

        /// <summary>
        /// The processor architecture the process is running as.
        /// </summary>
        public ProcessorArchitecture RuntimeArchitecture => m_WrappedSummary != null ? m_WrappedSummary.RuntimeArchitecture : m_WrappedISessionSummary.RuntimeArchitecture;

        /// <summary>
        /// The current application culture name.
        /// </summary>
        public string CurrentCultureName => m_WrappedSummary != null ? m_WrappedSummary.CurrentCultureName : m_WrappedISessionSummary.CurrentCultureName;

        /// <summary>
        /// The current user interface culture name.
        /// </summary>
        public string CurrentUICultureName => m_WrappedSummary != null ? m_WrappedSummary.CurrentUICultureName : m_WrappedISessionSummary.CurrentUICultureName;

        /// <summary>
        /// The number of megabytes of installed memory in the host computer.
        /// </summary>
        public int MemoryMB => m_WrappedSummary != null ? m_WrappedSummary.MemoryMB : m_WrappedISessionSummary.MemoryMB;

        /// <summary>
        /// The number of physical processor sockets in the host computer.
        /// </summary>
        public int Processors => m_WrappedSummary != null ? m_WrappedSummary.Processors : m_WrappedISessionSummary.Processors;

        /// <summary>
        /// The total number of processor cores in the host computer.
        /// </summary>
        public int ProcessorCores => m_WrappedSummary != null ? m_WrappedSummary.ProcessorCores : m_WrappedISessionSummary.ProcessorCores;

        /// <summary>
        /// Indicates if the session was run in a user interactive mode.
        /// </summary>
        public bool UserInteractive => m_WrappedSummary != null ? m_WrappedSummary.UserInteractive : m_WrappedISessionSummary.UserInteractive;

        /// <summary>
        /// Indicates if the session was run through terminal server.  Only applies to User Interactive sessions.
        /// </summary>
        public bool TerminalServer => m_WrappedSummary != null ? m_WrappedSummary.TerminalServer : m_WrappedISessionSummary.TerminalServer;

        /// <summary>
        /// The number of pixels wide of the virtual desktop.
        /// </summary>
        public int ScreenWidth => m_WrappedSummary != null ? m_WrappedSummary.ScreenWidth : m_WrappedISessionSummary.ScreenWidth;

        /// <summary>
        /// The number of pixels tall for the virtual desktop.
        /// </summary>
        public int ScreenHeight => m_WrappedSummary != null ? m_WrappedSummary.ScreenHeight : m_WrappedISessionSummary.ScreenHeight;

        /// <summary>
        /// The number of bits of color depth.
        /// </summary>
        public int ColorDepth => m_WrappedSummary != null ? m_WrappedSummary.ColorDepth : m_WrappedISessionSummary.ColorDepth;

        /// <summary>
        /// The complete command line used to execute the process including arguments.
        /// </summary>
        public string CommandLine => m_WrappedSummary != null ? m_WrappedSummary.CommandLine : m_WrappedISessionSummary.CommandLine;


        /// <summary>
        /// The final status of the session.
        /// </summary>
        public SessionStatus Status => m_WrappedSummary != null ? (SessionStatus)m_WrappedSummary.Status : (SessionStatus)m_WrappedISessionSummary.Status;

        /// <summary>
        /// The number of messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public int MessageCount => m_WrappedSummary != null ? m_WrappedSummary.MessageCount : m_WrappedISessionSummary.MessageCount;

        /// <summary>
        /// The number of critical messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public int CriticalCount => m_WrappedSummary != null ? m_WrappedSummary.CriticalCount : m_WrappedISessionSummary.CriticalCount;

        /// <summary>
        /// The number of error messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public int ErrorCount => m_WrappedSummary != null ? m_WrappedSummary.ErrorCount : m_WrappedISessionSummary.ErrorCount;

        /// <summary>
        /// The number of warning messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public int WarningCount => m_WrappedSummary != null ? m_WrappedSummary.WarningCount : m_WrappedISessionSummary.WarningCount;

        /// <summary>
        /// A copy of the collection of application specific properties. (Set via configuration at logging startup.  Do not modify here.)
        /// </summary>
        public Dictionary<string, string> Properties => m_Properties;

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Ensures that the provided object is used as the wrapped object.
        /// </summary>
        /// <param name="summary"></param>
        internal void SyncWrappedObject(Core.Monitor.SessionSummary summary)
        {
            if (ReferenceEquals(summary, m_WrappedSummary) == false)
            {
                m_WrappedSummary = summary;
            }
        }

        #endregion
    }
}