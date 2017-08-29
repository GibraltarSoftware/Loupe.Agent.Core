using System;
using Gibraltar.Agent;
using Microsoft.Extensions.Logging;

namespace Loupe.Extensions.Logging
{
    /// <inheritdoc />
    /// <summary>
    /// Implementation of <see cref="T:Microsoft.Extensions.Logging.ILogger" /> interface targeting Loupe server
    /// </summary>
    public class LoupeLogger : ILogger
    {
        private readonly string _category;

        public LoupeLogger(string category)
        {
            _category = category;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

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

            Gibraltar.Agent.Log.Write(severity, "loupe", 1, exception, LogWriteMode.Queued, null, _category, null, formatter(state, exception), state);
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return new LoupeLoggerScope(state);
        }
    }
}