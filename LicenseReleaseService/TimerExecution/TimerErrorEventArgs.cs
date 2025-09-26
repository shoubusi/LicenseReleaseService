using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for timer error events
    /// </summary>
    public class TimerErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exception that caused the error
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Gets the timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the timer status when the error occurred
        /// </summary>
        public TimerStatus Status { get; }

        /// <summary>
        /// Gets the execution identifier if the error occurred during execution
        /// </summary>
        public Guid? ExecutionId { get; }

        /// <summary>
        /// Gets the number of consecutive errors
        /// </summary>
        public int ConsecutiveErrors { get; }

        /// <summary>
        /// Gets a value indicating whether the error should trigger circuit breaker
        /// </summary>
        public bool ShouldTriggerCircuitBreaker { get; set; }

        /// <summary>
        /// Gets a value indicating whether the error is fatal and should stop the timer
        /// </summary>
        public bool IsFatal { get; set; }

        /// <summary>
        /// Gets the error severity level
        /// </summary>
        public TimerErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets the error category
        /// </summary>
        public TimerErrorCategory Category { get; set; }

        /// <summary>
        /// Gets the recovery action taken or suggested
        /// </summary>
        public TimerErrorRecoveryAction RecoveryAction { get; set; }

        /// <summary>
        /// Gets additional error context information
        /// </summary>
        public System.Collections.Generic.IDictionary<string, object> Context { get; }

        /// <summary>
        /// Gets the error message with additional context
        /// </summary>
        public string FormattedMessage { get; }

        /// <summary>
        /// Gets the internal error code for categorization
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets whether the error was handled successfully
        /// </summary>
        public bool WasHandled { get; set; }

        /// <summary>
        /// Gets the time taken to handle the error
        /// </summary>
        public TimeSpan HandlingDuration { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerErrorEventArgs class
        /// </summary>
        /// <param name="error">The exception that caused the error</param>
        /// <param name="status">The timer status when the error occurred</param>
        /// <param name="consecutiveErrors">The number of consecutive errors</param>
        /// <param name="executionId">The execution identifier, if available</param>
        public TimerErrorEventArgs(Exception error, TimerStatus status, int consecutiveErrors, Guid? executionId = null)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Status = status;
            ConsecutiveErrors = consecutiveErrors;
            ExecutionId = executionId;
            Timestamp = DateTime.UtcNow;
            ShouldTriggerCircuitBreaker = false;
            IsFatal = false;
            Severity = DetermineErrorSeverity(error);
            Category = DetermineErrorCategory(error);
            RecoveryAction = DetermineRecoveryAction(error, status);
            Context = new System.Collections.Generic.Dictionary<string, object>();
            FormattedMessage = FormatErrorMessage(error, status, consecutiveErrors);
            ErrorCode = GenerateErrorCode(error, status);
            WasHandled = false;
            HandlingDuration = TimeSpan.Zero;
        }

        /// <summary>
        /// Initializes a new instance of the TimerErrorEventArgs class with custom severity
        /// </summary>
        /// <param name="error">The exception that caused the error</param>
        /// <param name="status">The timer status when the error occurred</param>
        /// <param name="consecutiveErrors">The number of consecutive errors</param>
        /// <param name="severity">The error severity level</param>
        /// <param name="executionId">The execution identifier, if available</param>
        public TimerErrorEventArgs(Exception error, TimerStatus status, int consecutiveErrors, TimerErrorSeverity severity, Guid? executionId = null)
            : this(error, status, consecutiveErrors, executionId)
        {
            Severity = severity;
        }

        /// <summary>
        /// Adds context information to the error event
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="value">The context value</param>
        public void AddContext(string key, object value)
        {
            Context[key] = value;
        }

        /// <summary>
        /// Marks the error as handled with optional handling duration
        /// </summary>
        /// <param name="handlingDuration">The time taken to handle the error</param>
        public void MarkAsHandled(TimeSpan? handlingDuration = null)
        {
            WasHandled = true;
            HandlingDuration = handlingDuration ?? TimeSpan.Zero;
        }

        /// <summary>
        /// Determines the error severity based on the exception type
        /// </summary>
        /// <param name="error">The exception to analyze</param>
        /// <returns>The determined severity level</returns>
        private static TimerErrorSeverity DetermineErrorSeverity(Exception error)
        {
            if (error is OutOfMemoryException ||
                error is StackOverflowException ||
                error is System.AccessViolationException)
            {
                return TimerErrorSeverity.Critical;
            }

            if (error is System.InvalidOperationException ||
                error is System.ArgumentException ||
                error is System.NullReferenceException)
            {
                return TimerErrorSeverity.High;
            }

            if (error is System.TimeoutException ||
                error is System.IO.IOException ||
                error is System.Net.Sockets.SocketException)
            {
                return TimerErrorSeverity.Medium;
            }

            return TimerErrorSeverity.Low;
        }

        /// <summary>
        /// Determines the error category based on the exception type
        /// </summary>
        /// <param name="error">The exception to analyze</param>
        /// <returns>The determined error category</returns>
        private static TimerErrorCategory DetermineErrorCategory(Exception error)
        {
            if (error is System.TimeoutException)
                return TimerErrorCategory.Timeout;

            if (error is System.IO.IOException || error is System.IO.FileNotFoundException)
                return TimerErrorCategory.IO;

            if (error is System.Net.Sockets.SocketException || error is System.Net.WebException)
                return TimerErrorCategory.Network;

            if (error is System.InvalidOperationException)
                return TimerErrorCategory.Operation;

            if (error is System.ArgumentException || error is System.ArgumentNullException)
                return TimerErrorCategory.Argument;

            if (error is System.OutOfMemoryException)
                return TimerErrorCategory.Memory;

            if (error is System.Threading.ThreadAbortException)
                return TimerErrorCategory.Threading;

            return TimerErrorCategory.General;
        }

        /// <summary>
        /// Determines the appropriate recovery action based on the error and status
        /// </summary>
        /// <param name="error">The exception that occurred</param>
        /// <param name="status">The timer status when the error occurred</param>
        /// <returns>The suggested recovery action</returns>
        private static TimerErrorRecoveryAction DetermineRecoveryAction(Exception error, TimerStatus status)
        {
            if (error is System.TimeoutException)
                return TimerErrorRecoveryAction.Retry;

            if (error is System.IO.IOException || error is System.Net.Sockets.SocketException)
                return TimerErrorRecoveryAction.WaitAndRetry;

            if (error is System.InvalidOperationException)
                return TimerErrorRecoveryAction.Reset;

            if (error is System.OutOfMemoryException || error is System.StackOverflowException)
                return TimerErrorRecoveryAction.Stop;

            if (error is System.ArgumentException)
                return TimerErrorRecoveryAction.LogError;

            return TimerErrorRecoveryAction.Continue;
        }

        /// <summary>
        /// Formats the error message with additional context
        /// </summary>
        /// <param name="error">The exception that occurred</param>
        /// <param name="status">The timer status when the error occurred</param>
        /// <param name="consecutiveErrors">The number of consecutive errors</param>
        /// <returns>The formatted error message</returns>
        private static string FormatErrorMessage(Exception error, TimerStatus status, int consecutiveErrors)
        {
            return $"Timer error occurred at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} " +
                   $"(Status: {status}, Consecutive Errors: {consecutiveErrors}): " +
                   $"{error.GetType().Name}: {error.Message}";
        }

        /// <summary>
        /// Generates a unique error code for the error
        /// </summary>
        /// <param name="error">The exception that occurred</param>
        /// <param name="status">The timer status when the error occurred</param>
        /// <returns>The generated error code</returns>
        private static string GenerateErrorCode(Exception error, TimerStatus status)
        {
            var errorType = error.GetType().Name.Replace("Exception", "").ToUpper();
            var statusCode = status.ToString().ToUpper();
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            return $"TIMER_{statusCode}_{errorType}_{timestamp}";
        }

        /// <summary>
        /// Returns a string representation of the timer error event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Error Event:");
            builder.AppendLine($"  Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            builder.AppendLine($"  Error Code: {ErrorCode}");
            builder.AppendLine($"  Error Type: {Error.GetType().Name}");
            builder.AppendLine($"  Error Message: {Error.Message}");
            builder.AppendLine($"  Timer Status: {Status}");
            builder.AppendLine($"  Severity: {Severity}");
            builder.AppendLine($"  Category: {Category}");
            builder.AppendLine($"  Consecutive Errors: {ConsecutiveErrors}");
            builder.AppendLine($"  Should Trigger Circuit Breaker: {ShouldTriggerCircuitBreaker}");
            builder.AppendLine($"  Is Fatal: {IsFatal}");
            builder.AppendLine($"  Recovery Action: {RecoveryAction}");
            builder.AppendLine($"  Was Handled: {WasHandled}");

            if (ExecutionId.HasValue)
            {
                builder.AppendLine($"  Execution ID: {ExecutionId.Value}");
            }

            if (HandlingDuration > TimeSpan.Zero)
            {
                builder.AppendLine($"  Handling Duration: {HandlingDuration.TotalMilliseconds:F2}ms");
            }

            if (Context.Count > 0)
            {
                builder.AppendLine("  Context:");
                foreach (var kvp in Context)
                {
                    builder.AppendLine($"    {kvp.Key}: {kvp.Value}");
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Defines the severity levels for timer errors
    /// </summary>
    public enum TimerErrorSeverity
    {
        /// <summary>
        /// Low severity error, may be logged but doesn't affect operation
        /// </summary>
        Low = 0,

        /// <summary>
        /// Medium severity error, may affect performance but not functionality
        /// </summary>
        Medium = 1,

        /// <summary>
        /// High severity error, affects functionality but can be recovered
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical severity error, requires immediate attention and may stop the timer
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// Defines the categories for timer errors
    /// </summary>
    public enum TimerErrorCategory
    {
        /// <summary>
        /// General uncategorized error
        /// </summary>
        General = 0,

        /// <summary>
        /// Timeout-related error
        /// </summary>
        Timeout = 1,

        /// <summary>
        /// Input/Output related error
        /// </summary>
        IO = 2,

        /// <summary>
        /// Network-related error
        /// </summary>
        Network = 3,

        /// <summary>
        /// Operation-related error
        /// </summary>
        Operation = 4,

        /// <summary>
        /// Argument-related error
        /// </summary>
        Argument = 5,

        /// <summary>
        /// Memory-related error
        /// </summary>
        Memory = 6,

        /// <summary>
        /// Threading-related error
        /// </summary>
        Threading = 7,

        /// <summary>
        /// Configuration-related error
        /// </summary>
        Configuration = 8,

        /// <summary>
        /// Execution-related error
        /// </summary>
        Execution = 9
    }

    /// <summary>
    /// Defines the recovery actions for timer errors
    /// </summary>
    public enum TimerErrorRecoveryAction
    {
        /// <summary>
        /// Continue normal operation
        /// </summary>
        Continue = 0,

        /// <summary>
        /// Retry the operation immediately
        /// </summary>
        Retry = 1,

        /// <summary>
        /// Wait and retry the operation
        /// </summary>
        WaitAndRetry = 2,

        /// <summary>
        /// Reset the timer state
        /// </summary>
        Reset = 3,

        /// <summary>
        /// Log the error and continue
        /// </summary>
        LogError = 4,

        /// <summary>
        /// Stop the timer
        /// </summary>
        Stop = 5,

        /// <summary>
        /// Restart the timer
        /// </summary>
        Restart = 6,

        /// <summary>
        /// Trigger circuit breaker
        /// </summary>
        TriggerCircuitBreaker = 7,

        /// <summary>
        /// No action required
        /// </summary>
        None = 8
    }
}