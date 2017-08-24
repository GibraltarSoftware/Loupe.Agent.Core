using System;
using System.Net;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// The base class for all exceptions thrown by the Web Channel
    /// </summary>
    public class WebChannelException : GibraltarException
    {
        /// <summary>
        /// Create a new web channel exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public WebChannelException(string message, Exception innerException, Uri requestUri)
            : base(message, innerException)
        {
            RequestUri = requestUri;
        }

        /// <summary>
        /// Create a new web channel exception
        /// </summary>
        public WebChannelException(string message)
            : base(message)
        {

        }

        /// <summary>
        /// The inner exception as a web exception.  May be null.
        /// </summary>
        public WebException WebException
        {
            get { return InnerException as WebException; }
        }

        /// <summary>
        /// the url that was requested.
        /// </summary>
        public Uri RequestUri { get; private set; }
    }
}
