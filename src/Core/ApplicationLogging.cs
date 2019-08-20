using Microsoft.Extensions.Logging;

namespace Loupe.Core
{
    /// <summary>
    /// Loupe application logging factory
    /// </summary>
    public static class ApplicationLogging
    {
        /// <summary>
        /// Get the Loupe Logger Factory (one per process)
        /// </summary>
        public static ILoggerFactory LoggerFactory { get; } = new LoggerFactory();

        /// <summary>
        /// Create a new logger for the requested type from the common logger factory
        /// </summary>
        public static ILogger CreateLogger<T>() =>  LoggerFactory.CreateLogger<T>();
    }
}
