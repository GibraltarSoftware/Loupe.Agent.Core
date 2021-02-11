using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loupe.Agent.AspNetCore;
using Loupe.Agent.Core.Services;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MvcTestApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            /*
             * If your web application runs on a different port or host
             * then you will need to enable CORS support, to enable the web
             * application to log to this application. For example, the
             * following adds support for an application running locally on
             * the default Angular port:
             
            services.AddCors(options =>
            {
                options.AddPolicy("AllowOrigin", builder =>
                {
                    builder
                        .WithOrigins("http://localhost:4200")
                        .WithOrigins("https://localhost:4200")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            */

            /*
             * Add Loupe and the Loupe Web Client logging interceptor
             */
            services
                .AddLoupe()
                .AddClientLogging();

            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseLoupeCookies();

            app.UseRouting();

            /*
             * If you have added CORS support in ConfigureServices above
             * you need to use that policy here
             
            app.UseCors("AllowOrigin");

            */

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                
                endpoints.MapLoupeClientLogger();
            });
        }
    }
}
