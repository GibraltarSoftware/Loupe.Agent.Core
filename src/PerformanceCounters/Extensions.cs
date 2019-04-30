using System;
using System.Collections.Generic;
using System.Text;
using Loupe.Agent.Core.Services;

namespace Loupe.Agent.PerformanceCounters
{
    public static class Extensions
    {
        public static ILoupeAgentBuilder AddPerformanceCounters(this ILoupeAgentBuilder builder) => builder.AddListener<PerformanceMonitor>();
    }
}
