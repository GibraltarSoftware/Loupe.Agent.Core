using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.ObjectPool;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Tracking metric for an ASP.NET controller request
    /// </summary>
    [EventMetric("Loupe", "Web Site.Requests", "Controller Hit", Caption = "Controller Hit", Description = "Performance data for every call to an MVC controller or Web API controller in the application")]
    public class RequestMetric : IMessageSourceProvider
    {
        private readonly Stopwatch _timer;

        internal RequestMetric(HttpContext httpContext, ActionDescriptor actionDescriptor)
        {
            
            StartTimestamp = DateTimeOffset.Now;
            _timer = Stopwatch.StartNew();

            SessionId = httpContext.GetSessionId();
            AgentSessionId = httpContext.GetAgentSessionId();

            HttpMethod = httpContext.Request.Method;

            if (actionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
            {
                ControllerName = controllerActionDescriptor.ControllerName;
                ActionName = controllerActionDescriptor.ActionName;
                MethodName = controllerActionDescriptor.MethodInfo?.Name;
                ClassName = controllerActionDescriptor.ControllerTypeInfo?.Name;
            }

            UniqueId = actionDescriptor.Id;

            SubCategory = "MVC";

            Parameters = StringifyParameterNames(actionDescriptor.Parameters);
        }
        internal RequestMetric(ActionExecutingContext actionExecutingContext)
        {
            StartTimestamp = DateTimeOffset.Now;
            _timer = Stopwatch.StartNew();

            var httpContext = actionExecutingContext.HttpContext;
            SessionId = httpContext.GetSessionId();
            AgentSessionId = httpContext.GetAgentSessionId();

            HttpMethod = httpContext.Request.Method;

            if (actionExecutingContext.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
            {
                ControllerName = controllerActionDescriptor.ControllerName;
                ActionName = controllerActionDescriptor.ActionName;
                MethodName = controllerActionDescriptor.MethodInfo?.Name;
            }

            UniqueId = actionExecutingContext.ActionDescriptor.Id;

            SubCategory = "MVC";

            if (actionExecutingContext.Controller != null)
            {
                ClassName = actionExecutingContext.Controller.GetType().FullName;
            }

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
        public DateTimeOffset StartTimestamp { get; set; }

        /// <summary>
        /// The duration the request has been running
        /// </summary>
        /// <remarks>Once the request has been told to record it the timer duration will stop increasing.</remarks>
        [EventMetricValue("duration", SummaryFunction.Average, "ms", Caption = "Duration", Description = "The entire time spent processing the request, excluding time to return the response", IsDefaultValue = true)]
        public TimeSpan Duration { get { return _timer.Elapsed; }}

        /// <summary>
        /// The controller and action referenced by the request
        /// </summary>
        [EventMetricValue("request", SummaryFunction.Count, null, Caption = "Request", Description = "The controller and action referenced by the request")]
        public string Request { get { return string.Format("{0}:{1}", ControllerName, ActionName); } }

        /// <summary>
        /// Gets/Sets a String which indicates if the Action was an MVC or WebApi action
        /// </summary>
        [EventMetricValue("subCategory", SummaryFunction.Count, null, Caption = "Subcategory", Description = "The type of API this request came from (MVC or Web API)")]
        public string SubCategory { get; set; }

        /// <summary>
        /// Gets/Sets the name of the controller this action belongs to
        /// </summary>
        [EventMetricValue("controllerName", SummaryFunction.Count, null, Caption = "Controller", Description = "The short-form name of the controller used for the request (not the .NET class name)")]
        public string? ControllerName { get; set; }

        /// <summary>
        /// Gets/sets the name of this action
        /// </summary>
        [EventMetricValue("actionName", SummaryFunction.Count, null, Caption = "Action", Description = "The short-form name of the action used for the request (not the .NET method name)")]
        public string? ActionName { get; set; }

        /// <summary>
        /// The class name of the controller used for the request
        /// </summary>
        [EventMetricValue("controllerType", SummaryFunction.Count, null, Caption = "Controller Type", Description = "The class name of the controller used for the request")]
        public string? ControllerType => ClassName;

        /// <summary>
        /// Gets/Sets the HttpMethod (GET, POST, PUT, DELETE, etc) used for this action.
        /// </summary>
        /// <remarks>
        /// In MVC, some actions (typically an EDIT) have both definition for both GET and
        /// POST.  This value helps differentiate between those two calls
        /// </remarks>
        [EventMetricValue("httpMethod", SummaryFunction.Count, null, Caption = "Method", Description = "The HTTP Method (GET, POST, PUT, DELETE, etc) used for this action")]
        public string HttpMethod { get; set; }

        /// <summary>
        /// Gets/Sets a String that represents the parameters passed to this action
        /// </summary>
        /// <remarks></remarks>
        [EventMetricValue("parameters", SummaryFunction.Count, null, Caption = "Parameters", Description = "The list of parameters used for this action")]
        public string Parameters { get; set; }

        /// <summary>
        /// The unique Id of this controller &amp; action from the framework; MVC only.
        /// </summary>
        public string UniqueId { get; set; }

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
        [EventMetricValue("SessionId", SummaryFunction.Count, null, Caption = "SessionId", Description = "Session Id associated with action being performed")]
        public string? SessionId { get; set; }

        /// <summary>
        /// Id from JavaScript agent for session
        /// </summary>
        [EventMetricValue("AgentSessionId", SummaryFunction.Count, null, Caption = "AgentSessionId", Description = "Id from JavaScript agent for session")]
        public string? AgentSessionId { get; set; }

        /// <summary>
        /// Records the metrics for this request
        /// </summary>
        public void Record()
        {
            _timer.Stop(); 
            EventMetric.Write(this);
        }

        /// <summary>
        /// Records the metrics for this request
        /// </summary>
        /// <param name="actionExecutedContext">The <see cref="ActionExecutedContext"/> from ASP.NET Core.</param>
        public void Record(ActionExecutedContext actionExecutedContext)
        {
            if (actionExecutedContext.Exception != null)
            {
                Exception = actionExecutedContext.Exception;
            }
            Record();
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