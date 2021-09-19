using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using Loupe.Agent.AspNetCore.Infrastructure;
using Loupe.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Tracking metric for an ASP.NET controller request
    /// </summary>
    [EventMetric(Constants.LogSystem, Constants.MetricCategory, "Controller Action", Caption = "Controller Action", 
        Description = "Performance data for every controller call in the application")]
    public class ControllerMetric : ActionMetricBase
    {
        internal const string LogCategory = Constants.Category + ".Controller Request";

        private readonly ObjectPool<StringBuilder> _stringBuilderPool;

        /// <summary>
        /// Constructor for .NET Core 3 and later
        /// </summary>
        internal ControllerMetric(AspNetConfiguration options, 
            ObjectPool<StringBuilder> stringBuilderPool,
            HttpContext httpContext, 
            ControllerActionDescriptor controllerDescriptor)
            :base(options, stringBuilderPool, httpContext, controllerDescriptor)
        {
            _stringBuilderPool = stringBuilderPool;
            ControllerName = controllerDescriptor.ControllerName;
            ActionName = controllerDescriptor.ActionName;
            ClassName = controllerDescriptor.ControllerTypeInfo?.FullName;
            MethodName = controllerDescriptor.MethodInfo?.Name;
            Request = string.Format("{0}:{1}", ControllerName, ActionName);
            Path = httpContext.Request.Path;
        }

        /// <summary>
        /// Constructor for .NET Core 2
        /// </summary>
        internal ControllerMetric(AspNetConfiguration options,
            ObjectPool<StringBuilder> stringBuilderPool, 
            ActionExecutingContext actionExecutingContext)
            :base(options, stringBuilderPool, actionExecutingContext)
        {
            if (actionExecutingContext.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
            {
                ControllerName = controllerActionDescriptor.ControllerName;
                ActionName = controllerActionDescriptor.ActionName;
                ClassName = controllerActionDescriptor.ControllerTypeInfo.FullName;
                MethodName = controllerActionDescriptor.MethodInfo?.Name;
            }
        }

        /// <inheritdoc />
        protected override void OnLogRequest()
        {
            var caption = string.Format("Api {0} {1} Requested", ControllerName, ActionName);

            var descriptionBuilder = _stringBuilderPool.Get();

            try
            {

                if (!string.IsNullOrWhiteSpace(SessionId))
                {
                    descriptionBuilder.AppendFormat("SessionId: {0}\r\n", SessionId);
                }

                if (!string.IsNullOrWhiteSpace(AgentSessionId))
                {
                    descriptionBuilder.AppendFormat("JS Agent SessionId: {0}\r\n", AgentSessionId);
                }

                descriptionBuilder.AppendFormat("Controller: {0}\r\n", ControllerName);
                descriptionBuilder.AppendFormat("Action: {0}\r\n", ActionName);
                if (Options.LogRequestParameters && string.IsNullOrEmpty(ParameterDetails) == false)
                {
                    descriptionBuilder.AppendLine(ParameterDetails);
                }

                descriptionBuilder.AppendFormat("Path: {0}\r\n", Path);

                string? detailsJson = null;
                try
                {
                    detailsJson = JsonSerializer.Serialize(this, JsonSerializerOptions);
                }
                catch (Exception)
                {
                }

                Log.Write((LogMessageSeverity) Options.RequestMessageSeverity, Constants.LogSystem,
                    (IMessageSourceProvider) this, null, null,
                    LogWriteMode.Queued, detailsJson, LogCategory, caption, descriptionBuilder.ToString());
            }
            finally
            {
                _stringBuilderPool.Return(descriptionBuilder);
            }
        }

        /// <inheritdoc />
        protected override void OnLogRequestCompletion()
        {
            //wait, we only log if something has gone wrong.
            if (ResponseCode < 400)
                return;

            string responseMessage = ReasonPhrases.GetReasonPhrase(ResponseCode);

            LogMessageSeverity severity;
            string caption;
            if (ResponseCode < 500)
            {
                severity = LogMessageSeverity.Warning;
                caption = string.Format("Api {0} {1} returning {2} ({3})", ControllerName, ActionName, responseMessage, ResponseCode);
            }
            else
            {
                severity = LogMessageSeverity.Error;

                if (Exception != null)
                {
                    caption = string.Format("Api {0} {1} Failed due to {4} - {2} ({3})", ControllerName, ActionName, responseMessage, ResponseCode,
                        Exception.GetBaseException().GetType().Name);
                }
                else
                {
                    caption = string.Format("Api {0} {1} Failed - {2} ({3})", ControllerName, ActionName, responseMessage, ResponseCode);
                }
            }

            var descriptionBuilder = _stringBuilderPool.Get();

            try
            {
                var reportException = Exception?.GetBaseException();
                if (reportException != null)
                {
                    descriptionBuilder.AppendFormat("{0}\r\n", reportException.Message);

                    var source = reportException.TargetSite;
                    if (source != null)
                    {
                        descriptionBuilder.AppendFormat("Thrown by {0}:{1}\r\n\r\n", source.ReflectedType?.FullName, source.Name);
                    }
                }

                if (!string.IsNullOrWhiteSpace(SessionId))
                {
                    descriptionBuilder.AppendFormat("SessionId: {0}\r\n", SessionId);
                }

                if (!string.IsNullOrWhiteSpace(AgentSessionId))
                {
                    descriptionBuilder.AppendFormat("JS Agent SessionId: {0}\r\n", AgentSessionId);
                }

                descriptionBuilder.AppendFormat("Controller: {0}\r\n", ControllerName);
                descriptionBuilder.AppendFormat("Action: {0}\r\n", ActionName);
                if (Options.LogRequestParameters && string.IsNullOrEmpty(ParameterDetails) == false)
                {
                    descriptionBuilder.AppendLine(ParameterDetails);
                }

                descriptionBuilder.AppendFormat("Path: {0}\r\n", Path);

                string? detailsJson = null;
                try
                {
                    detailsJson = JsonSerializer.Serialize(this, JsonSerializerOptions);
                }
                catch (Exception)
                {
                }

                Log.Write(severity, Constants.LogSystem,
                    (IMessageSourceProvider)this, null, Exception,
                    LogWriteMode.Queued, detailsJson, LogCategory, caption, descriptionBuilder.ToString());
            }
            finally
            {
                _stringBuilderPool.Return(descriptionBuilder);
            }
        }

        /// <summary>
        /// The relative path from the application root for the request
        /// </summary>
        [EventMetricValue("path", SummaryFunction.Count, null, Caption = "Path", Description = "The relative path from the application root for the page")]
        [JsonPropertyName("RequestPath")] //Match MEL
        public string Path { get; }

        /// <summary>
        /// Gets/Sets the name of the controller this action belongs to
        /// </summary>
        [EventMetricValue("controllerName", SummaryFunction.Count, null, Caption = "Controller", Description = "The short-form name of the controller used for the request (not the .NET class name)")]
        public string? ControllerName { get;  }

        /// <summary>
        /// Gets/sets the name of this action
        /// </summary>
        [EventMetricValue("actionName", SummaryFunction.Count, null, Caption = "Action", Description = "The short-form name of the action used for the request (not the .NET method name)")]
        public string? ActionName { get;  }

        /// <summary>
        /// The class name of the controller used for the request
        /// </summary>
        [EventMetricValue("class", SummaryFunction.Count, null, Caption = "Class", Description = "The class name of the controller used for the request")]
        [JsonIgnore]
        public string? TypeFullName => ClassName;

    }
}