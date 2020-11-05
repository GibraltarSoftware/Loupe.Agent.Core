using System;
using System.Text.Json;
using System.Threading.Tasks;
using Gibraltar.Agent;
using Loupe.Agent.AspNetCore.Infrastructure;
using Loupe.Agent.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Convenience extension methods for EndpointRouteBuilder
    /// </summary>
    public static class EndpointRouteBuilderExtensions
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
#if (NETCOREAPP3_1)
        /// <summary>
        /// Add the Loupe JS client logging endpoint
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/></param>
        /// <param name="pattern">The URL pattern for the endpoint. Defaults to <c>/loupe/log</c></param>
        public static void MapLoupeClientLogger(this IEndpointRouteBuilder endpoints, string pattern = "/loupe/log")
        {
            endpoints.MapPost(pattern, Log);
        }
#else
        /// <summary>
        /// Add the Loupe JS client logging endpoint
        /// </summary>
        /// <param name="app"></param>
        /// <param name="pattern">The URL pattern for the endpoint. Defaults to <c>/loupe/log</c></param>
        /// <returns></returns>
        public static IApplicationBuilder AddLoupeClientLogger(this IApplicationBuilder app, string pattern = "/loupe/log")
        {
            var path = new PathString(pattern);
            
            return app.Use(async (context, next) =>
            {
                if (context.Request.Path.Equals(path))
                {
                    await Log(context);
                    return;
                }

                await next();
            });
        }
#endif

        private static async Task Log(HttpContext context)
        {
            var requestProcessor = context.RequestServices.GetRequiredService<RequestProcessor>();
            
            var logRequest = await JsonSerializer.DeserializeAsync<LogRequest>(context.Request.Body, JsonOptions);

            try
            {
                requestProcessor.Process(context, logRequest);
                context.Response.StatusCode = 200;
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                var detailsJson = JsonSerializer.Serialize(logRequest);
                Gibraltar.Agent.Log.Write(LogMessageSeverity.Critical, Constants.LogSystem, 0, ex, LogWriteMode.Queued,
                    context.StandardXmlRequestBlock(detailsJson), Constants.Category, "Unable to process message due to " + ex.GetType(),
                    "Exception caught in top level catch block, this should have be caught by error handler specific to the part of the request processing that failed.");
#endif
                context.Response.StatusCode = 500;
            }
        }
    }
}