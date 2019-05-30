using System;
using Loupe.Configuration;

namespace Loupe.Agent.Core.Services
{
    /// <summary>Wraps a configuration Action delegate so it can be injected.</summary>
    public class LoupeAgentConfigurationCallback
    {
        private readonly Action<AgentConfiguration> _callback;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoupeAgentConfigurationCallback"/> class.
        /// </summary>
        public LoupeAgentConfigurationCallback()
        {
            _callback = _ => { };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoupeAgentConfigurationCallback"/> class.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public LoupeAgentConfigurationCallback(Action<AgentConfiguration> callback)
        {
            _callback = callback;
        }

        /// <summary>
        /// Invokes the callback with the specified <see cref="AgentConfiguration"/>.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public void Invoke(AgentConfiguration configuration)
        {
            _callback(configuration);
        }
    }
}