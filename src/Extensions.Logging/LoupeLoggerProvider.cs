using Gibraltar.Agent;
using Loupe.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loupe.Extensions.Logging
{
    #if(NETCOREAPP2_0 || NETSTANDARD2_0)
    [ProviderAlias("Loupe")]
    #endif
    public class LoupeLoggerProvider : ILoggerProvider
    {
        public LoupeLoggerProvider(IConfiguration configuration)
        {
            var agentConfiguration = new AgentConfiguration();
            configuration.GetSection("Loupe").Bind(agentConfiguration);
            Log.StartSession(agentConfiguration);
        }
        
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName) => new LoupeLogger(categoryName);
    }
}
