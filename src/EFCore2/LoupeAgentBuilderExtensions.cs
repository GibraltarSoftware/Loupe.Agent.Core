using System;
using Loupe.Agent.Core.Services;
using Loupe.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Agent.EntityFrameworkCore
{
    /// <summary>
    /// Extension method to add the EF Core diagnostic listener to Loupe.
    /// </summary>
    public static class LoupeAgentBuilderExtensions
    {
        /// <summary>
        /// Adds the EF Core diagnostic listener.
        /// </summary>
        /// <param name="builder">The <see cref="ILoupeAgentBuilder"/>.</param>
        /// <returns>The builder.</returns>
        public static ILoupeAgentBuilder AddEntityFrameworkCoreDiagnostics(this ILoupeAgentBuilder builder) => builder.AddListener<EntityFrameworkCoreDiagnosticListener>();
    }
}