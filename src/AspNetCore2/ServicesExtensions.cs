﻿using System;
using System.Runtime.CompilerServices;
using Loupe.Agent.AspNetCore.Metrics;
using Loupe.Agent.AspNetCore.Metrics.AspNetCore;
using Loupe.Agent.AspNetCore.Metrics.EntityFrameworkCore;
using Loupe.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Agent.AspNetCore
{
    public static class ServicesExtensions
    {
        public static IServiceCollection AddLoupe(this IServiceCollection services, Action<AgentConfiguration> configure)
        {
            services.AddSingleton(_ => new LoupeAgentConfigurationCallback(configure));
            RegisterDiagnosticListeners(services);
            return services.AddSingleton<LoupeAgent>();
        }

        public static IServiceCollection AddLoupe(this IServiceCollection services)
        {
            services.AddSingleton(_ => new LoupeAgentConfigurationCallback());
            RegisterDiagnosticListeners(services);
            return services.AddSingleton<LoupeAgent>();
        }

        private static void RegisterDiagnosticListeners(IServiceCollection services)
        {
            services.AddSingleton<ILoupeDiagnosticListener, EntityFrameworkCoreDiagnosticListener>();
            services.AddSingleton<ILoupeDiagnosticListener, ActionDiagnosticListener>();
        }
    }
}