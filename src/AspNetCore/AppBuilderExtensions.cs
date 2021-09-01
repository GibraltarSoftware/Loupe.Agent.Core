using System;
using System.Threading.Tasks;
using Loupe.Agent.AspNetCore.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

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
        public static IApplicationBuilder UseLoupe(this IApplicationBuilder app) => app.UseMiddleware<LoupeMiddleware>();
        
        /// <summary>
        /// Add Loupe session cookies to all requests
        /// </summary>
        public static IApplicationBuilder UseLoupeCookies(this IApplicationBuilder app) => app.UseMiddleware<LoupeCookieMiddleware>();
    }
}