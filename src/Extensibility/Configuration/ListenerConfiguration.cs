namespace Loupe.Configuration
{
    /// <summary>
    /// Configuration information for the trace listener.
    /// </summary>
    public sealed class ListenerConfiguration
    {
        /// <summary>
        /// Initialize from application configuration section
        /// </summary>
        public ListenerConfiguration()
        {
            AutoTraceRegistration = true;
            EnableConsole = false; //this is nearly always duplicate information in .NET Core.
            EnableNetworkEvents = true;
            EndSessionOnTraceClose = true;
        }

        /// <summary>
        /// Configures whether Loupe should automatically make sure it is registered as a Trace Listener.
        /// </summary>
        /// <remarks>This is true by default to enable easy drop-in configuration (e.g. using the LiveLogViewer
        /// control on a form).  Normally, it should not be necessary to disable this feature even when adding
        /// Loupe as a Trace Listener in an app.config or by code.  But this setting can be configured
        /// to false if it is desirable to prevent Loupe from receiving Trace events directly, such as
        /// if the application is processing Trace events into the Loupe API itself.</remarks>
        public bool AutoTraceRegistration { get; set; }

        /// <summary>
        /// When true, anything written to the console out will be appended to the log.
        /// </summary>
        /// <remarks>Disabled by default as ASP.NET Core applications send significant duplicate data to the console.</remarks>
        public bool EnableConsole { get; set; }

        /// <summary>
        /// When true, network events (such as reconfiguration and disconnection) will be logged automatically.
        /// </summary>
        public bool EnableNetworkEvents { get; set; }

        /// <summary>
        /// When true, the Loupe LogListener will end the Loupe log session when Trace.Close() is called.
        /// </summary>
        /// <remarks>This setting has no effect if the trace listener is not enabled.  Unless disabled by setting
        /// this configuration value to false, a call to Trace.Close() to shutdown Trace logging will also be
        /// translated into a call to Gibraltar.Agent.Log.EndSession().</remarks>
        public bool EndSessionOnTraceClose { get; set; }
    }
}
