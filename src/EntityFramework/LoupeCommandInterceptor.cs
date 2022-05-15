using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;
using System.Text;
using Gibraltar.Agent;
using Loupe.Agent.EntityFramework.Internal;

namespace Loupe.Agent.EntityFramework
{
    /// <summary>
    /// Records performance and diagnostic information for Entity Framework
    /// </summary>
    /// <remarks>
    /// 	<para>This class extends Entity Framework 6 and later to automatically capture details of each database operation performed along with performance metrics so you
    /// can identify the most frequent, slowest, and most important queries.</para>
    /// </remarks>
    /// <example>
    /// 	<para>To activate the agent it isn't enough to simply deploy it with your project, you need to make one call to register it with Entity Framework. The call can be
    /// made multiple times safely (and without causing a double registration). Once registered with Entity Framework it will automatically record information for
    /// every EF 6.0 context in the application domain.</para>
    /// 	<code title="Registering the Interceptor" description="" lang="CS">
    /// //To register the Interceptor with Entity Framework call this method at least once
    /// Gibraltar.Agent.EntityFramework.LoupeCommandInterceptor.Register();
    ///  
    /// //You can choose to override the configuration from its defaults or the web.config
    /// var config = new Gibraltar.Agent.EntityFramework.Configuration.EntityFrameworkElement();
    /// config.LogCallStacks = true; //so you know what code is causing each operation
    /// Gibraltar.Agent.EntityFramework.LoupeCommandInterceptor.Register(config);</code>
    /// 	<code title="App.config configuration of the Agent" description="You can adjust the configuration of the agent, including disabling it, by merging the following with your application configuration file which adds the entityFramework configuration element." lang="XML">
    /// &lt;?xml version="1.0" encoding="utf-8"?&gt;
    /// &lt;configuration&gt;
    ///   &lt;configSections&gt;
    ///     &lt;sectionGroup name="gibraltar"&gt;
    ///       &lt;section name="entityFramework" type="Gibraltar.Agent.EntityFramework.Configuration.EntityFrameworkElement, Gibraltar.Agent.EntityFramework" /&gt;
    ///     &lt;/sectionGroup&gt;
    ///   &lt;/configSections&gt;
    ///   &lt;gibraltar&gt;
    ///     &lt;!-- See the properties on the EntityFrameworkElement object for the options --&gt;
    ///     &lt;entityFramework logCallStack="true" /&gt;
    ///   &lt;/gibraltar&gt;
    /// &lt;/configuration&gt;</code>
    /// </example>
    public class LoupeCommandInterceptor : IDbCommandInterceptor
    {
        private const string LogSystem = "Gibraltar";
        private const string LogCategory = EntityFrameworkConfiguration.LogCategory + ".Query";

        private static readonly object s_Lock = new object();
        private static bool s_IsRegistered = false; //PROTECTED BY LOCK

        private readonly ConcurrentDictionary<int, DatabaseMetric> _databaseMetrics = new ConcurrentDictionary<int, DatabaseMetric>();

        private readonly EntityFrameworkConfiguration _configuration;

        /// <summary>
        /// Create a new Entity Framework command interceptor using the provided configuration settings
        /// </summary>
        /// <param name="configuration"></param>
        private LoupeCommandInterceptor(EntityFrameworkConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _configuration = configuration;

            LogCallStack = _configuration.LogCallStack;
            LogExceptions = _configuration.LogExceptions;
        }

        /// <summary>
        /// Register the Loupe Command Interceptor with Entity Framework (safe to call multiple times)
        /// </summary>
        public static void Register(EntityFrameworkConfiguration? configuration = null)
        {
            lock(s_Lock)
            {
                if (s_IsRegistered)
                    return;

                s_IsRegistered = true;

                var effectiveConfiguration = configuration ?? new EntityFrameworkConfiguration();

                if (effectiveConfiguration.Enabled)
                    DbInterception.Add(new LoupeCommandInterceptor(effectiveConfiguration));
            }
        }

        /// <summary>
        /// Indicates if the call stack to the operation should be included in the log message
        /// </summary>
        public bool LogCallStack { get; set; }

