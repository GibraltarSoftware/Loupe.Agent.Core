#if NETCOREAPP3_0_OR_GREATER
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Gibraltar.Agent.Metrics;
using Loupe.Agent.AspNetCore.Infrastructure;
using Loupe.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.ObjectPool;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Tracking metric for an ASP.NET controller request
    /// </summary>
    [EventMetric(Constants.LogSystem, Constants.MetricCategory, "Page Action", Caption = "Page Action", 
        Description = "Performance data for every page call in the application")]
    public class PageMetric : ActionMetricBase
    {
        /// <summary>
        /// Constructor for .NET Core 3 and later
        /// </summary>
        internal PageMetric(AspNetConfiguration options,
            ObjectPool<StringBuilder> stringBuilderPool, 
            HttpContext httpContext, 
            PageActionDescriptor pageDescriptor)
            : base(options, stringBuilderPool, httpContext, pageDescriptor)
        {
            Path = pageDescriptor.RelativePath;
            AreaName = pageDescriptor.AreaName;
            Request = pageDescriptor.RelativePath;
            DisplayName = pageDescriptor.DisplayName;
        }

        /// <summary>
        /// The relative path from the application root for the page 
        /// </summary>
        [EventMetricValue("path", SummaryFunction.Count, null, Caption = "Path", Description = "The relative path from the application root for the page")]
        public string Path { get; }

        /// <summary>
        /// The area name for this page.
        /// </summary>
        [EventMetricValue("areaName", SummaryFunction.Count, null, Caption = "Area", Description = "The area name for this page")]
        public string? AreaName { get; }

        /// <summary>
        /// A display name for the page
        /// </summary>
        [EventMetricValue("displayName", SummaryFunction.Count, null, Caption = "Display Name", Description = "A display name for the page")]
        public string? DisplayName { get; } 

        /// <summary>
        /// The class name of the controller used for the request
        /// </summary>
        [EventMetricValue("type", SummaryFunction.Count, null, Caption = "Type", Description = "The class name of the page or area used for the request")]
        public string? TypeFullName => ClassName;
    }
}
#endif
