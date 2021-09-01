using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Handlers
{
    internal static class HttpRequestExtensions
    {
        private static readonly HashSet<string> ValidExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".htm",
            ".html",
            string.Empty
        };

        public static bool IsInteresting(this HttpRequest request) =>
            !request.Headers.ContainsKey("Origin")
            && ValidExtensions.Contains(PathExtension(request))
            && !IsBrowserLink(request);
        
        private static bool IsBrowserLink(HttpRequest request) =>
            (request.Path.HasValue && request.Path.Value!.Contains("__browserLink", StringComparison.OrdinalIgnoreCase));
        
        private static string PathExtension(HttpRequest request)
        {
            if (!request.Path.HasValue) return string.Empty;
            
            var pathValue = request.Path.Value;
            var lastSlash = pathValue!.LastIndexOf('/');
            if (lastSlash < 0) lastSlash = 0;
            var lastPeriod = pathValue.LastIndexOf('.', lastSlash);
            
            // If it's longer than 5 characters including the . then don't treat it as a file extension
            return lastPeriod < 0 || pathValue.Length - lastPeriod > 5
                ? string.Empty
                : pathValue.Substring(lastPeriod);
        }
    }
}