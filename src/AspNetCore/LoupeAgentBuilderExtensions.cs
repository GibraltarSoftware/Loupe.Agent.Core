using System;
using System.Security.Principal;
using Gibraltar.Monitor;
using Loupe.Agent.AspNetCore.Infrastructure;
using Loupe.Agent.AspNetCore.Metrics;
using Loupe.Agent.Core.Services;
using Microsoft.Extensions.DependencyInjection;

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
        public static ILoupeAgentBuilder AddAspNetCoreDiagnostics(this ILoupeAgentBuilder builder) => builder.AddListener<ActionDiagnosticListener>()
            .AddPrincipalResolver<ClaimsPrincipalResolver>()
            .AddApplicationUserProvider<ClaimsPrincipalApplicationUserProvider>();

        /// <summary>
        /// Adds ASP.NET Core diagnostic listeners for standard Loupe metric generation.
        /// </summary>
        /// <param name="builder">The <see cref="ILoupeAgentBuilder"/> instance.</param>
        /// <param name="applicationUserFunc">The function to use for application user mapping from an IPrincipal</param>
        /// <returns>The <see cref="ILoupeAgentBuilder"/> instance.</returns>
        public static ILoupeAgentBuilder AddAspNetCoreDiagnostics(this ILoupeAgentBuilder builder,
            Func<IPrincipal, Lazy<ApplicationUser>, bool> applicationUserFunc)
        {
            return AddAspNetCoreDiagnostics(builder, null, applicationUserFunc);
        }

        /// <summary>
        /// Adds ASP.NET Core diagnostic listeners for standard Loupe metric generation.
        /// </summary>
        /// <param name="builder">The <see cref="ILoupeAgentBuilder"/> instance.</param>
        /// <param name="principalFunc">The function to use for IPrincipal resolution</param>
        /// <param name="applicationUserFunc">Optional. The function to use for application user mapping from an IPrincipal</param>
        /// <returns>The <see cref="ILoupeAgentBuilder"/> instance.</returns>
        public static ILoupeAgentBuilder AddAspNetCoreDiagnostics(this ILoupeAgentBuilder builder,
            Func<IPrincipal>? principalFunc,
            Func<IPrincipal, Lazy<ApplicationUser>, bool>? applicationUserFunc = null)
        {
            builder = builder.AddListener<ActionDiagnosticListener>();

            builder = (principalFunc == null) 
                ? builder.AddPrincipalResolver<ClaimsPrincipalResolver>() 
                : builder;

            builder = (applicationUserFunc == null)
                ? builder.AddApplicationUserProvider<ClaimsPrincipalApplicationUserProvider>()
                : builder.AddApplicationUserProvider(applicationUserFunc);

            return builder;
        }

        /// <summary>
        /// Adds services required for client-side logging endpoint
        /// </summary>
        public static ILoupeAgentBuilder AddClientLogging(this ILoupeAgentBuilder builder)
        {
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<RequestProcessor>();
            builder.Services.AddSingleton<ILoupeLog, DefaultLoupeLog>();
            builder.Services.AddSingleton<JavaScriptLogger>();
            return builder;
        }
    }
}