using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

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
        public static ILoggingBuilder AddLoupe(this ILoggingBuilder builder) => AddLoupe(builder, null);
        
        /// <summary>
        /// Adds the Loupe provider for <c>Microsoft.Extensions.Logging</c>.
        /// </summary>
        /// <param name="builder">The <see cref="ILoggingBuilder"/>.</param>
        /// <param name="globalProperties">Global properties to apply to all log messages.</param>
        /// <returns>The <see cref="ILoggingBuilder"/>.</returns>
        public static ILoggingBuilder AddLoupe(this ILoggingBuilder builder, IEnumerable<KeyValuePair<string, string>> globalProperties)
        {
            builder.Services.AddSingleton(new LoupeGlobalProperties(globalProperties));
            builder.Services.AddSingleton<ILoggerProvider, LoupeLoggerProvider>();
            return builder;
        }

        /// <summary>
        /// Adds the Loupe provider for <c>Microsoft.Extensions.Logging</c>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddLoupeLogging(this IServiceCollection services)
        {
            services.AddLogging(builder => { builder.AddLoupe(); });
            return services;
        }

        /// <summary>
        /// Adds the Loupe provider for <c>Microsoft.Extensions.Logging</c>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="globalProperties">Global properties to apply to all log messages.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddLoupeLogging(this IServiceCollection services, IEnumerable<KeyValuePair<string, string>> globalProperties)
        {
            services.AddLogging(builder => { builder.AddLoupe(globalProperties); });
            return services;
        }

#if !NETCOREAPP2_0 && !NETSTANDARD2_0
        /// <summary>
        /// Adds the Loupe provider for <c>Microsoft.Extensions.Logging</c>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/>.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder AddLoupeLogging(this IHostBuilder builder)
        {
            return builder.ConfigureLogging(loggingBuilder => loggingBuilder.AddLoupe());
        }
        
        /// <summary>
        /// Adds the Loupe provider for <c>Microsoft.Extensions.Logging</c>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/>.</param>
        /// <param name="globalProperties">Global properties to apply to all log messages.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder AddLoupeLogging(this IHostBuilder builder, IEnumerable<KeyValuePair<string, string>> globalProperties)
        {
            return builder.ConfigureLogging(loggingBuilder => loggingBuilder.AddLoupe());
        }
#endif
    }
}