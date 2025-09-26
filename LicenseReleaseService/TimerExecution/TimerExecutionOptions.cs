using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Configuration options for timer execution behavior
    /// </summary>
    public class TimerExecutionOptions
    {
        private TimeSpan _defaultInterval = TimeSpan.FromMinutes(1);
        private TimeSpan _executionTimeout = TimeSpan.FromMinutes(5);
        private TimeSpan _circuitBreakerCooldown = TimeSpan.FromMinutes(5);
        private int _maxConsecutiveErrors = 5;
        private int _maxConcurrentExecutions = 1;
        private bool _enableAutoRestart = true;
        private bool _enableExecutionTimeout = true;
        private bool _enableMetrics = true;
        private bool _enableDetailedLogging = true;
        private bool _enableCircuitBreaker = true;
        private int _maxExecutionHistory = 100;
        private string _targetVersion = string.Empty;
        private List<string> _supportedVersions = new List<string>();
        private bool _enableVersionSpecificConfig = false;
        private TimeSpan _versionDetectionInterval = TimeSpan.FromMinutes(30);
        private bool _enableVersionFallback = true;
        private string _defaultVersion = string.Empty;

        /// <summary>
        /// Gets or sets the default timer interval
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:01:00")]
        public TimeSpan DefaultInterval
        {
            get => _defaultInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Default interval must be greater than zero", nameof(value));
                }
                _defaultInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of consecutive errors before triggering circuit breaker
        /// </summary>
        [DefaultValue(5)]
        public int MaxConsecutiveErrors
        {
            get => _maxConsecutiveErrors;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("Max consecutive errors must be at least 1", nameof(value));
                }
                _maxConsecutiveErrors = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent executions allowed
        /// </summary>
        [DefaultValue(1)]
        public int MaxConcurrentExecutions
        {
            get => _maxConcurrentExecutions;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("Max concurrent executions must be at least 1", nameof(value));
                }
                _maxConcurrentExecutions = value;
            }
        }

        /// <summary>
        /// Gets or sets the cooldown period for circuit breaker
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:05:00")]
        public TimeSpan CircuitBreakerCooldown
        {
            get => _circuitBreakerCooldown;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Circuit breaker cooldown must be greater than zero", nameof(value));
                }
                _circuitBreakerCooldown = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable automatic restart after failures
        /// </summary>
        [DefaultValue(true)]
        public bool EnableAutoRestart
        {
            get => _enableAutoRestart;
            set => _enableAutoRestart = value;
        }

        /// <summary>
        /// Gets or sets whether to enable execution timeout
        /// </summary>
        [DefaultValue(true)]
        public bool EnableExecutionTimeout
        {
            get => _enableExecutionTimeout;
            set => _enableExecutionTimeout = value;
        }

        /// <summary>
        /// Gets or sets the execution timeout for individual operations
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:05:00")]
        public TimeSpan ExecutionTimeout
        {
            get => _executionTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Execution timeout must be greater than zero", nameof(value));
                }
                _executionTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable performance metrics collection
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
        /// Gets or sets whether to enable circuit breaker functionality
        /// </summary>
        [DefaultValue(true)]
        public bool EnableCircuitBreaker
        {
            get => _enableCircuitBreaker;
            set => _enableCircuitBreaker = value;
        }

        /// <summary>
        /// Gets or sets the maximum number of execution history items to keep
        /// </summary>
        [DefaultValue(100)]
        public int MaxExecutionHistory
        {
            get => _maxExecutionHistory;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Max execution history cannot be negative", nameof(value));
                }
                _maxExecutionHistory = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum interval allowed
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:01")]
        public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum interval allowed
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "1.00:00:00")]
        public TimeSpan MaxInterval { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Gets or sets the synchronization timeout for thread-safe operations
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:30")]
        public TimeSpan SyncTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether to stop timer on unhandled exceptions
        /// </summary>
        [DefaultValue(false)]
        public bool StopOnUnhandledException { get; set; } = false;

        /// <summary>
        /// Gets or sets the grace period for timer disposal
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:05")]
        public TimeSpan DisposalGracePeriod { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets whether to enable execution overlap prevention
        /// </summary>
        [DefaultValue(true)]
        public bool PreventExecutionOverlap { get; set; } = true;

        /// <summary>
        /// Gets or sets the target SolidWorks version for this timer
        /// </summary>
        [DefaultValue("")]
        public string TargetVersion
        {
            get => _targetVersion;
            set
            {
                _targetVersion = value ?? string.Empty;
                if (!string.IsNullOrEmpty(_targetVersion))
                {
                    ValidateVersionFormat(_targetVersion);
                }
            }
        }

        /// <summary>
        /// Gets or sets the list of supported SolidWorks versions
        /// </summary>
        public List<string> SupportedVersions
        {
            get => _supportedVersions;
            set
            {
                _supportedVersions = value ?? new List<string>();
                foreach (var version in _supportedVersions)
                {
                    ValidateVersionFormat(version);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to enable version-specific configuration
        /// </summary>
        [DefaultValue(false)]
        public bool EnableVersionSpecificConfig
        {
            get => _enableVersionSpecificConfig;
            set => _enableVersionSpecificConfig = value;
        }

        /// <summary>
        /// Gets or sets the interval for version detection and validation
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:30:00")]
        public TimeSpan VersionDetectionInterval
        {
            get => _versionDetectionInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Version detection interval must be greater than zero", nameof(value));
                }
                _versionDetectionInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to enable version fallback when target version is unavailable
        /// </summary>
        [DefaultValue(true)]
        public bool EnableVersionFallback
        {
            get => _enableVersionFallback;
            set => _enableVersionFallback = value;
        }

        /// <summary>
        /// Gets or sets the default version to use when target version is unavailable
        /// </summary>
        [DefaultValue("")]
        public string DefaultVersion
        {
            get => _defaultVersion;
            set
            {
                _defaultVersion = value ?? string.Empty;
                if (!string.IsNullOrEmpty(_defaultVersion))
                {
                    ValidateVersionFormat(_defaultVersion);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionOptions class
        /// </summary>
        public TimerExecutionOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionOptions class with default interval
        /// </summary>
        /// <param name="defaultInterval">The default timer interval</param>
        public TimerExecutionOptions(TimeSpan defaultInterval) : this()
        {
            DefaultInterval = defaultInterval;
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionOptions class with specified parameters
        /// </summary>
        /// <param name="defaultInterval">The default timer interval</param>
        /// <param name="maxConsecutiveErrors">The maximum consecutive errors</param>
        /// <param name="circuitBreakerCooldown">The circuit breaker cooldown period</param>
        public TimerExecutionOptions(TimeSpan defaultInterval, int maxConsecutiveErrors, TimeSpan circuitBreakerCooldown)
            : this(defaultInterval)
        {
            MaxConsecutiveErrors = maxConsecutiveErrors;
            CircuitBreakerCooldown = circuitBreakerCooldown;
        }

        /// <summary>
        /// Validates the version format
        /// </summary>
        /// <param name="version">Version to validate</param>
        private void ValidateVersionFormat(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return;

            // Validate version format (YYYY pattern for years)
            if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^20[0-9]{2}$"))
            {
                throw new ArgumentException($"Invalid version format: {version}. Version must be in YYYY format (e.g., 2023, 2024)", nameof(version));
            }

            // Validate version is within supported range
            if (int.TryParse(version, out int year))
            {
                if (year < 2020 || year > 2030)
                {
                    throw new ArgumentException($"Version {version} is outside the supported range (2020-2030)", nameof(version));
                }
            }
        }

        /// <summary>
        /// Gets the list of default supported SolidWorks versions
        /// </summary>
        /// <returns>List of supported versions</returns>
        public static List<string> GetDefaultSupportedVersions()
        {
            return new List<string> { "2020", "2021", "2022", "2023", "2024", "2025" };
        }

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        /// <returns>Copy of the options</returns>
        public TimerExecutionOptions Clone()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = DefaultInterval,
                MaxConsecutiveErrors = MaxConsecutiveErrors,
                MaxConcurrentExecutions = MaxConcurrentExecutions,
                CircuitBreakerCooldown = CircuitBreakerCooldown,
                EnableAutoRestart = EnableAutoRestart,
                EnableExecutionTimeout = EnableExecutionTimeout,
                ExecutionTimeout = ExecutionTimeout,
                EnableMetrics = EnableMetrics,
                EnableDetailedLogging = EnableDetailedLogging,
                EnableCircuitBreaker = EnableCircuitBreaker,
                MaxExecutionHistory = MaxExecutionHistory,
                MinInterval = MinInterval,
                MaxInterval = MaxInterval,
                SyncTimeout = SyncTimeout,
                StopOnUnhandledException = StopOnUnhandledException,
                DisposalGracePeriod = DisposalGracePeriod,
                PreventExecutionOverlap = PreventExecutionOverlap,
                TargetVersion = TargetVersion,
                SupportedVersions = new List<string>(SupportedVersions),
                EnableVersionSpecificConfig = EnableVersionSpecificConfig,
                VersionDetectionInterval = VersionDetectionInterval,
                EnableVersionFallback = EnableVersionFallback,
                DefaultVersion = DefaultVersion
            };
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public System.Collections.Generic.List<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (DefaultInterval <= TimeSpan.Zero)
            {
                errors.Add("Default interval must be greater than zero");
            }

            if (ExecutionTimeout <= TimeSpan.Zero)
            {
                errors.Add("Execution timeout must be greater than zero");
            }

            if (CircuitBreakerCooldown <= TimeSpan.Zero)
            {
                errors.Add("Circuit breaker cooldown must be greater than zero");
            }

            if (MaxConsecutiveErrors < 1)
            {
                errors.Add("Max consecutive errors must be at least 1");
            }

            if (MaxConcurrentExecutions < 1)
            {
                errors.Add("Max concurrent executions must be at least 1");
            }

            if (MaxExecutionHistory < 0)
            {
                errors.Add("Max execution history cannot be negative");
            }

            if (MinInterval <= TimeSpan.Zero)
            {
                errors.Add("Min interval must be greater than zero");
            }

            if (MaxInterval <= TimeSpan.Zero)
            {
                errors.Add("Max interval must be greater than zero");
            }

            if (MinInterval > MaxInterval)
            {
                errors.Add("Min interval cannot be greater than max interval");
            }

            if (SyncTimeout <= TimeSpan.Zero)
            {
                errors.Add("Sync timeout must be greater than zero");
            }

            if (DisposalGracePeriod <= TimeSpan.Zero)
            {
                errors.Add("Disposal grace period must be greater than zero");
            }

            if (DefaultInterval < MinInterval)
            {
                errors.Add("Default interval cannot be less than min interval");
            }

            if (DefaultInterval > MaxInterval)
            {
                errors.Add("Default interval cannot be greater than max interval");
            }

            // Version-specific validation
            if (EnableVersionSpecificConfig)
            {
                if (string.IsNullOrEmpty(TargetVersion) && string.IsNullOrEmpty(DefaultVersion))
                {
                    errors.Add("Either TargetVersion or DefaultVersion must be specified when version-specific configuration is enabled");
                }

                if (!string.IsNullOrEmpty(TargetVersion) && SupportedVersions.Count > 0 && !SupportedVersions.Contains(TargetVersion))
                {
                    errors.Add($"Target version {TargetVersion} is not in the list of supported versions");
                }

                if (!string.IsNullOrEmpty(DefaultVersion) && SupportedVersions.Count > 0 && !SupportedVersions.Contains(DefaultVersion))
                {
                    errors.Add($"Default version {DefaultVersion} is not in the list of supported versions");
                }

                if (VersionDetectionInterval < TimeSpan.FromMinutes(1))
                {
                    errors.Add("Version detection interval must be at least 1 minute");
                }

                if (VersionDetectionInterval > TimeSpan.FromHours(24))
                {
                    errors.Add("Version detection interval should not exceed 24 hours");
                }
            }

            // Validate version formats
            if (!string.IsNullOrEmpty(TargetVersion))
            {
                try
                {
                    ValidateVersionFormat(TargetVersion);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"Invalid target version: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(DefaultVersion))
            {
                try
                {
                    ValidateVersionFormat(DefaultVersion);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"Invalid default version: {ex.Message}");
                }
            }

            // Validate supported versions
            foreach (var version in SupportedVersions)
            {
                try
                {
                    ValidateVersionFormat(version);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"Invalid supported version '{version}': {ex.Message}");
                }
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the timer execution options
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Execution Options:");
            builder.AppendLine($"  Default Interval: {DefaultInterval.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Max Consecutive Errors: {MaxConsecutiveErrors}");
            builder.AppendLine($"  Max Concurrent Executions: {MaxConcurrentExecutions}");
            builder.AppendLine($"  Circuit Breaker Cooldown: {CircuitBreakerCooldown.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Enable Auto Restart: {EnableAutoRestart}");
            builder.AppendLine($"  Enable Execution Timeout: {EnableExecutionTimeout}");
            builder.AppendLine($"  Execution Timeout: {ExecutionTimeout.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Enable Metrics: {EnableMetrics}");
            builder.AppendLine($"  Enable Detailed Logging: {EnableDetailedLogging}");
            builder.AppendLine($"  Enable Circuit Breaker: {EnableCircuitBreaker}");
            builder.AppendLine($"  Max Execution History: {MaxExecutionHistory}");
            builder.AppendLine($"  Min Interval: {MinInterval.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Max Interval: {MaxInterval.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Sync Timeout: {SyncTimeout.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Stop On Unhandled Exception: {StopOnUnhandledException}");
            builder.AppendLine($"  Disposal Grace Period: {DisposalGracePeriod.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Prevent Execution Overlap: {PreventExecutionOverlap}");

            // Version-specific configuration
            builder.AppendLine($"  Enable Version Specific Config: {EnableVersionSpecificConfig}");
            builder.AppendLine($"  Target Version: {TargetVersion ?? "(none)"}");
            builder.AppendLine($"  Default Version: {DefaultVersion ?? "(none)"}");
            builder.AppendLine($"  Supported Versions: {string.Join(", ", SupportedVersions)}");
            builder.AppendLine($"  Version Detection Interval: {VersionDetectionInterval.TotalMinutes:F1}m");
            builder.AppendLine($"  Enable Version Fallback: {EnableVersionFallback}");

            return builder.ToString();
        }

        /// <summary>
        /// Gets default options for license monitoring operations
        /// </summary>
        /// <returns>Default options for license monitoring</returns>
        public static TimerExecutionOptions DefaultLicenseMonitoringOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMinutes(5),
                MaxConsecutiveErrors = 3,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(10),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(2),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 50,
                MinInterval = TimeSpan.FromSeconds(30),
                MaxInterval = TimeSpan.FromHours(1),
                PreventExecutionOverlap = true
            };
        }

        /// <summary>
        /// Gets options for high-frequency monitoring operations
        /// </summary>
        /// <returns>Options for high-frequency monitoring</returns>
        public static TimerExecutionOptions HighFrequencyOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(30),
                MaxConsecutiveErrors = 5,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(2),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromSeconds(15),
                EnableMetrics = true,
                EnableDetailedLogging = false,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 200,
                MinInterval = TimeSpan.FromSeconds(5),
                MaxInterval = TimeSpan.FromMinutes(10),
                PreventExecutionOverlap = true
            };
        }

        /// <summary>
        /// Gets options for low-frequency background operations
        /// </summary>
        /// <returns>Options for low-frequency operations</returns>
        public static TimerExecutionOptions LowFrequencyOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromHours(1),
                MaxConsecutiveErrors = 2,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromHours(1),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(10),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 25,
                MinInterval = TimeSpan.FromMinutes(5),
                MaxInterval = TimeSpan.FromDays(7),
                PreventExecutionOverlap = false
            };
        }

        /// <summary>
        /// Gets options for testing scenarios
        /// </summary>
        /// <returns>Options for testing</returns>
        public static TimerExecutionOptions TestOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(1),
                MaxConsecutiveErrors = 2,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromSeconds(5),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromSeconds(5),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 10,
                MinInterval = TimeSpan.FromMilliseconds(100),
                MaxInterval = TimeSpan.FromSeconds(10),
                StopOnUnhandledException = true,
                PreventExecutionOverlap = true
            };
        }

        /// <summary>
        /// Gets version-specific options for SolidWorks 2025
        /// </summary>
        /// <returns>Options optimized for SolidWorks 2025</returns>
        public static TimerExecutionOptions SolidWorks2025Options()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMinutes(3),
                MaxConsecutiveErrors = 3,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(8),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(4),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 75,
                MinInterval = TimeSpan.FromSeconds(15),
                MaxInterval = TimeSpan.FromMinutes(30),
                PreventExecutionOverlap = true,
                TargetVersion = "2025",
                SupportedVersions = GetDefaultSupportedVersions(),
                EnableVersionSpecificConfig = true,
                VersionDetectionInterval = TimeSpan.FromMinutes(15),
                EnableVersionFallback = true,
                DefaultVersion = "2024"
            };
        }

        /// <summary>
        /// Gets version-specific options for SolidWorks 2024
        /// </summary>
        /// <returns>Options optimized for SolidWorks 2024</returns>
        public static TimerExecutionOptions SolidWorks2024Options()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMinutes(4),
                MaxConsecutiveErrors = 4,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(10),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(5),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 60,
                MinInterval = TimeSpan.FromSeconds(30),
                MaxInterval = TimeSpan.FromMinutes(45),
                PreventExecutionOverlap = true,
                TargetVersion = "2024",
                SupportedVersions = GetDefaultSupportedVersions(),
                EnableVersionSpecificConfig = true,
                VersionDetectionInterval = TimeSpan.FromMinutes(20),
                EnableVersionFallback = true,
                DefaultVersion = "2023"
            };
        }

        /// <summary>
        /// Gets options for multi-version environments
        /// </summary>
        /// <returns>Options optimized for multi-version support</returns>
        public static TimerExecutionOptions MultiVersionOptions()
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMinutes(5),
                MaxConsecutiveErrors = 5,
                MaxConcurrentExecutions = 2,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(15),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(10),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 150,
                MinInterval = TimeSpan.FromMinutes(1),
                MaxInterval = TimeSpan.FromHours(2),
                PreventExecutionOverlap = false,
                SupportedVersions = GetDefaultSupportedVersions(),
                EnableVersionSpecificConfig = true,
                VersionDetectionInterval = TimeSpan.FromMinutes(10),
                EnableVersionFallback = true,
                DefaultVersion = "2024"
            };
        }
    }
}