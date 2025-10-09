using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Configuration element for timer execution settings
    /// </summary>
    public class TimerConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("defaultInterval", DefaultValue = "00:01:00")]
        public TimeSpan DefaultInterval
        {
            get { return (TimeSpan)this["defaultInterval"]; }
            set { this["defaultInterval"] = value; }
        }

        [ConfigurationProperty("maxConsecutiveErrors", DefaultValue = 5)]
        [IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int MaxConsecutiveErrors
        {
            get { return (int)this["maxConsecutiveErrors"]; }
            set { this["maxConsecutiveErrors"] = value; }
        }

        [ConfigurationProperty("maxConcurrentExecutions", DefaultValue = 1)]
        [IntegerValidator(MinValue = 1, MaxValue = 50)]
        public int MaxConcurrentExecutions
        {
            get { return (int)this["maxConcurrentExecutions"]; }
            set { this["maxConcurrentExecutions"] = value; }
        }

        [ConfigurationProperty("circuitBreakerCooldown", DefaultValue = "00:05:00")]
        public TimeSpan CircuitBreakerCooldown
        {
            get { return (TimeSpan)this["circuitBreakerCooldown"]; }
            set { this["circuitBreakerCooldown"] = value; }
        }

        [ConfigurationProperty("enableAutoRestart", DefaultValue = true)]
        public bool EnableAutoRestart
        {
            get { return (bool)this["enableAutoRestart"]; }
            set { this["enableAutoRestart"] = value; }
        }

        [ConfigurationProperty("enableExecutionTimeout", DefaultValue = true)]
        public bool EnableExecutionTimeout
        {
            get { return (bool)this["enableExecutionTimeout"]; }
            set { this["enableExecutionTimeout"] = value; }
        }

        [ConfigurationProperty("executionTimeout", DefaultValue = "00:05:00")]
        public TimeSpan ExecutionTimeout
        {
            get { return (TimeSpan)this["executionTimeout"]; }
            set { this["executionTimeout"] = value; }
        }

        [ConfigurationProperty("enableMetrics", DefaultValue = true)]
        public bool EnableMetrics
        {
            get { return (bool)this["enableMetrics"]; }
            set { this["enableMetrics"] = value; }
        }

        [ConfigurationProperty("enableDetailedLogging", DefaultValue = true)]
        public bool EnableDetailedLogging
        {
            get { return (bool)this["enableDetailedLogging"]; }
            set { this["enableDetailedLogging"] = value; }
        }

        [ConfigurationProperty("enableCircuitBreaker", DefaultValue = true)]
        public bool EnableCircuitBreaker
        {
            get { return (bool)this["enableCircuitBreaker"]; }
            set { this["enableCircuitBreaker"] = value; }
        }

        [ConfigurationProperty("maxExecutionHistory", DefaultValue = 100)]
        [IntegerValidator(MinValue = 0, MaxValue = 10000)]
        public int MaxExecutionHistory
        {
            get { return (int)this["maxExecutionHistory"]; }
            set { this["maxExecutionHistory"] = value; }
        }

        [ConfigurationProperty("minInterval", DefaultValue = "00:00:01")]
        public TimeSpan MinInterval
        {
            get { return (TimeSpan)this["minInterval"]; }
            set { this["minInterval"] = value; }
        }

        [ConfigurationProperty("maxInterval", DefaultValue = "1.00:00:00")]
        public TimeSpan MaxInterval
        {
            get { return (TimeSpan)this["maxInterval"]; }
            set { this["maxInterval"] = value; }
        }

        [ConfigurationProperty("syncTimeout", DefaultValue = "00:00:30")]
        public TimeSpan SyncTimeout
        {
            get { return (TimeSpan)this["syncTimeout"]; }
            set { this["syncTimeout"] = value; }
        }

        [ConfigurationProperty("stopOnUnhandledException", DefaultValue = false)]
        public bool StopOnUnhandledException
        {
            get { return (bool)this["stopOnUnhandledException"]; }
            set { this["stopOnUnhandledException"] = value; }
        }

        [ConfigurationProperty("disposalGracePeriod", DefaultValue = "00:00:05")]
        public TimeSpan DisposalGracePeriod
        {
            get { return (TimeSpan)this["disposalGracePeriod"]; }
            set { this["disposalGracePeriod"] = value; }
        }

        [ConfigurationProperty("preventExecutionOverlap", DefaultValue = true)]
        public bool PreventExecutionOverlap
        {
            get { return (bool)this["preventExecutionOverlap"]; }
            set { this["preventExecutionOverlap"] = value; }
        }

        [ConfigurationProperty("startupDelay", DefaultValue = "00:00:10")]
        public TimeSpan StartupDelay
        {
            get { return (TimeSpan)this["startupDelay"]; }
            set { this["startupDelay"] = value; }
        }

        [ConfigurationProperty("shutdownTimeout", DefaultValue = "00:00:30")]
        public TimeSpan ShutdownTimeout
        {
            get { return (TimeSpan)this["shutdownTimeout"]; }
            set { this["shutdownTimeout"] = value; }
        }

        [ConfigurationProperty("healthCheckInterval", DefaultValue = 60)]
        [IntegerValidator(MinValue = 10, MaxValue = 3600)]
        public int HealthCheckInterval
        {
            get { return (int)this["healthCheckInterval"]; }
            set { this["healthCheckInterval"] = value; }
        }

        [ConfigurationProperty("metricsCollectionInterval", DefaultValue = 30)]
        [IntegerValidator(MinValue = 5, MaxValue = 600)]
        public int MetricsCollectionInterval
        {
            get { return (int)this["metricsCollectionInterval"]; }
            set { this["metricsCollectionInterval"] = value; }
        }

        [ConfigurationProperty("enableAdaptiveScheduling", DefaultValue = false)]
        public bool EnableAdaptiveScheduling
        {
            get { return (bool)this["enableAdaptiveScheduling"]; }
            set { this["enableAdaptiveScheduling"] = value; }
        }

        [ConfigurationProperty("adaptiveThreshold", DefaultValue = 80)]
        [IntegerValidator(MinValue = 50, MaxValue = 95)]
        public int AdaptiveThreshold
        {
            get { return (int)this["adaptiveThreshold"]; }
            set { this["adaptiveThreshold"] = value; }
        }

        [ConfigurationProperty("adaptiveCooldown", DefaultValue = "00:05:00")]
        public TimeSpan AdaptiveCooldown
        {
            get { return (TimeSpan)this["adaptiveCooldown"]; }
            set { this["adaptiveCooldown"] = value; }
        }

        [ConfigurationProperty("enableMemoryMonitoring", DefaultValue = true)]
        public bool EnableMemoryMonitoring
        {
            get { return (bool)this["enableMemoryMonitoring"]; }
            set { this["enableMemoryMonitoring"] = value; }
        }

        [ConfigurationProperty("memoryThreshold", DefaultValue = 52428800)]
        [LongValidator(MinValue = 1048576, MaxValue = 1073741824)]
        public long MemoryThreshold
        {
            get { return (long)this["memoryThreshold"]; }
            set { this["memoryThreshold"] = value; }
        }

        [ConfigurationProperty("enableCpuMonitoring", DefaultValue = true)]
        public bool EnableCpuMonitoring
        {
            get { return (bool)this["enableCpuMonitoring"]; }
            set { this["enableCpuMonitoring"] = value; }
        }

        [ConfigurationProperty("cpuThreshold", DefaultValue = 80)]
        [IntegerValidator(MinValue = 50, MaxValue = 100)]
        public int CpuThreshold
        {
            get { return (int)this["cpuThreshold"]; }
            set { this["cpuThreshold"] = value; }
        }

        [ConfigurationProperty("enableThreadMonitoring", DefaultValue = true)]
        public bool EnableThreadMonitoring
        {
            get { return (bool)this["enableThreadMonitoring"]; }
            set { this["enableThreadMonitoring"] = value; }
        }

        [ConfigurationProperty("maxThreadPoolThreads", DefaultValue = 10)]
        [IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int MaxThreadPoolThreads
        {
            get { return (int)this["maxThreadPoolThreads"]; }
            set { this["maxThreadPoolThreads"] = value; }
        }

        [ConfigurationProperty("targetVersion", DefaultValue = "")]
        public string TargetVersion
        {
            get { return (string)this["targetVersion"]; }
            set { this["targetVersion"] = value; }
        }

        [ConfigurationProperty("defaultVersion", DefaultValue = "")]
        public string DefaultVersion
        {
            get { return (string)this["defaultVersion"]; }
            set { this["defaultVersion"] = value; }
        }

        [ConfigurationProperty("enableVersionSpecificConfig", DefaultValue = false)]
        public bool EnableVersionSpecificConfig
        {
            get { return (bool)this["enableVersionSpecificConfig"]; }
            set { this["enableVersionSpecificConfig"] = value; }
        }

        [ConfigurationProperty("versionDetectionInterval", DefaultValue = "00:30:00")]
        public TimeSpan VersionDetectionInterval
        {
            get { return (TimeSpan)this["versionDetectionInterval"]; }
            set { this["versionDetectionInterval"] = value; }
        }

        [ConfigurationProperty("enableVersionFallback", DefaultValue = true)]
        public bool EnableVersionFallback
        {
            get { return (bool)this["enableVersionFallback"]; }
            set { this["enableVersionFallback"] = value; }
        }

        [ConfigurationProperty("supportedVersions", DefaultValue = "")]
        public string SupportedVersions
        {
            get { return (string)this["supportedVersions"]; }
            set { this["supportedVersions"] = value; }
        }

        // Additional properties for backward compatibility with TimerConfigurationProvider
        [ConfigurationProperty("enabled", DefaultValue = true)]
        public bool Enabled
        {
            get { return (bool)this["enabled"]; }
            set { this["enabled"] = value; }
        }

        [ConfigurationProperty("interval", DefaultValue = "00:01:00")]
        public TimeSpan Interval
        {
            get { return (TimeSpan)this["interval"]; }
            set { this["interval"] = value; }
        }

        [ConfigurationProperty("timeout", DefaultValue = "00:05:00")]
        public TimeSpan Timeout
        {
            get { return (TimeSpan)this["timeout"]; }
            set { this["timeout"] = value; }
        }

        [ConfigurationProperty("maxRetries", DefaultValue = 3)]
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        public int MaxRetries
        {
            get { return (int)this["maxRetries"]; }
            set { this["maxRetries"] = value; }
        }

        [ConfigurationProperty("licenseServer", DefaultValue = "localhost")]
        public string LicenseServer
        {
            get { return (string)this["licenseServer"]; }
            set { this["licenseServer"] = value; }
        }

        [ConfigurationProperty("port", DefaultValue = 27000)]
        [IntegerValidator(MinValue = 1, MaxValue = 65535)]
        public int Port
        {
            get { return (int)this["port"]; }
            set { this["port"] = value; }
        }

        [ConfigurationProperty("lmutilPath", DefaultValue = @"C:\flexlm\lmutil.exe")]
        public string LmutilPath
        {
            get { return (string)this["lmutilPath"]; }
            set { this["lmutilPath"] = value; }
        }

        [ConfigurationProperty("enableHealthMonitoring", DefaultValue = true)]
        public bool EnableHealthMonitoring
        {
            get { return (bool)this["enableHealthMonitoring"]; }
            set { this["enableHealthMonitoring"] = value; }
        }

        [ConfigurationProperty("enableLicenseRecovery", DefaultValue = true)]
        public bool EnableLicenseRecovery
        {
            get { return (bool)this["enableLicenseRecovery"]; }
            set { this["enableLicenseRecovery"] = value; }
        }

        [ConfigurationProperty("recoveryCheckInterval", DefaultValue = 300)]
        [IntegerValidator(MinValue = 60, MaxValue = 3600)]
        public int RecoveryCheckInterval
        {
            get { return (int)this["recoveryCheckInterval"]; }
            set { this["recoveryCheckInterval"] = value; }
        }

        /// <summary>
        /// Validates the timer configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate interval relationships
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

                if (MinInterval <= TimeSpan.Zero)
                {
                    errors.Add("Min interval must be greater than zero");
                }

                if (MaxInterval <= TimeSpan.Zero)
                {
                    errors.Add("Max interval must be greater than zero");
                }

                if (SyncTimeout <= TimeSpan.Zero)
                {
                    errors.Add("Sync timeout must be greater than zero");
                }

                if (DisposalGracePeriod <= TimeSpan.Zero)
                {
                    errors.Add("Disposal grace period must be greater than zero");
                }

                if (StartupDelay < TimeSpan.Zero)
                {
                    errors.Add("Startup delay cannot be negative");
                }

                if (ShutdownTimeout <= TimeSpan.Zero)
                {
                    errors.Add("Shutdown timeout must be greater than zero");
                }

                if (AdaptiveCooldown <= TimeSpan.Zero)
                {
                    errors.Add("Adaptive cooldown must be greater than zero");
                }

                // Validate interval boundaries
                if (MinInterval > MaxInterval)
                {
                    errors.Add("Min interval cannot be greater than max interval");
                }

                if (DefaultInterval < MinInterval)
                {
                    errors.Add("Default interval cannot be less than min interval");
                }

                if (DefaultInterval > MaxInterval)
                {
                    errors.Add("Default interval cannot be greater than max interval");
                }

                // Validate numeric constraints
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

                if (HealthCheckInterval < 10)
                {
                    errors.Add("Health check interval must be at least 10 seconds");
                }

                if (MetricsCollectionInterval < 5)
                {
                    errors.Add("Metrics collection interval must be at least 5 seconds");
                }

                if (AdaptiveThreshold < 50 || AdaptiveThreshold > 95)
                {
                    errors.Add("Adaptive threshold must be between 50 and 95");
                }

                if (MemoryThreshold < 1048576) // 1MB
                {
                    errors.Add("Memory threshold must be at least 1MB");
                }

                if (CpuThreshold < 50 || CpuThreshold > 100)
                {
                    errors.Add("CPU threshold must be between 50 and 100");
                }

                if (MaxThreadPoolThreads < 1)
                {
                    errors.Add("Max thread pool threads must be at least 1");
                }

                // Validate interval relationships for monitoring
                if (MetricsCollectionInterval > HealthCheckInterval)
                {
                    errors.Add("Metrics collection interval should be less than or equal to health check interval");
                }

                // Validate timeout relationships
                if (ExecutionTimeout < DefaultInterval)
                {
                    errors.Add("Execution timeout should be greater than or equal to default interval");
                }

                // Validate adaptive scheduling logic
                if (EnableAdaptiveScheduling && !EnableMetrics)
                {
                    errors.Add("Metrics must be enabled when adaptive scheduling is enabled");
                }

                // Validate resource monitoring dependencies
                if (EnableMemoryMonitoring && !EnableMetrics)
                {
                    errors.Add("Metrics must be enabled when memory monitoring is enabled");
                }

                if (EnableCpuMonitoring && !EnableMetrics)
                {
                    errors.Add("Metrics must be enabled when CPU monitoring is enabled");
                }

                if (EnableThreadMonitoring && !EnableMetrics)
                {
                    errors.Add("Metrics must be enabled when thread monitoring is enabled");
                }

                // Validate circuit breaker dependencies
                if (EnableCircuitBreaker && MaxConsecutiveErrors < 1)
                {
                    errors.Add("Max consecutive errors must be at least 1 when circuit breaker is enabled");
                }

                // Validate execution overlap prevention
                if (PreventExecutionOverlap && MaxConcurrentExecutions > 1)
                {
                    errors.Add("Max concurrent executions must be 1 when execution overlap prevention is enabled");
                }

                // Validate startup and shutdown timeouts
                if (StartupDelay > TimeSpan.FromMinutes(5))
                {
                    errors.Add("Startup delay should not exceed 5 minutes");
                }

                if (ShutdownTimeout > TimeSpan.FromMinutes(10))
                {
                    errors.Add("Shutdown timeout should not exceed 10 minutes");
                }

                // Validate memory limits
                if (MemoryThreshold > 1073741824) // 1GB
                {
                    errors.Add("Memory threshold exceeds recommended limit of 1GB");
                }

                // Version-specific validation
                if (EnableVersionSpecificConfig)
                {
                    if (string.IsNullOrEmpty(TargetVersion) && string.IsNullOrEmpty(DefaultVersion))
                    {
                        errors.Add("Either targetVersion or defaultVersion must be specified when version-specific configuration is enabled");
                    }

                    if (!string.IsNullOrEmpty(TargetVersion))
                    {
                        ValidateVersionFormat(TargetVersion, errors, "targetVersion");
                    }

                    if (!string.IsNullOrEmpty(DefaultVersion))
                    {
                        ValidateVersionFormat(DefaultVersion, errors, "defaultVersion");
                    }

                    // Parse and validate supported versions
                    if (!string.IsNullOrEmpty(SupportedVersions))
                    {
                        var versionList = ParseSupportedVersions();
                        foreach (var version in versionList)
                        {
                            ValidateVersionFormat(version, errors, "supportedVersions");
                        }

                        // Validate target and default versions are in supported list
                        if (!string.IsNullOrEmpty(TargetVersion) && versionList.Count > 0 && !versionList.Contains(TargetVersion))
                        {
                            errors.Add($"Target version '{TargetVersion}' is not in the list of supported versions");
                        }

                        if (!string.IsNullOrEmpty(DefaultVersion) && versionList.Count > 0 && !versionList.Contains(DefaultVersion))
                        {
                            errors.Add($"Default version '{DefaultVersion}' is not in the list of supported versions");
                        }
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
                else
                {
                    // Validate that version-specific settings are not used when disabled
                    if (!string.IsNullOrEmpty(TargetVersion))
                    {
                        errors.Add("Target version should not be specified when version-specific configuration is disabled");
                    }

                    if (!string.IsNullOrEmpty(DefaultVersion))
                    {
                        errors.Add("Default version should not be specified when version-specific configuration is disabled");
                    }

                    if (!string.IsNullOrEmpty(SupportedVersions))
                    {
                        errors.Add("Supported versions should not be specified when version-specific configuration is disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Timer configuration validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the timer configuration
        /// </summary>
        public override string ToString()
        {
            return $"TimerConfiguration[DefaultInterval={DefaultInterval.TotalMilliseconds:F2}ms, MaxConsecutiveErrors={MaxConsecutiveErrors}, MaxConcurrentExecutions={MaxConcurrentExecutions}, CircuitBreakerCooldown={CircuitBreakerCooldown.TotalMilliseconds:F2}ms, EnableAutoRestart={EnableAutoRestart}, EnableExecutionTimeout={EnableExecutionTimeout}, ExecutionTimeout={ExecutionTimeout.TotalMilliseconds:F2}ms, EnableMetrics={EnableMetrics}, EnableDetailedLogging={EnableDetailedLogging}, EnableCircuitBreaker={EnableCircuitBreaker}]";
        }

        /// <summary>
        /// Validates the version format
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="errors">Error list to add validation errors to</param>
        /// <param name="propertyName">Name of the property being validated</param>
        private void ValidateVersionFormat(string version, List<string> errors, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(version))
                return;

            // Validate version format (YYYY pattern for years)
            if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^20[0-9]{2}$"))
            {
                errors.Add($"Invalid {propertyName} format: {version}. Version must be in YYYY format (e.g., 2023, 2024)");
                return;
            }

            // Validate version is within supported range
            if (int.TryParse(version, out int year))
            {
                if (year < 2020 || year > 2030)
                {
                    errors.Add($"{propertyName} {version} is outside the supported range (2020-2030)");
                }
            }
            else
            {
                errors.Add($"Invalid {propertyName}: {version} is not a valid year");
            }
        }

        /// <summary>
        /// Parses the supported versions string into a list
        /// </summary>
        /// <returns>List of supported versions</returns>
        private List<string> ParseSupportedVersions()
        {
            if (string.IsNullOrWhiteSpace(SupportedVersions))
                return new List<string>();

            return SupportedVersions.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Converts the configuration to a TimerExecutionOptions object
        /// </summary>
        /// <returns>TimerExecutionOptions object</returns>
        public TimerExecution.TimerExecutionOptions ToTimerExecutionOptions()
        {
            var options = new TimerExecution.TimerExecutionOptions
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
                DefaultVersion = DefaultVersion,
                EnableVersionSpecificConfig = EnableVersionSpecificConfig,
                VersionDetectionInterval = VersionDetectionInterval,
                EnableVersionFallback = EnableVersionFallback,
                SupportedVersions = ParseSupportedVersions()
            };

            return options;
        }
    }
}