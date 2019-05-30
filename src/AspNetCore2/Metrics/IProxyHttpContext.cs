using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// An interface that will be implemented dynamically by Microsoft.Extensions.Diagnostics.
    /// </summary>
    public interface IProxyHttpContext
    {
        /// <summary>
        /// Gets the trace identifier.
        /// </summary>
        /// <value>
        /// The trace identifier.
        /// </value>
        string TraceIdentifier { get; }
        /// <summary>
        /// Gets the request.
        /// </summary>
        /// <value>
        /// The request.
        /// </value>
        HttpRequest Request { get; }
        /// <summary>
        /// Gets the response.
        /// </summary>
        /// <value>
        /// The response.
        /// </value>
        HttpResponse Response { get; }
        /// <summary>
        /// Gets the features.
        /// </summary>
        /// <value>
        /// The features.
        /// </value>
        IFeatureCollection Features { get; }
    }
}