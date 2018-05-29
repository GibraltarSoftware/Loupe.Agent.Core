using System;
using Loupe.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loupe.Agent.Core.Services
{
    public static class ServicesExtensions
    {
        public static ILoupeAgentBuilder AddLoupe(this IServiceCollection services, Action<AgentConfiguration> configure)
        {
            services.AddSingleton(_ => new LoupeAgentConfigurationCallback(configure));
            services.AddSingleton<IHostedService, LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();
            return new LoupeAgentBuilder(services);
        }

        public static ILoupeAgentBuilder AddLoupe(this IServiceCollection services)
        {
            services.AddSingleton(_ => new LoupeAgentConfigurationCallback());
            services.AddSingleton<IHostedService, LoupeAgentService>();
            services.AddSingleton<LoupeAgent>();
            return new LoupeAgentBuilder(services);
        }
    }
}