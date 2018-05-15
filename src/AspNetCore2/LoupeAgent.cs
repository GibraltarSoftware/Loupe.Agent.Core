using System;
using System.Diagnostics;
using Gibraltar.Agent;
using Loupe.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loupe.Agent.AspNetCore
{
    public sealed class LoupeAgent : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AgentConfiguration _agentConfiguration;
        private readonly LoupeDiagnosticListener _diagnosticListener;

        public LoupeAgent(LoupeAgentConfigurationCallback callback, IConfiguration configuration, IHostingEnvironment hostingEnvironment, IServiceProvider serviceProvider, IApplicationLifetime applicationLifetime)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (hostingEnvironment == null) throw new ArgumentNullException(nameof(hostingEnvironment));
            _serviceProvider = serviceProvider;

            _agentConfiguration =
                new AgentConfiguration {Packager = {ApplicationName = hostingEnvironment.ApplicationName}};
            configuration.Bind("Loupe", _agentConfiguration);
            callback.Invoke(_agentConfiguration);
            ApplicationName = _agentConfiguration.Packager.ApplicationName;
            _diagnosticListener = new LoupeDiagnosticListener();
            applicationLifetime.ApplicationStarted.Register(Start);
            applicationLifetime.ApplicationStopped.Register(() => End(SessionStatus.Normal, "ApplicationStopped"));
        }

        public void Start()
        {
            Log.StartSession(_agentConfiguration);
            foreach (var listener in _serviceProvider.GetServices<ILoupeDiagnosticListener>())
            {
                _diagnosticListener.Add(listener);
            }
            _diagnosticListener.Subscribe();
        }

        public void End(SessionStatus status, string reason)
        {
            Log.EndSession(status, reason);
        }

        public void Dispose()
        {
            _diagnosticListener.Dispose();
        }

        public string ApplicationName { get; }
    }
}