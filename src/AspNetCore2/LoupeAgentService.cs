using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Loupe.Agent.AspNetCore
{
    internal sealed class LoupeAgentService : IHostedService
    {
        private readonly LoupeAgent _agent;

        public LoupeAgentService(LoupeAgent agent)
        {
            _agent = agent;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}