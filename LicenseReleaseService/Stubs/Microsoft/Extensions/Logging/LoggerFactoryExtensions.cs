namespace Microsoft.Extensions.Logging
{
    using Microsoft.Extensions.DependencyInjection;
    public static class LoggerFactoryExtensions
    {
        public static ILoggerFactory AddConsole(this ILoggerFactory factory)
        {
            // Stub implementation - no actual console provider
            return factory;
        }

        public static ILoggerFactory AddDebug(this ILoggerFactory factory)
        {
            // Stub implementation - no actual debug provider
            return factory;
        }

        public static ILoggingBuilder AddConsole(this ILoggingBuilder builder)
        {
            // Stub implementation - no actual console provider
            return builder;
        }

        public static ILoggingBuilder AddDebug(this ILoggingBuilder builder)
        {
            // Stub implementation - no actual debug provider
            return builder;
        }

        public static ILoggingBuilder SetMinimumLevel(this ILoggingBuilder builder, LogLevel level)
        {
            // Stub implementation - minimum level would be set here
            return builder;
        }
    }
}