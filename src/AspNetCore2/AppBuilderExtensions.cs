using Microsoft.AspNetCore.Builder;

namespace Loupe.Agent.AspNetCore
{
    public static class AppBuilderExtensions
    {
        public static IApplicationBuilder UseLoupe(this IApplicationBuilder app) => app.UseMiddleware<LoupeMiddleware>();
    }
}