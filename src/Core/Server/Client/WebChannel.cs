using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gibraltar.Monitor;
using Gibraltar.Server.Client.Internal;
using Loupe.Extensibility.Data;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Provides in-order communication with a remote web server.
    /// </summary>
    [DebuggerDisplay("{HostName} State: {ConnectionState}")]
    public class WebChannel : IWebChannelConnection, IDisposable
    {
        private int m_RetryDelaySeconds = MinimumRetryDelaySeconds;
        private const int MinimumRetryDelaySeconds = 1;
        private const int MaximumRetryDelaySeconds = 120;
        private const int DefaultTimeooutSeconds = 120;
        public const string HeaderRequestMethod = "X-Request-Method";
        public const string HeaderRequestTimestamp = "X-Request-Timestamp";
        public const string HeaderRequestAppProtocolVersion = "X-Request-App-Protocol";

        /// <summary>
        /// The log category for the server client
        /// </summary>
        public const string LogCategory = "Loupe.Server.Client";

        private readonly object m_Lock = new object();
        private static readonly Dictionary<string, bool> s_ServerUseCompatibilitySetting = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> s_ServerUseHttpVersion10Setting = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource m_CancellationTokenSource; //PROTECTED BY LOCK
        private IWebAuthenticationProvider m_AuthenticationProvider; //PROTECTED BY LOCK
        private HttpClient m_Connection; //PROTECTED BY LOCK
        private Uri m_BaseAddress; //PROTECTED BY LOCK
        private volatile ChannelConnectionState m_ConnectionState;

        private readonly IClientLogger m_Logger;
        private readonly bool m_UseSsl;
        private readonly string m_HostName;
        private readonly int m_Port;
        private readonly string m_ApplicationBaseDirectory;
        private bool m_UseCompatibilityMethods;
        private bool m_UseHttpVersion10 = false; //used to force 1.0 instead of 1.1
        private bool m_FirstRequest = true; //used so we can defer checking some optimizations from construction until our first request.
        private bool m_RequestSupportsAuthentication; //crappy way of passing data from ExecuteRequest to PreProcessRequest
        private volatile bool m_Disposed;

        /// <summary>
        /// Raised whenever the connection state changes.
        /// </summary>
        public event ChannelConnectionStateChangedEventHandler ConnectionStateChanged;

        /// <summary>
        /// Create a new web channel to the specified host.
        /// </summary>
        public WebChannel(IClientLogger logger, string hostName)
            : this(logger, false, hostName, null, null)
        {

        }

        /// <summary>
        /// Create a new web channel to the specified host.
        /// </summary>
        public WebChannel(IClientLogger logger, bool useSsl, string hostName, string applicationBaseDirectory, Version appProtocolVersion)
            : this(logger, useSsl, hostName, useSsl ? 443 : 80, applicationBaseDirectory, appProtocolVersion)
        {

        }

        /// <summary>
        /// Create a new web channel to the specified host.
        /// </summary>
        public WebChannel(IClientLogger logger, bool useSsl, string hostName, int port, string applicationBaseDirectory, Version appProtocolVersion)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            if (hostName != null)
                m_HostName = hostName.Trim();

            if (string.IsNullOrEmpty(hostName))
                throw new ArgumentNullException(nameof(hostName), "A server name must be provided for a connection");

            m_Logger = logger;
            m_UseSsl = useSsl;
            m_Port = port;
            m_ApplicationBaseDirectory = applicationBaseDirectory;
            AppProtocolVersion = appProtocolVersion;

            //format up base directory in case we get something we can't use.  It has to have leading & trailing slashes.
            if (string.IsNullOrEmpty(m_ApplicationBaseDirectory) == false)
            {
                m_ApplicationBaseDirectory = m_ApplicationBaseDirectory.Trim();
                if (m_ApplicationBaseDirectory.StartsWith("/") == false)
                {
                    m_ApplicationBaseDirectory = "/" + m_ApplicationBaseDirectory;
                }

                if (m_ApplicationBaseDirectory.EndsWith("/") == false)
                {
                    m_ApplicationBaseDirectory = m_ApplicationBaseDirectory + "/";
                }
            }

            m_CancellationTokenSource = new CancellationTokenSource();
        }

        #region Public Properties and Methods

        /// <summary>
        /// Optional.  The version number to specify in the protocol header.
        /// </summary>
        public Version AppProtocolVersion { get; set; }

        /// <summary>
        /// Indicates if logging for events on the web channel is enabled or not.
        /// </summary>
        public bool EnableLogging { get; set; }

        /// <summary>
        /// The authentication provider to use for any requests that require authentication.
        /// </summary>
        public IWebAuthenticationProvider AuthenticationProvider
        {
            get
            {
                return m_AuthenticationProvider;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AuthenticationProvider = value;

                    System.Threading.Monitor.PulseAll(m_Lock);
                }
            }
        }

        /// <summary>
        /// The DNS name of the server being connected to.
        /// </summary>
        public string HostName { get { return m_HostName; } }

        /// <summary>
        /// The port number being used
        /// </summary>
        public int Port { get { return m_Port; } }

        /// <summary>
        /// Indicates if the channel is encrypted using SSL
        /// </summary>
        public bool UseSsl { get { return m_UseSsl; } }

        /// <summary>
        /// The path from the root of the web server to the start of the application (e.g. the virtual directory path)
        /// </summary>
        public string ApplicationBaseDirectory { get { return m_ApplicationBaseDirectory; } }

        /// <summary>
        /// The complete Uri to the start of all requests that can be executed on this channel.
        /// </summary>
        public string EntryUri
        {
            get
            {
                return CalculateBaseAddress().ToString();
            }
        }

        /// <summary>
        /// The current connection state of the channel.
        /// </summary>
        public ChannelConnectionState ConnectionState { get { return m_ConnectionState; } }

        private void EnsureRequestSuccessful(HttpResponseMessage response)
        {
            Uri requestUri = response.RequestMessage == null ? null : response.RequestMessage.RequestUri;

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    //throw our dedicated file not found exception.
                    throw new WebChannelFileNotFoundException(response.ReasonPhrase, null, requestUri);
                case HttpStatusCode.Unauthorized:
                    //create a new exception that tells our caller it's an authorization problem.
                    throw new WebChannelAuthorizationException(response.ReasonPhrase, null, requestUri);
                case HttpStatusCode.MethodNotAllowed:
                    throw new WebChannelMethodNotAllowedException(response.ReasonPhrase, null, requestUri);
                case HttpStatusCode.ExpectationFailed:
                    throw new WebChannelExpectationFailedException(response.ReasonPhrase, null, requestUri);
            }

            //if it isn't one of our special carve-outs but still not valid, let the web client throw its normal exception.
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Execute the provided request.
        /// </summary>
        /// <param name="newRequest"></param>
        /// <param name="maxRetries">The maximum number of times to retry the request.  Use -1 to retry indefinitely.</param>
        public async Task ExecuteRequest(IWebRequest newRequest, int maxRetries)
        {
            if (newRequest == null)
                throw new ArgumentNullException(nameof(newRequest));

            if ((newRequest.RequiresAuthentication) && (m_AuthenticationProvider == null))
                throw new ArgumentException("The request requires authentication and no authentication provider is available.", nameof(newRequest));

            EnsureConnectionInitialized();

            bool requestComplete = false;
            bool lastCallWasAuthentication = false; //indicates if we just tried an auth, so another 401 means no go.
            int errorCount = 0;

            CancellationToken cancellationToken = GetCommonCancellationToken();

            try
            {
                while ((cancellationToken.IsCancellationRequested == false)
                    && (requestComplete == false)
                    && ((maxRetries < 0) || (errorCount <= maxRetries)))
                {
                    bool reAuthenticate = false;
                    bool delayBeforeRetry = false;
                    try
                    {
                        if (m_ConnectionState == ChannelConnectionState.Disconnected)
                            SetConnectionState(ChannelConnectionState.Connecting);
                        else if (m_ConnectionState == ChannelConnectionState.Connected)
                            SetConnectionState(ChannelConnectionState.TransferingData);

                        if ((newRequest.RequiresAuthentication) && (m_AuthenticationProvider.IsAuthenticated == false))
                        {
                            //no point in waiting for the failure, go ahead and authenticate now.
                            await Authenticate().ConfigureAwait(false);
                        }

                        //Now, because we know we're not MT-safe we can do this "pass around"
                        m_RequestSupportsAuthentication = newRequest.SupportsAuthentication;

                        await newRequest.ProcessRequest(this).ConfigureAwait(false);
                        SetConnectionState(ChannelConnectionState.Connected);
                        requestComplete = true;
                    }
                    catch (WebChannelMethodNotAllowedException ex)
                    {
                        if (m_UseCompatibilityMethods == false)
                        {
                            //most likely we did a delete or put and the caller doesn't support that, enable compatibility methods.
                            m_UseCompatibilityMethods = true;
                            SetUseCompatiblilityMethodsOverride(m_HostName, m_UseCompatibilityMethods);
                                //so we don't have to repeatedly run into this for this server
                            if (EnableLogging)
                                m_Logger.Write(LogMessageSeverity.Information, ex, true, LogCategory,
                                    "Switching to HTTP method compatibility mode",
                                    "Because we got an HTTP 405 error from the server we're going to turn on Http method compatibility translation and try again.  Status Description:\r\n{0}",
                                    ex.Message);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    catch (WebChannelExpectationFailedException ex)
                    {
                        if (m_UseHttpVersion10 == false)
                        {
                            //most likely we are talking to an oddball proxy that doesn't support keepalive (like on a train or plane, seriously..)
                            m_UseHttpVersion10 = true;
                            SetUseHttpVersion10Override(m_HostName, m_UseHttpVersion10);
                                //so we don't have to repeatedly run into this for this server
                            if (EnableLogging)
                                m_Logger.Write(LogMessageSeverity.Information, ex, true, LogCategory,
                                    "Switching to HTTP 1.0 compatibility mode",
                                    "Because we got an HTTP 417 error from the server we're going to turn on Http 1.0 compatibility translation and try again.  Status Description:\r\n{0}",
                                    ex.Message);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    catch (WebChannelAuthorizationException ex)
                    {
                        if ((m_AuthenticationProvider != null) && newRequest.SupportsAuthentication //we can do an auth
                            && (lastCallWasAuthentication == false)) //and we didn't just try to do an auth..
                        {
                            if (EnableLogging)
                                m_Logger.Write(LogMessageSeverity.Information, ex, true, LogCategory,
                                    "Attempting to authenticate to server",
                                    "Because we got an HTTP 401 error from the server we're going to attempt to authenticate with our server credentials and try again.  Status Description:\r\n{0}",
                                    ex.Message);
                            lastCallWasAuthentication = true;
                            reAuthenticate = true;
                        }
                        else
                        {
                            //rethrow to tell our caller it's an authorization problem.
                            SetConnectionState(ChannelConnectionState.Disconnected);
                            throw;
                        }
                    }
                    catch (WebChannelFileNotFoundException)
                    {
                        //this is a real result, we don't want to retry.  But, if we got this then something didn't *expect* the null so keep the exception flying.
                        throw;
                    }
                    catch (HttpRequestException)
                    {
                        //if we didn't carve them out above then we consider these fatal.
                        throw;
                    }
                    catch (Exception ex)
                    {
                        //assume a retryable connection error
                        if (EnableLogging)
                            m_Logger.Write(LogMessageSeverity.Warning, ex, true, LogCategory,
                                "Connection error while making web channel request",
                                "We received a communication exception while executing the current request on our channel.  Since it isn't an authentication exception we're going to retry the request.\r\nRequest: {0}\r\nError Count: {1:N0}\r\nException: {2}",
                                newRequest, errorCount, ex.Message);
                        SetConnectionState(ChannelConnectionState.Disconnected);

                        errorCount++;
                        lastCallWasAuthentication = false;
                            //so if we get another request to authenticate we'll give it a shot.

                        if (CanRetry(maxRetries, errorCount))
                        {
                            if (errorCount > 1)
                            {
                                //back down our rate.
                                delayBeforeRetry = true;
                            }
                        }
                        else
                        {
                            throw new WebChannelException(ex.Message, ex, null);
                        }
                        break;
                    }

                    if (reAuthenticate)
                        await Authenticate().ConfigureAwait(false);

                    if (delayBeforeRetry)
                        await SleepForConnection().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                //this is just here so we can log exceptions that we're letting get thrown.
                if (EnableLogging) m_Logger.Write(LogMessageSeverity.Verbose, ex, true, LogCategory, ex.Message, "While executing a web channel request an exception was thrown, which will be thrown to our caller.\r\nRequest: {0}\r\n", newRequest);
                throw;
            }
        }

        /// <summary>
        /// Authenticate now (instead of waiting for a request to fail)
        /// </summary>
        public async Task Authenticate()
        {
            if (EnableLogging) m_Logger.Write(LogMessageSeverity.Verbose, LogCategory, "Attempting to authenticate communication channel", null);
            IWebAuthenticationProvider authenticationProvider;
            lock (m_Lock)
            {
                authenticationProvider = m_AuthenticationProvider;
                System.Threading.Monitor.PulseAll(m_Lock);
            }

            if (authenticationProvider == null)
            {
                if (EnableLogging) m_Logger.Write(LogMessageSeverity.Verbose, LogCategory, "Unable to authenticate communication channel", "There is no authentication provider available to process the current authentication request.");
                return; //nothing to do.
            }

            EnsureConnectionInitialized();

            await m_AuthenticationProvider.Login(this, m_Connection).ConfigureAwait(false);
        }

        /// <summary>
        /// Cancel the current request.
        /// </summary>
        public void Cancel()
        {
            lock(m_Lock)
            {
                m_CancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Downloads the resource with the specified URI to a byte array
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <param name="timeout">Optional.  The number of seconds to wait for a response to the request.</param>
        /// <returns>A byte array containing the body of the response from the resource</returns>
        public async Task<byte[]> DownloadData(string relativeUrl, int? timeout)
        {
            var request = PreProcessRequest(ref relativeUrl, null);

            var effectiveTimeout = timeout.HasValue ? timeout.Value : DefaultTimeooutSeconds;

            using (var timeoutCts = effectiveTimeout <= 0 ? null : new CancellationTokenSource(effectiveTimeout * 1000))
            {
                using (var linkedCts = timeoutCts == null ? null : CancellationTokenSource.CreateLinkedTokenSource(GetCommonCancellationToken(), timeoutCts.Token))
                {
                    var cancelToken = linkedCts == null ? GetCommonCancellationToken() : linkedCts.Token;
                    using (var response = await m_Connection.SendAsync(request, cancelToken).ConfigureAwait(false))
                    {
                        EnsureRequestSuccessful(response);
                        var result = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        return result;
                    }
                }
            }
        }


        /// <summary>
        /// Downloads the resource with the specified URI to a byte array
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <param name="additionalHeaders">Extra headers to add to the request</param>
        /// <param name="timeout">Optional.  The number of seconds to wait for a response to the request.</param>
        /// <returns>A byte array containing the body of the response from the resource</returns>
        public async Task<byte[]> DownloadData(string relativeUrl, IList<NameValuePair<string>> additionalHeaders, int? timeout)
        {
            var request = PreProcessRequest(ref relativeUrl, additionalHeaders);

            var effectiveTimeout = timeout.HasValue ? timeout.Value : DefaultTimeooutSeconds;

            using (var timeoutCts = effectiveTimeout <= 0 ? null : new CancellationTokenSource(effectiveTimeout * 1000))
            {
                using (var linkedCts = timeoutCts == null ? null : CancellationTokenSource.CreateLinkedTokenSource(GetCommonCancellationToken(), timeoutCts.Token))
                {
                    var cancelToken = linkedCts == null ? GetCommonCancellationToken() : linkedCts.Token;
                    using (var response = await m_Connection.SendAsync(request, cancelToken).ConfigureAwait(false))
                    {
                        EnsureRequestSuccessful(response);
                        var result = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        return result;
                    }
                }
            }
        }

        /// <summary>
        /// Downloads the resource with the specified URI to a local file.
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <param name="destinationFileName"></param>
        /// <param name="timeout">Optional.  The number of seconds to wait for a response to the request.</param>
        public async Task DownloadFile(string relativeUrl, string destinationFileName, int? timeout)
        {
            var request = PreProcessRequest(ref relativeUrl, null);

            var effectiveTimeout = timeout.HasValue ? timeout.Value : DefaultTimeooutSeconds;

            using (var timeoutCts = effectiveTimeout <= 0 ? null : new CancellationTokenSource(effectiveTimeout * 1000))
            {
                using (var linkedCts = timeoutCts == null ? null : CancellationTokenSource.CreateLinkedTokenSource(GetCommonCancellationToken(), timeoutCts.Token))
                {
                    var cancelToken = linkedCts == null ? GetCommonCancellationToken() : linkedCts.Token;
                    using (var response = await m_Connection.SendAsync(request, cancelToken).ConfigureAwait(false))
                    {
                        EnsureRequestSuccessful(response);
                        using (var resultTask = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            using (var fileStream = File.Create(destinationFileName))
                            {
                                await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                                fileStream.Flush(true);
                                fileStream.SetLength(fileStream.Position);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Downloads the resource with the specified URI to a string
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <param name="timeout">Optional.  The number of seconds to wait for a response to the request.</param>
        /// <returns></returns>
        public async Task<string> DownloadString(string relativeUrl, int? timeout)
        {
            var request = PreProcessRequest(ref relativeUrl, null);

            var effectiveTimeout = timeout.HasValue ? timeout.Value : DefaultTimeooutSeconds;

            using (var timeoutCts = effectiveTimeout <= 0 ? null : new CancellationTokenSource(effectiveTimeout * 1000))
            {
                using (var linkedCts = timeoutCts == null ? null : CancellationTokenSource.CreateLinkedTokenSource(GetCommonCancellationToken(), timeoutCts.Token))
                {
                    var cancelToken = linkedCts == null ? GetCommonCancellationToken() : linkedCts.Token;
                    using (var response = await m_Connection.SendAsync(request, cancelToken).ConfigureAwait(false))
                    {
                        EnsureRequestSuccessful(response);
                        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return result;
                    }
                }
            }
        }

        /// <summary>
        /// Downloads the resource with the specified URI to a string
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <param name="method">The HTTP method used to send the string to the resource.  If null, the default is GET</param>
        /// <param name="timeout">Optional.  The number of seconds to wait for a response to the request.</param>
        /// <returns></returns>
        public async Task<string> DownloadString(string relativeUrl, HttpMethod method, int? timeout)
        {
            method = method ?? HttpMethod.Get;
            var request = PreProcessRequest(ref relativeUrl, ref method, null);

            var effectiveTimeout = timeout.HasValue ? timeout.Value : DefaultTimeooutSeconds;

            using (var timeoutCts = effectiveTimeout <= 0 ? null : new CancellationTokenSource(effectiveTimeout * 1000))
            {
                using (var linkedCts = timeoutCts == null ? null : CancellationTokenSource.CreateLinkedTokenSource(GetCommonCancellationToken(), timeoutCts.Token))
                {
                    var cancelToken = linkedCts == null ? GetCommonCancellationToken() : linkedCts.Token;
                    using (var response = await m_Connection.SendAsync(request, cancelToken).ConfigureAwait(false))
                    {
                        EnsureRequestSuccessful(response);
                        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return result;
                    }
                }
            }
        }
        /// <summary>
        /// Uploads the provided byte array to the specified URI using the provided method.
        /// </summary>
        /// <param name="relativeUrl">The URI of the resource to receive the data. This URI must identify a resource that can accept a request sent with the method requested.</param>
        /// <param name="method">The HTTP method used to send the string to the resource.  If null, the default is POST</param>
        /// <param name="contentType">The content type to inform the server of for this file</param>
        /// <param name="data"></param>
        /// <param name="additionalHeaders">Extra headers to add to the request</param>
        /// <param name="timeout">Optional.  The number of seconds to wait for a response to the request.</param>
        /// <returns>A byte array containing the body of the response from the resource</returns>
        public async Task<byte[]> UploadData(string relativeUrl, HttpMethod method, string contentType, byte[] data, IList<NameValuePair<string>> additionalHeaders, int? timeout)
        {
            method = method ?? HttpMethod.Post;
            var request = PreProcessRequest(ref relativeUrl, ref method, additionalHeaders);

            var effectiveTimeout = timeout.HasValue ? timeout.Value : DefaultTimeooutSeconds;

            request.Content = new ByteArrayContent(data);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using (var timeoutCts = effectiveTimeout <= 0 ? null : new CancellationTokenSource(effectiveTimeout * 1000))
            {
                using (var linkedCts = timeoutCts == null ? null : CancellationTokenSource.CreateLinkedTokenSource(GetCommonCancellationToken(), timeoutCts.Token))
                {
                    var cancelToken = linkedCts == null ? GetCommonCancellationToken() : linkedCts.Token;
                    using (var response = await m_Connection.SendAsync(request, cancelToken).ConfigureAwait(false))
                    {
                        EnsureRequestSuccessful(response);
                        var result = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        return result;
                    }
                }
            }
        }

        /// <summary>
        /// Uploads the specified local file to the specified URI using the specified method
        /// </summary>
        /// <param name="relativeUrl">The URI of the resource to receive the file. This URI must identify a resource that can accept a request sent with the method requested.</param>
        /// <param name="method">The HTTP method used to send the string to the resource.  If null, the default is POST</param>
        /// <param name="contentType">The content type to inform the server of for this file</param>
        /// <param name="sourceFileNamePath"></param>
        /// <param name="timeout">Optional.  The number of seconds to wait for a response to the request.</param>
        /// <returns>A byte array containing the body of the response from the resource</returns>
        public async Task<byte[]> UploadFile(string relativeUrl, HttpMethod method, string contentType, string sourceFileNamePath, int? timeout)
        {
            method = method ?? HttpMethod.Post;
            var request = PreProcessRequest(ref relativeUrl, ref method, null);

            var effectiveTimeout = timeout.HasValue ? timeout.Value : DefaultTimeooutSeconds;

            using (var fileStream = File.OpenRead(sourceFileNamePath))
            {
                request.Content = new StreamContent(fileStream);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                using (var timeoutCts = effectiveTimeout <= 0 ? null : new CancellationTokenSource(effectiveTimeout * 1000))
                {
                    using (var linkedCts = timeoutCts == null ? null : CancellationTokenSource.CreateLinkedTokenSource(GetCommonCancellationToken(), timeoutCts.Token))
                    {
                        var cancelToken = linkedCts == null ? GetCommonCancellationToken() : linkedCts.Token;
                        using (var response = await m_Connection.SendAsync(request, cancelToken).ConfigureAwait(false))
                        {
                            EnsureRequestSuccessful(response);
                            var result = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                            return result;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Uploads the specified string to the specified resource, using the specified method
        /// </summary>
        /// <param name="relativeUrl">The URI of the resource to receive the string. This URI must identify a resource that can accept a request sent with the method requested.</param>
        /// <param name="method">The HTTP method used to send the string to the resource.  If null, the default is POST</param>
        /// <param name="contentType">The content type to inform the server of for this file</param>
        /// <param name="data">The string to be uploaded. </param>
        /// <param name="timeout">Optional.  The number of seconds to wait for a response to the request.</param>
        /// <returns>A string containing the body of the response from the resource</returns>
        public async Task<string> UploadString(string relativeUrl, HttpMethod method, string contentType, string data, int? timeout)
        {
            method = method ?? HttpMethod.Post;
            var request = PreProcessRequest(ref relativeUrl, ref method, null);

            var effectiveTimeout = timeout.HasValue ? timeout.Value : DefaultTimeooutSeconds;

            request.Content = new StringContent(data);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using (var timeoutCts = effectiveTimeout <= 0 ? null : new CancellationTokenSource(effectiveTimeout * 1000))
            {
                using (var linkedCts = timeoutCts == null ? null : CancellationTokenSource.CreateLinkedTokenSource(GetCommonCancellationToken(), timeoutCts.Token))
                {
                    var cancelToken = linkedCts == null ? GetCommonCancellationToken() : linkedCts.Token;
                    using (var response = await m_Connection.SendAsync(request, cancelToken).ConfigureAwait(false))
                    {
                        EnsureRequestSuccessful(response);
                        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return result;
                    }
                }
            }
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Dispose managed objects
        /// </summary>
        /// <param name="releaseManagedObjects"></param>
        protected virtual void Dispose(bool releaseManagedObjects)
        {
            if (releaseManagedObjects && (m_Disposed == false))
            {
                Cancel();
                if (m_Connection != null)
                {
                    m_Connection.Dispose();
                    m_Connection = null;
                }
                m_Disposed = true;
            }
        }

        /// <summary>
        /// Raises the ConnectionStateChanged event
        /// </summary>
        /// <param name="state">The new connection state</param>
        /// <remarks>Note to inheritors:  be sure to call the base implementation to ensure the event is raised.</remarks>
        protected virtual void OnConnectionStateChanged(ChannelConnectionState state)
        {
            ChannelConnectionStateChangedEventHandler tempEvent = ConnectionStateChanged;

            if (tempEvent != null)
            {
                var e = new ChannelConnectionStateChangedEventArgs(state);
                tempEvent.Invoke(this, e);
            }
        }

        #endregion

        #region Private Properties and Methods

        private void EnsureConnectionInitialized()
        {
            lock (m_Lock)
            {
                if (m_Connection == null)
                {

                    m_BaseAddress = CalculateBaseAddress();

                    var requestHandler = new HttpClientHandler();

                    if ((requestHandler.Proxy != null) && (requestHandler.Proxy.Credentials == null))
                    {
                        requestHandler.Proxy.Credentials = CredentialCache.DefaultCredentials;
                    }

                    if (requestHandler.SupportsAutomaticDecompression)
                    {
                        requestHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    }

                    if (!Log.SilentMode)
                    {
                        var clientLogger = new HttpClientLogger(m_Logger, requestHandler);
                        m_Connection = new HttpClient(clientLogger);
                    }
                    else
                    {
                        m_Connection = new HttpClient();
                    }

                    m_Connection.BaseAddress = m_BaseAddress;
                    m_Connection.Timeout = new TimeSpan(0, 2, 0);
                }

                System.Threading.Monitor.PulseAll(m_Lock);
            }
        }

        /// <summary>
        /// Indicates if we can continue retrying.
        /// </summary>
        /// <param name="maxRetries"></param>
        /// <param name="errorCount"></param>
        /// <returns></returns>
        private bool CanRetry(int maxRetries, int errorCount)
        {
            return ((maxRetries < 0) || (errorCount <= maxRetries));
        }

        private Uri CalculateBaseAddress()
        {
            bool usePort = true;
            if ((m_UseSsl == false) && ((m_Port == 0) || (m_Port == 80)))
            {
                usePort = false;
            }
            else if ((m_UseSsl) && ((m_Port == 0) || (m_Port == 443)))
            {
                usePort = false;
            }

            StringBuilder baseAddress = new StringBuilder(1024);

            baseAddress.AppendFormat("{0}://{1}", UseSsl ? "https" : "http", HostName);

            if (usePort)
            {
                baseAddress.AppendFormat(":{0}", m_Port);
            }

            if (string.IsNullOrEmpty(m_ApplicationBaseDirectory) == false)
            {
                baseAddress.Append(m_ApplicationBaseDirectory);
            }

            return new Uri(baseAddress.ToString());
        }

        /// <summary>
        /// Performs request processing just prior to execution but after the IWebRequest has called.
        /// </summary>
        private HttpRequestMessage PreProcessRequest(ref string relativeUrl)
        {
            HttpMethod method = HttpMethod.Get;
            return PreProcessRequest(ref relativeUrl, ref method, null);
        }

        /// <summary>
        /// Performs request processing just prior to execution but after the IWebRequest has called.
        /// </summary>
        private HttpRequestMessage PreProcessRequest(ref string relativeUrl, IList<NameValuePair<string>> additionalHeaders)
        {
            HttpMethod method = HttpMethod.Get;
            return PreProcessRequest(ref relativeUrl, ref method, additionalHeaders);
        }

        /// <summary>
        /// Performs request processing just prior to execution but after the IWebRequest has called.
        /// </summary>
        private HttpRequestMessage PreProcessRequest(ref string relativeUrl, ref HttpMethod method, IList<NameValuePair<string>> additionalHeaders)
        {
            //see if we've ever attempted a request - if not we're going to do some first time things.
            if (m_FirstRequest)
            {
                m_UseCompatibilityMethods = GetUseCompatiblilityMethodsOverride(m_HostName);
                m_UseHttpVersion10 = GetUseHttpVersion10Override(m_HostName);

                m_FirstRequest = false;
            }

            //get rid of any leading slashes
            relativeUrl = (relativeUrl.StartsWith("/") ? relativeUrl.Substring(1) : relativeUrl);

            var request = new HttpRequestMessage(method, relativeUrl);

            //put in any additional headers we got.  By doing them first, if they conflict with one of our headers
            //the conflict will be resolved in favor of the base implementation, forcing the dev to deal with their error first.
            if (additionalHeaders != null)
            {
                foreach (NameValuePair<string> additionalHeader in additionalHeaders)
                {
                    request.Headers.Add(additionalHeader.Name, additionalHeader.Value);
                }
            }

            //see if we need to override the method.  I'm just sick and tired of !@%@ IIS blocking PUT and DELETE.
            if (m_UseCompatibilityMethods)
            {
                if (method == HttpMethod.Put || method == HttpMethod.Delete)
                {
                    request.Headers.Add(HeaderRequestMethod, method.Method);

                    //and override the method back to post, which will work.
                    method = HttpMethod.Post;
                    request.Method = method;
                }
            }

            request.Version = (m_UseHttpVersion10) ? new Version(1, 0) : new Version(1, 1);

            //add our request timestamp so everyone agrees.
            request.Headers.Add(HeaderRequestTimestamp, DateTimeOffset.UtcNow.ToString("o"));

            //and if we have a protocol version the caller is using specify that so the server knows.
            if (AppProtocolVersion != null)
            {
                request.Headers.Add(HeaderRequestAppProtocolVersion, AppProtocolVersion.ToString());
            }

            //Extension our authentication headers if there is an authentication object
            if (m_AuthenticationProvider != null)
            {
                m_AuthenticationProvider.PreProcessRequest(this, m_Connection, request, relativeUrl, m_RequestSupportsAuthentication);
            }

            return request;
        }

        private async Task SleepForConnection()
        {
            //adjust our delay because there's yet another error.
            int delayIncrement = Math.Min(m_RetryDelaySeconds * 2, 5);
            m_RetryDelaySeconds += delayIncrement;
            m_RetryDelaySeconds = Math.Min(m_RetryDelaySeconds, MaximumRetryDelaySeconds);

            //and wait that long.
            await Task.Delay(new TimeSpan(0, 0, m_RetryDelaySeconds), GetCommonCancellationToken()).ConfigureAwait(false);
        }

        /// <summary>
        /// Set a new connection state, raising an event if it has changed.
        /// </summary>
        /// <param name="newState"></param>
        private void SetConnectionState(ChannelConnectionState newState)
        {
            bool stateChanged = false;

            //while we can atomically read or write connection state, we want to a get and set.
            lock (m_Lock)
            {
                if (newState != m_ConnectionState)
                {
                    stateChanged = true;
                    m_ConnectionState = newState;
                }

                System.Threading.Monitor.PulseAll(m_Lock);
            }

            //only raise the event if we changed the state, and now we're outside of the lock so it's safe.
            if (stateChanged)
                OnConnectionStateChanged(newState);
        }

        private Uri GenerateUri(string relativeUrl)
        {
            var targetUri = new Uri(m_BaseAddress, relativeUrl);

            return targetUri;
        }

        /// <summary>
        /// Get a cancellation token for the channel's explicit cancel source
        /// </summary>
        /// <returns></returns>
        private CancellationToken GetCommonCancellationToken()
        {
            lock (m_Lock)
            {
                //if we previously canceled we need to get a new cancellation token source because this is a new activity.
                if (m_CancellationTokenSource.IsCancellationRequested)
                {
                    m_CancellationTokenSource.Dispose();
                    m_CancellationTokenSource = new CancellationTokenSource();
                }

                return m_CancellationTokenSource.Token;
            }
        }

        /// <summary>
        /// Get the current cached compatibility method setting for a server.
        /// </summary>
        /// <param name="server">The DNS name of the server</param>
        /// <returns></returns>
        private bool GetUseCompatiblilityMethodsOverride(string server)
        {
            //don't forget that we have to lock shared collections, they aren't thread safe
            bool useCompatibilityMethods = true; //in the end it was just too painful to get all those exceptions.
            lock (s_ServerUseCompatibilitySetting)
            {
                if (s_ServerUseCompatibilitySetting.TryGetValue(server, out var rawValue))
                {
                    useCompatibilityMethods = rawValue;
                }

                System.Threading.Monitor.PulseAll(s_ServerUseCompatibilitySetting);
            }

            return useCompatibilityMethods;
        }

        /// <summary>
        /// Get the current cached compatibility method setting for a server.
        /// </summary>
        /// <param name="server">The DNS name of the server</param>
        /// <returns></returns>
        private bool GetUseHttpVersion10Override(string server)
        {
            //don't forget that we have to lock shared collections, they aren't threadsafe
            bool useHttpVerison10 = m_UseHttpVersion10;
            lock (s_ServerUseHttpVersion10Setting)
            {
                if (s_ServerUseHttpVersion10Setting.TryGetValue(server, out var rawValue))
                {
                    useHttpVerison10 = rawValue;
                }

                System.Threading.Monitor.PulseAll(s_ServerUseHttpVersion10Setting);
            }

            return useHttpVerison10;
        }

        /// <summary>
        /// Update the cached compatibility methods setting for a server (we assume a server will either need it or not)
        /// </summary>
        /// <param name="server">The DNS name of the server</param>
        /// <param name="useCompatibilityMethods">the new setting</param>
        private void SetUseCompatiblilityMethodsOverride(string server, bool useCompatibilityMethods)
        {
            //remember: generic collections are not thread safe.
            lock (s_ServerUseCompatibilitySetting)
            {
                if (s_ServerUseCompatibilitySetting.ContainsKey(server))
                {
                    s_ServerUseCompatibilitySetting[server] = useCompatibilityMethods;
                }
                else
                {
                    s_ServerUseCompatibilitySetting.Add(server, useCompatibilityMethods);
                }

                System.Threading.Monitor.PulseAll(s_ServerUseCompatibilitySetting);
            }
        }


        /// <summary>
        /// Update the cached HTTP protocol version setting for a server (we assume a server will either need it or not)
        /// </summary>
        /// <param name="server">The DNS name of the server</param>
        /// <param name="useHttpVersion10">the new setting</param>
        private void SetUseHttpVersion10Override(string server, bool useHttpVersion10)
        {
            //remember: generic collections are not threadsafe.
            lock (s_ServerUseHttpVersion10Setting)
            {
                if (s_ServerUseHttpVersion10Setting.ContainsKey(server))
                {
                    s_ServerUseHttpVersion10Setting[server] = useHttpVersion10;
                }
                else
                {
                    s_ServerUseHttpVersion10Setting.Add(server, useHttpVersion10);
                }

                System.Threading.Monitor.PulseAll(s_ServerUseHttpVersion10Setting);
            }
        }

        #endregion
    }
}
