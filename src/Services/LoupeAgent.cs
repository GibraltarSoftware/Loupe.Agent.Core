using System;
using Gibraltar.Agent;
using Gibraltar.Monitor;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
#if NETSTANDARD2_0 || NET461
using Microsoft.Extensions.Hosting;
#else
using Microsoft.Extensions.Hosting;
#endif
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
        private readonly LoupeDiagnosticListener _diagnosticListener;

#if NETSTANDARD2_0 || NET461
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
            Configuration = options.Value;
            _diagnosticListener = new LoupeDiagnosticListener();
            applicationLifetime.ApplicationStarted.Register(Start);
            applicationLifetime.ApplicationStopped.Register(() => End(SessionStatus.Normal, "ApplicationStopped"));
        }
#else
        /// <summary>Initializes a new instance of the <see cref="LoupeAgent"/> class.</summary>
        /// <param name="options"><see cref="AgentConfiguration"/> options.</param>
        /// <param name="serviceProvider">The DI service provider.</param>
        /// <param name="applicationLifetime">The application lifetime object.</param>
        /// <exception cref="ArgumentNullException">callback
        /// or
        /// configuration
        /// or
        /// hostingEnvironment</exception>
        public LoupeAgent(IOptions<AgentConfiguration> options, IServiceProvider serviceProvider, IHostApplicationLifetime applicationLifetime)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _serviceProvider = serviceProvider;
            Configuration = options.Value;
            _diagnosticListener = new LoupeDiagnosticListener();
            applicationLifetime.ApplicationStarted.Register(Start);
            applicationLifetime.ApplicationStopped.Register(() => End(SessionStatus.Normal, "ApplicationStopped"));
        }
#endif
        /// <summary>Starts this Agent instance.</summary>
        public void Start()
        {
            Log.StartSession(Configuration);
            foreach (var listener in _serviceProvider.GetServices<ILoupeDiagnosticListener>())
            {
                _diagnosticListener.Add(listener);
            }
            _diagnosticListener.Subscribe();

            foreach (var monitor in _serviceProvider.GetServices<ILoupeMonitor>())
            {
                Monitor.Subscribe(monitor);
            }

            foreach (var filter in _serviceProvider.GetServices<ILoupeFilter>())
            {
                Gibraltar.Monitor.Log.RegisterFilter(filter);
            }

            var principalResolver = _serviceProvider.GetService<IPrincipalResolver>();
            if (principalResolver != null)
            {
                Log.PrincipalResolver = principalResolver;
            }

            var userResolver = _serviceProvider.GetService<IApplicationUserProvider>();
            if (userResolver != null)
            {
                Log.ApplicationUserProvider = userResolver;
            }
        }

        /// <summary>
        /// The configuration used for the Loupe Agent
        /// </summary>
        public AgentConfiguration Configuration { get; }

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
    }
}