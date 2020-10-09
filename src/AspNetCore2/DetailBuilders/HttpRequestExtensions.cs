using System.Net;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.DetailBuilders
{
    public static class HttpRequestExtensions
    {
        public static bool IsLocal(this HttpRequest req)
        {
            var connection = req.HttpContext.Connection;
            if (connection.RemoteIpAddress != null)
            {
                return connection.LocalIpAddress != null
                    ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress)
                    : IPAddress.IsLoopback(connection.RemoteIpAddress);
            }

            // for in memory TestServer or when dealing with default connection info
            return connection.RemoteIpAddress == null && connection.LocalIpAddress == null;
        }
    }
}