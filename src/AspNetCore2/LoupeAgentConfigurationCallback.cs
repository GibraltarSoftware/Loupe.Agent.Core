using System;
using Loupe.Configuration;

namespace Loupe.Agent.AspNetCore
{
    public class LoupeAgentConfigurationCallback
    {
        private readonly Action<AgentConfiguration> _callback;

        public LoupeAgentConfigurationCallback()
        {
            _callback = _ => { };
        }

        public LoupeAgentConfigurationCallback(Action<AgentConfiguration> callback)
        {
            _callback = callback;
        }

        public void Invoke(AgentConfiguration configuration)
        {
            _callback(configuration);
        }
    }
}