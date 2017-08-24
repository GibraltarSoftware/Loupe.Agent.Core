using System;
using System.Net;

namespace Gibraltar.Server.Client
{
    public class WebChannelExpectationFailedException : WebChannelException
    {
        public WebChannelExpectationFailedException(string message, WebException innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
        }

        public WebChannelExpectationFailedException(string message)
            : base(message)
        {
        }
    }
}
