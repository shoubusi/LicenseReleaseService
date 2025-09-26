using System;
using System.Collections.Generic;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents the result of a license check operation
    /// </summary>
    public class LicenseCheckResult
    {
        /// <summary>
        /// Gets or sets the unique identifier for this result
        /// </summary>
        public Guid ResultId { get; set; }

        /// <summary>
        /// Gets or sets the operation identifier
        /// </summary>
        public Guid OperationId { get; set; }

        /// <summary>
        /// Gets or sets the type of license check operation
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
        /// Gets or sets the operation start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the operation end time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the operation duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the retry attempt count
        /// </summary>
        public int RetryAttempt { get; set; }

        /// <summary>
        /// Gets or sets the operation timeout
        /// </summary>
        public TimeSpan Timeout { get; set; }

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
        /// Gets or sets the number of idle licenses found
        /// </summary>
        public int IdleLicensesFound { get; set; }

        /// <summary>
        /// Gets or sets the number of borrowed licenses found
        /// </summary>
        public int BorrowedLicensesFound { get; set; }

        /// <summary>
        /// Gets or sets the license server status if applicable
        /// </summary>
        public LicenseServerStatus ServerStatus { get; set; }

        /// <summary>
        /// Gets or sets the license usage statistics if applicable
        /// </summary>
        public LicenseUsageStatistics UsageStatistics { get; set; }

        /// <summary>
        /// Gets or sets the list of idle licenses found
        /// </summary>
        public List<LicenseInfo> IdleLicenses { get; set; } = new List<LicenseInfo>();

        /// <summary>
        /// Gets or sets the list of borrowed licenses found
        /// </summary>
        public List<LicenseInfo> BorrowedLicenses { get; set; } = new List<LicenseInfo>();

        /// <summary>
        /// Gets or sets the operation metrics
        /// </summary>
        public LicenseCheckOperationMetrics Metrics { get; set; }

        /// <summary>
        /// Gets or sets additional result data
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the result tags
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the result timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the execution context
        /// </summary>
        public object ExecutionContext { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the result was cached
        /// </summary>
        public bool WasCached { get; set; }

        /// <summary>
        /// Gets or sets the cache timestamp if the result was cached
        /// </summary>
        public DateTime? CacheTimestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation timed out
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation was cancelled
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// Gets or sets the operation priority
        /// </summary>
        public LicenseCheckOperationPriority Priority { get; set; }

        /// <summary>
        /// Gets a value indicating whether the operation completed within its timeout
        /// </summary>
        public bool CompletedWithinTimeout => Duration <= Timeout;

        /// <summary>
        /// Gets a value indicating whether the operation has any warnings
        /// </summary>
        public bool HasWarnings => !string.IsNullOrEmpty(ErrorMessage) && Success;

        /// <summary>
        /// Gets a value indicating whether the operation has errors
        /// </summary>
        public bool HasErrors => !Success || !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Gets the operation success rate
        /// </summary>
        public double SuccessRate => LicensesChecked > 0 ? (double)(LicensesChecked - IdleLicensesFound - BorrowedLicensesFound) / LicensesChecked : 0;

        /// <summary>
        /// Gets the total number of licenses found
        /// </summary>
        public int TotalLicensesFound => ServerStatus?.TotalLicenses ?? 0;

        /// <summary>
        /// Gets the total number of licenses in use
        /// </summary>
        public int TotalLicensesInUse => ServerStatus?.LicensesInUse ?? 0;

        /// <summary>
        /// Gets the total number of available licenses
        /// </summary>
        public int TotalAvailableLicenses => ServerStatus?.AvailableLicenses ?? 0;

        /// <summary>
        /// Gets the license utilization percentage
        /// </summary>
        public double UtilizationPercentage => TotalLicensesFound > 0 ? (double)TotalLicensesInUse / TotalLicensesFound * 100 : 0;

        /// <summary>
        /// Initializes a new instance of the LicenseCheckResult class
        /// </summary>
        public LicenseCheckResult()
        {
            ResultId = Guid.NewGuid();
            StartTime = DateTime.Now;
            EndTime = DateTime.Now;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckResult class with operation details
        /// </summary>
        /// <param name="operation">The operation this result is for</param>
        public LicenseCheckResult(LicenseCheckOperation operation) : this()
        {
            OperationId = operation.OperationId;
            OperationType = operation.OperationType;
            Server = operation.Server;
            Port = operation.Port;
            Feature = operation.Feature;
            User = operation.User;
            Priority = operation.Priority;
            Timeout = operation.Timeout;
            RetryAttempt = operation.RetryAttempt;
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        /// <param name="operation">The operation that succeeded</param>
        /// <param name="serverStatus">The server status result</param>
        /// <returns>Successful result</returns>
        public static LicenseCheckResult CreateSuccess(LicenseCheckOperation operation, LicenseServerStatus serverStatus = null)
        {
            var result = new LicenseCheckResult(operation)
            {
                Success = true,
                ServerStatus = serverStatus,
                EndTime = DateTime.Now,
                Duration = DateTime.Now - operation.CreatedAt
            };

            if (serverStatus != null)
            {
                result.LicensesChecked = serverStatus.TotalLicenses;
                result.FeaturesProcessed = serverStatus.Features.Count;
                result.UsersProcessed = serverStatus.TotalActiveUsers;
            }

            return result;
        }

        /// <summary>
        /// Creates a failed result
        /// </summary>
        /// <param name="operation">The operation that failed</param>
        /// <param name="errorMessage">The error message</param>
        /// <param name="exception">The exception that caused the failure</param>
        /// <returns>Failed result</returns>
        public static LicenseCheckResult CreateFailure(LicenseCheckOperation operation, string errorMessage, Exception exception = null)
        {
            return new LicenseCheckResult(operation)
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                EndTime = DateTime.Now,
                Duration = DateTime.Now - operation.CreatedAt
            };
        }

        /// <summary>
        /// Creates a timed out result
        /// </summary>
        /// <param name="operation">The operation that timed out</param>
        /// <returns>Timed out result</returns>
        public static LicenseCheckResult CreateTimeout(LicenseCheckOperation operation)
        {
            return new LicenseCheckResult(operation)
            {
                Success = false,
                ErrorMessage = "Operation timed out",
                TimedOut = true,
                EndTime = DateTime.Now,
                Duration = operation.Timeout
            };
        }

        /// <summary>
        /// Creates a cancelled result
        /// </summary>
        /// <param name="operation">The operation that was cancelled</param>
        /// <returns>Cancelled result</returns>
        public static LicenseCheckResult CreateCancelled(LicenseCheckOperation operation)
        {
            return new LicenseCheckResult(operation)
            {
                Success = false,
                ErrorMessage = "Operation was cancelled",
                Cancelled = true,
                EndTime = DateTime.Now,
                Duration = DateTime.Now - operation.CreatedAt
            };
        }

        /// <summary>
        /// Marks the result as completed with the given server status
        /// </summary>
        /// <param name="serverStatus">The server status to set</param>
        public void CompleteWithServerStatus(LicenseServerStatus serverStatus)
        {
            ServerStatus = serverStatus;
            Success = string.IsNullOrEmpty(serverStatus.ErrorMessage);
            ErrorMessage = serverStatus.ErrorMessage;
            EndTime = DateTime.Now;
            Duration = EndTime - StartTime;

            LicensesChecked = serverStatus.TotalLicenses;
            FeaturesProcessed = serverStatus.Features.Count;
            UsersProcessed = serverStatus.TotalActiveUsers;
        }

        /// <summary>
        /// Adds idle licenses to the result
        /// </summary>
        /// <param name="idleLicenses">List of idle licenses</param>
        public void AddIdleLicenses(List<LicenseInfo> idleLicenses)
        {
            if (idleLicenses != null)
            {
                IdleLicenses.AddRange(idleLicenses);
                IdleLicensesFound = IdleLicenses.Count;
            }
        }

        /// <summary>
        /// Adds borrowed licenses to the result
        /// </summary>
        /// <param name="borrowedLicenses">List of borrowed licenses</param>
        public void AddBorrowedLicenses(List<LicenseInfo> borrowedLicenses)
        {
            if (borrowedLicenses != null)
            {
                BorrowedLicenses.AddRange(borrowedLicenses);
                BorrowedLicensesFound = BorrowedLicenses.Count;
            }
        }

        /// <summary>
        /// Sets the usage statistics
        /// </summary>
        /// <param name="statistics">Usage statistics</param>
        public void SetUsageStatistics(LicenseUsageStatistics statistics)
        {
            UsageStatistics = statistics;
            if (statistics != null)
            {
                LicensesChecked = statistics.TotalLicenses;
                UsersProcessed = statistics.ActiveUsers;
                IdleLicensesFound = statistics.IdleUsers;
                BorrowedLicensesFound = statistics.BorrowedUsers;
            }
        }

        /// <summary>
        /// Adds a tag to the result
        /// </summary>
        /// <param name="tag">Tag to add</param>
        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !Tags.Contains(tag))
            {
                Tags.Add(tag);
            }
        }

        /// <summary>
        /// Adds data to the result
        /// </summary>
        /// <param name="key">Data key</param>
        /// <param name="value">Data value</param>
        public void AddData(string key, object value)
        {
            Data[key] = value;
        }

        /// <summary>
        /// Gets data from the result
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="key">Data key</param>
        /// <param name="defaultValue">Default value if not found</param>
        /// <returns>Data value or default</returns>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (Data.TryGetValue(key, out var value) && value is T result)
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Checks if the result has a specific tag
        /// </summary>
        /// <param name="tag">Tag to check</param>
        /// <returns>True if the result has the tag</returns>
        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        /// <summary>
        /// Sets the operation metrics
        /// </summary>
        /// <param name="metrics">Operation metrics</param>
        public void SetMetrics(LicenseCheckOperationMetrics metrics)
        {
            Metrics = metrics;
        }

        /// <summary>
        /// Marks the result as cached
        /// </summary>
        /// <param name="cacheTimestamp">When the result was cached</param>
        public void MarkAsCached(DateTime cacheTimestamp)
        {
            WasCached = true;
            CacheTimestamp = cacheTimestamp;
        }

        /// <summary>
        /// Returns a string representation of the result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckResult[Id={ResultId:N}, Operation={OperationType}, " +
                   $"Server={Server}:{Port}, Success={Success}, Duration={Duration.TotalMilliseconds:F2}ms, " +
                   $"Licenses={LicensesChecked}, Features={FeaturesProcessed}, Users={UsersProcessed}, " +
                   $"Idle={IdleLicensesFound}, Borrowed={BorrowedLicensesFound}]";
        }

        /// <summary>
        /// Returns a detailed string representation of the result
        /// </summary>
        /// <returns>Detailed string representation</returns>
        public string ToDetailedString()
        {
            var details = $"LicenseCheckResult Details:\n";
            details += $"  Operation ID: {OperationId:N}\n";
            details += $"  Operation Type: {OperationType}\n";
            details += $"  Server: {Server}:{Port}\n";
            details += $"  Success: {Success}\n";
            details += $"  Duration: {Duration.TotalMilliseconds:F2}ms\n";
            details += $"  Timeout: {Timeout.TotalMilliseconds:F2}ms\n";
            details += $"  Retry Attempt: {RetryAttempt}\n";
            details += $"  Licenses Checked: {LicensesChecked}\n";
            details += $"  Features Processed: {FeaturesProcessed}\n";
            details += $"  Users Processed: {UsersProcessed}\n";
            details += $"  Idle Licenses Found: {IdleLicensesFound}\n";
            details += $"  Borrowed Licenses Found: {BorrowedLicensesFound}\n";
            details += $"  Utilization: {UtilizationPercentage:F1}%\n";

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                details += $"  Error: {ErrorMessage}\n";
            }

            if (TimedOut)
            {
                details += $"  Status: Timed Out\n";
            }

            if (Cancelled)
            {
                details += $"  Status: Cancelled\n";
            }

            if (WasCached)
            {
                details += $"  Cached: Yes (at {CacheTimestamp:yyyy-MM-dd HH:mm:ss})\n";
            }

            if (Tags.Count > 0)
            {
                details += $"  Tags: {string.Join(", ", Tags)}\n";
            }

            return details;
        }
    }

    /// <summary>
    /// Represents metrics for a license check operation
    /// </summary>
    public class LicenseCheckOperationMetrics
    {
        /// <summary>
        /// Gets or sets the server response time in milliseconds
        /// </summary>
        public long ServerResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the queue wait time in milliseconds
        /// </summary>
        public long QueueWaitTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the total data received in bytes
        /// </summary>
        public long DataReceivedBytes { get; set; }

        /// <summary>
        /// Gets or sets the memory usage in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsagePercentage { get; set; }

        /// <summary>
        /// Gets or sets the number of cache hits
        /// </summary>
        public int CacheHits { get; set; }

        /// <summary>
        /// Gets or sets the number of cache misses
        /// </summary>
        public int CacheMisses { get; set; }

        /// <summary>
        /// Gets or sets the cache hit ratio
        /// </summary>
        public double CacheHitRatio => CacheHits + CacheMisses > 0 ? (double)CacheHits / (CacheHits + CacheMisses) : 0;

        /// <summary>
        /// Gets or sets the number of network requests made
        /// </summary>
        public int NetworkRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of retries attempted
        /// </summary>
        public int RetriesAttempted { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when metrics were collected
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckOperationMetrics[ServerResponse={ServerResponseTimeMs}ms, " +
                   $"Processing={ProcessingTimeMs}ms, QueueWait={QueueWaitTimeMs}ms, " +
                   $"Data={DataReceivedBytes}bytes, Memory={MemoryUsageBytes}bytes, " +
                   $"CPU={CpuUsagePercentage:F1}%, CacheHitRatio={CacheHitRatio:P2}, " +
                   $"Requests={NetworkRequests}, Retries={RetriesAttempted}]";
        }
    }
}