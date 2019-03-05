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
        /// <summary>
        /// Add Loupe ASP.NET Telemetry to the current web site.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static ILoupeAgentBuilder AddAspNetCoreDiagnostics(this ILoupeAgentBuilder builder) => builder.AddListener<ActionDiagnosticListener>();
    }
}