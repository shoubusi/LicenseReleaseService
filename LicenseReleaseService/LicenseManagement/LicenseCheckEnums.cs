using System;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines the types of license check operations
    /// </summary>
    public enum LicenseCheckOperationType
    {
        /// <summary>
        /// Checks the overall server status
        /// </summary>
        ServerStatusCheck,

        /// <summary>
        /// Checks the health of the license server
        /// </summary>
        ServerHealthCheck,

        /// <summary>
        /// Checks the status of a specific license feature
        /// </summary>
        FeatureStatusCheck,

        /// <summary>
        /// Checks the status of a specific user
        /// </summary>
        UserStatusCheck,

        /// <summary>
        /// Checks for idle licenses
        /// </summary>
        IdleLicenseCheck,

        /// <summary>
        /// Checks for borrowed licenses
        /// </summary>
        BorrowedLicenseCheck,

        /// <summary>
        /// Checks license usage statistics
        /// </summary>
        UsageStatisticsCheck,

        /// <summary>
        /// Checks all available features
        /// </summary>
        AllFeaturesCheck,

        /// <summary>
        /// Checks all active users
        /// </summary>
        AllUsersCheck,

        /// <summary>
        /// Performs a comprehensive license check
        /// </summary>
        ComprehensiveCheck,

        /// <summary>
        /// Custom operation type
        /// </summary>
        Custom
    }

    /// <summary>
    /// Defines the priority levels for license check operations
    /// </summary>
    public enum LicenseCheckOperationPriority
    {
        /// <summary>
        /// Low priority operation
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority operation
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority operation
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority operation
        /// </summary>
        Critical = 3,

        /// <summary>
        /// Emergency priority operation
        /// </summary>
        Emergency = 4
    }

    /// <summary>
    /// Defines the status of license check operations
    /// </summary>
    public enum LicenseCheckOperationStatus
    {
        /// <summary>
        /// Operation is pending execution
        /// </summary>
        Pending,

        /// <summary>
        /// Operation is currently executing
        /// </summary>
        Executing,

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
        /// Operation timed out
        /// </summary>
        TimedOut,

        /// <summary>
        /// Operation is retrying
        /// </summary>
        Retrying,

        /// <summary>
        /// Operation is queued
        /// </summary>
        Queued,

        /// <summary>
        /// Operation is paused
        /// </summary>
        Paused
    }

    /// <summary>
    /// Defines the status of the license check scheduler
    /// </summary>
    public enum SchedulerStatus
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
        /// Scheduler is pausing
        /// </summary>
        Pausing,

        /// <summary>
        /// Scheduler is paused
        /// </summary>
        Paused,

        /// <summary>
        /// Scheduler is resuming
        /// </summary>
        Resuming,

        /// <summary>
        /// Scheduler is stopping
        /// </summary>
        Stopping,

        /// <summary>
        /// Scheduler is in error state
        /// </summary>
        Error,

        /// <summary>
        /// Scheduler is in circuit breaker state
        /// </summary>
        CircuitBreaker
    }

    /// <summary>
    /// Defines the type of license check
    /// </summary>
    public enum LicenseCheckType
    {
        /// <summary>
        /// Scheduled check
        /// </summary>
        Scheduled,

        /// <summary>
        /// Manual check
        /// </summary>
        Manual,

        /// <summary>
        /// Emergency check
        /// </summary>
        Emergency,

        /// <summary>
        /// Health check
        /// </summary>
        Health,

        /// <summary>
        /// Diagnostic check
        /// </summary>
        Diagnostic,

        /// <summary>
        /// Recovery check
        /// </summary>
        Recovery
    }
}