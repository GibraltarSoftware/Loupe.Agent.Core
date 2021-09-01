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
        public static string GetAgentSessionId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(Constants.ClientHeaderName, out var header) && header.Count > 0)
            {
                context.Items[Constants.AgentSessionId] = header[0];
                return header;
            }
            else
            {
                context.Items[Constants.AgentSessionId] = string.Empty;
                return null;
            }
        }
    }
}