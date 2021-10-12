using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using Loupe.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.ObjectPool;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Tracking metric for an ASP.NET controller request
    /// </summary>
    public abstract class ActionMetricBase : IMessageSourceProvider
    {
        private readonly ObjectPool<StringBuilder> _stringBuilderPool;
        private readonly long _startTicks;

        /// <summary>
        /// Default serializer options
        /// </summary>
        protected static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        {
#if NETCOREAPP2_1 || NETCOREAPP3_1
            IgnoreNullValues = true,
#else
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
#endif
            WriteIndented = true
        };

        /// <summary>
        /// Constructor for .NET Core 3 and later
        /// </summary>
        internal ActionMetricBase(AspNetConfiguration options,
            ObjectPool<StringBuilder> stringBuilderPool,
            HttpContext httpContext, 
            ActionDescriptor actionDescriptor,
            ActionMetricBase? rootActionMetric)
        {
            Options = options;
            _stringBuilderPool = stringBuilderPool;
            RootActionMetric = rootActionMetric;

            StartTimestamp = DateTimeOffset.Now;
            _startTicks = Stopwatch.GetTimestamp();

            ConnectionId = httpContext.Connection.Id;
            RequestId = httpContext.TraceIdentifier;

            SessionId = httpContext.GetSessionId();
            AgentSessionId = httpContext.GetAgentSessionId();

            HttpMethod = httpContext.Request.Method;

            Parameters = StringifyParameterNames(actionDescriptor.Parameters);
        }

        /// <summary>
        /// Constructor for .NET Core 2
        /// </summary>
        internal ActionMetricBase(AspNetConfiguration options,
            ObjectPool<StringBuilder> stringBuilderPool,
            ActionExecutingContext actionExecutingContext)
        {
            Options = options;
            _stringBuilderPool = stringBuilderPool;

            StartTimestamp = DateTimeOffset.Now;
            _startTicks = Stopwatch.GetTimestamp();

            var httpContext = actionExecutingContext.HttpContext;

            ConnectionId = httpContext.Connection.Id;
            RequestId = httpContext.TraceIdentifier;

            SessionId = httpContext.GetSessionId();
            AgentSessionId = httpContext.GetAgentSessionId();

            HttpMethod = httpContext.Request.Method;

            Parameters = StringifyParameterNames(actionExecutingContext.ActionDescriptor.Parameters);
        }

        internal void SetParameterDetails(IReadOnlyDictionary<string, object> actionArguments)
        {
            if (actionArguments.Count == 0) return;

            //calculate these now because we don't keep the context around.
            var parameterBuilder = _stringBuilderPool.Get();
            try
            {
                parameterBuilder.AppendLine("Parameters:");
                foreach (var argument in actionArguments)
                {
                    parameterBuilder.AppendFormat("- {0}: {1}\r\n", argument.Key,
                        Extensions.ObjectToString(argument.Value, Options.LogRequestParameterDetails));
                }

                ParameterDetails = parameterBuilder.ToString();
            }
            finally
            {
                _stringBuilderPool.Return(parameterBuilder);
            }
        }

        private static string StringifyParameterNames(IList<ParameterDescriptor> actionDescriptorParameters)
        {
            int count = actionDescriptorParameters.Count;

            if (count == 0)
            {
                return string.Empty;
            }

            if (count == 1)
            {
                return actionDescriptorParameters[0].Name;
            }

            int length = 0;
            for (int i = 0; i < count; i++)
            {
                length += actionDescriptorParameters[i].Name.Length + 2;
            }

            var buffer = ArrayPool<char>.Shared.Rent(length);
            try
            {
                string name = actionDescriptorParameters[0].Name;
                name.CopyTo(0, buffer, 0, name.Length);
                int index = name.Length;

                for (int i = 1; i < count; i++)
                {
                    buffer[index++] = ',';
                    buffer[index++] = ' ';
                    name = actionDescriptorParameters[i].Name;
                    name.CopyTo(0, buffer, index, name.Length);
                    index += name.Length;
                }

                return new string(buffer, 0, index);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Timestamp the request started
        /// </summary>
        [EventMetricValue("startTimestamp", SummaryFunction.Count, null, Caption = "Started", Description = "Timestamp the request started")]
        [JsonIgnore]
        public DateTimeOffset StartTimestamp { get; }

        /// <summary>
        /// The duration the request has been running
        /// </summary>
        /// <remarks>Once the request has been told to record it the timer duration will stop increasing.</remarks>
        [EventMetricValue("totalDuration", SummaryFunction.Average, "ms", Caption = "Total Request Duration", 
            Description = "The entire time spent processing the request, excluding time to return the response", IsDefaultValue = true)]
        [JsonIgnore]
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// The duration the Action took to execute
        /// </summary>
        /// <remarks>The action is typically the controller or page's internal code.</remarks>
        [EventMetricValue("actionDuration", SummaryFunction.Average, "ms", Caption = "Action Duration",
            Description = "The time spent in the Action or Page")]
        [JsonIgnore]
        public TimeSpan? ActionDuration { get; private set; }

        /// <summary>
        /// The time it took for the request to be authorized
        /// </summary>
        [EventMetricValue("authorizeRequestDuration", SummaryFunction.Average, "ms", Caption = "Authorize Request Duration",
            Description = "The time it took for the request to be authorized")]
        [JsonIgnore]
        public TimeSpan? AuthorizeRequestDuration { get; internal set; }

        /// <summary>
        /// The controller and action referenced by the request
        /// </summary>
        [EventMetricValue("request", SummaryFunction.Count, null, Caption = "Request", Description = "The controller and action referenced by the request")]
        public string? Request { get; protected set; }

        /// <summary>
        /// Gets/Sets the HttpMethod (GET, POST, PUT, DELETE, etc) used for this action.
        /// </summary>
        /// <remarks>
        /// In MVC, some actions (typically an EDIT) have both definition for both GET and
        /// POST.  This value helps differentiate between those two calls
        /// </remarks>
        [EventMetricValue("httpMethod", SummaryFunction.Count, null, Caption = "Http Method", Description = "The HTTP Method (GET, POST, PUT, DELETE, etc) used for this action")]
        public string HttpMethod { get; }

        /// <summary>
        /// Gets/Sets a String that represents the parameters passed to this action
        /// </summary>
        /// <remarks></remarks>
        [EventMetricValue("parameters", SummaryFunction.Count, null, Caption = "Parameters", Description = "The list of parameters used for this action")]
        [JsonIgnore]
        public string? Parameters { get; }

        /// <summary>
        /// A multi-line string describing the content of the parameters.
        /// </summary>
        /// <remarks>Only set if LogParameterDetails is enabled</remarks>
        [JsonIgnore]
        public string? ParameterDetails { get; protected set; }

        /// <summary>
        /// The Http Response Code for the request.
        /// </summary>
        [EventMetricValue("responseCode", SummaryFunction.Count, null, Caption = "Response Code", Description = "The Http response code for this request")]
        public int ResponseCode { get; set; }

        /// <summary>
        /// The user name for the action being performed.
        /// </summary>
        [EventMetricValue("userName", SummaryFunction.Count, null, Caption = "User", Description = "The user associated with the action being performed")]
        [JsonIgnore]
        public string? UserName { get; set; }

        /// <summary>
        /// The exception, if any, thrown at the completion of the routine
        /// </summary>
        [EventMetricValue("exception", SummaryFunction.Count, null, Caption = "Exception", Description = "The exception, if any, thrown at the completion of the routine")]
        [JsonIgnore]//System.Text.Json doesn't like varying types, and we would want to only serialize the type name.
        public Exception? Exception { get; set; }

        /// <summary>
        /// Id to identify a user session from the web browser
        /// </summary>
        [EventMetricValue("SessionId", SummaryFunction.Count, null, Caption = "Session Id", Description = "Session Id associated with action being performed")]
        [JsonPropertyName("LoupeSessionId")] //match MEL
        public string? SessionId { get; }

        /// <summary>
        /// Id from JavaScript agent for session
        /// </summary>
        [EventMetricValue("AgentSessionId", SummaryFunction.Count, null, Caption = "Agent Session Id", Description = "Id from JavaScript agent for session")]
        [JsonPropertyName("LoupeAgentSessionId")] //match MEL
        public string? AgentSessionId { get; }

        /// <summary>
        /// The unique Id of the Http connection that provided the request
        /// </summary>
        [EventMetricValue("ConnectionId", SummaryFunction.Count, null, Caption = "Connection Id", Description = "The unique Id of the Http connection that provided the request")]
        public string ConnectionId { get; set; }

        /// <summary>
        /// The unique Id of the request being executed
        /// </summary>
        [EventMetricValue("Id", SummaryFunction.Count, null, Caption = "Id", Description = "The unique Id of the request being executed")]
        public string? RequestId { get; set; }

        /// <summary>
        /// The unique Id of the request being executed
        /// </summary>
        [EventMetricValue("SpanId", SummaryFunction.Count, null, Caption = "Span Id", Description = "The W3C Span of the Request Id if present")]
        public string? SpanId { get; set; }

        /// <summary>
        /// The unique Id of the request being executed
        /// </summary>
        [EventMetricValue("TraceId", SummaryFunction.Count, null, Caption = "Trace Id", Description = "The W3C Id of the Request Id if present")]
        public string? TraceId { get; set; }

        /// <summary>
        /// The span Id of the parent span that this request is a part of
        /// </summary>
        [EventMetricValue("ParentId", SummaryFunction.Count, null, Caption = "Parent Id", Description = "The Id of the parent activity that this request is a part of")]
        public string? ParentId { get; set; }

        /// <summary>
        /// Pull the trace information from the provided activity.
        /// </summary>
        /// <param name="activity"></param>
        internal void SetActivity(Activity? activity)
        {
            if (activity != null)
            {
                RequestId = activity.Id;
                ParentId = activity.ParentId;

#if NETCOREAPP3_0_OR_GREATER
                if (activity.IdFormat == ActivityIdFormat.W3C)
                {
                    SpanId = activity.SpanId.ToString();
                    TraceId = activity.TraceId.ToString();
                }
#endif
            }
        }


#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Records the metrics for this request
        /// </summary>
        public void Stop(Activity? activity)
        {
            ActionDuration = new TimeSpan(Stopwatch.GetTimestamp() - _startTicks);

            if (Gibraltar.Monitor.Log.PrincipalResolver != null
                && Gibraltar.Monitor.Log.PrincipalResolver.TryResolveCurrentPrincipal(out var principal))
            {
                UserName = principal.Identity?.Name;
            }

            SetActivity(activity);
        }
#else
        /// <summary>
        /// Records the metrics for this request
        /// </summary>
        public void Stop(ActionExecutedContext actionExecutedContext, Activity activity)
        {
            ActionDuration = new TimeSpan(Stopwatch.GetTimestamp() - _startTicks);

            if (actionExecutedContext.Exception != null)
            {
                Exception = actionExecutedContext.Exception;
            }

            SetActivity(activity);
        }
#endif

        /// <summary>
        /// The active configuration for the Asp.NET Agent.
        /// </summary>
        [JsonIgnore]
        protected AspNetConfiguration Options { get; }

        /// <summary>
        /// The action metric for the original client request, if not this metric
        /// </summary>
        /// <remarks>Due to pipeline changes, exception handling, or other advanced
        /// scenarios a secondary action may be invoked in the same request. When that
        /// happens, this represents the original request.</remarks>
        [JsonIgnore]
        protected ActionMetricBase? RootActionMetric { get; }

        /// <summary>
        /// Record the request start information (just before user code executes)
        /// </summary>
        /// <remarks>Logs the request if logging is enabled.</remarks>
        public void RecordRequest()
        {
            if (Options.Enabled == false || Options.LogRequests == false)
                return;

            OnLogRequest();
        }

        /// <summary>
        /// Record the request completion information
        /// </summary>
        /// <param name="context"></param>
        /// <remarks>Writes an event metric for the request if metrics are enabled</remarks>
        public void Record(HttpContext context)
        {
            var response = context.Response;
            ResponseCode = response.StatusCode;

            if (Options.Enabled == false)
                return;

            if (Options.LogRequests)
                OnLogRequestCompletion();

            if (Options.LogRequestMetrics)
                EventMetric.Write(this);
        }

        /// <summary>
        /// Record the request to the log just before the controller executes.
        /// </summary>
        protected virtual void OnLogRequest()
        {

        }

        /// <summary>
        /// Record the outcome of the request to the log.
        /// </summary>
        protected virtual void OnLogRequestCompletion()
        {

        }

        /// <inheritdoc />
        [JsonIgnore]
        public string? MethodName { get; set; }

        /// <inheritdoc />
        [JsonIgnore]
        public string? ClassName { get; set; }

        /// <inheritdoc />
        [JsonIgnore]
        public string? FileName { get; set; }

        /// <inheritdoc />
        [JsonIgnore]
        public int LineNumber { get; set; }
    }
}