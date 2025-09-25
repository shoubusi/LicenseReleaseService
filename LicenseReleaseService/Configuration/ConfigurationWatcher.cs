using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Advanced file system watcher for configuration files with debouncing and error handling
    /// </summary>
    public class ConfigurationWatcher : IDisposable
    {
        private readonly object _lock = new object();
        private readonly FileSystemWatcher _fileWatcher;
        private readonly Timer _debounceTimer;
        private readonly Timer _healthCheckTimer;
        private readonly List<FileSystemWatcher> _additionalWatchers;

        private string _configFilePath;
        private string _configDirectory;
        private string _configFileName;
        private DateTime _lastFileChange;
        private DateTime _lastSuccessfulRead;
        private bool _isWatching;
        private bool _isDisposed;
        private int _debounceInterval;
        private int _healthCheckInterval;
        private int _maxRetryAttempts;
        private int _currentRetryAttempts;

        // Event handlers
        public event ConfigurationEvents.ConfigurationFileChangedEventHandler FileChanged;
        public event EventHandler<WatcherErrorEventArgs> WatcherError;
        public event EventHandler<WatcherHealthEventArgs> HealthCheck;

        /// <summary>
        /// Gets whether the watcher is currently active
        /// </summary>
        public bool IsWatching
        {
            get
            {
                lock (_lock)
                {
                    return _isWatching;
                }
            }
        }

        /// <summary>
        /// Gets the configuration file path being watched
        /// </summary>
        public string ConfigFilePath => _configFilePath;

        /// <summary>
        /// Gets the last file change timestamp
        /// </summary>
        public DateTime LastFileChange => _lastFileChange;

        /// <summary>
        /// Gets the last successful read timestamp
        /// </summary>
        public DateTime LastSuccessfulRead => _lastSuccessfulRead;

        /// <summary>
        /// Gets the current retry attempts
        /// </summary>
        public int CurrentRetryAttempts => _currentRetryAttempts;

        /// <summary>
        /// Gets the watcher statistics
        /// </summary>
        public WatcherStatistics Statistics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationWatcher class
        /// </summary>
        public ConfigurationWatcher(string configFilePath, int debounceInterval = 1000, int healthCheckInterval = 30000, int maxRetryAttempts = 3)
        {
            _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));
            _configDirectory = Path.GetDirectoryName(configFilePath);
            _configFileName = Path.GetFileName(configFilePath);
            _debounceInterval = debounceInterval;
            _healthCheckInterval = healthCheckInterval;
            _maxRetryAttempts = maxRetryAttempts;
            _additionalWatchers = new List<FileSystemWatcher>();

            Statistics = new WatcherStatistics();

            // Initialize file system watcher
            _fileWatcher = new FileSystemWatcher(_configDirectory, _configFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime |
                             NotifyFilters.Size | NotifyFilters.Security | NotifyFilters.Attributes,
                EnableRaisingEvents = false
            };

            // Set up event handlers
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Renamed += OnFileRenamed;
            _fileWatcher.Error += OnWatcherError;

            // Initialize timers
            _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _healthCheckTimer = new Timer(OnHealthCheckTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

            // Initialize last change time
            if (File.Exists(_configFilePath))
            {
                _lastFileChange = File.GetLastWriteTime(_configFilePath);
                _lastSuccessfulRead = _lastFileChange;
            }
        }

        /// <summary>
        /// Starts watching for configuration file changes
        /// </summary>
        public void StartWatching()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationWatcher));

                if (_isWatching)
                    return;

                try
                {
                    // Ensure the configuration file exists
                    if (!File.Exists(_configFilePath))
                    {
                        throw new FileNotFoundException($"Configuration file not found: {_configFilePath}");
                    }

                    // Start the main file watcher
                    _fileWatcher.EnableRaisingEvents = true;

                    // Start health check timer
                    _healthCheckTimer.Change(_healthCheckInterval, _healthCheckInterval);

                    _isWatching = true;
                    Statistics.StartTime = DateTime.UtcNow;
                    Statistics.TotalRestarts++;

                    LogInfo("Configuration watcher started successfully");
                }
                catch (Exception ex)
                {
                    Statistics.TotalErrors++;
                    throw new ConfigurationWatcherException($"Failed to start configuration watcher: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Stops watching for configuration file changes
        /// </summary>
        public void StopWatching()
        {
            lock (_lock)
            {
                if (_isDisposed || !_isWatching)
                    return;

                try
                {
                    // Stop the main file watcher
                    _fileWatcher.EnableRaisingEvents = false;

                    // Stop additional watchers
                    foreach (var watcher in _additionalWatchers)
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                    _additionalWatchers.Clear();

                    // Stop timers
                    _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    _isWatching = false;
                    Statistics.StopTime = DateTime.UtcNow;

                    LogInfo("Configuration watcher stopped successfully");
                }
                catch (Exception ex)
                {
                    Statistics.TotalErrors++;
                    throw new ConfigurationWatcherException($"Failed to stop configuration watcher: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Restarts the configuration watcher
        /// </summary>
        public void RestartWatcher()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationWatcher));

                StopWatching();

                // Add a small delay before restarting
                Thread.Sleep(500);

                StartWatching();
            }
        }

        /// <summary>
        /// Adds an additional file path to watch
        /// </summary>
        public void AddWatchPath(string filePath)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationWatcher));

                if (string.IsNullOrEmpty(filePath))
                    throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                    throw new ArgumentException("Invalid file path", nameof(filePath));

                var additionalWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime |
                                 NotifyFilters.Size | NotifyFilters.Security,
                    EnableRaisingEvents = _isWatching
                };

                additionalWatcher.Changed += OnFileChanged;
                additionalWatcher.Created += OnFileCreated;
                additionalWatcher.Deleted += OnFileDeleted;
                additionalWatcher.Renamed += OnFileRenamed;
                additionalWatcher.Error += OnWatcherError;

                _additionalWatchers.Add(additionalWatcher);
                Statistics.AdditionalWatchersCount++;
            }
        }

        /// <summary>
        /// Forces a configuration file check
        /// </summary>
        public void ForceCheck()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationWatcher));

                PerformHealthCheck();
            }
        }

        /// <summary>
        /// Gets the current watcher health status
        /// </summary>
        public WatcherHealthStatus GetHealthStatus()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return WatcherHealthStatus.Disposed;

                if (!_isWatching)
                    return WatcherHealthStatus.Stopped;

                // Check if file is accessible
                try
                {
                    if (!File.Exists(_configFilePath))
                        return WatcherHealthStatus.FileNotFound;

                    // Try to read the file
                    using (var stream = File.OpenRead(_configFilePath))
                    {
                        // Just test access, don't read content
                    }

                    return _currentRetryAttempts > 0 ? WatcherHealthStatus.Degraded : WatcherHealthStatus.Healthy;
                }
                catch (Exception ex)
                {
                    LogError($"Health check failed: {ex.Message}");
                    return WatcherHealthStatus.Error;
                }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e, FileChangeType.Changed);
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e, FileChangeType.Created);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e, FileChangeType.Deleted);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            lock (_lock)
            {
                try
                {
                    _lastFileChange = DateTime.UtcNow;
                    Statistics.TotalChanges++;

                    // Handle rename immediately as it's a critical change
                    var args = new ConfigurationFileChangedEventArgs(e.OldName, e.Name, DateTime.UtcNow, true);
                    OnFileChanged(args);

                    LogInfo($"Configuration file renamed from '{e.OldName}' to '{e.Name}'");
                }
                catch (Exception ex)
                {
                    var args = new ConfigurationFileChangedEventArgs(e.OldName, e.Name, DateTime.UtcNow, false, ex.Message);
                    OnFileChanged(args);

                    Statistics.TotalErrors++;
                    LogError($"Error handling file rename: {ex.Message}");
                }
            }
        }

        private void HandleFileChange(FileSystemEventArgs e, FileChangeType changeType)
        {
            lock (_lock)
            {
                try
                {
                    _lastFileChange = DateTime.UtcNow;
                    Statistics.TotalChanges++;

                    // Debounce rapid changes
                    _debounceTimer.Change(_debounceInterval, Timeout.Infinite);

                    LogDebug($"File {changeType} detected: {e.FullPath}");
                }
                catch (Exception ex)
                {
                    Statistics.TotalErrors++;
                    LogError($"Error handling file change: {ex.Message}");
                }
            }
        }

        private void OnDebounceTimerElapsed(object state)
        {
            try
            {
                ProcessFileChange();
            }
            catch (Exception ex)
            {
                Statistics.TotalErrors++;
                LogError($"Error processing debounced file change: {ex.Message}");
            }
        }

        private void ProcessFileChange()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_configFilePath))
                    {
                        var args = new ConfigurationFileChangedEventArgs(FileChangeType.Deleted, _configFilePath, DateTime.UtcNow, 0, false, "Configuration file not found");
                        OnFileChanged(args);
                        return;
                    }

                    var fileInfo = new FileInfo(_configFilePath);
                    var args = new ConfigurationFileChangedEventArgs(FileChangeType.Changed, _configFilePath, DateTime.UtcNow, fileInfo.Length, true);

                    OnFileChanged(args);

                    _lastSuccessfulRead = DateTime.UtcNow;
                    _currentRetryAttempts = 0;

                    LogInfo($"Configuration file processed successfully: {_configFilePath}");
                }
                catch (Exception ex)
                {
                    _currentRetryAttempts++;
                    Statistics.TotalErrors++;

                    var args = new ConfigurationFileChangedEventArgs(FileChangeType.Changed, _configFilePath, DateTime.UtcNow, 0, false, ex.Message);
                    OnFileChanged(args);

                    LogError($"Failed to process configuration file: {ex.Message}");

                    // Try to restart the watcher if we have too many retry attempts
                    if (_currentRetryAttempts >= _maxRetryAttempts)
                    {
                        LogWarning("Maximum retry attempts reached, restarting watcher...");
                        RestartWatcher();
                    }
                }
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            lock (_lock)
            {
                var ex = e.GetException();
                Statistics.TotalErrors++;

                var errorArgs = new WatcherErrorEventArgs(ex, DateTime.UtcNow);
                OnWatcherError(errorArgs);

                LogError($"FileSystemWatcher error: {ex.Message}");

                // Try to restart the watcher on error
                try
                {
                    RestartWatcher();
                }
                catch (Exception restartEx)
                {
                    LogError($"Failed to restart watcher after error: {restartEx.Message}");
                }
            }
        }

        private void OnHealthCheckTimerElapsed(object state)
        {
            try
            {
                PerformHealthCheck();
            }
            catch (Exception ex)
            {
                Statistics.TotalErrors++;
                LogError($"Error during health check: {ex.Message}");
            }
        }

        private void PerformHealthCheck()
        {
            lock (_lock)
            {
                var healthStatus = GetHealthStatus();
                var checkDuration = TimeSpan.FromMilliseconds(0);
                var startTime = DateTime.UtcNow;

                try
                {
                    // Perform detailed health checks
                    var healthChecks = new Dictionary<string, HealthCheckResult>();

                    // Check file accessibility
                    var fileCheck = CheckFileAccessibility();
                    healthChecks["FileAccessibility"] = fileCheck;

                    // Check watcher status
                    var watcherCheck = CheckWatcherStatus();
                    healthChecks["WatcherStatus"] = watcherCheck;

                    // Check memory usage
                    var memoryCheck = CheckMemoryUsage();
                    healthChecks["MemoryUsage"] = memoryCheck;

                    checkDuration = DateTime.UtcNow - startTime;

                    var healthArgs = new WatcherHealthEventArgs(healthStatus, healthChecks, DateTime.UtcNow, checkDuration);
                    OnHealthCheck(healthArgs);

                    Statistics.LastHealthCheck = DateTime.UtcNow;
                    Statistics.TotalHealthChecks++;
                }
                catch (Exception ex)
                {
                    checkDuration = DateTime.UtcNow - startTime;
                    Statistics.TotalErrors++;

                    var healthArgs = new WatcherHealthEventArgs(WatcherHealthStatus.Error, new Dictionary<string, HealthCheckResult>(), DateTime.UtcNow, checkDuration);
                    OnHealthCheck(healthArgs);

                    LogError($"Health check failed: {ex.Message}");
                }
            }
        }

        private HealthCheckResult CheckFileAccessibility()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                if (!File.Exists(_configFilePath))
                {
                    return new HealthCheckResult(ConfigurationHealthStatus.Error, "Configuration file not found", "FileAccessibility", DateTime.UtcNow - startTime, "File does not exist");
                }

                // Try to open the file for reading
                using (var stream = File.OpenRead(_configFilePath))
                {
                    // Just test access
                }

                var fileInfo = new FileInfo(_configFilePath);
                var details = new Dictionary<string, object>
                {
                    { "FileSize", fileInfo.Length },
                    { "LastWriteTime", fileInfo.LastWriteTimeUtc },
                    { "IsReadOnly", fileInfo.IsReadOnly }
                };

                return new HealthCheckResult(ConfigurationHealthStatus.Healthy, "File is accessible", "FileAccessibility", DateTime.UtcNow - startTime, null, details);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(ConfigurationHealthStatus.Error, "File access failed", "FileAccessibility", DateTime.UtcNow - startTime, ex.Message);
            }
        }

        private HealthCheckResult CheckWatcherStatus()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var details = new Dictionary<string, object>
                {
                    { "IsWatching", _isWatching },
                    { "EnableRaisingEvents", _fileWatcher.EnableRaisingEvents },
                    { "AdditionalWatchers", _additionalWatchers.Count },
                    { "RetryAttempts", _currentRetryAttempts }
                };

                var status = _isWatching ? ConfigurationHealthStatus.Healthy : ConfigurationHealthStatus.Warning;
                return new HealthCheckResult(status, "Watcher is functioning", "WatcherStatus", DateTime.UtcNow - startTime, null, details);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(ConfigurationHealthStatus.Error, "Watcher status check failed", "WatcherStatus", DateTime.UtcNow - startTime, ex.Message);
            }
        }

        private HealthCheckResult CheckMemoryUsage()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
                var memoryLimitMB = 500; // 500MB limit for watcher

                var status = memoryMB > memoryLimitMB ? ConfigurationHealthStatus.Warning : ConfigurationHealthStatus.Healthy;
                var description = memoryMB > memoryLimitMB ? $"High memory usage: {memoryMB:F1}MB" : $"Memory usage normal: {memoryMB:F1}MB";

                var details = new Dictionary<string, object>
                {
                    { "MemoryUsageMB", memoryMB },
                    { "MemoryLimitMB", memoryLimitMB },
                    { "IsMemoryCritical", memoryMB > memoryLimitMB * 1.5 }
                };

                return new HealthCheckResult(status, description, "MemoryUsage", DateTime.UtcNow - startTime, null, details);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(ConfigurationHealthStatus.Error, "Memory check failed", "MemoryUsage", DateTime.UtcNow - startTime, ex.Message);
            }
        }

        protected virtual void OnFileChanged(ConfigurationFileChangedEventArgs e)
        {
            FileChanged?.Invoke(this, e);
        }

        protected virtual void OnWatcherError(WatcherErrorEventArgs e)
        {
            WatcherError?.Invoke(this, e);
        }

        protected virtual void OnHealthCheck(WatcherHealthEventArgs e)
        {
            HealthCheck?.Invoke(this, e);
        }

        private void LogInfo(string message)
        {
            Console.WriteLine($"[ConfigurationWatcher] {message}");
        }

        private void LogWarning(string message)
        {
            Console.WriteLine($"[ConfigurationWatcher Warning] {message}");
        }

        private void LogError(string message)
        {
            Console.WriteLine($"[ConfigurationWatcher Error] {message}");
        }

        private void LogDebug(string message)
        {
            // Debug logging could be enabled via configuration
            // Console.WriteLine($"[ConfigurationWatcher Debug] {message}");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Stop watching
                    StopWatching();

                    // Dispose managed resources
                    _fileWatcher?.Dispose();
                    _debounceTimer?.Dispose();
                    _healthCheckTimer?.Dispose();

                    foreach (var watcher in _additionalWatchers)
                    {
                        watcher?.Dispose();
                    }
                    _additionalWatchers.Clear();
                }

                _isDisposed = true;
            }
        }

        ~ConfigurationWatcher()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Exception thrown by configuration watcher operations
    /// </summary>
    public class ConfigurationWatcherException : Exception
    {
        public ConfigurationWatcherException(string message) : base(message) { }
        public ConfigurationWatcherException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Event arguments for watcher errors
    /// </summary>
    public class WatcherErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public DateTime Timestamp { get; }

        public WatcherErrorEventArgs(Exception exception, DateTime timestamp)
        {
            Exception = exception;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Event arguments for watcher health checks
    /// </summary>
    public class WatcherHealthEventArgs : EventArgs
    {
        public WatcherHealthStatus HealthStatus { get; }
        public Dictionary<string, HealthCheckResult> HealthChecks { get; }
        public DateTime CheckedAt { get; }
        public TimeSpan CheckDuration { get; }

        public WatcherHealthEventArgs(WatcherHealthStatus healthStatus, Dictionary<string, HealthCheckResult> healthChecks, DateTime checkedAt, TimeSpan checkDuration)
        {
            HealthStatus = healthStatus;
            HealthChecks = healthChecks;
            CheckedAt = checkedAt;
            CheckDuration = checkDuration;
        }
    }

    /// <summary>
    /// Watcher health status
    /// </summary>
    public enum WatcherHealthStatus
    {
        Healthy,
        Warning,
        Error,
        Stopped,
        Disposed,
        FileNotFound
    }

    /// <summary>
    /// Statistics for the configuration watcher
    /// </summary>
    public class WatcherStatistics
    {
        public DateTime StartTime { get; set; }
        public DateTime StopTime { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public long TotalChanges { get; set; }
        public long TotalErrors { get; set; }
        public long TotalRestarts { get; set; }
        public long TotalHealthChecks { get; set; }
        public int AdditionalWatchersCount { get; set; }
        public TimeSpan Uptime => StopTime > StartTime ? StopTime - StartTime : DateTime.UtcNow - StartTime;
    }
}