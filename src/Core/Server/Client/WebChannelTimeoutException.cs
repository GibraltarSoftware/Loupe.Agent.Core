using System;
#pragma warning disable 1591

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Thrown when a request times out.
    /// </summary>
    public class WebChannelTimeoutException : WebChannelException
    {
        public WebChannelTimeoutException(string message, Exception innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
        }

        public WebChannelTimeoutException(string message)
            : base(message)
        {
        }
    }
}
