using System;
using System.Collections.Generic;
using LicenseReleaseService.TimerExecution;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents the result of a license release operation
    /// </summary>
    public class LicenseReleaseResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the license release was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the release failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detailed error information
        /// </summary>
        public string ErrorDetails { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the license feature name
        /// </summary>
        public string Feature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation execution time
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the operation was performed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the released feature name
        /// </summary>
        public string ReleasedFeature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the released user name
        /// </summary>
        public string ReleasedUser { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation result code
        /// </summary>
        public LicenseReleaseResultCode ResultCode { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses released
        /// </summary>
        public int LicensesReleased { get; set; }

        /// <summary>
        /// Gets or sets the retry attempt count
        /// </summary>
        public int RetryAttempt { get; set; }

        /// <summary>
        /// Gets or sets the process ID used for the operation
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the command executed
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command output
        /// </summary>
        public string CommandOutput { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command error output
        /// </summary>
        public string CommandError { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exit code from the lmutil process
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the operation
        /// </summary>
        public object Metadata { get; set; }

        /// <summary>
        /// Gets or sets the exception that occurred during the operation
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the list of released licenses
        /// </summary>
        public List<ReleasedLicenseInfo> ReleasedLicenses { get; set; }

        /// <summary>
        /// Gets or sets the list of failed releases
        /// </summary>
        public List<FailedLicenseRelease> FailedReleases { get; set; }

        /// <summary>
        /// Gets or sets the list of skipped releases
        /// </summary>
        public List<SkippedLicenseRelease> SkippedReleases { get; set; }

        /// <summary>
        /// Gets or sets the total number of candidates considered
        /// </summary>
        public int TotalCandidates { get; set; }

        /// <summary>
        /// Gets or sets the total number of released licenses
        /// </summary>
        public int TotalReleased { get; set; }

        /// <summary>
        /// Gets or sets the total number of failed releases
        /// </summary>
        public int TotalFailed { get; set; }

        /// <summary>
        /// Gets or sets the total number of skipped releases
        /// </summary>
        public int TotalSkipped { get; set; }

        /// <summary>
        /// Gets a value indicating whether the operation timed out
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// Gets a value indicating whether the operation was cancelled
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// Gets a value indicating whether the operation was retried
        /// </summary>
        public bool WasRetried => RetryAttempt > 0;

        /// <summary>
        /// Initializes a new instance of the LicenseReleaseResult class
        /// </summary>
        public LicenseReleaseResult()
        {
            ReleasedLicenses = new List<ReleasedLicenseInfo>();
            FailedReleases = new List<FailedLicenseRelease>();
            SkippedReleases = new List<SkippedLicenseRelease>();
        }

        /// <summary>
        /// Returns a string representation of the license release result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseReleaseResult[Success={Success}, Feature={Feature}, User={User}, " +
                   $"Server={Server}:{Port}, Code={ResultCode}, Time={ExecutionTime.TotalMilliseconds:F2}ms, " +
                   $"Retries={RetryAttempt}, Released={LicensesReleased}]";
        }

        /// <summary>
        /// Creates a successful license release result
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="user">User name</param>
        /// <param name="licensesReleased">Number of licenses released</param>
        /// <param name="executionTime">Execution time</param>
        /// <returns>Successful result</returns>
        public static LicenseReleaseResult CreateSuccess(string server, int port, string feature, string user,
            int licensesReleased, TimeSpan executionTime)
        {
            return new LicenseReleaseResult
            {
                Success = true,
                Server = server,
                Port = port,
                Feature = feature,
                User = user,
                LicensesReleased = licensesReleased,
                ExecutionTime = executionTime,
                ResultCode = LicenseReleaseResultCode.Success,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Creates a failed license release result
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="user">User name</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="resultCode">Result code</param>
        /// <param name="executionTime">Execution time</param>
        /// <returns>Failed result</returns>
        public static LicenseReleaseResult CreateFailure(string server, int port, string feature, string user,
            string errorMessage, LicenseReleaseResultCode resultCode, TimeSpan executionTime)
        {
            return new LicenseReleaseResult
            {
                Success = false,
                Server = server,
                Port = port,
                Feature = feature,
                User = user,
                ErrorMessage = errorMessage,
                ExecutionTime = executionTime,
                ResultCode = resultCode,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Creates a timeout result
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="user">User name</param>
        /// <param name="executionTime">Execution time</param>
        /// <returns>Timeout result</returns>
        public static LicenseReleaseResult CreateTimeout(string server, int port, string feature, string user, TimeSpan executionTime)
        {
            return new LicenseReleaseResult
            {
                Success = false,
                Server = server,
                Port = port,
                Feature = feature,
                User = user,
                ErrorMessage = "License release operation timed out",
                ExecutionTime = executionTime,
                ResultCode = LicenseReleaseResultCode.Timeout,
                TimedOut = true,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Creates a cancelled result
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="user">User name</param>
        /// <param name="executionTime">Execution time</param>
        /// <returns>Cancelled result</returns>
        public static LicenseReleaseResult CreateCancelled(string server, int port, string feature, string user, TimeSpan executionTime)
        {
            return new LicenseReleaseResult
            {
                Success = false,
                Server = server,
                Port = port,
                Feature = feature,
                User = user,
                ErrorMessage = "License release operation was cancelled",
                ExecutionTime = executionTime,
                ResultCode = LicenseReleaseResultCode.Cancelled,
                Cancelled = true,
                Timestamp = DateTime.Now
            };
        }
    }

    /// <summary>
    /// Defines the result codes for license release operations
    /// </summary>
    public enum LicenseReleaseResultCode
    {
        /// <summary>
        /// The operation was successful
        /// </summary>
        Success = 0,

        /// <summary>
        /// The license server is not available
        /// </summary>
        ServerUnavailable = 1,

        /// <summary>
        /// The specified feature does not exist
        /// </summary>
        FeatureNotFound = 2,

        /// <summary>
        /// The specified user is not using the license
        /// </summary>
        UserNotFound = 3,

        /// <summary>
        /// Insufficient permissions to release the license
        /// </summary>
        PermissionDenied = 4,

        /// <summary>
        /// The license server returned an error
        /// </summary>
        ServerError = 5,

        /// <summary>
        /// The operation timed out
        /// </summary>
        Timeout = 6,

        /// <summary>
        /// The operation was cancelled
        /// </summary>
        Cancelled = 7,

        /// <summary>
        /// Invalid parameters were provided
        /// </summary>
        InvalidParameters = 8,

        /// <summary>
        /// The lmutil executable was not found
        /// </summary>
        LmutilNotFound = 9,

        /// <summary>
        /// Network connectivity issues
        /// </summary>
        NetworkError = 10,

        /// <summary>
        /// Unknown error occurred
        /// </summary>
        UnknownError = 999
    }

    /// <summary>
    /// Represents information about a license feature
    /// </summary>
    public class LicenseFeatureInfo
    {
        /// <summary>
        /// Gets or sets the feature name
        /// </summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the feature version
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the feature description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the vendor name
        /// </summary>
        public string Vendor { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of licenses
        /// </summary>
        public int TotalLicenses { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses in use
        /// </summary>
        public int LicensesInUse { get; set; }

        /// <summary>
        /// Gets or sets the number of available licenses
        /// </summary>
        public int AvailableLicenses { get; set; }

        /// <summary>
        /// Gets or sets the license expiration date
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the feature status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the license type
        /// </summary>
        public string LicenseType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the checkout type
        /// </summary>
        public string CheckoutType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum borrow duration in hours
        /// </summary>
        public int MaxBorrowHours { get; set; }

        /// <summary>
        /// Gets or sets the hold time in seconds
        /// </summary>
        public int HoldTime { get; set; }

        /// <summary>
        /// Gets or sets the number of reservations
        /// </summary>
        public int Reservations { get; set; }

        /// <summary>
        /// Gets or sets the number of reserved licenses
        /// </summary>
        public int ReservedLicenses { get; set; }

        /// <summary>
        /// Gets or sets the list of users using this feature
        /// </summary>
        public List<LicenseUserInfo> Users { get; set; } = new List<LicenseUserInfo>();

        /// <summary>
        /// Gets a value indicating whether the feature is available
        /// </summary>
        public bool IsAvailable => Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) &&
                                 AvailableLicenses > 0 &&
                                 (!ExpirationDate.HasValue || ExpirationDate.Value > DateTime.Now);

        /// <summary>
        /// Gets the utilization percentage
        /// </summary>
        public double UtilizationPercentage => TotalLicenses > 0 ? (LicensesInUse * 100.0 / TotalLicenses) : 0;

        /// <summary>
        /// Returns a string representation of the license feature info
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseFeatureInfo[Feature={FeatureName}, Version={Version}, " +
                   $"Total={TotalLicenses}, InUse={LicensesInUse}, Available={AvailableLicenses}, " +
                   $"Status={Status}, Type={LicenseType}]";
        }
    }

    /// <summary>
    /// Represents license usage statistics
    /// </summary>
    public class LicenseUsageStatistics
    {
        /// <summary>
        /// Gets or sets the server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the total number of licenses across all features
        /// </summary>
        public int TotalLicenses { get; set; }

        /// <summary>
        /// Gets or sets the total number of licenses in use
        /// </summary>
        public int TotalLicensesInUse { get; set; }

        /// <summary>
        /// Gets or sets the total number of available licenses
        /// </summary>
        public int TotalAvailableLicenses { get; set; }

        /// <summary>
        /// Gets or sets the number of unique users
        /// </summary>
        public int UniqueUsers { get; set; }

        /// <summary>
        /// Gets or sets the number of active features
        /// </summary>
        public int ActiveFeatures { get; set; }

        /// <summary>
        /// Gets or sets the overall utilization percentage
        /// </summary>
        public double OverallUtilization { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the statistics were collected
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the peak usage count
        /// </summary>
        public int PeakUsage { get; set; }

        /// <summary>
        /// Gets or sets the peak usage time
        /// </summary>
        public DateTime? PeakUsageTime { get; set; }

        /// <summary>
        /// Gets or sets the average usage over the last 24 hours
        /// </summary>
        public double AverageUsage24h { get; set; }

        /// <summary>
        /// Gets or sets the number of active users
        /// </summary>
        public int ActiveUsers { get; set; }

        /// <summary>
        /// Gets or sets the number of idle users
        /// </summary>
        public int IdleUsers { get; set; }

        /// <summary>
        /// Gets or sets the number of borrowed users
        /// </summary>
        public int BorrowedUsers { get; set; }

        /// <summary>
        /// Returns a string representation of the license usage statistics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseUsageStatistics[Server={Server}:{Port}, Total={TotalLicenses}, " +
                   $"InUse={TotalLicensesInUse}, Available={TotalAvailableLicenses}, " +
                   $"Users={UniqueUsers}, Features={ActiveFeatures}, Utilization={OverallUtilization:F1}%]";
        }
    }

    /// <summary>
    /// Represents a failed license release
    /// </summary>
    public class FailedLicenseRelease
    {
        /// <summary>
        /// Gets or sets the license identifier
        /// </summary>
        public string LicenseId { get; set; }

        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the failure
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the computer name
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets or sets the feature name
        /// </summary>
        public string Feature { get; set; }

        /// <summary>
        /// Gets or sets the license release candidate that failed
        /// </summary>
        public LicenseReleaseCandidate Candidate { get; set; }

        /// <summary>
        /// Gets or sets the exception that caused the failure
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Initializes a new instance of the FailedLicenseRelease class
        /// </summary>
        public FailedLicenseRelease()
        {
            Timestamp = DateTime.UtcNow;
            LicenseId = string.Empty;
            ErrorMessage = string.Empty;
            UserName = string.Empty;
            ComputerName = string.Empty;
            Feature = string.Empty;
            Candidate = null;
            Exception = null;
        }

        /// <summary>
        /// Initializes a new instance of the FailedLicenseRelease class
        /// </summary>
        /// <param name="licenseId">License identifier</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="userName">User name</param>
        /// <param name="computerName">Computer name</param>
        /// <param name="feature">Feature name</param>
        public FailedLicenseRelease(string licenseId, string errorMessage, string userName, string computerName, string feature)
        {
            LicenseId = licenseId ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            UserName = userName ?? string.Empty;
            ComputerName = computerName ?? string.Empty;
            Feature = feature ?? string.Empty;
            Timestamp = DateTime.UtcNow;
            Candidate = null;
            Exception = null;
        }
    }

    /// <summary>
    /// Represents a skipped license release
    /// </summary>
    public class SkippedLicenseRelease
    {
        /// <summary>
        /// Gets or sets the license identifier
        /// </summary>
        public string LicenseId { get; set; }

        /// <summary>
        /// Gets or sets the reason for skipping
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the skip
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the computer name
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Gets or sets the feature name
        /// </summary>
        public string Feature { get; set; }

        /// <summary>
        /// Initializes a new instance of the SkippedLicenseRelease class
        /// </summary>
        public SkippedLicenseRelease()
        {
            Timestamp = DateTime.UtcNow;
            LicenseId = string.Empty;
            Reason = string.Empty;
            UserName = string.Empty;
            ComputerName = string.Empty;
            Feature = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the SkippedLicenseRelease class
        /// </summary>
        /// <param name="licenseId">License identifier</param>
        /// <param name="reason">Reason for skipping</param>
        /// <param name="userName">User name</param>
        /// <param name="computerName">Computer name</param>
        /// <param name="feature">Feature name</param>
        public SkippedLicenseRelease(string licenseId, string reason, string userName, string computerName, string feature)
        {
            LicenseId = licenseId ?? string.Empty;
            Reason = reason ?? string.Empty;
            UserName = userName ?? string.Empty;
            ComputerName = computerName ?? string.Empty;
            Feature = feature ?? string.Empty;
            Timestamp = DateTime.UtcNow;
        }
    }
}