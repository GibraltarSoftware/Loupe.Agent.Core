using System;
using System.Collections.Generic;
using System.Diagnostics;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;

namespace Loupe.Agent.Core.Services
{
    /// <summary>
    /// The master Diagnostic listener that will subscribe any registered listeners to the relevant <see cref="DiagnosticSource"/>.
    /// </summary>
    public sealed class LoupeDiagnosticListener : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly List<ILoupeDiagnosticListener> _listeners = new List<ILoupeDiagnosticListener>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <summary>
        /// Adds the specified listener.
        /// </summary>
        /// <param name="listener">The listener.</param>
        public void Add(ILoupeDiagnosticListener listener)
        {
            _listeners.Add(listener);
        }

        /// <summary>
        /// Subscribes this instance to the <see cref="DiagnosticListener.AllListeners"/> observable.
        /// </summary>
        public void Subscribe()
        {
            DiagnosticListener.AllListeners.Subscribe(this);
        }

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
        }

        /// <summary>
        /// Provides the observer with new data.
        /// </summary>
        /// <param name="value">The current notification information.</param>
        public void OnNext(DiagnosticListener value)
        {
            foreach (var listener in _listeners)
            {
                if (listener.Name != value.Name) continue;

                if (listener is IObserver<KeyValuePair<string, object>> observer)
                {
                    _subscriptions.Add(value.Subscribe(observer));
                }
#if NET461 || NETSTANDARD2_0 || NETSTANDARD2_1
                else
                {
                    value.SubscribeWithAdapter(listener);
                }
#else
                else
                {
                    //in .NET 5 we don't have the diagnostic adapter we used to.
                    Gibraltar.Monitor.Log.Write(LogMessageSeverity.Information, "Loupe", "Skipping diagnostic source that isn't observable",
                        "In .NET 5 and later we no longer have the adapter to read these.\r\n{0}", value.GetType().FullName);
                }
#endif
            }
        }

        /// <summary>
        /// Unsubscribes any existing listeners.
        /// </summary>
        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                try { subscription.Dispose(); }
                catch
                {
                    // ignored
                }
            }
        }
    }
}