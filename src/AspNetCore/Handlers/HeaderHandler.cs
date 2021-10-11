using Loupe.Agent.AspNetCore.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Handlers
{
    internal static class HeaderHandler
    {
        /// <summary>
        /// Retrieves the Loupe Agent Session Id from the request header, if present
        /// </summary>
        /// <param name="context"></param>
        /// <returns>The header value or null of no header was present.</returns>
        public static string? GetAgentSessionId(HttpContext context)
        {
            string? sessionId = null;
            if (context.Request.Headers.TryGetValue(Constants.AgentSessionIdHeaderName, out var header) && header.Count > 0)
            {
                sessionId = header[0];
            }

            context.SetAgentSessionId(sessionId);
            return sessionId;
        }
    }
}