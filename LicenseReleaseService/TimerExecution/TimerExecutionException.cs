using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Exception thrown when timer execution encounters errors
    /// </summary>
    [Serializable]
    public class TimerExecutionException : Exception
    {
        /// <summary>
        /// Gets the timer state when the exception occurred
        /// </summary>
        public TimerState State { get; }

        /// <summary>
        /// Gets the execution identifier if the exception occurred during execution
        /// </summary>
        public Guid? ExecutionId { get; }

        /// <summary>
        /// Gets the number of consecutive errors when this exception occurred
        /// </summary>
        public int ConsecutiveErrors { get; }

        /// <summary>
        /// Gets a value indicating whether the exception should trigger circuit breaker
        /// </summary>
        public bool ShouldTriggerCircuitBreaker { get; }

        /// <summary>
        /// Gets the timer interval when the exception occurred
        /// </summary>
        public TimeSpan? TimerInterval { get; }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionException class
        /// </summary>
        public TimerExecutionException()
            : base("Timer execution failed")
        {
            State = TimerState.Error;
            ConsecutiveErrors = 1;
            ShouldTriggerCircuitBreaker = false;
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionException class with a specified error message
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        public TimerExecutionException(string message)
            : base(message)
        {
            State = TimerState.Error;
            ConsecutiveErrors = 1;
            ShouldTriggerCircuitBreaker = false;
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionException class with a specified error message and inner exception
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public TimerExecutionException(string message, Exception innerException)
            : base(message, innerException)
        {
            State = TimerState.Error;
            ConsecutiveErrors = 1;
            ShouldTriggerCircuitBreaker = false;
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionException class with detailed timer information
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="state">The timer state when the exception occurred</param>
        /// <param name="executionId">The execution identifier</param>
        /// <param name="consecutiveErrors">The number of consecutive errors</param>
        /// <param name="shouldTriggerCircuitBreaker">Whether this should trigger circuit breaker</param>
        /// <param name="timerInterval">The timer interval</param>
        /// <param name="innerException">The inner exception</param>
        public TimerExecutionException(string message, TimerState state, Guid? executionId, int consecutiveErrors,
            bool shouldTriggerCircuitBreaker, TimeSpan? timerInterval = null, Exception innerException = null)
            : base(message, innerException)
        {
            State = state;
            ExecutionId = executionId;
            ConsecutiveErrors = consecutiveErrors;
            ShouldTriggerCircuitBreaker = shouldTriggerCircuitBreaker;
            TimerInterval = timerInterval;
        }

        /// <summary>
        /// Creates a TimerExecutionException for timer start failures
        /// </summary>
        /// <param name="innerException">The inner exception</param>
        /// <param name="interval">The timer interval</param>
        /// <returns>TimerExecutionException for start failure</returns>
        public static TimerExecutionException StartFailure(Exception innerException, TimeSpan? interval = null)
        {
            return new TimerExecutionException(
                $"Failed to start timer: {innerException.Message}",
                TimerState.Error,
                null,
                1,
                false,
                interval,
                innerException);
        }

        /// <summary>
        /// Creates a TimerExecutionException for timer stop failures
        /// </summary>
        /// <param name="innerException">The inner exception</param>
        /// <returns>TimerExecutionException for stop failure</returns>
        public static TimerExecutionException StopFailure(Exception innerException)
        {
            return new TimerExecutionException(
                $"Failed to stop timer: {innerException.Message}",
                TimerState.Error,
                null,
                1,
                false,
                null,
                innerException);
        }

        /// <summary>
        /// Creates a TimerExecutionException for execution failures
        /// </summary>
        /// <param name="executionId">The execution identifier</param>
        /// <param name="innerException">The inner exception</param>
        /// <param name="consecutiveErrors">The number of consecutive errors</param>
        /// <param name="shouldTriggerCircuitBreaker">Whether this should trigger circuit breaker</param>
        /// <returns>TimerExecutionException for execution failure</returns>
        public static TimerExecutionException ExecutionFailure(Guid executionId, Exception innerException, int consecutiveErrors, bool shouldTriggerCircuitBreaker)
        {
            return new TimerExecutionException(
                $"Timer execution failed: {innerException.Message}",
                TimerState.Error,
                executionId,
                consecutiveErrors,
                shouldTriggerCircuitBreaker,
                null,
                innerException);
        }

        /// <summary>
        /// Creates a TimerExecutionException for circuit breaker activation
        /// </summary>
        /// <param name="consecutiveErrors">The number of consecutive errors</param>
        /// <param name="threshold">The circuit breaker threshold</param>
        /// <returns>TimerExecutionException for circuit breaker activation</returns>
        public static TimerExecutionException CircuitBreakerActivated(int consecutiveErrors, int threshold)
        {
            return new TimerExecutionException(
                $"Circuit breaker activated after {consecutiveErrors} consecutive errors (threshold: {threshold})",
                TimerState.CircuitBreaker,
                null,
                consecutiveErrors,
                true);
        }

        /// <summary>
        /// Creates a TimerExecutionException for invalid timer state operations
        /// </summary>
        /// <param name="operation">The operation that was attempted</param>
        /// <param name="currentState">The current timer state</param>
        /// <param name="requiredState">The required state for the operation</param>
        /// <returns>TimerExecutionException for invalid state</returns>
        public static TimerExecutionException InvalidStateOperation(string operation, TimerState currentState, TimerState requiredState)
        {
            return new TimerExecutionException(
                $"Cannot {operation} while timer is in {currentState} state. Required state: {requiredState}",
                currentState,
                null,
                1,
                false);
        }

        /// <summary>
        /// Creates a TimerExecutionException for timer disposal attempts
        /// </summary>
        /// <returns>TimerExecutionException for disposal attempt</returns>
        public static TimerExecutionException TimerDisposed()
        {
            return new TimerExecutionException(
                "Timer has been disposed and cannot be used",
                TimerState.Disposed,
                null,
                1,
                false);
        }

        /// <summary>
        /// Creates a TimerExecutionException for interval validation failures
        /// </summary>
        /// <param name="interval">The invalid interval</param>
        /// <returns>TimerExecutionException for invalid interval</returns>
        public static TimerExecutionException InvalidInterval(TimeSpan interval)
        {
            return new TimerExecutionException(
                $"Invalid timer interval: {interval}. Interval must be greater than zero.",
                TimerState.Error,
                null,
                1,
                false);
        }

        /// <summary>
        /// Creates a TimerExecutionException for timeout scenarios
        /// </summary>
        /// <param name="executionId">The execution identifier</param>
        /// <param name="timeout">The timeout that was exceeded</param>
        /// <param name="actualDuration">The actual execution duration</param>
        /// <returns>TimerExecutionException for timeout</returns>
        public static TimerExecutionException ExecutionTimeout(Guid executionId, TimeSpan timeout, TimeSpan actualDuration)
        {
            return new TimerExecutionException(
                $"Timer execution {executionId} timed out after {timeout.TotalSeconds:F1} seconds (actual duration: {actualDuration.TotalSeconds:F1} seconds)",
                TimerState.Error,
                executionId,
                1,
                false);
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionException class with serialization data
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">The streaming context</param>
        protected TimerExecutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            State = (TimerState)info.GetInt32(nameof(State));
            ExecutionId = (Guid)info.GetValue(nameof(ExecutionId), typeof(Guid));
            ConsecutiveErrors = info.GetInt32(nameof(ConsecutiveErrors));
            ShouldTriggerCircuitBreaker = info.GetBoolean(nameof(ShouldTriggerCircuitBreaker));
            TimerInterval = (TimeSpan)info.GetValue(nameof(TimerInterval), typeof(TimeSpan));
        }

        /// <summary>
        /// Sets the serialization data for the exception
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">The streaming context</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(State), (int)State);
            info.AddValue(nameof(ExecutionId), ExecutionId ?? Guid.Empty);
            info.AddValue(nameof(ConsecutiveErrors), ConsecutiveErrors);
            info.AddValue(nameof(ShouldTriggerCircuitBreaker), ShouldTriggerCircuitBreaker);
            info.AddValue(nameof(TimerInterval), TimerInterval);
        }

        /// <summary>
        /// Returns a string representation of the timer execution exception
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"Timer Execution Details:");
            builder.AppendLine($"  State: {State}");
            builder.AppendLine($"  Execution ID: {ExecutionId?.ToString() ?? "N/A"}");
            builder.AppendLine($"  Consecutive Errors: {ConsecutiveErrors}");
            builder.AppendLine($"  Should Trigger Circuit Breaker: {ShouldTriggerCircuitBreaker}");

            if (TimerInterval.HasValue)
            {
                builder.AppendLine($"  Timer Interval: {TimerInterval.Value.TotalMilliseconds:F2}ms");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets a user-friendly error message
        /// </summary>
        /// <returns>User-friendly error message</returns>
        public string GetUserFriendlyMessage()
        {
            if (ShouldTriggerCircuitBreaker)
            {
                return "The timer service has encountered too many errors and has been temporarily stopped for safety. It will automatically restart after a cooldown period.";
            }

            if (State == TimerState.Disposed)
            {
                return "The timer service has been disposed and is no longer available.";
            }

            if (State == TimerState.CircuitBreaker)
            {
                return "The timer service is currently in circuit breaker mode due to repeated failures. It will restart automatically after the cooldown period.";
            }

            return Message;
        }
    }
}