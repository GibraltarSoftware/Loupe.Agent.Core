using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore
{
    public class LoupeMiddleware
    {
        private readonly LoupeAgent _agent;
        private readonly RequestDelegate _next;

        public LoupeMiddleware(RequestDelegate next, LoupeAgent agent)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        public Task Invoke(HttpContext context) => _next(context);
    }
}