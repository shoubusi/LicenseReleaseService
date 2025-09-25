using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.Process
{
    /// <summary>
    /// Implements process execution with comprehensive error handling, timeout support, and logging
    /// </summary>
    public class ProcessExecutor : IProcessExecutor, IDisposable
    {
        private readonly ILogger<ProcessExecutor> _logger;
        private readonly ProcessExecutionOptions _options;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the ProcessExecutor class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The process execution options</param>
        public ProcessExecutor(ILogger<ProcessExecutor> logger, ProcessExecutionOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Executes a process asynchronously with default timeout
        /// </summary>
        /// <param name="filePath">Path to the executable file</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process execution result</returns>
        public async Task<ProcessExecutionResult> ExecuteAsync(string filePath, string arguments,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(filePath, arguments, _options.Timeout, cancellationToken);
        }

        /// <summary>
        /// Executes a process asynchronously with custom timeout
        /// </summary>
        /// <param name="filePath">Path to the executable file</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="timeout">Execution timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process execution result</returns>
        public async Task<ProcessExecutionResult> ExecuteAsync(string filePath, string arguments,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace", nameof(filePath));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Process execution cancelled before start: {FilePath} {Arguments}", filePath, arguments);
                return ProcessExecutionResult.CancelledResult(TimeSpan.Zero, -1);
            }

            var startTime = DateTime.Now;
            var attempt = 0;
            var maxAttempts = _options.MaxRetries + 1;

            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    var result = await ExecuteInternalAsync(filePath, arguments, timeout, cancellationToken, attempt);

                    if (_options.ThrowOnNonZeroExitCode && !result.Success && !result.TimedOut && !result.Cancelled)
                    {
                        throw ProcessExecutionException.NonZeroExitCodeException(filePath, arguments, result);
                    }

                    return result;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout occurred
                    if (attempt < maxAttempts)
                    {
                        _logger.LogWarning("Process execution attempt {Attempt}/{MaxAttempts} timed out: {FilePath} {Arguments}. Retrying in {RetryDelay}ms...",
                            attempt, maxAttempts, filePath, arguments, _options.RetryDelay.TotalMilliseconds);

                        await Task.Delay(_options.RetryDelay, cancellationToken);
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Process execution attempt {Attempt}/{MaxAttempts} failed: {FilePath} {Arguments}. Retrying in {RetryDelay}ms...",
                        attempt, maxAttempts, filePath, arguments, _options.RetryDelay.TotalMilliseconds);

                    await Task.Delay(_options.RetryDelay, cancellationToken);
                }
            }

            // This should never be reached, but just in case
            throw new InvalidOperationException("Unexpected error in retry logic");
        }

        /// <summary>
        /// Executes a process synchronously with default timeout
        /// </summary>
        /// <param name="filePath">Path to the executable file</param>
        /// <param name="arguments">Command line arguments</param>
        /// <returns>Process execution result</returns>
        public ProcessExecutionResult Execute(string filePath, string arguments)
        {
            return Execute(filePath, arguments, _options.Timeout);
        }

        /// <summary>
        /// Executes a process synchronously with custom timeout
        /// </summary>
        /// <param name="filePath">Path to the executable file</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="timeout">Execution timeout</param>
        /// <returns>Process execution result</returns>
        public ProcessExecutionResult Execute(string filePath, string arguments, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace", nameof(filePath));
            }

            var startTime = DateTime.Now;
            var attempt = 0;
            var maxAttempts = _options.MaxRetries + 1;

            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    var result = ExecuteInternal(filePath, arguments, timeout, attempt);

                    if (_options.ThrowOnNonZeroExitCode && !result.Success && !result.TimedOut && !result.Cancelled)
                    {
                        throw ProcessExecutionException.NonZeroExitCodeException(filePath, arguments, result);
                    }

                    return result;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning(ex, "Process execution attempt {Attempt}/{MaxAttempts} failed: {FilePath} {Arguments}. Retrying in {RetryDelay}ms...",
                        attempt, maxAttempts, filePath, arguments, _options.RetryDelay.TotalMilliseconds);

                    Thread.Sleep(_options.RetryDelay);
                }
            }

            // This should never be reached, but just in case
            throw new InvalidOperationException("Unexpected error in retry logic");
        }

        private async Task<ProcessExecutionResult> ExecuteInternalAsync(string filePath, string arguments,
            TimeSpan timeout, CancellationToken cancellationToken, int attempt)
        {
            var startTime = DateTime.Now;
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            long outputSize = 0;
            long errorSize = 0;

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Starting process execution (attempt {Attempt}): {FilePath} {Arguments}",
                    attempt, filePath, arguments);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = arguments,
                UseShellExecute = _options.UseShellExecute,
                RedirectStandardOutput = !_options.UseShellExecute,
                RedirectStandardError = !_options.UseShellExecute,
                RedirectStandardInput = _options.RedirectStandardInput && !_options.UseShellExecute,
                CreateNoWindow = _options.CreateNoWindow,
                WindowStyle = _options.CreateNoWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                StandardOutputEncoding = _options.Encoding,
                StandardErrorEncoding = _options.Encoding
            };

            // Set working directory if specified
            if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
            {
                startInfo.WorkingDirectory = _options.WorkingDirectory;
            }

            // Set environment variables if specified
            if (_options.EnvironmentVariables.Count > 0)
            {
                foreach (var kvp in _options.EnvironmentVariables)
                {
                    startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            using var process = new Process { StartInfo = startInfo };
            var tcs = new TaskCompletionSource<bool>();

            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) => tcs.TrySetResult(true);

            // Setup output and error handlers
            if (!_options.UseShellExecute)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        outputSize += _options.Encoding.GetByteCount(e.Data) + 2; // +2 for newline

                        if (_options.MaxOutputSize > 0 && outputSize > _options.MaxOutputSize)
                        {
                            _logger.LogWarning("Process output size exceeded maximum limit of {MaxSize} bytes", _options.MaxOutputSize);
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        errorSize += _options.Encoding.GetByteCount(e.Data) + 2; // +2 for newline

                        if (_options.MaxOutputSize > 0 && errorSize > _options.MaxOutputSize)
                        {
                            _logger.LogWarning("Process error output size exceeded maximum limit of {MaxSize} bytes", _options.MaxOutputSize);
                        }
                    }
                };
            }

            try
            {
                if (!process.Start())
                {
                    throw ProcessExecutionException.StartFailureException(filePath, arguments, null);
                }

                var processId = process.Id;
                var actualStartTime = process.StartTime;

                if (!_options.UseShellExecute)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                // Wait for exit with timeout
                var timeoutTask = Task.Delay(timeout, cancellationToken);
                var exitTask = tcs.Task;

                var completedTask = await Task.WhenAny(exitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Timeout occurred
                    if (_options.KillProcessTreeOnTimeout)
                    {
                        KillProcessTree(processId);
                    }
                    else
                    {
                        process.Kill();
                    }

                    var executionTime = DateTime.Now - startTime;
                    _logger.LogWarning("Process execution timed out after {Timeout}ms: {FilePath} {Arguments}",
                        timeout.TotalMilliseconds, filePath, arguments);

                    throw ProcessExecutionException.TimeoutException(filePath, arguments, timeout, executionTime);
                }

                await process.WaitForExitAsync(cancellationToken);

                var exitCode = process.ExitCode;
                var endTime = process.HasExited ? process.ExitTime : DateTime.Now;
                var executionTime = endTime - actualStartTime;

                var result = new ProcessExecutionResult
                {
                    ExitCode = exitCode,
                    Output = outputBuilder.ToString(),
                    Error = errorBuilder.ToString(),
                    ExecutionTime = executionTime,
                    ProcessId = processId,
                    StartTime = actualStartTime,
                    EndTime = endTime
                };

                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug("Process execution completed (attempt {Attempt}): {FilePath} {Arguments} - ExitCode: {ExitCode}, ExecutionTime: {ExecutionTime}ms",
                        attempt, filePath, arguments, exitCode, executionTime.TotalMilliseconds);
                }

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                var executionTime = DateTime.Now - startTime;
                _logger.LogInformation("Process execution cancelled (attempt {Attempt}): {FilePath} {Arguments}",
                    attempt, filePath, arguments);

                if (process != null && !process.HasExited)
                {
                    if (_options.KillProcessTreeOnTimeout)
                    {
                        KillProcessTree(process.Id);
                    }
                    else
                    {
                        process.Kill();
                    }
                }

                throw ProcessExecutionException.CancelledException(filePath, arguments, executionTime);
            }
        }

        private ProcessExecutionResult ExecuteInternal(string filePath, string arguments,
            TimeSpan timeout, int attempt)
        {
            var startTime = DateTime.Now;
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            long outputSize = 0;
            long errorSize = 0;

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Starting process execution (attempt {Attempt}): {FilePath} {Arguments}",
                    attempt, filePath, arguments);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = arguments,
                UseShellExecute = _options.UseShellExecute,
                RedirectStandardOutput = !_options.UseShellExecute,
                RedirectStandardError = !_options.UseShellExecute,
                RedirectStandardInput = _options.RedirectStandardInput && !_options.UseShellExecute,
                CreateNoWindow = _options.CreateNoWindow,
                WindowStyle = _options.CreateNoWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                StandardOutputEncoding = _options.Encoding,
                StandardErrorEncoding = _options.Encoding
            };

            // Set working directory if specified
            if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
            {
                startInfo.WorkingDirectory = _options.WorkingDirectory;
            }

            // Set environment variables if specified
            if (_options.EnvironmentVariables.Count > 0)
            {
                foreach (var kvp in _options.EnvironmentVariables)
                {
                    startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            using var process = new Process { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    throw ProcessExecutionException.StartFailureException(filePath, arguments, null);
                }

                var processId = process.Id;
                var actualStartTime = process.StartTime;

                if (!_options.UseShellExecute)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                // Wait for exit with timeout
                var exited = process.WaitForExit((int)timeout.TotalMilliseconds);

                if (!exited)
                {
                    // Timeout occurred
                    if (_options.KillProcessTreeOnTimeout)
                    {
                        KillProcessTree(processId);
                    }
                    else
                    {
                        process.Kill();
                    }

                    var executionTime = DateTime.Now - startTime;
                    _logger.LogWarning("Process execution timed out after {Timeout}ms: {FilePath} {Arguments}",
                        timeout.TotalMilliseconds, filePath, arguments);

                    throw ProcessExecutionException.TimeoutException(filePath, arguments, timeout, executionTime);
                }

                var exitCode = process.ExitCode;
                var endTime = process.ExitTime;
                var executionTime = endTime - actualStartTime;

                var result = new ProcessExecutionResult
                {
                    ExitCode = exitCode,
                    Output = outputBuilder.ToString(),
                    Error = errorBuilder.ToString(),
                    ExecutionTime = executionTime,
                    ProcessId = processId,
                    StartTime = actualStartTime,
                    EndTime = endTime
                };

                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug("Process execution completed (attempt {Attempt}): {FilePath} {Arguments} - ExitCode: {ExitCode}, ExecutionTime: {ExecutionTime}ms",
                        attempt, filePath, arguments, exitCode, executionTime.TotalMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
                throw;
            }
        }

        private void KillProcessTree(int processId)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processName = currentProcess.ProcessName;

                // Find all processes with the same name
                var processes = Process.GetProcessesByName(processName);

                foreach (var process in processes)
                {
                    try
                    {
                        if (GetParentProcessId(process.Id) == processId)
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill child process {ProcessId}", process.Id);
                    }
                }

                // Kill the main process
                var mainProcess = Process.GetProcessById(processId);
                mainProcess.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill process tree for process {ProcessId}", processId);
            }
        }

        private int GetParentProcessId(int processId)
        {
            // This is a simplified version - in a real implementation, you might want to use WMI or P/Invoke
            try
            {
                var process = Process.GetProcessById(processId);
                return process.Id; // This is a placeholder - actual implementation would get parent PID
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Disposes the process executor
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Cleanup any resources
                _disposed = true;
            }
        }
    }
}