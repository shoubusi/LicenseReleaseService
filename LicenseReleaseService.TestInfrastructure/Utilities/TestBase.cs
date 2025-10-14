using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using LicenseReleaseService.TestInfrastructure.Logging;
using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TestInfrastructure.Utilities;

/// <summary>
/// Base class for all test classes providing common setup and teardown functionality
/// </summary>
public abstract class TestBase : IDisposable
{
    protected System.IServiceProvider ServiceProvider { get; private set; } = null!;
    protected ILogger Logger { get; private set; } = null!;
    protected TestExecutionLogger ExecutionLogger { get; private set; } = null!;
    protected IConfiguration Configuration { get; private set; } = null!;

    private bool _disposed = false;
    private readonly List<IDisposable> _disposables = new();

    /// <summary>
    /// Setup method called before each test
    /// </summary>
    protected virtual void Setup()
    {
        // Create service collection
        var services = new ServiceCollection();

        // Add configuration
        Configuration = CreateTestConfiguration();
        services.AddSingleton(Configuration);

        // Configure test logging
        services.ConfigureTestLogging(Configuration);

        // Build service provider
        ServiceProvider = services.BuildServiceProvider();

        // Get logger for this test class
        Logger = TestLoggingConfiguration.CreateTestLogger(ServiceProvider, GetType().Name);
        ExecutionLogger = new TestExecutionLogger(Logger);

        Logger.LogDebug("Test setup completed for {TestClassName}", GetType().Name);
    }

    /// <summary>
    /// Cleanup method called after each test
    /// </summary>
    protected virtual void Cleanup()
    {
        Logger?.LogDebug("Test cleanup started for {TestClassName}", GetType().Name);

        // Dispose all registered disposables
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Error disposing resource during cleanup");
            }
        }
        _disposables.Clear();

        // Dispose service provider
        if (ServiceProvider is IDisposable providerDisposable)
        {
            providerDisposable.Dispose();
        }

        Logger?.LogDebug("Test cleanup completed for {TestClassName}", GetType().Name);
    }

    /// <summary>
    /// Creates test configuration for the test environment
    /// </summary>
    /// <returns>Test configuration instance</returns>
    protected virtual IConfiguration CreateTestConfiguration()
    {
        var configurationBuilder = new ConfigurationBuilder();

        // Add in-memory test configuration
        var testSettings = new Dictionary<string, string>
        {
            {"Logging:EnableFileLogging", "false"},
            {"TestEnvironment", "Unit"},
            {"TestMode", "true"},
            {"ServiceSettings:PollIntervalMs", "1000"},
            {"ServiceSettings:HealthCheckIntervalMs", "5000"},
            {"Logging:LogLevel:Default", "Debug"},
            {"Logging:LogLevel:LicenseReleaseService", "Debug"}
        };

        configurationBuilder.AddInMemoryCollection(testSettings);

        // Add environment variables if needed
        // configurationBuilder.AddEnvironmentVariables();

        return configurationBuilder.Build();
    }

    /// <summary>
    /// Gets a service from the dependency injection container
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    /// <returns>The service instance</returns>
    protected T GetService<T>() where T : class
    {
        var service = ServiceProvider.GetService<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
        }
        return service;
    }

    /// <summary>
    /// Gets a required service from the dependency injection container
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    /// <returns>The service instance</returns>
    protected T GetRequiredService<T>() where T : class
    {
        var service = ServiceProvider.GetService<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
        }
        return service;
    }

    /// <summary>
    /// Registers a disposable resource for automatic cleanup
    /// </summary>
    /// <param name="disposable">The disposable resource</param>
    protected void RegisterDisposable(IDisposable disposable)
    {
        if (disposable != null)
        {
            _disposables.Add(disposable);
        }
    }

    /// <summary>
    /// Starts a new test execution with logging
    /// </summary>
    /// <param name="testName">The name of the test method</param>
    protected void StartTestExecution(string testName)
    {
        ExecutionLogger.StartTest($"{GetType().Name}.{testName}");
    }

    /// <summary>
    /// Records a test step in the execution log
    /// </summary>
    /// <param name="stepName">The name of the step</param>
    /// <param name="description">Optional description</param>
    protected void RecordTestStep(string stepName, string? description = null)
    {
        ExecutionLogger.RecordStep(stepName, description);
    }

    /// <summary>
    /// Records a test error in the execution log
    /// </summary>
    /// <param name="stepName">The step where error occurred</param>
    /// <param name="exception">The exception</param>
    /// <param name="description">Optional description</param>
    protected void RecordTestError(string stepName, Exception exception, string? description = null)
    {
        ExecutionLogger.RecordError(stepName, exception, description);
    }

    /// <summary>
    /// Records a validation failure
    /// </summary>
    /// <param name="validationName">The validation that failed</param>
    /// <param name="expected">Expected value</param>
    /// <param name="actual">Actual value</param>
    protected void RecordValidationFailure(string validationName, object expected, object actual)
    {
        ExecutionLogger.RecordValidationFailure(validationName, expected, actual);
    }

    /// <summary>
    /// Completes test execution and returns summary
    /// </summary>
    /// <param name="success">Whether test succeeded</param>
    /// <returns>Test execution summary</returns>
    protected TestExecutionSummary CompleteTestExecution(bool success = true)
    {
        return ExecutionLogger.CompleteTest(success);
    }

    /// <summary>
    /// Asserts that a condition is true with proper logging
    /// </summary>
    /// <param name="condition">The condition to test</param>
    /// <param name="message">The assertion message</param>
    protected void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            RecordTestError("Assertion Failed", new InvalidOperationException(message), message);
        }
        Logger.LogDebug("Assertion: {Message} - {Result}", message, condition ? "PASS" : "FAIL");
    }

    /// <summary>
    /// Asserts that two objects are equal with proper logging
    /// </summary>
    /// <param name="expected">Expected value</param>
    /// <param name="actual">Actual value</param>
    /// <param name="message">The assertion message</param>
    protected void AssertEquals(object expected, object actual, string message)
    {
        var isEqual = Equals(expected, actual);
        if (!isEqual)
        {
            RecordValidationFailure(message, expected, actual);
        }
        Logger.LogDebug("Equals assertion: {Message} - Expected: {Expected}, Actual: {Actual} - {Result}",
            message, expected, actual, isEqual ? "PASS" : "FAIL");
    }

    /// <summary>
    /// Dispose implementation
    /// </summary>
    public virtual void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            ExecutionLogger?.Dispose();
            _disposed = true;
        }
    }
}