using Loupe.Agent.AspNetCore.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Handlers
{
    internal static class HeaderHandler
    {
        public static void Handle(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(Constants.ClientHeaderName, out var header) && header.Count > 0)
            {
                context.Items[Constants.AgentSessionId] = header[0];
            }
            else
            {
                context.Items[Constants.AgentSessionId] = string.Empty;
            }
        }
    }
}