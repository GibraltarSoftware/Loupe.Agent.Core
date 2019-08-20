using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Loupe.Agent.Net
{
    /// <summary>
    /// Basic Authentication credentials for authenticating with the server
    /// </summary>
    public sealed class BasicAuthenticationProvider : IServerAuthenticationProvider
    {
        /// <summary>
        /// Create a new instance of the HTTP Basic Authentication Provider with the specified username and password
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        public BasicAuthenticationProvider(string userName, string password)
        {
            UserName = userName;
            Password = password;
        }

        /// <summary>
        /// The user name to use for basic authentication
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// The password to use for basic authentication
        /// </summary>
        public string Password { get; set; }

        /// <inheritdoc />
        public bool IsAuthenticated
        {
            get
            {
                //we don't need to pre-authenticate to get a token so we say yes.
                return true;
            }
        }

        /// <inheritdoc />
        public bool LogoutIsSupported
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
#pragma warning disable 1998
        public async Task Login(Uri entryUri, HttpClient client)
#pragma warning restore 1998
        {
            //nothing to do
        }

        /// <inheritdoc />
#pragma warning disable 1998
        public async Task Logout(Uri entryUri, HttpClient client)
#pragma warning restore 1998
        {
            //nothing to do
        }

        /// <inheritdoc />
        public void PreProcessRequest(Uri entryUri, HttpClient client, string resourceUrl, bool requestSupportsAuthentication)
        {
            var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{UserName}:{Password}")));
            client.DefaultRequestHeaders.Authorization = authHeader;
        }
    }
}
