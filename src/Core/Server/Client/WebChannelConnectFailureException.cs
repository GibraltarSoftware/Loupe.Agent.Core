using System;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// An exception thrown to indicate a connection failure on the web channel.
    /// </summary>
    public class WebChannelConnectFailureException : WebChannelException
    {
        /// <summary>
        /// Create a new connection failure exception
        /// </summary>
        public WebChannelConnectFailureException(string message, Exception innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
            
        }

        /// <summary>
        /// Create a new connection failure exception
        /// </summary>
        public WebChannelConnectFailureException(string message)
            : base(message)
        {

        }
    }
}
