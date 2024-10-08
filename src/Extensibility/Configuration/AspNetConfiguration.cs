﻿using System;
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
            LogBadRequests = true;
            LogRequests = true;
            LogRequestFailures = true;
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
        /// Determines if a log message is written for each request that returns a 400-series response code.  Defaults to true.
        /// </summary>
        public bool LogBadRequests { get; set; }

        /// <summary>
        /// Determines if a log message is written for each request that returns a 500-series response code or throws an exception.  Defaults to true.
        /// </summary>
        public bool LogRequestFailures { get; set; }

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
        /// The severity used for log messages for the start of each request handled by MVC and Web API. Defaults to Information.
        /// </summary>
        public LogMessageSeverity RequestMessageSeverity { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("\tEnabled: {0}\r\n", Enabled);
            if (Enabled)
            {
                stringBuilder.AppendFormat("\tLog Requests: {0}\r\n", LogRequests);
                stringBuilder.AppendFormat("\tRequest Message Severity: {0}\r\n", RequestMessageSeverity);
                stringBuilder.AppendFormat("\tLog Bad Requests: {0}\r\n", LogBadRequests);
                stringBuilder.AppendFormat("\tLog Request Failures: {0}\r\n", LogRequestFailures);
                stringBuilder.AppendFormat("\tLog Request Metrics: {0}\r\n", LogRequestMetrics);
                stringBuilder.AppendFormat("\tLog Request Parameters: {0}\r\n", LogRequestParameters);
                stringBuilder.AppendFormat("\tLog Request Parameter Details: {0}\r\n", LogRequestParameterDetails);
            }

            return stringBuilder.ToString();
        }
    }
}
