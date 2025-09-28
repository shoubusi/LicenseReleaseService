using System;

namespace Microsoft.Extensions.Logging
{
    public static class NullLogger
    {
        public static ILogger Instance { get; } = new NullLoggerImplementation();

        private class NullLoggerImplementation : ILogger
        {
            public System.IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return false;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception exception, System.Func<TState, System.Exception, string> formatter)
            {
                // Do nothing
            }
        }
    }

    public static class NullLoggerFactory
    {
        public static ILoggerFactory Instance { get; } = new NullLoggerFactoryImplementation();

        private class NullLoggerFactoryImplementation : ILoggerFactory
        {
            public ILogger CreateLogger(string categoryName)
            {
                return NullLogger.Instance;
            }

            public void AddProvider(ILoggerProvider provider)
            {
                // Do nothing
            }

            public void Dispose()
            {
                // Do nothing
            }
        }
    }
}