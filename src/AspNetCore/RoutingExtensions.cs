#if(!NETCORE3)
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Routing;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Provides useful extension methods over <see cref="IRoutingFeature"/>.
    /// </summary>
    public static class RoutingExtensions
    {
        /// <summary>
        /// Gets the route as a readable string.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> instance.</param>
        /// <returns>A readable string form of the route, if available; otherwise, <c>null</c>.</returns>
        public static string? GetRouteString(this HttpContext httpContext)
        {
            var routing = httpContext.Features.Get<IRoutingFeature>();
            if (routing == null) return null;

            var templateRoute = routing.RouteData.Routers.OfType<Route>().FirstOrDefault();
            if (templateRoute != null)
            {
                if (routing.RouteData.Values.TryGetValue("controller", out var controller)
                    && routing.RouteData.Values.TryGetValue("action", out var action))
                {
                    return $"{controller}.{action}";
                }
            }

            var attributeRoute = routing.RouteData.Routers.OfType<MvcAttributeRouteHandler>().FirstOrDefault();
            return attributeRoute?.Actions.FirstOrDefault()?.AttributeRouteInfo?.Template;
        }

        /// <summary>
        /// Gets the page name as a readable string.
        /// </summary>
        /// <param name="routing">The <see cref="IRoutingFeature"/> instance.</param>
        /// <returns>A readable string form of the page name, if available; otherwise, <c>null</c>.</returns>
        public static string? GetPageName(this IRoutingFeature? routing)
        {
            if (routing == null) return null;

            var attributeRouteHandler = routing.RouteData.Routers.OfType<MvcAttributeRouteHandler>().FirstOrDefault();
            if (attributeRouteHandler?.Actions.FirstOrDefault() is ControllerActionDescriptor action)
            {
                return $"{action.ControllerName}.{action.ActionName}";
            }
            return attributeRouteHandler?.Actions.FirstOrDefault()?.DisplayName;

        }
    }
}
#endif
