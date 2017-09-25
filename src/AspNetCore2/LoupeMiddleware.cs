using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Loupe.Agent.AspNetCore
{
    public class LoupeMiddleware
    {
        private readonly LoupeAgent _agent;
        private readonly RequestDelegate _next;
        private readonly EventMetric _requestMetric;

        public LoupeMiddleware(RequestDelegate next, LoupeAgent agent, IApplicationLifetime applicationLifetime)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            if (applicationLifetime == null) throw new ArgumentNullException(nameof(applicationLifetime));

            applicationLifetime.ApplicationStarted.Register(StartSession);
            applicationLifetime.ApplicationStopped.Register(OnApplicationStopping);

            var requestMetricDefinition = DefineRequestMetric(agent.ApplicationName);
            _requestMetric = EventMetric.Register(requestMetricDefinition, null);
        }

        public async Task Invoke(HttpContext context)
        {
            context.RequestAborted.Register(OnRequestAborted, context);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
                stopwatch.Stop();
                WriteSample(context, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                WriteSample(context, stopwatch.Elapsed, ex);
                throw;
            }

        }

        private void WriteSample(HttpContext context, TimeSpan elapsed, Exception ex = null)
        {
            var sample = _requestMetric.CreateSample();
            
            var routeString = context.Features.Get<IRoutingFeature>().GetRouteString();
            sample.SetValue("request", routeString);
            sample.SetValue("duration", elapsed);
            if (ex != null)
            {
                sample.SetValue("error", ex.Message);
            }
            sample.Write();
        }

        private void StartSession()
        {
            _agent.Start();
        }

        private void OnApplicationStopping()
        {
            _agent.End(SessionStatus.Normal, "ApplicationStopping");
        }

        private static void OnRequestAborted(object obj)
        {
            var context = (HttpContext) obj;
            Log.Warning(LogWriteMode.Queued, "HttpContext", "RequestAborted", $"{context.Request.Path}");
        }

        private static EventMetricDefinition DefineRequestMetric(string applicationName)
        {
            
            var definition = new EventMetricDefinition(applicationName, "AspNetCore", "Request");
            definition.AddValue("request", typeof(string), SummaryFunction.Count, "Request", "Request",
                "The query that was executed.");
            definition.AddValue("duration", typeof(TimeSpan), SummaryFunction.Average, "Duration", "Duration",
                "Time taken to execute the query");
            definition.AddValue("error", typeof(string), SummaryFunction.Count, "Errors", "Errors",
                "Errors executing query.");
            EventMetricDefinition.Register(ref definition);
            return definition;
        }
    }
}