using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using Loupe.Agent.Core.Services;
using Loupe.Extensibility.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
#if NETCOREAPP3_0_OR_GREATER
using Microsoft.Extensions.Hosting;
#endif

namespace Loupe.Agent.AspNetCore
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// ASP.NET Core Middleware to time requests and track errors.
    /// </summary>
    public class LoupeMiddleware
    {
        private readonly LoupeAgent _agent;
        private readonly RequestDelegate _next;
        private readonly EventMetric _requestMetric;

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Constructs and instance of <see cref="LoupeMiddleware"/>.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="agent"></param>
        /// <param name="applicationLifetime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public LoupeMiddleware(RequestDelegate next, LoupeAgent agent, IHostApplicationLifetime applicationLifetime)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            if (applicationLifetime == null) throw new ArgumentNullException(nameof(applicationLifetime));

            applicationLifetime.ApplicationStarted.Register(StartSession);
            applicationLifetime.ApplicationStopped.Register(OnApplicationStopping);

            var requestMetricDefinition = DefineRequestMetric(agent.ApplicationName);
            _requestMetric = EventMetric.Register(requestMetricDefinition, null);
        }
#else
        /// <summary>
        /// Constructs and instance of <see cref="LoupeMiddleware"/>.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="agent"></param>
        /// <param name="applicationLifetime"></param>
        /// <exception cref="ArgumentNullException"></exception>
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
#endif

        /// <summary>
        /// The automagically-called method that processes the request.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> instance for the current request.</param>
        /// <returns>A <see cref="Task"/> that completes when the middleware has processed the request.</returns>
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

        private void WriteSample(HttpContext context, TimeSpan elapsed, Exception? ex = null)
        {
            var sample = _requestMetric.CreateSample();

            var routeString = context.GetRouteString();
            sample.SetValue("request", routeString);
            sample.SetValue("duration", elapsed);
            if (ex != null)
            {
                sample.SetValue("error", ex.GetBaseException().GetType().Name);
            }
            sample.Write();
        }

        private void StartSession()
        {
            _agent.Start();
        }

        private void OnApplicationStopping()
        {
            _agent.End(SessionStatus.Normal, "Application Stopping");
        }

        private static void OnRequestAborted(object? obj)
        {
            if (obj is HttpContext context)
            {
                Log.Warning(LogWriteMode.Queued, "HttpContext", "Request Aborted", $"{context.Request.Path}");
            }
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