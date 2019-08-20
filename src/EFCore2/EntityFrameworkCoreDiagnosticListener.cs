using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Loupe.Agent;
using Loupe.Agent.Core.Services;
using Loupe.Logging;
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
    internal class EntityFrameworkCoreDiagnosticListener : ILoupeDiagnosticListener, IObserver<KeyValuePair<string, object>>
    {
        private readonly CommandMetricFactory _commandMetricFactory;
        private readonly ConnectionMetricFactory _connectionMetricFactory;
        private readonly ConcurrentDictionary<Guid, CommandMetric> _commands = new ConcurrentDictionary<Guid, CommandMetric>();
        private readonly ConcurrentDictionary<Guid, ConnectionMetric> _openings = new ConcurrentDictionary<Guid, ConnectionMetric>();
        private readonly ConcurrentDictionary<Guid, ConnectionMetric> _closings = new ConcurrentDictionary<Guid, ConnectionMetric>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityFrameworkCoreDiagnosticListener"/> class.
        /// </summary>
        /// <param name="agent">The Loupe agent.</param>
        public EntityFrameworkCoreDiagnosticListener(LoupeAgent agent)
        {
            _commandMetricFactory = new CommandMetricFactory(agent.ApplicationName);
            _connectionMetricFactory = new ConnectionMetricFactory(agent.ApplicationName);
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
                case ConnectionErrorEventData eventData:
                    HandleConnectionError(value.Key, eventData);
                    break;
                case ConnectionEndEventData eventData:
                    HandleConnectionEnd(value.Key, eventData);
                    break;
                case ConnectionEventData eventData:
                    HandleConnection(value.Key, eventData);
                    break;
            }
        }

        private void HandleConnectionError(string name, ConnectionErrorEventData eventData)
        {
            if (TryGetConnectionMetric(name, eventData.ConnectionId, out var metric))
            {
                metric.Stop(eventData);
            }
        }

        private void HandleConnectionEnd(string name, ConnectionEndEventData eventData)
        {
            if (TryGetConnectionMetric(name, eventData.ConnectionId, out var metric))
            {
                metric.Stop(eventData);
            }
        }

        private bool TryGetConnectionMetric(string name, Guid connectionId, out ConnectionMetric metric)
        {
            if (name == RelationalEventId.ConnectionOpened.Name)
            {
                return _openings.TryRemove(connectionId, out metric);
            }
            if (name == RelationalEventId.ConnectionClosed.Name)
            {
                return _closings.TryRemove(connectionId, out metric);
            }
            metric = default;
            return false;
        }

        private void HandleConnection(string name, ConnectionEventData eventData)
        {
            ConnectionMetric metric;
            if (name == RelationalEventId.ConnectionOpening.Name)
            {
                metric = _connectionMetricFactory.Opening(eventData);
            }
            else if (name == RelationalEventId.ConnectionClosing.Name)
            {
                metric = _connectionMetricFactory.Closing(eventData);
            }
            else
            {
                return;
            }
            _openings[eventData.ConnectionId] = metric;
        }

        private void HandleDataReaderDisposing(DataReaderDisposingEventData eventData)
        {
            if (_commands.TryRemove(eventData.CommandId, out var commandMetric))
            {
                commandMetric.Stop(eventData);
            }
        }

        private void HandleCommandError(CommandErrorEventData eventData)
        {
            if (_commands.TryRemove(eventData.CommandId, out var commandMetric))
            {
                commandMetric.Stop(eventData);
            }
        }

        private void HandleCommandEnd(CommandEndEventData eventData)
        {
            if (_commands.TryRemove(eventData.CommandId, out var commandMetric))
            {
                commandMetric.Stop(eventData);
            }
        }

        private void HandleCommandExecuted(CommandExecutedEventData eventData)
        {
            if (eventData.Result is RelationalDataReader) return;
            if (_commands.TryRemove(eventData.CommandId, out var commandMetric))
            {
                commandMetric.Stop(eventData);
            }
        }

        private void HandleCommand(CommandEventData eventData)
        {
            _commands[eventData.CommandId] = _commandMetricFactory.Start(eventData);
        }
    }
}