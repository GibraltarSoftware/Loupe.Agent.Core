using Microsoft.AspNetCore.Builder;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static class AppBuilderExtensions
    {
        /// <summary>
        /// Add a Loupe middleware to time requests and log errors.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
        public static IApplicationBuilder UseLoupe(this IApplicationBuilder app) => app.UseMiddleware<LoupeMiddleware>();
    }
}