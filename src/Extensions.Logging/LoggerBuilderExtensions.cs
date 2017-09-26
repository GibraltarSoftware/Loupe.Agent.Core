using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#if(NETCOREAPP2_0)
namespace Loupe.Extensions.Logging
{
    public static class LoggerBuilderExtensions
    {
        public static ILoggingBuilder AddLoupe(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, LoupeLoggerProvider>();
            return builder;
        }
    }
}
#endif