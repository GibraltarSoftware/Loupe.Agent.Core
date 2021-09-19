using System;
using System.Collections.Generic;
using System.Text;
using Loupe.Extensibility.Data;

namespace Loupe.Configuration
{
    /// <summary>
    /// Options for the Loupe Agent for ASP.NET Core
    /// </summary>
    public class AspNetConfiguration
    {
        /// <summary>
        /// Create a new AspNetConfiguration
        /// </summary>
        public AspNetConfiguration()
        {
            Enabled = true;
            LogRequests = true;
            LogRequestMetrics = true;
            LogRequestParameters = true;
            LogRequestParameterDetails = false;
            RequestMessageSeverity = LogMessageSeverity.Information;
        }

        /// <summary>
        /// Determines if any agent functionality should be enabled.  Defaults to true.
        /// </summary>
        /// <remarks>To disable the entire agent set this option to false.  Even if individual
        /// options are enabled they will be ignored if this is set to false.</remarks>
        public bool Enabled { get; set; }

        /// <summary>
        /// Determines if a log message is written for each request handled by a controller. Defaults to true.
        /// </summary>
        public bool LogRequests { get; set; }

        /// <summary>
        /// Determines if an event metric is written to Loupe for each request handled by a controller. Defaults to true.
        /// </summary>
        public bool LogRequestMetrics { get; set; }

        /// <summary>
        /// Determines if request log messages should include the parameter values used for the request. Defaults to true.
        /// </summary>
        public bool LogRequestParameters { get; set; }

        /// <summary>
        /// Determines if request log messages should include object details. Defaults to false.
        /// </summary>
        /// <remarks>This setting has no effect if LogRequestParameters is false.</remarks>
        public bool LogRequestParameterDetails { get; set; }

        /// <summary>
        /// The severity used for log messages for the start of each request handled by MVC and Web API. Defaults to Verbose.
        /// </summary>
        public LogMessageSeverity RequestMessageSeverity { get; set; }
    }
}
