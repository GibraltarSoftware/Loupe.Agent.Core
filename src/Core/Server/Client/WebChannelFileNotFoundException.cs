using System;
using System.Net;

namespace Loupe.Server.Client
{
    /// <summary>
    /// Thrown by the web channel when the server reports that the file was not found..
    /// </summary>
    public class WebChannelFileNotFoundException : WebChannelException
    {
        /// <summary>
        /// Create a new file not found exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        /// <param name="requestUri"></param>
        public WebChannelFileNotFoundException(string message, WebException innerException, Uri requestUri)
            : base(message, innerException, requestUri)
        {
        }
    }
}
