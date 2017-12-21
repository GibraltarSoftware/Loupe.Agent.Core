using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Agent.AspNetCore
{
    public sealed class LoupeDiagnosticListener : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly List<ILoupeDiagnosticListener> _listeners = new List<ILoupeDiagnosticListener>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public void Add(ILoupeDiagnosticListener listener)
        {
            _listeners.Add(listener);
        }

        public void Subscribe()
        {
            DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener value)
        {
            foreach (var listener in _listeners)
            {
                if (listener.Name != value.Name) continue;
                if (listener is IObserver<KeyValuePair<string, object>> observer)
                {
                    _subscriptions.Add(value.Subscribe(observer));
                }
                else
                {
                    value.SubscribeWithAdapter(listener);
                }
            }
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }

    public interface ILoupeDiagnosticListener
    {
        string Name { get; }
    }

}