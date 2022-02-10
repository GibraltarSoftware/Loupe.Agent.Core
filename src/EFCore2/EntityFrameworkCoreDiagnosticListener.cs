using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using Gibraltar.Agent;
using Gibraltar.Agent.EntityFramework.Internal;
using Gibraltar.Agent.Metrics;
using Loupe.Agent.Core.Services;
using Loupe.Agent.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using ILoupeDiagnosticListener = Loupe.Agent.Core.Services.ILoupeDiagnosticListener;

namespace Loupe.Agent.EntityFrameworkCore
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// Diagnostic listener for EF Core events.
    /// </summary>
    /// <seealso cref="Loupe.Agent.Core.Services.ILoupeDiagnosticListener" />
    public class EntityFrameworkCoreDiagnosticListener : ILoupeDiagnosticListener, IObserver<KeyValuePair<string, object>>
    {
        private const string LogSystem = "Loupe";
        private const string LogCategory = EntityFrameworkConfiguration.LogCategory + ".Query";

        private readonly EntityFrameworkConfiguration _configuration;
        private readonly ConcurrentDictionary<Guid, DatabaseMetric> _commands = new ConcurrentDictionary<Guid, DatabaseMetric>();
        private readonly ConcurrentDictionary<Guid, string> _connectionNames = new ConcurrentDictionary<Guid, string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityFrameworkCoreDiagnosticListener"/> class.
        /// </summary>
        /// <param name="agent">The Loupe agent.</param>
        /// <param name="configuration">Optional. The framework for the listener</param>
        public EntityFrameworkCoreDiagnosticListener(LoupeAgent agent, EntityFrameworkConfiguration configuration = null)
        {
            _configuration = configuration ?? new EntityFrameworkConfiguration();
            LogCallStack = _configuration.LogCallStack;
            LogExceptions = _configuration.LogExceptions;
        }

        /// <summary>
        /// Returns the name of the <see cref="T:System.Diagnostics.DiagnosticSource" /> this implementation targets.
        /// </summary>
        public string Name => "Microsoft.EntityFrameworkCore";

        /// <summary>
        /// Notifies the observer that the provider has finished sending push-based notifications.
        /// </summary>
        public void OnCompleted()
        {
        }

        /// <summary>
        /// Notifies the observer that the provider has experienced an error condition.
        /// </summary>
        /// <param name="error">An object that provides additional information about the error.</param>
        public void OnError(Exception error)
        {
            Log.Error(error, LogWriteMode.Queued, "EntityFrameworkCoreDiagnosticListener", error.Message, "LoupeDiagnosticListener");
        }

        /// <summary>
        /// Provides the observer with new data.
        /// </summary>
        /// <param name="value">The current notification information.</param>
        public void OnNext(KeyValuePair<string, object> value)
        {
            switch (value.Value)
            {
                case CommandExecutedEventData eventData:
                    HandleCommandExecuted(eventData);
                    break;
                case CommandErrorEventData eventData:
                    HandleCommandError(eventData);
                    break;
                case CommandEndEventData eventData:
                    HandleCommandEnd(eventData);
                    break;
                case CommandEventData eventData:
                    HandleCommand(eventData);
                    break;
                case DataReaderDisposingEventData eventData:
                    HandleDataReaderDisposing(eventData);
                    break;
                case ConnectionEndEventData eventData:
                    HandleConnection(value.Key, eventData);
                    break;
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

        private void HandleConnection(string name, ConnectionEndEventData eventData)
        {
            ConnectionMetric metric;
            if (name == RelationalEventId.ConnectionOpened.Name)
            {
                metric = new ConnectionMetric(eventData)
                {
                    Action = "Open",
                    ConnectionDelta = 1,
                    Duration = eventData.Duration
                };

                _connectionNames.TryAdd(eventData.ConnectionId, metric.InstanceName);
            }
            else if (name == RelationalEventId.ConnectionClosed.Name)
            {
                // *sigh*.  We've found the server names don't always match between open and close
                //so look up our cached value..
                if (_connectionNames.TryRemove(eventData.ConnectionId, out string instanceName))
                {
                    metric = new ConnectionMetric(eventData, instanceName)
                    {
                        Action = "Closed",
                        ConnectionDelta = -1,
                        Duration = eventData.Duration
                    };
                }
                else
                {
                    // we ignore the else clause because it's not a "matching" event.
                    return;
                }
            }
            else
            {
                return;
            }

            EventMetric.Write(metric);
            SampledMetric.Write(metric);
        }

        private void HandleCommand(CommandEventData eventData)
        {
            if (eventData?.Command == null)
                return;

            try
            {
                var messageBuilder = new StringBuilder(1024);
                var command = eventData.Command;

                string caption, shortenedQuery;
                if (command.CommandType == CommandType.StoredProcedure)
                {
                    shortenedQuery = command.CommandText;
                    caption = string.Format("Executing Procedure '{0}'", shortenedQuery);
                }
                else
                {
                    //we want to make a more compact version of the SQL Query for the caption...
                    var queryLines = command.CommandText.Split(new[] { '\r', '\n' });

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
                if (eventData.LogParameterValues && (command.Parameters.Count > 0))
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

                var trackingMetric = new DatabaseMetric(eventData, shortenedQuery);
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
                    _commands[eventData.CommandId] = new DatabaseMetric(eventData, shortenedQuery);
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


        private void HandleCommandError(CommandErrorEventData eventData)
        {
            if (_commands.TryRemove(eventData.CommandId, out var commandMetric))
            {
                commandMetric.Record(eventData);

                if (LogExceptions && (eventData.Exception != null))
                {
                    if (commandMetric.ShortenedQuery.Length < commandMetric.Query.Length)
                    {
                        Log.Write(_configuration.ExceptionSeverity, LogSystem, commandMetric.MessageSourceProvider, null, eventData.Exception, LogWriteMode.Queued, null, LogCategory,
                            "Database Call failed due to " + eventData.Exception.GetType() + ": " + commandMetric.ShortenedQuery,
                                  "Exception: {2}\r\n\r\nFull Query:\r\n\r\n{0}\r\n\r\nParameters: {1}\r\n\r\nServer:\r\n    DataSource: {3}\r\n    Database: {4}\r\n",
                            commandMetric.Query, commandMetric.Parameters ?? "(none)", eventData.Exception.Message, 
                            commandMetric.Server, commandMetric.Database);
                    }
                    else
                    {
                        Log.Write(_configuration.ExceptionSeverity, LogSystem, commandMetric.MessageSourceProvider, null, eventData.Exception, LogWriteMode.Queued, null, LogCategory,
                            "Database Call failed due to " + eventData.Exception.GetType() + ": " + commandMetric.ShortenedQuery,
                                  "Exception: {1}\r\n\r\nParameters: {0}\r\n\r\nServer:\r\n    DataSource: {2}\r\n    Database: {3}\r\n",
                            commandMetric.Parameters ?? "(none)", eventData.Exception.Message, commandMetric.Server, commandMetric.Database);
                    }
                }
            }
        }

        private void HandleCommandEnd(CommandEndEventData eventData)
        {
            if (_commands.TryRemove(eventData.CommandId, out var commandMetric))
            {
                commandMetric.Record(eventData);
            }
        }

        private void HandleCommandExecuted(CommandExecutedEventData eventData)
        {
            if (eventData.Result is RelationalDataReader) return;

            if (_commands.TryRemove(eventData.CommandId, out var commandMetric))
            {
                commandMetric.Record(eventData);
            }
        }

        private void HandleDataReaderDisposing(DataReaderDisposingEventData eventData)
        {
            if (_commands.TryRemove(eventData.CommandId, out var commandMetric))
            {
                commandMetric.Record(eventData);
            }
        }
    }
}