using Loupe.Agent.AspNetCore.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MvcTestApp;

namespace Loupe.Agent.AspNetCore.Tests
{
    public class TestApplicationFactory : WebApplicationFactory<Startup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(ConfigureServices);
        }
        
        public ILoupeLog MockLog { get; set; }

        private void ConfigureServices(IServiceCollection services)
        {
            if (MockLog is null) return;
            services.RemoveAll<ILoupeLog>();
            services.AddSingleton(MockLog);
        }
    }
}