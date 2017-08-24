using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Loupe.Extensibility.Data;

namespace Gibraltar.Server.Client.Internal
{
    public class HttpClientLogger : DelegatingHandler
    {
        private readonly IClientLogger m_Logger;
        private const string LogSystem = "Loupe";
        private const string LogCategory = "Data Access.Web";

        public HttpClientLogger(IClientLogger logger, HttpClientHandler innerHandler)
            :base(innerHandler)
        {
            m_Logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                m_Logger.Write(LogMessageSeverity.Verbose, LogCategory, request.Method.Method + " " + request.RequestUri.AbsolutePath,
                    "Full URL: {0}\r\nServer: {1}\r\nParameters: {2}", request.RequestUri.ToString(),
                    request.RequestUri.DnsSafeHost, request.RequestUri.Query);

                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        m_Logger.Write(LogMessageSeverity.Information, LogCategory, request.Method.Method + " " + request.RequestUri.AbsolutePath + " " + response.ReasonPhrase,
                                       "Full URL: {0}\r\nServer: {1}\r\n", request.RequestUri.ToString(),
                                       request.RequestUri.DnsSafeHost);
                    }
                    else
                    {
                        m_Logger.Write(LogMessageSeverity.Warning, LogCategory, request.Method.Method + " " + request.RequestUri.AbsolutePath + " failed due to " + response.ReasonPhrase,
                                       "Full URL: {0}\r\nServer: {1}\r\n", request.RequestUri.ToString(),
                                       request.RequestUri.DnsSafeHost);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                m_Logger.Write(LogMessageSeverity.Warning, ex, true, LogCategory, "Unable to complete web request due to "+ ex.GetType(), ex.Message);
                throw;
            }
        }
    }
}
