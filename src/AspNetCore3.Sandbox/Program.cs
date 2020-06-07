using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loupe.Agent.AspNetCore;
using Loupe.Agent.Core.Services;
using Loupe.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCore3.Sandbox
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .AddLoupe(b => b.AddAspNetCoreDiagnostics())
                .AddLoupeLogging()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
