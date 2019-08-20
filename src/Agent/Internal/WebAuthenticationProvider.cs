using System.Net.Http;
using System.Threading.Tasks;
using Loupe.Core.Server.Client;

namespace Loupe.Agent.Internal
{
    internal class WebAuthenticationProvider : IWebAuthenticationProvider
    {
        private readonly Net.IServerAuthenticationProvider m_WrappedProvider;

        public WebAuthenticationProvider(Net.IServerAuthenticationProvider wrappedProvider)
        {
            m_WrappedProvider = wrappedProvider;
        }

        public bool IsAuthenticated { get { return m_WrappedProvider.IsAuthenticated; } }

        public bool LogoutIsSupported { get { return m_WrappedProvider.LogoutIsSupported; } }

        public async Task Login(WebChannel channel, HttpClient client)
        {
            await m_WrappedProvider.Login(client.BaseAddress, client).ConfigureAwait(false);
        }

        public async Task Logout(WebChannel channel, HttpClient client)
        {
            if (m_WrappedProvider.LogoutIsSupported)
            {
                await m_WrappedProvider.Logout(client.BaseAddress, client).ConfigureAwait(false);
            }
        }

        public void PreProcessRequest(WebChannel channel, HttpClient client, HttpRequestMessage request, string resourceUrl, bool requestSupportsAuthentication)
        {
            m_WrappedProvider.PreProcessRequest(client.BaseAddress, client, resourceUrl, requestSupportsAuthentication);
        }
    }
}
