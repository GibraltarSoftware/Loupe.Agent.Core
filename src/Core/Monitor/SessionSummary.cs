using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Loupe.Data;
using Loupe.Monitor.Serialization;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Loupe.Monitor
{
    /// <summary>
    /// Summary information about the entire session.
    /// </summary>
    /// <remarks>This information is available from sessions without loading the entire session into memory.</remarks>
    public class SessionSummary: ISessionSummary
    {
        /// <summary>
        /// A default value for when the product name is unknown.
        /// </summary>
        public const string UnknownProduct = "Unknown Product";

        /// <summary>
        /// A default value for when the application name is unknown.
        /// </summary>
        public const string UnknownApplication = "Unknown Application";

        private readonly bool m_IsLive;
        private readonly SessionSummaryPacket m_Packet;
        private int m_CriticalCount;
        private int m_ErrorCount;
        private int m_WarningCount;
        private int m_MessageCount;
        volatile private SessionStatus m_SessionStatus;
        private ApplicationType m_AgentAppType;

        private readonly bool m_PrivacyEnabled;

        /// <summary>
        /// Raised whenever a property changes on the object
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Create a new session summary as the live collection session for the current process
        /// </summary>
        /// <remarks>This constructor figures out all of the summary information when invoked, which can take a moment.</remarks>
        internal SessionSummary(AgentConfiguration configuration)
        {
            m_IsLive = true;
            m_Packet = new SessionSummaryPacket();
            m_SessionStatus = SessionStatus.Running;

            m_PrivacyEnabled = configuration.Publisher.EnableAnonymousMode;

            try
            {
                m_Packet.ID = Guid.NewGuid();
                m_Packet.Caption = null;

                //this stuff all tends to succeed
                if (m_PrivacyEnabled)
                {
                    m_Packet.UserName = string.Empty;
                    m_Packet.UserDomainName = string.Empty;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    m_Packet.UserName = System.Environment.GetEnvironmentVariable("USERNAME") ?? string.Empty;
                    m_Packet.UserDomainName = System.Environment.GetEnvironmentVariable("USERDOMAIN") ?? string.Empty;
                }
                else
                {
                    m_Packet.UserName = System.Environment.GetEnvironmentVariable("USER") ?? string.Empty;
                    m_Packet.UserDomainName = System.Environment.GetEnvironmentVariable("HOSTNAME") ?? string.Empty;
                }

                m_Packet.TimeZoneCaption = TimeZoneInfo.Local.StandardName;
                m_Packet.EndDateTime = StartDateTime; //we want to ALWAYS have an end time, and since we just created our start time we need to move that over to end time

                //this stuff, on the other hand, doesn't always succeed

                //Lets see if the user has already picked some things for us...
                PublisherConfiguration publisherConfig = configuration.Publisher;
                string productName = null, applicationName = null, applicationDescription = null;
                Version applicationVersion = null;

                //what kind of process are we?
                if (publisherConfig.ApplicationType != ApplicationType.Unknown)
                {
                    // They specified an application type, so just use that.
                    m_AgentAppType = publisherConfig.ApplicationType; // Start with the type they specified.
                }

                m_Packet.ApplicationType = m_AgentAppType; // Finally, set the application type from our determined type.
                if (m_AgentAppType != ApplicationType.AspNet)
                {
                    //we want to find our entry assembly and get default product/app info from it.
                    GetApplicationNameSafe(out productName, out applicationName, out applicationVersion, out applicationDescription);                    
                }

                //OK, now apply configuration overrides or what we discovered...
                m_Packet.ProductName = string.IsNullOrEmpty(publisherConfig.ProductName) ? productName : publisherConfig.ProductName;
                m_Packet.ApplicationName = string.IsNullOrEmpty(publisherConfig.ApplicationName) ? applicationName : publisherConfig.ApplicationName;
                m_Packet.ApplicationVersion = publisherConfig.ApplicationVersion ?? applicationVersion;
                m_Packet.ApplicationDescription = string.IsNullOrEmpty(publisherConfig.ApplicationDescription) ? applicationDescription : publisherConfig.ApplicationDescription;
                m_Packet.EnvironmentName = publisherConfig.EnvironmentName;
                m_Packet.PromotionLevelName = publisherConfig.PromotionLevelName;

                //Finally, no nulls allowed! Fix any...
                m_Packet.ProductName = string.IsNullOrEmpty(m_Packet.ProductName) ? "Unknown" : m_Packet.ProductName;
                m_Packet.ApplicationName = string.IsNullOrEmpty(m_Packet.ApplicationName) ? "Unknown" : m_Packet.ApplicationName;
                m_Packet.ApplicationVersion = m_Packet.ApplicationVersion ?? new Version(0, 0);
                m_Packet.ApplicationDescription = m_Packet.ApplicationDescription ?? string.Empty;
                m_Packet.EnvironmentName = m_Packet.EnvironmentName ?? string.Empty;
                m_Packet.PromotionLevelName = m_Packet.PromotionLevelName ?? string.Empty;

                m_Packet.ComputerId = GetComputerIdSafe(m_Packet.ProductName, configuration);
                m_Packet.AgentVersion = GetAgentVersionSafe();
            }
            catch (Exception ex)
            {
                //we really don't want an init error to fail us, not here!
                GC.KeepAlive(ex);
            }

            if (m_PrivacyEnabled == false)
            {
                try
                {
                    IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                    m_Packet.HostName = ipGlobalProperties.HostName;
                    m_Packet.DnsDomainName = ipGlobalProperties.DomainName ?? string.Empty;
                }
                catch
                {
                    //fallback to environment names
                    try
                    {
                        m_Packet.HostName = System.Environment.MachineName;
                    }
                    catch (Exception ex)
                    {
                        //we really don't want an init error to fail us, not here!
                        GC.KeepAlive(ex);

                        m_Packet.HostName = "unknown";
                    }
                    m_Packet.DnsDomainName = string.Empty;
                }
            }
            else
            {
                // Privacy mode.  Don't store "personally-identifying information".
                m_Packet.HostName = "anonymous";
                m_Packet.DnsDomainName = string.Empty;
            }

#if (NETCOREAPP2_0 || NETSTANDARD2_0)
            var os = System.Environment.OSVersion;
            m_Packet.OSPlatformCode = (int) os.Platform; //we copied this enum for our value.
            m_Packet.OSVersion = os.Version;
            m_Packet.OSServicePack = os.ServicePack;
#else
            var osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                m_Packet.OSPlatformCode = 2; //Win32NT
                m_Packet.OSVersion = new Version(0,0); // BUG
                m_Packet.OSServicePack = string.Empty; // BUG
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                m_Packet.OSPlatformCode = 4; //Unix
                m_Packet.OSVersion = new Version(0, 0); // BUG
                m_Packet.OSServicePack = string.Empty; // BUG
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                m_Packet.OSPlatformCode = 6; //OSX
                m_Packet.OSVersion = new Version(0, 0); // BUG
                m_Packet.OSServicePack = string.Empty; // BUG
            }
#endif

            try
            {
                m_Packet.OSArchitecture = RuntimeInformation.OSArchitecture.ToProcessorArchitecture();
                m_Packet.RuntimeArchitecture = RuntimeInformation.ProcessArchitecture.ToProcessorArchitecture();

                m_Packet.OSCultureName = CultureInfo.CurrentUICulture.ToString();
                m_Packet.CurrentCultureName = CultureInfo.CurrentCulture.ToString();
                m_Packet.CurrentUICultureName = CultureInfo.CurrentUICulture.ToString();

                m_Packet.OSBootMode = OSBootMode.Normal;

                m_Packet.Processors = System.Environment.ProcessorCount;
                m_Packet.ProcessorCores = m_Packet.Processors; //BUG

                try
                {
                    var frameworkDescription = RuntimeInformation.FrameworkDescription;

                    //hopefully this has a version number in it...
                    //Thanks to SO.. https://stackoverflow.com/questions/6618868/regular-expression-for-version-numbers
                    var versionMatch = Regex.Match(frameworkDescription, @"\d+(?:\.\d+)+");
                    if (versionMatch.Success)
                    {
                        if (Version.TryParse(versionMatch.Value, out var version))
                        {
                            m_Packet.RuntimeVersion = version;
                        }
                        else
                        {
                            m_Packet.RuntimeVersion = new Version(0, 0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
                    m_Packet.RuntimeVersion = new Version(0, 0);
                }


                m_Packet.MemoryMB = 0; // BUG
#if (NETCOREAPP2_0 || NETSTANDARD2_0)
                m_Packet.UserInteractive = System.Environment.UserInteractive;
#else
                m_Packet.UserInteractive = false; 
#endif

                //find the active screen resolution
                if (m_AgentAppType == ApplicationType.Windows)
                {
                    //We don't know if we can reliably get these on .NET Core.
                    m_Packet.TerminalServer = false;
                    m_Packet.ColorDepth = 0;
                    m_Packet.ScreenHeight = 0;
                    m_Packet.ScreenWidth = 0;
                }

                if (m_PrivacyEnabled)
                {
                    m_Packet.CommandLine = string.Empty;
                }
                else
                {
                    //.NET Core doesn't expose the command line because of concerns over how it is handled cross-platform.
                    var commandArgs = System.Environment.GetCommandLineArgs();
                    m_Packet.CommandLine = string.Join(" ", commandArgs); //the first arg is the executable name
                }
            }
            catch (Exception ex)
            {
                //we really don't want an init error to fail us, not here!
                GC.KeepAlive(ex);
            }

            //now do user defined properties
            try
            {
                foreach (KeyValuePair<string, string> keyValuePair in configuration.Properties)
                {
                    m_Packet.Properties.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
            catch (Exception ex)
            {
                //we aren't expecting any errors, but best be safe.
                GC.KeepAlive(ex);
            }

            m_Packet.Caption = m_Packet.ApplicationName;
        }

        internal SessionSummary(SessionSummaryPacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            m_Packet = packet;
            m_SessionStatus = SessionStatus.Unknown; //it should be set for us in a minute...
        }

#region Public Properties and Methods

        /// <summary>
        /// Overrides the native recorded product and application information with the specified values to reflect the server rules.
        /// </summary>
        public void ApplyMappingOverrides(string productName, string applicationName, Version applicationVersion, string environmentName, string promotionLevelName)
        {
            m_Packet.ProductName = productName;
            m_Packet.ApplicationName = applicationName;
            m_Packet.ApplicationVersion = applicationVersion;
            m_Packet.EnvironmentName = environmentName;
            m_Packet.PromotionLevelName = promotionLevelName;

            m_Packet.Caption = applicationName; // this is what the packet constructor does.
        }

        /// <summary>
        /// Get a copy of the full session detail this session refers to.  
        /// </summary>
        /// <remarks>Session objects can be large in memory.  This method will return a new object
        /// each time it is called which should be released by the caller as soon as feasible to control memory usage.</remarks>
        ISession ISessionSummary.Session()
        {
            throw new NotSupportedException("Retrieving the full session from a SessionSummary is not supported");
        }

        /// <summary>
        /// The unique Id of the session
        /// </summary>
        public Guid Id => m_Packet.ID;

        /// <summary>
        /// The link to this item on the server
        /// </summary>
        public Uri Uri => throw new NotSupportedException("Links are not supported in this context");

        /// <summary>
        /// Indicates if the session has ever been viewed or exported
        /// </summary>
        bool ISessionSummary.IsNew => true;

        /// <summary>
        /// Indicates if all of the session data is stored that is expected to be available
        /// </summary>
        bool ISessionSummary.IsComplete => true;

        /// <summary>
        /// Indicates if the session is currently running and a live stream is available.
        /// </summary>
        bool ISessionSummary.IsLive => false;

        /// <summary>
        /// Indicates if session data is available.
        /// </summary>
        /// <remarks>The session summary can be transfered separately from the session details
        /// and isn't subject to pruning so it may be around long before or after the detailed data is.</remarks>
        bool ISessionSummary.HasData //we are just a header, we presume we stand alone.
            => false;

        /// <summary>
        /// The unique Id of the local computer.
        /// </summary>
        public Guid? ComputerId => m_Packet.ComputerId;

        /// <summary>
        /// The display caption of the time zone where the session was recorded
        /// </summary>
        public string TimeZoneCaption => m_Packet.TimeZoneCaption;

        /// <summary>
        /// The date and time the session started
        /// </summary>
        public DateTimeOffset StartDateTime => m_Packet.Timestamp;

        /// <summary>
        /// The date and time the session started
        /// </summary>
        public DateTimeOffset DisplayStartDateTime => StartDateTime;

        /// <summary>
        /// The date and time the session ended or was last confirmed running
        /// </summary>
        public DateTimeOffset EndDateTime
        {
            get
            {
                if (m_IsLive)
                {
                    //we're the live session and still kicking - we haven't ended yet!
                    m_Packet.EndDateTime = DateTimeOffset.Now;
                }

                return m_Packet.EndDateTime;
            } 

            internal set => m_Packet.EndDateTime = value;
        }

        /// <summary>
        /// The date and time the session ended or was last confirmed running in the time zone the user has requested for display
        /// </summary>
        public DateTimeOffset DisplayEndDateTime => EndDateTime;

        /// <summary>
        /// The time range between the start and end of this session, or the last message logged if the session ended unexpectedly.
        /// </summary>
        public TimeSpan Duration => EndDateTime - StartDateTime;

        /// <summary>
        /// The date and time the session was added to the repository
        /// </summary>
        DateTimeOffset ISessionSummary.AddedDateTime => StartDateTime;

        /// <summary>
        /// The date and time the session was added to the repository in the time zone the user has requested for display
        /// </summary>
        DateTimeOffset ISessionSummary.DisplayAddedDateTime => StartDateTime;

        /// <summary>
        /// The date and time the session was added to the repository
        /// </summary>
        DateTimeOffset ISessionSummary.UpdatedDateTime => EndDateTime;

        /// <summary>
        /// The date and time the session header was last updated locally in the time zone the user has requested for display
        /// </summary>
        DateTimeOffset ISessionSummary.DisplayUpdatedDateTime => EndDateTime;

        /// <summary>
        /// The time range between the start and end of this session, or the last message logged if the session ended unexpectedly.
        /// Formatted as a string in HH:MM:SS format.
        /// </summary>
        public string DurationShort
        {
            get
            {
                string formattedDuration;

                TimeSpan duration = Duration;

                //we have to format it manually; I couldn't find anything built-in that would format a timespan.
                if (duration.Days > 0)
                {
                    // It spans at least a day, so put Days in front, too
                    formattedDuration = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}:{3:00}",
                                                      duration.Days, duration.Hours, duration.Minutes, duration.Seconds);
                }
                else
                {
                    // It spans less than a day, so leave Days off
                    formattedDuration = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                                                      duration.Hours, duration.Minutes, duration.Seconds);
                }

                return formattedDuration;
            }
        }


        /// <summary>
        /// A display caption for the session
        /// </summary>
        public string Caption
        {
            get => m_Packet.Caption;
            set
            {
                if (m_Packet.Caption != value)
                {
                    m_Packet.Caption = value;

                    //and signal that we changed a property we expose
                    SendPropertyChanged("Caption");
                }
            }
        }

        /// <summary>
        /// The product name of the application that recorded the session
        /// </summary>
        public string Product => m_Packet.ProductName;

        /// <summary>
        /// The title of the application that recorded the session
        /// </summary>
        public string Application => m_Packet.ApplicationName;

        /// <summary>
        /// Optional.  The environment this session is running in.
        /// </summary>
        /// <remarks>Environments are useful for categorizing sessions, for example to 
        /// indicate the hosting environment. If a value is provided it will be 
        /// carried with the session data to upstream servers and clients.  If the 
        /// corresponding entry does not exist it will be automatically created.</remarks>
        public string Environment => m_Packet.EnvironmentName;

        /// <summary>
        /// Optional.  The promotion level of the session.
        /// </summary>
        /// <remarks>Promotion levels are useful for categorizing sessions, for example to 
        /// indicate whether it was run in development, staging, or production. 
        /// If a value is provided it will be carried with the session data to upstream servers and clients.  
        /// If the corresponding entry does not exist it will be automatically created.</remarks>
        public string PromotionLevel => m_Packet.PromotionLevelName;

        /// <summary>
        /// The type of process the application ran as (as declared or detected for recording).  (See AgentAppType for internal Agent use.)
        /// </summary>
        public ApplicationType ApplicationType => m_Packet.ApplicationType;

        /// <summary>
        /// The type of process the application ran as (as seen by the Agent internally).
        /// </summary>
        public ApplicationType AgentAppType => m_AgentAppType;

        /// <summary>
        /// The description of the application from its manifest.
        /// </summary>
        public string ApplicationDescription => m_Packet.ApplicationDescription;

        /// <summary>
        /// The version of the application that recorded the session
        /// </summary>
        public Version ApplicationVersion => m_Packet.ApplicationVersion;

        /// <summary>
        /// The version of the Loupe Agent used to monitor the session
        /// </summary>
        public Version AgentVersion => m_Packet.AgentVersion;

        /// <summary>
        /// The host name / NetBIOS name of the computer that recorded the session
        /// </summary>
        /// <remarks>Does not include the domain name portion of the fully qualified DNS name.</remarks>
        public string HostName => m_Packet.HostName;

        /// <summary>
        /// The DNS domain name of the computer that recorded the session.  May be empty.
        /// </summary>
        /// <remarks>Does not include the host name portion of the fully qualified DNS name.</remarks>
        public string DnsDomainName => m_Packet.DnsDomainName;

        /// <summary>
        /// The fully qualified user name of the user the application was run as.
        /// </summary>
        public string FullyQualifiedUserName => m_Packet.FullyQualifiedUserName;

        /// <summary>
        /// The user Id that was used to run the session
        /// </summary>
        public string UserName => m_Packet.UserName;

        /// <summary>
        /// The domain of the user id that was used to run the session
        /// </summary>
        public string UserDomainName => m_Packet.UserDomainName;

        /// <summary>
        /// The version information of the installed operating system (without service pack or patches)
        /// </summary>
        public Version OSVersion => m_Packet.OSVersion;

        /// <summary>
        /// The operating system service pack, if any.
        /// </summary>
        public string OSServicePack => m_Packet.OSServicePack;

        /// <summary>
        /// The culture name of the underlying operating system installation
        /// </summary>
        public string OSCultureName => m_Packet.OSCultureName;

        /// <summary>
        /// The processor architecture of the operating system.
        /// </summary>
        public ProcessorArchitecture OSArchitecture => m_Packet.OSArchitecture;

        /// <summary>
        /// The boot mode of the operating system.
        /// </summary>
        public OSBootMode OSBootMode => m_Packet.OSBootMode;

        /// <summary>
        /// The OS Platform code, nearly always 1 indicating Windows NT
        /// </summary>
        public int OSPlatformCode => m_Packet.OSPlatformCode;

        /// <summary>
        /// The OS product type code, used to differentiate specific editions of various operating systems.
        /// </summary>
        public int OSProductType => m_Packet.OSProductType;

        /// <summary>
        /// The OS Suite Mask, used to differentiate specific editions of various operating systems.
        /// </summary>
        public int OSSuiteMask => m_Packet.OSSuiteMask;

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
        /// The version of the .NET runtime that the application domain is running as.
        /// </summary>
        public Version RuntimeVersion => m_Packet.RuntimeVersion;

        /// <summary>
        /// The processor architecture the process is running as.
        /// </summary>
        public ProcessorArchitecture RuntimeArchitecture => m_Packet.RuntimeArchitecture;

        /// <summary>
        /// The current application culture name.
        /// </summary>
        public string CurrentCultureName => m_Packet.CurrentCultureName;

        /// <summary>
        /// The current user interface culture name.
        /// </summary>
        public string CurrentUICultureName => m_Packet.CurrentUICultureName;

        /// <summary>
        /// The number of megabytes of installed memory in the host computer.
        /// </summary>
        public int MemoryMB => m_Packet.MemoryMB;

        /// <summary>
        /// The number of physical processor sockets in the host computer.
        /// </summary>
        public int Processors => m_Packet.Processors;

        /// <summary>
        /// The total number of processor cores in the host computer.
        /// </summary>
        public int ProcessorCores => m_Packet.ProcessorCores;

        /// <summary>
        /// Indicates if the session was run in a user interactive mode.
        /// </summary>
        public bool UserInteractive => m_Packet.UserInteractive;

        /// <summary>
        /// Indicates if the session was run through terminal server.  Only applies to User Interactive sessions.
        /// </summary>
        public bool TerminalServer => m_Packet.TerminalServer;

        /// <summary>
        /// The number of pixels wide of the virtual desktop.
        /// </summary>
        public int ScreenWidth => m_Packet.ScreenWidth;

        /// <summary>
        /// The number of pixels tall for the virtual desktop.
        /// </summary>
        public int ScreenHeight => m_Packet.ScreenHeight;

        /// <summary>
        /// The number of bits of color depth.
        /// </summary>
        public int ColorDepth => m_Packet.ColorDepth;

        /// <summary>
        /// The complete command line used to execute the process including arguments.
        /// </summary>
        public string CommandLine => m_Packet.CommandLine;


        /// <summary>
        /// The final status of the session.
        /// </summary>
        public SessionStatus Status { get => m_SessionStatus;
            internal set => m_SessionStatus = value;
        }

            /// <summary>
        /// The number of messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public int MessageCount { get => m_MessageCount;
                internal set => m_MessageCount = value;
            }

        /// <summary>
        /// The number of critical messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public int CriticalCount { get => m_CriticalCount;
            internal set => m_CriticalCount = value;
        }

        /// <summary>
        /// The number of error messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public int ErrorCount { get => m_ErrorCount;
            internal set => m_ErrorCount = value;
        }

        /// <summary>
        /// The number of error messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        public int WarningCount { get => m_WarningCount;
            internal set => m_WarningCount = value;
        }

        /// <summary>
        /// A collection of application specific properties.
        /// </summary>
        public IDictionary<string, string> Properties => m_Packet.Properties;

        /// <summary>
        /// Optional. Represents the computer that sent the session
        /// </summary>
        public IComputer Computer => null;

        /// <summary>
        /// Generates a reasonable default caption for the provided session that has no caption
        /// </summary>
        /// <param name="sessionSummary">The session summary object to generate a default caption for</param>
        /// <returns>The default caption</returns>
        public static string DefaultCaption(SessionSummary sessionSummary)
        {
            string defaultCaption = string.Empty;

            //We are currently shooting for <appname> <Short Date> <Short time>
            if (string.IsNullOrEmpty(sessionSummary.Application))
            {
                defaultCaption += "(Unknown app)";
            }
            else
            {
                //we want to truncate the application if it's over a max length
                if (sessionSummary.Application.Length > 32)
                {
                    defaultCaption += sessionSummary.Application.Substring(0, 32);
                }
                else
                {
                    defaultCaption += sessionSummary.Application;
                }
            }

            defaultCaption += " " + sessionSummary.StartDateTime.ToString("d");

            defaultCaption += " " + sessionSummary.StartDateTime.ToString("t");

            return defaultCaption;
        }

