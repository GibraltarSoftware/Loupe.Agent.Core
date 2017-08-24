using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Gibraltar.Server.Client.Data;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Uploads a session XML document to the endpoint of the web channel
    /// </summary>
    internal class SessionHeaderUploadRequest : WebChannelRequestBase
    {
        /// <summary>
        /// Create a new session header upload request.
        /// </summary>
        /// <param name="sessionHeader"></param>
        /// <param name="clientId"></param>
        public SessionHeaderUploadRequest(SessionXml sessionHeader, Guid clientId)
            :base(true, false)
        {
            ClientId = clientId;
            SessionHeader = sessionHeader;
        }

        /// <summary>
        /// The unique Id of this client
        /// </summary>
        public Guid ClientId { get; private set; }

        /// <summary>
        /// The session header to be uploaded.
        /// </summary>
        public SessionXml SessionHeader { get; private set; }

        /// <summary>
        /// Implemented by inheritors to perform the request on the provided web client.
        /// </summary>
        /// <param name="connection"></param>
        protected override async Task OnProcessRequest(IWebChannelConnection connection)
        {
            string strRequestUrl = string.Format("/Hub/Hosts/{0}/Sessions/{1}/session.xml", ClientId, SessionHeader.id);

            Debug.Assert(SessionHeader.sessionDetail.status != SessionStatusXml.running);
            Debug.Assert(SessionHeader.sessionDetail.status != SessionStatusXml.unknown);

            //we can't encode using XmlSerializer because it will only work with public types, and we 
            //aren't public if we get ILMerged into something.
            byte[] encodedXml = DataConverter.SessionXmlToByteArray(SessionHeader);

            await connection.UploadData(strRequestUrl, HttpMethod.Post, "text/xml", encodedXml).ConfigureAwait(false);
        }
    }
}
