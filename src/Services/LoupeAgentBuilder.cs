using System.Diagnostics;
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

        /// <summary>Adds a <see cref="DiagnosticSource"/> listener.</summary>
        /// <typeparam name="T">The type of the listener, which must implement <see cref="ILoupeDiagnosticListener"/>.</typeparam>
        /// <returns>The builder instance.</returns>
        public ILoupeAgentBuilder AddListener<T>() where T : class, ILoupeDiagnosticListener
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoupeDiagnosticListener, T>());
            return this;
        }
    }
}