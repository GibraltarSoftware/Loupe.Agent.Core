using Loupe.Agent.AspNetCore.Metrics;
using Loupe.Agent.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Loupe.Agent.AspNetCore
{
    public static class LoupeAgentBuilderExtensions
    {
        public static ILoupeAgentBuilder AddAspNetCoreDiagnostics(this ILoupeAgentBuilder builder) => builder.AddListener<ActionDiagnosticListener>();
    }
}