using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Gibraltar.Server.Client.Data;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Uploads the state of a client repository, adding it if necessary.
    /// </summary>
    public class ClientRepositoryUploadRequest : WebChannelRequestBase
    {
        /// <summary>
        /// Create a new sessions version request
        /// </summary>
        public ClientRepositoryUploadRequest(ClientRepositoryXml repositoryXml)
            : base(true, true)
        {
            InputRepository = repositoryXml;
        }

        #region Public Properties and Methods

        /// <summary>
        /// The repository data to commit to the server
        /// </summary>
        public ClientRepositoryXml InputRepository { get; private set; }

        /// <summary>
        /// The repository data returned by the server as a result of the request.
        /// </summary>
        public ClientRepositoryXml ResponseRepository { get; private set; }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Implemented by inheritors to perform the request on the provided web client.
        /// </summary>
        /// <param name="connection"></param>
        protected override async Task OnProcessRequest(IWebChannelConnection connection)
        {
            byte[] requestedRepositoryRawData = await connection.UploadData(GenerateResourceUri(), HttpMethod.Put, XmlContentType, ConvertXmlToByteArray(InputRepository)).ConfigureAwait(false);

            //now we deserialize the response which is the new state of the document.

            //now, this is supposed to be a sessions list...
            using (var inputStream = new MemoryStream(requestedRepositoryRawData))
            {
                XmlSerializerNamespaces xmlNsEmpty = new XmlSerializerNamespaces();
                xmlNsEmpty.Add("", "http://www.gibraltarsoftware.com/Gibraltar/Repository.xsd"); //gets rid of the default namespaces we'd otherwise generate

                var xmlReader = XmlReader.Create(inputStream);
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(ClientRepositoryXml), "http://www.gibraltarsoftware.com/Gibraltar/Repository.xsd");

                ClientRepositoryXml repositoryXml = (ClientRepositoryXml)xmlSerializer.Deserialize(xmlReader);
                ResponseRepository = repositoryXml;
            }

        }

        #endregion

        #region Private Properties and Methods

        private string GenerateResourceUri()
        {
            Guid repositoryId = new Guid(InputRepository.id); //to make sure we have a valid GUID
            return string.Format("/Hub/Repositories/{0}/Repository.xml", repositoryId);
        }

        #endregion
    }
}
