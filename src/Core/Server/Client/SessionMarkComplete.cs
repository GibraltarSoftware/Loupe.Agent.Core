using System;
using System.Net.Http;
using System.Threading.Tasks;
using Loupe.Server.Client.Data;

namespace Loupe.Server.Client
{
    /// <summary>
    /// Informs the server that the session is complete (assuming it is a protocol 1.2 or higher server)
    /// </summary>
    internal class SessionMarkComplete : WebChannelRequestBase
    {
        /// <summary>
        /// Create a new session header upload request.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="clientId"></param>
        public SessionMarkComplete(Guid sessionId, Guid clientId)
            : base(true, false)
        {
            SessionId = sessionId;
            ClientId = clientId;
        }

        /// <summary>
        /// The unique Id of this client
        /// </summary>
        public Guid ClientId { get; private set; }

        /// <summary>
        /// The unique Id of the session that is complete
        /// </summary>
        public Guid SessionId { get; private set; }

        /// <summary>
        /// Implemented by inheritors to perform the request on the provided web client.
        /// </summary>
        /// <param name="connection"></param>
        protected override async Task OnProcessRequest(IWebChannelConnection connection)
        {
            string strRequestUrl = string.Format("/Hub/Hosts/{0}/Sessions/{1}/session.xml", ClientId, SessionId);

            SessionXml sessionHeaderXml = new SessionXml();
            sessionHeaderXml.id = SessionId.ToString();
            sessionHeaderXml.isComplete = true;
            sessionHeaderXml.isCompleteSpecified = true;

            //we can't encode using XmlSerializer because it will only work with public types, and we 
            //aren't public if we get ILMerged into something.
            byte[] encodedXml = DataConverter.SessionXmlToByteArray(sessionHeaderXml);

            await connection.UploadData(strRequestUrl, HttpMethod.Post, "text/xml", encodedXml).ConfigureAwait(false);
        }
    }
}
