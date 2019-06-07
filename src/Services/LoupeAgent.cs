using System;
using Gibraltar.Agent;
using Gibraltar.Monitor;
using Loupe.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Log = Gibraltar.Agent.Log;
using Microsoft.Extensions.Options;

namespace Loupe.Agent.Core.Services
{
    /// <summary>
    /// The main Loupe agent.
    /// </summary>
    public sealed class LoupeAgent : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AgentConfiguration _agentConfiguration;
        private readonly LoupeDiagnosticListener _diagnosticListener;

        /// <summary>Initializes a new instance of the <see cref="LoupeAgent"/> class.</summary>
        /// <param name="options"><see cref="AgentConfiguration"/> options.</param>
        /// <param name="serviceProvider">The DI service provider.</param>
        /// <param name="applicationLifetime">The application lifetime object.</param>
        /// <exception cref="ArgumentNullException">callback
        /// or
        /// configuration
        /// or
        /// hostingEnvironment</exception>
        public LoupeAgent(IOptions<AgentConfiguration> options, IServiceProvider serviceProvider, IApplicationLifetime applicationLifetime)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _serviceProvider = serviceProvider;
            _agentConfiguration = options.Value;
            ApplicationName = _agentConfiguration.Packager.ApplicationName;
            _diagnosticListener = new LoupeDiagnosticListener();
            applicationLifetime.ApplicationStarted.Register(Start);
            applicationLifetime.ApplicationStopped.Register(() => End(SessionStatus.Normal, "ApplicationStopped"));
        }

        /// <summary>Starts this Agent instance.</summary>
        public void Start()
        {
            Log.StartSession(_agentConfiguration);
            foreach (var listener in _serviceProvider.GetServices<ILoupeDiagnosticListener>())
            {
                _diagnosticListener.Add(listener);
            }
            _diagnosticListener.Subscribe();

            foreach (var monitor in _serviceProvider.GetServices<ILoupeMonitor>())
            {
                Monitor.Subscribe(monitor);
            }
        }

        /// <summary>Stops this Agent instance.</summary>
        /// <param name="status">The session status.</param>
        /// <param name="reason">The reason.</param>
        public void End(SessionStatus status, string reason)
        {
            Log.EndSession(status, reason);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            _diagnosticListener.Dispose();
        }

        /// <summary>Gets the name of the application.</summary>
        /// <value>The name of the application.</value>
        public string ApplicationName { get; }
    }
}