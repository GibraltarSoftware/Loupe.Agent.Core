using Microsoft.Extensions.Logging;

namespace Loupe.Extensions.Logging
{
    /// <summary>
    /// Extension methods for <see cref="ILoggerFactory"/>.
    /// </summary>
    public static class LoggerFactoryExtension
    {
        public static ILoggerFactory AddLoupe(this ILoggerFactory factory)
        {
            factory.AddProvider(new LoupeLoggerProvider());
            return factory;
        }
    }
}