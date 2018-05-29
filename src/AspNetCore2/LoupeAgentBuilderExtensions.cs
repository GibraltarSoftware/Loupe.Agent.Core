using Loupe.Agent.AspNetCore.Metrics;
using Loupe.Agent.Core.Services;

namespace Loupe.Agent.AspNetCore
{
    public static class LoupeAgentBuilderExtensions
    {
        public static ILoupeAgentBuilder AddAspNetCoreDiagnostics(this ILoupeAgentBuilder builder) => builder.AddListener<ActionDiagnosticListener>();
    }
}