using System;

namespace Loupe.Extensions.Logging
{
    public class LoupeLoggerScope : IDisposable
    {
        private readonly object _state;

        public LoupeLoggerScope(object state)
        {
            _state = state;
        }

        public void Dispose()
        {
        }
    }
}