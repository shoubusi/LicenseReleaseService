using System;

namespace Microsoft.Extensions.Logging
{
    // ILogger interface stub for compilation when Microsoft.Extensions.Logging.Abstractions is not available
    public interface ILogger
    {
        IDisposable BeginScope<TState>(TState state);
        bool IsEnabled(LogLevel logLevel);
        void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter);
    }

    // Generic ILogger interface stub
    public interface ILogger<T> : ILogger
    {
    }

    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6
    }

    public struct EventId
    {
        public EventId(int id, string name = null)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }
    }

    public static class LoggerExtensions
    {
        public static void LogDebug(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Debug, default(EventId), message, null, (state, ex) => state.ToString());
        }

        public static void LogDebug(this ILogger logger, string message, params object[] args)
        {
            logger.Log(LogLevel.Debug, default(EventId), message, null, (state, ex) => string.Format(state.ToString(), args));
        }

        public static void LogDebug(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Debug, default(EventId), message, exception, (state, ex) => state.ToString());
        }

        public static void LogDebug(this ILogger logger, Exception exception, string message, params object[] args)
        {
            logger.Log(LogLevel.Debug, default(EventId), message, exception, (state, ex) => string.Format(state.ToString(), args));
        }

        public static void LogInformation(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Information, default(EventId), message, null, (state, ex) => state.ToString());
        }

        public static void LogInformation(this ILogger logger, string message, params object[] args)
        {
            logger.Log(LogLevel.Information, default(EventId), message, null, (state, ex) => string.Format(state.ToString(), args));
        }

        public static void LogInformation(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Information, default(EventId), message, exception, (state, ex) => state.ToString());
        }

        public static void LogWarning(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Warning, default(EventId), message, null, (state, ex) => state.ToString());
        }

        public static void LogWarning(this ILogger logger, string message, params object[] args)
        {
            logger.Log(LogLevel.Warning, default(EventId), message, null, (state, ex) => string.Format(state.ToString(), args));
        }

        public static void LogWarning(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Warning, default(EventId), message, exception, (state, ex) => state.ToString());
        }

        public static void LogError(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Error, default(EventId), message, null, (state, ex) => state.ToString());
        }

        public static void LogError(this ILogger logger, string message, params object[] args)
        {
            logger.Log(LogLevel.Error, default(EventId), message, null, (state, ex) => string.Format(state.ToString(), args));
        }

        public static void LogError(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Error, default(EventId), message, exception, (state, ex) => state.ToString());
        }

        public static void LogError(this ILogger logger, Exception exception, string message, params object[] args)
        {
            logger.Log(LogLevel.Error, default(EventId), message, exception, (state, ex) => string.Format(state.ToString(), args));
        }

        public static void LogCritical(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Critical, default(EventId), message, null, (state, ex) => state.ToString());
        }

        public static void LogCritical(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Critical, default(EventId), message, exception, (state, ex) => state.ToString());
        }
    }
}