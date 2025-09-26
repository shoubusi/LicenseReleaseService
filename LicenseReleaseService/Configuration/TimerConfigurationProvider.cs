using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private Dictionary<string, TimerConfigurationElement> _versionSpecificCache;
        private DateTime _lastVersionDetectionTime;
        private readonly List<string> _detectedVersions = new List<string>();
        private Timer _versionDetectionTimer;

        /// <summary>
        /// Event raised when configuration is reloaded
        /// </summary>
        public event EventHandler<TimerConfigurationChangedEventArgs> ConfigurationReloaded;

        /// <summary>
        /// Event raised when versions are detected or changed
        /// </summary>
        public event EventHandler<TimerVersionDetectionEventArgs> VersionsDetected;

        /// <summary>
        /// Event raised when version-specific configuration is loaded
        /// </summary>
        public event EventHandler<TimerVersionConfigurationEventArgs> VersionConfigurationLoaded;

        /// <summary>
        /// Initializes a new instance of the TimerConfigurationProvider class
        /// </summary>
        /// <param name="configurationManager">The configuration manager instance</param>
        public TimerConfigurationProvider(ConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _lastCacheTime = DateTime.MinValue;
            _versionSpecificCache = new Dictionary<string, TimerConfigurationElement>();
            InitializeConfigWatcher();
            InitializeVersionDetection();
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
        /// Gets the currently detected versions
        /// </summary>
        public List<string> DetectedVersions
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_detectedVersions);
                }
            }
        }

        /// <summary>
        /// Gets whether version-specific configuration is enabled
        /// </summary>
        public bool IsVersionSpecificConfigEnabled => CurrentConfiguration.EnableVersionSpecificConfig;

        /// <summary>
        /// Gets the target version for configuration
        /// </summary>
        public string TargetVersion => CurrentConfiguration.TargetVersion;

        /// <summary>
        /// Gets the default version for fallback
        /// </summary>
        public string DefaultVersion => CurrentConfiguration.DefaultVersion;

        /// <summary>
        /// Gets the supported versions from configuration
        /// </summary>
        public List<string> SupportedVersions
        {
            get
            {
                lock (_lock)
                {
                    return CurrentConfiguration.ToTimerExecutionOptions().SupportedVersions;
                }
            }
        }

        /// <summary>
        /// Gets configuration for a specific version
        /// </summary>
        /// <param name="version">Version to get configuration for</param>
        /// <returns>Configuration for the specified version</returns>
        public TimerConfigurationElement GetVersionConfiguration(string version)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(version))
                    throw new ArgumentException("Version cannot be null or empty", nameof(version));

                // Return cached version if available
                if (_versionSpecificCache.TryGetValue(version, out var cachedConfig))
                {
                    return cachedConfig;
                }

                // Load version-specific configuration
                var versionConfig = LoadVersionSpecificConfiguration(version);
                if (versionConfig != null)
                {
                    _versionSpecificCache[version] = versionConfig;
                    OnVersionConfigurationLoaded(new TimerVersionConfigurationEventArgs
                    {
                        Version = version,
                        Configuration = versionConfig,
                        LoadTime = DateTime.Now
                    });
                }

                return versionConfig ?? GetFallbackConfiguration(version);
            }
        }

        /// <summary>
        /// Gets the active configuration based on version detection
        /// </summary>
        /// <returns>Active configuration for current environment</returns>
        public TimerConfigurationElement GetActiveConfiguration()
        {
            lock (_lock)
            {
                if (!IsVersionSpecificConfigEnabled)
                {
                    return CurrentConfiguration;
                }

                var activeVersion = GetActiveVersion();
                if (!string.IsNullOrEmpty(activeVersion))
                {
                    return GetVersionConfiguration(activeVersion);
                }

                // Fallback to default version or base configuration
                return GetFallbackConfiguration(activeVersion);
            }
        }

        /// <summary>
        /// Gets the active version based on detection logic
        /// </summary>
        /// <returns>Active version or empty string</returns>
        private string GetActiveVersion()
        {
            var config = CurrentConfiguration;

            // Use target version if specified and detected
            if (!string.IsNullOrEmpty(config.TargetVersion) && _detectedVersions.Contains(config.TargetVersion))
            {
                return config.TargetVersion;
            }

            // Use the latest detected version if no specific target
            if (_detectedVersions.Count > 0)
            {
                return _detectedVersions.OrderByDescending(v => v).First();
            }

            // Fallback to default version if enabled
            if (config.EnableVersionFallback && !string.IsNullOrEmpty(config.DefaultVersion))
            {
                return config.DefaultVersion;
            }

            return string.Empty;
        }

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
        /// Initializes version detection
        /// </summary>
        private void InitializeVersionDetection()
        {
            var config = CurrentConfiguration;
            if (config.EnableVersionSpecificConfig)
            {
                _versionDetectionTimer = new Timer(DetectVersionsCallback, null,
                    TimeSpan.FromSeconds(10), // Initial delay
                    config.VersionDetectionInterval);
            }
        }

        /// <summary>
        /// Callback for version detection timer
        /// </summary>
        /// <param name="state">Timer state</param>
        private void DetectVersionsCallback(object state)
        {
            try
            {
                DetectVersions();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in version detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects available SolidWorks versions
        /// </summary>
        public void DetectVersions()
        {
            lock (_lock)
            {
                try
                {
                    var newVersions = DetectAvailableVersions();
                    var versionsChanged = !_detectedVersions.SequenceEqual(newVersions);

                    if (versionsChanged)
                    {
                        var oldVersions = new List<string>(_detectedVersions);
                        _detectedVersions.Clear();
                        _detectedVersions.AddRange(newVersions);

                        // Clear version cache when versions change
                        _versionSpecificCache.Clear();

                        OnVersionsDetected(new TimerVersionDetectionEventArgs
                        {
                            OldVersions = oldVersions,
                            NewVersions = new List<string>(_detectedVersions),
                            DetectionTime = DateTime.Now
                        });
                    }

                    _lastVersionDetectionTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error detecting versions: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Detects available SolidWorks versions from the system
        /// </summary>
        /// <returns>List of detected versions</returns>
        private List<string> DetectAvailableVersions()
        {
            var detectedVersions = new List<string>();
            var config = CurrentConfiguration;

            // Check registry for SolidWorks installations
            detectedVersions.AddRange(DetectVersionsFromRegistry());

            // Check common installation paths
            detectedVersions.AddRange(DetectVersionsFromInstallationPaths());

            // Filter by supported versions if specified
            if (config.SupportedVersions.Count > 0)
            {
                detectedVersions = detectedVersions
                    .Where(v => config.SupportedVersions.Contains(v))
                    .ToList();
            }

            // Remove duplicates and return sorted
            return detectedVersions.Distinct().OrderBy(v => v).ToList();
        }

        /// <summary>
        /// Detects versions from Windows registry
        /// </summary>
        /// <returns>List of versions from registry</returns>
        private List<string> DetectVersionsFromRegistry()
        {
            var versions = new List<string>();

            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(subKeyName, @"^20[0-9]{2}$"))
                            {
                                versions.Add(subKeyName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting versions from registry: {ex.Message}");
            }

            return versions;
        }

        /// <summary>
        /// Detects versions from common installation paths
        /// </summary>
        /// <returns>List of versions from installation paths</returns>
        private List<string> DetectVersionsFromInstallationPaths()
        {
            var versions = new List<string>();
            var commonPaths = new[]
            {
                @"C:\Program Files\SOLIDWORKS Corp",
                @"C:\Program Files (x86)\SOLIDWORKS Corp",
                @"D:\SOLIDWORKS",
                @"E:\SOLIDWORKS"
            };

            foreach (var basePath in commonPaths)
            {
                try
                {
                    if (Directory.Exists(basePath))
                    {
                        foreach (var dir in Directory.GetDirectories(basePath))
                        {
                            var dirName = Path.GetFileName(dir);
                            if (System.Text.RegularExpressions.Regex.IsMatch(dirName, @"^SOLIDWORKS (20[0-9]{2})$"))
                            {
                                var match = System.Text.RegularExpressions.Regex.Match(dirName, @"^SOLIDWORKS (20[0-9]{2})$");
                                if (match.Success)
                                {
                                    versions.Add(match.Groups[1].Value);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error detecting versions from path {basePath}: {ex.Message}");
                }
            }

            return versions;
        }

        /// <summary>
        /// Loads version-specific configuration
        /// </summary>
        /// <param name="version">Version to load configuration for</param>
        /// <returns>Version-specific configuration or null</returns>
        private TimerConfigurationElement LoadVersionSpecificConfiguration(string version)
        {
            try
            {
                // For now, create version-specific config by modifying base config
                // In a real implementation, this could load from separate config files
                var baseConfig = CurrentConfiguration;
                var versionConfig = (TimerConfigurationElement)baseConfig.Clone();

                // Apply version-specific overrides
                ApplyVersionSpecificOverrides(versionConfig, version);

                return versionConfig;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading version-specific configuration for {version}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Applies version-specific configuration overrides
        /// </summary>
        /// <param name="config">Configuration to modify</param>
        /// <param name="version">Version to apply overrides for</param>
        private void ApplyVersionSpecificOverrides(TimerConfigurationElement config, string version)
        {
            // Apply version-specific timing adjustments
            switch (version)
            {
                case "2025":
                    config.DefaultInterval = TimeSpan.FromMinutes(3);
                    config.ExecutionTimeout = TimeSpan.FromMinutes(4);
                    config.CircuitBreakerCooldown = TimeSpan.FromMinutes(8);
                    config.MaxExecutionHistory = 75;
                    break;
                case "2024":
                    config.DefaultInterval = TimeSpan.FromMinutes(4);
                    config.ExecutionTimeout = TimeSpan.FromMinutes(5);
                    config.CircuitBreakerCooldown = TimeSpan.FromMinutes(10);
                    config.MaxExecutionHistory = 60;
                    break;
                case "2023":
                    config.DefaultInterval = TimeSpan.FromMinutes(5);
                    config.ExecutionTimeout = TimeSpan.FromMinutes(6);
                    config.CircuitBreakerCooldown = TimeSpan.FromMinutes(12);
                    config.MaxExecutionHistory = 50;
                    break;
                default:
                    config.DefaultInterval = TimeSpan.FromMinutes(5);
                    config.ExecutionTimeout = TimeSpan.FromMinutes(5);
                    config.CircuitBreakerCooldown = TimeSpan.FromMinutes(10);
                    config.MaxExecutionHistory = 100;
                    break;
            }

            // Set target version
            config.TargetVersion = version;
        }

        /// <summary>
        /// Gets fallback configuration
        /// </summary>
        /// <param name="requestedVersion">Requested version</param>
        /// <returns>Fallback configuration</returns>
        private TimerConfigurationElement GetFallbackConfiguration(string requestedVersion)
        {
            var config = CurrentConfiguration;

            if (config.EnableVersionFallback && !string.IsNullOrEmpty(config.DefaultVersion))
            {
                return GetVersionConfiguration(config.DefaultVersion);
            }

            // Return base configuration as fallback
            return config;
        }

        /// <summary>
        /// Raises the VersionsDetected event
        /// </summary>
        protected virtual void OnVersionsDetected(TimerVersionDetectionEventArgs e)
        {
            VersionsDetected?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the VersionConfigurationLoaded event
        /// </summary>
        protected virtual void OnVersionConfigurationLoaded(TimerVersionConfigurationEventArgs e)
        {
            VersionConfigurationLoaded?.Invoke(this, e);
        }

        /// <summary>
        /// Forces version detection
        /// </summary>
        public void ForceVersionDetection()
        {
            DetectVersions();
        }

        /// <summary>
        /// Gets version-specific statistics
        /// </summary>
        /// <returns>Version-specific statistics</returns>
        public TimerVersionStatistics GetVersionStatistics()
        {
            var baseStats = GetStatistics();

            return new TimerVersionStatistics
            {
                BaseStatistics = baseStats,
                IsVersionSpecificConfigEnabled = IsVersionSpecificConfigEnabled,
                TargetVersion = TargetVersion,
                DefaultVersion = DefaultVersion,
                DetectedVersions = new List<string>(DetectedVersions),
                SupportedVersions = new List<string>(SupportedVersions),
                VersionCacheCount = _versionSpecificCache.Count,
                LastVersionDetectionTime = _lastVersionDetectionTime,
                ActiveVersion = GetActiveVersion(),
                VersionDetectionInterval = CurrentConfiguration.VersionDetectionInterval
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
                    _versionDetectionTimer?.Dispose();
                    _versionDetectionTimer = null;
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

    /// <summary>
    /// Event arguments for version detection events
    /// </summary>
    public class TimerVersionDetectionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old versions list
        /// </summary>
        public List<string> OldVersions { get; set; }

        /// <summary>
        /// Gets the new versions list
        /// </summary>
        public List<string> NewVersions { get; set; }

        /// <summary>
        /// Gets the detection time
        /// </summary>
        public DateTime DetectionTime { get; set; }

        /// <summary>
        /// Gets whether versions were added
        /// </summary>
        public bool VersionsAdded => NewVersions.Except(OldVersions).Any();

        /// <summary>
        /// Gets whether versions were removed
        /// </summary>
        public bool VersionsRemoved => OldVersions.Except(NewVersions).Any();

        /// <summary>
        /// Gets the list of added versions
        /// </summary>
        public List<string> AddedVersions => NewVersions.Except(OldVersions).ToList();

        /// <summary>
        /// Gets the list of removed versions
        /// </summary>
        public List<string> RemovedVersions => OldVersions.Except(NewVersions).ToList();
    }

    /// <summary>
    /// Event arguments for version configuration loading events
    /// </summary>
    public class TimerVersionConfigurationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets the loaded configuration
        /// </summary>
        public TimerConfigurationElement Configuration { get; set; }

        /// <summary>
        /// Gets the load time
        /// </summary>
        public DateTime LoadTime { get; set; }

        /// <summary>
        /// Gets whether this was loaded from cache
        /// </summary>
        public bool FromCache { get; set; }
    }

    /// <summary>
    /// Version-specific statistics for timer configuration
    /// </summary>
    public class TimerVersionStatistics
    {
        /// <summary>
        /// Gets the base configuration statistics
        /// </summary>
        public TimerConfigurationStatistics BaseStatistics { get; set; }

        /// <summary>
        /// Gets whether version-specific configuration is enabled
        /// </summary>
        public bool IsVersionSpecificConfigEnabled { get; set; }

        /// <summary>
        /// Gets the target version
        /// </summary>
        public string TargetVersion { get; set; }

        /// <summary>
        /// Gets the default version for fallback
        /// </summary>
        public string DefaultVersion { get; set; }

        /// <summary>
        /// Gets the detected versions
        /// </summary>
        public List<string> DetectedVersions { get; set; }

        /// <summary>
        /// Gets the supported versions
        /// </summary>
        public List<string> SupportedVersions { get; set; }

        /// <summary>
        /// Gets the number of cached version configurations
        /// </summary>
        public int VersionCacheCount { get; set; }

        /// <summary>
        /// Gets the last version detection time
        /// </summary>
        public DateTime LastVersionDetectionTime { get; set; }

        /// <summary>
        /// Gets the currently active version
        /// </summary>
        public string ActiveVersion { get; set; }

        /// <summary>
        /// Gets the version detection interval
        /// </summary>
        public TimeSpan VersionDetectionInterval { get; set; }

        /// <summary>
        /// Gets whether version detection is running
        /// </summary>
        public bool VersionDetectionRunning { get; set; }

        /// <summary>
        /// Gets the time until next version detection
        /// </summary>
        public TimeSpan TimeUntilNextDetection { get; set; }
    }
}