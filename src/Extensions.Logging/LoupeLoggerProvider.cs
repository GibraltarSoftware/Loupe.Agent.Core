using Loupe.Agent;
using Loupe.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loupe.Extensions.Logging
{
    /// <summary>
    /// Loupe implementation of <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <seealso cref="ILoggerProvider" />
#if(NETCOREAPP2_0 || NETSTANDARD2_0)
    [ProviderAlias("Loupe")]
#endif
    public class LoupeLoggerProvider : ILoggerProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoupeLoggerProvider"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public LoupeLoggerProvider(IConfiguration configuration)
        {
            var agentConfiguration = new AgentConfiguration();
            configuration.GetSection("Loupe").Bind(agentConfiguration);
            Log.StartSession(agentConfiguration);
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
        public ILogger CreateLogger(string categoryName) => new LoupeLogger(categoryName);
    }
}
