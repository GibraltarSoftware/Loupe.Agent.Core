using Loupe.Agent.AspNetCore;
using Loupe.Agent.Core.Services;
using Loupe.Agent.EntityFrameworkCore;
using Loupe.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loupe.Agent.PerformanceCounters;

namespace MvcTestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .AddLoupe(builder => builder.AddAspNetCoreDiagnostics()
                    .AddClientLogging() //The Loupe endpoint feature for your ASP.NET API
                    .AddPerformanceCounters() //Windows Perf Counters
                    .AddEntityFrameworkCoreDiagnostics(), //EF Core monitoring
                    config => config.Properties.Add("Run Id", Guid.NewGuid().ToString()))
                .AddLoupeLogging()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
