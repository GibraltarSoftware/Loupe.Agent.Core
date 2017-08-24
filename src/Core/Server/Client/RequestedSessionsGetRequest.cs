using System;
using System.Threading.Tasks;
using Gibraltar.Server.Client.Data;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Get the requested sessions for a client from the server
    /// </summary>
    internal class RequestedSessionsGetRequest : WebChannelRequestBase
    {
        /// <summary>
        /// create a new request for the specified client.
        /// </summary>
        /// <param name="clientId"></param>
        public RequestedSessionsGetRequest(Guid clientId)
            :base(true, false)
        {
            ClientId = clientId;
        }

        /// <summary>
        /// The unique Id of this client
        /// </summary>
        public Guid ClientId { get; private set; }

        /// <summary>
        /// The list of sessions requested from the server.
        /// </summary>
        public SessionsListXml RequestedSessions { get; private set; }

        /// <summary>
        /// Implemented by inheritors to perform the request on the provided web client.
        /// </summary>
        /// <param name="connection"></param>
        protected override async Task OnProcessRequest(IWebChannelConnection connection)
        {
            byte[] requestedSessionsRawData = await connection.DownloadData(string.Format("/Hub/Hosts/{0}/RequestedSessions.xml", ClientId)).ConfigureAwait(false);

            //even though it's a session list we can't actually deserialize it directly - because we cant use XmlSerializer
            //since the types will not necessarily be public.
            RequestedSessions = DataConverter.ByteArrayToSessionsListXml(requestedSessionsRawData);
        }
    }
}
