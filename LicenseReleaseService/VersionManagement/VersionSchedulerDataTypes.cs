using System;
using System.Collections.Generic;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Version scheduler status
    /// </summary>
    public enum VersionSchedulerStatus
    {
        /// <summary>
        /// Scheduler is stopped
        /// </summary>
        Stopped,

        /// <summary>
        /// Scheduler is starting
        /// </summary>
        Starting,

        /// <summary>
        /// Scheduler is running
        /// </summary>
        Running,

        /// <summary>
        /// Scheduler is stopping
        /// </summary>
        Stopping,

        /// <summary>
        /// Scheduler is in error state
        /// </summary>
        Error,

        /// <summary>
        /// Scheduler is disposed
        /// </summary>
        Disposed
    }

    /// <summary>
    /// Scheduled operation status
    /// </summary>
    public enum ScheduledOperationStatus
    {
        /// <summary>
        /// Operation is pending execution
        /// </summary>
        Pending,

        /// <summary>
        /// Operation is running
        /// </summary>
        Running,

        /// <summary>
        /// Operation completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Operation failed
        /// </summary>
        Failed,

        /// <summary>
        /// Operation was cancelled
        /// </summary>
        Cancelled,

        /// <summary>
        /// Operation is paused
        /// </summary>
        Paused
    }

    /// <summary>
    /// Represents a scheduled version operation
    /// </summary>
    public class ScheduledVersionOperation
    {
        /// <summary>
        /// Gets or sets the operation ID
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public VersionOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the interval
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Gets or sets the operation parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Gets or sets when the operation was scheduled
        /// </summary>
        public DateTime ScheduledAt { get; set; }

        /// <summary>
        /// Gets or sets the operation status
        /// </summary>
        public ScheduledOperationStatus Status { get; set; }

        /// <summary>
        /// Gets or sets when the operation was last executed
        /// </summary>
        public DateTime? LastExecutedAt { get; set; }

        /// <summary>
        /// Gets or sets when the operation was completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets when the operation was cancelled
        /// </summary>
        public DateTime? CancelledAt { get; set; }

        /// <summary>
        /// Gets or sets the number of times this operation has been executed
        /// </summary>
        public int ExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of successful executions
        /// </summary>
        public int SuccessfulExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of failed executions
        /// </summary>
        public int FailedExecutions { get; set; }

        /// <summary>
        /// Gets or sets whether this is a one-time operation
        /// </summary>
        public bool IsOneTime { get; set; }

        /// <summary>
        /// Gets or sets the next scheduled execution time
        /// </summary>
        public DateTime? NextExecutionAt { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents a version operation request
    /// </summary>
    public class VersionOperationRequest
    {
        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public VersionOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the operation parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Gets or sets when the operation was requested
        /// </summary>
        public DateTime RequestedAt { get; set; }

        /// <summary>
        /// Gets or sets whether this is an immediate execution request
        /// </summary>
        public bool IsImmediate { get; set; }

        /// <summary>
        /// Gets or sets the request priority
        /// </summary>
        public AllocationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the request timeout
        /// </summary>
        public TimeSpan Timeout { get; set; }
    }

    /// <summary>
    /// Version scheduler options
    /// </summary>
    public class VersionSchedulerOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of concurrent schedules
        /// </summary>
        public int MaxConcurrentSchedules { get; set; } = 10;

        /// <summary>
        /// Gets or sets the minimum interval for version timers
        /// </summary>
        public TimeSpan MinVersionInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum interval for version timers
        /// </summary>
        public TimeSpan MaxVersionInterval { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Gets or sets the maximum concurrent operations per version
        /// </summary>
        public int MaxConcurrentOperationsPerVersion { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether to enable circuit breaker for version timers
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum consecutive errors before triggering circuit breaker
        /// </summary>
        public int MaxConsecutiveErrors { get; set; } = 5;

        /// <summary>
        /// Gets or sets the circuit breaker cooldown period
        /// </summary>
        public TimeSpan CircuitBreakerCooldown { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether to enable auto-restart after circuit breaker
        /// </summary>
        public bool EnableAutoRestart { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to prevent execution overlap
        /// </summary>
        public bool PreventExecutionOverlap { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to stop on unhandled exceptions
        /// </summary>
        public bool StopOnUnhandledException { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable detailed logging
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum execution history to keep
        /// </summary>
        public int MaxExecutionHistory { get; set; } = 100;

        /// <summary>
        /// Gets or sets the disposal grace period
        /// </summary>
        public TimeSpan DisposalGracePeriod { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether to enable performance optimization for timers
        /// </summary>
        public bool EnableTimerPerformanceOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable maintenance operations
        /// </summary>
        public bool EnableMaintenanceOperations { get; set; } = true;

        /// <summary>
        /// Gets or sets the maintenance interval in minutes
        /// </summary>
        public int MaintenanceIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the default operation timeout in minutes
        /// </summary>
        public int DefaultOperationTimeoutMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets the expected operation duration in seconds
        /// </summary>
        public int ExpectedOperationDurationSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Version scheduler metrics
    /// </summary>
    public class VersionSchedulerMetrics
    {
        /// <summary>
        /// Gets or sets when the metrics were collected
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the current scheduler status
        /// </summary>
        public VersionSchedulerStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the total number of scheduled operations
        /// </summary>
        public int TotalScheduledOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of active version timers
        /// </summary>
        public int ActiveVersionTimers { get; set; }

        /// <summary>
        /// Gets or sets the number of queued operations
        /// </summary>
        public int QueuedOperations { get; set; }

        /// <summary>
        /// Gets or sets the maximum concurrent schedules
        /// </summary>
        public int MaxConcurrentSchedules { get; set; }

        /// <summary>
        /// Gets or sets the operation history summary
        /// </summary>
        public Dictionary<string, int> OperationHistory { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets or sets version-specific timer metrics
        /// </summary>
        public Dictionary<string, VersionTimerMetrics> VersionMetrics { get; set; } = new Dictionary<string, VersionTimerMetrics>();
    }

    /// <summary>
    /// Version-specific timer metrics
    /// </summary>
    public class VersionTimerMetrics
    {
        /// <summary>
        /// Gets or sets the total number of executions
        /// </summary>
        public long TotalExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of successful executions
        /// </summary>
        public long SuccessfulExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of failed executions
        /// </summary>
        public long FailedExecutions { get; set; }

        /// <summary>
        /// Gets or sets the average execution duration
        /// </summary>
        public TimeSpan AverageExecutionDuration { get; set; }

        /// <summary>
        /// Gets or sets the current timer interval
        /// </summary>
        public TimeSpan CurrentInterval { get; set; }

        /// <summary>
        /// Gets or sets the timer state
        /// </summary>
        public TimerState State { get; set; }

        /// <summary>
        /// Gets or sets the success rate (0.0 to 1.0)
        /// </summary>
        public double SuccessRate
        {
            get
            {
                if (TotalExecutions == 0)
                    return 0.0;
                return (double)SuccessfulExecutions / TotalExecutions;
            }
        }
    }

    /// <summary>
    /// Event arguments for version scheduled events
    /// </summary>
    public class VersionScheduledEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the operation ID
        /// </summary>
        public string OperationId { get; }

        /// <summary>
        /// Gets the operation type
        /// </summary>
        public VersionOperationType OperationType { get; }

        /// <summary>
        /// Gets the version
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the interval
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Gets when the operation was scheduled
        /// </summary>
        public DateTime ScheduledAt { get; }

        /// <summary>
        /// Gets whether this is a one-time operation
        /// </summary>
        public bool IsOneTime { get; }

        /// <summary>
        /// Initializes a new instance of the VersionScheduledEventArgs class
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="version">The version</param>
        /// <param name="interval">The interval</param>
        /// <param name="scheduledAt">When the operation was scheduled</param>
        /// <param name="isOneTime">Whether this is a one-time operation</param>
        public VersionScheduledEventArgs(
            string operationId,
            VersionOperationType operationType,
            string version,
            TimeSpan interval,
            DateTime scheduledAt,
            bool isOneTime = false)
        {
            OperationId = operationId;
            OperationType = operationType;
            Version = version;
            Interval = interval;
            ScheduledAt = scheduledAt;
            IsOneTime = isOneTime;
        }
    }

    /// <summary>
    /// Event arguments for version operation events
    /// </summary>
    public class VersionOperationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the operation ID
        /// </summary>
        public string OperationId { get; }

        /// <summary>
        /// Gets the operation type
        /// </summary>
        public VersionOperationType OperationType { get; }

        /// <summary>
        /// Gets the version
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets when the operation started
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// Gets when the operation ended (if completed)
        /// </summary>
        public DateTime? EndTime { get; }

        /// <summary>
        /// Gets the operation duration (if completed)
        /// </summary>
        public TimeSpan? Duration { get; }

        /// <summary>
        /// Gets whether the operation was successful
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Initializes a new instance of the VersionOperationEventArgs class for operation start
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="version">The version</param>
        /// <param name="startTime">When the operation started</param>
        public VersionOperationEventArgs(
            string operationId,
            VersionOperationType operationType,
            string version,
            DateTime startTime)
        {
            OperationId = operationId;
            OperationType = operationType;
            Version = version;
            StartTime = startTime;
        }

        /// <summary>
        /// Initializes a new instance of the VersionOperationEventArgs class for operation completion
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="version">The version</param>
        /// <param name="startTime">When the operation started</param>
        /// <param name="endTime">When the operation ended</param>
        /// <param name="duration">The operation duration</param>
        /// <param name="success">Whether the operation was successful</param>
        public VersionOperationEventArgs(
            string operationId,
            VersionOperationType operationType,
            string version,
            DateTime startTime,
            DateTime endTime,
            TimeSpan duration,
            bool success)
        {
            OperationId = operationId;
            OperationType = operationType;
            Version = version;
            StartTime = startTime;
            EndTime = endTime;
            Duration = duration;
            Success = success;
        }
    }

    /// <summary>
    /// Event arguments for version operation error events
    /// </summary>
    public class VersionOperationErrorEventArgs : VersionOperationEventArgs
    {
        /// <summary>
        /// Gets the error message
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Gets the exception that caused the error (if available)
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Initializes a new instance of the VersionOperationErrorEventArgs class
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="version">The version</param>
        /// <param name="startTime">When the operation started</param>
        /// <param name="endTime">When the operation ended</param>
        /// <param name="duration">The operation duration</param>
        /// <param name="errorMessage">The error message</param>
        /// <param name="exception">The exception that caused the error</param>
        public VersionOperationErrorEventArgs(
            string operationId,
            VersionOperationType operationType,
            string version,
            DateTime startTime,
            DateTime endTime,
            TimeSpan duration,
            string errorMessage,
            Exception exception = null)
            : base(operationId, operationType, version, startTime, endTime, duration, false)
        {
            ErrorMessage = errorMessage;
            Exception = exception;
        }
    }

    /// <summary>
    /// Event arguments for version scheduler status change events
    /// </summary>
    public class VersionSchedulerStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous status
        /// </summary>
        public VersionSchedulerStatus PreviousStatus { get; }

        /// <summary>
        /// Gets the new status
        /// </summary>
        public VersionSchedulerStatus NewStatus { get; }

        /// <summary>
        /// Gets the reason for the status change
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets when the status changed
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the VersionSchedulerStatusChangedEventArgs class
        /// </summary>
        /// <param name="previousStatus">The previous status</param>
        /// <param name="newStatus">The new status</param>
        /// <param name="reason">The reason for the status change</param>
        public VersionSchedulerStatusChangedEventArgs(
            VersionSchedulerStatus previousStatus,
            VersionSchedulerStatus newStatus,
            string reason)
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }
    }
}