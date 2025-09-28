using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Models;

namespace LicenseReleaseService.Interfaces
{
    /// <summary>
    /// Defines the contract for safety validation operations
    /// </summary>
    public interface ISafetyValidator : IDisposable
    {
        /// <summary>
        /// Gets the safety validation configuration
        /// </summary>
        SafetyValidationConfiguration Configuration { get; }

        /// <summary>
        /// Gets a value indicating whether the validator is initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Event raised when safety validation is completed
        /// </summary>
        event EventHandler<SafetyValidationCompletedEventArgs> SafetyValidationCompleted;

        /// <summary>
        /// Event raised when a safety check fails
        /// </summary>
        event EventHandler<SafetyCheckFailedEventArgs> SafetyCheckFailed;

        /// <summary>
        /// Event raised when user activity is detected
        /// </summary>
        event EventHandler<UserActivityEventArgs> UserActivityDetected;

        /// <summary>
        /// Initializes the safety validator
        /// </summary>
        /// <param name="configuration">Safety validation configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Initialization result</returns>
        Task<InitializationResult> InitializeAsync(SafetyValidationConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the safety of a license release operation
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="user">User name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Safety validation result</returns>
        Task<SafetyValidationResult> ValidateLicenseReleaseSafetyAsync(string server, int port, string feature, string user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates process accessibility for license release
        /// </summary>
        /// <param name="processName">Process name to validate</param>
        /// <param name="userName">User name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process accessibility validation result</returns>
        Task<SafetyValidationResult> ValidateProcessAccessibilityAsync(string processName, string userName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates user activity for safe license release
        /// </summary>
        /// <param name="userName">User name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>User activity validation result</returns>
        Task<SafetyValidationResult> ValidateUserActivityAsync(string userName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates system resources for safe operation
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>System resource validation result</returns>
        Task<SafetyValidationResult> ValidateSystemResourcesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates network connectivity for license server communication
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Network connectivity validation result</returns>
        Task<SafetyValidationResult> ValidateNetworkConnectivityAsync(string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates critical operations protection
        /// </summary>
        /// <param name="operation">Operation type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Critical operations validation result</returns>
        Task<SafetyValidationResult> ValidateCriticalOperationsAsync(string operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a comprehensive safety validation
        /// </summary>
        /// <param name="context">Validation context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Comprehensive safety validation result</returns>
        Task<SafetyValidationResult> ValidateComprehensiveSafetyAsync(ValidationContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts monitoring user activity
        /// </summary>
        /// <param name="userName">User name to monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Monitoring result</returns>
        Task<MonitoringResult> StartUserActivityMonitoringAsync(string userName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops monitoring user activity
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stop result</returns>
        Task<MonitoringResult> StopUserActivityMonitoringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current user activity information
        /// </summary>
        /// <param name="userName">User name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>User activity information</returns>
        Task<UserActivityInfo> GetUserActivityInfoAsync(string userName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the list of protected processes
        /// </summary>
        /// <returns>List of protected process names</returns>
        Task<List<string>> GetProtectedProcessesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the list of critical applications
        /// </summary>
        /// <returns>List of critical application names</returns>
        Task<List<string>> GetCriticalApplicationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the safety validation configuration
        /// </summary>
        /// <param name="configuration">New configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Update result</returns>
        Task<UpdateResult> UpdateConfigurationAsync(SafetyValidationConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the safety validation statistics
        /// </summary>
        /// <returns>Safety validation statistics</returns>
        Task<SafetyValidationStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the safety validation statistics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Reset result</returns>
        Task<ResetResult> ResetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a custom safety validation rule
        /// </summary>
        /// <param name="ruleName">Rule name</param>
        /// <param name="validationFunc">Validation function</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Add result</returns>
        Task<AddRuleResult> AddCustomValidationRuleAsync(string ruleName, Func<ValidationContext, Task<SafetyCheckResult>> validationFunc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a custom safety validation rule
        /// </summary>
        /// <param name="ruleName">Rule name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Remove result</returns>
        Task<RemoveRuleResult> RemoveCustomValidationRuleAsync(string ruleName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Event arguments for safety validation completion
    /// </summary>
    public class SafetyValidationCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the validation result
        /// </summary>
        public SafetyValidationResult Result { get; }

        /// <summary>
        /// Gets the validation context
        /// </summary>
        public ValidationContext Context { get; }

        /// <summary>
        /// Gets the validation duration
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Initializes a new instance of the SafetyValidationCompletedEventArgs class
        /// </summary>
        /// <param name="result">Validation result</param>
        /// <param name="context">Validation context</param>
        /// <param name="duration">Validation duration</param>
        public SafetyValidationCompletedEventArgs(SafetyValidationResult result, ValidationContext context, TimeSpan duration)
        {
            Result = result;
            Context = context;
            Duration = duration;
        }
    }

    /// <summary>
    /// Event arguments for safety check failure
    /// </summary>
    public class SafetyCheckFailedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the failed check name
        /// </summary>
        public string CheckName { get; }

        /// <summary>
        /// Gets the failure reason
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// Gets the failure severity
        /// </summary>
        public SafetyValidationAction RecommendedAction { get; }

        /// <summary>
        /// Gets the timestamp of the failure
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the SafetyCheckFailedEventArgs class
        /// </summary>
        /// <param name="checkName">Failed check name</param>
        /// <param name="failureReason">Failure reason</param>
        /// <param name="recommendedAction">Recommended action</param>
        public SafetyCheckFailedEventArgs(string checkName, string failureReason, SafetyValidationAction recommendedAction)
        {
            CheckName = checkName;
            FailureReason = failureReason;
            RecommendedAction = recommendedAction;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for user activity detection
    /// </summary>
    public class UserActivityEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the user name
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// Gets the activity information
        /// </summary>
        public UserActivityInfo ActivityInfo { get; }

        /// <summary>
        /// Gets the timestamp of the activity
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the UserActivityEventArgs class
        /// </summary>
        /// <param name="userName">User name</param>
        /// <param name="activityInfo">Activity information</param>
        public UserActivityEventArgs(string userName, UserActivityInfo activityInfo)
        {
            UserName = userName;
            ActivityInfo = activityInfo;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Validation context for safety operations
    /// </summary>
    public class ValidationContext
    {
        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public string OperationType { get; set; }

        /// <summary>
        /// Gets or sets the user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the license server address
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// Gets or sets the license server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the license feature name
        /// </summary>
        public string Feature { get; set; }

        /// <summary>
        /// Gets or sets the process name
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Gets or sets additional context data
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; set; }

        /// <summary>
        /// Initializes a new instance of the ValidationContext class
        /// </summary>
        public ValidationContext()
        {
            AdditionalData = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Initialization result
    /// </summary>
    public class InitializationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether initialization was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the initialization message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the initialization errors
        /// </summary>
        public List<string> Errors { get; set; }

        /// <summary>
        /// Initializes a new instance of the InitializationResult class
        /// </summary>
        public InitializationResult()
        {
            Errors = new List<string>();
        }
    }

    /// <summary>
    /// Monitoring result
    /// </summary>
    public class MonitoringResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether monitoring was started/stopped successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the monitoring message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the monitoring ID
        /// </summary>
        public string MonitoringId { get; set; }
    }

    /// <summary>
    /// Update result
    /// </summary>
    public class UpdateResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether update was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the update message
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Reset result
    /// </summary>
    public class ResetResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether reset was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the reset message
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Add rule result
    /// </summary>
    public class AddRuleResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether rule was added successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the add rule message
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Remove rule result
    /// </summary>
    public class RemoveRuleResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether rule was removed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the remove rule message
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Safety validation statistics
    /// </summary>
    public class SafetyValidationStatistics
    {
        /// <summary>
        /// Gets or sets the total number of validations performed
        /// </summary>
        public long TotalValidations { get; set; }

        /// <summary>
        /// Gets or sets the number of successful validations
        /// </summary>
        public long SuccessfulValidations { get; set; }

        /// <summary>
        /// Gets or sets the number of failed validations
        /// </summary>
        public long FailedValidations { get; set; }

        /// <summary>
        /// Gets or sets the number of warnings
        /// </summary>
        public long Warnings { get; set; }

        /// <summary>
        /// Gets or sets the average validation time in milliseconds
        /// </summary>
        public double AverageValidationTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the last validation time
        /// </summary>
        public DateTime LastValidationTime { get; set; }

        /// <summary>
        /// Gets or sets the success rate percentage
        /// </summary>
        public double SuccessRatePercentage { get; set; }

        /// <summary>
        /// Gets or sets the most common failure reason
        /// </summary>
        public string MostCommonFailureReason { get; set; }
    }
}