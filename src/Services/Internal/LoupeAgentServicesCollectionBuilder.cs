using System;
using System.Diagnostics;
using System.Security.Principal;
using Gibraltar.Agent;
using Gibraltar.Monitor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Loupe.Agent.Core.Services.Internal
{
    /// <summary>Default implementation of <see cref="ILoupeAgentBuilder"/>.</summary>
    internal sealed class LoupeAgentServicesCollectionBuilder : ILoupeAgentBuilder
    {
        public IServiceCollection Services { get; }

        /// <summary>Initializes a new instance of the <see cref="LoupeAgentServicesCollectionBuilder"/> class.</summary>
        /// <param name="services">The services container.</param>
        public LoupeAgentServicesCollectionBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddListener<T>() where T : class, ILoupeDiagnosticListener
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoupeDiagnosticListener, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddMonitor<T>() where T : class, ILoupeMonitor
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoupeMonitor, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddFilter<T>() where T : class, ILoupeFilter
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoupeFilter, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddPrincipalResolver<T>() where T : class, IPrincipalResolver
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrincipalResolver, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddPrincipalResolver(Func<IPrincipal> func)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrincipalResolver, DelegatePrincipalResolver>(resolver => new DelegatePrincipalResolver(func)));
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddApplicationUserProvider<T>() where T : class, IApplicationUserProvider
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<IApplicationUserProvider, T>());
            return this;
        }

        /// <inheritdoc />
        public ILoupeAgentBuilder AddApplicationUserProvider(Func<IPrincipal, Lazy<ApplicationUser>, bool> func)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<IApplicationUserProvider, DelegateApplicationUserProvider>(provider => new DelegateApplicationUserProvider(func)));
            return this;
        }
    }
}