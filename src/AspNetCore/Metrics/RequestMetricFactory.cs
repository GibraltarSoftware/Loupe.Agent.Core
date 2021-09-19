using Gibraltar.Agent.Metrics;
using Loupe.Agent.AspNetCore.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Factory to create action metrics for requests as they start
    /// </summary>
    public class RequestMetricFactory
    {
        private const string MetricName = "request";
        private const string MetricCaption = "Request";
        private const string MetricDescription = "Performance tracking data about static content & external requests";

        private readonly EventMetric _metric;

        /// <summary>
        /// Create a new action metric factory fur the current application
        /// </summary>
        public RequestMetricFactory()
        {
            var definition = GetMetricDefinition();
            _metric = EventMetric.Register(definition, null);
        }

        /// <summary>
        /// Start a new action metric for the current request
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public RequestMetric Start(HttpContext context)
        {
            return new RequestMetric(_metric, context);
        }

        private static EventMetricDefinition GetMetricDefinition()
        {
            if (!EventMetricDefinition.TryGetValue(Constants.LogSystem, Constants.MetricCategory, MetricCaption, out var eventMetricDefinition))
            {
                eventMetricDefinition = new EventMetricDefinition(Constants.LogSystem, Constants.MetricCategory, MetricName)
                {
                    Caption = MetricCaption,
                    Description = MetricDescription
                };

                eventMetricDefinition.AddHitCount(MetricValue.PageName, "Page", "The page name without path");

                eventMetricDefinition.AddHitCount(MetricValue.AbsolutePath, "Absolute Path",
                    "The full path from the root of the web site to the page that was requested including the page");

                eventMetricDefinition.AddDuration(MetricValue.TotalDuration,
                    "Total Request Duration",
                    "The entire time it took for the request to be satisfied");

                eventMetricDefinition.AddDuration(MetricValue.AuthenticateDuration,
                    "Authenticate Request Duration",
                    "The time it took for the request to be authenticated");

                eventMetricDefinition.AddDuration(MetricValue.AuthorizeRequestDuration,
                    "Authorize Request Duration",
                    "The time it took for the request to be authorized");

                eventMetricDefinition.AddDuration(MetricValue.ResolveRequestCacheDuration,
                    "Resolve Request Cache Duration",
                    "The time it took for the request to be looked up in cache");

                eventMetricDefinition.AddDuration(MetricValue.AcquireRequestStateDuration,
                    "Acquire Request State Duration",
                    "The time it took for the request state to be acquired");

                eventMetricDefinition.AddDuration(MetricValue.RequestHandlerExecuteDuration,
                    "Request Handler Execute Duration",
                    "The time it took for the request handler to execute. This includes the time for most ASP.NET page code");

                eventMetricDefinition.AddDuration(MetricValue.ReleaseRequestStateDuration,
                    "Release Request State Duration",
                    "The time it took for the request state to be released");

                eventMetricDefinition.AddDuration(MetricValue.UpdateRequestCacheDuration,
                    "Update Request Cache Duration",
                    "The time it took for the request cache to be updated");

                eventMetricDefinition.AddDuration(MetricValue.LogRequestDuration,
                    "Log Request Duration",
                    "The time it took for the request to be logged");

                eventMetricDefinition.AddFlag(MetricValue.ServedFromCache,
                    "Cached Response",
                    "Indicates if the response was served from the output cache instead of generated");

                eventMetricDefinition.AddHitCount(MetricValue.QueryString,
                    "Query String",
                    "The query string used for the request");

                eventMetricDefinition.AddCount(MetricValue.UserName,
                    "User",
                    "The user associated with the action being performed");

                eventMetricDefinition.AddCount(MetricValue.SessionId,
                    "SessionId",
                    "Session Id associated with action being performed");

                eventMetricDefinition.AddCount(MetricValue.AgentSessionId,
                    "AgentSessionId",
                    "Id from JavaScript agent for session");

                EventMetricDefinition.Register(ref eventMetricDefinition);
            }

            return eventMetricDefinition;
        }
    }
}