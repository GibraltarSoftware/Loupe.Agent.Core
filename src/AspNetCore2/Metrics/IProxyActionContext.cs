using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// An interface that will be implemented dynamically by Microsoft.Extensions.Diagnostics.
    /// </summary>
    public interface IProxyActionContext
    {
        /// <summary>
        /// Gets the HTTP context.
        /// </summary>
        /// <value>
        /// The HTTP context.
        /// </value>
        HttpContext HttpContext { get; }
    }
}