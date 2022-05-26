using System.Threading;
using System.Threading.Tasks;
using Loupe.Extensibility.Data;
using Microsoft.Extensions.Hosting;

namespace Loupe.Agent.Core.Services
{
    /// <summary>
    /// An implementation of <see cref="IHostedService"/> that exists to force the creation of a <see cref="LoupeAgent"/>
    /// by the Dependency Injection container and to hold the reference until the application ends.
    /// </summary>
    /// <seealso cref="Microsoft.Extensions.Hosting.IHostedService" />
    internal sealed class LoupeAgentService : BackgroundService
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

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tcs = new TaskCompletionSource<object>();
            stoppingToken.Register(() =>
            {
                _agent.End(SessionStatus.Normal, "ApplicationStopped");
                tcs.SetResult(null);
            });
            return tcs.Task;
        }

        public override void Dispose()
        {
            _agent.Dispose();
            base.Dispose();
        }
    }
}