        /// <summary>
        /// Indicates if execution exceptions should be logged
        /// </summary>
        public bool LogExceptions { get; set; }

        /// <summary>
        /// This method is called before a call to <see cref="M:System.Data.Common.DbCommand.ExecuteNonQuery"/> or
        ///                 one of its async counterparts is made.
        /// </summary>
        /// <param name="command">The command being executed.</param><param name="interceptionContext">Contextual information associated with the call.</param>
        public void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            StartTrackingCommand(command, interceptionContext);
        }

        /// <summary>
        /// This method is called after a call to <see cref="M:System.Data.Common.DbCommand.ExecuteNonQuery"/>  or
        ///                 one of its async counterparts is made. This method should return the given result.
        ///                 However, the result used by Entity Framework can be changed by returning a different value.
        /// </summary>
        /// <remarks>
        /// For async operations this method is not called until after the async task has completed
        ///                 or failed.
        /// </remarks>
        /// <param name="command">The command being executed.</param><param name="interceptionContext">Contextual information associated with the call.</param>
        public void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            StopTrackingCommand(command, interceptionContext, interceptionContext.Result);
        }

        /// <summary>
        /// This method is called before a call to <see cref="M:System.Data.Common.DbCommand.ExecuteReader(System.Data.CommandBehavior)"/>  or
        ///                 one of its async counterparts is made.
        /// </summary>
        /// <param name="command">The command being executed.</param><param name="interceptionContext">Contextual information associated with the call.</param>
        public void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            StartTrackingCommand(command, interceptionContext);
        }

        /// <summary>
        /// This method is called after a call to <see cref="M:System.Data.Common.DbCommand.ExecuteReader(System.Data.CommandBehavior)"/>  or
        ///                 one of its async counterparts is made. This method should return the given result. However, the
        ///                 result used by Entity Framework can be changed by returning a different value.
        /// </summary>
        /// <remarks>
        /// For async operations this method is not called until after the async task has completed
        ///                 or failed.
        /// </remarks>
        /// <param name="command">The command being executed.</param><param name="interceptionContext">Contextual information associated with the call.</param>
        public void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            StopTrackingCommand(command, interceptionContext, null);
        }

        /// <summary>
        /// This method is called before a call to <see cref="M:System.Data.Common.DbCommand.ExecuteScalar"/>  or
        ///                 one of its async counterparts is made.
        /// </summary>
        /// <param name="command">The command being executed.</param><param name="interceptionContext">Contextual information associated with the call.</param>
        public void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            StartTrackingCommand(command, interceptionContext);
        }

        /// <summary>
        /// This method is called after a call to <see cref="M:System.Data.Common.DbCommand.ExecuteScalar"/>  or
        ///                 one of its async counterparts is made. This method should return the given result.
        ///                 However, the result used by Entity Framework can be changed by returning a different value.
        /// </summary>
        /// <remarks>
        /// For async operations this method is not called until after the async task has completed
        ///                 or failed.
        /// </remarks>
        /// <param name="command">The command being executed.</param><param name="interceptionContext">Contextual information associated with the call.</param>
        public void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            StopTrackingCommand(command, interceptionContext, null);
        }

        /// <summary>
        /// This method is unused at this time until we can determine a good way to detect the first moment we are working with a specific save changes sequence
        /// </summary>
        /// <param name="context"></param>
        private void StartTrackingContext(DbCommandInterceptionContext context)
        {
            foreach (var dbContext in context.DbContexts)
            {
                StringBuilder messageBuilder = new StringBuilder();

                var addedEntities = dbContext.ChangeTracker.Entries().Where(changeEntry => changeEntry.State == EntityState.Added).ToList();
                if (addedEntities.Count > 0)
                {
                    messageBuilder.AppendLine("Added Entities:");

                    foreach (var dbEntityEntry in addedEntities)
                    {
                        messageBuilder.AppendFormat("     {0}\r\n", dbEntityEntry.Entity.GetType());
                    }

                    messageBuilder.AppendLine();
                }

                var changedEntities = dbContext.ChangeTracker.Entries().Where(changeEntry => changeEntry.State == EntityState.Deleted).ToList();
                if (changedEntities.Count > 0)
                {
                    messageBuilder.AppendLine("Changed Entities:");

                    foreach (var dbEntityEntry in changedEntities)
                    {
                        messageBuilder.AppendFormat("     {0}\r\n", dbEntityEntry.Entity.GetType());
                    }

                    messageBuilder.AppendLine();
                }

                var removedEntities = dbContext.ChangeTracker.Entries().Where(changeEntry => changeEntry.State == EntityState.Modified).ToList();
                if (removedEntities.Count > 0)
                {
                    messageBuilder.AppendLine("Changed Entities:");

                    foreach (var dbEntityEntry in removedEntities)
                    {
                        messageBuilder.AppendFormat("     {0}\r\n", dbEntityEntry.Entity.GetType());
                    }

                    messageBuilder.AppendLine();
                }
            }
        }

        private void StartTrackingCommand(DbCommand command, DbCommandInterceptionContext context)
        {
            if (command == null)
                return;

            try
            {
                var messageBuilder = new StringBuilder(1024);

                string caption, shortenedQuery;
                if (command.CommandType == CommandType.StoredProcedure)
                {
                    shortenedQuery = command.CommandText;
                    caption = string.Format("Executing Procedure '{0}'", shortenedQuery);
                }
                else
                {
                    //we want to make a more compact version of the SQL Query for the caption...
                    var queryLines = command.CommandText.Split(new[] {'\r', '\n'});

                    //now rip out any leading/trailing white space...
                    var cleanedUpLines = new List<string>(queryLines.Length);
                    foreach (var queryLine in queryLines)
                    {
                        if (string.IsNullOrWhiteSpace(queryLine) == false)
                        {
                            string minimizedLine = queryLine.Trim();

                            if (string.IsNullOrWhiteSpace(minimizedLine) == false)
                            {
                                cleanedUpLines.Add(minimizedLine);
                            }
                        }
                    }

                    //and rejoin to make the shortened command.
                    shortenedQuery = string.Join(" ", cleanedUpLines);
                    if (shortenedQuery.Length > 512)
                    {
                        shortenedQuery = shortenedQuery.Substring(0, 512) + "(...)";
                        messageBuilder.AppendFormat("Full Query:\r\n\r\n{0}\r\n\r\n", command.CommandText);
                    }
                    caption = string.Format("Executing Sql: '{0}'", shortenedQuery);
                }

                string paramString = null;
                if (command.Parameters.Count > 0)
                {
                    messageBuilder.AppendLine("Parameters:");
 
                    var paramStringBuilder = new StringBuilder(1024);
                    foreach (DbParameter parameter in command.Parameters)
                    {
                        string value = parameter.Value.FormatDbValue();
                        messageBuilder.AppendFormat("    {0}: {1}\r\n", parameter.ParameterName, value);
                        paramStringBuilder.AppendFormat("{0}: {1}, ", parameter.ParameterName, value);
                    }

                    paramString = paramStringBuilder.ToString();
                    paramString = paramString.Substring(0, paramString.Length - 2); //get rid of the trailing comma

                    messageBuilder.AppendLine();
                }

                var trackingMetric = new DatabaseMetric(shortenedQuery, command.CommandText);
                trackingMetric.Parameters = paramString;

                if (command.Transaction != null)
                {
                    messageBuilder.AppendFormat("Transaction:\r\n    Id: {0:X}\r\n    Isolation Level: {1}\r\n\r\n", command.Transaction.GetHashCode(), command.Transaction.IsolationLevel);
                }

                var connection = command.Connection;
                if (connection != null)
                {
                    trackingMetric.Server = connection.DataSource;
                    trackingMetric.Database = connection.Database;
                    messageBuilder.AppendFormat("Server:\r\n    DataSource: {3}\r\n    Database: {4}\r\n    Connection Timeout: {2:N0} Seconds\r\n    Provider: {0}\r\n    Server Version: {1}\r\n\r\n",
                                                connection.GetType(), connection.ServerVersion, connection.ConnectionTimeout, connection.DataSource, connection.Database);
                }

                var messageSourceProvider = new MessageSourceProvider(2); //It's a minimum of two frames to our caller.
                if (LogCallStack)
                {
                    messageBuilder.AppendFormat("Call Stack:\r\n{0}\r\n\r\n", messageSourceProvider.StackTrace);
                }

                Log.Write(_configuration.QueryMessageSeverity, LogSystem, messageSourceProvider, null, null, LogWriteMode.Queued, null, LogCategory, caption,
                          messageBuilder.ToString());

                trackingMetric.MessageSourceProvider = messageSourceProvider;

                //we have to stuff the tracking metric in our index so that we can update it on the flipside.
                try
                {
                    _databaseMetrics[command.GetHashCode()] = trackingMetric;
                }
                catch (Exception ex)
                {
#if DEBUG
                    Log.Error(ex, LogCategory, "Unable to set database tracking metric for command due to " + ex.GetType(), "While storing the database metric for the current operation a {0} was thrown so it's unpredictable what will be recorded at the end of the operation.\r\n{1}", ex.GetType(), ex.Message);
#endif
                    GC.KeepAlive(ex);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Log.Error(ex, LogCategory, "Unable to record Entity Framework event due to " + ex.GetType(), "While calculating the log message for this event a {0} was thrown so we are unable to record the event.\r\n{1}", ex.GetType(), ex.Message);
#endif
                GC.KeepAlive(ex);
            }
        }

        private void StopTrackingCommand<T>(DbCommand command, DbCommandInterceptionContext<T> context, int? result)
        {
            string paramString = null;

            //see if we have a tracking metric for this command...
            DatabaseMetric trackingMetric;
            _databaseMetrics.TryRemove(command.GetHashCode(), out trackingMetric);
            if (trackingMetric != null)
            {
                trackingMetric.Stop();
                paramString = trackingMetric.Parameters;

                if (result != null)
                {
                    trackingMetric.Rows = result.Value;
                }
            }

            if (context.Exception != null)
            {
                if (trackingMetric != null)
                {
                    trackingMetric.Result = context.Exception.ToString();
                }

                if (LogExceptions)
                {
                    var shortenedCaption = (trackingMetric == null) ? command.CommandText : trackingMetric.ShortenedQuery;
                    var server = (trackingMetric == null) ? (command.Connection == null) ? "(unknown)" : command.Connection.DataSource
                                     : trackingMetric.Server;

                    var database = (trackingMetric == null) ? (command.Connection == null) ? "(unknown)" : command.Connection.Database
                                     : trackingMetric.Database;

                    var messageSourceProvider = (trackingMetric == null) ? new MessageSourceProvider(2) : trackingMetric.MessageSourceProvider; 

                    if (shortenedCaption.Length < command.CommandText.Length)
                    {
                        Log.Write(_configuration.ExceptionSeverity, LogSystem, messageSourceProvider, null, context.Exception, LogWriteMode.Queued, null, LogCategory, 
                            "Database Call failed due to " + context.Exception.GetType() + ": " + shortenedCaption,
                                  "Exception: {2}\r\n\r\nFull Query:\r\n\r\n{0}\r\n\r\nParameters: {1}\r\n\r\nServer:\r\n    DataSource: {3}\r\n    Database: {4}\r\n",
                                  command.CommandText, paramString ?? "(none)", context.Exception.Message, server, database);
                    }
                    else
                    {
                        Log.Write(_configuration.ExceptionSeverity, LogSystem, messageSourceProvider, null, context.Exception, LogWriteMode.Queued, null, LogCategory,
                            "Database Call failed due to " + context.Exception.GetType() + ": " + shortenedCaption,
                                  "Exception: {1}\r\n\r\nParameters: {0}\r\n\r\nServer:\r\n    DataSource: {2}\r\n    Database: {3}\r\n",
                                  paramString ?? "(none)", context.Exception.Message, server, database);
                    }
                }
            }

            if (trackingMetric != null)
            {
                trackingMetric.Record();
            }
        }
    }
}
