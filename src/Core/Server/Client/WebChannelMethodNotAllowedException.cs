using System;
using System.Net;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Loupe.Server.Client
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
