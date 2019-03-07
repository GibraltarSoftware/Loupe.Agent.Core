using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Loupe.Agent.Core.Services
{
    /// <summary>
    /// An implementation of <see cref="IHostedService"/> that exists to force the creation of a <see cref="LoupeAgent"/>
    /// by the Dependency Injection container and to hold the reference until the application ends.
    /// </summary>
    /// <seealso cref="Microsoft.Extensions.Hosting.IHostedService" />
    internal sealed class LoupeAgentService : IHostedService
    {
        private readonly LoupeAgent _agent;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoupeAgentService"/> class.
        /// </summary>
        /// <param name="agent">The agent.</param>
        public LoupeAgentService(LoupeAgent agent)
        {
            _agent = agent;
        }

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to abort the call.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to abort the call.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}