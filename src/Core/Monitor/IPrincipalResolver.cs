using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using Gibraltar.Messaging;
using Gibraltar.Monitor.Serialization;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Resolves the user principle for the current message
    /// </summary>
    public interface IPrincipalResolver
    {
        /// <summary>
        /// Resolve the user principal for the current activity
        /// </summary>
        /// <returns>The IPrincipal that was resolved, if any.</returns>
        IPrincipal ResolveCurrentPrincipal();
    }
}
