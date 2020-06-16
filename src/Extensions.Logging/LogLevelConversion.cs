using System;
using Gibraltar.Agent;
using Microsoft.Extensions.Logging;

namespace Loupe.Extensions.Logging
{
    internal static class LogLevelConversion
    {
        public static LogMessageSeverity ToSeverity(LogLevel logLevel)
        {
            LogMessageSeverity severity;
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    severity = LogMessageSeverity.Verbose;
                    break;
                case LogLevel.Information:
                    severity = LogMessageSeverity.Information;
                    break;
                case LogLevel.Warning:
                    severity = LogMessageSeverity.Warning;
                    break;
                case LogLevel.Error:
                    severity = LogMessageSeverity.Error;
                    break;
                case LogLevel.Critical:
                    severity = LogMessageSeverity.Critical;
                    break;
                case LogLevel.None:
                    severity = LogMessageSeverity.None;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }

            return severity;
        }
    }
}