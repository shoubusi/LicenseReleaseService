using System;
using System.Text;

namespace LicenseReleaseService.Process
{
    /// <summary>
    /// Represents the result of a process execution
    /// </summary>
    public class ProcessExecutionResult
    {
        /// <summary>
        /// Gets or sets the exit code of the process
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Gets or sets the standard output of the process
        /// </summary>
        public string Output { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the standard error output of the process
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the standard output of the process (alias for Output)
        /// </summary>
        public string StandardOutput
        {
            get => Output;
            set => Output = value;
        }

        /// <summary>
        /// Gets or sets the standard error output of the process (alias for Error)
        /// </summary>
        public string StandardError
        {
            get => Error;
            set => Error = value;
        }

        /// <summary>
        /// Gets or sets the time taken to execute the process
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the start time of the process
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time of the process
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the retry attempt number
        /// </summary>
        public int RetryAttempt { get; set; }

        /// <summary>
        /// Gets a value indicating whether the process succeeded
        /// </summary>
        public bool Success => ExitCode == 0;

        /// <summary>
        /// Gets a value indicating whether the process timed out
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// Gets a value indicating whether the process was cancelled
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// Gets the combined output (stdout and stderr)
        /// </summary>
        public string CombinedOutput
        {
            get
            {
                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(Output))
                {
                    builder.AppendLine("STDOUT:");
                    builder.AppendLine(Output);
                }
                if (!string.IsNullOrWhiteSpace(Error))
                {
                    builder.AppendLine("STDERR:");
                    builder.AppendLine(Error);
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// Returns a string representation of the process execution result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Process Execution Result:");
            builder.AppendLine($"  Exit Code: {ExitCode}");
            builder.AppendLine($"  Success: {Success}");
            builder.AppendLine($"  Process ID: {ProcessId}");
            builder.AppendLine($"  Execution Time: {ExecutionTime.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Start Time: {StartTime:yyyy-MM-dd HH:mm:ss.fff}");
            builder.AppendLine($"  End Time: {EndTime:yyyy-MM-dd HH:mm:ss.fff}");
            builder.AppendLine($"  Timed Out: {TimedOut}");
            builder.AppendLine($"  Cancelled: {Cancelled}");

            if (!string.IsNullOrWhiteSpace(Output))
            {
                builder.AppendLine($"  Output: {Output.Length} characters");
            }

            if (!string.IsNullOrWhiteSpace(Error))
            {
                builder.AppendLine($"  Error: {Error.Length} characters");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Creates a successful process execution result
        /// </summary>
        /// <param name="output">Standard output</param>
        /// <param name="executionTime">Execution time</param>
        /// <param name="processId">Process ID</param>
        /// <returns>Successful result</returns>
        public static ProcessExecutionResult SuccessResult(string output, TimeSpan executionTime, int processId)
        {
            return new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = output,
                ExecutionTime = executionTime,
                ProcessId = processId,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.Add(executionTime)
            };
        }

        /// <summary>
        /// Creates a failed process execution result
        /// </summary>
        /// <param name="exitCode">Exit code</param>
        /// <param name="error">Error output</param>
        /// <param name="executionTime">Execution time</param>
        /// <param name="processId">Process ID</param>
        /// <returns>Failed result</returns>
        public static ProcessExecutionResult FailureResult(int exitCode, string error, TimeSpan executionTime, int processId)
        {
            return new ProcessExecutionResult
            {
                ExitCode = exitCode,
                Error = error,
                ExecutionTime = executionTime,
                ProcessId = processId,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.Add(executionTime)
            };
        }

        /// <summary>
        /// Creates a timeout process execution result
        /// </summary>
        /// <param name="executionTime">Execution time</param>
        /// <param name="processId">Process ID</param>
        /// <returns>Timeout result</returns>
        public static ProcessExecutionResult TimeoutResult(TimeSpan executionTime, int processId)
        {
            return new ProcessExecutionResult
            {
                ExitCode = -1,
                ExecutionTime = executionTime,
                ProcessId = processId,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.Add(executionTime),
                TimedOut = true,
                Error = "Process execution timed out"
            };
        }

        /// <summary>
        /// Creates a cancelled process execution result
        /// </summary>
        /// <param name="executionTime">Execution time</param>
        /// <param name="processId">Process ID</param>
        /// <returns>Cancelled result</returns>
        public static ProcessExecutionResult CancelledResult(TimeSpan executionTime, int processId)
        {
            return new ProcessExecutionResult
            {
                ExitCode = -1,
                ExecutionTime = executionTime,
                ProcessId = processId,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.Add(executionTime),
                Cancelled = true,
                Error = "Process execution was cancelled"
            };
        }
    }
}