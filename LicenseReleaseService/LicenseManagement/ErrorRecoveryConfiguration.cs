using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Configuration for graceful degradation settings
    /// </summary>
    public class GracefulDegradationConfiguration
    {
        /// <summary>
        /// Gets or sets whether graceful degradation is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the auto-recovery threshold in minutes
        /// </summary>
        public int AutoRecoveryThresholdMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the default timeout multiplier
        /// </summary>
        public double DefaultTimeoutMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the monitoring interval
        /// </summary>
        public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the degradation rules configuration
        /// </summary>
        public Dictionary<string, DegradationRuleConfiguration> DegradationRules { get; set; } = new();
    }

    /// <summary>
    /// Configuration for a specific degradation rule
    /// </summary>
    public class DegradationRuleConfiguration
    {
        /// <summary>
        /// Gets or sets the operation name
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// Gets or sets the operation priority
        /// </summary>
        public OperationPriority Priority { get; set; } = OperationPriority.Normal;

        /// <summary>
        /// Gets or sets the minimum degradation level
        /// </summary>
        public DegradationLevel MinimumLevel { get; set; } = DegradationLevel.Full;

        /// <summary>
        /// Gets or sets the maximum degradation level
        /// </summary>
        public DegradationLevel MaximumLevel { get; set; } = DegradationLevel.Emergency;

        /// <summary>
        /// Gets or sets the timeout multiplier
        /// </summary>
        public double TimeoutMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets whether to skip during degradation
        /// </summary>
        public bool SkipDuringDegradation { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to use fallback
        /// </summary>
        public bool UseFallback { get; set; } = false;
    }

    /// <summary>
    /// Configuration for fallback strategies
    /// </summary>
    public class FallbackStrategyConfiguration
    {
        /// <summary>
        /// Gets or sets whether fallback strategies are enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the default strategy timeout
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the maximum number of retries
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the default retry delay
        /// </summary>
        public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the strategy configurations
        /// </summary>
        public Dictionary<string, FallbackStrategyItemConfiguration> Strategies { get; set; } = new();
    }

    /// <summary>
    /// Configuration for a specific fallback strategy
    /// </summary>
    public class FallbackStrategyItemConfiguration
    {
        /// <summary>
        /// Gets or sets the strategy type
        /// </summary>
        public FallbackStrategyType Type { get; set; }

        /// <summary>
        /// Gets or sets the strategy priority
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Gets or sets the execution order
        /// </summary>
        public FallbackExecutionOrder ExecutionOrder { get; set; } = FallbackExecutionOrder.Sequential;

        /// <summary>
        /// Gets or sets the timeout
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the maximum retries
        /// </summary>
        public int MaxRetries { get; set; } = 1;

        /// <summary>
        /// Gets or sets the retry delay
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets or sets the strategy-specific configuration
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Configuration for circuit breaker settings
    /// </summary>
    public class CircuitBreakerConfiguration
    {
        /// <summary>
        /// Gets or sets whether circuit breaker is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the failure threshold
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets the recovery timeout
        /// </summary>
        public TimeSpan RecoveryTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the operation timeout
        /// </summary>
        public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the half-open success threshold
        /// </summary>
        public TimeSpan HalfOpenSuccessThreshold { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the sliding window duration
        /// </summary>
        public TimeSpan SlidingWindow { get; set; } = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Configuration for retry policies
    /// </summary>
    public class RetryPolicyConfiguration
    {
        /// <summary>
        /// Gets or sets whether retry policies are enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retries
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the initial delay
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum delay
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the retry strategy type
        /// </summary>
        public RetryStrategyType Strategy { get; set; } = RetryStrategyType.Exponential;

        /// <summary>
        /// Gets or sets the backoff multiplier
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the jitter factor
        /// </summary>
        public double JitterFactor { get; set; } = 0.1;

        /// <summary>
        /// Gets or sets whether to retry on timeout
        /// </summary>
        public bool RetryOnTimeout { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to retry on transient errors
        /// </summary>
        public bool RetryOnTransientErrors { get; set; } = true;
    }

    /// <summary>
    /// Configuration for health monitoring
    /// </summary>
    public class HealthMonitoringConfiguration
    {
        /// <summary>
        /// Gets or sets whether health monitoring is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the monitoring interval
        /// </summary>
        public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the health check configurations
        /// </summary>
        public Dictionary<string, HealthCheckConfiguration> HealthChecks { get; set; } = new();
    }

    /// <summary>
    /// Main error recovery configuration
    /// </summary>
    public class ErrorRecoverySettings
    {
        /// <summary>
        /// Gets or sets whether error recovery is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the graceful degradation configuration
        /// </summary>
        public GracefulDegradationConfiguration GracefulDegradation { get; set; } = new();

        /// <summary>
        /// Gets or sets the fallback strategy configuration
        /// </summary>
        public FallbackStrategyConfiguration FallbackStrategies { get; set; } = new();

        /// <summary>
        /// Gets or sets the circuit breaker configuration
        /// </summary>
        public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new();

        /// <summary>
        /// Gets or sets the retry policy configuration
        /// </summary>
        public RetryPolicyConfiguration RetryPolicies { get; set; } = new();

        /// <summary>
        /// Gets or sets the health monitoring configuration
        /// </summary>
        public HealthMonitoringConfiguration HealthMonitoring { get; set; } = new();

        /// <summary>
        /// Gets or sets the recovery engine configuration
        /// </summary>
        public RecoveryEngineConfiguration RecoveryEngine { get; set; } = new();

        /// <summary>
        /// Gets or sets the logging configuration
        /// </summary>
        public ErrorRecoveryLoggingConfiguration Logging { get; set; } = new();
    }

    /// <summary>
    /// Configuration for error recovery logging
    /// </summary>
    public class ErrorRecoveryLoggingConfiguration
    {
        /// <summary>
        /// Gets or sets whether to log recovery attempts
        /// </summary>
        public bool LogRecoveryAttempts { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to log successful recoveries
        /// </summary>
        public bool LogSuccessfulRecoveries { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to log failed recoveries
        /// </summary>
        public bool LogFailedRecoveries { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to log degradation events
        /// </summary>
        public bool LogDegradationEvents { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to log circuit breaker events
        /// </summary>
        public bool LogCircuitBreakerEvents { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to log fallback attempts
        /// </summary>
        public bool LogFallbackAttempts { get; set; } = true;

        /// <summary>
        /// Gets or sets the log retention period
        /// </summary>
        public TimeSpan LogRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    }

    /// <summary>
    /// Manages error recovery configuration
    /// </summary>
    public class ErrorRecoveryConfigurationManager
    {
        private readonly IConfiguration _configuration;
        private readonly string _configurationPath;
        private ErrorRecoverySettings _settings;

        /// <summary>
        /// Event raised when configuration is reloaded
        /// </summary>
        public event EventHandler<ErrorRecoverySettings> ConfigurationReloaded;

        /// <summary>
        /// Gets the current settings
        /// </summary>
        public ErrorRecoverySettings Settings => _settings;

        /// <summary>
        /// Initializes a new instance of the ErrorRecoveryConfigurationManager class
        /// </summary>
        /// <param name="configuration">The configuration root</param>
        /// <param name="configurationPath">The path to the error recovery configuration section</param>
        public ErrorRecoveryConfigurationManager(IConfiguration configuration, string configurationPath = "ErrorRecovery")
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _configurationPath = configurationPath;
            _settings = LoadConfiguration();
        }

        /// <summary>
        /// Reloads the configuration
        /// </summary>
        public void ReloadConfiguration()
        {
            _settings = LoadConfiguration();
            OnConfigurationReloaded(_settings);
        }

        /// <summary>
        /// Saves the current configuration to file
        /// </summary>
        /// <param name="filePath">The file path to save to</param>
        public void SaveConfiguration(string filePath)
        {
            // This would serialize the current settings to a file
            // For now, we'll just log that we would save
            Console.WriteLine($"Would save configuration to {filePath}");
        }

        /// <summary>
        /// Gets a configuration value
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="key">The configuration key</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The configuration value</returns>
        public T GetValue<T>(string key, T defaultValue = default)
        {
            return _configuration.GetValue($"{_configurationPath}:{key}", defaultValue);
        }

        /// <summary>
        /// Gets a configuration section
        /// </summary>
        /// <param name="key">The section key</param>
        /// <returns>The configuration section</returns>
        public IConfigurationSection GetSection(string key)
        {
            return _configuration.GetSection($"{_configurationPath}:{key}");
        }

        /// <summary>
        /// Updates a configuration value
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="key">The configuration key</param>
        /// <param name="value">The new value</param>
        public void UpdateValue<T>(string key, T value)
        {
            // This would update the configuration in memory and potentially persist it
            // For now, we'll just update the in-memory settings
            var keyParts = key.Split(':');
            UpdateNestedValue(_settings, keyParts, 0, value);
        }

        private ErrorRecoverySettings LoadConfiguration()
        {
            var settings = new ErrorRecoverySettings();

            // Load main enabled flag
            settings.IsEnabled = GetValue("IsEnabled", true);

            // Load graceful degradation configuration
            settings.GracefulDegradation = new GracefulDegradationConfiguration
            {
                IsEnabled = GetValue("GracefulDegradation:IsEnabled", true),
                AutoRecoveryThresholdMinutes = GetValue("GracefulDegradation:AutoRecoveryThresholdMinutes", 30),
                DefaultTimeoutMultiplier = GetValue("GracefulDegradation:DefaultTimeoutMultiplier", 2.0),
                MonitoringInterval = GetValue("GracefulDegradation:MonitoringInterval", TimeSpan.FromMinutes(1))
            };

            // Load fallback strategy configuration
            settings.FallbackStrategies = new FallbackStrategyConfiguration
            {
                IsEnabled = GetValue("FallbackStrategies:IsEnabled", true),
                DefaultTimeout = GetValue("FallbackStrategies:DefaultTimeout", TimeSpan.FromSeconds(30)),
                MaxRetries = GetValue("FallbackStrategies:MaxRetries", 3),
                DefaultRetryDelay = GetValue("FallbackStrategies:DefaultRetryDelay", TimeSpan.FromSeconds(1))
            };

            // Load circuit breaker configuration
            settings.CircuitBreaker = new CircuitBreakerConfiguration
            {
                IsEnabled = GetValue("CircuitBreaker:IsEnabled", true),
                FailureThreshold = GetValue("CircuitBreaker:FailureThreshold", 5),
                RecoveryTimeout = GetValue("CircuitBreaker:RecoveryTimeout", TimeSpan.FromMinutes(1)),
                OperationTimeout = GetValue("CircuitBreaker:OperationTimeout", TimeSpan.FromSeconds(30)),
                HalfOpenSuccessThreshold = GetValue("CircuitBreaker:HalfOpenSuccessThreshold", TimeSpan.FromMinutes(2)),
                SlidingWindow = GetValue("CircuitBreaker:SlidingWindow", TimeSpan.FromMinutes(10))
            };

            // Load retry policy configuration
            settings.RetryPolicies = new RetryPolicyConfiguration
            {
                IsEnabled = GetValue("RetryPolicies:IsEnabled", true),
                MaxRetries = GetValue("RetryPolicies:MaxRetries", 3),
                InitialDelay = GetValue("RetryPolicies:InitialDelay", TimeSpan.FromSeconds(1)),
                MaxDelay = GetValue("RetryPolicies:MaxDelay", TimeSpan.FromSeconds(30)),
                Strategy = GetValue("RetryPolicies:Strategy", RetryStrategyType.Exponential),
                BackoffMultiplier = GetValue("RetryPolicies:BackoffMultiplier", 2.0),
                JitterFactor = GetValue("RetryPolicies:JitterFactor", 0.1),
                RetryOnTimeout = GetValue("RetryPolicies:RetryOnTimeout", true),
                RetryOnTransientErrors = GetValue("RetryPolicies:RetryOnTransientErrors", true)
            };

            // Load health monitoring configuration
            settings.HealthMonitoring = new HealthMonitoringConfiguration
            {
                IsEnabled = GetValue("HealthMonitoring:IsEnabled", true),
                MonitoringInterval = GetValue("HealthMonitoring:MonitoringInterval", TimeSpan.FromMinutes(1))
            };

            // Load recovery engine configuration
            settings.RecoveryEngine = new RecoveryEngineConfiguration
            {
                MaxConcurrentRecoveries = GetValue("RecoveryEngine:MaxConcurrentRecoveries", 3),
                MaxQueueSize = GetValue("RecoveryEngine:MaxQueueSize", 1000),
                ProcessingInterval = GetValue("RecoveryEngine:ProcessingInterval", TimeSpan.FromSeconds(1)),
                DefaultTimeout = GetValue("RecoveryEngine:DefaultTimeout", TimeSpan.FromMinutes(5)),
                MaxRecoveryAttempts = GetValue("RecoveryEngine:MaxRecoveryAttempts", 3),
                EnableAutomaticRecovery = GetValue("RecoveryEngine:EnableAutomaticRecovery", true),
                EnableManualRecovery = GetValue("RecoveryEngine:EnableManualRecovery", true),
                StrategySelectionMode = GetValue("RecoveryEngine:StrategySelectionMode", "Adaptive"),
                StatisticsRetentionPeriod = GetValue("RecoveryEngine:StatisticsRetentionPeriod", TimeSpan.FromDays(7))
            };

            // Load logging configuration
            settings.Logging = new ErrorRecoveryLoggingConfiguration
            {
                LogRecoveryAttempts = GetValue("Logging:LogRecoveryAttempts", true),
                LogSuccessfulRecoveries = GetValue("Logging:LogSuccessfulRecoveries", true),
                LogFailedRecoveries = GetValue("Logging:LogFailedRecoveries", true),
                LogDegradationEvents = GetValue("Logging:LogDegradationEvents", true),
                LogCircuitBreakerEvents = GetValue("Logging:LogCircuitBreakerEvents", true),
                LogFallbackAttempts = GetValue("Logging:LogFallbackAttempts", true),
                LogRetentionPeriod = GetValue("Logging:LogRetentionPeriod", TimeSpan.FromDays(7))
            };

            return settings;
        }

        private void UpdateNestedValue(object obj, string[] keyParts, int index, object value)
        {
            if (index >= keyParts.Length)
                return;

            var propertyName = keyParts[index];
            var property = obj.GetType().GetProperty(propertyName);

            if (property != null && property.CanWrite)
            {
                if (index == keyParts.Length - 1)
                {
                    // Last part of the key, set the value
                    var convertedValue = Convert.ChangeType(value, property.PropertyType);
                    property.SetValue(obj, convertedValue);
                }
                else
                {
                    // Navigate to nested object
                    var nestedObj = property.GetValue(obj);
                    if (nestedObj != null)
                    {
                        UpdateNestedValue(nestedObj, keyParts, index + 1, value);
                    }
                }
            }
        }

        private void OnConfigurationReloaded(ErrorRecoverySettings settings)
        {
            ConfigurationReloaded?.Invoke(this, settings);
        }

        /// <summary>
        /// Creates a default configuration file
        /// </summary>
        /// <param name="filePath">The file path to create</param>
        public static void CreateDefaultConfigurationFile(string filePath)
        {
            var defaultConfig = new ErrorRecoverySettings
            {
                IsEnabled = true,
                GracefulDegradation = new GracefulDegradationConfiguration
                {
                    IsEnabled = true,
                    AutoRecoveryThresholdMinutes = 30,
                    DefaultTimeoutMultiplier = 2.0,
                    MonitoringInterval = TimeSpan.FromMinutes(1)
                },
                FallbackStrategies = new FallbackStrategyConfiguration
                {
                    IsEnabled = true,
                    DefaultTimeout = TimeSpan.FromSeconds(30),
                    MaxRetries = 3,
                    DefaultRetryDelay = TimeSpan.FromSeconds(1)
                },
                CircuitBreaker = new CircuitBreakerConfiguration
                {
                    IsEnabled = true,
                    FailureThreshold = 5,
                    RecoveryTimeout = TimeSpan.FromMinutes(1),
                    OperationTimeout = TimeSpan.FromSeconds(30)
                },
                RetryPolicies = new RetryPolicyConfiguration
                {
                    IsEnabled = true,
                    MaxRetries = 3,
                    InitialDelay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(30),
                    Strategy = RetryStrategyType.Exponential
                },
                HealthMonitoring = new HealthMonitoringConfiguration
                {
                    IsEnabled = true,
                    MonitoringInterval = TimeSpan.FromMinutes(1)
                },
                RecoveryEngine = new RecoveryEngineConfiguration
                {
                    MaxConcurrentRecoveries = 3,
                    MaxQueueSize = 1000,
                    ProcessingInterval = TimeSpan.FromSeconds(1),
                    DefaultTimeout = TimeSpan.FromMinutes(5),
                    MaxRecoveryAttempts = 3,
                    EnableAutomaticRecovery = true,
                    EnableManualRecovery = true
                },
                Logging = new ErrorRecoveryLoggingConfiguration
                {
                    LogRecoveryAttempts = true,
                    LogSuccessfulRecoveries = true,
                    LogFailedRecoveries = true,
                    LogDegradationEvents = true,
                    LogCircuitBreakerEvents = true,
                    LogFallbackAttempts = true,
                    LogRetentionPeriod = TimeSpan.FromDays(7)
                }
            };

            // In a real implementation, this would serialize to JSON/JSON and save to file
            // For now, we'll just log that we would create the file
            Console.WriteLine($"Would create default configuration file at {filePath}");
        }
    }
}