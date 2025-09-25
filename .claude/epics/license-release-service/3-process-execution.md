---
title: "Process Execution Wrapper"
description: "lmutil.exe integration with comprehensive error handling, logging, and monitoring"
author: "claude"
created_date: "2025-09-25"
epic: "license-release-service"
task_number: "003"
priority: "high"
status: "in_progress"
parallel: true
depends_on: []
estimated_hours: 16
estimated_size: "Large"
tags: ["process-execution", "lmutil", "error-handling", "license-management"]
github: "https://github.com/shoubusi/LicenseReleaseService/issues/4"
updated: "2025-09-25T07:49:51Z"
---

## Overview

Implement a robust process execution wrapper for lmutil.exe integration that provides comprehensive error handling, logging, and monitoring for license management operations.

## Technical Approach

### 1. Process Execution Framework
- Create generic process execution wrapper
- Implement timeout handling and cancellation support
- Add stdout/stderr capture and parsing
- Support for both synchronous and asynchronous execution

### 2. lmutil.exe Specific Implementation
- Implement lmutil.exe command builders
- Add license server status checking
- Implement license release functionality
- Support for license feature queries

### 3. Error Handling and Recovery
- Implement comprehensive error detection
- Add retry logic for transient failures
- Include timeout and deadlock handling
- Provide meaningful error messages and logging

### 4. Output Parsing and Validation
- Parse lmutil.exe output formats
- Validate license status responses
- Extract relevant information from output
- Handle various output formats and edge cases

## Implementation Details

### Process Execution Wrapper
```csharp
public interface IProcessExecutor
{
    Task<ProcessExecutionResult> ExecuteAsync(string filePath, string arguments,
        CancellationToken cancellationToken = default);
    Task<ProcessExecutionResult> ExecuteAsync(string filePath, string arguments,
        TimeSpan timeout, CancellationToken cancellationToken = default);
}

public class ProcessExecutor : IProcessExecutor
{
    private readonly ILogger<ProcessExecutor> _logger;
    private readonly ProcessExecutionOptions _options;

    public ProcessExecutor(ILogger<ProcessExecutor> logger, ProcessExecutionOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public async Task<ProcessExecutionResult> ExecuteAsync(string filePath, string arguments,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        var tcs = new TaskCompletionSource<bool>();
        process.EnableRaisingEvents = true;
        process.Exited += (sender, e) => tcs.SetResult(true);

        if (!process.Start())
        {
            throw new ProcessExecutionException($"Failed to start process: {filePath}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeoutTask = Task.Delay(timeout, cancellationToken);
        var exitTask = tcs.Task;

        var completedTask = await Task.WhenAny(exitTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            process.Kill();
            throw new ProcessExecutionException($"Process timed out after {timeout.TotalSeconds} seconds");
        }

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessExecutionResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString(),
            ExecutionTime = DateTime.Now - process.StartTime
        };
    }
}
```

