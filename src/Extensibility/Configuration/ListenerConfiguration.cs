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
            EnableConsole = false; //this is nearly always duplicate information in .NET Core.
            EnableNetworkEvents = true;
        }

        /// <summary>
        /// When true, anything written to the console out will be appended to the log.
        /// </summary>
        /// <remarks>Disabled by default as ASP.NET Core applications send significant duplicate data to the console.</remarks>
        public bool EnableConsole { get; set; }

        /// <summary>
        /// When true, network events (such as reconfiguration and disconnection) will be logged automatically.
        /// </summary>
        public bool EnableNetworkEvents { get; set; }
    }
}
