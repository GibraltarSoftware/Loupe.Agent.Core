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
using Gibraltar.Data;
using Gibraltar.Monitor.Platform;
using Gibraltar.Monitor.Serialization;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
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
        private readonly ApplicationType m_AgentAppType;
        private long m_CriticalCount;
        private long m_ErrorCount;
        private long m_WarningCount;
        private long m_MessageCount;
        private volatile SessionStatus m_SessionStatus;

        private readonly bool m_PrivacyEnabled;

        /// <summary>
        /// Raised whenever a property changes on the object
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Create a new session summary as the live collection session for the current process
        /// </summary>
        /// <remarks>This constructor figures out all the summary information when invoked, which can take a moment.</remarks>
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

                //Let's see if the user has already picked some things for us...
                var publisherConfig = configuration.Publisher;

                //what kind of process are we?
                if (publisherConfig.ApplicationType != ApplicationType.Unknown)
                {
                    // They specified an application type, so just use that.
                    m_AgentAppType = publisherConfig.ApplicationType;
                }
                else
                {
                    var entryAssembly = Assembly.GetEntryAssembly();
                    if (entryAssembly != null)
                    {
                        // See if we're running in the root OS session or not
                        int sessionId;
                        try
                        {
                            sessionId = Process.GetCurrentProcess().SessionId; // May not work outside of Windows.
                        }
                        catch
                        {
                            sessionId = 0; // 
                        }

                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && sessionId == 0 && System.Environment.UserInteractive == false)
                        {
                            // If we're in SessionId 0 then we're started by the kernel.  If also non-interactive, call it a service.
                            m_AgentAppType = ApplicationType.Service;
                        }
                        else
                        {
                            m_AgentAppType = ApplicationType.Console;
                            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || System.Environment.UserInteractive) // Linux always thinks it's not interactive.
                            {
                                var referencedAssemblies = entryAssembly.GetReferencedAssemblies();
                                foreach (var assemblyName in referencedAssemblies)
                                {
                                    if (assemblyName.Name == "System.Windows.Forms")
                                    {
                                        m_AgentAppType = ApplicationType.Windows;
                                        break;
                                    }

                                    if (assemblyName.Name == "System.Windows.Presentation")
                                    {
                                        m_AgentAppType = ApplicationType.Windows;
                                        break;
                                    }
                                }
                                // If it doesn't reference System.Windows.Forms, it can't be a winforms app.
                            }
                            // Otherwise, non-interactive can't be a winforms app, so leave it as Console.
                        }
                    }
                }

                m_Packet.ApplicationType = m_AgentAppType; // Finally, set the application type from our determined type.

                //we want to find our entry assembly and get default product/app info from it.
                GetApplicationNameSafe(out var productName, out var applicationName, out var applicationVersion, out var applicationDescription);                    

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
                m_Packet.ApplicationVersion ??= new Version(0, 0);
                m_Packet.ApplicationDescription ??= string.Empty;
                m_Packet.EnvironmentName ??= string.Empty;
                m_Packet.PromotionLevelName ??= string.Empty;

                m_Packet.ComputerId = GetComputerIdSafe(m_Packet.ProductName, configuration);
                m_Packet.AgentVersion = GetAgentVersionSafe();
            }
            catch (Exception ex)
            {
                //we really don't want an init error to fail us, not here!
                GC.KeepAlive(ex);
            }

            var isContainer = IsRunningInContainer();

            if (isContainer)
            {
                m_Packet.HostName = "container";
                m_Packet.DnsDomainName = string.Empty;
            }
            else if (m_PrivacyEnabled == false)
            {
                try
                {
                    var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
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

            // If the configuration has a host name it overrides everything.
            if (!string.IsNullOrEmpty(configuration.Publisher.HostName))
            {
                m_Packet.HostName = configuration.Publisher.HostName;
            }

            var os = System.Environment.OSVersion;
            m_Packet.OSPlatformCode = (int) os.Platform; //we copied this enum for our value.
            m_Packet.OSVersion = os.Version;
            m_Packet.OSServicePack = os.ServicePack;

            try
            {
                m_Packet.OSArchitecture = RuntimeInformation.OSArchitecture.ToProcessorArchitecture();
                m_Packet.RuntimeArchitecture = RuntimeInformation.ProcessArchitecture.ToProcessorArchitecture();

                m_Packet.OSCultureName = CultureInfo.CurrentUICulture.ToString();
                m_Packet.CurrentCultureName = CultureInfo.CurrentCulture.ToString();
                m_Packet.CurrentUICultureName = CultureInfo.CurrentUICulture.ToString();

                m_Packet.OSBootMode = OSBootMode.Normal;

                m_Packet.Processors = System.Environment.ProcessorCount;
                m_Packet.ProcessorCores = System.Environment.ProcessorCount; //No universal way to get this now.

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

                    if (frameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
                    {
                        m_Packet.Framework = Framework.DotNet;
                    }
                    else
                    {
                        m_Packet.Framework = Framework.DotNetCore;
                    }
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
                    m_Packet.RuntimeVersion = new Version(0, 0);
                }

                var hostMemoryMB = 0;
                try
                {
                    var totalMemoryBytes = HostCapabilities.GetTotalMemory();
                    hostMemoryMB = (int)(totalMemoryBytes / (1024 * 1024)); //to MB
                }
                catch (PlatformNotSupportedException)
                {
                }

                m_Packet.MemoryMB = hostMemoryMB;
                m_Packet.UserInteractive = System.Environment.UserInteractive;

                //find the active screen resolution
                if (m_AgentAppType == ApplicationType.Windows)
                {
                    //We don't want to pull in WinForms to grab this...
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
                foreach (var keyValuePair in configuration.Properties)
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

        /// <inheritdoc />
        ISession ISessionSummary.Session()
        {
            throw new NotSupportedException("Retrieving the full session from a SessionSummary is not supported");
        }

        /// <inheritdoc />
        public Guid Id => m_Packet.ID;

        /// <inheritdoc />
        public Uri Uri => throw new NotSupportedException("Links are not supported in this context");

        /// <inheritdoc />
        bool ISessionSummary.IsNew => true;

        /// <inheritdoc />
        bool ISessionSummary.IsComplete => true;

        /// <inheritdoc />
        bool ISessionSummary.IsLive => false;

        /// <inheritdoc />
        bool ISessionSummary.HasData //we are just a header, we presume we stand alone.
            => false;

        /// <inheritdoc />
        public Guid? ComputerId => m_Packet.ComputerId;

        /// <inheritdoc />
        public string TimeZoneCaption => m_Packet.TimeZoneCaption;

        /// <inheritdoc />
        public DateTimeOffset StartDateTime => m_Packet.Timestamp;

        /// <inheritdoc />
        public DateTimeOffset DisplayStartDateTime => StartDateTime;

        /// <inheritdoc />
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

        /// <inheritdoc />
        public DateTimeOffset DisplayEndDateTime => EndDateTime;

        /// <inheritdoc />
        public TimeSpan Duration => EndDateTime - StartDateTime;

        /// <inheritdoc />
        DateTimeOffset ISessionSummary.AddedDateTime => StartDateTime;

        /// <inheritdoc />
        DateTimeOffset ISessionSummary.DisplayAddedDateTime => StartDateTime;

        /// <inheritdoc />
        DateTimeOffset ISessionSummary.UpdatedDateTime => EndDateTime;

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public string Product => m_Packet.ProductName;

        /// <inheritdoc />
        public string Application => m_Packet.ApplicationName;

        /// <inheritdoc />
        public string Environment => m_Packet.EnvironmentName;

        /// <inheritdoc />
        public string PromotionLevel => m_Packet.PromotionLevelName;

        /// <inheritdoc />
        public Guid? ApplicationEnvironmentId => null;

        /// <inheritdoc />
        public string ApplicationEnvironmentCaption => null;

        /// <inheritdoc />
        public Guid? ApplicationEnvironmentServiceId => null;

        /// <inheritdoc />
        public string ApplicationEnvironmentServiceCaption => null;

        /// <inheritdoc />
        public ApplicationType ApplicationType => m_Packet.ApplicationType;

        /// <summary>
        /// The type of process the application ran as (as seen by the Agent internally).
        /// </summary>
        public ApplicationType AgentAppType => m_AgentAppType;

        /// <inheritdoc />
        public string ApplicationDescription => m_Packet.ApplicationDescription;

        /// <inheritdoc />
        public Version ApplicationVersion => m_Packet.ApplicationVersion;

        /// <inheritdoc />
        public Version AgentVersion => m_Packet.AgentVersion;

        /// <inheritdoc />
        public string HostName => m_Packet.HostName;

        /// <inheritdoc />
        public string DnsDomainName => m_Packet.DnsDomainName;

        /// <inheritdoc />
        public string FullyQualifiedUserName => m_Packet.FullyQualifiedUserName;

        /// <inheritdoc />
        public string UserName => m_Packet.UserName;

        /// <inheritdoc />
        public string UserDomainName => m_Packet.UserDomainName;

        /// <inheritdoc />
        public Version OSVersion => m_Packet.OSVersion;

        /// <inheritdoc />
        public string OSServicePack => m_Packet.OSServicePack;

        /// <inheritdoc />
        public string OSCultureName => m_Packet.OSCultureName;

        /// <inheritdoc />
        public ProcessorArchitecture OSArchitecture => m_Packet.OSArchitecture;

        /// <inheritdoc />
        public OSBootMode OSBootMode => m_Packet.OSBootMode;

        /// <inheritdoc />
        public int OSPlatformCode => m_Packet.OSPlatformCode;

        /// <inheritdoc />
        public int OSProductType => m_Packet.OSProductType;

        /// <inheritdoc />
        public int OSSuiteMask => m_Packet.OSSuiteMask;

        /// <inheritdoc />
        public string OSFamilyName => string.Empty; // BUG 

        /// <inheritdoc />
        public string OSEditionName => string.Empty; // BUG 

        /// <inheritdoc />
        public string OSFullName => string.Empty; // BUG 

        /// <inheritdoc />
        public string OSFullNameWithServicePack => string.Empty; // BUG 

        /// <inheritdoc />
        public Version RuntimeVersion => m_Packet.RuntimeVersion;

        /// <inheritdoc />
        public ProcessorArchitecture RuntimeArchitecture => m_Packet.RuntimeArchitecture;

        /// <inheritdoc />
        public string CurrentCultureName => m_Packet.CurrentCultureName;

        /// <inheritdoc />
        public string CurrentUICultureName => m_Packet.CurrentUICultureName;

        /// <inheritdoc />
        public int MemoryMB => m_Packet.MemoryMB;

        /// <inheritdoc />
        public int Processors => m_Packet.Processors;

        /// <inheritdoc />
        public int ProcessorCores => m_Packet.ProcessorCores;

        /// <inheritdoc />
        public bool UserInteractive => m_Packet.UserInteractive;

        /// <inheritdoc />
        public bool TerminalServer => m_Packet.TerminalServer;

        /// <inheritdoc />
        public int ScreenWidth => m_Packet.ScreenWidth;

        /// <inheritdoc />
        public int ScreenHeight => m_Packet.ScreenHeight;

        /// <inheritdoc />
        public int ColorDepth => m_Packet.ColorDepth;

        /// <inheritdoc />
        public string CommandLine => m_Packet.CommandLine;

        /// <inheritdoc />
        public SessionStatus Status { get => m_SessionStatus;
            internal set => m_SessionStatus = value;
        }

        /// <inheritdoc />
        public long MessageCount 
        { 
            get => m_MessageCount;
            internal set => m_MessageCount = value;
        }

            /// <inheritdoc />
        public long CriticalCount 
        {
            get => m_CriticalCount;
            internal set => m_CriticalCount = value;
        }

        /// <inheritdoc />
        public long ErrorCount 
        { 
            get => m_ErrorCount;
            internal set => m_ErrorCount = value;
        }

        /// <inheritdoc />
        public long WarningCount 
        { 
            get => m_WarningCount;
            internal set => m_WarningCount = value;
        }

        /// <inheritdoc />
        public IDictionary<string, string> Properties => m_Packet.Properties;

        /// <inheritdoc />
        public Framework Framework => m_Packet.Framework;

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
            Version version;
            try
            {
                version = Assembly.GetExecutingAssembly().GetName().Version;
            }
            catch (Exception)
            {

                version = new Version(5, 0);
            }

            return version;
        }

        /// <summary>
        /// Indicates if the current process is running in a container.
        /// </summary>
        private static bool IsRunningInContainer()
        {
            var runningInContainer = System.Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");

            return !string.IsNullOrEmpty(runningInContainer) && runningInContainer.ToLowerInvariant() == "true";
        }

        private static Guid GetComputerIdSafe(string product, AgentConfiguration configuration)
        {
            var computerId = Guid.Empty;  //we can't fail, this is a good default value since upstream items will treat it as a "don't know"
            try
            {
                //If we're in a container we want to use the log storage folder - the computer Id will be transient
                //which can cause problems with our upload process (registering the same session to many computers).
                //So on a container we want to use the local repository.
                var preferredPath = IsRunningInContainer() ? LocalRepository.CalculateRepositoryPath(product, configuration.SessionFile.Folder) 
                    : PathManager.FindBestPath(PathType.Collection);

                //first see if we have a GUID file in the system-wide location.
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
                    //read back the existing computer id
                    var rawComputerId = File.ReadAllText(computerIdFile, Encoding.UTF8);
                    computerId = new Guid(rawComputerId);
                }

                //create a new computer id
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
