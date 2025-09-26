using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Represents the result of a license check operation
    /// </summary>
    public class LicenseCheckResult
    {
        /// <summary>
        /// Gets the operation ID associated with this result
        /// </summary>
        public string OperationId { get; }

        /// <summary>
        /// Gets the type of license check operation
        /// </summary>
        public LicenseCheckOperationType OperationType { get; }

        /// <summary>
        /// Gets the target SolidWorks version for this result
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
        /// Gets the execution status of the license check
        /// </summary>
        public LicenseCheckExecutionStatus Status { get; }

        /// <summary>
        /// Gets the timestamp when the check started
        /// </summary>
        public DateTime StartedAt { get; }

        /// <summary>
        /// Gets the timestamp when the check completed
        /// </summary>
        public DateTime CompletedAt { get; }

        /// <summary>
        /// Gets the duration of the license check
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets the license check results data
        /// </summary>
        public IReadOnlyList<LicenseFeatureResult> FeatureResults { get; }

        /// <summary>
        /// Gets the error information if the check failed
        /// </summary>
        public LicenseCheckErrorInfo Error { get; }

        /// <summary>
        /// Gets the metrics for this license check execution
        /// </summary>
        public LicenseCheckExecutionMetrics Metrics { get; }

        /// <summary>
        /// Gets the additional result data
        /// </summary>
        public IReadOnlyDictionary<string, object> AdditionalData { get; }

        /// <summary>
        /// Gets a value indicating whether the license check was successful
        /// </summary>
        public bool IsSuccess => Status == LicenseCheckExecutionStatus.Completed;

        /// <summary>
        /// Gets a value indicating whether the license check was cancelled
        /// </summary>
        public bool IsCancelled => Status == LicenseCheckExecutionStatus.Cancelled;

        /// <summary>
        /// Gets a value indicating whether the license check failed
        /// </summary>
        public bool IsFailed => Status == LicenseCheckExecutionStatus.Failed;

        /// <summary>
        /// Gets a value indicating whether the license check timed out
        /// </summary>
        public bool IsTimeout => Status == LicenseCheckExecutionStatus.Timeout;

        /// <summary>
        /// Gets a value indicating whether any licenses are available
        /// </summary>
        public bool HasAvailableLicenses
        {
            get
            {
                if (!IsSuccess || FeatureResults == null)
                    return false;

                foreach (var featureResult in FeatureResults)
                {
                    if (featureResult.AvailableLicenses > 0)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the total number of available licenses across all features
        /// </summary>
        public int TotalAvailableLicenses
        {
            get
            {
                if (!IsSuccess || FeatureResults == null)
                    return 0;

                int total = 0;
                foreach (var featureResult in FeatureResults)
                {
                    total += featureResult.AvailableLicenses;
                }

                return total;
            }
        }

        /// <summary>
        /// Gets the total number of licenses in use across all features
        /// </summary>
        public int TotalLicensesInUse
        {
            get
            {
                if (!IsSuccess || FeatureResults == null)
                    return 0;

                int total = 0;
                foreach (var featureResult in FeatureResults)
                {
                    total += featureResult.LicensesInUse;
                }

                return total;
            }
        }

        /// <summary>
        /// Gets the total number of licensed users across all features
        /// </summary>
        public int TotalLicensedUsers
        {
            get
            {
                if (!IsSuccess || FeatureResults == null)
                    return 0;

                int total = 0;
                foreach (var featureResult in FeatureResults)
                {
                    total += featureResult.LicensedUsers.Count;
                }

                return total;
            }
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckResult class for successful execution
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="targetVersion">The target version</param>
        /// <param name="licenseServer">The license server</param>
        /// <param name="port">The port</param>
        /// <param name="startedAt">The start time</param>
        /// <param name="completedAt">The completion time</param>
        /// <param name="featureResults">The feature results</param>
        /// <param name="metrics">The execution metrics</param>
        /// <param name="additionalData">Additional result data</param>
        public LicenseCheckResult(
            string operationId,
            LicenseCheckOperationType operationType,
            string targetVersion,
            string licenseServer,
            int port,
            DateTime startedAt,
            DateTime completedAt,
            IEnumerable<LicenseFeatureResult> featureResults,
            LicenseCheckExecutionMetrics metrics = null,
            IDictionary<string, object> additionalData = null)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            OperationType = operationType;
            TargetVersion = targetVersion ?? throw new ArgumentNullException(nameof(targetVersion));
            LicenseServer = licenseServer ?? throw new ArgumentNullException(nameof(licenseServer));
            Port = port;
            Status = LicenseCheckExecutionStatus.Completed;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            Duration = completedAt - startedAt;
            FeatureResults = featureResults != null ? new List<LicenseFeatureResult>(featureResults).AsReadOnly() : new List<LicenseFeatureResult>().AsReadOnly();
            Error = null;
            Metrics = metrics ?? new LicenseCheckExecutionMetrics();
            AdditionalData = additionalData != null ? new Dictionary<string, object>(additionalData).AsReadOnly() : new Dictionary<string, object>().AsReadOnly();
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckResult class for failed execution
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="targetVersion">The target version</param>
        /// <param name="licenseServer">The license server</param>
        /// <param name="port">The port</param>
        /// <param name="startedAt">The start time</param>
        /// <param name="completedAt">The completion time</param>
        /// <param name="status">The execution status</param>
        /// <param name="error">The error information</param>
        /// <param name="metrics">The execution metrics</param>
        /// <param name="additionalData">Additional result data</param>
        public LicenseCheckResult(
            string operationId,
            LicenseCheckOperationType operationType,
            string targetVersion,
            string licenseServer,
            int port,
            DateTime startedAt,
            DateTime completedAt,
            LicenseCheckExecutionStatus status,
            LicenseCheckErrorInfo error,
            LicenseCheckExecutionMetrics metrics = null,
            IDictionary<string, object> additionalData = null)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            OperationType = operationType;
            TargetVersion = targetVersion ?? throw new ArgumentNullException(nameof(targetVersion));
            LicenseServer = licenseServer ?? throw new ArgumentNullException(nameof(licenseServer));
            Port = port;
            Status = status;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            Duration = completedAt - startedAt;
            FeatureResults = new List<LicenseFeatureResult>().AsReadOnly();
            Error = error;
            Metrics = metrics ?? new LicenseCheckExecutionMetrics();
            AdditionalData = additionalData != null ? new Dictionary<string, object>(additionalData).AsReadOnly() : new Dictionary<string, object>().AsReadOnly();
        }

        /// <summary>
        /// Gets a feature result by feature code
        /// </summary>
        /// <param name="featureCode">The feature code to find</param>
        /// <returns>The feature result, or null if not found</returns>
        public LicenseFeatureResult GetFeatureResult(string featureCode)
        {
            if (FeatureResults == null)
                return null;

            foreach (var featureResult in FeatureResults)
            {
                if (string.Equals(featureResult.FeatureCode, featureCode, StringComparison.OrdinalIgnoreCase))
                    return featureResult;
            }

            return null;
        }

        /// <summary>
        /// Gets all feature results for a specific status
        /// </summary>
        /// <param name="licenseStatus">The license status to filter by</param>
        /// <returns>List of feature results with the specified status</returns>
        public IReadOnlyList<LicenseFeatureResult> GetFeaturesByStatus(LicenseStatus licenseStatus)
        {
            if (FeatureResults == null)
                return new List<LicenseFeatureResult>().AsReadOnly();

            var filteredResults = new List<LicenseFeatureResult>();
            foreach (var featureResult in FeatureResults)
            {
                if (featureResult.Status == licenseStatus)
                    filteredResults.Add(featureResult);
            }

            return filteredResults.AsReadOnly();
        }

        /// <summary>
        /// Returns a string representation of the license check result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckResult [Id={OperationId}, Status={Status}, Duration={Duration.TotalMilliseconds:F0}ms, Features={FeatureResults.Count}]";
        }

        /// <summary>
        /// Gets a detailed string representation of the result
        /// </summary>
        /// <returns>Detailed string representation</returns>
        public string ToDetailedString()
        {
            var errorText = Error != null ? $"\n  Error: {Error.Message}" : "";
            var featureSummary = FeatureResults.Count > 0 ?
                $"\n  Available Licenses: {TotalAvailableLicenses}\n  Licenses In Use: {TotalLicensesInUse}\n  Licensed Users: {TotalLicensedUsers}" :
                "";

            return $@"LicenseCheckResult Details:
  Operation ID: {OperationId}
  Type: {OperationType}
  Target Version: {TargetVersion}
  License Server: {LicenseServer}:{Port}
  Status: {Status}
  Started At: {StartedAt:yyyy-MM-dd HH:mm:ss UTC}
  Completed At: {CompletedAt:yyyy-MM-dd HH:mm:ss UTC}
  Duration: {Duration.TotalMilliseconds:F0}ms{errorText}{featureSummary}
  Features Checked: {FeatureResults.Count}
  Metrics: {Metrics.ToSummaryString()}";
        }
    }

    /// <summary>
    /// License check execution status enumeration
    /// </summary>
    public enum LicenseCheckExecutionStatus
    {
        /// <summary>
        /// License check completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// License check failed due to an error
        /// </summary>
        Failed,

        /// <summary>
        /// License check was cancelled
        /// </summary>
        Cancelled,

        /// <summary>
        /// License check timed out
        /// </summary>
        Timeout,

        /// <summary>
        /// License check was skipped due to queue or scheduling logic
        /// </summary>
        Skipped
    }

    /// <summary>
    /// Error information for failed license checks
    /// </summary>
    public class LicenseCheckErrorInfo
    {
        /// <summary>
        /// Gets the error message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the error type
        /// </summary>
        public string ErrorType { get; }

        /// <summary>
        /// Gets the error code
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets the stack trace
        /// </summary>
        public string StackTrace { get; }

        /// <summary>
        /// Gets the timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets additional error details
        /// </summary>
        public IReadOnlyDictionary<string, object> Details { get; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckErrorInfo class
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="errorType">The error type</param>
        /// <param name="errorCode">The error code</param>
        /// <param name="stackTrace">The stack trace</param>
        /// <param name="details">Additional error details</param>
        public LicenseCheckErrorInfo(
            string message,
            string errorType = null,
            string errorCode = null,
            string stackTrace = null,
            IDictionary<string, object> details = null)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ErrorType = errorType ?? "General";
            ErrorCode = errorCode;
            StackTrace = stackTrace;
            Timestamp = DateTime.UtcNow;
            Details = details != null ? new Dictionary<string, object>(details).AsReadOnly() : new Dictionary<string, object>().AsReadOnly();
        }

        /// <summary>
        /// Creates a LicenseCheckErrorInfo from an exception
        /// </summary>
        /// <param name="exception">The exception to convert</param>
        /// <returns>A new LicenseCheckErrorInfo instance</returns>
        public static LicenseCheckErrorInfo FromException(Exception exception)
        {
            return new LicenseCheckErrorInfo(
                exception.Message,
                exception.GetType().Name,
                exception.HResult.ToString(),
                exception.StackTrace,
                new Dictionary<string, object>
                {
                    ["InnerException"] = exception.InnerException?.Message,
                    ["Source"] = exception.Source,
                    ["HelpLink"] = exception.HelpLink
                });
        }
    }

    /// <summary>
    /// Execution metrics for license check operations
    /// </summary>
    public class LicenseCheckExecutionMetrics
    {
        /// <summary>
        /// Gets the execution time in milliseconds
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets the memory usage during execution in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Gets the number of features checked
        /// </summary>
        public int FeaturesChecked { get; set; }

        /// <summary>
        /// Gets the number of retries attempted
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets the network latency in milliseconds
        /// </summary>
        public long NetworkLatencyMs { get; set; }

        /// <summary>
        /// Gets the processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Gets a value indicating whether caching was used
        /// </summary>
        public bool CacheUsed { get; set; }

        /// <summary>
        /// Gets the number of cache hits
        /// </summary>
        public int CacheHits { get; set; }

        /// <summary>
        /// Gets the number of cache misses
        /// </summary>
        public int CacheMisses { get; set; }

        /// <summary>
        /// Gets the CPU usage during execution
        /// </summary>
        public double CpuUsagePercentage { get; set; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckExecutionMetrics class
        /// </summary>
        public LicenseCheckExecutionMetrics()
        {
        }

        /// <summary>
        /// Returns a summary string of the metrics
        /// </summary>
        /// <returns>Summary string</returns>
        public string ToSummaryString()
        {
            return $"Execution: {ExecutionTimeMs}ms, Memory: {MemoryUsageBytes} bytes, Features: {FeaturesChecked}, Retries: {RetryCount}";
        }

        /// <summary>
        /// Returns a detailed string of the metrics
        /// </summary>
        /// <returns>Detailed string</returns>
        public string ToDetailedString()
        {
            return $@"LicenseCheckExecutionMetrics:
  Execution Time: {ExecutionTimeMs}ms
  Memory Usage: {MemoryUsageBytes} bytes
  Features Checked: {FeaturesChecked}
  Retry Count: {RetryCount}
  Network Latency: {NetworkLatencyMs}ms
  Processing Time: {ProcessingTimeMs}ms
  Cache Used: {CacheUsed}
  Cache Hits: {CacheHits}
  Cache Misses: {CacheMisses}
  CPU Usage: {CpuUsagePercentage:F1}%";
        }
    }
}