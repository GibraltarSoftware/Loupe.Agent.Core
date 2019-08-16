using System;
using System.Security.Principal;
using Gibraltar.Monitor;

namespace Gibraltar.Agent
{
    /// <summary>
    /// Wraps a delegate function for mapping an IPrincipal to an ApplicationUser
    /// </summary>
    /// <remarks>To support simple scenarios such as lambda functions, the DelegateApplicationUserProvider
    /// takes a simple function and uses it to resolve the application user.</remarks>
    public class DelegateApplicationUserProvider : IApplicationUserProvider
    {
        private readonly Func<IPrincipal, Lazy<ApplicationUser>, bool> _func;

        /// <summary>
        /// Create a new application user provider for the specified function
        /// </summary>
        /// <param name="func">THe function to delegate user providing to</param>
        public DelegateApplicationUserProvider(Func<IPrincipal, Lazy<ApplicationUser>, bool> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            _func = func;
        }

        /// <inheritdoc />
        public bool TryGetApplicationUser(IPrincipal principal, Lazy<ApplicationUser> applicationUser)
        {
            try
            {
                return _func(principal, applicationUser);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                return false;
            }
        }
    }
}
