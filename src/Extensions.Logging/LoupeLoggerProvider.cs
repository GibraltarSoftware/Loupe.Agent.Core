using System.Threading;
using Gibraltar.Agent;
using Loupe.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loupe.Extensions.Logging
{
    public class LoupeLoggerProvider : ILoggerProvider
    {
//        private readonly AgentConfiguration _agentConfiguration;
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
