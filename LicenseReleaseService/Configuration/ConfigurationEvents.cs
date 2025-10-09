using System;
using System.Collections.Generic;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Defines configuration-related events and event arguments
    /// </summary>
    public static class ConfigurationEvents
    {
        /// <summary>
        /// Event handler for configuration file changes
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        public delegate void ConfigurationFileChangedEventHandler(object sender, ConfigurationFileChangedEventArgs e);

        /// <summary>
        /// Event handler for configuration reload events
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        public delegate void ConfigurationReloadEventHandler(object sender, ConfigurationReloadEventArgs e);

        /// <summary>
        /// Event handler for configuration health events
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        public delegate void ConfigurationHealthEventHandler(object sender, ConfigurationHealthEventArgs e);

        /// <summary>
        /// Event handler for configuration backup events
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        public delegate void ConfigurationBackupEventHandler(object sender, ConfigurationBackupEventArgs e);
    }

    /// <summary>
    /// Event arguments for configuration file change events
    /// </summary>
    public class ConfigurationFileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the type of file change
        /// </summary>
        public FileChangeType ChangeType { get; }

        /// <summary>
        /// Gets the file path that changed
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the old file name (for rename operations)
        /// </summary>
        public string OldFileName { get; }

        /// <summary>
        /// Gets the new file name (for rename operations)
        /// </summary>
        public string NewFileName { get; }

        /// <summary>
        /// Gets the timestamp when the change was detected
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the file size in bytes
        /// </summary>
        public long FileSize { get; }

        /// <summary>
        /// Gets whether the change was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message if the change failed
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationFileChangedEventArgs class
        /// </summary>
        public ConfigurationFileChangedEventArgs(FileChangeType changeType, string filePath, DateTime timestamp, long fileSize, bool isSuccess = true, string errorMessage = null)
        {
            ChangeType = changeType;
            FilePath = filePath;
            Timestamp = timestamp;
            FileSize = fileSize;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Initializes a new instance of the ConfigurationFileChangedEventArgs class for rename operations
        /// </summary>
        public ConfigurationFileChangedEventArgs(string oldFileName, string newFileName, DateTime timestamp, bool isSuccess = true, string errorMessage = null)
        {
            ChangeType = FileChangeType.Renamed;
            OldFileName = oldFileName;
            NewFileName = newFileName;
            FilePath = newFileName;
            Timestamp = timestamp;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Event arguments for configuration reload events
    /// </summary>
    public class ConfigurationReloadEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old configuration
        /// </summary>
        public LicenseReleaseServiceSection OldConfiguration { get; }

        /// <summary>
        /// Gets the new configuration
        /// </summary>
        public LicenseReleaseServiceSection NewConfiguration { get; }

        /// <summary>
        /// Gets the validation errors
        /// </summary>
        public List<string> ValidationErrors { get; }

        /// <summary>
        /// Gets whether the reload was successful
        /// </summary>
        public bool IsSuccess => ValidationErrors.Count == 0;

        /// <summary>
        /// Gets the reload duration
        /// </summary>
        public TimeSpan ReloadDuration { get; }

        /// <summary>
        /// Gets the timestamp when the reload was initiated
        /// </summary>
        public DateTime InitiatedAt { get; }

        /// <summary>
        /// Gets the timestamp when the reload completed
        /// </summary>
        public DateTime CompletedAt { get; }

        /// <summary>
        /// Gets the reload trigger source
        /// </summary>
        public ReloadTrigger Trigger { get; }

        /// <summary>
        /// Gets whether this was a rollback operation
        /// </summary>
        public bool IsRollback { get; }

        /// <summary>
        /// Gets the backup file path if backup was created
        /// </summary>
        public string BackupFilePath { get; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationReloadEventArgs class
        /// </summary>
        public ConfigurationReloadEventArgs(LicenseReleaseServiceSection oldConfig, LicenseReleaseServiceSection newConfig, List<string> validationErrors, ReloadTrigger trigger, TimeSpan reloadDuration, DateTime initiatedAt, DateTime completedAt, bool isRollback = false, string backupFilePath = null, string errorMessage = null)
        {
            OldConfiguration = oldConfig;
            NewConfiguration = newConfig;
            ValidationErrors = validationErrors ?? new List<string>();
            Trigger = trigger;
            ReloadDuration = reloadDuration;
            InitiatedAt = initiatedAt;
            CompletedAt = completedAt;
            IsRollback = isRollback;
            BackupFilePath = backupFilePath;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets the error message if the reload failed
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Constructor for simple reload scenarios
        /// </summary>
        public ConfigurationReloadEventArgs(LicenseReleaseServiceSection oldConfig, LicenseReleaseServiceSection newConfig, List<string> validationErrors)
        {
            OldConfiguration = oldConfig;
            NewConfiguration = newConfig;
            ValidationErrors = validationErrors ?? new List<string>();
            Trigger = ReloadTrigger.Manual;
            ReloadDuration = TimeSpan.Zero;
            InitiatedAt = DateTime.UtcNow;
            CompletedAt = DateTime.UtcNow;
            IsRollback = false;
            BackupFilePath = null;
            ErrorMessage = null;
        }
    }

    /// <summary>
    /// Event arguments for configuration health events
    /// </summary>
    public class ConfigurationHealthEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the configuration health status
        /// </summary>
        public ConfigurationHealthStatus HealthStatus { get; }

        /// <summary>
        /// Gets the health check results
        /// </summary>
        public Dictionary<string, HealthCheckResult> HealthChecks { get; }

        /// <summary>
        /// Gets the configuration metrics
        /// </summary>
        public Dictionary<string, ConfigurationMetric> Metrics { get; }

        /// <summary>
        /// Gets the timestamp when the health check was performed
        /// </summary>
        public DateTime CheckedAt { get; }

        /// <summary>
        /// Gets the health check duration
        /// </summary>
        public TimeSpan CheckDuration { get; }

        /// <summary>
        /// Gets whether the configuration is considered healthy
        /// </summary>
        public bool IsHealthy => HealthStatus == ConfigurationHealthStatus.Healthy;

        /// <summary>
        /// Gets the list of health issues
        /// </summary>
        public List<string> HealthIssues { get; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationHealthEventArgs class
        /// </summary>
        public ConfigurationHealthEventArgs(ConfigurationHealthStatus healthStatus, Dictionary<string, HealthCheckResult> healthChecks, Dictionary<string, ConfigurationMetric> metrics, DateTime checkedAt, TimeSpan checkDuration, List<string> healthIssues = null)
        {
            HealthStatus = healthStatus;
            HealthChecks = healthChecks ?? new Dictionary<string, HealthCheckResult>();
            Metrics = metrics ?? new Dictionary<string, ConfigurationMetric>();
            CheckedAt = checkedAt;
            CheckDuration = checkDuration;
            HealthIssues = healthIssues ?? new List<string>();
        }
    }

    /// <summary>
    /// Event arguments for configuration backup events
    /// </summary>
    public class ConfigurationBackupEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the backup operation type
        /// </summary>
        public BackupOperationType OperationType { get; }

        /// <summary>
        /// Gets the source configuration file path
        /// </summary>
        public string SourceFilePath { get; }

        /// <summary>
        /// Gets the backup file path
        /// </summary>
        public string BackupFilePath { get; }

        /// <summary>
        /// Gets the timestamp when the backup was created
        /// </summary>
        public DateTime BackupTimestamp { get; }

        /// <summary>
        /// Gets whether the backup operation was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message if the backup failed
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Gets the backup file size in bytes
        /// </summary>
        public long BackupFileSize { get; }

        /// <summary>
        /// Gets the backup reason
        /// </summary>
        public BackupReason Reason { get; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationBackupEventArgs class
        /// </summary>
        public ConfigurationBackupEventArgs(BackupOperationType operationType, string sourceFilePath, string backupFilePath, DateTime backupTimestamp, bool isSuccess, BackupReason reason, long backupFileSize = 0, string errorMessage = null)
        {
            OperationType = operationType;
            SourceFilePath = sourceFilePath;
            BackupFilePath = backupFilePath;
            BackupTimestamp = backupTimestamp;
            IsSuccess = isSuccess;
            Reason = reason;
            BackupFileSize = backupFileSize;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Types of file changes
    /// </summary>
    public enum FileChangeType
    {
        /// <summary>
        /// File was created
        /// </summary>
        Created,

        /// <summary>
        /// File was changed
        /// </summary>
        Changed,

        /// <summary>
        /// File was deleted
        /// </summary>
        Deleted,

        /// <summary>
        /// File was renamed
        /// </summary>
        Renamed
    }

    /// <summary>
    /// Configuration health status
    /// </summary>
    public enum ConfigurationHealthStatus
    {
        /// <summary>
        /// Configuration is healthy
        /// </summary>
        Healthy,

        /// <summary>
        /// Configuration has warnings but is functional
        /// </summary>
        Warning,

        /// <summary>
        /// Configuration has errors and may not function properly
        /// </summary>
        Error,

        /// <summary>
        /// Configuration health is unknown
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Reload trigger types
    /// </summary>
    public enum ReloadTrigger
    {
        /// <summary>
        /// Manual reload triggered by user
        /// </summary>
        Manual,

        /// <summary>
        /// Automatic reload triggered by file change
        /// </summary>
        FileChange,

        /// <summary>
        /// Reload triggered by timer
        /// </summary>
        Timer,

        /// <summary>
        /// Reload triggered by health check
        /// </summary>
        HealthCheck,

        /// <summary>
        /// Reload triggered by rollback operation
        /// </summary>
        Rollback
    }

    /// <summary>
    /// Backup operation types
    /// </summary>
    public enum BackupOperationType
    {
        /// <summary>
        /// Backup was created
        /// </summary>
        Create,

        /// <summary>
        /// Backup was restored
        /// </summary>
        Restore,

        /// <summary>
        /// Backup was deleted
        /// </summary>
        Delete,

        /// <summary>
        /// Backup was cleaned up
        /// </summary>
        Cleanup
    }

    /// <summary>
    /// Backup reasons
    /// </summary>
    public enum BackupReason
    {
        /// <summary>
        /// Backup before configuration change
        /// </summary>
        PreChange,

        /// <summary>
        /// Scheduled backup
        /// </summary>
        Scheduled,

        /// <summary>
        /// Backup before reload
        /// </summary>
        PreReload,

        /// <summary>
        /// Manual backup
        /// </summary>
        Manual,

        /// <summary>
        /// Emergency backup
        /// </summary>
        Emergency
    }

    /// <summary>
    /// Health check result for configuration components
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// Gets the health status
        /// </summary>
        public ConfigurationHealthStatus Status { get; set; }

        /// <summary>
        /// Gets the health check description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets the error message if the check failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets the duration of the health check
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets the component name that was checked
        /// </summary>
        public string ComponentName { get; set; }

        /// <summary>
        /// Gets additional details about the health check
        /// </summary>
        public Dictionary<string, object> Details { get; set; }

        /// <summary>
        /// Initializes a new instance of the HealthCheckResult class
        /// </summary>
        public HealthCheckResult(ConfigurationHealthStatus status, string description, string componentName, TimeSpan duration, string errorMessage = null, Dictionary<string, object> details = null)
        {
            Status = status;
            Description = description;
            ComponentName = componentName;
            Duration = duration;
            ErrorMessage = errorMessage;
            Details = details ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Configuration metric for monitoring
    /// </summary>
    public class ConfigurationMetric
    {
        /// <summary>
        /// Gets the metric name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the metric value
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets the metric unit
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// Gets the metric description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets the timestamp when the metric was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets the metric history
        /// </summary>
        public Queue<MetricValue> History { get; set; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationMetric class
        /// </summary>
        public ConfigurationMetric(string name, double value, string unit, string description)
        {
            Name = name;
            Value = value;
            Unit = unit;
            Description = description;
            LastUpdated = DateTime.UtcNow;
            History = new Queue<MetricValue>();
        }

        /// <summary>
        /// Adds a value to the metric history
        /// </summary>
        public void AddHistoryValue(double value)
        {
            var metricValue = new MetricValue
            {
                Value = value,
                Timestamp = DateTime.UtcNow
            };

            History.Enqueue(metricValue);

            // Keep only last 100 values
            while (History.Count > 100)
            {
                History.Dequeue();
            }
        }
    }

    /// <summary>
    /// Metric value with timestamp
    /// </summary>
    public class MetricValue
    {
        /// <summary>
        /// Gets the metric value
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets the timestamp when the value was recorded
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}