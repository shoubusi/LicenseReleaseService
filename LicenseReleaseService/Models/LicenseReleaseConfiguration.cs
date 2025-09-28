using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LicenseReleaseService.Models
{
    /// <summary>
    /// Main configuration class for the License Release Service
    /// </summary>
    public class LicenseReleaseConfiguration
    {
        /// <summary>
        /// Gets or sets whether the service is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the service check interval in seconds
        /// </summary>
        [DefaultValue(60)]
        public int CheckIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the maximum concurrent operations
        /// </summary>
        [DefaultValue(5)]
        public int MaxConcurrentOperations { get; set; } = 5;

        /// <summary>
        /// Gets or sets the license server configuration
        /// </summary>
        public LicenseServerConfiguration LicenseServer { get; set; } = new LicenseServerConfiguration();

        /// <summary>
        /// Gets or sets the error recovery configuration
        /// </summary>
        public ErrorRecoveryConfiguration ErrorRecovery { get; set; } = new ErrorRecoveryConfiguration();

        /// <summary>
        /// Gets or sets the health monitoring configuration
        /// </summary>
        public HealthMonitoringConfiguration HealthMonitoring { get; set; } = new HealthMonitoringConfiguration();

        /// <summary>
        /// Gets or sets the circuit breaker configuration
        /// </summary>
        public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new CircuitBreakerConfiguration();

        /// <summary>
        /// Gets or sets the retry policy configuration
        /// </summary>
        public RetryPolicyConfiguration RetryPolicy { get; set; } = new RetryPolicyConfiguration();

        /// <summary>
        /// Gets or sets the logging configuration
        /// </summary>
        public LoggingConfiguration Logging { get; set; } = new LoggingConfiguration();

        /// <summary>
        /// Gets or sets the performance configuration
        /// </summary>
        public PerformanceConfiguration Performance { get; set; } = new PerformanceConfiguration();

        /// <summary>
        /// Gets or sets the safety validation configuration
        /// </summary>
        public SafetyValidationConfiguration SafetyValidation { get; set; } = new SafetyValidationConfiguration();

        /// <summary>
        /// Gets or sets the timer execution configuration
        /// </summary>
        public TimerExecutionConfiguration TimerExecution { get; set; } = new TimerExecutionConfiguration();

        /// <summary>
        /// Gets or sets the configuration management settings
        /// </summary>
        public ConfigurationManagementSettings ConfigurationManagement { get; set; } = new ConfigurationManagementSettings();

        /// <summary>
        /// Gets or sets the deployment settings
        /// </summary>
        public DeploymentSettings Deployment { get; set; } = new DeploymentSettings();

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (CheckIntervalSeconds <= 0)
                errors.Add("Check interval must be greater than 0");

            if (MaxConcurrentOperations <= 0)
                errors.Add("Maximum concurrent operations must be greater than 0");

            // Validate nested configurations
            errors.AddRange(LicenseServer.Validate().Errors);
            errors.AddRange(ErrorRecovery.Validate().Errors);
            errors.AddRange(HealthMonitoring.Validate().Errors);
            errors.AddRange(CircuitBreaker.Validate().Errors);
            errors.AddRange(RetryPolicy.Validate().Errors);
            errors.AddRange(Logging.Validate().Errors);
            errors.AddRange(Performance.Validate().Errors);
            errors.AddRange(SafetyValidation.Validate().Errors);
            errors.AddRange(TimerExecution.Validate().Errors);
            errors.AddRange(ConfigurationManagement.Validate().Errors);
            errors.AddRange(Deployment.Validate().Errors);

            return new ValidationResult(errors);
        }

        /// <summary>
        /// Gets a summary of the configuration
        /// </summary>
        /// <returns>Configuration summary</returns>
        public string GetSummary()
        {
            return $"LicenseReleaseConfiguration: Enabled={Enabled}, CheckInterval={CheckIntervalSeconds}s, MaxOperations={MaxConcurrentOperations}";
        }
    }

    /// <summary>
    /// License server configuration
    /// </summary>
    public class LicenseServerConfiguration
    {
        /// <summary>
        /// Gets or sets the license server host
        /// </summary>
        [DefaultValue("localhost")]
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the license server port
        /// </summary>
        [DefaultValue(27000)]
        public int Port { get; set; } = 27000;

        /// <summary>
        /// Gets or sets the connection timeout in seconds
        /// </summary>
        [DefaultValue(30)]
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the license file path
        /// </summary>
        public string LicenseFilePath { get; set; } = @"C:\flexlm\license.dat";

        /// <summary>
        /// Gets or sets the lmutil executable path
        /// </summary>
        public string LmutilPath { get; set; } = @"C:\flexlm\lmutil.exe";

        /// <summary>
        /// Gets or sets the list of monitored license features
        /// </summary>
        public List<string> MonitoredFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Host))
                errors.Add("License server host cannot be empty");

            if (Port <= 0 || Port > 65535)
                errors.Add("License server port must be between 1 and 65535");

            if (ConnectionTimeoutSeconds <= 0)
                errors.Add("Connection timeout must be greater than 0");

            if (string.IsNullOrWhiteSpace(LicenseFilePath))
                errors.Add("License file path cannot be empty");

            if (string.IsNullOrWhiteSpace(LmutilPath))
                errors.Add("Lmutil path cannot be empty");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Error recovery configuration
    /// </summary>
    public class ErrorRecoveryConfiguration
    {
        /// <summary>
        /// Gets or sets whether error recovery is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum retry attempts
        /// </summary>
        [DefaultValue(3)]
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the retry delay in milliseconds
        /// </summary>
        [DefaultValue(1000)]
        public int RetryDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the backoff multiplier
        /// </summary>
        [DefaultValue(2.0)]
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the recovery timeout in seconds
        /// </summary>
        [DefaultValue(300)]
        public int RecoveryTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Gets or sets whether to enable graceful degradation
        /// </summary>
        [DefaultValue(true)]
        public bool EnableGracefulDegradation { get; set; } = true;

        /// <summary>
        /// Gets or sets the list of recoverable error types
        /// </summary>
        public List<string> RecoverableErrorTypes { get; set; } = new List<string>
        {
            "NetworkError",
            "TimeoutError",
            "TransientError"
        };

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (MaxRetryAttempts < 0)
                errors.Add("Maximum retry attempts must be non-negative");

            if (RetryDelayMilliseconds < 0)
                errors.Add("Retry delay must be non-negative");

            if (BackoffMultiplier <= 0)
                errors.Add("Backoff multiplier must be greater than 0");

            if (RecoveryTimeoutSeconds <= 0)
                errors.Add("Recovery timeout must be greater than 0");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Health monitoring configuration
    /// </summary>
    public class HealthMonitoringConfiguration
    {
        /// <summary>
        /// Gets or sets whether health monitoring is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the health check interval in seconds
        /// </summary>
        [DefaultValue(30)]
        public int HealthCheckIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the health check timeout in seconds
        /// </summary>
        [DefaultValue(10)]
        public int HealthCheckTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Gets or sets the health check endpoint
        /// </summary>
        [DefaultValue("http://localhost:8080/health")]
        public string HealthCheckEndpoint { get; set; } = "http://localhost:8080/health";

        /// <summary>
        /// Gets or sets the maximum consecutive failures before unhealthy
        /// </summary>
        [DefaultValue(3)]
        public int MaxConsecutiveFailures { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether to enable detailed health reporting
        /// </summary>
        [DefaultValue(true)]
        public bool EnableDetailedHealthReporting { get; set; } = true;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (HealthCheckIntervalSeconds <= 0)
                errors.Add("Health check interval must be greater than 0");

            if (HealthCheckTimeoutSeconds <= 0)
                errors.Add("Health check timeout must be greater than 0");

            if (string.IsNullOrWhiteSpace(HealthCheckEndpoint))
                errors.Add("Health check endpoint cannot be empty");

            if (MaxConsecutiveFailures <= 0)
                errors.Add("Maximum consecutive failures must be greater than 0");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public class CircuitBreakerConfiguration
    {
        /// <summary>
        /// Gets or sets whether circuit breaker is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the failure threshold to open circuit
        /// </summary>
        [DefaultValue(5)]
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets the reset timeout in seconds
        /// </summary>
        [DefaultValue(60)]
        public int ResetTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the half-open attempts
        /// </summary>
        [DefaultValue(1)]
        public int HalfOpenAttempts { get; set; } = 1;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (FailureThreshold <= 0)
                errors.Add("Failure threshold must be greater than 0");

            if (ResetTimeoutSeconds <= 0)
                errors.Add("Reset timeout must be greater than 0");

            if (HalfOpenAttempts <= 0)
                errors.Add("Half-open attempts must be greater than 0");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public class RetryPolicyConfiguration
    {
        /// <summary>
        /// Gets or sets whether retry policy is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum retry attempts
        /// </summary>
        [DefaultValue(3)]
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the initial delay in milliseconds
        /// </summary>
        [DefaultValue(1000)]
        public int InitialDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum delay in milliseconds
        /// </summary>
        [DefaultValue(30000)]
        public int MaxDelayMilliseconds { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the backoff multiplier
        /// </summary>
        [DefaultValue(2.0)]
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (MaxRetryAttempts < 0)
                errors.Add("Maximum retry attempts must be non-negative");

            if (InitialDelayMilliseconds < 0)
                errors.Add("Initial delay must be non-negative");

            if (MaxDelayMilliseconds < 0)
                errors.Add("Maximum delay must be non-negative");

            if (BackoffMultiplier <= 0)
                errors.Add("Backoff multiplier must be greater than 0");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Logging configuration
    /// </summary>
    public class LoggingConfiguration
    {
        /// <summary>
        /// Gets or sets the log level
        /// </summary>
        [DefaultValue("Information")]
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// Gets or sets the log file path
        /// </summary>
        public string LogFilePath { get; set; } = @"C:\Logs\LicenseReleaseService.log";

        /// <summary>
        /// Gets or sets the maximum log file size in MB
        /// </summary>
        [DefaultValue(10)]
        public int MaxLogFileSizeMB { get; set; } = 10;

        /// <summary>
        /// Gets or sets the maximum log files to retain
        /// </summary>
        [DefaultValue(5)]
        public int MaxLogFiles { get; set; } = 5;

        /// <summary>
        /// Gets or sets whether to enable event logging
        /// </summary>
        [DefaultValue(true)]
        public bool EnableEventLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable console logging
        /// </summary>
        [DefaultValue(true)]
        public bool EnableConsoleLogging { get; set; } = true;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(LogLevel))
                errors.Add("Log level cannot be empty");

            if (MaxLogFileSizeMB <= 0)
                errors.Add("Maximum log file size must be greater than 0");

            if (MaxLogFiles <= 0)
                errors.Add("Maximum log files must be greater than 0");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Performance configuration
    /// </summary>
    public class PerformanceConfiguration
    {
        /// <summary>
        /// Gets or sets whether performance monitoring is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the performance monitoring interval in seconds
        /// </summary>
        [DefaultValue(60)]
        public int MonitoringIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the CPU usage threshold percentage
        /// </summary>
        [DefaultValue(80)]
        public int CpuUsageThresholdPercent { get; set; } = 80;

        /// <summary>
        /// Gets or sets the memory usage threshold percentage
        /// </summary>
        [DefaultValue(85)]
        public int MemoryUsageThresholdPercent { get; set; } = 85;

        /// <summary>
        /// Gets or sets whether to enable performance counters
        /// </summary>
        [DefaultValue(true)]
        public bool EnablePerformanceCounters { get; set; } = true;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (MonitoringIntervalSeconds <= 0)
                errors.Add("Monitoring interval must be greater than 0");

            if (CpuUsageThresholdPercent <= 0 || CpuUsageThresholdPercent > 100)
                errors.Add("CPU usage threshold must be between 1 and 100");

            if (MemoryUsageThresholdPercent <= 0 || MemoryUsageThresholdPercent > 100)
                errors.Add("Memory usage threshold must be between 1 and 100");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Timer execution configuration
    /// </summary>
    public class TimerExecutionConfiguration
    {
        /// <summary>
        /// Gets or sets whether timer execution is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the timer interval in seconds
        /// </summary>
        [DefaultValue(60)]
        public int TimerIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the timer execution timeout in seconds
        /// </summary>
        [DefaultValue(30)]
        public int TimerTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets whether to enable timer synchronization
        /// </summary>
        [DefaultValue(true)]
        public bool EnableTimerSynchronization { get; set; } = true;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (TimerIntervalSeconds <= 0)
                errors.Add("Timer interval must be greater than 0");

            if (TimerTimeoutSeconds <= 0)
                errors.Add("Timer timeout must be greater than 0");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Configuration management settings
    /// </summary>
    public class ConfigurationManagementSettings
    {
        /// <summary>
        /// Gets or sets whether configuration management is enabled
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable hot reload
        /// </summary>
        [DefaultValue(true)]
        public bool EnableHotReload { get; set; } = true;

        /// <summary>
        /// Gets or sets the hot reload debounce time in seconds
        /// </summary>
        [DefaultValue(5)]
        public int HotReloadDebounceSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets whether to enable configuration validation
        /// </summary>
        [DefaultValue(true)]
        public bool EnableValidation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable configuration backup
        /// </summary>
        [DefaultValue(true)]
        public bool EnableBackup { get; set; } = true;

        /// <summary>
        /// Gets or sets the backup retention days
        /// </summary>
        [DefaultValue(30)]
        public int BackupRetentionDays { get; set; } = 30;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (HotReloadDebounceSeconds <= 0)
                errors.Add("Hot reload debounce time must be greater than 0");

            if (BackupRetentionDays <= 0)
                errors.Add("Backup retention days must be greater than 0");

            return new ValidationResult(errors);
        }
    }

    /// <summary>
    /// Deployment settings
    /// </summary>
    public class DeploymentSettings
    {
        /// <summary>
        /// Gets or sets the deployment environment
        /// </summary>
        [DefaultValue("Production")]
        public string Environment { get; set; } = "Production";

        /// <summary>
        /// Gets or sets the service account name
        /// </summary>
        public string ServiceAccount { get; set; } = "NT AUTHORITY\\LocalService";

        /// <summary>
        /// Gets or sets the service display name
        /// </summary>
        [DefaultValue("License Release Service")]
        public string ServiceDisplayName { get; set; } = "License Release Service";

        /// <summary>
        /// Gets or sets the service description
        /// </summary>
        [DefaultValue("Manages software license releases and monitoring")]
        public string ServiceDescription { get; set; } = "Manages software license releases and monitoring";

        /// <summary>
        /// Gets or sets the service startup mode
        /// </summary>
        [DefaultValue("Automatic")]
        public string StartupMode { get; set; } = "Automatic";

        /// <summary>
        /// Gets or sets whether to enable service recovery
        /// </summary>
        [DefaultValue(true)]
        public bool EnableServiceRecovery { get; set; } = true;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Environment))
                errors.Add("Environment cannot be empty");

            if (string.IsNullOrWhiteSpace(ServiceAccount))
                errors.Add("Service account cannot be empty");

            if (string.IsNullOrWhiteSpace(ServiceDisplayName))
                errors.Add("Service display name cannot be empty");

            if (string.IsNullOrWhiteSpace(ServiceDescription))
                errors.Add("Service description cannot be empty");

            if (string.IsNullOrWhiteSpace(StartupMode))
                errors.Add("Startup mode cannot be empty");

            return new ValidationResult(errors);
        }
    }
}