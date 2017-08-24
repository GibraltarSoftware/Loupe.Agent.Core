using Microsoft.Extensions.Logging;

namespace Loupe.Extensions.Logging
{
    public class LoupeLoggerProvider : ILoggerProvider
    {
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName) => new LoupeLogger(categoryName);
    }
}
