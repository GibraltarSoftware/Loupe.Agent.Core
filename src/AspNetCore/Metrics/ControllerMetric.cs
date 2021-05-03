using Gibraltar.Agent.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Tracking metric for an ASP.NET controller request
    /// </summary>
    [EventMetric("Loupe", "Web Site.Requests", "Controller Action", Caption = "Controller Action", 
        Description = "Performance data for every controller call in the application")]
    public class ControllerMetric : ActionMetricBase
    {
        /// <summary>
        /// Constructor for .NET Core 3 and later
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="controllerDescriptor"></param>
        internal ControllerMetric(HttpContext httpContext, ControllerActionDescriptor controllerDescriptor)
            :base(httpContext, controllerDescriptor)
        {
            ControllerName = controllerDescriptor.ControllerName;
            ActionName = controllerDescriptor.ActionName;
            ClassName = controllerDescriptor.ControllerTypeInfo?.FullName;
            MethodName = controllerDescriptor.MethodInfo?.Name;
            Request = string.Format("{0}:{1}", ControllerName, ActionName);
        }

        /// <summary>
        /// Constructor for .NET Core 2
        /// </summary>
        /// <param name="actionExecutingContext"></param>
        internal ControllerMetric(ActionExecutingContext actionExecutingContext)
            :base(actionExecutingContext)
        {
            if (actionExecutingContext.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
            {
                ControllerName = controllerActionDescriptor.ControllerName;
                ActionName = controllerActionDescriptor.ActionName;
                ClassName = controllerActionDescriptor.ControllerTypeInfo.FullName;
                MethodName = controllerActionDescriptor.MethodInfo?.Name;
            }
        }

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
        public string? TypeFullName => ClassName;
    }
}