using Loupe.Agent.AspNetCore.Metrics;
using Loupe.Agent.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Extension methods for <see cref="ILoupeAgentBuilder"/>.
    /// </summary>
    public static class LoupeAgentBuilderExtensions
    {
        /// <summary>
        /// Adds ASP.NET Core diagnostic listeners for standard Loupe metric generation.
        /// </summary>
        /// <param name="builder">The <see cref="ILoupeAgentBuilder"/> instance.</param>
        /// <returns>The <see cref="ILoupeAgentBuilder"/> instance.</returns>
        public static ILoupeAgentBuilder AddAspNetCoreDiagnostics(this ILoupeAgentBuilder builder) => builder.AddListener<ActionDiagnosticListener>();
    }
}