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
            switch (logLevel)
                {
                    case LogLevel.Trace:
                        // TODO: What does Trace map to?
                        break;
                    case LogLevel.Debug:
                        break;
                    case LogLevel.Information:
                        Gibraltar.Agent.Log.Information(exception, LogWriteMode.Queued, _category, string.Empty, formatter(state, exception), state);
                        break;
                    case LogLevel.Warning:
                        Gibraltar.Agent.Log.Warning(exception, LogWriteMode.Queued, _category, string.Empty, formatter(state, exception), state);
                        break;
                    case LogLevel.Error:
                        Gibraltar.Agent.Log.Error(exception, LogWriteMode.Queued, _category, string.Empty, formatter(state, exception), state);
                        break;
                    case LogLevel.Critical:
                        Gibraltar.Agent.Log.Critical(exception, LogWriteMode.Queued, _category, string.Empty, formatter(state, exception), state);
                        break;
                    case LogLevel.None:
                        // TODO: What does None map to?
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
                }
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