using System;
using Loupe.Configuration;

namespace Loupe.Core.Monitor
{
    /// <summary>
    /// Event arguments for the Log.Initializing event of the Loupe Agent Logging class.
    /// </summary>
    public class LogInitializingEventArgs : EventArgs
    {
        internal LogInitializingEventArgs(AgentConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// If set to true the initialization process will not complete and the agent will stay dormant.
        /// </summary>
        public bool Cancel { get; set; }


        /// <summary>
        /// The configuration for the agent to start with
        /// </summary>
        /// <remarks>The configuration will reflect the effect of the current application configuration file and Agent default values.</remarks>
        public AgentConfiguration Configuration { get; private set; }
    }
}
