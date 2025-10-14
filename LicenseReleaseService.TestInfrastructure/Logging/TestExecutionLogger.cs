using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LicenseReleaseService.TestInfrastructure.Logging;

/// <summary>
/// Utility class for tracking test execution with detailed logging
/// </summary>
public class TestExecutionLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch;
    private readonly List<TestStep> _executionSteps;

    public TestExecutionLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stopwatch = new Stopwatch();
        _executionSteps = new List<TestStep>();
    }

    /// <summary>
    /// Starts a new test execution tracking session
    /// </summary>
    /// <param name="testName">The name of the test being executed</param>
    public void StartTest(string testName)
    {
        if (string.IsNullOrEmpty(testName))
            throw new ArgumentException("Test name cannot be null or empty", nameof(testName));

        _logger.LogInformation("Starting test execution: {TestName}", testName);
        _stopwatch.Restart();
        _executionSteps.Clear();

        AddStep("Test Started", $"Test '{testName}' execution begun");
    }

    /// <summary>
    /// Records a test step completion
    /// </summary>
    /// <param name="stepName">The name of the step</param>
    /// <param name="description">Optional description of what was done</param>
    public void RecordStep(string stepName, string? description = null)
    {
        if (string.IsNullOrEmpty(stepName))
            throw new ArgumentException("Step name cannot be null or empty", nameof(stepName));

        var elapsed = _stopwatch.ElapsedMilliseconds;
        AddStep(stepName, description ?? $"Step '{stepName}' completed", elapsed);
        _logger.LogDebug("Test step completed: {StepName} ({ElapsedMs}ms)", stepName, elapsed);
    }

    /// <summary>
    /// Records an error during test execution
    /// </summary>
    /// <param name="stepName">The step where error occurred</param>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="description">Optional description of the error</param>
    public void RecordError(string stepName, Exception exception, string? description = null)
    {
        if (string.IsNullOrEmpty(stepName))
            throw new ArgumentException("Step name cannot be null or empty", nameof(stepName));
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        var elapsed = _stopwatch.ElapsedMilliseconds;
        var message = description ?? $"Error in step '{stepName}': {exception.Message}";

        AddStep(stepName, message, elapsed, true);
        _logger.LogError(exception, "Test step failed: {StepName} - {Message}", stepName, message);
    }

    /// <summary>
    /// Records a validation failure
    /// </summary>
    /// <param name="validationName">The name of the validation that failed</param>
    /// <param name="expected">The expected value</param>
    /// <param name="actual">The actual value</param>
    public void RecordValidationFailure(string validationName, object expected, object actual)
    {
        if (string.IsNullOrEmpty(validationName))
            throw new ArgumentException("Validation name cannot be null or empty", nameof(validationName));

        var elapsed = _stopwatch.ElapsedMilliseconds;
        var message = $"Validation failed: Expected '{expected}', but was '{actual}'";

        AddStep($"Validation: {validationName}", message, elapsed, true);
        _logger.LogWarning("Test validation failed: {ValidationName} - Expected: {Expected}, Actual: {Actual}",
            validationName, expected, actual);
    }

    /// <summary>
    /// Completes the test execution and logs summary
    /// </summary>
    /// <param name="success">Whether the test completed successfully</param>
    /// <returns>Test execution summary</returns>
    public TestExecutionSummary CompleteTest(bool success = true)
    {
        _stopwatch.Stop();
        var totalElapsed = _stopwatch.ElapsedMilliseconds;

        var summary = new TestExecutionSummary
        {
            TotalDurationMs = totalElapsed,
            StepCount = _executionSteps.Count,
            Success = success,
            Steps = _executionSteps.ToList()
        };

        if (success)
        {
            _logger.LogInformation("Test completed successfully in {ElapsedMs}ms with {StepCount} steps",
                totalElapsed, summary.StepCount);
        }
        else
        {
            _logger.LogError("Test failed after {ElapsedMs}ms with {StepCount} steps",
                totalElapsed, summary.StepCount);
        }

        return summary;
    }

    /// <summary>
    /// Logs the current memory usage for performance tracking
    /// </summary>
    /// <param name="stepName">The step name for context</param>
    public void LogMemoryUsage(string stepName)
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var memoryMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2);

        _logger.LogDebug("Memory usage at {StepName}: {MemoryMB} MB", stepName, memoryMB);
    }

    private void AddStep(string name, string description, long elapsedMs = 0, bool isError = false)
    {
        _executionSteps.Add(new TestStep
        {
            Name = name,
            Description = description,
            ElapsedMs = elapsedMs,
            IsError = isError,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Dispose the TestExecutionLogger
    /// </summary>
    public void Dispose()
    {
        _stopwatch?.Stop();
        _executionSteps.Clear();
    }
}

/// <summary>
/// Represents a single step in test execution
/// </summary>
public class TestStep
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public bool IsError { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Summary of test execution
/// </summary>
public class TestExecutionSummary
{
    public long TotalDurationMs { get; set; }
    public int StepCount { get; set; }
    public bool Success { get; set; }
    public List<TestStep> Steps { get; set; } = new();
    public int ErrorCount => Steps.Count(s => s.IsError);
    public long AverageStepMs => StepCount > 0 ? Steps.Sum(s => s.ElapsedMs) / StepCount : 0;
}