#if NETCOREAPP3_0_OR_GREATER
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Gibraltar.Agent.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.ObjectPool;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Tracking metric for an ASP.NET controller request
    /// </summary>
    [EventMetric("Loupe", "Web Site.Requests", "Page Action", Caption = "Page Action", 
        Description = "Performance data for every page call in the application")]
    public class PageMetric : ActionMetricBase
    {
        /// <summary>
        /// Constructor for .NET Core 3 and later
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="pageDescriptor"></param>
        internal PageMetric(HttpContext httpContext, PageActionDescriptor pageDescriptor)
            : base(httpContext, pageDescriptor)
        {
            Path = pageDescriptor.RelativePath;
            AreaName = pageDescriptor.AreaName;
            Request = pageDescriptor.RelativePath;
            DisplayName = pageDescriptor.DisplayName;
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
        /// Gets/Sets the name of the controller this action belongs to
        /// </summary>
        [EventMetricValue("path", SummaryFunction.Count, null, Caption = "Path", Description = "The relative path from the application root for the page")]
        public string Path { get; }

        /// <summary>
        /// The area name for this page.
        /// </summary>
        [EventMetricValue("areaName", SummaryFunction.Count, null, Caption = "Area", Description = "The area name for this page")]
        public string? AreaName { get; }

        /// <summary>
        /// The area name for this page.
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
