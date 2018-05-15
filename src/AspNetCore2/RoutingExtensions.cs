using System.Linq;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Routing;

namespace Loupe.Agent.AspNetCore
{
    public static class RoutingExtensions
    {
        public static string GetRouteString(this IRoutingFeature routing)
        {
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
    }
}