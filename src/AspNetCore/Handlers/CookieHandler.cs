using System;
using System.Linq;
using Loupe.Agent.AspNetCore.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Handlers
{
    internal static class CookieHandler
    {
        /// <summary>
        /// Get the session Id from request cookies, if present.
        /// </summary>
        public static string GetSessionId(HttpContext context, CookieOptions cookieOptions)
        {
            string sessionId = null;
            
            // Check the loupesessionid header
            if (context.Request.Headers.TryGetValue(Constants.SessionIdHeaderName, out var sessionIdHeader))
            {
                if (sessionIdHeader.Count > 0)
                {
                    sessionId = sessionIdHeader[0];
                }
            }

            // If not in the header, try the cookie
            if (sessionId is null)
            {
                if (!context.Request.Cookies.TryGetValue(Constants.SessionIdCookie, out sessionId))
                {
                    sessionId = NewSessionId(context, cookieOptions);
                }
            }
            else
            {
                // Make sure the cookie is set
                SetSessionIdCookie(context, sessionId, cookieOptions);
            }
            
            context.SetSessionId(sessionId);

            return sessionId;
        }

        private static string NewSessionId(HttpContext context, CookieOptions options)
        {
            var sessionId = Guid.NewGuid().ToString();

            context.Response.Headers.Add(Constants.SessionIdHeaderName, sessionId);
            context.Response.Cookies.Append(Constants.SessionIdCookie, sessionId, options);
            
            return sessionId;
        }

        private static void SetSessionIdCookie(HttpContext context, string sessionId, CookieOptions options)
        {
            context.Response.Cookies.Append(Constants.SessionIdCookie, sessionId, options);
        }
    }
}