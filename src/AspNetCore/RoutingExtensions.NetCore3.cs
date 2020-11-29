#if(!NETCOREAPP2_1)
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Provides useful extension methods over Routing
    /// </summary>
    public static class RoutingExtensions
    {
        /// <summary>
        /// Gets the route as a readable string.
        /// </summary>
        /// <param name="httpContext">The current context</param>
        /// <returns>A readable string form of the route, if available; otherwise, <c>null</c>.</returns>
        public static string GetRouteString(this HttpContext httpContext)
        {
            var endpoint = httpContext.GetEndpoint();
            if (endpoint is RouteEndpoint routeEndpoint)
            {
                return routeEndpoint.RoutePattern.RawText;
            }

            return endpoint.DisplayName;
        }
    }
}
#endif
