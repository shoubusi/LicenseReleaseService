using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace LicenseReleaseService.TestInfrastructure.Logging;

/// <summary>
/// Test file logger provider for capturing test execution logs
/// </summary>
public class TestFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, TestFileLogger> _loggers = new();
    private readonly object _lock = new object();

    public TestFileLoggerProvider(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new TestFileLogger(name, _filePath, _lock));
    }

    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            logger.Dispose();
        }
        _loggers.Clear();
    }
}

/// <summary>
/// Test file logger for writing log entries to a file
/// </summary>
public class TestFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _filePath;
    private readonly object _lock;

    public TestFileLogger(string categoryName, string filePath, object @lock)
    {
        _categoryName = categoryName;
        _filePath = filePath;
        _lock = @lock;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Debug;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message))
            return;

        var logEntry = FormatLogEntry(logLevel, eventId, message, exception);

        lock (_lock)
        {
            File.AppendAllText(_filePath, logEntry + Environment.NewLine);
        }
    }

    private string FormatLogEntry(LogLevel logLevel, EventId eventId, string message, Exception? exception)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLevelString = logLevel.ToString().ToUpper();

        var builder = new StringBuilder();
        builder.Append($"{timestamp} [{logLevelString}] [{_categoryName}]");

        if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
        {
            builder.Append($" [{eventId.Id}:{eventId.Name}]");
        }

        builder.Append($" {message}");

        if (exception != null)
        {
            builder.AppendLine();
            builder.Append($"Exception: {exception.GetType().Name}: {exception.Message}");
            builder.AppendLine();
            builder.Append(exception.StackTrace);
        }

        return builder.ToString();
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}