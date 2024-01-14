using Gibraltar.Agent;

namespace Loupe.Agent.EntityFramework
{
    /// <summary>
    /// Loupe Agent configuration options for Entity Framework
    /// </summary>
    public class EntityFrameworkConfiguration
    {
        /// <summary>
        /// The root log category for this agent
        /// </summary>
        internal const string LogCategory = "Data Access";

        /// <summary>
        /// Create a new default configuration
        /// </summary>
        public EntityFrameworkConfiguration()
        {
            Enabled = true;
            LogQuery = true;
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
        /// Determines if the agent writes a log message for each SQL operation.  Defaults to true.
        /// </summary>
        /// <remarks>Set to false to disable writing log messages for each SQL operation before they are run.
        /// For database-heavy applications this can create a significant volume of log data, but does not
        /// affect overall application performance.</remarks>
        public bool LogQuery { get; set; }

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
