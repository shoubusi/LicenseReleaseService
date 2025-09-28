namespace Microsoft.Extensions.Logging
{
    public static class ILoggerExtensions
    {
        public static ILogger<T> CreateLogger<T>(this ILoggerFactory factory)
        {
            var loggerFactory = factory as LoggerFactory;
            if (loggerFactory != null)
            {
                return loggerFactory.CreateLogger<T>();
            }

            // Fallback to non-generic and cast
            return (ILogger<T>)factory.CreateLogger(typeof(T).FullName);
        }
    }
}