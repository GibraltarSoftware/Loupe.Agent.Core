using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Loupe.Core.Server.Client
{
    /// <summary>
    /// Authentication credentials for a repository to a shared data service.
    /// </summary>
    public sealed class RepositoryCredentials : IWebAuthenticationProvider
    {
        /// <summary>
        /// The prefix for the authorization header for this credential type
        /// </summary>
        public const string AuthorizationPrefix = "Gibraltar-Repository";

        /// <summary>
        /// The HTTP Request header identifying the client repository
        /// </summary>
        public const string ClientRepositoryHeader = "X-Gibraltar-Repository";

        internal const string AuthorizationHeader = "Authorization";

        private readonly object m_Lock = new object();

        private string m_AccessToken; //PROTECTED BY LOCK

        /// <summary>
        /// Create a new set of repository credentials
        /// </summary>
        /// <param name="repositoryId">The owner Id to specify to the server (for example repository Id)</param>
        /// <param name="keyContainerName">The name of the key container to retrieve the private key from</param>
        /// <param name="useMachineStore">True to use the machine store instead of the user store for the digital certificate</param>
        public RepositoryCredentials(Guid repositoryId, string keyContainerName, bool useMachineStore)
        {
            if (repositoryId == Guid.Empty)
                throw new ArgumentOutOfRangeException(nameof(repositoryId), "The supplied repository Id is an empty guid, which can't be right.");

            RepositoryId = repositoryId;
            KeyContainerName = keyContainerName;
            UseMachineStore = useMachineStore;
        }

        #region Public Properties and Methods

        /// <summary>
        /// True to use the machine store instead of the user store for the digital certificate
        /// </summary>
        public bool UseMachineStore { get; private set; }

        /// <summary>
        /// The name of the key container to retrieve the private key from
        /// </summary>
        public string KeyContainerName { get; private set; }

        /// <summary>
        /// The owner Id to specify to the server (for example repository Id)
        /// </summary>
        public Guid RepositoryId { get; private set; }

        #endregion

        #region IWebAuthenticaitonProvider implementation

        /// <summary>
        /// Indicates if the authentication provider believes it has authenticated with the channel
        /// </summary>
        /// <remarks>If false then no logout will be attempted, and any request that requires authentication will
        /// cause a login attempt without waiting for an authentication failure.</remarks>
        public bool IsAuthenticated
        {
            get
            {
                bool isAuthenticated = false;

                //we have to always use a lock when handling the access token.
                lock (m_Lock)
                {
                    isAuthenticated = (string.IsNullOrEmpty(m_AccessToken) == false);
                    System.Threading.Monitor.PulseAll(m_Lock);
                }

                return isAuthenticated;
            }
        }

        /// <summary>
        /// indicates if the authentication provider can perform a logout
        /// </summary>
        bool IWebAuthenticationProvider.LogoutIsSupported { get { return false; } }

        /// <summary>
        /// Perform a login on the supplied channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="client"></param>
        async Task IWebAuthenticationProvider.Login(WebChannel channel, HttpClient client)
        {
            //we need to get the access token for our repository id
            string requestUrl = string.Format("Repositories/{0}/AccessToken.bin", RepositoryId);

            var accessToken = await client.GetStringAsync(requestUrl).ConfigureAwait(false);

            lock(m_Lock)
            {
                //and here we WOULD decrypt the access token if it was encrypted
                m_AccessToken = accessToken;
                System.Threading.Monitor.PulseAll(m_Lock);
            }
        }

        /// <summary>
        /// Perform a logout on the supplied channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="client"></param>
#pragma warning disable 1998
        async Task IWebAuthenticationProvider.Logout(WebChannel channel, HttpClient client)
#pragma warning restore 1998
        {
            //we have to always use a lock when handling the access token.
            lock (m_Lock)
            {
                m_AccessToken = null;
                System.Threading.Monitor.PulseAll(m_Lock);
            }
        }

        /// <summary>
        /// Perform per-request authentication processing.
        /// </summary>
        /// <param name="channel">The channel object</param>
        /// <param name="client">The web client that is about to be used to execute the request.  It can't be used by the authentication provider to make requests.</param>
        /// <param name="request">The request that is about to be sent</param>
        /// <param name="resourceUrl">The resource URL (with query string) specified by the client.</param>
        /// <param name="requestSupportsAuthentication">Indicates if the request being processed supports authentication or not.</param>
        /// <remarks>If the request doesn't support authentication, it's a best practice to not provide any authentication information.</remarks>
        void IWebAuthenticationProvider.PreProcessRequest(WebChannel channel, HttpClient client, HttpRequestMessage request, string resourceUrl, bool requestSupportsAuthentication)
        {
            //figure out the effective relative URL.
            string fullUrl = resourceUrl;
            if (client.BaseAddress != null)
            {
                fullUrl = client.BaseAddress + resourceUrl;
            }

            var clientUri = new Uri(fullUrl);

            //we're doing sets not adds to make sure we overwrite any existing value.
            if (requestSupportsAuthentication)
            {
                request.Headers.TryAddWithoutValidation(AuthorizationHeader, AuthorizationPrefix + ": " + CalculateHash(clientUri.PathAndQuery));
                request.Headers.Add(ClientRepositoryHeader, RepositoryId.ToString());
            }
            else
            {
                //remove our repository header.
                request.Headers.Remove(ClientRepositoryHeader);
            }
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Calculates the effective hash given the provided salt text.
        /// </summary>
        /// <param name="saltText"></param>
        /// <returns></returns>
        private string CalculateHash(string saltText)
        {
            UTF8Encoding encoder = new UTF8Encoding();
            byte[] buffer;

            //we have to always use a lock when handling the access token.
            lock (m_Lock)
            {
                buffer = encoder.GetBytes(m_AccessToken + saltText);
                System.Threading.Monitor.PulseAll(m_Lock);
            }

            using (var csp = SHA1.Create())
            {
                return Convert.ToBase64String(csp.ComputeHash(buffer));
            }
        }

        #endregion
    }
}
