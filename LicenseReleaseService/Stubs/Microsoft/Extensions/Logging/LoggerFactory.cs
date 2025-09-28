using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.Logging
{
    public class LoggerFactory : ILoggerFactory
    {
        private readonly List<ILoggerProvider> _providers = new List<ILoggerProvider>();

        public ILogger CreateLogger(string categoryName)
        {
            return new Logger(categoryName, _providers);
        }

        public ILogger<T> CreateLogger<T>()
        {
            return new Logger<T>(typeof(T).FullName, _providers);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            if (provider != null && !_providers.Contains(provider))
            {
                _providers.Add(provider);
            }
        }

        public void Dispose()
        {
            foreach (var provider in _providers)
            {
                try
                {
                    provider.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _providers.Clear();
        }

        private class Logger : ILogger
        {
            private readonly string _categoryName;
            private readonly List<ILoggerProvider> _providers;

            public Logger(string categoryName, List<ILoggerProvider> providers)
            {
                _categoryName = categoryName;
                _providers = providers;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null; // Simple stub implementation
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true; // Enable all logging
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                // Simple stub implementation - write to console
                if (formatter != null)
                {
                    var message = formatter(state, exception);
                    Console.WriteLine($"[{logLevel}] {_categoryName}: {message}");
                    if (exception != null)
                    {
                        Console.WriteLine($"Exception: {exception}");
                    }
                }
            }
        }

        private class Logger<T> : ILogger<T>
        {
            private readonly ILogger _logger;

            public Logger(string categoryName, List<ILoggerProvider> providers)
            {
                _logger = new Logger(categoryName, providers);
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return _logger.BeginScope(state);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return _logger.IsEnabled(logLevel);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}