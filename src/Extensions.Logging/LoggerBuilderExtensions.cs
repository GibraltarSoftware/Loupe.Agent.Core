using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#if(NETCOREAPP2_0 || NETSTANDARD2_0)
namespace Loupe.Extensions.Logging
{
    /// <summary>
    /// Extension methods for <see cref="ILoggingBuilder"/>.
    /// </summary>
    public static class LoggerBuilderExtensions
    {
        /// <summary>
        /// Adds the Loupe provider for <c>Microsoft.Extensions.Logging</c>.
        /// </summary>
        /// <param name="builder">The <see cref="ILoggingBuilder"/>.</param>
        /// <returns>The <see cref="ILoggingBuilder"/>.</returns>
        public static ILoggingBuilder AddLoupe(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, LoupeLoggerProvider>();
            return builder;
        }
    }
}
#endif