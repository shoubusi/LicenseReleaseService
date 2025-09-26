using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Represents a license check operation to be executed by the scheduler
    /// </summary>
    public class LicenseCheckOperation
    {
        /// <summary>
        /// Gets the unique identifier for this operation
        /// </summary>
        public string OperationId { get; }

        /// <summary>
        /// Gets the type of license check operation
        /// </summary>
        public LicenseCheckOperationType OperationType { get; }

        /// <summary>
        /// Gets the target SolidWorks version for this operation
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
        /// Gets the feature codes to check
        /// </summary>
        public IReadOnlyList<string> FeatureCodes { get; }

        /// <summary>
        /// Gets the operation parameters
        /// </summary>
        public IReadOnlyDictionary<string, object> Parameters { get; }

        /// <summary>
        /// Gets the created timestamp
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the scheduled execution time
        /// </summary>
        public DateTime? ScheduledAt { get; }

        /// <summary>
        /// Gets the execution timeout
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// Gets the maximum retry count
        /// </summary>
        public int MaxRetryCount { get; }

        /// <summary>
        /// Gets the current retry count
        /// </summary>
        public int CurrentRetryCount { get; internal set; }

        /// <summary>
        /// Gets the user who initiated this operation
        /// </summary>
        public string InitiatingUser { get; }

        /// <summary>
        /// Gets the computer name associated with this operation
        /// </summary>
        public string ComputerName { get; }

        /// <summary>
        /// Gets the operation priority
        /// </summary>
        public LicenseCheckPriority Priority { get; }

        /// <summary>
        /// Gets a value indicating whether this operation can be cancelled
        /// </summary>
        public bool CanCancel { get; }

        /// <summary>
        /// Gets a value indicating whether this operation is a one-time execution
        /// </summary>
        public bool IsOneTimeExecution { get; }

        /// <summary>
        /// Gets the operation tags for categorization and filtering
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckOperation class
        /// </summary>
        /// <param name="operationType">The type of license check operation</param>
        /// <param name="targetVersion">The target SolidWorks version</param>
        /// <param name="licenseServer">The license server address</param>
        /// <param name="port">The license server port</param>
        /// <param name="featureCodes">The feature codes to check</param>
        /// <param name="initiatingUser">The user who initiated this operation</param>
        /// <param name="computerName">The computer name</param>
        public LicenseCheckOperation(
            LicenseCheckOperationType operationType,
            string targetVersion,
            string licenseServer,
            int port,
            IEnumerable<string> featureCodes,
            string initiatingUser = null,
            string computerName = null)
            : this(operationType, targetVersion, licenseServer, port, featureCodes, initiatingUser, computerName, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckOperation class with parameters
        /// </summary>
        /// <param name="operationType">The type of license check operation</param>
        /// <param name="targetVersion">The target SolidWorks version</param>
        /// <param name="licenseServer">The license server address</param>
        /// <param name="port">The license server port</param>
        /// <param name="featureCodes">The feature codes to check</param>
        /// <param name="initiatingUser">The user who initiated this operation</param>
        /// <param name="computerName">The computer name</param>
        /// <param name="parameters">Additional operation parameters</param>
        public LicenseCheckOperation(
            LicenseCheckOperationType operationType,
            string targetVersion,
            string licenseServer,
            int port,
            IEnumerable<string> featureCodes,
            string initiatingUser = null,
            string computerName = null,
            IDictionary<string, object> parameters = null)
        {
            OperationId = Guid.NewGuid().ToString("N")[..8];
            OperationType = operationType;
            TargetVersion = targetVersion ?? throw new ArgumentNullException(nameof(targetVersion));
            LicenseServer = licenseServer ?? throw new ArgumentNullException(nameof(licenseServer));
            Port = port > 0 ? port : throw new ArgumentOutOfRangeException(nameof(port), "Port must be greater than 0");
            FeatureCodes = featureCodes != null ? new List<string>(featureCodes).AsReadOnly() : throw new ArgumentNullException(nameof(featureCodes));
            Parameters = parameters != null ? new Dictionary<string, object>(parameters).AsReadOnly() : new Dictionary<string, object>().AsReadOnly();
            CreatedAt = DateTime.UtcNow;
            Timeout = TimeSpan.FromMinutes(5);
            MaxRetryCount = 3;
            CurrentRetryCount = 0;
            InitiatingUser = initiatingUser ?? "system";
            ComputerName = computerName ?? Environment.MachineName;
            Priority = LicenseCheckPriority.Normal;
            CanCancel = true;
            IsOneTimeExecution = true;
            Tags = new List<string>().AsReadOnly();
        }

        /// <summary>
        /// Creates a copy of this operation with updated properties
        /// </summary>
        /// <param name="scheduledAt">The new scheduled time</param>
        /// <param name="priority">The new priority</param>
        /// <param name="timeout">The new timeout</param>
        /// <param name="maxRetryCount">The new maximum retry count</param>
        /// <param name="tags">The new tags</param>
        /// <returns>A new LicenseCheckOperation with updated properties</returns>
        public LicenseCheckOperation WithUpdates(
            DateTime? scheduledAt = null,
            LicenseCheckPriority? priority = null,
            TimeSpan? timeout = null,
            int? maxRetryCount = null,
            IEnumerable<string> tags = null)
        {
            var operation = new LicenseCheckOperation(
                OperationType,
                TargetVersion,
                LicenseServer,
                Port,
                FeatureCodes,
                InitiatingUser,
                ComputerName,
                Parameters);

            // Update properties using reflection to set internal values
            var operationType = operation.GetType();
            if (scheduledAt.HasValue)
                operationType.GetProperty(nameof(ScheduledAt))?.SetValue(operation, scheduledAt.Value);
            if (priority.HasValue)
                operationType.GetProperty(nameof(Priority))?.SetValue(operation, priority.Value);
            if (timeout.HasValue)
                operationType.GetProperty(nameof(Timeout))?.SetValue(operation, timeout.Value);
            if (maxRetryCount.HasValue)
                operationType.GetProperty(nameof(MaxRetryCount))?.SetValue(operation, maxRetryCount.Value);

            if (tags != null)
            {
                var tagsProperty = operationType.GetProperty(nameof(Tags));
                var tagsList = new List<string>(tags);
                tagsProperty?.SetValue(operation, tagsList.AsReadOnly());
            }

            return operation;
        }

        /// <summary>
        /// Creates a retry operation from this operation
        /// </summary>
        /// <returns>A new LicenseCheckOperation for retry</returns>
        public LicenseCheckOperation CreateRetryOperation()
        {
            if (CurrentRetryCount >= MaxRetryCount)
            {
                throw new InvalidOperationException($"Maximum retry count ({MaxRetryCount}) has been reached");
            }

            var retryOperation = new LicenseCheckOperation(
                OperationType,
                TargetVersion,
                LicenseServer,
                Port,
                FeatureCodes,
                InitiatingUser,
                ComputerName,
                Parameters);

            // Set retry count and schedule for immediate execution
            var operationType = retryOperation.GetType();
            operationType.GetProperty(nameof(CurrentRetryCount))?.SetValue(retryOperation, CurrentRetryCount + 1);
            operationType.GetProperty(nameof(ScheduledAt))?.SetValue(retryOperation, DateTime.UtcNow);

            return retryOperation;
        }

        /// <summary>
        /// Returns a string representation of the license check operation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckOperation [Id={OperationId}, Type={OperationType}, Version={TargetVersion}, Server={LicenseServer}:{Port}, Features={FeatureCodes.Count}]";
        }

        /// <summary>
        /// Gets a detailed string representation of the operation
        /// </summary>
        /// <returns>Detailed string representation</returns>
        public string ToDetailedString()
        {
            return $@"LicenseCheckOperation Details:
  Operation ID: {OperationId}
  Type: {OperationType}
  Target Version: {TargetVersion}
  License Server: {LicenseServer}:{Port}
  Feature Codes: {string.Join(", ", FeatureCodes)}
  Created At: {CreatedAt:yyyy-MM-dd HH:mm:ss UTC}
  Scheduled At: {ScheduledAt?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Not scheduled"}
  Timeout: {Timeout.TotalSeconds:F0}s
  Max Retry Count: {MaxRetryCount}
  Current Retry Count: {CurrentRetryCount}
  Initiating User: {InitiatingUser}
  Computer Name: {ComputerName}
  Priority: {Priority}
  Can Cancel: {CanCancel}
  One Time Execution: {IsOneTimeExecution}
  Tags: {string.Join(", ", Tags)}
  Parameters: {Parameters.Count} items";
        }
    }

    /// <summary>
    /// Types of license check operations
    /// </summary>
    public enum LicenseCheckOperationType
    {
        /// <summary>
        /// Basic license availability check
        /// </summary>
        AvailabilityCheck,

        /// <summary>
        /// Detailed license usage query
        /// </summary>
        UsageQuery,

        /// <summary>
        /// License borrowing status check
        /// </summary>
        BorrowingStatus,

        /// <summary>
        /// License server health check
        /// </summary>
        ServerHealth,

        /// <summary>
        /// Feature-specific license check
        /// </summary>
        FeatureCheck,

        /// <summary>
        /// User-specific license check
        /// </summary>
        UserCheck,

        /// <summary>
        /// Computer-specific license check
        /// </summary>
        ComputerCheck,

        /// <summary>
        /// Comprehensive license audit
        /// </summary>
        FullAudit,

        /// <summary>
        /// License release verification
        /// </summary>
        ReleaseVerification,

        /// <summary>
        /// License violation detection
        /// </summary>
        ViolationDetection
    }
}