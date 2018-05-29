using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Loupe.Agent.Core.Services
{
    internal sealed class LoupeAgentBuilder : ILoupeAgentBuilder
    {
        private readonly IServiceCollection _services;

        public LoupeAgentBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public ILoupeAgentBuilder AddListener<T>() where T : class, ILoupeDiagnosticListener
        {
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoupeDiagnosticListener, T>());
            return this;
        }
    }
}