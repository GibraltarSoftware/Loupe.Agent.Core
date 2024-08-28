using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Loupe.Extensibility.Data
{
    /// <summary>An interface for accessing Summary information about the current session.</summary>
    /// <remarks>
    /// 	<para>The session summary includes all of the information that is available in
    ///     Loupe to categorize the session. This includes the product,
    ///     application, and version information that was detected by Loupe (or overridden
    ///     in the application configuration) as well as a range of information about the
    ///     current computing environment (such as Operating System Family and process
    ///     architecture).</para>
    /// </remarks>
    public interface ISessionSummary : INotifyPropertyChanged
    {
        /// <summary>
        /// Get a copy of the full session detail this session refers to.  
        /// </summary>
        /// <remarks>Session objects can be large in memory.  This method will return a new object
        /// each time it is called which should be released by the caller as soon as feasible to control memory usage.</remarks>
        ISession Session();

        /// <summary>
        /// The unique Id of the session.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The link to this item on the server.
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// Indicates if the session has ever been viewed or exported.
        /// </summary>
        bool IsNew { get; }

        /// <summary>
        /// Indicates if all of the session data is stored that is expected to be available.
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Indicates if the session is currently running and a live stream is available.
        /// </summary>
        bool IsLive { get; }

        /// <summary>
        /// Indicates if session data is available.
        /// </summary>
        /// <remarks>The session summary can be transfered separately from the session details
        /// and isn't subject to pruning so it may be around long before or after the detailed data is.</remarks>
        bool HasData { get; }

        /// <summary>
        /// The display caption of the time zone where the session was recorded.
        /// </summary>
        string TimeZoneCaption { get; }

        /// <summary>
        /// The date and time the session started.
        /// </summary>
        DateTimeOffset StartDateTime { get; }

        /// <summary>
        /// The date and time the session started in the time zone the user has requested for display.
        /// </summary>
        DateTimeOffset DisplayStartDateTime { get; }

        /// <summary>
        /// The date and time the session ended or was last confirmed running.
        /// </summary>
        DateTimeOffset EndDateTime { get; }

        /// <summary>
        /// The date and time the session ended or was last confirmed running in the time zone the user has requested for display.
        /// </summary>
        DateTimeOffset DisplayEndDateTime { get; }

        /// <summary>
        /// The time range between the start and end of this session.
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// The date and time the session was added to the repository.
        /// </summary>
        DateTimeOffset AddedDateTime { get; }

        /// <summary>
        /// The date and time the session was added to the repository in the time zone the user has requested for display.
        /// </summary>
        DateTimeOffset DisplayAddedDateTime { get; }

        /// <summary>
        /// The date and time the session header was last updated locally.
        /// </summary>
        DateTimeOffset UpdatedDateTime { get; }

        /// <summary>
        /// The date and time the session header was last updated locally in the time zone the user has requested for display.
        /// </summary>
        DateTimeOffset DisplayUpdatedDateTime { get; }

        /// <summary>
        /// A display caption for the session.
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// The product name of the application that recorded the session.
        /// </summary>
        string Product { get; }

        /// <summary>
        /// The title of the application that recorded the session.
        /// </summary>
        string Application { get; }

        /// <summary>
        /// Optional.  The environment this session is running in.
        /// </summary>
        /// <remarks>Environments are useful for categorizing sessions, for example to 
        /// indicate the hosting environment. If a value is provided it will be 
        /// carried with the session data to upstream servers and clients.  If the 
        /// corresponding entry does not exist it will be automatically created.</remarks>
        string Environment { get; }

        /// <summary>
        /// Optional.  The promotion level of the session.
        /// </summary>
        /// <remarks>Promotion levels are useful for categorizing sessions, for example to 
        /// indicate whether it was run in development, staging, or production. 
        /// If a value is provided it will be carried with the session data to upstream servers and clients.  
        /// If the corresponding entry does not exist it will be automatically created.</remarks>
        string PromotionLevel { get; }

        /// <summary>
        /// Optional.  The unique Id of the application environment this session is a part of.
        /// </summary>
        Guid? ApplicationEnvironmentId { get; }

        /// <summary>
        /// Optional. The caption of the application environment this session is a part of.
        /// </summary>
        string ApplicationEnvironmentCaption { get; }

        /// <summary>
        /// Optional.  The unique Id of the service within the application environment this session is a part of.
        /// </summary>
        Guid? ApplicationEnvironmentServiceId { get; }

        /// <summary>
        /// Optional.  The caption of the service within the application environment this session is a part of.
        /// </summary>
        string ApplicationEnvironmentServiceCaption { get; }

        /// <summary>
        /// The type of process the application ran as.
        /// </summary>
        ApplicationType ApplicationType { get; }

        /// <summary>
        /// The description of the application from its manifest.
        /// </summary>
        string ApplicationDescription { get; }

        /// <summary>
        /// The version of the application that recorded the session.
        /// </summary>
        Version ApplicationVersion { get; }

        /// <summary>
        /// The version of the Loupe Agent used to monitor the session.
        /// </summary>
        Version AgentVersion { get; }

        /// <summary>
        /// The host name / NetBIOS name of the computer that recorded the session.
        /// </summary>
        /// <remarks>Does not include the domain name portion of the fully qualified DNS name.</remarks>
        string HostName { get; }

        /// <summary>
        /// The DNS domain name of the computer that recorded the session.  May be empty.
        /// </summary>
        /// <remarks>Does not include the host name portion of the fully qualified DNS name.</remarks>
        string DnsDomainName { get; }

        /// <summary>
        /// The fully qualified user name of the user the application was run as.
        /// </summary>
        string FullyQualifiedUserName { get; }

        /// <summary>
        /// The user Id that was used to run the session.
        /// </summary>
        string UserName { get; }

        /// <summary>
        /// The domain of the user id that was used to run the session.
        /// </summary>
        string UserDomainName { get; }

        /// <summary>
        /// The version information of the installed operating system (without service pack or patches).
        /// </summary>
        Version OSVersion { get; }

        /// <summary>
        /// The operating system service pack, if any.
        /// </summary>
        string OSServicePack { get; }

        /// <summary>
        /// The culture name of the underlying operating system installation.
        /// </summary>
        string OSCultureName { get; }

        /// <summary>
        /// The processor architecture of the operating system.
        /// </summary>
        ProcessorArchitecture OSArchitecture { get; }

        /// <summary>
        /// The boot mode of the operating system.
        /// </summary>
        OSBootMode OSBootMode { get; }

        /// <summary>
        /// The OS Platform code, nearly always 1 indicating Windows NT.
        /// </summary>
        int OSPlatformCode { get; }

        /// <summary>
        /// The OS product type code, used to differentiate specific editions of various operating systems.
        /// </summary>
        int OSProductType { get; }

        /// <summary>
        /// The OS Suite Mask, used to differentiate specific editions of various operating systems.
        /// </summary>
        int OSSuiteMask { get; }

        /// <summary>
        /// The well known operating system family name, like Windows Vista or Windows Server 2003.
        /// </summary>
        string OSFamilyName { get; }

        /// <summary>
        /// The edition of the operating system without the family name, such as Workstation or Standard Server.
        /// </summary>
        string OSEditionName { get; }

        /// <summary>
        /// The well known OS name and edition name.
        /// </summary>
        string OSFullName { get; }

        /// <summary>
        /// The well known OS name, edition name, and service pack like Windows XP Professional Service Pack 3.
        /// </summary>
        string OSFullNameWithServicePack { get; }

        /// <summary>
        /// The version of the .NET runtime that the application domain is running as.
        /// </summary>
        Version RuntimeVersion { get; }

        /// <summary>
        /// The processor architecture the process is running as.
        /// </summary>
        ProcessorArchitecture RuntimeArchitecture { get; }

        /// <summary>
        /// The current application culture name.
        /// </summary>
        string CurrentCultureName { get; }

        /// <summary>
        /// The current user interface culture name.
        /// </summary>
        string CurrentUICultureName { get; }

        /// <summary>
        /// The number of megabytes of installed memory in the host computer.
        /// </summary>
        int MemoryMB { get; }

        /// <summary>
        /// The number of physical processor sockets in the host computer.
        /// </summary>
        int Processors { get; }

        /// <summary>
        /// The total number of processor cores in the host computer.
        /// </summary>
        int ProcessorCores { get; }

        /// <summary>
        /// Indicates if the session was run in a user interactive mode.
        /// </summary>
        bool UserInteractive { get; }

        /// <summary>
        /// Indicates if the session was run through terminal server.  Only applies to User Interactive sessions.
        /// </summary>
        bool TerminalServer { get; }

        /// <summary>
        /// The number of pixels wide of the virtual desktop.
        /// </summary>
        int ScreenWidth { get; }

        /// <summary>
        /// The number of pixels tall for the virtual desktop.
        /// </summary>
        int ScreenHeight { get; }

        /// <summary>
        /// The number of bits of color depth.
        /// </summary>
        int ColorDepth { get; }

        /// <summary>
        /// The complete command line used to execute the process including arguments.
        /// </summary>
        string CommandLine { get; }

        /// <summary>
        /// Optional. The unique tracking Id of the computer that recorded this session.
        /// </summary>
        Guid? ComputerId { get; }

        /// <summary>
        /// The final status of the session.
        /// </summary>
        SessionStatus Status { get; }

        /// <summary>
        /// The number of messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        long MessageCount { get; }

        /// <summary>
        /// The number of critical messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        long CriticalCount { get; }

        /// <summary>
        /// The number of error messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        long ErrorCount { get; }

        /// <summary>
        /// The number of warning messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        long WarningCount { get; }

        /// <summary>
        /// A copy of the collection of application specific properties. (Set via configuration at logging startup.  Do not modify here.)
        /// </summary>
        IDictionary<string, string> Properties { get; }

        /// <summary>
        /// The primary application framework that recorded the session
        /// </summary>
        Framework Framework { get; }
    }
}
