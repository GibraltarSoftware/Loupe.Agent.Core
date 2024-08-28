using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// An abstract implementation of a web request that simplifies making new requests.
    /// </summary>
    public abstract class WebChannelRequestBase : IWebRequest
    {
        /// <summary>
        /// The standard content type for GZip'd data.
        /// </summary>
        public const string GZipContentType = "application/gzip";

        /// <summary>
        /// The standard content type for raw binary data.
        /// </summary>
        public const string BinaryContentType = "application/octet-stream";

        /// <summary>
        /// The standard content type for a zip file
        /// </summary>
        public const string ZipContentType = "application/zipfile";

        /// <summary>
        /// The standard content type for XML data
        /// </summary>
        protected const string XmlContentType = "text/xml";

        /// <summary>
        /// The standard content type for text data
        /// </summary>
        protected const string TextContentType = "text/text";

        private readonly object m_StatusLock = new object();
        private readonly bool m_SupportsAuthentication;
        private readonly bool m_RequiresAuthentication;

        /// <summary>
        /// Create a new web channel request
        /// </summary>
        /// <param name="supportsAuthentication"></param>
        /// <param name="requiresAuthentication"></param>
        protected WebChannelRequestBase(bool supportsAuthentication, bool requiresAuthentication)
        {
            m_SupportsAuthentication = supportsAuthentication;
            m_RequiresAuthentication = requiresAuthentication;
        }

        /// <summary>
        /// Indicates if the web request requires authentication (so the channel should authenticate before attempting the request)
        /// </summary>
        public bool RequiresAuthentication
        {
            get { return m_RequiresAuthentication; }
        }

        /// <summary>
        /// Indicates if the web request supports authentication, so if the server requests credentials the request can provide them.
        /// </summary>
        public bool SupportsAuthentication
        {
            get { return m_SupportsAuthentication; }
        }

        /// <summary>
        /// Perform the request against the specified web client connection.
        /// </summary>
        /// <param name="connection"></param>
        public async Task ProcessRequest(IWebChannelConnection connection)
        {
            await OnProcessRequest(connection).ConfigureAwait(false);
        }

        #region Protected Properties and Methods

        /// <summary>
        /// Implemented by inheritors to perform the request on the provided web client.
        /// </summary>
        /// <param name="connection"></param>
        protected abstract Task OnProcessRequest(IWebChannelConnection connection);

        /// <summary>
        /// Convert the provided XML fragment to a byte array of UTF8 data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="xmlFragment"></param>
        /// <returns></returns>
        protected static byte[] ConvertXmlToByteArray<T>(T xmlFragment)
        {
            //we want to get a byte array
            using (MemoryStream outputStream = new MemoryStream(2048))
            {
                using (TextWriter textWriter = new StreamWriter(outputStream, Encoding.UTF8))
                {
                    XmlTextWriter xmlWriter = new XmlTextWriter(textWriter);
                    // Write XML using xmlWriter
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
                    XmlSerializerNamespaces xmlNsEmpty = new XmlSerializerNamespaces();
                    xmlSerializer.Serialize(xmlWriter, xmlFragment, xmlNsEmpty);
                    xmlWriter.Flush(); // to make sure it writes it all out now.
                }

                return outputStream.ToArray();
            }
        }

        #endregion
    }
}
