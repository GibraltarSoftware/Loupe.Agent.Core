using System.Security.Principal;
using Loupe.Core;
using Loupe.Core.Monitor;
using Loupe.Extensibility;
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

        /// <summary>
        /// Create a new principal resolver
        /// </summary>
        /// <param name="contextAccessor"></param>
        public ClaimsPrincipalResolver(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _defaultPrincipalResolver = new DefaultPrincipalResolver();
        }

        /// <inheritdoc />
        public bool TryResolveCurrentPrincipal(out IPrincipal principal)
        {
            principal = _contextAccessor.HttpContext?.User;

            //If we got a principal, great - otherwise ask the default resolver to give us the default.
            if (principal != null)
            {
                return true;
            }

            return _defaultPrincipalResolver.TryResolveCurrentPrincipal(out principal);
        }
    }
}
