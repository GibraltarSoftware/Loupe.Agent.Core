using Microsoft.Extensions.Logging;

namespace Gibraltar
{
    public static class ApplicationLogging
    {
        public static ILoggerFactory LoggerFactory { get; } = new LoggerFactory();

        public static ILogger CreateLogger<T>() =>  LoggerFactory.CreateLogger<T>();
    }
}
