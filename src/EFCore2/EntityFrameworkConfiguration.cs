using System;
using System.Collections.Generic;
using System.Text;
using Gibraltar.Agent;

namespace Loupe.Agent.EntityFrameworkCore
{
    public class EntityFrameworkConfiguration
    {
        /// <summary>
        /// The root log category for this agent
        /// </summary>
        internal const string LogCategory = "Data Access";

        public EntityFrameworkConfiguration()
        {
            Enabled = true;
            LogCallStack = false;
            QueryMessageSeverity = LogMessageSeverity.Verbose;
            LogExceptions = true;
            ExceptionSeverity = LogMessageSeverity.Error;
        }

        /// <summary>
        /// Determines if any agent functionality should be enabled.  Defaults to true.
        /// </summary>
        /// <remarks>To disable the entire agent set this option to false.  Even if individual
        /// options are enabled they will be ignored if this is set to false.</remarks>
        public bool Enabled { get; set; }

        /// <summary>
        /// Determines if the call stack for each operation should be recorded
        /// </summary>
        /// <remarks>This is useful for determining what application code causes each query</remarks>
        public bool LogCallStack { get; set; }

        /// <summary>
        /// The severity used for log messages for the Entity Framework trace message. Defaults to Verbose.
        /// </summary>
        public LogMessageSeverity QueryMessageSeverity { get; set; }

        /// <summary>
        /// Determines if a log message is written for exceptions during entity framework operations. Defaults to true.
        /// </summary>
        public bool LogExceptions { get; set; }

        /// <summary>
        /// The severity used for log messages for entity framework operations that throw an exception. Defaults to Error.
        /// </summary>
        public LogMessageSeverity ExceptionSeverity { get; set; }
    }
}
