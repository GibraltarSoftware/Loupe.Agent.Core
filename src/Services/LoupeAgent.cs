using System;
using Gibraltar.Agent;
using Gibraltar.Monitor;
using Loupe.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Log = Gibraltar.Agent.Log;

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
        /// <param name="callback">A callback to modify configuration from code.</param>
        /// <param name="configuration">The ASP.NET Core configuration instance.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <param name="serviceProvider">The DI service provider.</param>
        /// <param name="applicationLifetime">The application lifetime object.</param>
        /// <exception cref="ArgumentNullException">callback
        /// or
        /// configuration
        /// or
        /// hostingEnvironment</exception>
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
                Listener.Subscribe(monitor);
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