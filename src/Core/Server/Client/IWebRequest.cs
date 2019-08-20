using System.Threading.Tasks;

namespace Loupe.Core.Server.Client
{
    /// <summary>
    /// The format of a web request provided to the GibraltarWebClient
    /// </summary>
    public interface IWebRequest
    {
        /// <summary>
        /// Indicates if the web request requires authentication (so the channel should authenticate before attempting the request)
        /// </summary>
        bool RequiresAuthentication { get; }

        /// <summary>
        /// Indicates if the web request supports authentication, so if the server requests credentials the request can provide them.
        /// </summary>
        bool SupportsAuthentication { get; }

        /// <summary>
        /// Perform the request against the specified web client connection.
        /// </summary>
        /// <param name="connection"></param>
        Task ProcessRequest(IWebChannelConnection connection);
    }
}
