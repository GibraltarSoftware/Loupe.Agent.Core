using System;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// An interface that will be implemented dynamically by Microsoft.Extensions.Diagnostics.
    /// </summary>
    public interface IProxyActionExecutedContext
    {
        /// <summary>
        /// Gets the exception.
        /// </summary>
        /// <value>
        /// The exception.
        /// </value>
        Exception Exception { get; }
        /// <summary>
        /// Gets the HTTP context.
        /// </summary>
        /// <value>
        /// The HTTP context.
        /// </value>
        HttpContext HttpContext { get; }
    }
    
    /// <summary>
    /// An interface that will be implemented dynamically by Microsoft.Extensions.Diagnostics.
    /// </summary>
    public interface IProxyActionExecutingContext
    {
        /// <summary>
        /// Gets the exception.
        /// </summary>
        /// <value>
        /// The exception.
        /// </value>
        Exception Exception { get; }
        /// <summary>
        /// Gets the HTTP context.
        /// </summary>
        /// <value>
        /// The HTTP context.
        /// </value>
        HttpContext HttpContext { get; }
    }
}