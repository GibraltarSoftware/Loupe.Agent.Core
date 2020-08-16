using System;
using System.Net;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Gibraltar.Server.Client
{
    public class WebChannelMethodNotAllowedException : WebChannelException
    {
        public WebChannelMethodNotAllowedException(string message, Exception innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
        }

        public WebChannelMethodNotAllowedException(string message)
            : base(message)
        {
        }
    }
}
