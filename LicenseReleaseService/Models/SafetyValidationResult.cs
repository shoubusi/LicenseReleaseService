using System;
using System.Collections.Generic;

namespace LicenseReleaseService.Models
{
    /// <summary>
    /// Represents the result of a safety validation operation
    /// </summary>
    public class SafetyValidationResult
    {
        /// <summary>
        /// Initializes a new instance of the SafetyValidationResult class
        /// </summary>
        public SafetyValidationResult()
        {
            ValidationChecks = new Dictionary<string, SafetyCheckResult>();
            Warnings = new List<string>();
            Errors = new List<string>();
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the validation was successful
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the overall safety status
        /// </summary>
        public SafetyStatus Status { get; set; } = SafetyStatus.Unknown;

        /// <summary>
        /// Gets or sets the validation message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the detailed validation checks
        /// </summary>
        public Dictionary<string, SafetyCheckResult> ValidationChecks { get; set; }

        /// <summary>
        /// Gets or sets the list of warnings
        /// </summary>
        public List<string> Warnings { get; set; }

        /// <summary>
        /// Gets or sets the list of errors
        /// </summary>
        public List<string> Errors { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the validation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the user activity information
        /// </summary>
        public UserActivityInfo UserActivity { get; set; }

        /// <summary>
        /// Gets or sets the system resource usage information
        /// </summary>
        public SystemResourceUsage SystemResources { get; set; }

        /// <summary>
        /// Gets or sets the recommended action
        /// </summary>
        public SafetyValidationAction RecommendedAction { get; set; }

        /// <summary>
        /// Gets or sets the failure reason if validation failed
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Gets or sets the confidence level of the validation
        /// </summary>
        public double ConfidenceLevel { get; set; }

        /// <summary>
        /// Gets a value indicating whether the operation can proceed safely
        /// </summary>
        public bool CanProceed => IsValid && RecommendedAction != SafetyValidationAction.Block;

        /// <summary>
        /// Gets a value indicating whether user confirmation is required
        /// </summary>
        public bool RequiresUserConfirmation => RecommendedAction == SafetyValidationAction.RequireConfirmation;

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        /// <param name="message">Success message</param>
        /// <returns>Successful validation result</returns>
        public static SafetyValidationResult Success(string message = "Safety validation passed")
        {
            return new SafetyValidationResult
            {
                IsValid = true,
                Status = SafetyStatus.Safe,
                Message = message,
                RecommendedAction = SafetyValidationAction.Allow,
                ConfidenceLevel = 1.0
            };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="failureReason">Reason for failure</param>
        /// <param name="action">Recommended action</param>
        /// <returns>Failed validation result</returns>
        public static SafetyValidationResult Failure(string failureReason, SafetyValidationAction action = SafetyValidationAction.Block)
        {
            return new SafetyValidationResult
            {
                IsValid = false,
                Status = SafetyStatus.Unsafe,
                Message = "Safety validation failed",
                FailureReason = failureReason,
                RecommendedAction = action,
                Errors = new List<string> { failureReason },
                ConfidenceLevel = 1.0
            };
        }

        /// <summary>
        /// Creates a warning validation result
        /// </summary>
        /// <param name="message">Warning message</param>
        /// <param name="warnings">List of warnings</param>
        /// <returns>Warning validation result</returns>
        public static SafetyValidationResult Warning(string message, List<string> warnings = null)
        {
            return new SafetyValidationResult
            {
                IsValid = true,
                Status = SafetyStatus.Warning,
                Message = message,
                Warnings = warnings ?? new List<string>(),
                RecommendedAction = SafetyValidationAction.Warn,
                ConfidenceLevel = 0.8
            };
        }

        /// <summary>
        /// Adds a validation check result
        /// </summary>
        /// <param name="checkName">Name of the validation check</param>
        /// <param name="passed">Whether the check passed</param>
        /// <param name="message">Check message</param>
        /// <param name="details">Additional details</param>
        public void AddValidationCheck(string checkName, bool passed, string message, object details = null)
        {
            ValidationChecks[checkName] = new SafetyCheckResult
            {
                CheckName = checkName,
                Passed = passed,
                Message = message,
                Details = details,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Adds a warning to the validation result
        /// </summary>
        /// <param name="warning">Warning message</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// Adds an error to the validation result
        /// </summary>
        /// <param name="error">Error message</param>
        public void AddError(string error)
        {
            Errors.Add(error);
        }

        /// <summary>
        /// Updates the overall validation status based on individual checks
        /// </summary>
        public void UpdateOverallStatus()
        {
            var hasCriticalFailures = false;
            var hasWarnings = false;
            var allChecksPassed = true;

            foreach (var check in ValidationChecks.Values)
            {
                if (!check.Passed)
                {
                    allChecksPassed = false;
                    if (check.IsCritical)
                    {
                        hasCriticalFailures = true;
                    }
                    else
                    {
                        hasWarnings = true;
                    }
                }
            }

            if (hasCriticalFailures)
            {
                Status = SafetyStatus.Unsafe;
                RecommendedAction = SafetyValidationAction.Block;
                IsValid = false;
            }
            else if (hasWarnings)
            {
                Status = SafetyStatus.Warning;
                RecommendedAction = SafetyValidationAction.Warn;
                IsValid = true;
            }
            else if (allChecksPassed)
            {
                Status = SafetyStatus.Safe;
                RecommendedAction = SafetyValidationAction.Allow;
                IsValid = true;
            }
            else
            {
                Status = SafetyStatus.Unknown;
                RecommendedAction = SafetyValidationAction.RequireConfirmation;
                IsValid = true;
            }
        }
    }

    /// <summary>
    /// Safety status levels
    /// </summary>
    public enum SafetyStatus
    {
        /// <summary>
        /// System is safe to proceed
        /// </summary>
        Safe,

        /// <summary>
        /// System has warnings but can proceed
        /// </summary>
        Warning,

        /// <summary>
        /// System is unsafe and should not proceed
        /// </summary>
        Unsafe,

        /// <summary>
        /// Safety status cannot be determined
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Represents the result of an individual safety check
    /// </summary>
    public class SafetyCheckResult
    {
        /// <summary>
        /// Gets or sets the name of the validation check
        /// </summary>
        public string CheckName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the check passed
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Gets or sets the check message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets additional details about the check
        /// </summary>
        public object Details { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a critical check
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the check
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Information about user activity
    /// </summary>
    public class UserActivityInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether the user is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the last activity time
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// Gets or sets the idle time duration
        /// </summary>
        public TimeSpan IdleDuration { get; set; }

        /// <summary>
        /// Gets or sets the list of active processes
        /// </summary>
        public List<string> ActiveProcesses { get; set; }

        /// <summary>
        /// Gets or sets the input device activity
        /// </summary>
        public InputDeviceActivity InputActivity { get; set; }
    }

    /// <summary>
    /// Input device activity information
    /// </summary>
    public class InputDeviceActivity
    {
        /// <summary>
        /// Gets or sets a value indicating whether there was keyboard activity
        /// </summary>
        public bool KeyboardActivity { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there was mouse activity
        /// </summary>
        public bool MouseActivity { get; set; }

        /// <summary>
        /// Gets or sets the last keyboard input time
        /// </summary>
        public DateTime LastKeyboardInput { get; set; }

        /// <summary>
        /// Gets or sets the last mouse input time
        /// </summary>
        public DateTime LastMouseInput { get; set; }
    }

    /// <summary>
    /// System resource usage information
    /// </summary>
    public class SystemResourceUsage
    {
        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Gets or sets the memory usage percentage
        /// </summary>
        public double MemoryUsagePercent { get; set; }

        /// <summary>
        /// Gets or sets the available memory in bytes
        /// </summary>
        public long AvailableMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the total memory in bytes
        /// </summary>
        public long TotalMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the disk usage percentage
        /// </summary>
        public double DiskUsagePercent { get; set; }

        /// <summary>
        /// Gets or sets the network activity status
        /// </summary>
        public NetworkActivity NetworkActivity { get; set; }

        /// <summary>
        /// Gets a value indicating whether system resources are under acceptable limits
        /// </summary>
        public bool IsWithinLimits => CpuUsagePercent <= 90 && MemoryUsagePercent <= 90;
    }

    /// <summary>
    /// Network activity information
    /// </summary>
    public class NetworkActivity
    {
        /// <summary>
        /// Gets or sets a value indicating whether network connectivity is available
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Gets or sets the network latency in milliseconds
        /// </summary>
        public double LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the number of active connections
        /// </summary>
        public int ActiveConnections { get; set; }
    }
}