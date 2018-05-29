using System;
using Loupe.Agent.Core.Services;
using Loupe.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Agent.EntityFrameworkCore
{
    public static class LoupeAgentBuilderExtensions
    {
        public static ILoupeAgentBuilder AddEntityFrameworkCoreDiagnostics(this ILoupeAgentBuilder builder) => builder.AddListener<EntityFrameworkCoreDiagnosticListener>();
    }
}