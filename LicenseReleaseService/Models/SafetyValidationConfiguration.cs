using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LicenseReleaseService.Models
{
    /// <summary>
    /// Configuration for safety validation checks
    /// </summary>
    public class SafetyValidationConfiguration
    {
        /// <summary>
        /// Gets or sets whether safety validation is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the timeout for validation operations in seconds
        /// </summary>
        [DefaultValue(30)]
        public int ValidationTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum number of consecutive failures before safety lockout
        /// </summary>
        [DefaultValue(5)]
        public int MaxConsecutiveFailures { get; set; } = 5;

        /// <summary>
        /// Gets or sets the safety lockout duration in minutes
        /// </summary>
        [DefaultValue(60)]
        public int SafetyLockoutDurationMinutes { get; set; } = 60;

        /// <summary>
        /// Gets or sets the minimum user idle time in minutes before allowing license release
        /// </summary>
        [DefaultValue(15)]
        public int MinimumIdleTimeMinutes { get; set; } = 15;

        /// <summary>
        /// Gets or sets the list of protected processes that should not be interrupted
        /// </summary>
        public List<string> ProtectedProcesses { get; set; } = new List<string>
        {
            "explorer.exe",
            "lsass.exe",
            "csrss.exe",
            "winlogon.exe",
            "services.exe",
            "svchost.exe"
        };

        /// <summary>
        /// Gets or sets the list of critical applications that require user confirmation
        /// </summary>
        public List<string> CriticalApplications { get; set; } = new List<string>
        {
            "devenv.exe",
            "notepad++.exe",
            "code.exe",
            "winword.exe",
            "excel.exe"
        };

        /// <summary>
        /// Gets or sets whether to require user confirmation for license release
        /// </summary>
        [DefaultValue(true)]
        public bool RequireUserConfirmation { get; set; } = true;

        /// <summary>
        /// Gets or sets the user activity check interval in seconds
        /// </summary>
        [DefaultValue(5)]
        public int UserActivityCheckIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets the maximum CPU usage percentage before considering system busy
        /// </summary>
        [DefaultValue(80)]
        public int MaxCpuUsagePercent { get; set; } = 80;

        /// <summary>
        /// Gets or sets the maximum memory usage percentage before considering system busy
        /// </summary>
        [DefaultValue(85)]
        public int MaxMemoryUsagePercent { get; set; } = 85;

        /// <summary>
        /// Gets or sets whether to validate network connectivity
        /// </summary>
        [DefaultValue(true)]
        public bool ValidateNetworkConnectivity { get; set; } = true;

        /// <summary>
        /// Gets or sets the network connectivity test timeout in seconds
        /// </summary>
        [DefaultValue(10)]
        public int NetworkConnectivityTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Gets or sets the list of allowed license servers
        /// </summary>
        public List<string> AllowedLicenseServers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets whether to enable debug logging for safety validation
        /// </summary>
        [DefaultValue(false)]
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Gets or sets the custom validation rules
        /// </summary>
        public Dictionary<string, string> CustomValidationRules { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the validation failure action
        /// </summary>
        [DefaultValue(SafetyValidationAction.Block)]
        public SafetyValidationAction FailureAction { get; set; } = SafetyValidationAction.Block;

        /// <summary>
        /// Gets or sets the validation warning action
        /// </summary>
        [DefaultValue(SafetyValidationAction.Warn)]
        public SafetyValidationAction WarningAction { get; set; } = SafetyValidationAction.Warn;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (ValidationTimeoutSeconds <= 0)
                errors.Add("Validation timeout must be greater than 0");

            if (MaxConsecutiveFailures <= 0)
                errors.Add("Maximum consecutive failures must be greater than 0");

            if (SafetyLockoutDurationMinutes <= 0)
                errors.Add("Safety lockout duration must be greater than 0");

            if (MinimumIdleTimeMinutes < 0)
                errors.Add("Minimum idle time must be non-negative");

            if (UserActivityCheckIntervalSeconds <= 0)
                errors.Add("User activity check interval must be greater than 0");

            if (MaxCpuUsagePercent <= 0 || MaxCpuUsagePercent > 100)
                errors.Add("Maximum CPU usage must be between 1 and 100");

            if (MaxMemoryUsagePercent <= 0 || MaxMemoryUsagePercent > 100)
                errors.Add("Maximum memory usage must be between 1 and 100");

            if (NetworkConnectivityTimeoutSeconds <= 0)
                errors.Add("Network connectivity timeout must be greater than 0");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Safety validation actions
    /// </summary>
    public enum SafetyValidationAction
    {
        /// <summary>
        /// Allow the operation to proceed
        /// </summary>
        Allow,

        /// <summary>
        /// Warn the user but allow operation
        /// </summary>
        Warn,

        /// <summary>
        /// Block the operation
        /// </summary>
        Block,

        /// <summary>
        /// Require additional confirmation
        /// </summary>
        RequireConfirmation
    }

    /// <summary>
    /// Represents validation result
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Initializes a new instance of the ValidationResult class
        /// </summary>
        public ValidationResult()
        {
            Errors = new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the ValidationResult class
        /// </summary>
        /// <param name="errors">List of validation errors</param>
        public ValidationResult(List<string> errors)
        {
            Errors = errors ?? new List<string>();
        }

        /// <summary>
        /// Gets a value indicating whether the validation was successful
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets the list of validation errors
        /// </summary>
        public List<string> Errors { get; }
    }
}