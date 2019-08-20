using System;
using Loupe.Monitor;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Loupe.Server.Client
{
    /// <summary>
    /// Client logger implementation for our core Loupe logging interface
    /// </summary>
    internal class ClientLogger : IClientLogger
    {
        public bool SilentMode { get { return Log.SilentMode; } }

        public void Write(LogMessageSeverity severity, string category, string caption, string description, params object[] args)
        {
            Log.Write(severity, category, caption, description, args);
        }

        public void Write(LogMessageSeverity severity, Exception exception, bool attributeToException, string category, string caption,
            string description, params object[] args)
        {
            Log.Write(severity, LogWriteMode.Queued, exception, attributeToException, category, caption, description, args);
        }
    }
}
