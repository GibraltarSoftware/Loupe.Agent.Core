using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;
using Gibraltar.Agent;
using Loupe.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loupe.Extensions.Logging
{
    /// <summary>
    /// Loupe implementation of <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <seealso cref="ILoggerProvider" />
#if (NETCOREAPP2_0 || NETSTANDARD2_0 || NETSTANDARD2_1)
    [ProviderAlias("Loupe")]
#endif
    public class LoupeLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, LoupeLogger> _loggers =
            new ConcurrentDictionary<string, LoupeLogger>();

        /// <summary>
        /// Initializes a new instance of the <see cref="LoupeLoggerProvider"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public LoupeLoggerProvider(IConfiguration configuration)
        {
            var agentConfiguration = new AgentConfiguration();
            configuration?.GetSection("Loupe")?.Bind(agentConfiguration);
            Log.StartSession(agentConfiguration);
        }

        /// <summary>
        /// For testing only.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public LoupeLoggerProvider()
        {
            
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Creates a new <see cref="ILogger" /> instance.
        /// </summary>
        /// <param name="categoryName">The category name for messages produced by the logger.</param>
        /// <returns>A new <see cref="ILogger"/> instance.</returns>
        public ILogger CreateLogger(string categoryName) => _loggers.TryGetValue(categoryName, out var provider)
            ? provider
            : _loggers.GetOrAdd(categoryName, new LoupeLogger(this, categoryName));
    
        // AsyncLocal field to hold CurrentScope
        private readonly AsyncLocal<LoupeLoggerScope> _scope = new AsyncLocal<LoupeLoggerScope>();

        internal LoupeLoggerScope CurrentScope
        {
            get => _scope.Value;
            set => _scope.Value = value;
        }

        /// <summary>
        /// Starts a new Scope.
        /// </summary>
        /// <param name="state">The state from <see cref="ILogger.BeginScope{TState}"/>.</param>
        /// <typeparam name="T">The type of the state parameter.</typeparam>
        /// <returns>A new logging scope.</returns>
        internal IDisposable BeginScope<T>(T state)
        {
            return new LoupeLoggerScope(this, state);
        }


    }
}
