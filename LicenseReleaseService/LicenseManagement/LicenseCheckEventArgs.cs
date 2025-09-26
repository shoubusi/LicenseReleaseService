using System;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Event arguments for license check scheduler events
    /// </summary>
    public class LicenseCheckSchedulerEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the event timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the scheduler status
        /// </summary>
        public SchedulerStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the previous scheduler status
        /// </summary>
        public SchedulerStatus PreviousStatus { get; set; }

        /// <summary>
        /// Gets or sets the event message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exception if the event resulted from an error
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the execution context
        /// </summary>
        public object ExecutionContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckSchedulerEventArgs class
        /// </summary>
        public LicenseCheckSchedulerEventArgs()
        {
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckSchedulerEventArgs class with specified parameters
        /// </summary>
        /// <param name="status">Current scheduler status</param>
        /// <param name="previousStatus">Previous scheduler status</param>
        /// <param name="message">Event message</param>
        public LicenseCheckSchedulerEventArgs(SchedulerStatus status, SchedulerStatus previousStatus, string message) : this()
        {
            Status = status;
            PreviousStatus = previousStatus;
            Message = message;
        }
    }

    /// <summary>
    /// Event arguments for license check operation events
    /// </summary>
    public class LicenseCheckOperationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the operation identifier
        /// </summary>
        public Guid OperationId { get; set; }

        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public LicenseCheckOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the license server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the feature name (for feature-specific operations)
        /// </summary>
        public string Feature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name (for user-specific operations)
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation priority
        /// </summary>
        public LicenseCheckOperationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the operation status
        /// </summary>
        public LicenseCheckOperationStatus OperationStatus { get; set; }

        /// <summary>
        /// Gets or sets the start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the operation duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exception if the operation failed
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the retry attempt count
        /// </summary>
        public int RetryAttempt { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses checked
        /// </summary>
        public int LicensesChecked { get; set; }

        /// <summary>
        /// Gets or sets the number of features processed
        /// </summary>
        public int FeaturesProcessed { get; set; }

        /// <summary>
        /// Gets or sets the number of users processed
        /// </summary>
        public int UsersProcessed { get; set; }

        /// <summary>
        /// Gets or sets the operation result
        /// </summary>
        public LicenseCheckResult Result { get; set; }

        /// <summary>
        /// Gets or sets the operation tags
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the execution context
        /// </summary>
        public object ExecutionContext { get; set; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckOperationEventArgs class
        /// </summary>
        public LicenseCheckOperationEventArgs()
        {
            StartTime = DateTime.Now;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckOperationEventArgs class with operation details
        /// </summary>
        /// <param name="operation">The operation</param>
        /// <param name="operationStatus">The operation status</param>
        public LicenseCheckOperationEventArgs(LicenseCheckOperation operation, LicenseCheckOperationStatus operationStatus) : this()
        {
            OperationId = operation.OperationId;
            OperationType = operation.OperationType;
            Server = operation.Server;
            Port = operation.Port;
            Feature = operation.Feature;
            User = operation.User;
            Priority = operation.Priority;
            OperationStatus = operationStatus;
            RetryAttempt = operation.RetryAttempt;
            Tags = operation.Tags.ToArray();
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckOperationEventArgs class with result details
        /// </summary>
        /// <param name="result">The operation result</param>
        public LicenseCheckOperationEventArgs(LicenseCheckResult result) : this()
        {
            OperationId = result.OperationId;
            OperationType = result.OperationType;
            Server = result.Server;
            Port = result.Port;
            Feature = result.Feature;
            User = result.User;
            Priority = result.Priority;
            OperationStatus = result.Success ? LicenseCheckOperationStatus.Completed : LicenseCheckOperationStatus.Failed;
            Success = result.Success;
            ErrorMessage = result.ErrorMessage;
            Exception = result.Exception;
            RetryAttempt = result.RetryAttempt;
            LicensesChecked = result.LicensesChecked;
            FeaturesProcessed = result.FeaturesProcessed;
            UsersProcessed = result.UsersProcessed;
            Result = result;
            Tags = result.Tags.ToArray();
        }

        /// <summary>
        /// Returns a string representation of the event arguments
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckOperationEventArgs[Id={OperationId:N}, Type={OperationType}, " +
                   $"Server={Server}:{Port}, Status={OperationStatus}, Success={Success}, " +
                   $"Duration={Duration.TotalMilliseconds:F2}ms, Licenses={LicensesChecked}, " +
                   $"Features={FeaturesProcessed}, Users={UsersProcessed}]";
        }
    }

    /// <summary>
    /// Event arguments for scheduler status change events
    /// </summary>
    public class SchedulerStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the previous status
        /// </summary>
        public SchedulerStatus PreviousStatus { get; set; }

        /// <summary>
        /// Gets or sets the new status
        /// </summary>
        public SchedulerStatus NewStatus { get; set; }

        /// <summary>
        /// Gets or sets the change timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the change reason
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exception if the change resulted from an error
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Initializes a new instance of the SchedulerStatusChangedEventArgs class
        /// </summary>
        public SchedulerStatusChangedEventArgs()
        {
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Initializes a new instance of the SchedulerStatusChangedEventArgs class with specified parameters
        /// </summary>
        /// <param name="previousStatus">Previous status</param>
        /// <param name="newStatus">New status</param>
        /// <param name="reason">Reason for the change</param>
        public SchedulerStatusChangedEventArgs(SchedulerStatus previousStatus, SchedulerStatus newStatus, string reason) : this()
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
            Reason = reason;
        }

        /// <summary>
        /// Returns a string representation of the event arguments
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"SchedulerStatusChangedEventArgs[From={PreviousStatus}, To={NewStatus}, " +
                   $"Time={Timestamp:yyyy-MM-dd HH:mm:ss}, Reason={Reason}]";
        }
    }

    /// <summary>
    /// Represents the queue status for the license check scheduler
    /// </summary>
    public class LicenseCheckQueueStatus
    {
        /// <summary>
        /// Gets or sets the number of operations currently in the queue
        /// </summary>
        public int QueueSize { get; set; }

        /// <summary>
        /// Gets or sets the number of operations currently executing
        /// </summary>
        public int ExecutingCount { get; set; }

        /// <summary>
        /// Gets or sets the number of operations completed
        /// </summary>
        public long CompletedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of operations failed
        /// </summary>
        public long FailedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of operations cancelled
        /// </summary>
        public long CancelledCount { get; set; }

        /// <summary>
        /// Gets or sets the number of operations timed out
        /// </summary>
        public long TimedOutCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum queue size observed
        /// </summary>
        public int MaxQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the average queue wait time
        /// </summary>
        public TimeSpan AverageQueueWaitTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum queue wait time
        /// </summary>
        public TimeSpan MaxQueueWaitTime { get; set; }

        /// <summary>
        /// Gets or sets the queue throughput (operations per second)
        /// </summary>
        public double Throughput { get; set; }

        /// <summary>
        /// Gets or sets the success rate
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the status timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets the total number of operations processed
        /// </summary>
        public long TotalProcessed => CompletedCount + FailedCount + CancelledCount + TimedOutCount;

        /// <summary>
        /// Gets the failure rate
        /// </summary>
        public double FailureRate => TotalProcessed > 0 ? (double)FailedCount / TotalProcessed : 0;

        /// <summary>
        /// Gets the cancellation rate
        /// </summary>
        public double CancellationRate => TotalProcessed > 0 ? (double)CancelledCount / TotalProcessed : 0;

        /// <summary>
        /// Gets the timeout rate
        /// </summary>
        public double TimeoutRate => TotalProcessed > 0 ? (double)TimedOutCount / TotalProcessed : 0;

        /// <summary>
        /// Returns a string representation of the queue status
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckQueueStatus[Queue={QueueSize}, Executing={ExecutingCount}, " +
                   $"Completed={CompletedCount}, Failed={FailedCount}, Cancelled={CancelledCount}, " +
                   $"TimedOut={TimedOutCount}, SuccessRate={SuccessRate:P2}, Throughput={Throughput:F2}/s]";
        }
    }

    /// <summary>
    /// Represents performance metrics for the license check scheduler
    /// </summary>
    public class LicenseCheckSchedulerMetrics
    {
        /// <summary>
        /// Gets or sets the start time of the scheduler
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the uptime of the scheduler
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets or sets the total number of operations processed
        /// </summary>
        public long TotalOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of successful operations
        /// </summary>
        public long SuccessfulOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of failed operations
        /// </summary>
        public long FailedOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of cancelled operations
        /// </summary>
        public long CancelledOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of timed out operations
        /// </summary>
        public long TimedOutOperations { get; set; }

        /// <summary>
        /// Gets or sets the average operation duration
        /// </summary>
        public TimeSpan AverageOperationDuration { get; set; }

        /// <summary>
        /// Gets or sets the minimum operation duration
        /// </summary>
        public TimeSpan MinOperationDuration { get; set; }

        /// <summary>
        /// Gets or sets the maximum operation duration
        /// </summary>
        public TimeSpan MaxOperationDuration { get; set; }

        /// <summary>
        /// Gets or sets the average server response time
        /// </summary>
        public TimeSpan AverageServerResponseTime { get; set; }

        /// <summary>
        /// Gets or sets the total data processed
        /// </summary>
        public long TotalDataProcessed { get; set; }

        /// <summary>
        /// Gets or sets the memory usage in bytes
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsagePercentage { get; set; }

        /// <summary>
        /// Gets or sets the number of active timers
        /// </summary>
        public int ActiveTimers { get; set; }

        /// <summary>
        /// Gets or sets the number of recurring operations
        /// </summary>
        public int RecurringOperations { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when metrics were collected
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets the success rate
        /// </summary>
        public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations : 0;

        /// <summary>
        /// Gets the failure rate
        /// </summary>
        public double FailureRate => TotalOperations > 0 ? (double)FailedOperations / TotalOperations : 0;

        /// <summary>
        /// Gets the cancellation rate
        /// </summary>
        public double CancellationRate => TotalOperations > 0 ? (double)CancelledOperations / TotalOperations : 0;

        /// <summary>
        /// Gets the timeout rate
        /// </summary>
        public double TimeoutRate => TotalOperations > 0 ? (double)TimedOutOperations / TotalOperations : 0;

        /// <summary>
        /// Gets the operations per second
        /// </summary>
        public double OperationsPerSecond => Uptime.TotalSeconds > 0 ? (double)TotalOperations / Uptime.TotalSeconds : 0;

        /// <summary>
        /// Gets the data throughput per second
        /// </summary>
        public double DataThroughputPerSecond => Uptime.TotalSeconds > 0 ? (double)TotalDataProcessed / Uptime.TotalSeconds : 0;

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckSchedulerMetrics[Uptime={Uptime}, Operations={TotalOperations}, " +
                   $"Success={SuccessfulOperations}, Failed={FailedOperations}, SuccessRate={SuccessRate:P2}, " +
                   $"AvgDuration={AverageOperationDuration.TotalMilliseconds:F2}ms, " +
                   $"OpsPerSecond={OperationsPerSecond:F2}, Memory={MemoryUsage}bytes]";
        }
    }
}