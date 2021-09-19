using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loupe.Agent.AspNetCore.Handlers;
using Loupe.Agent.AspNetCore.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Create and read a cookie for each request to track sessions.
    /// </summary>
    /// <remarks>If the <see cref="ILoggerFactory">Microsoft.Extensions.Logging.ILoggerFactory</see> is available
    /// the LoupeSessionId and LoupeAgentSessionId for the current request will be added to the logging scope for all
    /// messages logged during the request</remarks>
    public class LoupeCookieMiddleware
    {
        private readonly ILoggerFactory? _loggerFactory;
        private readonly RequestDelegate _next;

        /// <summary>
        /// Constructs and instance of <see cref="LoupeCookieMiddleware"/>.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="loggerFactory"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public LoupeCookieMiddleware(RequestDelegate next, ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>
        /// The automagically-called method that processes the request.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> instance for the current request.</param>
        /// <returns>A <see cref="Task"/> that completes when the middleware has processed the request.</returns>
        public async Task Invoke(HttpContext context)
        {
            IDisposable? localScope = null;
            try
            {
                if (context.Request.IsInteresting())
                {
                    var sessionId = CookieHandler.GetSessionId(context);
                    var agentSessionId = HeaderHandler.GetAgentSessionId(context);

                    //only grab session ids if we could possibly log them...
                    if (_loggerFactory != null)
                    {
                        IList<KeyValuePair<string, object>>? requestProperties = null;

                        if (!string.IsNullOrEmpty(sessionId))
                        {
                            requestProperties ??= new List<KeyValuePair<string, object>>();
                            requestProperties.Add(new KeyValuePair<string, object>(Constants.SessionId, sessionId));
                        }

                        if (!string.IsNullOrEmpty(agentSessionId))
                        {
                            requestProperties ??= new List<KeyValuePair<string, object>>();
                            requestProperties.Add(
                                new KeyValuePair<string, object>(Constants.AgentSessionId, agentSessionId));
                        }

                        if (requestProperties != null)
                        {
                            var logger = _loggerFactory!.CreateLogger(Constants.Category);
                            localScope = logger.BeginScope(requestProperties);
                        }
                    }
                }

                await _next(context);
            }
            finally
            {
                localScope?.Dispose();
            }
        }
    }
}
