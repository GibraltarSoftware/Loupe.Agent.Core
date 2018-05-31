namespace Loupe.Agent.AspNetCore.Metrics
{
    public class MetricValue
    {
        public const string PageName = "pageName";
        public const string AbsolutePath = "absolutePath";
        public const string TotalDuration = "totalDuration";
        public const string AuthenticateDuration = "authenticateDuration";
        public const string AuthorizeRequestDuration = "authorizeRequestDuration";
        public const string ResolveRequestCacheDuration = "resolveRequestCacheDuration";
        public const string AcquireRequestStateDuration = "acquireRequestStateDuration";
        public const string RequestHandlerExecuteDuration = "requestHandlerExecuteDuration";
        public const string ReleaseRequestStateDuration = "releaseRequestStateDuration";
        public const string UpdateRequestCacheDuration = "updateRequestCacheDuration";
        public const string LogRequestDuration = "logRequestDuration";
        public const string ServedFromCache = "servedFromCache";
        public const string QueryString = "queryString";
        public const string UserName = "userName";
        public const string SessionId = "sessionId";
        public const string AgentSessionId = "agentSessionId";
    }
}