using System;

namespace Loupe.Extensions.Logging
{
    /// <summary>
    /// Enclosing Scope for Loupe Logger.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class LoupeLoggerScope : IDisposable
    {
        private readonly object _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoupeLoggerScope"/> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public LoupeLoggerScope(object state)
        {
            _state = state;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }
    }
}