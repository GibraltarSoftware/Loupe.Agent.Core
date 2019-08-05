using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Implemented to translate an IPrincipal to a Loupe ApplicationUser
    /// </summary>
    public interface IApplicationUserResolver
    {
        /// <summary>
        /// Determine the application user for the provided principal
        /// </summary>
        /// <param name="principal">The Principal being resolved</param>
        /// <param name="userFactory">Function for creating a new user</param>
        /// <returns>Null if the user can't be resolved, an ApplicationUser otherwise.</returns>
        ApplicationUser ResolveApplicationUser(IPrincipal principal, Func<ApplicationUser> userFactory);
    }
}
