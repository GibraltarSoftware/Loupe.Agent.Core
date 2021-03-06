﻿using System;
using System.Net;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Gibraltar.Server.Client
{
    public class WebChannelExpectationFailedException : WebChannelException
    {
        public WebChannelExpectationFailedException(string message, Exception innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
        }

        public WebChannelExpectationFailedException(string message)
            : base(message)
        {
        }
    }
}
