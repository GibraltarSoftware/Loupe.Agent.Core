using System;
using System.Security.Principal;
using Loupe.Monitor;

namespace Loupe.Agent
{
    /// <summary>
    /// Resolve the current IPrincipal using a delegate
    /// </summary>
    /// <remarks>This resolver takes a simple function to resolve the IPrincipal, making it easy to
    /// use a lambda or local function.</remarks>
    public class DelegatePrincipalResolver : IPrincipalResolver
    {
        private readonly Func<IPrincipal> _resolver;

        /// <summary>
        /// Create a resolver with the provided function.
        /// </summary>
        /// <param name="resolver"></param>
        public DelegatePrincipalResolver(Func<IPrincipal> resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            _resolver = resolver;
        }

        /// <inheritdoc />
        public bool TryResolveCurrentPrincipal(out IPrincipal principal)
        {
            try
            {
                principal = _resolver();
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                principal = null;
                return false;
            }

            return (principal != null);
        }
    }
}
