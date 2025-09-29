using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Defines severity levels for timer execution errors
    /// </summary>
    public enum TimerErrorSeverity
    {
        /// <summary>
        /// Low severity error, can be handled automatically
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity error, may affect operation but can recover
        /// </summary>
        Medium,

        /// <summary>
        /// High severity error, affects system operation significantly
        /// </summary>
        High,

        /// <summary>
        /// Critical severity error, system may become unstable
        /// </summary>
        Critical
    }

    /// <summary>
    /// Defines categories for timer execution errors
    /// </summary>
    public enum TimerErrorCategory
    {
        /// <summary>
        /// Timer configuration errors
        /// </summary>
        Configuration,

        /// <summary>
        /// Timer execution runtime errors
        /// </summary>
        Execution,

        /// <summary>
        /// Network connectivity errors
        /// </summary>
        Network,

        /// <summary>
        /// Process execution errors
        /// </summary>
        Process,

        /// <summary>
        /// Resource exhaustion errors
        /// </summary>
        Resource,

        /// <summary>
        /// License server errors
        /// </summary>
        LicenseServer,

        /// <summary>
        /// Timeout errors
        /// </summary>
        Timeout,

        /// <summary>
        /// Unknown or uncategorized errors
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Defines recovery actions for timer execution errors
    /// </summary>
    public enum TimerRecoveryAction
    {
        /// <summary>
        /// No action required
        /// </summary>
        None,

        /// <summary>
        /// Retry the operation
        /// </summary>
        Retry,

        /// <summary>
        /// Reset the timer state
        /// </summary>
        ResetTimer,

        /// <summary>
        /// Restart the timer service
        /// </summary>
        RestartService,

        /// <summary>
        /// Increase timer interval
        /// </summary>
        IncreaseInterval,

        /// <summary>
        /// Decrease timer interval
        /// </summary>
        DecreaseInterval,

        /// <summary>
        /// Enable circuit breaker
        /// </summary>
        EnableCircuitBreaker,

        /// <summary>
        /// Disable timer temporarily
        /// </summary>
        DisableTimer,

        /// <summary>
        /// Log and continue
        /// </summary>
        LogAndContinue,

        /// <summary>
        /// Custom recovery action
        /// </summary>
        Custom
    }

    /// <summary>
    /// Event arguments for timer recovery events
    /// </summary>
    public class TimerRecoveryEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the unique identifier for the recovery event
        /// </summary>
        public Guid RecoveryId { get; set; }

        /// <summary>
        /// Gets or sets the associated error event
        /// </summary>
        public TimerErrorEventArgs ErrorEvent { get; set; }

        /// <summary>
        /// Gets or sets the recovery action performed
        /// </summary>
        public TimerRecoveryAction Action { get; set; }

        /// <summary>
        /// Gets or sets the recovery start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the recovery end time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the recovery duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets whether recovery was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the recovery result message
        /// </summary>
        public string ResultMessage { get; set; }

        /// <summary>
        /// Gets or sets any exception that occurred during recovery
        /// </summary>
        public Exception RecoveryException { get; set; }

        /// <summary>
        /// Gets or sets additional recovery context
        /// </summary>
        public Dictionary<string, object> Context { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerRecoveryEventArgs class
        /// </summary>
        public TimerRecoveryEventArgs()
        {
            RecoveryId = Guid.NewGuid();
            StartTime = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
        }

        /// <summary>
        /// Initializes a new instance of the TimerRecoveryEventArgs class with parameters
        /// </summary>
        /// <param name="errorEvent">The associated error event</param>
        /// <param name="action">The recovery action performed</param>
        public TimerRecoveryEventArgs(TimerErrorEventArgs errorEvent, TimerRecoveryAction action)
            : this()
        {
            ErrorEvent = errorEvent ?? throw new ArgumentNullException(nameof(errorEvent));
            Action = action;
        }

        /// <summary>
        /// Marks the recovery as completed
        /// </summary>
        /// <param name="success">Whether recovery was successful</param>
        /// <param name="resultMessage">The result message</param>
        public void Complete(bool success, string resultMessage = null)
        {
            EndTime = DateTime.UtcNow;
            Duration = EndTime - StartTime;
            Success = success;
            ResultMessage = resultMessage ?? (success ? "Recovery completed successfully" : "Recovery failed");
        }

        /// <summary>
        /// Adds context information to the recovery
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="value">The context value</param>
        public void AddContext(string key, object value)
        {
            Context[key] = value;
        }

        /// <summary>
        /// Returns a string representation of the recovery event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"TimerRecoveryEventArgs[RecoveryId={RecoveryId}, " +
                   $"Action={Action}, Success={Success}, " +
                   $"Duration={Duration.TotalMilliseconds:F2}ms, " +
                   $"ErrorId={ErrorEvent?.ErrorId}, " +
                   $"Message={ResultMessage}]";
        }
    }

    /// <summary>
    /// Event arguments for timer health check events
    /// </summary>
    public class TimerHealthEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the unique identifier for the health check
        /// </summary>
        public Guid HealthCheckId { get; set; }

        /// <summary>
        /// Gets or sets the health check timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the timer state
        /// </summary>
        public TimerState TimerState { get; set; }

        /// <summary>
        /// Gets or sets whether the timer is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the health score (0-100)
        /// </summary>
        public int HealthScore { get; set; }

        /// <summary>
        /// Gets or sets the health issues found
        /// </summary>
        public List<string> Issues { get; set; }

        /// <summary>
        /// Gets or sets the health metrics
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; }

        /// <summary>
        /// Gets or sets the recommendations for improving health
        /// </summary>
        public List<string> Recommendations { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerHealthEventArgs class
        /// </summary>
        public TimerHealthEventArgs()
        {
            HealthCheckId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            Issues = new List<string>();
            Metrics = new Dictionary<string, object>();
            Recommendations = new List<string>();
            HealthScore = 100;
            IsHealthy = true;
        }

        /// <summary>
        /// Adds a health issue
        /// </summary>
        /// <param name="issue">The issue description</param>
        /// <param name="severity">The issue severity (affects health score)</param>
        public void AddIssue(string issue, TimerErrorSeverity severity = TimerErrorSeverity.Medium)
        {
            Issues.Add(issue);
            IsHealthy = false;

            // Reduce health score based on severity
            switch (severity)
            {
                case TimerErrorSeverity.Low:
                    HealthScore = Math.Max(0, HealthScore - 5);
                    break;
                case TimerErrorSeverity.Medium:
                    HealthScore = Math.Max(0, HealthScore - 15);
                    break;
                case TimerErrorSeverity.High:
                    HealthScore = Math.Max(0, HealthScore - 30);
                    break;
                case TimerErrorSeverity.Critical:
                    HealthScore = Math.Max(0, HealthScore - 50);
                    break;
            }
        }

        /// <summary>
        /// Adds a health metric
        /// </summary>
        /// <param name="key">The metric key</param>
        /// <param name="value">The metric value</param>
        public void AddMetric(string key, object value)
        {
            Metrics[key] = value;
        }

        /// <summary>
        /// Adds a recommendation
        /// </summary>
        /// <param name="recommendation">The recommendation</param>
        public void AddRecommendation(string recommendation)
        {
            Recommendations.Add(recommendation);
        }

        /// <summary>
        /// Returns a string representation of the health event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"TimerHealthEventArgs[HealthCheckId={HealthCheckId}, " +
                   $"Healthy={IsHealthy}, Score={HealthScore}, " +
                   $"Issues={Issues.Count}, State={TimerState}, " +
                   $"Timestamp={Timestamp:yyyy-MM-dd HH:mm:ss}]";
        }
    }

    /// <summary>
    /// Event arguments for timer circuit breaker events
    /// </summary>
    public class TimerCircuitBreakerEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the unique identifier for the circuit breaker event
        /// </summary>
        public Guid EventId { get; set; }

        /// <summary>
        /// Gets or sets the circuit breaker state change
        /// </summary>
        public CircuitBreakerState OldState { get; set; }

        /// <summary>
        /// Gets or sets the new circuit breaker state
        /// </summary>
        public CircuitBreakerState NewState { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the state change
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the reason for the state change
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the failure count that triggered the change
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the failure threshold
        /// </summary>
        public int FailureThreshold { get; set; }

        /// <summary>
        /// Gets or sets the cooldown period
        /// </summary>
        public TimeSpan CooldownPeriod { get; set; }

        /// <summary>
        /// Gets or sets the estimated reset time
        /// </summary>
        public DateTime? EstimatedResetTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerCircuitBreakerEventArgs class
        /// </summary>
        public TimerCircuitBreakerEventArgs()
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the TimerCircuitBreakerEventArgs class with parameters
        /// </summary>
        /// <param name="oldState">The previous state</param>
        /// <param name="newState">The new state</param>
        /// <param name="reason">The reason for the change</param>
        /// <param name="failureCount">The failure count</param>
        /// <param name="failureThreshold">The failure threshold</param>
        public TimerCircuitBreakerEventArgs(CircuitBreakerState oldState, CircuitBreakerState newState,
            string reason, int failureCount, int failureThreshold)
            : this()
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason;
            FailureCount = failureCount;
            FailureThreshold = failureThreshold;
        }

        /// <summary>
        /// Returns a string representation of the circuit breaker event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"TimerCircuitBreakerEventArgs[EventId={EventId}, " +
                   $"{OldState}->{NewState}, Failures={FailureCount}/{FailureThreshold}, " +
                   $"Reason={Reason}, ResetTime={EstimatedResetTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}]";
        }
    }

    /// <summary>
    /// Defines circuit breaker states
    /// </summary>
    public enum CircuitBreakerState
    {
        /// <summary>
        /// Circuit is closed, operations are allowed
        /// </summary>
        Closed,

        /// <summary>
        /// Circuit is open, operations are blocked
        /// </summary>
        Open,

        /// <summary>
        /// Circuit is half-open, testing if operations should be allowed
        /// </summary>
        HalfOpen
    }
}