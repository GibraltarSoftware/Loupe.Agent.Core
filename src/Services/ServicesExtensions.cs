using System;
using Loupe.Agent.Core.Services.Internal;
using Loupe.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loupe.Agent.Core.Services
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServicesExtensions
    {
        /*
        /// <summary>
        /// Adds Loupe to the services with a configuration callback.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configure">A callback to be invoked after configuration is loaded.</param>
        /// <returns>An instance of <see cref="ILoupeAgentBuilder"/> for further customization.</returns>
        public static ILoupeAgentBuilder AddLoupe(this IServiceCollection services, Action<AgentConfiguration> configure)
        {
            AddOptions(services, configure);
            services.AddSingleton<IHostedService, LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();
            return new LoupeAgentServicesCollectionBuilder(services);
        }

        /// <summary>
        /// Adds Loupe to the services collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>An instance of <see cref="ILoupeAgentBuilder"/> for further customization.</returns>
        [Obsolete("Returns nonstandard type")]
        public static ILoupeAgentBuilder AddLoupe(this IServiceCollection services)
        {
            AddOptions(services);
            services.AddSingleton<IHostedService, LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();
            return new LoupeAgentServicesCollectionBuilder(services);
        }
        */

        /// <summary>
        /// Adds Loupe to the services collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configure">Optional.  An Agent configuration delegate</param>
        /// <returns>The modified <see cref="IServiceCollection"/></returns>
        public static IServiceCollection AddLoupe(this IServiceCollection services, Action<AgentConfiguration> configure = null)
        {
            AddOptions(services, configure);
            services.AddSingleton<IHostedService, LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();

            return services;
        }

        /// <summary>
        /// Adds Loupe to the services collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="agentBuilder">The <see cref="ILoupeAgentBuilder"/> delegate for further customization.</param>
        /// <param name="configure">Optional.  An Agent configuration delegate</param>
        /// <returns>The modified <see cref="IServiceCollection"/></returns>
        public static IServiceCollection AddLoupe(this IServiceCollection services,
            Action<ILoupeAgentBuilder> agentBuilder, Action<AgentConfiguration> configure = null)
        {
            AddOptions(services, configure);
            services.AddSingleton<IHostedService, LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();

            if (agentBuilder != null)
            {
                var agentServices = new LoupeAgentServicesCollectionBuilder(services);
                agentBuilder(agentServices);
            }

            return services;
        }

#if !NETSTANDARD2_0 && !NET461
        /// <summary>
        /// Adds the main Loupe Agent.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/>.</param>
        /// <param name="configure">Optional.  An Agent configuration delegate</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder AddLoupe(this IHostBuilder builder, Action<AgentConfiguration> configure = null)
        {
            return builder.ConfigureServices((context, services) =>
            {
                AddOptions(services, configure);
                services.AddSingleton<IHostedService, LoupeAgentService>();
                services.AddSingleton<LoupeAgent>();
            });
        }

        /// <summary>
        /// Adds the main Loupe Agent.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/>.</param>
        /// <param name="agentBuilder">A Loupe Agent builder delegate</param>
        /// <param name="configure">Optional.  An Agent configuration delegate</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder AddLoupe(this IHostBuilder builder, Action<ILoupeAgentBuilder> agentBuilder, Action<AgentConfiguration> configure = null)
        {
            return builder.ConfigureServices((context, services) =>
            {
                AddOptions(services, configure);
                services.AddSingleton<IHostedService, LoupeAgentService>();
                services.AddSingleton<LoupeAgent>();

                agentBuilder(new LoupeAgentServicesCollectionBuilder(services));
            });
        }

#endif

        private static void AddOptions(IServiceCollection services, Action<AgentConfiguration> configure = null)
        {
            // Set up a configuration callback
            if (configure == null)
            {
                services.AddSingleton(_ => new LoupeAgentConfigurationCallback());
            }
            else
            {
                services.AddSingleton(_ => new LoupeAgentConfigurationCallback(configure));
            }

#if NET461 || NETSTANDARD2_0
            // Set up options for AgentConfiguration with callback and default ApplicationName from IHostingEnvironment
            services.AddOptions<AgentConfiguration>().Configure<IConfiguration, IHostingEnvironment, LoupeAgentConfigurationCallback>(
                (options, configuration, hostingEnvironment, callback) =>
                {
                    configuration.Bind("Loupe", options);
                    callback?.Invoke(options);
                });
#else
            // Set up options for AgentConfiguration with callback and default ApplicationName from IHostingEnvironment
            services.AddOptions<AgentConfiguration>().Configure<IConfiguration, IHostEnvironment, LoupeAgentConfigurationCallback>(
                (options, configuration, hostEnvironment, callback) =>
                {
                    configuration.Bind("Loupe", options);
                    callback?.Invoke(options);
                });
#endif
        }
    }
}