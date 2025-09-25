using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GlobalConfig = System.Configuration.ConfigurationManager;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Manages configuration loading, validation, and reload capabilities
    /// </summary>
    public class ConfigurationManager : IDisposable
    {
        private static readonly object _lock = new object();
        private static ConfigurationManager _instance;
        private static readonly object _instanceLock = new object();

        private readonly FileSystemWatcher _configWatcher;
        private readonly Timer _reloadTimer;
        private readonly object _configLock = new object();
        private LicenseReleaseServiceSection _currentConfiguration;
        private ServiceSettings _serviceSettings;
        private bool _isDisposed;
        private DateTime _lastConfigChange;
        private DateTime _lastSuccessfulReload;
        private readonly string _configFilePath;
        private readonly List<string> _reloadErrors = new List<string>();

        // New components for advanced monitoring
        private ConfigurationWatcher _advancedWatcher;
        private ConfigurationReloadManager _reloadManager;
        private ConfigurationHealthMonitor _healthMonitor;
        private bool _advancedFeaturesEnabled;

        /// <summary>
        /// Event raised when configuration is reloaded
        /// </summary>
        public event EventHandler<ConfigurationReloadEventArgs> ConfigurationChanged;

        /// <summary>
        /// Event raised when configuration file changes
        /// </summary>
        public event ConfigurationEvents.ConfigurationFileChangedEventHandler ConfigurationFileChanged;

        /// <summary>
        /// Event raised when configuration health changes
        /// </summary>
        public event ConfigurationEvents.ConfigurationHealthEventHandler ConfigurationHealthChanged;

        /// <summary>
        /// Event raised when configuration backup operations complete
        /// </summary>
        public event ConfigurationEvents.ConfigurationBackupEventHandler ConfigurationBackupCompleted;

        /// <summary>
        /// Gets the singleton instance of ConfigurationManager
        /// </summary>
        public static ConfigurationManager Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = new ConfigurationManager();
                    }
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        public LicenseReleaseServiceSection CurrentConfiguration
        {
            get
            {
                lock (_configLock)
                {
                    return _currentConfiguration ?? LoadConfigurationInternal();
                }
            }
        }

        /// <summary>
        /// Gets the service settings wrapper
        /// </summary>
        public ServiceSettings Settings
        {
            get
            {
                lock (_configLock)
                {
                    return _serviceSettings ?? (_serviceSettings = new ServiceSettings(this));
                }
            }
        }

        /// <summary>
        /// Gets whether the configuration is valid
        /// </summary>
        public bool IsConfigurationValid
        {
            get
            {
                try
                {
                    var config = CurrentConfiguration;
                    var errors = config.Validate();
                    return errors.Count == 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the last configuration change time
        /// </summary>
        public DateTime LastConfigChange => _lastConfigChange;

        /// <summary>
        /// Gets the last successful reload time
        /// </summary>
        public DateTime LastSuccessfulReload => _lastSuccessfulReload;

        /// <summary>
        /// Gets the configuration file path being monitored
        /// </summary>
        public string ConfigFilePath => _configFilePath;

        /// <summary>
        /// Gets the list of recent reload errors
        /// </summary>
        public IReadOnlyList<string> ReloadErrors => _reloadErrors.AsReadOnly();

        /// <summary>
        /// Gets the advanced configuration watcher
        /// </summary>
        public ConfigurationWatcher AdvancedWatcher => _advancedWatcher;

        /// <summary>
        /// Gets the configuration reload manager
        /// </summary>
        public ConfigurationReloadManager ReloadManager => _reloadManager;

        /// <summary>
        /// Gets the configuration health monitor
        /// </summary>
        public ConfigurationHealthMonitor HealthMonitor => _healthMonitor;

        /// <summary>
        /// Gets whether advanced features are enabled
        /// </summary>
        public bool AdvancedFeaturesEnabled => _advancedFeaturesEnabled;

        /// <summary>
        /// Gets the configuration health status
        /// </summary>
        public ConfigurationHealthStatus HealthStatus => _healthMonitor?.CurrentHealthStatus ?? ConfigurationHealthStatus.Unknown;

        /// <summary>
        /// Gets the configuration health report
        /// </summary>
        public ConfigurationHealthReport HealthReport => _healthMonitor?.GetHealthReport();

        /// <summary>
        /// Initializes a new instance of the ConfigurationManager class
        /// </summary>
        private ConfigurationManager()
        {
            try
            {
                _configFilePath = GetConfigFilePath();
                _lastConfigChange = File.GetLastWriteTime(_configFilePath);

                // Initialize configuration
                _currentConfiguration = LoadConfigurationInternal();
                _serviceSettings = new ServiceSettings(this);
                _lastSuccessfulReload = DateTime.Now;

                // Set up file system watcher for config changes
                var configDirectory = Path.GetDirectoryName(_configFilePath);
                var configFileName = Path.GetFileName(_configFilePath);

                _configWatcher = new FileSystemWatcher(configDirectory, configFileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _configWatcher.Changed += OnConfigFileChanged;
                _configWatcher.Created += OnConfigFileChanged;
                _configWatcher.Renamed += OnConfigFileRenamed;
                _configWatcher.Error += OnConfigWatcherError;

                // Set up reload timer to debounce rapid changes
                _reloadTimer = new Timer(OnReloadTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

                // Initialize advanced components (but don't start them yet)
                InitializeAdvancedComponents();
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException($"Failed to initialize ConfigurationManager: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the configuration file path
        /// </summary>
        private string GetConfigFilePath()
        {
            var appConfigPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (!string.IsNullOrEmpty(appConfigPath) && File.Exists(appConfigPath))
            {
                return appConfigPath;
            }

            // Fallback to App.config in the application directory
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appPath, "App.config");
        }

        /// <summary>
        /// Handles configuration file changes
        /// </summary>
        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                _lastConfigChange = DateTime.Now;
                _reloadTimer.Change(1000, Timeout.Infinite); // Debounce for 1 second
            }
            catch (Exception ex)
            {
                LogError($"Error handling config file change: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles configuration file rename events
        /// </summary>
        private void OnConfigFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                _lastConfigChange = DateTime.Now;
                _reloadTimer.Change(1000, Timeout.Infinite); // Debounce for 1 second
            }
            catch (Exception ex)
            {
                LogError($"Error handling config file rename: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles configuration watcher errors
        /// </summary>
        private void OnConfigWatcherError(object sender, ErrorEventArgs e)
        {
            var ex = e.GetException();
            LogError($"Configuration watcher error: {ex.Message}");

            // Try to restart the watcher
            try
            {
                _configWatcher.EnableRaisingEvents = false;
                _configWatcher.EnableRaisingEvents = true;
            }
            catch (Exception restartEx)
            {
                LogError($"Failed to restart configuration watcher: {restartEx.Message}");
            }
        }

        /// <summary>
        /// Handles the reload timer elapsed event
        /// </summary>
        private void OnReloadTimerElapsed(object state)
        {
            try
            {
                ReloadConfiguration();
            }
            catch (Exception ex)
            {
                LogError($"Error during configuration reload: {ex.Message}");
            }
        }

        /// <summary>
        /// Reloads the configuration from file
        /// </summary>
        public void ReloadConfiguration()
        {
            lock (_configLock)
            {
                try
                {
                    var oldConfig = _currentConfiguration;
                    var newConfig = LoadConfigurationInternal();

                    // Validate the new configuration
                    var validationErrors = newConfig.Validate();
                    if (validationErrors.Count > 0)
                    {
                        var errorString = string.Join("; ", validationErrors);
                        LogError($"Configuration validation failed: {errorString}");
                        _reloadErrors.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Validation failed: {errorString}");
                        KeepOnlyRecentErrors();
                        throw new ConfigurationErrorsException($"Configuration validation failed: {errorString}");
                    }

                    // Apply the new configuration
                    _currentConfiguration = newConfig;
                    _serviceSettings?.ReloadAppSettings();
                    _lastSuccessfulReload = DateTime.Now;

                    // Clear reload errors on successful reload
                    _reloadErrors.Clear();

                    // Raise the configuration changed event
                    OnConfigurationChanged(new ConfigurationReloadEventArgs(oldConfig, newConfig, validationErrors));
                }
                catch (Exception ex)
                {
                    LogError($"Failed to reload configuration: {ex.Message}");
                    _reloadErrors.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Reload failed: {ex.Message}");
                    KeepOnlyRecentErrors();
                    throw;
                }
            }
        }

        /// <summary>
        /// Loads the configuration from the configuration file
        /// </summary>
        private LicenseReleaseServiceSection LoadConfigurationInternal()
        {
            try
            {
                // Refresh configuration to get latest changes
                GlobalConfig.RefreshSection("licenseReleaseService");
                GlobalConfig.RefreshSection("appSettings");

                var config = (LicenseReleaseServiceSection)GlobalConfig.GetSection("licenseReleaseService");
                if (config == null)
                {
                    throw new ConfigurationErrorsException("licenseReleaseService section not found in configuration file");
                }

                return config;
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException($"Failed to load configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> ValidateConfiguration()
        {
            try
            {
                var config = CurrentConfiguration;
                var errors = config.Validate();

                // Validate service settings
                var settingsErrors = Settings.Validate();
                errors.AddRange(settingsErrors);

                return errors;
            }
            catch (Exception ex)
            {
                return new List<string> { $"Configuration validation error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Gets a specific configuration section
        /// </summary>
        /// <typeparam name="T">The configuration section type</typeparam>
        /// <param name="sectionName">The section name</param>
        /// <returns>The configuration section</returns>
        public T GetSection<T>(string sectionName) where T : ConfigurationSection
        {
            try
            {
                return (T)GlobalConfig.GetSection(sectionName);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException($"Failed to get section '{sectionName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets an application setting value
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The setting value</returns>
        public string GetAppSetting(string key, string defaultValue = null)
        {
            try
            {
                return GlobalConfig.AppSettings[key] ?? defaultValue;
            }
            catch (Exception ex)
            {
                LogError($"Failed to get app setting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Gets a connection string
        /// </summary>
        /// <param name="name">The connection string name</param>
        /// <returns>The connection string</returns>
        public string GetConnectionString(string name)
        {
            try
            {
                var connectionString = GlobalConfig.ConnectionStrings[name];
                return connectionString?.ConnectionString;
            }
            catch (Exception ex)
            {
                LogError($"Failed to get connection string '{name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Forces a configuration reload
        /// </summary>
        public void ForceReload()
        {
            ReloadConfiguration();
        }

        /// <summary>
        /// Gets the configuration as a formatted string
        /// </summary>
        public string GetConfigurationSummary()
        {
            try
            {
                var config = CurrentConfiguration;
                var sb = new System.Text.StringBuilder();

                sb.AppendLine("Configuration Summary:");
                sb.AppendLine($"  Config File: {_configFilePath}");
                sb.AppendLine($"  Last Change: {_lastConfigChange:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"  Last Reload: {_lastSuccessfulReload:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"  Is Valid: {IsConfigurationValid}");
                sb.AppendLine($"  Reload Errors: {_reloadErrors.Count}");

                if (_reloadErrors.Count > 0)
                {
                    sb.AppendLine("  Recent Errors:");
                    foreach (var error in _reloadErrors.Take(3))
                    {
                        sb.AppendLine($"    - {error}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine(config.ToString());
                sb.AppendLine();
                sb.AppendLine(Settings.ToString());

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Failed to get configuration summary: {ex.Message}";
            }
        }

        /// <summary>
        /// Raises the ConfigurationChanged event
        /// </summary>
        protected virtual void OnConfigurationChanged(ConfigurationReloadEventArgs e)
        {
            ConfigurationChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        private void LogError(string message)
        {
            // Use the service settings logger if available, otherwise use console
            try
            {
                if (Settings?.EnableEventLogging == true)
                {
                    // In a real implementation, this would use the ILogger interface
                    Console.WriteLine($"[ConfigurationManager Error] {message}");
                }
                else
                {
                    Console.WriteLine($"[ConfigurationManager Error] {message}");
                }
            }
            catch
            {
                Console.WriteLine($"[ConfigurationManager Error] {message}");
            }
        }

        /// <summary>
        /// Logs an info message
        /// </summary>
        private void LogInfo(string message)
        {
            try
            {
                Console.WriteLine($"[ConfigurationManager Info] {message}");
            }
            catch
            {
                Console.WriteLine($"[ConfigurationManager Info] {message}");
            }
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        private void LogWarning(string message)
        {
            try
            {
                Console.WriteLine($"[ConfigurationManager Warning] {message}");
            }
            catch
            {
                Console.WriteLine($"[ConfigurationManager Warning] {message}");
            }
        }

        /// <summary>
        /// Keeps only recent errors in the error list
        /// </summary>
        private void KeepOnlyRecentErrors()
        {
            const int maxErrors = 10;
            if (_reloadErrors.Count > maxErrors)
            {
                _reloadErrors.RemoveRange(0, _reloadErrors.Count - maxErrors);
            }
        }

        /// <summary>
        /// Initializes advanced monitoring components
        /// </summary>
        private void InitializeAdvancedComponents()
        {
            try
            {
                var backupDirectory = Path.Combine(Path.GetDirectoryName(_configFilePath), "backups");

                // Initialize advanced components
                _advancedWatcher = new ConfigurationWatcher(_configFilePath);
                _reloadManager = new ConfigurationReloadManager(_configFilePath, backupDirectory);
                _healthMonitor = new ConfigurationHealthMonitor();

                // Wire up event handlers
                _advancedWatcher.FileChanged += OnAdvancedFileChanged;
                _advancedWatcher.WatcherError += OnAdvancedWatcherError;
                _advancedWatcher.HealthCheck += OnAdvancedHealthCheck;

                _reloadManager.ReloadCompleted += OnReloadManagerReloadCompleted;
                _reloadManager.BackupCompleted += OnReloadManagerBackupCompleted;
                _reloadManager.ReloadOperationStarted += OnReloadManagerOperationStarted;
                _reloadManager.ReloadOperationFailed += OnReloadManagerOperationFailed;

                _healthMonitor.HealthCheckCompleted += OnHealthMonitorCheckCompleted;
                _healthMonitor.HealthAlertRaised += OnHealthMonitorAlertRaised;
                _healthMonitor.DiagnosticCompleted += OnHealthMonitorDiagnosticCompleted;
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize advanced components: {ex.Message}");
            }
        }

        /// <summary>
        /// Enables advanced monitoring features
        /// </summary>
        public void EnableAdvancedFeatures()
        {
            if (_advancedFeaturesEnabled)
                return;

            try
            {
                _advancedWatcher?.StartWatching();
                _reloadManager?.Start();
                _healthMonitor?.Start();

                _advancedFeaturesEnabled = true;
                LogInfo("Advanced configuration monitoring features enabled");
            }
            catch (Exception ex)
            {
                LogError($"Failed to enable advanced features: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Disables advanced monitoring features
        /// </summary>
        public void DisableAdvancedFeatures()
        {
            if (!_advancedFeaturesEnabled)
                return;

            try
            {
                _advancedWatcher?.StopWatching();
                _reloadManager?.Stop();
                _healthMonitor?.Stop();

                _advancedFeaturesEnabled = false;
                LogInfo("Advanced configuration monitoring features disabled");
            }
            catch (Exception ex)
            {
                LogError($"Failed to disable advanced features: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Performs an immediate health check
        /// </summary>
        public async Task<ConfigurationHealthStatus> PerformHealthCheckAsync()
        {
            if (_healthMonitor == null)
                return ConfigurationHealthStatus.Unknown;

            return await _healthMonitor.PerformHealthCheckAsync();
        }

        /// <summary>
        /// Reloads configuration using the advanced reload manager
        /// </summary>
        public async Task<bool> ReloadConfigurationAsync(ReloadTrigger trigger, bool createBackup = true)
        {
            if (_reloadManager == null)
                return false;

            return await _reloadManager.ReloadConfigurationAsync(trigger, createBackup);
        }

        /// <summary>
        /// Creates a backup of the current configuration
        /// </summary>
        public async Task<string> CreateBackupAsync(BackupReason reason)
        {
            if (_reloadManager == null)
                throw new InvalidOperationException("Reload manager is not available");

            return await _reloadManager.CreateBackupAsync(reason);
        }

        /// <summary>
        /// Restores configuration from a backup
        /// </summary>
        public async Task<bool> RestoreFromBackupAsync(Guid backupId)
        {
            if (_reloadManager == null)
                return false;

            return await _reloadManager.RestoreFromBackupAsync(backupId);
        }

        /// <summary>
        /// Gets available backups
        /// </summary>
        public IReadOnlyList<ConfigurationBackup> GetAvailableBackups()
        {
            return _reloadManager?.BackupHistory ?? new List<ConfigurationBackup>().AsReadOnly();
        }

        /// <summary>
        /// Forces a diagnostic collection
        /// </summary>
        public async Task<ConfigurationDiagnostic> ForceDiagnosticAsync()
        {
            if (_healthMonitor == null)
                throw new InvalidOperationException("Health monitor is not available");

            return await _healthMonitor.ForceDiagnosticAsync();
        }

        /// <summary>
        /// Adds a custom health rule to the monitor
        /// </summary>
        public void AddHealthRule(ConfigurationHealthRule rule)
        {
            _healthMonitor?.AddHealthRule(rule);
        }

        /// <summary>
        /// Removes a health rule from the monitor
        /// </summary>
        public bool RemoveHealthRule(string ruleName)
        {
            return _healthMonitor?.RemoveHealthRule(ruleName) ?? false;
        }

        /// <summary>
        /// Sets the reload strategy for the advanced reload manager
        /// </summary>
        public void SetReloadStrategy(ReloadStrategy strategy, int intervalSeconds = 60)
        {
            _reloadManager?.SetReloadStrategy(strategy, intervalSeconds);
        }

        // Advanced component event handlers

        private void OnAdvancedFileChanged(object sender, ConfigurationFileChangedEventArgs e)
        {
            try
            {
                ConfigurationFileChanged?.Invoke(this, e);
                LogInfo($"Advanced file change detected: {e.ChangeType} - {e.FilePath}");
            }
            catch (Exception ex)
            {
                LogError($"Error handling advanced file change: {ex.Message}");
            }
        }

        private void OnAdvancedWatcherError(object sender, WatcherErrorEventArgs e)
        {
            LogError($"Advanced watcher error: {e.Exception.Message}");
        }

        private void OnAdvancedHealthCheck(object sender, WatcherHealthEventArgs e)
        {
            // Log health check results
            LogInfo($"Advanced watcher health check: {e.HealthStatus}");
        }

        private void OnReloadManagerReloadCompleted(object sender, ConfigurationReloadEventArgs e)
        {
            try
            {
                // Update current configuration if reload was successful
                if (e.IsSuccess)
                {
                    lock (_configLock)
                    {
                        _currentConfiguration = e.NewConfiguration;
                        _lastSuccessfulReload = DateTime.UtcNow;
                    }

                    // Raise the original configuration changed event
                    OnConfigurationChanged(e);
                }

                LogInfo($"Reload manager operation completed: {e.IsSuccess}");
            }
            catch (Exception ex)
            {
                LogError($"Error handling reload manager completion: {ex.Message}");
            }
        }

        private void OnReloadManagerBackupCompleted(object sender, ConfigurationBackupEventArgs e)
        {
            try
            {
                ConfigurationBackupCompleted?.Invoke(this, e);
                LogInfo($"Backup operation completed: {e.OperationType} - {e.IsSuccess}");
            }
            catch (Exception ex)
            {
                LogError($"Error handling backup completion: {ex.Message}");
            }
        }

        private void OnReloadManagerOperationStarted(object sender, ReloadOperationStartedEventArgs e)
        {
            LogInfo($"Reload operation started: {e.Operation.Id}");
        }

        private void OnReloadManagerOperationFailed(object sender, ReloadOperationFailedEventArgs e)
        {
            LogError($"Reload operation failed: {e.Operation.Id} - {e.ErrorMessage}");
        }

        private void OnHealthMonitorCheckCompleted(object sender, ConfigurationHealthEventArgs e)
        {
            try
            {
                ConfigurationHealthChanged?.Invoke(this, e);
                LogInfo($"Health monitor check completed: {e.HealthStatus}");
            }
            catch (Exception ex)
            {
                LogError($"Error handling health monitor completion: {ex.Message}");
            }
        }

        private void OnHealthMonitorAlertRaised(object sender, ConfigurationHealthAlertEventArgs e)
        {
            LogWarning($"Health alert raised: {e.AlertLevel} - {string.Join(", ", e.Issues)}");
        }

        private void OnHealthMonitorDiagnosticCompleted(object sender, ConfigurationDiagnosticEventArgs e)
        {
            LogInfo($"Health monitor diagnostic completed: {e.Diagnostic.Id} - {e.Diagnostic.IsSuccess}");
        }

        /// <summary>
        /// Disposes the configuration manager
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the configuration manager
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Disable advanced features first
                    DisableAdvancedFeatures();

                    // Dispose managed resources
                    _configWatcher?.Dispose();
                    _reloadTimer?.Dispose();
                    _advancedWatcher?.Dispose();
                    _reloadManager?.Dispose();
                    _healthMonitor?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ConfigurationManager()
        {
            Dispose(false);
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
        /// Initializes a new instance of the ConfigurationReloadEventArgs class
        /// </summary>
        public ConfigurationReloadEventArgs(LicenseReleaseServiceSection oldConfig, LicenseReleaseServiceSection newConfig, List<string> validationErrors)
        {
            OldConfiguration = oldConfig;
            NewConfiguration = newConfig;
            ValidationErrors = validationErrors ?? new List<string>();
        }
    }
}