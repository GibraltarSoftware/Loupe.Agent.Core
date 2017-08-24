using System;
using System.Net;

namespace Gibraltar.Server.Client
{
    public class WebChannelMethodNotAllowedException : WebChannelException
    {
        public WebChannelMethodNotAllowedException(string message, WebException innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
        }

        public WebChannelMethodNotAllowedException(string message)
            : base(message)
        {
        }
    }
}
