using System;
using System.Security.Principal;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Implemented to translate an <see cref="IPrincipal">IPrincipal</see> to a Loupe <see cref="ApplicationUser">ApplicationUser</see>.
    /// </summary>
    public interface IApplicationUserProvider
    {
        /// <summary>
        /// Determine the application user for the provided principal
        /// </summary>
        /// <param name="principal">The Principal being resolved</param>
        /// <param name="userFactory">Function for creating a new user</param>
        /// <param name="applicationUser">Optional.  The application user if it could be provided</param>
        /// <returns>True if the application user could be provided, false otherwise.</returns>
        bool TryGetApplicationUser(IPrincipal principal, Func<ApplicationUser> userFactory, out ApplicationUser applicationUser);
    }
}
