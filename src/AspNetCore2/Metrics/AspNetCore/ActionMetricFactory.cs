using System;
using System.Threading;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public class ActionMetricFactory
    {
        private const string MetricCategory = "Web Site.Requests";
        private const string MetricName = "Page Hit";
        private const string MetricCaption = "Page Hit";
        private const string MetricDescription = "Performance tracking data about every web page hit";
        private const string MetricSystem = "Gibraltar";

        private readonly EventMetric _metric;
        private string _applicationName;

        public ActionMetricFactory(string applicationName)
        {
            _applicationName = applicationName;
            var definition = GetMetricDefinition();
            _metric = EventMetric.Register(definition, null);
        }

        public ActionMetric Start(IProxyHttpContext context)
        {
            return new ActionMetric(_metric, context);
        }

        private static EventMetricDefinition GetMetricDefinition()
        {
            if (!EventMetricDefinition.TryGetValue(MetricSystem, MetricCategory, MetricCaption, out var eventMetricDefinition))
            {
                eventMetricDefinition = new EventMetricDefinition(MetricSystem, MetricCategory, MetricName)
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