using System.Security.Principal;

namespace Loupe.Core.Monitor
{
    /// <summary>
    /// Resolves the user principle for the current thread
    /// </summary>
    /// <remarks>The resolver is invoked every time user-related data is recorded
    /// to associate an <see cref="IPrincipal">IPrincipal</see> with the data.  It is invoked on the thread that
    /// is recording the data so it can reference the current activity context to determine
    /// the IPrincipal, however this means any implementation should be as fast as
    /// feasible and should not make blocking calls - like to a network, file system,
    /// or database.</remarks>
    public interface IPrincipalResolver
    {
        /// <summary>
        /// Resolve the user principal for the current activity
        /// </summary>
        /// <param name="principal">The <see cref="IPrincipal">principal</see> that was resolved, if any.</param>
        /// <returns>True if the <see cref="IPrincipal">IPrincipal</see> was resolved.</returns>
        /// <remarks><para>Implementations should be optimized to not throw
        /// exceptions but instead return false if no principal could be resolved.  Since
        /// this method is called on each logging thread before queuing data it is
        /// important to have a high performance, non-blocking implementation.</para>
        /// <para>To avoid deadlocks and infinite loops, any attempt to log
        /// within this method (directly or indirectly through a dependency) will
        /// be ignored.</para></remarks>
        bool TryResolveCurrentPrincipal(out IPrincipal principal);
    }
}
