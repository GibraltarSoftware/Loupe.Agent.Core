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
        private readonly LoupeLoggerProvider _provider;
        private readonly string _category;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoupeLogger"/> class.
        /// </summary>
        public LoupeLogger(LoupeLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var severity = LogLevelConversion.ToSeverity(logLevel);

            var description = formatter(state, exception);
            var details = LoupeLogEnricher.GetJson(state, _provider);
            Gibraltar.Agent.Log.Write(severity, "Microsoft.Extensions.Logging", 1, exception, LogWriteMode.Queued, details, _category, null, description);
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return _provider.BeginScope(state);
        }
    }
}