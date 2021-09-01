using System;
using Loupe.Agent.AspNetCore.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Handlers
{
    internal static class CookieHandler
    {
        /// <summary>
        /// Get the session Id from request cookies, if present.
        /// </summary>
        /// <param name="context"></param>
        public static string GetSessionId(HttpContext context)
        {
            if (!context.Request.Cookies.TryGetValue(Constants.SessionId, out var sessionId))
            {
                sessionId = SetSessionCookie(context);
            }

            context.Items[Constants.SessionId] = sessionId;

            return sessionId;
        }

        private static string SetSessionCookie(HttpContext context)
        {
            var sessionId = Guid.NewGuid().ToString();
            context.Response.Cookies.Append(Constants.SessionId, sessionId, new CookieOptions
            {
                HttpOnly = true
            });
            return sessionId;
        }
    }
}