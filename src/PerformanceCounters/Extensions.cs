using System;
using System.Collections.Generic;
using System.Text;
using Loupe.Agent.Core.Services;

namespace Loupe.Agent.PerformanceCounters
{
    public static class Extensions
    {
        /// <summary>
        /// Add performance counter monitoring to the Loupe Agent
        /// </summary>
        /// <param name="builder"></param>
        /// <returns>The Loupe Agent Builder</returns>
        public static ILoupeAgentBuilder AddPerformanceCounters(this ILoupeAgentBuilder builder) => builder.AddMonitor<PerformanceMonitor>();
    }
}
