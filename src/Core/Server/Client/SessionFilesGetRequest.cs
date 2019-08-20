using System;
using System.Threading.Tasks;
using Loupe.Server.Client.Data;

namespace Loupe.Server.Client
{
    /// <summary>
    /// Get the list of session fragment files for a session
    /// </summary>
    internal class SessionFilesGetRequest : WebChannelRequestBase
    {
        /// <summary>
        /// Create a new session headers request
        /// </summary>
        public SessionFilesGetRequest(Guid sessionId)
            : base(true, true)
        {
            SessionId = sessionId;
        }

        /// <summary>
        /// create a new request for the specified client and session.
        /// </summary>
        public SessionFilesGetRequest(Guid clientId, Guid sessionId)
            :base(true, false)
        {
            ClientId = clientId;
            SessionId = sessionId;
        }

        /// <summary>
        /// The unique Id of this client when being used from an Agent
        /// </summary>
        public Guid? ClientId { get; private set; }

        /// <summary>
        /// The unique Id of the session we want to get the existing files for
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// The list of session files on the server
        /// </summary>
        public SessionFilesListXml Files { get; private set; }

        /// <summary>
        /// Implemented by inheritors to perform the request on the provided web client.
        /// </summary>
        /// <param name="connection"></param>
        protected override async Task OnProcessRequest(IWebChannelConnection connection)
        {
            string url;
            if (ClientId.HasValue)
            {
                url = string.Format("/Hub/Hosts/{0}/{1}", ClientId, GenerateResourceUri());
            }
            else
            {
                url = string.Format("/Hub/{0}", GenerateResourceUri());
            }

            byte[] sessionFilesListRawData = await connection.DownloadData(url).ConfigureAwait(false);

            //even though it's a session list we can't actually deserialize it directly - because we cant use XmlSerializer
            //since the types will not necessarily be public.
            Files = DataConverter.ByteArrayToSessionFilesListXml(sessionFilesListRawData);
        }

        private string GenerateResourceUri()
        {
            return string.Format("Sessions/{0}/Files.xml", SessionId);
        }
    }
}
