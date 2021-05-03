using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
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
        private readonly long _startTicks;

        /// <summary>
        /// Constructor for .NET Core 3 and later
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="actionDescriptor"></param>
        internal ActionMetricBase(HttpContext httpContext, ActionDescriptor actionDescriptor)
        {

            StartTimestamp = DateTimeOffset.Now;
            _startTicks = Stopwatch.GetTimestamp();

            SessionId = httpContext.GetSessionId();
            AgentSessionId = httpContext.GetAgentSessionId();

            HttpMethod = httpContext.Request.Method;

            Parameters = StringifyParameterNames(actionDescriptor.Parameters);
        }

        /// <summary>
        /// Constructor for .NET Core 2
        /// </summary>
        /// <param name="actionExecutingContext"></param>
        internal ActionMetricBase(ActionExecutingContext actionExecutingContext)
        {
            StartTimestamp = DateTimeOffset.Now;
            _startTicks = Stopwatch.GetTimestamp();

            var httpContext = actionExecutingContext.HttpContext;
            SessionId = httpContext.GetSessionId();
            AgentSessionId = httpContext.GetAgentSessionId();

            HttpMethod = httpContext.Request.Method;

            Parameters = StringifyParameterNames(actionExecutingContext.ActionDescriptor.Parameters);
        }

        private static string StringifyParameterNames(IList<ParameterDescriptor> actionDescriptorParameters)
        {
            new DefaultObjectPoolProvider().Create<StringBuilder>(new StringBuilderPooledObjectPolicy());
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

            var builder = new StringBuilder();

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
        public DateTimeOffset StartTimestamp { get; }

        /// <summary>
        /// The duration the request has been running
        /// </summary>
        /// <remarks>Once the request has been told to record it the timer duration will stop increasing.</remarks>
        [EventMetricValue("totalDuration", SummaryFunction.Average, "ms", Caption = "Total Request Duration", 
            Description = "The entire time spent processing the request, excluding time to return the response", IsDefaultValue = true)]
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// The duration the Action took to execute
        /// </summary>
        /// <remarks>The action is typically the controller or page's internal code.</remarks>
        [EventMetricValue("actionDuration", SummaryFunction.Average, "ms", Caption = "Action Duration",
            Description = "The time spent in the Action or Page")]
        public TimeSpan? ActionDuration { get; private set; }

        /// <summary>
        /// The time it took for the request to be authorized
        /// </summary>
        [EventMetricValue("authorizeRequestDuration", SummaryFunction.Average, "ms", Caption = "Authorize Request Duration",
            Description = "The time it took for the request to be authorized")]
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
        public string Parameters { get; }

        /// <summary>
        /// The Http Response Code for the request.
        /// </summary>
        [EventMetricValue("responseCode", SummaryFunction.Count, null, Caption = "Response Code", Description = "The Http response code for this request")]
        public int ResponseCode { get; set; }

        /// <summary>
        /// The user name for the action being performed.
        /// </summary>
        [EventMetricValue("userName", SummaryFunction.Count, null, Caption = "User", Description = "The user associated with the action being performed")]
        public string? UserName { get; set; }

        /// <summary>
        /// The exception, if any, thrown at the completion of the routine
        /// </summary>
        [EventMetricValue("exception", SummaryFunction.Count, null, Caption = "Exception", Description = "The exception, if any, thrown at the completion of the routine")]
        public Exception? Exception { get; set; }

        /// <summary>
        /// Id to identify a user session from the web browser
        /// </summary>
        [EventMetricValue("SessionId", SummaryFunction.Count, null, Caption = "Session Id", Description = "Session Id associated with action being performed")]
        public string? SessionId { get; }

        /// <summary>
        /// Id from JavaScript agent for session
        /// </summary>
        [EventMetricValue("AgentSessionId", SummaryFunction.Count, null, Caption = "Agent Session Id", Description = "Id from JavaScript agent for session")]
        public string? AgentSessionId { get; }

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

        public void Record(HttpContext context)
        {
            var response = context.Response;
            ResponseCode = response.StatusCode;
            EventMetric.Write(this);
        }

        /// <inheritdoc />
        public string? MethodName { get; set; }

        /// <inheritdoc />
        public string? ClassName { get; set; }

        /// <inheritdoc />
        public string? FileName { get; set; }

        /// <inheritdoc />
        public int LineNumber { get; set; }

    }
}