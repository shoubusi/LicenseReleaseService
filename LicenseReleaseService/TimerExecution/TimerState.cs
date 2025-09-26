using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Represents the current state of the timer execution service
    /// </summary>
    public enum TimerState
    {
        /// <summary>
        /// Timer is stopped and not running
        /// </summary>
        Stopped,

        /// <summary>
        /// Timer is running and executing periodically
        /// </summary>
        Running,

        /// <summary>
        /// Timer is paused and not executing
        /// </summary>
        Paused,

        /// <summary>
        /// Timer is currently executing an operation
        /// </summary>
        Executing,

        /// <summary>
        /// Timer is in error state due to repeated failures
        /// </summary>
        Error,

        /// <summary>
        /// Timer is in circuit breaker state due to too many consecutive errors
        /// </summary>
        CircuitBreaker,

        /// <summary>
        /// Timer is stopping and cleaning up resources
        /// </summary>
        Stopping,

        /// <summary>
        /// Timer is disposed and cannot be used
        /// </summary>
        Disposed
    }

    /// <summary>
    /// Event arguments for timer state changes
    /// </summary>
    public class TimerStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous timer state
        /// </summary>
        public TimerState PreviousState { get; }

        /// <summary>
        /// Gets the new timer state
        /// </summary>
        public TimerState NewState { get; }

        /// <summary>
        /// Gets the timestamp when the state change occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the reason for the state change, if any
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the TimerStateChangedEventArgs class
        /// </summary>
        /// <param name="previousState">The previous timer state</param>
        /// <param name="newState">The new timer state</param>
        /// <param name="reason">The reason for the state change</param>
        public TimerStateChangedEventArgs(TimerState previousState, TimerState newState, string reason = null)
        {
            PreviousState = previousState;
            NewState = newState;
            Timestamp = DateTime.UtcNow;
            Reason = reason;
        }
    }

    /// <summary>
    /// Event arguments for timer execution events
    /// </summary>
    public class TimerExecutionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the unique identifier for this execution
        /// </summary>
        public Guid ExecutionId { get; }

        /// <summary>
        /// Gets the start time of the execution
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// Gets the end time of the execution
        /// </summary>
        public DateTime EndTime { get; }

        /// <summary>
        /// Gets the duration of the execution
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets the attempt number for this execution
        /// </summary>
        public int Attempt { get; }

        /// <summary>
        /// Gets a value indicating whether the execution was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the error message if the execution failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets the number of operations processed during execution
        /// </summary>
        public int OperationsProcessed { get; set; }

        /// <summary>
        /// Gets additional execution metadata
        /// </summary>
        public System.Collections.Generic.IDictionary<string, object> Metadata { get; }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionEventArgs class
        /// </summary>
        /// <param name="executionId">The execution identifier</param>
        /// <param name="startTime">The start time</param>
        /// <param name="attempt">The attempt number</param>
        public TimerExecutionEventArgs(Guid executionId, DateTime startTime, int attempt)
        {
            ExecutionId = executionId;
            StartTime = startTime;
            Attempt = attempt;
            EndTime = startTime;
            Duration = TimeSpan.Zero;
            Success = false;
            ErrorMessage = null;
            OperationsProcessed = 0;
            Metadata = new System.Collections.Generic.Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Event arguments for timer execution errors
    /// </summary>
    public class TimerExecutionErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exception that caused the error
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Gets the number of consecutive errors
        /// </summary>
        public int ConsecutiveErrors { get; }

        /// <summary>
        /// Gets the timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the current timer state when the error occurred
        /// </summary>
        public TimerState State { get; }

        /// <summary>
        /// Gets the execution identifier if the error occurred during execution
        /// </summary>
        public Guid? ExecutionId { get; }

        /// <summary>
        /// Gets a value indicating whether the error should trigger circuit breaker
        /// </summary>
        public bool ShouldTriggerCircuitBreaker { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionErrorEventArgs class
        /// </summary>
        /// <param name="error">The exception that caused the error</param>
        /// <param name="consecutiveErrors">The number of consecutive errors</param>
        /// <param name="state">The current timer state</param>
        /// <param name="executionId">The execution identifier</param>
        public TimerExecutionErrorEventArgs(Exception error, int consecutiveErrors, TimerState state, Guid? executionId = null)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            ConsecutiveErrors = consecutiveErrors;
            Timestamp = DateTime.UtcNow;
            State = state;
            ExecutionId = executionId;
            ShouldTriggerCircuitBreaker = false;
        }
    }

    /// <summary>
    /// Performance metrics for timer execution
    /// </summary>
    public class TimerPerformanceMetrics
    {
        /// <summary>
        /// Gets the total number of executions
        /// </summary>
        public long TotalExecutions { get; internal set; }

        /// <summary>
        /// Gets the number of successful executions
        /// </summary>
        public long SuccessfulExecutions { get; internal set; }

        /// <summary>
        /// Gets the number of failed executions
        /// </summary>
        public long FailedExecutions { get; internal set; }

        /// <summary>
        /// Gets the average execution duration
        /// </summary>
        public TimeSpan AverageExecutionDuration { get; internal set; }

        /// <summary>
        /// Gets the minimum execution duration
        /// </summary>
        public TimeSpan MinExecutionDuration { get; internal set; }

        /// <summary>
        /// Gets the maximum execution duration
        /// </summary>
        public TimeSpan MaxExecutionDuration { get; internal set; }

        /// <summary>
        /// Gets the uptime of the timer
        /// </summary>
        public TimeSpan Uptime { get; internal set; }

        /// <summary>
        /// Gets the last execution time
        /// </summary>
        public DateTime? LastExecutionTime { get; internal set; }

        /// <summary>
        /// Gets the success rate as a percentage
        /// </summary>
        public double SuccessRate => TotalExecutions > 0 ? (SuccessfulExecutions * 100.0 / TotalExecutions) : 0;

        /// <summary>
        /// Gets the executions per minute rate
        /// </summary>
        public double ExecutionsPerMinute => Uptime.TotalMinutes > 0 ? TotalExecutions / Uptime.TotalMinutes : 0;

        /// <summary>
        /// Gets the current consecutive error count
        /// </summary>
        public int CurrentConsecutiveErrors { get; internal set; }

        /// <summary>
        /// Gets the maximum consecutive errors encountered
        /// </summary>
        public int MaxConsecutiveErrors { get; internal set; }

        /// <summary>
        /// Gets the timer start time
        /// </summary>
        public DateTime? StartTime { get; internal set; }

        /// <summary>
        /// Gets the timer stop time
        /// </summary>
        public DateTime? StopTime { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the TimerPerformanceMetrics class
        /// </summary>
        public TimerPerformanceMetrics()
        {
            TotalExecutions = 0;
            SuccessfulExecutions = 0;
            FailedExecutions = 0;
            AverageExecutionDuration = TimeSpan.Zero;
            MinExecutionDuration = TimeSpan.Zero;
            MaxExecutionDuration = TimeSpan.Zero;
            Uptime = TimeSpan.Zero;
            CurrentConsecutiveErrors = 0;
            MaxConsecutiveErrors = 0;
        }

        /// <summary>
        /// Returns a string representation of the performance metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Performance Metrics:");
            builder.AppendLine($"  Total Executions: {TotalExecutions}");
            builder.AppendLine($"  Successful Executions: {SuccessfulExecutions}");
            builder.AppendLine($"  Failed Executions: {FailedExecutions}");
            builder.AppendLine($"  Success Rate: {SuccessRate:F2}%");
            builder.AppendLine($"  Average Duration: {AverageExecutionDuration.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Min Duration: {MinExecutionDuration.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Max Duration: {MaxExecutionDuration.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Executions/Minute: {ExecutionsPerMinute:F2}");
            builder.AppendLine($"  Uptime: {Uptime}");
            builder.AppendLine($"  Current Consecutive Errors: {CurrentConsecutiveErrors}");
            builder.AppendLine($"  Max Consecutive Errors: {MaxConsecutiveErrors}");

            if (LastExecutionTime.HasValue)
            {
                builder.AppendLine($"  Last Execution: {LastExecutionTime.Value}");
            }

            return builder.ToString();
        }
    }
}