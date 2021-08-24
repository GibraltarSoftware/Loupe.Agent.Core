using System.Text;

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
            EnableAssemblyEvents = true;
            EnableAssemblyLoadFailureEvents = false;
            EnableConsole = false; //this is nearly always duplicate information in .NET Core.
            EnableNetworkEvents = true;
            EnableGCEvents = true;
        }

        /// <summary>
        /// When true, assembly load information will be logged automatically.
        /// </summary>
        public bool EnableAssemblyEvents { get; set; }

        /// <summary>
        /// When true, CLR events related to assembly resolution failures will be logged automatically.
        /// </summary>
        public bool EnableAssemblyLoadFailureEvents { get; set; }

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
        /// When true, metrics are recorded for Garbage Collector (GC) events.
        /// </summary>
        public bool EnableGCEvents { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("\tEnable Assembly Load: {0}\r\n", EnableAssemblyEvents);
            stringBuilder.AppendFormat("\tEnable Assembly Load Failure: {0}\r\n", EnableAssemblyLoadFailureEvents);
            stringBuilder.AppendFormat("\tEnable Console: {0}\r\n", EnableConsole);
            stringBuilder.AppendFormat("\tEnable Network Events: {0}\r\n", EnableNetworkEvents);
            stringBuilder.AppendFormat("\tEnable GC Events: {0}\r\n", EnableGCEvents);

            return stringBuilder.ToString();
        }
    }
}
