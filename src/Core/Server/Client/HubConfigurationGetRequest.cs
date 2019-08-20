using System.Threading.Tasks;
using Loupe.Server.Client.Data;

namespace Loupe.Server.Client
{
    /// <summary>
    /// Get the current hub configuration information for the hub
    /// </summary>
    /// <remarks>We rely on this being anonymously accessible.  First, for performance reasons and second because it's used as a Ping by the agent.</remarks>
    public class HubConfigurationGetRequest : WebChannelRequestBase
    {
        /// <summary>
        /// Create a new sessions version request
        /// </summary>
        public HubConfigurationGetRequest()
            : base(false, false)
        {
        }

        #region Public Properties and Methods

        /// <summary>
        /// The current hub configuration from the hub.
        /// </summary>
        public HubConfigurationXml Configuration { get; private set; }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Implemented by inheritors to perform the request on the provided web client.
        /// </summary>
        /// <param name="connection"></param>
        protected override async Task OnProcessRequest(IWebChannelConnection connection)
        {
            byte[] requestedHubConfigurationRawData = await connection.DownloadData("/Hub/Configuration.xml").ConfigureAwait(false);

            //and now do it without using XMLSerializer since that doesn't work in the agent.
            Configuration = DataConverter.ByteArrayToHubConfigurationXml(requestedHubConfigurationRawData);
        }

        #endregion
    }
}