#endregion

#region Internal Properties and Methods

        internal SessionSummaryPacket Packet => m_Packet;

        internal bool PrivacyEnabled => m_PrivacyEnabled;

        /// <summary>
        /// Inspect the provided packet to update relevant statistics
        /// </summary>
        /// <param name="packet">A Log message packet to count</param>
        internal void UpdateMessageStatistics(LogMessagePacket packet)
        {
            m_MessageCount++;

            switch (packet.Severity)
            {
                case LogMessageSeverity.Critical:
                    m_CriticalCount++;
                    break;
                case LogMessageSeverity.Error:
                    m_ErrorCount++;
                    break;
                case LogMessageSeverity.Warning:
                    m_WarningCount++;
                    break;
            }
        }

        /// <summary>
        /// Clear the existing statistic counters
        /// </summary>
        /// <remarks>Typically used before the messages are recounted to ensure
        /// they can be correctly updated.</remarks>
        internal void ClearMessageStatistics()
        {
            m_MessageCount = 0;
            m_CriticalCount = 0;
            m_ErrorCount = 0;
            m_WarningCount = 0;
        }

#endregion

#region Private Properties and Methods

        private static Version GetAgentVersionSafe()
        {
            Version version = new Version(4, 0);

            return version;
        }

        private static Guid GetComputerIdSafe(string product, AgentConfiguration configuration)
        {
            Guid computerId = Guid.Empty;  //we can't fail, this is a good default value since upstream items will treat it as a "don't know"
            try
            {
                //first see if we have a GUID file in the system-wide location.
                var preferredPath = PathManager.FindBestPath(PathType.Collection);
                var computerIdFile = Path.Combine(preferredPath, LocalRepository.ComputerKeyFile);

                if (!File.Exists(computerIdFile))
                {
                    //see if we have a repository Id we should copy...
                    var repositoryPath = LocalRepository.CalculateRepositoryPath(product, configuration.SessionFile.Folder);
                    var repositoryIdFile = Path.Combine(repositoryPath, LocalRepository.RepositoryKeyFile);
                    if (File.Exists(repositoryIdFile))
                    {
                        //try to copy it as a candidate..
                        try
                        {
                            File.Copy(repositoryIdFile, computerIdFile, false);
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            if (Debugger.IsAttached)
                                Debugger.Break();
#endif
                            GC.KeepAlive(ex);
                        }
                    }
                }

                if (File.Exists(computerIdFile))
                {
                    //read back the existing repository id
                    string rawComputerId = File.ReadAllText(computerIdFile, Encoding.UTF8);
                    computerId = new Guid(rawComputerId);
                }

                //create a new repository id
                if (computerId == Guid.Empty)
                {
                    computerId = Guid.NewGuid();
                    File.WriteAllText(computerIdFile, computerId.ToString(), Encoding.UTF8);
                    File.SetAttributes(computerIdFile, FileAttributes.Hidden);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif
                GC.KeepAlive(ex);
                computerId = Guid.Empty; //we shouldn't trust anything we have- it's probably a dynamically created id.
            }

            return computerId;
        }

        /// <summary>
        /// Determine the correct application name for logging purposes of the current process.
        /// </summary>
        /// <param name="productName">The product name the logging system will use for this process.</param>
        /// <param name="applicationName">The application name the logging system will use for this process.</param>
        /// <param name="applicationVersion">The version of the application the logging system will use for this process.</param>
        /// <param name="applicationDescription">A description of the current application.</param>
        /// <remarks>This method isn't the complete story; the SessionSummary constructor has a more complete mechanism that takes into account
        /// the full scope of overrides.  This method will not throw exceptions if it is unable to determine suitable values.  Instead, default values of product name 'Unknown Product'
        /// application name 'Unknown Application', version 0.0, and an empty description will be used.</remarks>
        private static void GetApplicationNameSafe(out string productName, out string applicationName, out Version applicationVersion, out string applicationDescription)
        {
            productName = UnknownProduct;
            applicationName = UnknownApplication;
            applicationVersion = new Version(0, 0);
            applicationDescription = string.Empty;

            try
            {
                //we generally work off the top executable that started the whole thing.
                var topAssembly = Assembly.GetEntryAssembly();

                if (topAssembly == null)
                {
                    //we must be either ASP.NET or some bizarre reflected environment.
                    return;
                }
                else
                {
                    //the version isn't a custom attribute so we can get it directly.
                    applicationVersion = topAssembly.GetName().Version;

                    //now get the attributes we need.
                    AssemblyFileVersionAttribute[] fileVersionAttributes = topAssembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute)) as AssemblyFileVersionAttribute[];
                    AssemblyProductAttribute[] productAttributes = topAssembly.GetCustomAttributes(typeof(AssemblyProductAttribute)) as AssemblyProductAttribute[];
                    AssemblyTitleAttribute[] titleAttributes = topAssembly.GetCustomAttributes(typeof(AssemblyTitleAttribute)) as AssemblyTitleAttribute[];
                    AssemblyDescriptionAttribute[] descriptionAttributes = topAssembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute)) as AssemblyDescriptionAttribute[];

                    //and interpret this information
                    if ((fileVersionAttributes != null) && (fileVersionAttributes.Length > 0))
                    {
                        //try to parse this value (it's text so it might not parse)
                        string rawFileVersion = fileVersionAttributes[0].Version;
                        try
                        {
                            applicationVersion = new Version(rawFileVersion);
                        }
                        // ReSharper disable EmptyGeneralCatchClause
                        catch
                        // ReSharper restore EmptyGeneralCatchClause
                        {
                        }
                    }

                    if ((productAttributes != null) && (productAttributes.Length > 0))
                    {
                        productName = productAttributes[0].Product ?? string.Empty; //protected against null explicit values
                    }

                    if ((titleAttributes != null) && (titleAttributes.Length > 0))
                    {
                        applicationName = titleAttributes[0].Title ?? string.Empty; //protected against null explicit values
                    }

                    if ((descriptionAttributes != null) && (descriptionAttributes.Length > 0))
                    {
                        applicationDescription = descriptionAttributes[0].Description ?? string.Empty; //protected against null explicit values
                    }
                }
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch
            // ReSharper restore EmptyGeneralCatchClause
            {
            }
        }

        private void SendPropertyChanged(String propertyName)
        {
            if ((PropertyChanged != null))
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

#endregion

    }
}
