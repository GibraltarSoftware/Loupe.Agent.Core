
using Loupe.Agent.Core.Services;
using Loupe.Agent.AspNetCore;
using Loupe.Agent.PerformanceCounters;
using Loupe.Extensions.Logging;

namespace WebApi8
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddHttpContextAccessor(); //Required Service for Loupe

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Host.AddLoupe(l => l.AddAspNetCoreDiagnostics()
                    .AddPerformanceCounters()) //add optional agents here
                .AddLoupeLogging();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
