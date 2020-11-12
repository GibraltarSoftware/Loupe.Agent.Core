namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Provides constants for metric names.
    /// </summary>
    public class MetricValue
    {
        /// <summary>
        /// The page name metric name.
        /// </summary>
        public const string PageName = "pageName";
        /// <summary>
        /// The absolute path metric name.
        /// </summary>
        public const string AbsolutePath = "absolutePath";
        /// <summary>
        /// The total duration metric name.
        /// </summary>
        public const string TotalDuration = "totalDuration";
        /// <summary>
        /// The authenticate duration metric name.
        /// </summary>
        public const string AuthenticateDuration = "authenticateDuration";
        /// <summary>
        /// The authorize request duration metric name.
        /// </summary>
        public const string AuthorizeRequestDuration = "authorizeRequestDuration";
        /// <summary>
        /// The resolve request cache duration metric name.
        /// </summary>
        public const string ResolveRequestCacheDuration = "resolveRequestCacheDuration";
        /// <summary>
        /// The acquire request state duration metric name.
        /// </summary>
        public const string AcquireRequestStateDuration = "acquireRequestStateDuration";
        /// <summary>
        /// The request handler execute duration metric name.
        /// </summary>
        public const string RequestHandlerExecuteDuration = "requestHandlerExecuteDuration";
        /// <summary>
        /// The release request state duration metric name.
        /// </summary>
        public const string ReleaseRequestStateDuration = "releaseRequestStateDuration";
        /// <summary>
        /// The update request cache duration metric name.
        /// </summary>
        public const string UpdateRequestCacheDuration = "updateRequestCacheDuration";
        /// <summary>
        /// The log request duration metric name.
        /// </summary>
        public const string LogRequestDuration = "logRequestDuration";
        /// <summary>
        /// The served from cache metric name.
        /// </summary>
        public const string ServedFromCache = "servedFromCache";
        /// <summary>
        /// The query string metric name.
        /// </summary>
        public const string QueryString = "queryString";
        /// <summary>
        /// The user name metric name.
        /// </summary>
        public const string UserName = "userName";
        /// <summary>
        /// The session identifier metric name.
        /// </summary>
        public const string SessionId = "sessionId";
        /// <summary>
        /// The agent session identifier metric name.
        /// </summary>
        public const string AgentSessionId = "agentSessionId";
    }
}