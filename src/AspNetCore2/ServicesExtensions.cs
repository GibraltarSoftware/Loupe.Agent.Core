using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Loupe.Agent.AspNetCore.Metrics;
using Loupe.Agent.AspNetCore.Metrics.AspNetCore;
using Loupe.Agent.AspNetCore.Metrics.EntityFrameworkCore;
using Loupe.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loupe.Agent.AspNetCore
{
    public static class ServicesExtensions
    {
        public static IServiceCollection AddLoupe(this IServiceCollection services, Action<AgentConfiguration> configure)
        {
            services.AddSingleton(_ => new LoupeAgentConfigurationCallback(configure));
            RegisterDiagnosticListeners(services);
            services.AddSingleton<IHostedService, LoupeAgentService>();
            return services.AddSingleton<LoupeAgent>();
        }

        public static IServiceCollection AddLoupe(this IServiceCollection services)
        {
            services.AddSingleton(_ => new LoupeAgentConfigurationCallback());
            RegisterDiagnosticListeners(services);
            services.AddSingleton<IHostedService, LoupeAgentService>();
            return services.AddSingleton<LoupeAgent>();
        }

        private static void RegisterDiagnosticListeners(IServiceCollection services)
        {
            services.AddSingleton<ILoupeDiagnosticListener, EntityFrameworkCoreDiagnosticListener>();
            services.AddSingleton<ILoupeDiagnosticListener, ActionDiagnosticListener>();
        }
    }
}