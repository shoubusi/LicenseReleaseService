using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Provides configuration management for timer execution settings
    /// </summary>
    public class TimerConfigurationProvider
    {
        private static readonly object _lock = new object();
        private readonly ConfigurationManager _configurationManager;
        private TimerConfigurationElement _cachedConfiguration;
        private DateTime _lastCacheTime;
        private TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private FileSystemWatcher _configWatcher;
        private bool _disposed;

        /// <summary>
        /// Event raised when configuration is reloaded
        /// </summary>
        public event EventHandler<TimerConfigurationChangedEventArgs> ConfigurationReloaded;

        /// <summary>
        /// Initializes a new instance of the TimerConfigurationProvider class
        /// </summary>
        /// <param name="configurationManager">The configuration manager instance</param>
        public TimerConfigurationProvider(ConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _lastCacheTime = DateTime.MinValue;
            InitializeConfigWatcher();
        }

        /// <summary>
        /// Gets or sets the cache duration for configuration
        /// </summary>
        public TimeSpan CacheDuration
        {
            get => _cacheDuration;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Cache duration must be greater than zero", nameof(value));
                }
                _cacheDuration = value;
            }
        }

        /// <summary>
        /// Gets the current timer configuration
        /// </summary>
        public TimerConfigurationElement CurrentConfiguration
        {
            get
            {
                lock (_lock)
                {
                    if (NeedsReload())
                    {
                        ReloadConfiguration();
                    }
                    return _cachedConfiguration ?? GetDefaultConfiguration();
                }
            }
        }

        /// <summary>
        /// Gets the timer execution options from configuration
        /// </summary>
        public TimerExecution.TimerExecutionOptions TimerExecutionOptions => CurrentConfiguration.ToTimerExecutionOptions();

        /// <summary>
        /// Gets the default timer configuration
        /// </summary>
        /// <returns>Default timer configuration</returns>
        private TimerConfigurationElement GetDefaultConfiguration()
        {
            return new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                MaxConcurrentExecutions = 1,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(5),
                EnableAutoRestart = true,
                EnableExecutionTimeout = true,
                ExecutionTimeout = TimeSpan.FromMinutes(5),
                EnableMetrics = true,
                EnableDetailedLogging = true,
                EnableCircuitBreaker = true,
                MaxExecutionHistory = 100,
                MinInterval = TimeSpan.FromSeconds(1),
                MaxInterval = TimeSpan.FromDays(1),
                SyncTimeout = TimeSpan.FromSeconds(30),
                StopOnUnhandledException = false,
                DisposalGracePeriod = TimeSpan.FromSeconds(5),
                PreventExecutionOverlap = true,
                StartupDelay = TimeSpan.FromSeconds(10),
                ShutdownTimeout = TimeSpan.FromSeconds(30),
                HealthCheckInterval = 60,
                MetricsCollectionInterval = 30,
                EnableAdaptiveScheduling = false,
                AdaptiveThreshold = 80,
                AdaptiveCooldown = TimeSpan.FromMinutes(5),
                EnableMemoryMonitoring = true,
                MemoryThreshold = 52428800, // 50MB
                EnableCpuMonitoring = true,
                CpuThreshold = 80,
                EnableThreadMonitoring = true,
                MaxThreadPoolThreads = 10
            };
        }

        /// <summary>
        /// Determines if the configuration needs to be reloaded
        /// </summary>
        /// <returns>True if reload is needed</returns>
        private bool NeedsReload()
        {
            return _cachedConfiguration == null ||
                   (DateTime.Now - _lastCacheTime) > _cacheDuration;
        }

        /// <summary>
        /// Reloads the configuration from the configuration system
        /// </summary>
        private void ReloadConfiguration()
        {
            try
            {
                var oldConfiguration = _cachedConfiguration;
                var newConfiguration = LoadConfigurationFromSystem();

                lock (_lock)
                {
                    _cachedConfiguration = newConfiguration;
                    _lastCacheTime = DateTime.Now;
                }

                // Raise configuration changed event if configuration actually changed
                if (!ConfigurationsEqual(oldConfiguration, newConfiguration))
                {
                    OnConfigurationReloaded(new TimerConfigurationChangedEventArgs
                    {
                        OldConfiguration = oldConfiguration,
                        NewConfiguration = newConfiguration,
                        ReloadTime = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                // Log the error but keep using cached configuration if available
                System.Diagnostics.Debug.WriteLine($"Error reloading timer configuration: {ex.Message}");

                if (_cachedConfiguration == null)
                {
                    // If no cached configuration, use defaults
                    _cachedConfiguration = GetDefaultConfiguration();
                    _lastCacheTime = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Loads configuration from the configuration system
        /// </summary>
        /// <returns>Timer configuration element</returns>
        private TimerConfigurationElement LoadConfigurationFromSystem()
        {
            try
            {
                var serviceConfig = _configurationManager.CurrentConfiguration;
                return serviceConfig?.Timer ?? GetDefaultConfiguration();
            }
            catch (ConfigurationErrorsException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Configuration error loading timer configuration: {ex.Message}");
                return GetDefaultConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error loading timer configuration: {ex.Message}");
                return GetDefaultConfiguration();
            }
        }

        /// <summary>
        /// Initializes the configuration file watcher
        /// </summary>
        private void InitializeConfigWatcher()
        {
            try
            {
                var configPath = GetConfigFilePath();
                if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                {
                    var directory = Path.GetDirectoryName(configPath);
                    var fileName = Path.GetFileName(configPath);

                    _configWatcher = new FileSystemWatcher(directory, fileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };

                    _configWatcher.Changed += OnConfigFileChanged;
                    _configWatcher.Created += OnConfigFileChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing configuration watcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the configuration file path
        /// </summary>
        /// <returns>Configuration file path</returns>
        private string GetConfigFilePath()
        {
            try
            {
                var appDomain = AppDomain.CurrentDomain;
                var configPath = appDomain.SetupInformation.ConfigurationFile;
                return !string.IsNullOrEmpty(configPath) ? configPath : "App.config";
            }
            catch
            {
                return "App.config";
            }
        }

        /// <summary>
        /// Handles configuration file changes
        /// </summary>
        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Debounce file system events
                Thread.Sleep(500);

                lock (_lock)
                {
                    ReloadConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling configuration file change: {ex.Message}");
            }
        }

        /// <summary>
        /// Compares two timer configurations for equality
        /// </summary>
        private bool ConfigurationsEqual(TimerConfigurationElement config1, TimerConfigurationElement config2)
        {
            if (config1 == null && config2 == null) return true;
            if (config1 == null || config2 == null) return false;

            return config1.DefaultInterval == config2.DefaultInterval &&
                   config1.MaxConsecutiveErrors == config2.MaxConsecutiveErrors &&
                   config1.MaxConcurrentExecutions == config2.MaxConcurrentExecutions &&
                   config1.CircuitBreakerCooldown == config2.CircuitBreakerCooldown &&
                   config1.EnableAutoRestart == config2.EnableAutoRestart &&
                   config1.EnableExecutionTimeout == config2.EnableExecutionTimeout &&
                   config1.ExecutionTimeout == config2.ExecutionTimeout &&
                   config1.EnableMetrics == config2.EnableMetrics &&
                   config1.EnableDetailedLogging == config2.EnableDetailedLogging &&
                   config1.EnableCircuitBreaker == config2.EnableCircuitBreaker &&
                   config1.MaxExecutionHistory == config2.MaxExecutionHistory &&
                   config1.MinInterval == config2.MinInterval &&
                   config1.MaxInterval == config2.MaxInterval &&
                   config1.SyncTimeout == config2.SyncTimeout &&
                   config1.StopOnUnhandledException == config2.StopOnUnhandledException &&
                   config1.DisposalGracePeriod == config2.DisposalGracePeriod &&
                   config1.PreventExecutionOverlap == config2.PreventExecutionOverlap &&
                   config1.StartupDelay == config2.StartupDelay &&
                   config1.ShutdownTimeout == config2.ShutdownTimeout &&
                   config1.HealthCheckInterval == config2.HealthCheckInterval &&
                   config1.MetricsCollectionInterval == config2.MetricsCollectionInterval &&
                   config1.EnableAdaptiveScheduling == config2.EnableAdaptiveScheduling &&
                   config1.AdaptiveThreshold == config2.AdaptiveThreshold &&
                   config1.AdaptiveCooldown == config2.AdaptiveCooldown &&
                   config1.EnableMemoryMonitoring == config2.EnableMemoryMonitoring &&
                   config1.MemoryThreshold == config2.MemoryThreshold &&
                   config1.EnableCpuMonitoring == config2.EnableCpuMonitoring &&
                   config1.CpuThreshold == config2.CpuThreshold &&
                   config1.EnableThreadMonitoring == config2.EnableThreadMonitoring &&
                   config1.MaxThreadPoolThreads == config2.MaxThreadPoolThreads;
        }

        /// <summary>
        /// Raises the ConfigurationReloaded event
        /// </summary>
        protected virtual void OnConfigurationReloaded(TimerConfigurationChangedEventArgs e)
        {
            ConfigurationReloaded?.Invoke(this, e);
        }

        /// <summary>
        /// Forces a reload of the configuration
        /// </summary>
        public void ForceReload()
        {
            lock (_lock)
            {
                ReloadConfiguration();
            }
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> ValidateConfiguration()
        {
            var configuration = CurrentConfiguration;
            var errors = configuration.Validate();

            // Additional provider-level validation
            if (CacheDuration < TimeSpan.FromSeconds(30))
            {
                errors.Add("Cache duration should be at least 30 seconds for performance reasons");
            }

            if (CacheDuration > TimeSpan.FromHours(1))
            {
                errors.Add("Cache duration should not exceed 1 hour for configuration responsiveness");
            }

            return errors;
        }

        /// <summary>
        /// Gets configuration statistics
        /// </summary>
        /// <returns>Configuration statistics</returns>
        public TimerConfigurationStatistics GetStatistics()
        {
            var config = CurrentConfiguration;

            return new TimerConfigurationStatistics
            {
                LastReloadTime = _lastCacheTime,
                CacheDuration = _cacheDuration,
                CacheTimeRemaining = _cacheDuration - (DateTime.Now - _lastCacheTime),
                IsCacheValid = !NeedsReload(),
                DefaultIntervalMs = config.DefaultInterval.TotalMilliseconds,
                MaxConsecutiveErrors = config.MaxConsecutiveErrors,
                MaxConcurrentExecutions = config.MaxConcurrentExecutions,
                CircuitBreakerCooldownMs = config.CircuitBreakerCooldown.TotalMilliseconds,
                EnableAutoRestart = config.EnableAutoRestart,
                EnableExecutionTimeout = config.EnableExecutionTimeout,
                ExecutionTimeoutMs = config.ExecutionTimeout.TotalMilliseconds,
                EnableMetrics = config.EnableMetrics,
                EnableDetailedLogging = config.EnableDetailedLogging,
                EnableCircuitBreaker = config.EnableCircuitBreaker,
                EnableAdaptiveScheduling = config.EnableAdaptiveScheduling,
                EnableMemoryMonitoring = config.EnableMemoryMonitoring,
                EnableCpuMonitoring = config.EnableCpuMonitoring,
                EnableThreadMonitoring = config.EnableThreadMonitoring,
                ConfigWatcherEnabled = _configWatcher?.EnableRaisingEvents ?? false
            };
        }

        /// <summary>
        /// Disposes the configuration provider
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _configWatcher?.Dispose();
                    _configWatcher = null;
                }
                _disposed = true;
            }
        }

        ~TimerConfigurationProvider()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Event arguments for timer configuration changes
    /// </summary>
    public class TimerConfigurationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old configuration
        /// </summary>
        public TimerConfigurationElement OldConfiguration { get; set; }

        /// <summary>
        /// Gets the new configuration
        /// </summary>
        public TimerConfigurationElement NewConfiguration { get; set; }

        /// <summary>
        /// Gets the reload time
        /// </summary>
        public DateTime ReloadTime { get; set; }
    }

    /// <summary>
    /// Statistics for timer configuration
    /// </summary>
    public class TimerConfigurationStatistics
    {
        /// <summary>
        /// Gets the last reload time
        /// </summary>
        public DateTime LastReloadTime { get; set; }

        /// <summary>
        /// Gets the cache duration
        /// </summary>
        public TimeSpan CacheDuration { get; set; }

        /// <summary>
        /// Gets the remaining cache time
        /// </summary>
        public TimeSpan CacheTimeRemaining { get; set; }

        /// <summary>
        /// Gets whether the cache is valid
        /// </summary>
        public bool IsCacheValid { get; set; }

        /// <summary>
        /// Gets the default interval in milliseconds
        /// </summary>
        public double DefaultIntervalMs { get; set; }

        /// <summary>
        /// Gets the maximum consecutive errors
        /// </summary>
        public int MaxConsecutiveErrors { get; set; }

        /// <summary>
        /// Gets the maximum concurrent executions
        /// </summary>
        public int MaxConcurrentExecutions { get; set; }

        /// <summary>
        /// Gets the circuit breaker cooldown in milliseconds
        /// </summary>
        public double CircuitBreakerCooldownMs { get; set; }

        /// <summary>
        /// Gets whether auto restart is enabled
        /// </summary>
        public bool EnableAutoRestart { get; set; }

        /// <summary>
        /// Gets whether execution timeout is enabled
        /// </summary>
        public bool EnableExecutionTimeout { get; set; }

        /// <summary>
        /// Gets the execution timeout in milliseconds
        /// </summary>
        public double ExecutionTimeoutMs { get; set; }

        /// <summary>
        /// Gets whether metrics are enabled
        /// </summary>
        public bool EnableMetrics { get; set; }

        /// <summary>
        /// Gets whether detailed logging is enabled
        /// </summary>
        public bool EnableDetailedLogging { get; set; }

        /// <summary>
        /// Gets whether circuit breaker is enabled
        /// </summary>
        public bool EnableCircuitBreaker { get; set; }

        /// <summary>
        /// Gets whether adaptive scheduling is enabled
        /// </summary>
        public bool EnableAdaptiveScheduling { get; set; }

        /// <summary>
        /// Gets whether memory monitoring is enabled
        /// </summary>
        public bool EnableMemoryMonitoring { get; set; }

        /// <summary>
        /// Gets whether CPU monitoring is enabled
        /// </summary>
        public bool EnableCpuMonitoring { get; set; }

        /// <summary>
        /// Gets whether thread monitoring is enabled
        /// </summary>
        public bool EnableThreadMonitoring { get; set; }

        /// <summary>
        /// Gets whether the configuration watcher is enabled
        /// </summary>
        public bool ConfigWatcherEnabled { get; set; }
    }
}