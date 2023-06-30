using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Loupe.Extensibility.Data;

namespace Loupe.Configuration
{
    /// <summary>
    /// The configuration of the publisher.
    /// </summary>
    [DebuggerDisplay("{ProductName} - {ApplicationName}")]
    public sealed class PublisherConfiguration
    {
        /// <summary>
        /// Initialize the publisher from the application configuration
        /// </summary>
        public PublisherConfiguration()
        {
            ApplicationType = ApplicationType.Unknown;
            ForceSynchronous = false;
            MaxQueueLength = 2000;
            EnableAnonymousMode = false;
            EnableDebugMode = false;
        }

        /// <summary>
        /// Optional.  The name of the product for logging purposes.
        /// </summary>
        /// <remarks>Generally unnecessary for windows services, console apps, and WinForm applications.
        /// Useful for web applications where there is no reasonable way of automatically determining
        /// product name from the assemblies that initiate logging.</remarks>
        public string? ProductName { get; set; }

        /// <summary>
        /// Optional.  A description of the application to include with the session information.
        /// </summary>
        /// <remarks>Generally unnecessary for windows services, console apps, and WinForm applications.
        /// Useful for web applications where there is no reasonable way of automatically determining
        /// application description from the assemblies that initiate logging.</remarks>
        public string? ApplicationDescription { get; set; }

        /// <summary>
        /// Optional.  The name of the application for logging purposes.
        /// </summary>
        /// <remarks>Generally unnecessary for windows services, console apps, and WinForm applications.
        /// Useful for web applications where there is no reasonable way of automatically determining
        /// product name from the assemblies that initiate logging.</remarks>
        public string? ApplicationName { get; set; }

        /// <summary>
        /// Optional.  The ApplicationType to treat the application as, overriding the Agent's automatic determination.
        /// </summary>
        /// <remarks>This setting is not generally necessary as the Agent will automatically determine the application
        /// type correctly in most typical windows services, console apps, WinForm applications, and ASP.NET applications.
        /// If the automatic determination is unsuccessful or incorrect with a particular application, the correct type
        /// can be configured with this setting to bypass the automatic determination.  However, setting this incorrectly
        /// for the application could have undesirable effects.</remarks>
        public ApplicationType ApplicationType { get; set; }

        /// <summary>
        /// Optional.  The version of the application for logging purposes.
        /// </summary>
        /// <remarks><para>Generally unnecessary for windows services, console apps, and WinForm applications.
        /// Useful for web applications where there is no reasonable way of automatically determining
        /// product name from the assemblies that initiate logging.</para></remarks>
        public Version? ApplicationVersion { get; set; }

        /// <summary>
        /// We need this to load from JSON, because there's currently no custom binding
        /// and the standard binder doesn't use Version.Parse.
        /// </summary>
        /// <remarks>Added Attributes to hide in IntelliSense.</remarks>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string? ApplicationVersionNumber
        {
            get => ApplicationVersion?.ToString();
            set => ApplicationVersion = (value == null) ? null : Version.Parse(value);
        }

        /// <summary>
        /// Optional.  The environment this session is running in.
        /// </summary>
        /// <remarks>Environments are useful for categorizing sessions, for example to 
        /// indicate the hosting environment. If a value is provided it will be 
        /// carried with the session data to upstream servers and clients.  If the 
        /// corresponding entry does not exist it will be automatically created.</remarks>
        public string? EnvironmentName { get; set; }

        /// <summary>
        /// Optional.  The promotion level of the session.
        /// </summary>
        /// <remarks>Promotion levels are useful for categorizing sessions, for example to 
        /// indicate whether it was run in development, staging, or production. 
        /// If a value is provided it will be carried with the session data to upstream servers and clients.  
        /// If the corresponding entry does not exist it will be automatically created.</remarks>
        public string? PromotionLevelName { get; set; }

        /// <summary>
        /// When true, the publisher will treat all publish requests as write-through requests.
        /// </summary>
        /// <remarks>This overrides the write through request flag for all published requests, acting
        /// as if they are set true.  This will slow down logging and change the degree of parallelism of 
        /// multithreaded applications since each log message will block until it is committed to every
        /// configured messenger.</remarks>
        public bool ForceSynchronous { get; set; }

        /// <summary>
        /// The maximum number of queued messages waiting to be published.
        /// </summary>
        /// <remarks>Once the total number of messages waiting to be published exceeds the
        /// maximum queue length the log publisher will switch to a synchronous mode to 
        /// catch up.  This will cause the client to block until each new message is published.</remarks>
        public int MaxQueueLength { get; set; }

        /// <summary>
        /// When true, the Agent will record session data without collecting personally-identifying information.
        /// </summary>
        /// <remarks>In anonymous mode the Agent will not collect personally-identifying information such as user name,
        /// user domain name, host name, host domain name, and the application's command line.  Anonymous mode is disabled
        /// by default, and normal operation will collect this information automatically.</remarks>
        public bool EnableAnonymousMode { get; set; }

        /// <summary>
        /// When true, the Agent will include debug messages in logs. Not intended for production use
        /// </summary>
        /// <remarks><para>Normally the Agent will fail silently and otherwise compensate for problems to ensure
        /// that it does not cause a problem for your application. When you are developing your application 
        /// you can enable this mode to get more detail about why th Agent is behaving as it is and resolve
        /// issues.</para>
        /// <para>In debug mode the agent may throw exceptions to indicate calling errors it normally would 
        /// just silently ignore. Therefore, this option is not recommended for consistent production use.</para></remarks>
        public bool EnableDebugMode { get; set; }

        /// <summary>
        /// Normalize configuration data
        /// </summary>
        public void Sanitize()
        {
            if (MaxQueueLength <= 0)
                MaxQueueLength = 2000;
            else if (MaxQueueLength > 50000)
                MaxQueueLength = 2000;

            if (string.IsNullOrEmpty(ProductName))
                ProductName = null;

            if (string.IsNullOrEmpty(ApplicationDescription))
                ApplicationDescription = null;

            if (string.IsNullOrEmpty(ApplicationName))
                ApplicationName = null;

        }

        /// <inheritdoc />
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("\tProduct Name: {0}\r\n", ProductName);
            stringBuilder.AppendFormat("\tApplication Name: {0}\r\n", ApplicationName);
            stringBuilder.AppendFormat("\tApplication Description: {0}\r\n", ApplicationDescription);
            stringBuilder.AppendFormat("\tApplication Type: {0}\r\n", ApplicationType);
            stringBuilder.AppendFormat("\tApplication Version: {0}\r\n", ApplicationVersion);
            stringBuilder.AppendFormat("\tEnvironment Name: {0}\r\n", EnvironmentName);
            stringBuilder.AppendFormat("\tPromotion Level Name: {0}\r\n", PromotionLevelName);
            stringBuilder.AppendFormat("\tForce Synchronous: {0}\r\n", ForceSynchronous);
            stringBuilder.AppendFormat("\tMax Queue Length: {0}\r\n", MaxQueueLength);
            stringBuilder.AppendFormat("\tEnable Anonymous Mode: {0}\r\n", EnableAnonymousMode);
            stringBuilder.AppendFormat("\tEnable Debug Mode: {0}\r\n", EnableDebugMode);

            return stringBuilder.ToString();
        }
    }
}
