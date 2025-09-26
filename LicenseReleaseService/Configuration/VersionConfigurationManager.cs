using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Manages version-specific configuration operations including loading, validation, and inheritance
    /// </summary>
    public class VersionConfigurationManager : IDisposable
    {
        private readonly ILogger<VersionConfigurationManager> _logger;
        private readonly ConfigurationManager _configManager;
        private readonly object _lock = new object();
        private readonly Timer _refreshTimer;
        private readonly Timer _cleanupTimer;
        private readonly Dictionary<string, VersionConfigurationElement> _configCache;
        private readonly Dictionary<string, DateTime> _cacheTimestamps;
        private readonly Dictionary<string, VersionHealthStatus> _versionHealthStatus;
        private readonly Dictionary<string, List<string>> _versionDependencies;
        private readonly Dictionary<string, DateTime> _lastValidationResults;
        private bool _isDisposed;
        private bool _isInitialized;
        private TimeSpan _cacheExpiration;
        private TimeSpan _refreshInterval;
        private TimeSpan _cleanupInterval;
        private VersionConfigurationSection _currentConfig;
        private DateTime _lastRefreshTime;
        private DateTime _lastCleanupTime;

        /// <summary>
        /// Event raised when version configuration is reloaded
        /// </summary>
        public event EventHandler<VersionConfigurationReloadEventArgs> ConfigurationReloaded;

        /// <summary>
        /// Event raised when version health status changes
        /// </summary>
        public event EventHandler<VersionHealthChangedEventArgs> VersionHealthChanged;

        /// <summary>
        /// Event raised when cache is refreshed
        /// </summary>
        public event EventHandler<CacheRefreshEventArgs> CacheRefreshed;

        /// <summary>
        /// Gets the current version configuration
        /// </summary>
        public VersionConfigurationSection CurrentConfiguration
        {
            get
            {
                lock (_lock)
                {
                    return _currentConfig ?? LoadConfigurationInternal();
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
        /// Gets the last configuration refresh time
        /// </summary>
        public DateTime LastRefreshTime => _lastRefreshTime;

        /// <summary>
        /// Gets the number of cached configurations
        /// </summary>
        public int CachedConfigurationCount => _configCache.Count;

        /// <summary>
        /// Gets the list of monitored versions
        /// </summary>
        public IReadOnlyList<string> MonitoredVersions
        {
            get
            {
                lock (_lock)
                {
                    return _configCache.Keys.ToList().AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the version health status for all monitored versions
        /// </summary>
        public IReadOnlyDictionary<string, VersionHealthStatus> VersionHealthStatus
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, VersionHealthStatus>(_versionHealthStatus);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the VersionConfigurationManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configManager">Configuration manager instance</param>
        public VersionConfigurationManager(ILogger<VersionConfigurationManager> logger, ConfigurationManager configManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));

            _configCache = new Dictionary<string, VersionConfigurationElement>();
            _cacheTimestamps = new Dictionary<string, DateTime>();
            _versionHealthStatus = new Dictionary<string, VersionHealthStatus>();
            _versionDependencies = new Dictionary<string, List<string>>();
            _lastValidationResults = new Dictionary<string, DateTime>();

            // Initialize with default values
            _cacheExpiration = TimeSpan.FromMinutes(5);
            _refreshInterval = TimeSpan.FromMinutes(1);
            _cleanupInterval = TimeSpan.FromHours(1);

            // Set up timers
            _refreshTimer = new Timer(RefreshTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer = new Timer(CleanupTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            _lastRefreshTime = DateTime.UtcNow;
            _lastCleanupTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes the version configuration manager
        /// </summary>
        /// <param name="cacheExpiration">Cache expiration time</param>
        /// <param name="refreshInterval">Refresh interval</param>
        /// <param name="cleanupInterval">Cleanup interval</param>
        /// <param name="autoStartTimers">Whether to start timers automatically</param>
        public async Task InitializeAsync(TimeSpan cacheExpiration, TimeSpan refreshInterval, TimeSpan cleanupInterval, bool autoStartTimers = true)
        {
            lock (_lock)
            {
                if (_isInitialized)
                    return;

                _cacheExpiration = cacheExpiration;
                _refreshInterval = refreshInterval;
                _cleanupInterval = cleanupInterval;
            }

            _logger.LogInformation("Initializing VersionConfigurationManager with CacheExpiration={CacheExpiration}, RefreshInterval={RefreshInterval}, CleanupInterval={CleanupInterval}",
                cacheExpiration, refreshInterval, cleanupInterval);

            try
            {
                // Load initial configuration
                await ReloadConfigurationAsync();

                // Start timers if requested
                if (autoStartTimers)
                {
                    StartTimers();
                }

                _isInitialized = true;
                _logger.LogInformation("VersionConfigurationManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize VersionConfigurationManager");
                throw;
            }
        }

        /// <summary>
        /// Starts the background timers
        /// </summary>
        public void StartTimers()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(VersionConfigurationManager));

                _refreshTimer.Change(_refreshInterval, _refreshInterval);
                _cleanupTimer.Change(_cleanupInterval, _cleanupInterval);

                _logger.LogInformation("Version configuration timers started");
            }
        }

        /// <summary>
        /// Stops the background timers
        /// </summary>
        public void StopTimers()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);

                _logger.LogInformation("Version configuration timers stopped");
            }
        }

        /// <summary>
        /// Gets the configuration for a specific version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="useCache">Whether to use cached configuration</param>
        /// <returns>Version configuration element or null if not found</returns>
        public VersionConfigurationElement GetVersionConfiguration(string version, bool useCache = true)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            lock (_lock)
            {
                // Check cache first
                if (useCache && _configCache.TryGetValue(version, out var cachedConfig))
                {
                    if (DateTime.UtcNow - _cacheTimestamps[version] < _cacheExpiration)
                    {
                        _logger.LogDebug("Retrieved version {Version} configuration from cache", version);
                        return cachedConfig;
                    }
                    else
                    {
                        // Cache expired, remove it
                        _configCache.Remove(version);
                        _cacheTimestamps.Remove(version);
                        _logger.LogDebug("Cache expired for version {Version}", version);
                    }
                }

                // Load configuration
                var config = LoadVersionConfigurationInternal(version);
                if (config != null)
                {
                    // Cache the configuration
                    _configCache[version] = config;
                    _cacheTimestamps[version] = DateTime.UtcNow;
                    _logger.LogDebug("Loaded and cached version {Version} configuration", version);
                }

                return config;
            }
        }

        /// <summary>
        /// Gets the configuration for a specific version with inheritance applied
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>Version configuration element with inheritance applied</returns>
        public VersionConfigurationElement GetVersionConfigurationWithInheritance(string version)
        {
            var config = GetVersionConfiguration(version);
            if (config == null)
                return null;

            if (!_currentConfig.EnableVersionInheritance || string.IsNullOrWhiteSpace(config.InheritFrom))
                return config;

            var parentConfig = GetVersionConfigurationWithInheritance(config.InheritFrom);
            return config.MergeWith(parentConfig);
        }

        /// <summary>
        /// Gets the best available configuration for a version (with fallback)
        /// </summary>
        /// <param name="requestedVersion">Requested version</param>
        /// <returns>Best available version configuration</returns>
        public VersionConfigurationElement GetBestAvailableConfiguration(string requestedVersion)
        {
            if (string.IsNullOrWhiteSpace(requestedVersion))
                return GetDefaultVersionConfiguration();

            // Try exact match first
            var config = GetVersionConfigurationWithInheritance(requestedVersion);
            if (config != null && config.Enabled)
            {
                _logger.LogDebug("Found exact match for version {Version}", requestedVersion);
                return config;
            }

            // Try fallback strategies
            if (_currentConfig.FallbackToDefault)
            {
                var defaultConfig = GetDefaultVersionConfiguration();
                if (defaultConfig != null && defaultConfig.Enabled)
                {
                    _logger.LogDebug("Using default configuration for version {Version}", requestedVersion);
                    return defaultConfig;
                }
            }

            // Find closest enabled version
            var closestVersion = FindClosestEnabledVersion(requestedVersion);
            if (closestVersion != null)
            {
                var closestConfig = GetVersionConfigurationWithInheritance(closestVersion);
                _logger.LogDebug("Using closest version {ClosestVersion} for requested version {Version}", closestVersion, requestedVersion);
                return closestConfig;
            }

            _logger.LogWarning("No suitable configuration found for version {Version}", requestedVersion);
            return null;
        }

        /// <summary>
        /// Gets the default version configuration
        /// </summary>
        /// <returns>Default version configuration or null if not set</returns>
        public VersionConfigurationElement GetDefaultVersionConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_currentConfig.DefaultVersion))
                return null;

            return GetVersionConfigurationWithInheritance(_currentConfig.DefaultVersion);
        }

        /// <summary>
        /// Gets all enabled version configurations
        /// </summary>
        /// <returns>List of enabled version configurations</returns>
        public List<VersionConfigurationElement> GetEnabledConfigurations()
        {
            lock (_lock)
            {
                return _currentConfig.Versions.GetEnabledConfigurations();
            }
        }

        /// <summary>
        /// Gets version configurations sorted by priority
        /// </summary>
        /// <returns>List of version configurations sorted by priority</returns>
        public List<VersionConfigurationElement> GetSortedConfigurations()
        {
            lock (_lock)
            {
                return _currentConfig.Versions.GetSortedConfigurations();
            }
        }

        /// <summary>
        /// Gets configurations for versions in a specific range
        /// </summary>
        /// <param name="minVersion">Minimum version (inclusive)</param>
        /// <param name="maxVersion">Maximum version (inclusive)</param>
        /// <returns>List of version configurations in the specified range</returns>
        public List<VersionConfigurationElement> GetConfigurationsInRange(string minVersion, string maxVersion)
        {
            lock (_lock)
            {
                return _currentConfig.Versions.GetConfigurationsInRange(minVersion, maxVersion);
            }
        }

        /// <summary>
        /// Validates a specific version configuration
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>Validation result</returns>
        public async Task<VersionValidationResult> ValidateVersionConfigurationAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new VersionValidationResult { Version = version, Errors = { "Version cannot be null or empty" } };

            try
            {
                _logger.LogDebug("Validating version {Version} configuration", version);

                var config = GetVersionConfigurationWithInheritance(version);
                if (config == null)
                {
                    return new VersionValidationResult { Version = version, Errors = { $"Configuration not found for version {version}" } };
                }

                var errors = config.Validate();
                var result = new VersionValidationResult
                {
                    Version = version,
                    Errors = errors,
                    Warnings = new List<string>(),
                    IsValid = errors.Count == 0
                };

                // Additional validation checks
                await PerformAdditionalValidationAsync(config, result);

                // Cache validation result
                lock (_lock)
                {
                    _lastValidationResults[version] = DateTime.UtcNow;
                }

                _logger.LogDebug("Version {Version} validation completed. Errors: {ErrorCount}, Warnings: {WarningCount}",
                    version, result.Errors.Count, result.Warnings.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating version {Version} configuration", version);
                return new VersionValidationResult { Version = version, Errors = { $"Validation error: {ex.Message}" } };
            }
        }

        /// <summary>
        /// Validates all version configurations
        /// </summary>
        /// <returns>Dictionary of version validation results</returns>
        public async Task<Dictionary<string, VersionValidationResult>> ValidateAllConfigurationsAsync()
        {
            _logger.LogInformation("Starting validation of all version configurations");

            var results = new Dictionary<string, VersionValidationResult>();
            var enabledVersions = GetEnabledVersions();

            var validationTasks = enabledVersions.Select(async version =>
            {
                var result = await ValidateVersionConfigurationAsync(version);
                return new { Version = version, Result = result };
            });

            var validationResults = await Task.WhenAll(validationTasks);

            foreach (var validation in validationResults)
            {
                results[validation.Version] = validation.Result;
            }

            _logger.LogInformation("Validation of all version configurations completed. Processed {Count} versions", results.Count);

            return results;
        }

        /// <summary>
        /// Reloads the configuration from the configuration file
        /// </summary>
        public async Task ReloadConfigurationAsync()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(VersionConfigurationManager));
            }

            try
            {
                _logger.LogInformation("Reloading version configuration");

                var oldConfig = _currentConfig;
                var newConfig = LoadConfigurationInternal();

                // Validate the new configuration
                var validationErrors = newConfig.Validate();
                if (validationErrors.Count > 0)
                {
                    var errorString = string.Join("; ", validationErrors);
                    _logger.LogError("Configuration validation failed: {ErrorString}", errorString);
                    throw new ConfigurationErrorsException($"Configuration validation failed: {errorString}");
                }

                // Apply the new configuration
                _currentConfig = newConfig;
                _lastRefreshTime = DateTime.UtcNow;

                // Clear cache on configuration reload
                ClearCache();

                // Update version health status
                UpdateVersionHealthStatus();

                // Raise configuration reloaded event
                OnConfigurationReloaded(new VersionConfigurationReloadEventArgs(oldConfig, newConfig, validationErrors));

                _logger.LogInformation("Version configuration reloaded successfully. {EnabledCount} enabled versions loaded",
                    newConfig.Versions.GetEnabledConfigurations().Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload version configuration");
                throw;
            }
        }

        /// <summary>
        /// Updates the health status for a specific version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="healthStatus">New health status</param>
        /// <param name="reason">Reason for health status change</param>
        public void UpdateVersionHealth(string version, VersionHealthStatus healthStatus, string reason = null)
        {
            if (string.IsNullOrWhiteSpace(version))
                return;

            lock (_lock)
            {
                var oldStatus = _versionHealthStatus.TryGetValue(version, out var currentStatus) ? currentStatus : VersionHealthStatus.Unknown;

                if (oldStatus != healthStatus)
                {
                    _versionHealthStatus[version] = healthStatus;
                    _logger.LogInformation("Version {Version} health status changed from {OldStatus} to {NewStatus}: {Reason}",
                        version, oldStatus, healthStatus, reason ?? "No reason provided");

                    OnVersionHealthChanged(new VersionHealthChangedEventArgs(version, oldStatus, healthStatus, reason));
                }
            }
        }

        /// <summary>
        /// Refreshes the configuration cache
        /// </summary>
        public void RefreshCache()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                var removedCount = _configCache.Count;
                _configCache.Clear();
                _cacheTimestamps.Clear();

                _logger.LogDebug("Configuration cache refreshed. Removed {Count} cached configurations", removedCount);

                OnCacheRefreshed(new CacheRefreshEventArgs(removedCount, 0));
            }
        }

        /// <summary>
        /// Clears the configuration cache
        /// </summary>
        public void ClearCache()
        {
            lock (_lock)
            {
                var removedCount = _configCache.Count;
                _configCache.Clear();
                _cacheTimestamps.Clear();

                _logger.LogDebug("Configuration cache cleared. Removed {Count} cached configurations", removedCount);
            }
        }

        /// <summary>
        /// Gets the cache statistics
        /// </summary>
        /// <returns>Cache statistics</returns>
        public CacheStatistics GetCacheStatistics()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var expiredEntries = _cacheTimestamps.Count(kvp => now - kvp.Value > _cacheExpiration);

                return new CacheStatistics
                {
                    TotalCachedEntries = _configCache.Count,
                    ExpiredEntries = expiredEntries,
                    CacheExpiration = _cacheExpiration,
                    LastRefreshTime = _lastRefreshTime,
                    CacheHitRate = CalculateCacheHitRate()
                };
            }
        }

        /// <summary>
        /// Gets the list of enabled versions
        /// </summary>
        /// <returns>List of enabled version identifiers</returns>
        public List<string> GetEnabledVersions()
        {
            lock (_lock)
            {
                return _currentConfig.GetEnabledVersions();
            }
        }

        /// <summary>
        /// Gets the list of available versions
        /// </summary>
        /// <returns>List of available version identifiers</returns>
        public List<string> GetAvailableVersions()
        {
            lock (_lock)
            {
                return _currentConfig.GetAvailableVersions();
            }
        }

        /// <summary>
        /// Finds the closest enabled version to the requested version
        /// </summary>
        /// <param name="requestedVersion">Requested version</param>
        /// <returns>Closest enabled version or null if not found</returns>
        private string FindClosestEnabledVersion(string requestedVersion)
        {
            if (!int.TryParse(requestedVersion, out int requestedYear))
                return null;

            var enabledVersions = GetEnabledVersions()
                .Where(v => int.TryParse(v, out int year) && year != requestedYear)
                .Select(v => new { Version = v, Year = int.Parse(v) })
                .OrderBy(v => Math.Abs(v.Year - requestedYear))
                .ToList();

            return enabledVersions.FirstOrDefault()?.Version;
        }

        /// <summary>
        /// Loads the configuration from the configuration file
        /// </summary>
        /// <returns>Loaded configuration</returns>
        private VersionConfigurationSection LoadConfigurationInternal()
        {
            try
            {
                // Refresh configuration to get latest changes
                System.Configuration.ConfigurationManager.RefreshSection("versionConfiguration");

                var config = (VersionConfigurationSection)System.Configuration.ConfigurationManager.GetSection("versionConfiguration");
                if (config == null)
                {
                    _logger.LogWarning("versionConfiguration section not found in configuration file, using empty configuration");
                    return new VersionConfigurationSection();
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load version configuration");
                throw new ConfigurationErrorsException($"Failed to load version configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads configuration for a specific version
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <returns>Version configuration element or null if not found</returns>
        private VersionConfigurationElement LoadVersionConfigurationInternal(string version)
        {
            try
            {
                return _currentConfig.GetVersionConfigurationWithInheritance(version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration for version {Version}", version);
                return null;
            }
        }

        /// <summary>
        /// Performs additional validation on a version configuration
        /// </summary>
        /// <param name="config">Configuration to validate</param>
        /// <param name="result">Validation result to update</param>
        private async Task PerformAdditionalValidationAsync(VersionConfigurationElement config, VersionValidationResult result)
        {
            // Validate network connectivity to license server
            if (!string.IsNullOrWhiteSpace(config.LicenseServer) && config.EnableHealthCheck)
            {
                if (!await IsLicenseServerAccessibleAsync(config.LicenseServer, config.Port))
                {
                    result.Warnings.Add($"License server {config.LicenseServer}:{config.Port} is not accessible");
                }
            }

            // Validate feature codes format
            var featureCodes = config.ParsedFeatureCodes;
            foreach (var featureCode in featureCodes)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(featureCode, @"^[A-Za-z0-9_\-]+$"))
                {
                    result.Warnings.Add($"Invalid feature code format: {featureCode}");
                }
            }

            // Validate timeout reasonableness
            if (config.Timeout > 300)
            {
                result.Warnings.Add($"Timeout value {config.Timeout} seconds is unusually high");
            }

            if (config.CommandTimeout > 600)
            {
                result.Warnings.Add($"Command timeout value {config.CommandTimeout} seconds is unusually high");
            }
        }

        /// <summary>
        /// Checks if a license server is accessible
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>True if accessible, false otherwise</returns>
        private async Task<bool> IsLicenseServerAccessibleAsync(string server, int port)
        {
            try
            {
                using (var tcpClient = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(server, port);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    return completedTask == connectTask;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates version health status for all versions
        /// </summary>
        private void UpdateVersionHealthStatus()
        {
            lock (_lock)
            {
                var enabledVersions = GetEnabledVersions();
                foreach (var version in enabledVersions)
                {
                    if (!_versionHealthStatus.ContainsKey(version))
                    {
                        _versionHealthStatus[version] = VersionHealthStatus.Unknown;
                    }
                }

                // Remove health status for versions that are no longer enabled
                var versionsToRemove = _versionHealthStatus.Keys
                    .Except(enabledVersions)
                    .ToList();

                foreach (var version in versionsToRemove)
                {
                    _versionHealthStatus.Remove(version);
                }
            }
        }

        /// <summary>
        /// Calculates cache hit rate
        /// </summary>
        /// <returns>Cache hit rate (0.0-1.0)</returns>
        private double CalculateCacheHitRate()
        {
            // This is a simplified implementation
            // In a real implementation, you would track cache hits and misses
            return 0.8; // Placeholder
        }

        /// <summary>
        /// Handles the refresh timer callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private async void RefreshTimerCallback(object state)
        {
            try
            {
                await ReloadConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during configuration refresh timer callback");
            }
        }

        /// <summary>
        /// Handles the cleanup timer callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private void CleanupTimerCallback(object state)
        {
            try
            {
                PerformCleanup();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup timer callback");
            }
        }

        /// <summary>
        /// Performs cleanup operations
        /// </summary>
        private void PerformCleanup()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                var now = DateTime.UtcNow;
                var removedCount = 0;

                // Clean expired cache entries
                var expiredKeys = _cacheTimestamps
                    .Where(kvp => now - kvp.Value > _cacheExpiration)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _configCache.Remove(key);
                    _cacheTimestamps.Remove(key);
                    removedCount++;
                }

                // Clean old validation results
                var oldValidationKeys = _lastValidationResults
                    .Where(kvp => now - kvp.Value > TimeSpan.FromHours(1))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldValidationKeys)
                {
                    _lastValidationResults.Remove(key);
                }

                _lastCleanupTime = now;

                if (removedCount > 0)
                {
                    _logger.LogDebug("Cleanup completed. Removed {Count} expired cache entries", removedCount);
                }
            }
        }

        /// <summary>
        /// Raises the ConfigurationReloaded event
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnConfigurationReloaded(VersionConfigurationReloadEventArgs e)
        {
            ConfigurationReloaded?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the VersionHealthChanged event
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnVersionHealthChanged(VersionHealthChangedEventArgs e)
        {
            VersionHealthChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the CacheRefreshed event
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnCacheRefreshed(CacheRefreshEventArgs e)
        {
            CacheRefreshed?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the version configuration manager
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the version configuration manager
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    StopTimers();
                    _refreshTimer?.Dispose();
                    _cleanupTimer?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~VersionConfigurationManager()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Event arguments for version configuration reload events
    /// </summary>
    public class VersionConfigurationReloadEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old configuration
        /// </summary>
        public VersionConfigurationSection OldConfiguration { get; }

        /// <summary>
        /// Gets the new configuration
        /// </summary>
        public VersionConfigurationSection NewConfiguration { get; }

        /// <summary>
        /// Gets the validation errors
        /// </summary>
        public List<string> ValidationErrors { get; }

        /// <summary>
        /// Gets whether the reload was successful
        /// </summary>
        public bool IsSuccess => ValidationErrors.Count == 0;

        /// <summary>
        /// Initializes a new instance of the VersionConfigurationReloadEventArgs class
        /// </summary>
        public VersionConfigurationReloadEventArgs(VersionConfigurationSection oldConfig, VersionConfigurationSection newConfig, List<string> validationErrors)
        {
            OldConfiguration = oldConfig;
            NewConfiguration = newConfig;
            ValidationErrors = validationErrors ?? new List<string>();
        }
    }

    /// <summary>
    /// Event arguments for version health change events
    /// </summary>
    public class VersionHealthChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the version identifier
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the old health status
        /// </summary>
        public VersionHealthStatus OldStatus { get; }

        /// <summary>
        /// Gets the new health status
        /// </summary>
        public VersionHealthStatus NewStatus { get; }

        /// <summary>
        /// Gets the reason for the health status change
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the VersionHealthChangedEventArgs class
        /// </summary>
        public VersionHealthChangedEventArgs(string version, VersionHealthStatus oldStatus, VersionHealthStatus newStatus, string reason)
        {
            Version = version;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Reason = reason;
        }
    }

    /// <summary>
    /// Event arguments for cache refresh events
    /// </summary>
    public class CacheRefreshEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the number of removed entries
        /// </summary>
        public int RemovedEntries { get; }

        /// <summary>
        /// Gets the number of added entries
        /// </summary>
        public int AddedEntries { get; }

        /// <summary>
        /// Initializes a new instance of the CacheRefreshEventArgs class
        /// </summary>
        public CacheRefreshEventArgs(int removedEntries, int addedEntries)
        {
            RemovedEntries = removedEntries;
            AddedEntries = addedEntries;
        }
    }

    /// <summary>
    /// Represents the health status of a version
    /// </summary>
    public enum VersionHealthStatus
    {
        /// <summary>
        /// Health status is unknown
        /// </summary>
        Unknown,

        /// <summary>
        /// Version is healthy
        /// </summary>
        Healthy,

        /// <summary>
        /// Version has warnings
        /// </summary>
        Warning,

        /// <summary>
        /// Version has errors
        /// </summary>
        Error,

        /// <summary>
        /// Version is unavailable
        /// </summary>
        Unavailable
    }

    /// <summary>
    /// Represents the result of validating a version configuration
    /// </summary>
    public class VersionValidationResult
    {
        /// <summary>
        /// Gets or sets the version that was validated
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets the list of validation errors
        /// </summary>
        public List<string> Errors { get; set; }

        /// <summary>
        /// Gets the list of validation warnings
        /// </summary>
        public List<string> Warnings { get; set; }

        /// <summary>
        /// Gets a value indicating whether validation passed
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets a value indicating whether validation has any issues
        /// </summary>
        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Initializes a new instance of the VersionValidationResult class
        /// </summary>
        public VersionValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }

    /// <summary>
    /// Represents cache statistics
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Gets or sets the total number of cached entries
        /// </summary>
        public int TotalCachedEntries { get; set; }

        /// <summary>
        /// Gets or sets the number of expired entries
        /// </summary>
        public int ExpiredEntries { get; set; }

        /// <summary>
        /// Gets or sets the cache expiration time
        /// </summary>
        public TimeSpan CacheExpiration { get; set; }

        /// <summary>
        /// Gets or sets the last refresh time
        /// </summary>
        public DateTime LastRefreshTime { get; set; }

        /// <summary>
        /// Gets or sets the cache hit rate (0.0-1.0)
        /// </summary>
        public double CacheHitRate { get; set; }
    }
}