using System;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// A Bad Request (400) Exception
    /// </summary>
    public class WebChannelBadRequestException : WebChannelException
    {
        /// <summary>
        /// Create a new Bad Request (400) exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        /// <param name="requestUri"></param>
        public WebChannelBadRequestException(string message, Exception innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
        }
    }
}
