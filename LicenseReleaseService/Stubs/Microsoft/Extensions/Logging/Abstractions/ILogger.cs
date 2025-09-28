namespace Microsoft.Extensions.Logging.Abstractions
{
    public interface ILogger
    {
        void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, System.Exception exception, System.Func<TState, System.Exception, string> formatter);
        bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel);
        System.IDisposable BeginScope<TState>(TState state);
    }
}