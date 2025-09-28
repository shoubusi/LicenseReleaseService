namespace Microsoft.Extensions.Logging
{
    // ILoggerFactory interface stub for compilation when Microsoft.Extensions.Logging.Abstractions is not available
    public interface ILoggerFactory
    {
        ILogger CreateLogger(string categoryName);
        void AddProvider(ILoggerProvider provider);
        void Dispose();
    }

    public interface ILoggerProvider : System.IDisposable
    {
        ILogger CreateLogger(string categoryName);
    }
}