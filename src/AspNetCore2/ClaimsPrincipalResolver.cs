using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using Gibraltar.Monitor;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Loupe Principal Resolver for ASP.NET Core
    /// </summary>
    /// <remarks>Finds the current principal for any activity that's part of an
    /// active Http request.</remarks>
    public class ClaimsPrincipalResolver : IPrincipalResolver
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IPrincipalResolver _defaultPrincipalResolver;

        public ClaimsPrincipalResolver(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _defaultPrincipalResolver = new DefaultPrincipalResolver();
        }

        /// <inheritdoc />
        public IPrincipal ResolveCurrentPrincipal()
        {
            var principal = _contextAccessor.HttpContext?.User;

            //If we got a principal, great - otherwise ask the default resolver to give us the default.
            return principal ?? _defaultPrincipalResolver.ResolveCurrentPrincipal();
        }
    }
}
