using System;
using System.Security.Principal;

namespace Loupe.Core.Monitor
{
    /// <summary>
    /// Implemented to translate an <see cref="IPrincipal">IPrincipal</see> to a Loupe <see cref="ApplicationUser">ApplicationUser</see>.
    /// </summary>
    /// <remarks><para>Loupe stores extended information about each application user in an <see cref="ApplicationUser">ApplicationUser</see>
    /// object.  This application user is associated with a single IPrincipal based on the name of that principal (from the Identity).
    /// Each time a data packet is recorded that has an IPrincipal specified the name is checked.  If no ApplicationUser has been
    /// provided then the TryGetApplicationUser method is invoked.  Once a valid application user is provided it is cached for
    /// the remainder of the session and won't be requested again.</para>
    /// <para>If the application user can't be determined for any reason the provider should return false.  The
    /// provider can query databases and other information if necessary to get a complete set of information for
    /// the application user.  Any messages logged by the provider directly or indirectly will be non-blocking,
    /// possibly being dropped if necessary, to ensure the Loupe agent doesn't deadlock.</para>
    /// <para>If the application is running in anonymous mode the ApplicationUserProvider won't be invoked.</para></remarks>
    public interface IApplicationUserProvider
    {
        /// <summary>
        /// Determine the application user for the provided principal
        /// </summary>
        /// <param name="principal">The Principal being resolved</param>
        /// <param name="applicationUser">The application user if it could be provided</param>
        /// <remarks>Application Users are lazily initialized when the Value property is invoked, but the ApplicationUser
        /// object will be ignored if the method returns false.</remarks>
        /// <returns>True if the application user could be provided, false otherwise.</returns>
        bool TryGetApplicationUser(IPrincipal principal, Lazy<ApplicationUser> applicationUser);
    }
}
