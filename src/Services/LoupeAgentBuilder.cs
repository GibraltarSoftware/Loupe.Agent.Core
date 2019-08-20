using System;
using System.Diagnostics;
using System.Security.Principal;
using Loupe.Agent;
using Loupe.Core;
using Loupe.Core.Monitor;
using Loupe.Extensibility;
using Loupe.Extensibility.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Loupe.Agent.Core.Services
{
    /// <summary>Default implementation of <see cref="ILoupeAgentBuilder"/>.</summary>
    internal sealed class LoupeAgentBuilder : ILoupeAgentBuilder
    {
        private readonly IServiceCollection _services;

        /// <summary>Initializes a new instance of the <see cref="LoupeAgentBuilder"/> class.</summary>
        /// <param name="services">The services container.</param>
        public LoupeAgentBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddListener<T>() where T : class, ILoupeDiagnosticListener
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoupeDiagnosticListener, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddMonitor<T>() where T : class, ILoupeMonitor
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoupeMonitor, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddFilter<T>() where T : class, ILoupeFilter
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoupeFilter, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddPrincipalResolver<T>() where T : class, IPrincipalResolver
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrincipalResolver, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddPrincipalResolver(Func<IPrincipal> func)
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrincipalResolver, DelegatePrincipalResolver>(resolver => new DelegatePrincipalResolver(func)));
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddApplicationUserProvider<T>() where T : class, IApplicationUserProvider
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<IApplicationUserProvider, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddApplicationUserProvider(Func<IPrincipal, Lazy<IApplicationUser>, bool> func)
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<IApplicationUserProvider, DelegateApplicationUserProvider>(provider => new DelegateApplicationUserProvider(func)));
            return this;
        }
    }
}