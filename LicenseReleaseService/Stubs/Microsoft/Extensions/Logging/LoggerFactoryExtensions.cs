namespace Microsoft.Extensions.Logging
{
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
    }
}