using System;
using Loupe.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loupe.Agent.Core.Services
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServicesExtensions
    {
        /// <summary>
        /// Adds Loupe to the services with a configuration callback.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configure">A callback to be invoked after configuration is loaded.</param>
        /// <returns>An instance of <see cref="ILoupeAgentBuilder"/> for further customization.</returns>
        public static ILoupeAgentBuilder AddLoupe(this IServiceCollection services, Action<AgentConfiguration> configure)
        {
            services.AddSingleton(_ => new LoupeAgentConfigurationCallback(configure));
            services.AddSingleton<IHostedService, LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();
            return new LoupeAgentBuilder(services);
        }

        /// <summary>
        /// Adds Loupe to the services with a configuration callback.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>An instance of <see cref="ILoupeAgentBuilder"/> for further customization.</returns>
        public static ILoupeAgentBuilder AddLoupe(this IServiceCollection services)
        {
            services.AddSingleton(_ => new LoupeAgentConfigurationCallback());
            services.AddSingleton<IHostedService, LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();
            return new LoupeAgentBuilder(services);
        }
    }
}