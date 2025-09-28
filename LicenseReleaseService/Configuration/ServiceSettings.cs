using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading;
using GlobalConfig = System.Configuration.ConfigurationManager;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Strongly-typed wrapper for service configuration settings
    /// </summary>
    public class ServiceSettings
    {
        private static readonly object _lock = new object();
        private readonly ConfigurationManager _configurationManager;
        private DateTime _lastReloadTime;
        private Dictionary<string, string> _appSettingsCache;

        /// <summary>
        /// Initializes a new instance of the ServiceSettings class
        /// </summary>
        /// <param name="configurationManager">The configuration manager instance</param>
        public ServiceSettings(ConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _appSettingsCache = new Dictionary<string, string>();
            _lastReloadTime = DateTime.MinValue;
            LoadAppSettings();
        }

        #region Service Configuration

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => GetAppSetting("ServiceName", "LicenseReleaseService");

        /// <summary>
        /// Gets the environment (Production, Staging, Development)
        /// </summary>
        public string Environment => GetAppSetting("Environment", "Production");

        /// <summary>
        /// Gets the service version
        /// </summary>
        public string Version => GetAppSetting("Version", "1.0.0");

        /// <summary>
        /// Gets whether advanced features are enabled
        /// </summary>
        public bool EnableAdvancedFeatures => GetAppSetting("EnableAdvancedFeatures", false);

        /// <summary>
        /// Gets the polling interval in seconds
        /// </summary>
        public int PollingInterval => GetAppSetting("PollingInterval", 300);

        /// <summary>
        /// Gets the maximum concurrent operations
        /// </summary>
        public int MaxConcurrentOperations => GetAppSetting("MaxConcurrentOperations", 10);

        #endregion

        #region Service Mode Configuration

        /// <summary>
        /// Gets the service mode (Automatic, Manual)
        /// </summary>
        public string ServiceMode => GetAppSetting("ServiceMode", "Automatic");

        /// <summary>
        /// Gets the startup delay in seconds
        /// </summary>
        public int StartupDelay => GetAppSetting("StartupDelay", 10);

        /// <summary>
        /// Gets the shutdown timeout in seconds
        /// </summary>
        public int ShutdownTimeout => GetAppSetting("ShutdownTimeout", 30);

        #endregion

        #region Debug and Development Settings

        /// <summary>
        /// Gets whether debug mode is enabled
        /// </summary>
        public bool DebugMode => GetAppSetting("DebugMode", false);

        /// <summary>
        /// Gets whether diagnostics are enabled
        /// </summary>
        public bool EnableDiagnostics => GetAppSetting("EnableDiagnostics", true);

        /// <summary>
        /// Gets whether configuration changes should be logged
        /// </summary>
        public bool LogConfigurationChanges => GetAppSetting("LogConfigurationChanges", true);

        #endregion

        #region File Paths and Directories

        /// <summary>
        /// Gets the configuration backup path
        /// </summary>
        public string ConfigBackupPath => GetAppSetting("ConfigBackupPath", @"C:\ProgramData\LicenseReleaseService\Backup");

        /// <summary>
        /// Gets the temporary file path
        /// </summary>
        public string TempFilePath => GetAppSetting("TempFilePath", @"C:\ProgramData\LicenseReleaseService\Temp");

        /// <summary>
        /// Gets the working directory
        /// </summary>
        public string WorkingDirectory => GetAppSetting("WorkingDirectory", @"C:\ProgramData\LicenseReleaseService");

        #endregion

        #region Performance Settings

        /// <summary>
        /// Gets the minimum thread pool threads
        /// </summary>
        public int ThreadPoolMinThreads => GetAppSetting("ThreadPoolMinThreads", 2);

        /// <summary>
        /// Gets the maximum thread pool threads
        /// </summary>
        public int ThreadPoolMaxThreads => GetAppSetting("ThreadPoolMaxThreads", 10);

        /// <summary>
        /// Gets the cache timeout in seconds
        /// </summary>
        public int CacheTimeout => GetAppSetting("CacheTimeout", 300);

        #endregion

        #region Error Handling Settings

        /// <summary>
        /// Gets whether error recovery is enabled
        /// </summary>
        public bool EnableErrorRecovery => GetAppSetting("EnableErrorRecovery", true);

        /// <summary>
        /// Gets the maximum retry attempts
        /// </summary>
        public int MaxRetryAttempts => GetAppSetting("MaxRetryAttempts", 3);

        /// <summary>
        /// Gets the retry delay in milliseconds
        /// </summary>
        public int RetryDelay => GetAppSetting("RetryDelay", 5000);

        #endregion

        #region Event Logging Settings

        /// <summary>
        /// Gets the event log source name
        /// </summary>
        public string EventLogSource => GetAppSetting("EventLogSource", "LicenseReleaseService");

        /// <summary>
        /// Gets the event log name
        /// </summary>
        public string EventLogName => GetAppSetting("EventLogName", "Application");

        /// <summary>
        /// Gets whether event logging is enabled
        /// </summary>
        public bool EnableEventLogging => GetAppSetting("EnableEventLogging", true);

        #endregion

        #region License Manager Settings (via Configuration Section)

        /// <summary>
        /// Gets the lmutil.exe path
        /// </summary>
        public string LmutilPath => _configurationManager.CurrentConfiguration?.LicenseManager.LmutilPath ?? string.Empty;

        /// <summary>
        /// Gets the license server address
        /// </summary>
        public string LicenseServer => _configurationManager.CurrentConfiguration?.LicenseManager.LicenseServer ?? string.Empty;

        /// <summary>
        /// Gets the license server port
        /// </summary>
        public int LicenseServerPort => _configurationManager.CurrentConfiguration?.LicenseManager.Port ?? 27000;

        /// <summary>
        /// Gets the license manager timeout
        /// </summary>
        public int LicenseManagerTimeout => _configurationManager.CurrentConfiguration?.LicenseManager.Timeout ?? 30;

        /// <summary>
        /// Gets the license manager retry count
        /// </summary>
        public int LicenseManagerRetryCount => _configurationManager.CurrentConfiguration?.LicenseManager.RetryCount ?? 3;

        #endregion

        #region Logging Settings (via Configuration Section)

        /// <summary>
        /// Gets the log level
        /// </summary>
        public string LogLevel => _configurationManager.CurrentConfiguration?.Logging.LogLevel ?? "Information";

        /// <summary>
        /// Gets the log file path
        /// </summary>
        public string LogFilePath => _configurationManager.CurrentConfiguration?.Logging.LogFilePath ?? @"C:\Logs\LicenseReleaseService";

        /// <summary>
        /// Gets whether file logging is enabled
        /// </summary>
        public bool EnableFileLogging => _configurationManager.CurrentConfiguration?.Logging.EnableFileLogging ?? true;

        /// <summary>
        /// Gets whether event log logging is enabled
        /// </summary>
        public bool EnableEventLogLogging => _configurationManager.CurrentConfiguration?.Logging.EnableEventLog ?? true;

        /// <summary>
        /// Gets whether console logging is enabled
        /// </summary>
        public bool EnableConsoleLogging => _configurationManager.CurrentConfiguration?.Logging.EnableConsoleLogging ?? false;

        #endregion

        #region Monitoring Settings (via Configuration Section)

        /// <summary>
        /// Gets the health check interval in seconds
        /// </summary>
        public int HealthCheckInterval => _configurationManager.CurrentConfiguration?.Monitoring.HealthCheckInterval ?? 60;

        /// <summary>
        /// Gets whether performance counters are enabled
        /// </summary>
        public bool PerformanceCountersEnabled => _configurationManager.CurrentConfiguration?.Monitoring.PerformanceCountersEnabled ?? true;

        /// <summary>
        /// Gets whether metrics are enabled
        /// </summary>
        public bool EnableMetrics => _configurationManager.CurrentConfiguration?.Monitoring.EnableMetrics ?? true;

        /// <summary>
        /// Gets the metrics interval in seconds
        /// </summary>
        public int MetricsInterval => _configurationManager.CurrentConfiguration?.Monitoring.MetricsInterval ?? 30;

        #endregion

        #region License Query Engine Settings (via Configuration Section)

        /// <summary>
        /// Gets the license query engine configuration options
        /// </summary>
        public LicenseQueryOptions LicenseQueryOptions => _configurationManager.CurrentConfiguration?.LicenseQuery.ToLicenseQueryOptions() ?? new LicenseQueryOptions();

        /// <summary>
        /// Gets whether the license query engine is enabled
        /// </summary>
        public bool EnableLicenseQueryEngine => GetAppSetting("EnableLicenseQueryEngine", true);

        /// <summary>
        /// Gets the cache expiration time in seconds for license queries
        /// </summary>
        public int LicenseQueryCacheExpiration => GetAppSetting("LicenseQueryCacheExpiration", 300);

        /// <summary>
        /// Gets the query timeout in seconds for license queries
        /// </summary>
        public int LicenseQueryTimeout => GetAppSetting("LicenseQueryTimeout", 30);

        /// <summary>
        /// Gets the maximum cache size for license queries
        /// </summary>
        public int LicenseQueryMaxCacheSize => GetAppSetting("LicenseQueryMaxCacheSize", 1000);

        /// <summary>
        /// Gets the maximum concurrent queries for license queries
        /// </summary>
        public int LicenseQueryMaxConcurrentQueries => GetAppSetting("LicenseQueryMaxConcurrentQueries", 10);

        /// <summary>
        /// Gets whether caching is enabled for license queries
        /// </summary>
        public bool EnableLicenseQueryCaching => GetAppSetting("EnableLicenseQueryCaching", true);

        /// <summary>
        /// Gets whether statistics collection is enabled for license queries
        /// </summary>
        public bool EnableLicenseQueryStatistics => GetAppSetting("EnableLicenseQueryStatistics", true);

        /// <summary>
        /// Gets whether verbose output is enabled for license queries
        /// </summary>
        public bool EnableLicenseQueryVerboseOutput => GetAppSetting("EnableLicenseQueryVerboseOutput", false);

        /// <summary>
        /// Gets whether health monitoring is enabled for license queries
        /// </summary>
        public bool EnableLicenseQueryHealthMonitoring => GetAppSetting("EnableLicenseQueryHealthMonitoring", true);

        /// <summary>
        /// Gets whether performance metrics are enabled for license queries
        /// </summary>
        public bool EnableLicenseQueryPerformanceMetrics => GetAppSetting("EnableLicenseQueryPerformanceMetrics", true);

        /// <summary>
        /// Gets the alert threshold percentage for license queries
        /// </summary>
        public int LicenseQueryAlertThreshold => GetAppSetting("LicenseQueryAlertThreshold", 90);

        /// <summary>
        /// Gets the health check interval in seconds for license queries
        /// </summary>
        public int LicenseQueryHealthCheckInterval => GetAppSetting("LicenseQueryHealthCheckInterval", 60);

        /// <summary>
        /// Gets the performance metrics interval in seconds for license queries
        /// </summary>
        public int LicenseQueryPerformanceMetricsInterval => GetAppSetting("LicenseQueryPerformanceMetricsInterval", 30);

        /// <summary>
        /// Gets the cleanup interval in seconds for license queries
        /// </summary>
        public int LicenseQueryCleanupInterval => GetAppSetting("LicenseQueryCleanupInterval", 3600);

        #endregion

        #region Timer Execution Settings (via Configuration Section)

        /// <summary>
        /// Gets the timer execution configuration options
        /// </summary>
        public TimerExecution.TimerExecutionOptions TimerExecutionOptions => _configurationManager.CurrentConfiguration?.Timer.ToTimerExecutionOptions() ?? new TimerExecution.TimerExecutionOptions();

        /// <summary>
        /// Gets whether the timer execution system is enabled
        /// </summary>
        public bool EnableTimerExecution => GetAppSetting("EnableTimerExecution", true);

        /// <summary>
        /// Gets the timer startup delay in seconds
        /// </summary>
        public int TimerStartupDelay => GetAppSetting("TimerStartupDelay", 10);

        /// <summary>
        /// Gets the timer default interval in seconds
        /// </summary>
        public int TimerDefaultInterval => GetAppSetting("TimerDefaultInterval", 60);

        /// <summary>
        /// Gets the timer health check interval in seconds
        /// </summary>
        public int TimerHealthCheckInterval => GetAppSetting("TimerHealthCheckInterval", 60);

        /// <summary>
        /// Gets the timer metrics collection interval in seconds
        /// </summary>
        public int TimerMetricsCollectionInterval => GetAppSetting("TimerMetricsCollectionInterval", 30);

        /// <summary>
        /// Gets the timer configuration cache duration in seconds
        /// </summary>
        public int TimerConfigurationCacheDuration => GetAppSetting("TimerConfigurationCacheDuration", 300);

        /// <summary>
        /// Gets whether timer configuration file watching is enabled
        /// </summary>
        public bool EnableTimerConfigWatcher => GetAppSetting("EnableTimerConfigWatcher", true);

        /// <summary>
        /// Gets whether timer execution statistics are enabled
        /// </summary>
        public bool EnableTimerStatistics => GetAppSetting("EnableTimerStatistics", true);

        /// <summary>
        /// Gets whether timer execution metrics are enabled
        /// </summary>
        public bool EnableTimerMetrics => GetAppSetting("EnableTimerMetrics", true);

        /// <summary>
        /// Gets whether timer adaptive scheduling is enabled
        /// </summary>
        public bool EnableTimerAdaptiveScheduling => GetAppSetting("EnableTimerAdaptiveScheduling", false);

        /// <summary>
        /// Gets the timer memory monitoring threshold in bytes
        /// </summary>
        public long TimerMemoryThreshold => GetAppSetting("TimerMemoryThreshold", 52428800L);

        /// <summary>
        /// Gets the timer CPU monitoring threshold percentage
        /// </summary>
        public int TimerCpuThreshold => GetAppSetting("TimerCpuThreshold", 80);

        /// <summary>
        /// Gets the timer thread pool monitoring threshold
        /// </summary>
        public int TimerThreadPoolThreshold => GetAppSetting("TimerThreadPoolThreshold", 10);

        /// <summary>
        /// Gets the timer maximum execution history size
        /// </summary>
        public int TimerMaxExecutionHistory => GetAppSetting("TimerMaxExecutionHistory", 100);

        /// <summary>
        /// Gets whether timer execution overlap prevention is enabled
        /// </summary>
        public bool PreventTimerExecutionOverlap => GetAppSetting("PreventTimerExecutionOverlap", true);

        /// <summary>
        /// Gets whether timer circuit breaker is enabled
        /// </summary>
        public bool EnableTimerCircuitBreaker => GetAppSetting("EnableTimerCircuitBreaker", true);

        /// <summary>
        /// Gets the timer circuit breaker cooldown in seconds
        /// </summary>
        public int TimerCircuitBreakerCooldown => GetAppSetting("TimerCircuitBreakerCooldown", 300);

        /// <summary>
        /// Gets the timer maximum consecutive errors before circuit breaker
        /// </summary>
        public int TimerMaxConsecutiveErrors => GetAppSetting("TimerMaxConsecutiveErrors", 5);

        /// <summary>
        /// Gets whether timer auto restart is enabled
        /// </summary>
        public bool EnableTimerAutoRestart => GetAppSetting("EnableTimerAutoRestart", true);

        /// <summary>
        /// Gets whether timer execution timeout is enabled
        /// </summary>
        public bool EnableTimerExecutionTimeout => GetAppSetting("EnableTimerExecutionTimeout", true);

        /// <summary>
        /// Gets the timer execution timeout in seconds
        /// </summary>
        public int TimerExecutionTimeout => GetAppSetting("TimerExecutionTimeout", 300);

        /// <summary>
        /// Gets whether timer detailed logging is enabled
        /// </summary>
        public bool EnableTimerDetailedLogging => GetAppSetting("EnableTimerDetailedLogging", true);

        /// <summary>
        /// Gets whether timer stops on unhandled exceptions
        /// </summary>
        public bool TimerStopOnUnhandledException => GetAppSetting("TimerStopOnUnhandledException", false);

        /// <summary>
        /// Gets the timer disposal grace period in seconds
        /// </summary>
        public int TimerDisposalGracePeriod => GetAppSetting("TimerDisposalGracePeriod", 5);

        /// <summary>
        /// Gets the timer synchronization timeout in seconds
        /// </summary>
        public int TimerSyncTimeout => GetAppSetting("TimerSyncTimeout", 30);

        /// <summary>
        /// Gets the timer minimum interval in seconds
        /// </summary>
        public int TimerMinInterval => GetAppSetting("TimerMinInterval", 1);

        /// <summary>
        /// Gets the timer maximum interval in seconds
        /// </summary>
        public int TimerMaxInterval => GetAppSetting("TimerMaxInterval", 86400);

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets an application setting value with a default fallback
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The setting value</returns>
        private string GetAppSetting(string key, string defaultValue)
        {
            lock (_lock)
            {
                if (_appSettingsCache.TryGetValue(key, out var cachedValue))
                {
                    return cachedValue;
                }

                var value = ConfigurationManager.AppSettings[key] ?? defaultValue;
                _appSettingsCache[key] = value;
                return value;
            }
        }

        /// <summary>
        /// Gets an application setting value with a default fallback
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The setting value</returns>
        private int GetAppSetting(string key, int defaultValue)
        {
            if (int.TryParse(GetAppSetting(key, defaultValue.ToString()), out var result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets an application setting value with a default fallback
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The setting value</returns>
        private bool GetAppSetting(string key, bool defaultValue)
        {
            if (bool.TryParse(GetAppSetting(key, defaultValue.ToString()), out var result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets an application setting value with a default fallback
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The setting value</returns>
        private long GetAppSetting(string key, long defaultValue)
        {
            if (long.TryParse(GetAppSetting(key, defaultValue.ToString()), out var result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Loads application settings into cache
        /// </summary>
        private void LoadAppSettings()
        {
            lock (_lock)
            {
                _appSettingsCache.Clear();
                foreach (string key in GlobalConfig.AppSettings.Keys)
                {
                    _appSettingsCache[key] = GlobalConfig.AppSettings[key];
                }
                _lastReloadTime = DateTime.Now;
            }
        }

        /// <summary>
        /// Reloads the application settings cache
        /// </summary>
        public void ReloadAppSettings()
        {
            lock (_lock)
            {
                LoadAppSettings();
            }
        }

        /// <summary>
        /// Gets the last reload time
        /// </summary>
        public DateTime LastReloadTime
        {
            get
            {
                lock (_lock)
                {
                    return _lastReloadTime;
                }
            }
        }

        /// <summary>
        /// Gets all application settings
        /// </summary>
        public Dictionary<string, string> GetAllAppSettings()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(_appSettingsCache);
            }
        }

        /// <summary>
        /// Gets an application setting value
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <returns>The setting value or null if not found</returns>
        public string GetAppSetting(string key)
        {
            lock (_lock)
            {
                return _appSettingsCache.TryGetValue(key, out var value) ? value : null;
            }
        }

        /// <summary>
        /// Checks if an application setting exists
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <returns>True if the setting exists</returns>
        public bool HasAppSetting(string key)
        {
            lock (_lock)
            {
                return _appSettingsCache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Gets a connection string
        /// </summary>
        /// <param name="name">The connection string name</param>
        /// <returns>The connection string or null if not found</returns>
        public string GetConnectionString(string name)
        {
            var connectionString = GlobalConfig.ConnectionStrings[name];
            return connectionString?.ConnectionString;
        }

        /// <summary>
        /// Gets all connection strings
        /// </summary>
        public Dictionary<string, string> GetAllConnectionStrings()
        {
            var result = new Dictionary<string, string>();
            foreach (ConnectionStringSettings connectionString in GlobalConfig.ConnectionStrings)
            {
                result[connectionString.Name] = connectionString.ConnectionString;
            }
            return result;
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate app settings
                if (string.IsNullOrWhiteSpace(ServiceName))
                {
                    errors.Add("ServiceName cannot be empty");
                }

                if (string.IsNullOrWhiteSpace(Environment))
                {
                    errors.Add("Environment cannot be empty");
                }

                if (PollingInterval < 1)
                {
                    errors.Add("PollingInterval must be greater than 0");
                }

                if (MaxConcurrentOperations < 1)
                {
                    errors.Add("MaxConcurrentOperations must be greater than 0");
                }

                // Validate file paths
                if (!string.IsNullOrWhiteSpace(ConfigBackupPath))
                {
                    try
                    {
                        Directory.CreateDirectory(ConfigBackupPath);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Cannot create backup directory: {ex.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(TempFilePath))
                {
                    try
                    {
                        Directory.CreateDirectory(TempFilePath);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Cannot create temp directory: {ex.Message}");
                    }
                }

                // Validate configuration section
                if (_configurationManager.CurrentConfiguration != null)
                {
                    errors.AddRange(_configurationManager.CurrentConfiguration.Validate());
                }
            }
            catch (Exception ex)
            {
                errors.Add($"ServiceSettings validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the service settings
        /// </summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ServiceSettings Configuration:");
            sb.AppendLine($"  ServiceName: {ServiceName}");
            sb.AppendLine($"  Environment: {Environment}");
            sb.AppendLine($"  Version: {Version}");
            sb.AppendLine($"  PollingInterval: {PollingInterval}s");
            sb.AppendLine($"  MaxConcurrentOperations: {MaxConcurrentOperations}");
            sb.AppendLine($"  DebugMode: {DebugMode}");
            sb.AppendLine($"  LogLevel: {LogLevel}");
            sb.AppendLine($"  LogPath: {LogFilePath}");
            sb.AppendLine($"  LicenseServer: {LicenseServer}:{LicenseServerPort}");
            sb.AppendLine($"  HealthCheckInterval: {HealthCheckInterval}s");
            sb.AppendLine($"  LastReload: {_lastReloadTime:yyyy-MM-dd HH:mm:ss}");
            return sb.ToString();
        }

        #endregion
    }
}