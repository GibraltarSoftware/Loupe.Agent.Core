using System;
using System.Security.Claims;
using System.Security.Principal;
using Loupe.Monitor;

namespace Loupe.Agent.AspNetCore
{
    /// <summary>
    /// Translate the basic ClaimsPrincipal into an ApplicationUser.
    /// </summary>
    public class ClaimsPrincipalApplicationUserProvider : IApplicationUserProvider
    {
        /// <inheritdoc />
        public bool TryGetApplicationUser(IPrincipal principal, Lazy<ApplicationUser> applicationUser)
        {
            if (principal is ClaimsPrincipal claimsPrincipal)
            {
                //Materialize the user - we don't have anything more we can do without an application-specific implementation.
                var user = applicationUser.Value;

                return true;
            }

            return false;
        }
    }
}
