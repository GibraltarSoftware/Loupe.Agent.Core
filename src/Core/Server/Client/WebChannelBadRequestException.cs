using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Gibraltar.Server.Client
{
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
