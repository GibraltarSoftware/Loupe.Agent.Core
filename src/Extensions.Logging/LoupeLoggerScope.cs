using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace Loupe.Extensions.Logging
{
    /// <summary>
    /// Enclosing Scope for Loupe Logger.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class LoupeLoggerScope : IDisposable
    {
        private readonly LoupeLoggerProvider _provider;
        private readonly object _state;
        private bool _disposed;
        private readonly LoupeLoggerScope _parent;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoupeLoggerScope"/> class.
        /// </summary>
        /// <param name="provider">The <see cref="LoupeLoggerProvider"/>.</param>
        /// <param name="state">The state.</param>
        public LoupeLoggerScope(LoupeLoggerProvider provider, object state)
        {
            _provider = provider;
            _state = state;
            
            // Replace the current scope
            _parent = provider.CurrentScope;
            provider.CurrentScope = this;
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Scopes might be disposed out of order, if we're not in the current scope then just ignore it
            for (var scope = _provider.CurrentScope; scope != null; scope = scope._parent)
            {
                if (ReferenceEquals(scope, this))
                {
                    _provider.CurrentScope = _parent;
                }
            }
        }

        internal void Enrich(Utf8JsonWriter writer, HashSet<string> propertySet)
        {
            if (_state is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                LoupeLogEnricher.Write(writer, propertySet, pairs);
            }
            
            _parent?.Enrich(writer, propertySet);
        }
    }
}