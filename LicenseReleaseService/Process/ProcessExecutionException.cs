using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace LicenseReleaseService.Process
{
    /// <summary>
    /// Exception thrown when process execution fails
    /// </summary>
    [Serializable]
    public class ProcessExecutionException : Exception
    {
        /// <summary>
        /// Gets the file path of the process that failed
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the arguments of the process that failed
        /// </summary>
        public string Arguments { get; }

        /// <summary>
        /// Gets the exit code of the process
        /// </summary>
        public int? ExitCode { get; set; }

        /// <summary>
        /// Gets the process ID
        /// </summary>
        public int? ProcessId { get; }

        /// <summary>
        /// Gets the standard output of the process
        /// </summary>
        public string Output { get; }

        /// <summary>
        /// Gets the standard error of the process
        /// </summary>
        public string Error { get; }

        /// <summary>
        /// Gets a value indicating whether the process timed out
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// Gets a value indicating whether the process was cancelled
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// Gets the execution time of the process
        /// </summary>
        public TimeSpan? ExecutionTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class
        /// </summary>
        public ProcessExecutionException()
            : base("Process execution failed")
        {
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class with a specified error message
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        public ProcessExecutionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class with a specified error message and inner exception
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public ProcessExecutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class with process execution details
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="filePath">The file path of the process</param>
        /// <param name="arguments">The arguments of the process</param>
        /// <param name="exitCode">The exit code of the process</param>
        /// <param name="output">The standard output of the process</param>
        /// <param name="error">The standard error of the process</param>
        public ProcessExecutionException(string message, string filePath, string arguments, int? exitCode, string output, string error)
            : base(message)
        {
            FilePath = filePath;
            Arguments = arguments;
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class with process execution result
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="filePath">The file path of the process</param>
        /// <param name="arguments">The arguments of the process</param>
        /// <param name="result">The process execution result</param>
        public ProcessExecutionException(string message, string filePath, string arguments, ProcessExecutionResult result)
            : base(message)
        {
            FilePath = filePath;
            Arguments = arguments;
            ExitCode = result.ExitCode;
            ProcessId = result.ProcessId;
            Output = result.Output;
            Error = result.Error;
            TimedOut = result.TimedOut;
            Cancelled = result.Cancelled;
            ExecutionTime = result.ExecutionTime;
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class with timeout information
        /// </summary>
        /// <param name="filePath">The file path of the process</param>
        /// <param name="arguments">The arguments of the process</param>
        /// <param name="timeout">The timeout that was exceeded</param>
        /// <param name="executionTime">The actual execution time</param>
        /// <returns>ProcessExecutionException for timeout</returns>
        public static ProcessExecutionException TimeoutException(string filePath, string arguments, TimeSpan timeout, TimeSpan executionTime)
        {
            return new ProcessExecutionException(
                $"Process execution timed out after {timeout.TotalSeconds:F1} seconds",
                filePath,
                arguments,
                null,
                string.Empty,
                $"Process timed out after {timeout.TotalSeconds:F1} seconds (executed for {executionTime.TotalSeconds:F1} seconds)")
            {
                TimedOut = true,
                ExecutionTime = executionTime
            };
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class for cancelled operations
        /// </summary>
        /// <param name="filePath">The file path of the process</param>
        /// <param name="arguments">The arguments of the process</param>
        /// <param name="executionTime">The actual execution time</param>
        /// <returns>ProcessExecutionException for cancellation</returns>
        public static ProcessExecutionException CancelledException(string filePath, string arguments, TimeSpan executionTime)
        {
            return new ProcessExecutionException(
                "Process execution was cancelled",
                filePath,
                arguments,
                null,
                string.Empty,
                "Process execution was cancelled")
            {
                Cancelled = true,
                ExecutionTime = executionTime
            };
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class for process start failures
        /// </summary>
        /// <param name="filePath">The file path of the process</param>
        /// <param name="arguments">The arguments of the process</param>
        /// <param name="innerException">The inner exception</param>
        /// <returns>ProcessExecutionException for start failure</returns>
        public static ProcessExecutionException StartFailureException(string filePath, string arguments, Exception innerException)
        {
            return new ProcessExecutionException(
                $"Failed to start process: {filePath}",
                filePath,
                arguments,
                null,
                string.Empty,
                innerException?.Message ?? "Unknown error")
            {
                ExitCode = -1
            };
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class for non-zero exit codes
        /// </summary>
        /// <param name="filePath">The file path of the process</param>
        /// <param name="arguments">The arguments of the process</param>
        /// <param name="result">The process execution result</param>
        /// <returns>ProcessExecutionException for non-zero exit code</returns>
        public static ProcessExecutionException NonZeroExitCodeException(string filePath, string arguments, ProcessExecutionResult result)
        {
            return new ProcessExecutionException(
                $"Process exited with non-zero code: {result.ExitCode}",
                filePath,
                arguments,
                result);
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionException class with serialization data
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">The streaming context</param>
        protected ProcessExecutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            FilePath = info.GetString(nameof(FilePath));
            Arguments = info.GetString(nameof(Arguments));
            ExitCode = info.GetInt32(nameof(ExitCode));
            ProcessId = info.GetInt32(nameof(ProcessId));
            Output = info.GetString(nameof(Output));
            Error = info.GetString(nameof(Error));
            TimedOut = info.GetBoolean(nameof(TimedOut));
            Cancelled = info.GetBoolean(nameof(Cancelled));
            ExecutionTime = (TimeSpan)info.GetValue(nameof(ExecutionTime), typeof(TimeSpan));
        }

        /// <summary>
        /// Sets the serialization data for the exception
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">The streaming context</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(FilePath), FilePath);
            info.AddValue(nameof(Arguments), Arguments);
            info.AddValue(nameof(ExitCode), ExitCode ?? -1);
            info.AddValue(nameof(ProcessId), ProcessId ?? -1);
            info.AddValue(nameof(Output), Output);
            info.AddValue(nameof(Error), Error);
            info.AddValue(nameof(TimedOut), TimedOut);
            info.AddValue(nameof(Cancelled), Cancelled);
            info.AddValue(nameof(ExecutionTime), ExecutionTime);
        }

        /// <summary>
        /// Returns a string representation of the process execution exception
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"Process Details:");
            builder.AppendLine($"  File Path: {FilePath ?? "N/A"}");
            builder.AppendLine($"  Arguments: {Arguments ?? "N/A"}");
            builder.AppendLine($"  Exit Code: {ExitCode?.ToString() ?? "N/A"}");
            builder.AppendLine($"  Process ID: {ProcessId?.ToString() ?? "N/A"}");
            builder.AppendLine($"  Timed Out: {TimedOut}");
            builder.AppendLine($"  Cancelled: {Cancelled}");

            if (ExecutionTime.HasValue)
            {
                builder.AppendLine($"  Execution Time: {ExecutionTime.Value.TotalMilliseconds:F2}ms");
            }

            if (!string.IsNullOrWhiteSpace(Output))
            {
                builder.AppendLine($"  Output ({Output.Length} characters): {Output}");
            }

            if (!string.IsNullOrWhiteSpace(Error))
            {
                builder.AppendLine($"  Error ({Error.Length} characters): {Error}");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets a user-friendly error message
        /// </summary>
        /// <returns>User-friendly error message</returns>
        public string GetUserFriendlyMessage()
        {
            if (TimedOut)
            {
                return $"The operation timed out. Please try again or increase the timeout value.";
            }

            if (Cancelled)
            {
                return "The operation was cancelled.";
            }

            if (ExitCode.HasValue && ExitCode.Value != 0)
            {
                return $"The operation failed with exit code {ExitCode.Value}. {(string.IsNullOrWhiteSpace(Error) ? "" : $"Error: {Error}")}";
            }

            return Message;
        }
    }
}