using System;
using System.Threading.Tasks;
using Loupe.Agent.AspNetCore.Handlers;
using Loupe.Agent.Core.Services;
using Loupe.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static class AppBuilderExtensions
    {

#if NET8_0_OR_GREATER

        /// <summary>
        /// Adds the main Loupe Agent
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/>.</param>
        /// <param name="configure">Optional.  An Agent configuration delegate</param>
        /// <returns>The <see cref="WebApplicationBuilder"/>.</returns>
        /// <remarks>This is used for non-web applications in .NET 8 and later.</remarks>
        public static WebApplicationBuilder AddLoupe(this WebApplicationBuilder builder, Action<AgentConfiguration> configure = null)
        {
            builder.Host.AddLoupe(configure);

            return builder;
        }

        /// <summary>
        /// Adds the main Loupe Agent.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/>.</param>
        /// <param name="agentBuilder">A Loupe Agent builder delegate</param>
        /// <param name="configure">Optional.  An Agent configuration delegate</param>
        /// <returns>The <see cref="WebApplicationBuilder"/>.</returns>
        /// <remarks>This is used for non-web applications in .NET 8 and later.</remarks>
        public static WebApplicationBuilder AddLoupe(this WebApplicationBuilder builder, Action<ILoupeAgentBuilder> agentBuilder, Action<AgentConfiguration> configure = null)
        {
            builder.Host.AddLoupe(agentBuilder, configure);

            return builder;
        }

#endif

        /// <summary>
        /// Add a Loupe middleware to time requests and log errors.
        /// </summary>
        public static IApplicationBuilder UseLoupe(this IApplicationBuilder app) => app.UseMiddleware<LoupeMiddleware>();

        /// <summary>
        /// Add Loupe session cookies to all requests
        /// </summary>
        /// <remarks>
        /// Loupe defaults to using a Session cookie with HttpOnly, Secure, and SameSite = None
        /// which works with Cross-Origin requests. If this does not work, use the
        /// <see cref="UseLoupeCookies(Microsoft.AspNetCore.Builder.IApplicationBuilder, Microsoft.AspNetCore.Http.CookieOptions)"/>
        /// overload to specify your own cookie settings.
        /// </remarks>
        public static IApplicationBuilder UseLoupeCookies(this IApplicationBuilder app)
            => UseLoupeCookies(app, null);

        /// <summary>
        /// Add Loupe session cookies to all requests
        /// </summary>
        /// <param name="cookieOptions">Override the default cookie options</param>
        /// <remarks>
        /// Loupe defaults to using a Session cookie with HttpOnly, Secure, and SameSite = None
        /// which works with Cross-Origin requests.
        /// </remarks>
        public static IApplicationBuilder UseLoupeCookies(this IApplicationBuilder app, CookieOptions? cookieOptions)
            => app.UseMiddleware<LoupeCookieMiddleware>(cookieOptions ?? DefaultCookieOptions());

        // As of Chrome v80, SameSite defaults to Lax, which won't set the cookie cross-domain
        // Explicitly setting SameSite to None requires the Secure flag to be set too
        private static CookieOptions DefaultCookieOptions() =>
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.None,
                Secure = true,
            };
    }
}