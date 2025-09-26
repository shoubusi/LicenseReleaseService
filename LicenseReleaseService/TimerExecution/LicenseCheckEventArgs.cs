using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for license check events
    /// </summary>
    public class LicenseCheckEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the operation ID associated with this event
        /// </summary>
        public string OperationId { get; }

        /// <summary>
        /// Gets the type of license check operation
        /// </summary>
        public LicenseCheckOperationType OperationType { get; }

        /// <summary>
        /// Gets the target SolidWorks version
        /// </summary>
        public string TargetVersion { get; }

        /// <summary>
        /// Gets the license server address
        /// </summary>
        public string LicenseServer { get; }

        /// <summary>
        /// Gets the license server port
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the timestamp when this event occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the event type
        /// </summary>
        public LicenseCheckEventType EventType { get; }

        /// <summary>
        /// Gets the license check result (available for completion events)
        /// </summary>
        public LicenseCheckResult Result { get; }

        /// <summary>
        /// Gets the execution duration (available for completion events)
        /// </summary>
        public TimeSpan? Duration { get; }

        /// <summary>
        /// Gets the user who initiated the operation
        /// </summary>
        public string InitiatingUser { get; }

        /// <summary>
        /// Gets the computer name associated with the operation
        /// </summary>
        public string ComputerName { get; }

        /// <summary>
        /// Gets additional event data
        /// </summary>
        public IReadOnlyDictionary<string, object> AdditionalData { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckEventArgs class
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="targetVersion">The target version</param>
        /// <param name="licenseServer">The license server</param>
        /// <param name="port">The port</param>
        /// <param name="eventType">The event type</param>
        /// <param name="initiatingUser">The initiating user</param>
        /// <param name="computerName">The computer name</param>
        public LicenseCheckEventArgs(
            string operationId,
            LicenseCheckOperationType operationType,
            string targetVersion,
            string licenseServer,
            int port,
            LicenseCheckEventType eventType,
            string initiatingUser = null,
            string computerName = null)
            : this(operationId, operationType, targetVersion, licenseServer, port, eventType, null, null, initiatingUser, computerName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckEventArgs class with result
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="targetVersion">The target version</param>
        /// <param name="licenseServer">The license server</param>
        /// <param name="port">The port</param>
        /// <param name="eventType">The event type</param>
        /// <param name="result">The license check result</param>
        /// <param name="additionalData">Additional event data</param>
        /// <param name="initiatingUser">The initiating user</param>
        /// <param name="computerName">The computer name</param>
        public LicenseCheckEventArgs(
            string operationId,
            LicenseCheckOperationType operationType,
            string targetVersion,
            string licenseServer,
            int port,
            LicenseCheckEventType eventType,
            LicenseCheckResult result = null,
            IDictionary<string, object> additionalData = null,
            string initiatingUser = null,
            string computerName = null)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            OperationType = operationType;
            TargetVersion = targetVersion ?? throw new ArgumentNullException(nameof(targetVersion));
            LicenseServer = licenseServer ?? throw new ArgumentNullException(nameof(licenseServer));
            Port = port;
            EventType = eventType;
            Timestamp = DateTime.UtcNow;
            Result = result;
            Duration = result?.Duration;
            InitiatingUser = initiatingUser ?? "system";
            ComputerName = computerName ?? Environment.MachineName;
            AdditionalData = additionalData != null ? new Dictionary<string, object>(additionalData).AsReadOnly() : new Dictionary<string, object>().AsReadOnly();
        }

        /// <summary>
        /// Returns a string representation of the event arguments
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckEventArgs [Id={OperationId}, Type={EventType}, Operation={OperationType}, Version={TargetVersion}]";
        }

        /// <summary>
        /// Gets a detailed string representation of the event arguments
        /// </summary>
        /// <returns>Detailed string representation</returns>
        public string ToDetailedString()
        {
            var resultText = Result != null ?
                $"\n  Result: {Result.IsSuccess ? "Success" : "Failed"}" :
                "";
            var durationText = Duration.HasValue ?
                $"\n  Duration: {Duration.Value.TotalMilliseconds:F0}ms" :
                "";

            return $@"LicenseCheckEventArgs Details:
  Operation ID: {OperationId}
  Event Type: {EventType}
  Operation Type: {OperationType}
  Target Version: {TargetVersion}
  License Server: {LicenseServer}:{Port}
  Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss UTC}
  Initiating User: {InitiatingUser}
  Computer Name: {ComputerName}{resultText}{durationText}";
        }
    }

    /// <summary>
    /// Event arguments for license check error events
    /// </summary>
    public class LicenseCheckErrorEventArgs : LicenseCheckEventArgs
    {
        /// <summary>
        /// Gets the error information
        /// </summary>
        public LicenseCheckErrorInfo Error { get; }

        /// <summary>
        /// Gets the error severity level
        /// </summary>
        public LicenseCheckErrorSeverity Severity { get; }

        /// <summary>
        /// Gets a value indicating whether the operation can be retried
        /// </summary>
        public bool CanRetry { get; }

        /// <summary>
        /// Gets the recommended retry delay
        /// </summary>
        public TimeSpan? RecommendedRetryDelay { get; }

        /// <summary>
        /// Gets the error category
        /// </summary>
        public string ErrorCategory { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckErrorEventArgs class
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="targetVersion">The target version</param>
        /// <param name="licenseServer">The license server</param>
        /// <param name="port">The port</param>
        /// <param name="error">The error information</param>
        /// <param name="severity">The error severity</param>
        /// <param name="canRetry">Whether the operation can be retried</param>
        /// <param name="errorCategory">The error category</param>
        /// <param name="recommendedRetryDelay">The recommended retry delay</param>
        /// <param name="initiatingUser">The initiating user</param>
        /// <param name="computerName">The computer name</param>
        public LicenseCheckErrorEventArgs(
            string operationId,
            LicenseCheckOperationType operationType,
            string targetVersion,
            string licenseServer,
            int port,
            LicenseCheckErrorInfo error,
            LicenseCheckErrorSeverity severity = LicenseCheckErrorSeverity.Error,
            bool canRetry = true,
            string errorCategory = null,
            TimeSpan? recommendedRetryDelay = null,
            string initiatingUser = null,
            string computerName = null)
            : base(operationId, operationType, targetVersion, licenseServer, port, LicenseCheckEventType.Error, initiatingUser, computerName)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Severity = severity;
            CanRetry = canRetry;
            RecommendedRetryDelay = recommendedRetryDelay;
            ErrorCategory = errorCategory ?? "General";
        }

        /// <summary>
        /// Creates a LicenseCheckErrorEventArgs from an exception
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="targetVersion">The target version</param>
        /// <param name="licenseServer">The license server</param>
        /// <param name="port">The port</param>
        /// <param name="exception">The exception</param>
        /// <param name="severity">The error severity</param>
        /// <param name="canRetry">Whether the operation can be retried</param>
        /// <param name="errorCategory">The error category</param>
        /// <param name="recommendedRetryDelay">The recommended retry delay</param>
        /// <param name="initiatingUser">The initiating user</param>
        /// <param name="computerName">The computer name</param>
        /// <returns>A new LicenseCheckErrorEventArgs instance</returns>
        public static LicenseCheckErrorEventArgs FromException(
            string operationId,
            LicenseCheckOperationType operationType,
            string targetVersion,
            string licenseServer,
            int port,
            Exception exception,
            LicenseCheckErrorSeverity severity = LicenseCheckErrorSeverity.Error,
            bool canRetry = true,
            string errorCategory = null,
            TimeSpan? recommendedRetryDelay = null,
            string initiatingUser = null,
            string computerName = null)
        {
            var error = LicenseCheckErrorInfo.FromException(exception);
            return new LicenseCheckErrorEventArgs(
                operationId,
                operationType,
                targetVersion,
                licenseServer,
                port,
                error,
                severity,
                canRetry,
                errorCategory,
                recommendedRetryDelay,
                initiatingUser,
                computerName);
        }

        /// <summary>
        /// Returns a string representation of the error event arguments
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckErrorEventArgs [Id={OperationId}, Severity={Severity}, Error={Error.Message}]";
        }

        /// <summary>
        /// Gets a detailed string representation of the error event arguments
        /// </summary>
        /// <returns>Detailed string representation</returns>
        public new string ToDetailedString()
        {
            var retryText = RecommendedRetryDelay.HasValue ?
                $"\n  Recommended Retry Delay: {RecommendedRetryDelay.Value.TotalSeconds:F0}s" :
                "";
            var canRetryText = CanRetry ?
                "\n  Can Retry: Yes" :
                "\n  Can Retry: No";

            return $@"LicenseCheckErrorEventArgs Details:
  Operation ID: {OperationId}
  Error Severity: {Severity}
  Error Category: {ErrorCategory}
  Error Message: {Error.Message}
  Error Type: {Error.ErrorType}
  Error Code: {Error.ErrorCode}
  Can Retry: {CanRetry}{retryText}{canRetryText}

{base.ToDetailedString()}";
        }
    }

    /// <summary>
    /// Event arguments for license check queue events
    /// </summary>
    public class LicenseCheckQueueEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the queue event type
        /// </summary>
        public LicenseCheckQueueEventType QueueEventType { get; }

        /// <summary>
        /// Gets the current queue size
        /// </summary>
        public int QueueSize { get; }

        /// <summary>
        /// Gets the maximum queue capacity
        /// </summary>
        public int MaxCapacity { get; }

        /// <summary>
        /// Gets the operation associated with this event (if applicable)
        /// </summary>
        public LicenseCheckOperation Operation { get; }

        /// <summary>
        /// Gets the queue statistics
        /// </summary>
        public LicenseCheckQueueStatistics Statistics { get; }

        /// <summary>
        /// Gets the timestamp when this event occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets additional queue event data
        /// </summary>
        public IReadOnlyDictionary<string, object> AdditionalData { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckQueueEventArgs class
        /// </summary>
        /// <param name="queueEventType">The queue event type</param>
        /// <param name="queueSize">The current queue size</param>
        /// <param name="maxCapacity">The maximum queue capacity</param>
        /// <param name="statistics">The queue statistics</param>
        /// <param name="operation">The associated operation</param>
        /// <param name="additionalData">Additional event data</param>
        public LicenseCheckQueueEventArgs(
            LicenseCheckQueueEventType queueEventType,
            int queueSize,
            int maxCapacity,
            LicenseCheckQueueStatistics statistics = null,
            LicenseCheckOperation operation = null,
            IDictionary<string, object> additionalData = null)
        {
            QueueEventType = queueEventType;
            QueueSize = queueSize;
            MaxCapacity = maxCapacity;
            Operation = operation;
            Statistics = statistics ?? new LicenseCheckQueueStatistics();
            Timestamp = DateTime.UtcNow;
            AdditionalData = additionalData != null ? new Dictionary<string, object>(additionalData).AsReadOnly() : new Dictionary<string, object>().AsReadOnly();
        }

        /// <summary>
        /// Gets the queue utilization percentage
        /// </summary>
        public double QueueUtilizationPercentage
        {
            get
            {
                if (MaxCapacity == 0)
                    return 0;

                return (double)QueueSize / MaxCapacity * 100;
            }
        }

        /// <summary>
        /// Returns a string representation of the queue event arguments
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckQueueEventArgs [Event={QueueEventType}, Size={QueueSize}/{MaxCapacity}]";
        }

        /// <summary>
        /// Gets a detailed string representation of the queue event arguments
        /// </summary>
        /// <returns>Detailed string representation</returns>
        public string ToDetailedString()
        {
            var operationText = Operation != null ?
                $"\n  Operation: {Operation.OperationId} ({Operation.OperationType})" :
                "";

            return $@"LicenseCheckQueueEventArgs Details:
  Queue Event Type: {QueueEventType}
  Queue Size: {QueueSize}
  Max Capacity: {MaxCapacity}
  Queue Utilization: {QueueUtilizationPercentage:F1}%
  Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss UTC}{operationText}
  Statistics: {Statistics.ToSummaryString()}";
        }
    }

    /// <summary>
    /// License check event type enumeration
    /// </summary>
    public enum LicenseCheckEventType
    {
        /// <summary>
        /// License check started
        /// </summary>
        Started,

        /// <summary>
        /// License check completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// License check encountered an error
        /// </summary>
        Error,

        /// <summary>
        /// License check was cancelled
        /// </summary>
        Cancelled,

        /// <summary>
        /// License check timed out
        /// </summary>
        Timeout,

        /// <summary>
        /// License check was skipped
        /// </summary>
        Skipped,

        /// <summary>
        /// License check was retried
        /// </summary>
        Retried,

        /// <summary>
        /// License check was queued
        /// </summary>
        Queued,

        /// <summary>
        /// License check was dequeued
        /// </summary>
        Dequeued
    }

    /// <summary>
    /// License check error severity enumeration
    /// </summary>
    public enum LicenseCheckErrorSeverity
    {
        /// <summary>
        /// Informational message
        /// </summary>
        Information,

        /// <summary>
        /// Warning that doesn't prevent operation
        /// </summary>
        Warning,

        /// <summary>
        /// Error that prevents operation completion
        /// </summary>
        Error,

        /// <summary>
        /// Critical error that requires immediate attention
        /// </summary>
        Critical,

        /// <summary>
        /// Fatal error that cannot be recovered from
        /// </summary>
        Fatal
    }

    /// <summary>
    /// License check queue event type enumeration
    /// </summary>
    public enum LicenseCheckQueueEventType
    {
        /// <summary>
        /// Operation was added to the queue
        /// </summary>
        OperationEnqueued,

        /// <summary>
        /// Operation was removed from the queue
        /// </summary>
        OperationDequeued,

        /// <summary>
        /// Queue was cleared
        /// </summary>
        QueueCleared,

        /// <summary>
        /// Queue is approaching capacity
        /// </summary>
        QueueCapacityWarning,

        /// <summary>
        /// Queue has reached capacity
        /// </summary>
        QueueCapacityReached,

        /// <summary>
        /// Queue processing started
        /// </summary>
        QueueProcessingStarted,

        /// <summary>
        /// Queue processing completed
        /// </summary>
        QueueProcessingCompleted,

        /// <summary>
        /// Queue processing was paused
        /// </summary>
        QueueProcessingPaused,

        /// <summary>
        /// Queue processing was resumed
        /// </summary>
        QueueProcessingResumed
    }
}