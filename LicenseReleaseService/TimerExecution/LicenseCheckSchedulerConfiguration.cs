using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Configuration for the license check scheduler
    /// </summary>
    public class LicenseCheckSchedulerConfiguration
    {
        private TimeSpan _defaultCheckInterval = TimeSpan.FromMinutes(5);
        private TimeSpan _executionTimeout = TimeSpan.FromMinutes(2);
        private TimeSpan _maxRetryDelay = TimeSpan.FromMinutes(10);
        private int _maxConcurrentChecks = 2;
        private int _maxQueueSize = 1000;
        private int _maxRetryCount = 3;
        private bool _enableQueueing = true;
        private bool _enablePrioritization = true;
        private bool _enableRetry = true;
        private bool _enableMetrics = true;
        private bool _enableDetailedLogging = true;
        private TimeSpan _metricsRetentionPeriod = TimeSpan.FromDays(7);
        private int _maxExecutionHistory = 1000;
        private int _cleanupIntervalMinutes = 60;
        private double _queueCapacityWarningThreshold = 0.8;
        private TimeSpan _queueProcessingInterval = TimeSpan.FromSeconds(30);
        private List<string> _defaultFeatureCodes = new List<string> { "solidworks" };
        private string _defaultLicenseServer = "localhost";
        private int _defaultPort = 27000;
        private List<string> _supportedVersions = new List<string> { "2020", "2021", "2022", "2023", "2024", "2025" };

        /// <summary>
        /// Gets or sets the default interval between license checks
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:05:00")]
        public TimeSpan DefaultCheckInterval
        {
            get => _defaultCheckInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Default check interval must be greater than zero", nameof(value));
                _defaultCheckInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout for individual license check executions
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:02:00")]
        public TimeSpan ExecutionTimeout
        {
            get => _executionTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Execution timeout must be greater than zero", nameof(value));
                _executionTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum delay between retry attempts
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:10:00")]
        public TimeSpan MaxRetryDelay
        {
            get => _maxRetryDelay;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Max retry delay must be greater than zero", nameof(value));
                _maxRetryDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent license checks
        /// </summary>
        [DefaultValue(2)]
        public int MaxConcurrentChecks
        {
            get => _maxConcurrentChecks;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Max concurrent checks must be at least 1", nameof(value));
                _maxConcurrentChecks = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum size of the operation queue
        /// </summary>
        [DefaultValue(1000)]
        public int MaxQueueSize
        {
            get => _maxQueueSize;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Max queue size must be at least 1", nameof(value));
                _maxQueueSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed operations
        /// </summary>
        [DefaultValue(3)]
        public int MaxRetryCount
        {
            get => _maxRetryCount;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Max retry count cannot be negative", nameof(value));
                _maxRetryCount = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable operation queueing
        /// </summary>
        [DefaultValue(true)]
        public bool EnableQueueing
        {
            get => _enableQueueing;
            set => _enableQueueing = value;
        }

        /// <summary>
        /// Gets or sets whether to enable operation prioritization
        /// </summary>
        [DefaultValue(true)]
        public bool EnablePrioritization
        {
            get => _enablePrioritization;
            set => _enablePrioritization = value;
        }

        /// <summary>
        /// Gets or sets whether to enable retry for failed operations
        /// </summary>
        [DefaultValue(true)]
        public bool EnableRetry
        {
            get => _enableRetry;
            set => _enableRetry = value;
        }

        /// <summary>
        /// Gets or sets whether to enable metrics collection
        /// </summary>
        [DefaultValue(true)]
        public bool EnableMetrics
        {
            get => _enableMetrics;
            set => _enableMetrics = value;
        }

        /// <summary>
        /// Gets or sets whether to enable detailed logging
        /// </summary>
        [DefaultValue(true)]
        public bool EnableDetailedLogging
        {
            get => _enableDetailedLogging;
            set => _enableDetailedLogging = value;
        }

        /// <summary>
        /// Gets or sets the retention period for metrics data
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "7.00:00:00")]
        public TimeSpan MetricsRetentionPeriod
        {
            get => _metricsRetentionPeriod;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Metrics retention period must be greater than zero", nameof(value));
                _metricsRetentionPeriod = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of execution history items to keep
        /// </summary>
        [DefaultValue(1000)]
        public int MaxExecutionHistory
        {
            get => _maxExecutionHistory;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Max execution history cannot be negative", nameof(value));
                _maxExecutionHistory = value;
            }
        }

        /// <summary>
        /// Gets or sets the interval for cleanup operations in minutes
        /// </summary>
        [DefaultValue(60)]
        public int CleanupIntervalMinutes
        {
            get => _cleanupIntervalMinutes;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Cleanup interval must be at least 1 minute", nameof(value));
                _cleanupIntervalMinutes = value;
            }
        }

        /// <summary>
        /// Gets or sets the queue capacity warning threshold (0.0 to 1.0)
        /// </summary>
        [DefaultValue(0.8)]
        public double QueueCapacityWarningThreshold
        {
            get => _queueCapacityWarningThreshold;
            set
            {
                if (value < 0.0 || value > 1.0)
                    throw new ArgumentException("Queue capacity warning threshold must be between 0.0 and 1.0", nameof(value));
                _queueCapacityWarningThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the interval for queue processing
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:30")]
        public TimeSpan QueueProcessingInterval
        {
            get => _queueProcessingInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Queue processing interval must be greater than zero", nameof(value));
                _queueProcessingInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the default feature codes to check
        /// </summary>
        public List<string> DefaultFeatureCodes
        {
            get => _defaultFeatureCodes;
            set => _defaultFeatureCodes = value ?? new List<string>();
        }

        /// <summary>
        /// Gets or sets the default license server address
        /// </summary>
        [DefaultValue("localhost")]
        public string DefaultLicenseServer
        {
            get => _defaultLicenseServer;
            set => _defaultLicenseServer = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets or sets the default license server port
        /// </summary>
        [DefaultValue(27000)]
        public int DefaultPort
        {
            get => _defaultPort;
            set
            {
                if (value <= 0 || value > 65535)
                    throw new ArgumentException("Port must be between 1 and 65535", nameof(value));
                _defaultPort = value;
            }
        }

        /// <summary>
        /// Gets or sets the list of supported SolidWorks versions
        /// </summary>
        public List<string> SupportedVersions
        {
            get => _supportedVersions;
            set => _supportedVersions = value ?? new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckSchedulerConfiguration class
        /// </summary>
        public LicenseCheckSchedulerConfiguration()
        {
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (DefaultCheckInterval <= TimeSpan.Zero)
                errors.Add("Default check interval must be greater than zero");

            if (ExecutionTimeout <= TimeSpan.Zero)
                errors.Add("Execution timeout must be greater than zero");

            if (MaxRetryDelay <= TimeSpan.Zero)
                errors.Add("Max retry delay must be greater than zero");

            if (MaxConcurrentChecks < 1)
                errors.Add("Max concurrent checks must be at least 1");

            if (MaxQueueSize < 1)
                errors.Add("Max queue size must be at least 1");

            if (MaxRetryCount < 0)
                errors.Add("Max retry count cannot be negative");

            if (MetricsRetentionPeriod <= TimeSpan.Zero)
                errors.Add("Metrics retention period must be greater than zero");

            if (MaxExecutionHistory < 0)
                errors.Add("Max execution history cannot be negative");

            if (CleanupIntervalMinutes < 1)
                errors.Add("Cleanup interval must be at least 1 minute");

            if (QueueCapacityWarningThreshold < 0.0 || QueueCapacityWarningThreshold > 1.0)
                errors.Add("Queue capacity warning threshold must be between 0.0 and 1.0");

            if (QueueProcessingInterval <= TimeSpan.Zero)
                errors.Add("Queue processing interval must be greater than zero");

            if (string.IsNullOrWhiteSpace(DefaultLicenseServer))
                errors.Add("Default license server cannot be empty");

            if (DefaultPort <= 0 || DefaultPort > 65535)
                errors.Add("Default port must be between 1 and 65535");

            // Validate supported versions
            foreach (var version in SupportedVersions)
            {
                if (string.IsNullOrWhiteSpace(version))
                    errors.Add("Supported versions cannot contain empty strings");
            }

            // Validate feature codes
            foreach (var featureCode in DefaultFeatureCodes)
            {
                if (string.IsNullOrWhiteSpace(featureCode))
                    errors.Add("Default feature codes cannot contain empty strings");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of the current configuration
        /// </summary>
        /// <returns>Copy of the configuration</returns>
        public LicenseCheckSchedulerConfiguration Clone()
        {
            return new LicenseCheckSchedulerConfiguration
            {
                DefaultCheckInterval = DefaultCheckInterval,
                ExecutionTimeout = ExecutionTimeout,
                MaxRetryDelay = MaxRetryDelay,
                MaxConcurrentChecks = MaxConcurrentChecks,
                MaxQueueSize = MaxQueueSize,
                MaxRetryCount = MaxRetryCount,
                EnableQueueing = EnableQueueing,
                EnablePrioritization = EnablePrioritization,
                EnableRetry = EnableRetry,
                EnableMetrics = EnableMetrics,
                EnableDetailedLogging = EnableDetailedLogging,
                MetricsRetentionPeriod = MetricsRetentionPeriod,
                MaxExecutionHistory = MaxExecutionHistory,
                CleanupIntervalMinutes = CleanupIntervalMinutes,
                QueueCapacityWarningThreshold = QueueCapacityWarningThreshold,
                QueueProcessingInterval = QueueProcessingInterval,
                DefaultFeatureCodes = new List<string>(DefaultFeatureCodes),
                DefaultLicenseServer = DefaultLicenseServer,
                DefaultPort = DefaultPort,
                SupportedVersions = new List<string>(SupportedVersions)
            };
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseCheckSchedulerConfiguration [Interval={DefaultCheckInterval}, Concurrent={MaxConcurrentChecks}, QueueSize={MaxQueueSize}]";
        }

        /// <summary>
        /// Gets default configuration for license check scheduler
        /// </summary>
        /// <returns>Default configuration</returns>
        public static LicenseCheckSchedulerConfiguration GetDefault()
        {
            return new LicenseCheckSchedulerConfiguration
            {
                DefaultCheckInterval = TimeSpan.FromMinutes(5),
                ExecutionTimeout = TimeSpan.FromMinutes(2),
                MaxRetryDelay = TimeSpan.FromMinutes(10),
                MaxConcurrentChecks = 2,
                MaxQueueSize = 1000,
                MaxRetryCount = 3,
                EnableQueueing = true,
                EnablePrioritization = true,
                EnableRetry = true,
                EnableMetrics = true,
                EnableDetailedLogging = true,
                MetricsRetentionPeriod = TimeSpan.FromDays(7),
                MaxExecutionHistory = 1000,
                CleanupIntervalMinutes = 60,
                QueueCapacityWarningThreshold = 0.8,
                QueueProcessingInterval = TimeSpan.FromSeconds(30),
                DefaultFeatureCodes = new List<string> { "solidworks" },
                DefaultLicenseServer = "localhost",
                DefaultPort = 27000,
                SupportedVersions = new List<string> { "2020", "2021", "2022", "2023", "2024", "2025" }
            };
        }

        /// <summary>
        /// Gets configuration optimized for high-frequency monitoring
        /// </summary>
        /// <returns>High-frequency configuration</returns>
        public static LicenseCheckSchedulerConfiguration GetHighFrequencyConfiguration()
        {
            return new LicenseCheckSchedulerConfiguration
            {
                DefaultCheckInterval = TimeSpan.FromSeconds(30),
                ExecutionTimeout = TimeSpan.FromSeconds(30),
                MaxRetryDelay = TimeSpan.FromMinutes(2),
                MaxConcurrentChecks = 4,
                MaxQueueSize = 500,
                MaxRetryCount = 2,
                EnableQueueing = true,
                EnablePrioritization = true,
                EnableRetry = true,
                EnableMetrics = true,
                EnableDetailedLogging = false,
                MetricsRetentionPeriod = TimeSpan.FromDays(1),
                MaxExecutionHistory = 500,
                CleanupIntervalMinutes = 30,
                QueueCapacityWarningThreshold = 0.9,
                QueueProcessingInterval = TimeSpan.FromSeconds(10),
                DefaultFeatureCodes = new List<string> { "solidworks" },
                DefaultLicenseServer = "localhost",
                DefaultPort = 27000,
                SupportedVersions = new List<string> { "2020", "2021", "2022", "2023", "2024", "2025" }
            };
        }

        /// <summary>
        /// Gets configuration optimized for low-frequency monitoring
        /// </summary>
        /// <returns>Low-frequency configuration</returns>
        public static LicenseCheckSchedulerConfiguration GetLowFrequencyConfiguration()
        {
            return new LicenseCheckSchedulerConfiguration
            {
                DefaultCheckInterval = TimeSpan.FromHours(1),
                ExecutionTimeout = TimeSpan.FromMinutes(5),
                MaxRetryDelay = TimeSpan.FromHours(1),
                MaxConcurrentChecks = 1,
                MaxQueueSize = 100,
                MaxRetryCount = 3,
                EnableQueueing = true,
                EnablePrioritization = true,
                EnableRetry = true,
                EnableMetrics = true,
                EnableDetailedLogging = true,
                MetricsRetentionPeriod = TimeSpan.FromDays(30),
                MaxExecutionHistory = 2000,
                CleanupIntervalMinutes = 120,
                QueueCapacityWarningThreshold = 0.7,
                QueueProcessingInterval = TimeSpan.FromMinutes(1),
                DefaultFeatureCodes = new List<string> { "solidworks" },
                DefaultLicenseServer = "localhost",
                DefaultPort = 27000,
                SupportedVersions = new List<string> { "2020", "2021", "2022", "2023", "2024", "2025" }
            };
        }

        /// <summary>
        /// Gets configuration optimized for testing scenarios
        /// </summary>
        /// <returns>Test configuration</returns>
        public static LicenseCheckSchedulerConfiguration GetTestConfiguration()
        {
            return new LicenseCheckSchedulerConfiguration
            {
                DefaultCheckInterval = TimeSpan.FromSeconds(1),
                ExecutionTimeout = TimeSpan.FromSeconds(5),
                MaxRetryDelay = TimeSpan.FromSeconds(5),
                MaxConcurrentChecks = 1,
                MaxQueueSize = 10,
                MaxRetryCount = 1,
                EnableQueueing = true,
                EnablePrioritization = true,
                EnableRetry = true,
                EnableMetrics = true,
                EnableDetailedLogging = true,
                MetricsRetentionPeriod = TimeSpan.FromHours(1),
                MaxExecutionHistory = 50,
                CleanupIntervalMinutes = 5,
                QueueCapacityWarningThreshold = 0.8,
                QueueProcessingInterval = TimeSpan.FromSeconds(1),
                DefaultFeatureCodes = new List<string> { "solidworks" },
                DefaultLicenseServer = "localhost",
                DefaultPort = 27000,
                SupportedVersions = new List<string> { "2020", "2021", "2022", "2023", "2024", "2025" }
            };
        }
    }
}