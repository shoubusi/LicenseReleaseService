using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.TestInfrastructure.Mocks;

/// <summary>
/// Factory class for creating common mock objects used in tests
/// </summary>
public static class MockFactory
{
    /// <summary>
    /// Creates a mock logger for the specified type
    /// </summary>
    /// <typeparam name="T">The type to create logger for</typeparam>
    /// <returns>Mock logger instance</returns>
    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        var mockLogger = new Mock<ILogger<T>>();

        // Setup Log method to capture all log levels
        mockLogger.Setup(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
        .Callback<LogLevel, EventId, It.IsAnyType, Exception?, Func<It.IsAnyType, Exception?, string>>(
            (level, eventId, state, exception, formatter) =>
            {
                // Optional: Add custom logging behavior here
                // For now, we'll just let the mock capture the calls
            });

        // Setup.IsEnabled to return true for all log levels
        mockLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Setup.BeginScope to return a disposable
        mockLogger.Setup(l => l.BeginScope(It.IsAny<It.IsAnyType>())).Returns(Mock.Of<IDisposable>());

        return mockLogger;
    }

    /// <summary>
    /// Creates a mock logger with verification capability
    /// </summary>
    /// <typeparam name="T">The type to create logger for</typeparam>
    /// <returns>Mock logger with verification enabled</returns>
    public static Mock<ILogger<T>> CreateVerifiableLogger<T>()
    {
        var mockLogger = CreateMockLogger<T>();

        // Make the mock verifiable
        mockLogger.Setup(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
        .Verifiable();

        return mockLogger;
    }

    /// <summary>
    /// Creates a mock process with standard behavior
    /// </summary>
    /// <param name="exitCode">Process exit code</param>
    /// <param name="standardOutput">Standard output text</param>
    /// <param name="standardError">Standard error text</param>
    /// <param name="executionTime">Time the process takes to execute</param>
    /// <returns>Mock process</returns>
    public static Mock<System.Diagnostics.Process> CreateMockProcess(
        int exitCode = 0,
        string standardOutput = "",
        string standardError = "",
        TimeSpan? executionTime = null)
    {
        var mockProcess = new Mock<System.Diagnostics.Process> { CallBase = true };

        // Setup basic process properties
        mockProcess.Setup(p => p.Id).Returns(12345);
        mockProcess.Setup(p => p.ProcessName).Returns("testprocess.exe");
        mockProcess.Setup(p => p.HasExited).Returns(true);
        mockProcess.Setup(p => p.ExitCode).Returns(exitCode);

        // Setup start info
        var mockStartInfo = new Mock<System.Diagnostics.ProcessStartInfo>();
        mockStartInfo.Setup(si => si.FileName).Returns("testprocess.exe");
        mockStartInfo.Setup(si => si.Arguments).Returns("--test");
        mockProcess.Setup(p => p.StartInfo).Returns(mockStartInfo.Object);

        return mockProcess;
    }

    /// <summary>
    /// Creates a mock configuration for testing
    /// </summary>
    /// <param name="settings">Custom settings to include</param>
    /// <returns>Mock configuration</returns>
    public static Dictionary<string, string> CreateMockConfiguration(
        Dictionary<string, string>? settings = null)
    {
        var defaultSettings = new Dictionary<string, string>
        {
            {"ServiceSettings:LicenseServerAddress", "test-server.company.com"},
            {"ServiceSettings:PollIntervalMs", "30000"},
            {"ServiceSettings:HealthCheckIntervalMs", "60000"},
            {"ServiceSettings:MaxRetryAttempts", "3"},
            {"ServiceSettings:RetryDelayMs", "1000"},
            {"ServiceSettings:EnableLogging", "true"},
            {"ServiceSettings:LogLevel", "Information"},
            {"Logging:LogLevel:Default", "Debug"},
            {"Logging:LogLevel:LicenseReleaseService", "Debug"}
        };

        if (settings != null)
        {
            foreach (var setting in settings)
            {
                defaultSettings[setting.Key] = setting.Value;
            }
        }

        return defaultSettings;
    }

    /// <summary>
    /// Creates a mock cancellation token source
    /// </summary>
    /// <param name="shouldCancel">Whether the token should be cancelled immediately</param>
    /// <returns>Mock cancellation token source</returns>
    public static CancellationTokenSource CreateMockCancellationTokenSource(bool shouldCancel = false)
    {
        var cts = new CancellationTokenSource();
        if (shouldCancel)
        {
            cts.Cancel();
        }
        return cts;
    }

    /// <summary>
    /// Creates a mock stopwatch for timing tests
    /// </summary>
    /// <param name="elapsedTime">Time that should have elapsed</param>
    /// <param name="isRunning">Whether the stopwatch is running</param>
    /// <returns>Mock stopwatch</returns>
    public static Mock<System.Diagnostics.Stopwatch> CreateMockStopwatch(
        TimeSpan? elapsedTime = null,
        bool isRunning = false)
    {
        var mockStopwatch = new Mock<System.Diagnostics.Stopwatch> { CallBase = true };
        var time = elapsedTime ?? TimeSpan.Zero;

        mockStopwatch.Setup(s => s.Elapsed).Returns(time);
        mockStopwatch.Setup(s => s.ElapsedMilliseconds).Returns((long)time.TotalMilliseconds);
        mockStopwatch.Setup(s => s.IsRunning).Returns(isRunning);

        return mockStopwatch;
    }

    /// <summary>
    /// Creates a mock timer with configurable behavior
    /// </summary>
    /// <param name="interval">Timer interval</param>
    /// <param name="autoStart">Whether timer should start automatically</param>
    /// <returns>Mock timer</returns>
    public static Mock<System.Timers.Timer> CreateMockTimer(
        TimeSpan? interval = null,
        bool autoStart = false)
    {
        var mockTimer = new Mock<System.Timers.Timer> { CallBase = true };
        var timeInterval = interval ?? TimeSpan.FromSeconds(1);

        mockTimer.Setup(t => t.Interval).Returns(timeInterval.TotalMilliseconds);
        mockTimer.Setup(t => t.AutoReset).Returns(true);
        mockTimer.Setup(t => t.Enabled).Returns(autoStart);

        return mockTimer;
    }

    /// <summary>
    /// Creates a mock service with common interface methods
    /// </summary>
    /// <typeparam name="T">The service interface type</typeparam>
    /// <param name="setupMethod">Optional setup method for the mock</param>
    /// <returns>Configured mock service</returns>
    public static Mock<T> CreateMockService<T>(Action<Mock<T>>? setupMethod = null) where T : class
    {
        var mock = new Mock<T>();
        setupMethod?.Invoke(mock);
        return mock;
    }

    /// <summary>
    /// Creates mock data for license query results
    /// </summary>
    /// <param name="totalLicenses">Total license count</param>
    /// <param name="usedLicenses">Used license count</param>
    /// <param name="features">License features</param>
    /// <returns>License query result mock data</returns>
    public static object CreateMockLicenseQueryResult(
        int totalLicenses = 10,
        int usedLicenses = 4,
        params string[] features)
    {
        // This would typically return a mock of your actual LicenseQueryResult type
        // For now, returning an anonymous object that can be adapted to your needs
        return new
        {
            TotalLicenses = totalLicenses,
            UsedLicenses = usedLicenses,
            AvailableLicenses = totalLicenses - usedLicenses,
            Features = features.Length > 0 ? features : new[] { "SolidWorks", "SolidWorks_PDM" },
            ServerAddress = "test-server.company.com",
            QueryTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a sequence of mock objects for testing multiple scenarios
    /// </summary>
    /// <typeparam name="T">Type of objects to create</typeparam>
    /// <param name="factory">Factory method to create objects</param>
    /// <param name="count">Number of objects to create</param>
    /// <returns>Sequence of mock objects</returns>
    public static IEnumerable<T> CreateMockSequence<T>(Func<int, T> factory, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return factory(i);
        }
    }

    /// <summary>
    /// Creates a mock with lazy initialization for performance testing
    /// </summary>
    /// <typeparam name="T">Type to mock</typeparam>
    /// <param name="factory">Factory method to create the mock</param>
    /// <returns>Lazy-initialized mock</returns>
    public static Lazy<T> CreateLazyMock<T>(Func<T> factory) where T : class
    {
        return new Lazy<T>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}