### lmutil.exe Integration
```csharp
public interface ILicenseManager
{
    Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default);
    Task<LicenseReleaseResult> ReleaseLicenseAsync(string server, int port, string feature, string user, CancellationToken cancellationToken = default);
    Task<LicenseFeatureInfo> GetFeatureInfoAsync(string server, int port, string feature, CancellationToken cancellationToken = default);
}

public class LmutilLicenseManager : ILicenseManager
{
    private readonly IProcessExecutor _processExecutor;
    private readonly ILogger<LmutilLicenseManager> _logger;
    private readonly string _lmutilPath;
    private readonly ProcessExecutionOptions _options;

    public LmutilLicenseManager(IProcessExecutor processExecutor, ILogger<LmutilLicenseManager> logger,
        string lmutilPath, ProcessExecutionOptions options)
    {
        _processExecutor = processExecutor;
        _logger = logger;
        _lmutilPath = lmutilPath;
        _options = options;
    }

    public async Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default)
    {
        var arguments = $"lmstat -c {port}@{server}";
        var result = await ExecuteWithRetryAsync(arguments, cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError("lmstat failed with exit code {ExitCode}: {Error}", result.ExitCode, result.Error);
            throw new LicenseManagerException($"License server status check failed: {result.Error}");
        }

        return ParseServerStatus(result.Output);
    }

    public async Task<LicenseReleaseResult> ReleaseLicenseAsync(string server, int port, string feature, string user, CancellationToken cancellationToken = default)
    {
        var arguments = $"lmremove -c {port}@{server} {feature} {user}";
        var result = await ExecuteWithRetryAsync(arguments, cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError("lmremove failed with exit code {ExitCode}: {Error}", result.ExitCode, result.Error);
            return new LicenseReleaseResult { Success = false, ErrorMessage = result.Error };
        }

        return new LicenseReleaseResult { Success = true };
    }

    private async Task<ProcessExecutionResult> ExecuteWithRetryAsync(string arguments, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var maxRetries = _options.MaxRetries;

        while (retryCount <= maxRetries)
        {
            try
            {
                var result = await _processExecutor.ExecuteAsync(_lmutilPath, arguments,
                    _options.Timeout, cancellationToken);

                if (result.ExitCode == 0 || retryCount == maxRetries)
                {
                    return result;
                }

                _logger.LogWarning("lmutil command failed, retry {RetryCount}/{MaxRetries}: {Error}",
                    retryCount + 1, maxRetries, result.Error);

                retryCount++;
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
            catch (Exception ex) when (retryCount < maxRetries)
            {
                _logger.LogWarning(ex, "lmutil command failed, retry {RetryCount}/{MaxRetries}",
                    retryCount + 1, maxRetries);
                retryCount++;
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
        }

        throw new LicenseManagerException($"lmutil command failed after {maxRetries} retries");
    }

    private LicenseServerStatus ParseServerStatus(string output)
    {
        // Parse lmutil lmstat output
        var status = new LicenseServerStatus();

        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("license server UP"))
            {
                status.IsServerUp = true;
            }
            else if (line.Contains("license server DOWN"))
            {
                status.IsServerUp = false;
            }
            // Parse other status information
        }

        return status;
    }
}
```

### Error Handling Classes
```csharp
public class ProcessExecutionResult
{
    public int ExitCode { get; set; }
    public string Output { get; set; }
    public string Error { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

public class LicenseServerStatus
{
    public bool IsServerUp { get; set; }
    public DateTime LastChecked { get; set; }
    public Dictionary<string, LicenseFeatureStatus> Features { get; set; } = new();
}

public class LicenseManagerException : Exception
{
    public LicenseManagerException(string message) : base(message) { }
    public LicenseManagerException(string message, Exception innerException) : base(message, innerException) { }
}
```

## Acceptance Criteria

### Must Have
- [ ] Process execution wrapper handles timeouts correctly
- [ ] Process execution captures both stdout and stderr
- [ ] lmutil.exe commands execute successfully with valid parameters
- [ ] Process failures are handled gracefully with meaningful error messages
- [ ] Retry logic works for transient failures
- [ ] Output parsing handles various lmutil.exe output formats
- [ ] Resource cleanup is performed properly (process disposal)
- [ ] Cancellation is supported for long-running operations

### Should Have
- [ ] Process execution includes performance metrics
- [ ] Advanced output parsing with structured data
- [ ] Support for multiple license server configurations
- [ ] Detailed logging of process execution
- [ ] Configuration-based retry settings
- [ ] Process execution monitoring and health checks

### Could Have
- [ ] Support for alternative license management tools
- [ ] Process execution with elevated privileges
- [ ] Distributed process execution across multiple servers
- [ ] Machine learning-based failure prediction

## Dependencies

### External Dependencies
- lmutil.exe from Autodesk Network License Manager
- System.Diagnostics.Process namespace
- System.Threading.Tasks namespace
- System.Text namespace for string manipulation

### Internal Dependencies
- Windows Service Implementation (Task 001)
- Configuration Management (Task 002)

## Testing Strategy

### Unit Tests
- Test process execution with various exit codes
- Test timeout handling and cancellation
- Test retry logic for different failure scenarios
- Test output parsing and validation

### Integration Tests
- Test actual lmutil.exe command execution
- Test license server connectivity
- Test license release operations
- Test error handling with invalid parameters

### Acceptance Tests
- Verify license status can be retrieved successfully
- Verify licenses can be released properly
- Verify error conditions are handled gracefully
- Verify system performance under load

## Risk Assessment

### High Risk
- lmutil.exe is not available or accessible
- License server connectivity issues
- Process execution hangs or timeouts
- Incorrect output parsing leads to false results

### Mitigation Strategies
- Comprehensive error handling and logging
- Process timeout and cleanup mechanisms
- Retry logic with exponential backoff
- Regular monitoring and health checks

## Success Metrics

- Process execution success rate: > 99%
- Average response time: < 2 seconds
- License release success rate: > 95%
- Timeout occurrences: < 1%
- Error rate due to parsing issues: < 0.1%