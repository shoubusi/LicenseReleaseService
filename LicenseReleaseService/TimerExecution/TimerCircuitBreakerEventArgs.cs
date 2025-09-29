using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for timer circuit breaker events
    /// </summary>
    public class TimerCircuitBreakerEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the unique identifier for the circuit breaker event
        /// </summary>
        public Guid EventId { get; set; }

        /// <summary>
        /// Gets the previous state of the circuit breaker
        /// </summary>
        public CircuitBreakerState PreviousState { get; set; }

        /// <summary>
        /// Gets the new state of the circuit breaker
        /// </summary>
        public CircuitBreakerState NewState { get; set; }

        /// <summary>
        /// Gets the timestamp when the state change occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the reason for the state change
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets the number of failures that triggered the state change
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets the exception that caused the state change (if applicable)
        /// </summary>
        public Exception TriggerException { get; set; }

        /// <summary>
        /// Gets a value indicating whether this is a manual state change
        /// </summary>
        public bool IsManual { get; set; }

        /// <summary>
        /// Gets the suggested retry time (if circuit is open)
        /// </summary>
        public DateTime? RetryAfter { get; set; }

        /// <summary>
        /// Gets additional context information
        /// </summary>
        public System.Collections.Generic.IDictionary<string, object> Context { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerCircuitBreakerEventArgs class
        /// </summary>
        public TimerCircuitBreakerEventArgs()
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            Context = new System.Collections.Generic.Dictionary<string, object>();
        }

        /// <summary>
        /// Initializes a new instance of the TimerCircuitBreakerEventArgs class with specified parameters
        /// </summary>
        /// <param name="previousState">The previous circuit breaker state</param>
        /// <param name="newState">The new circuit breaker state</param>
        /// <param name="reason">The reason for the state change</param>
        public TimerCircuitBreakerEventArgs(CircuitBreakerState previousState, CircuitBreakerState newState, string reason)
            : this()
        {
            PreviousState = previousState;
            NewState = newState;
            Reason = reason;
        }
    }
}