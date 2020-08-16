using System;
using System.Net;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Thrown by the web channel when it is unable to authenticate to the remote server.
    /// </summary>
    public class WebChannelAuthorizationException : WebChannelException
    {
        /// <summary>
        /// Create a new authorization exception
        /// </summary>
        public WebChannelAuthorizationException(string message, Exception innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
            
        }

        /// <summary>
        /// Create a new authorization exception
        /// </summary>
        public WebChannelAuthorizationException(string message)
            : base(message)
        {
            
        }
    }
}
