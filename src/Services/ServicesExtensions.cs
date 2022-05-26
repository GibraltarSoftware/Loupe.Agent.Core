using System;
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
        /// <summary>
        /// Adds Loupe to the services with a configuration callback.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="configure">A callback to be invoked after configuration is loaded.</param>
        /// <returns>An instance of <see cref="ILoupeAgentBuilder"/> for further customization.</returns>
        public static ILoupeAgentBuilder AddLoupe(this IServiceCollection services, Action<AgentConfiguration> configure)
        {
            AddOptions(services, configure);
            services.AddHostedService<LoupeAgentService>();
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
            AddOptions(services);
            services.AddHostedService<LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();
            return new LoupeAgentBuilder(services);
        }

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

            // Set up options for AgentConfiguration with callback and default ApplicationName from IHostingEnvironment
            services.AddOptions<AgentConfiguration>().Configure<IConfiguration, IHostingEnvironment, LoupeAgentConfigurationCallback>(
                (options, configuration, hostingEnvironment, callback) =>
                {
                    configuration.Bind("Loupe", options);
                    callback?.Invoke(options);
                    if (options.Packager == null)
                    {
                        options.Packager = new PackagerConfiguration {ApplicationName = hostingEnvironment.ApplicationName};
                    }
                    else if (options.Packager.ApplicationName == null)
                    {
                        options.Packager.ApplicationName = hostingEnvironment.ApplicationName;
                    }
                });
        }
    }
}