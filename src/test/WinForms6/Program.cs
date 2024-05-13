using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gibraltar.Agent;
using Loupe.Agent.Core.Services;
using Loupe.Agent.PerformanceCounters;
using Loupe.Extensibility.Data;
using Loupe.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace WinForms6
{
    internal static class Program
    {
        private static IServiceProvider _serviceProvider;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static async Task Main()
        {
            // Initialize the standard host builder so the agent is fired up and appsettings.json is loaded.
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Since we're on Windows, we can add performance counters.
                    services.AddLoupe(builder => builder.AddPerformanceCounters());

                    // Add Loupe's support for Microsoft.Extensions.Logging.
                    services.AddLoupeLogging();

                    // Add your services here (optional).
                })
                .Build();

            _serviceProvider = host.Services;

            var logger = _serviceProvider.GetRequiredService<ILogger<MainForm>>();

            try
            {
                await host.StartAsync(); // This will start background services like the LoupeAgentService.

                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm(logger));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Application exiting due to unhandled {Exception}", ex.GetBaseException().GetType().Name);
                Log.EndSession(SessionStatus.Crashed, "Application failed due to unhandled exception");
                await Log.SendSessions(SessionCriteria.ActiveSession);
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}