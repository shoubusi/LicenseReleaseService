using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if !NET9_0
using System.Configuration;
#endif

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Manages configuration reload operations with backup, rollback, and validation capabilities
    /// </summary>
    public class ConfigurationReloadManager : IDisposable
    {
        private readonly object _lock = new object();
        private readonly string _configFilePath;
        private readonly string _backupDirectory;
        private readonly Timer _reloadTimer;
        private readonly Timer _cleanupTimer;
        private readonly List<ConfigurationBackup> _backupHistory;
        private readonly Queue<ReloadOperation> _reloadHistory;

        private bool _isDisposed;
        private bool _isEnabled;
        private int _maxBackupCount;
        private int _maxHistorySize;
        private int _reloadTimeout;
        private int _maxConcurrentReloads;
        private int _currentReloads;
        private ReloadStrategy _reloadStrategy;
        private ReloadOperation _currentOperation;

        // Event handlers
        public event ConfigurationEvents.ConfigurationReloadEventHandler ReloadCompleted;
        public event ConfigurationEvents.ConfigurationBackupEventHandler BackupCompleted;
        public event EventHandler<ReloadOperationStartedEventArgs> ReloadOperationStarted;
        public event EventHandler<ReloadOperationFailedEventArgs> ReloadOperationFailed;

        /// <summary>
        /// Gets whether the reload manager is enabled
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                lock (_lock)
                {
                    return _isEnabled;
                }
            }
            set
            {
                lock (_lock)
                {
                    _isEnabled = value;
                    if (_isEnabled)
                    {
                        StartTimers();
                    }
                    else
                    {
                        StopTimers();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current reload operation
        /// </summary>
        public ReloadOperation CurrentOperation
        {
            get
            {
                lock (_lock)
                {
                    return _currentOperation;
                }
            }
        }

        /// <summary>
        /// Gets the backup history
        /// </summary>
        public IReadOnlyList<ConfigurationBackup> BackupHistory
        {
            get
            {
                lock (_lock)
                {
                    return _backupHistory.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the reload history
        /// </summary>
        public IReadOnlyList<ReloadOperation> ReloadHistory
        {
            get
            {
                lock (_lock)
                {
                    return _reloadHistory.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the reload manager statistics
        /// </summary>
        public ReloadManagerStatistics Statistics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationReloadManager class
        /// </summary>
        public ConfigurationReloadManager(string configFilePath, string backupDirectory = null)
        {
            _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));
            _backupDirectory = backupDirectory ?? Path.Combine(Path.GetDirectoryName(configFilePath), "backups");
            _backupHistory = new List<ConfigurationBackup>();
            _reloadHistory = new Queue<ReloadOperation>();
            _maxBackupCount = 10;
            _maxHistorySize = 100;
            _reloadTimeout = 30000; // 30 seconds
            _maxConcurrentReloads = 1;
            _reloadStrategy = ReloadStrategy.Immediate;
            _currentReloads = 0;

            Statistics = new ReloadManagerStatistics();

            // Create backup directory if it doesn't exist
            Directory.CreateDirectory(_backupDirectory);

            // Initialize timers
            _reloadTimer = new Timer(OnReloadTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer = new Timer(OnCleanupTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

            // Load existing backup history
            LoadBackupHistory();
        }

        /// <summary>
        /// Starts the reload manager
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationReloadManager));

                _isEnabled = true;
                StartTimers();

                Statistics.StartTime = DateTime.UtcNow;
                LogInfo("Configuration reload manager started");
            }
        }

        /// <summary>
        /// Stops the reload manager
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_isDisposed || !_isEnabled)
                    return;

                _isEnabled = false;
                StopTimers();

                Statistics.StopTime = DateTime.UtcNow;
                LogInfo("Configuration reload manager stopped");
            }
        }

        /// <summary>
        /// Reloads the configuration with the specified trigger
        /// </summary>
        public async Task<bool> ReloadConfigurationAsync(ReloadTrigger trigger, bool createBackup = true)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationReloadManager));

                if (!_isEnabled)
                    return false;

                if (_currentReloads >= _maxConcurrentReloads)
                {
                    LogWarning($"Maximum concurrent reloads ({_maxConcurrentReloads}) reached, reload request queued");
                    return false;
                }
            }

            var operation = new ReloadOperation
            {
                Id = Guid.NewGuid(),
                Trigger = trigger,
                Status = ReloadOperationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                CreateBackup = createBackup
            };

            try
            {
                // Notify that reload operation is starting
                OnReloadOperationStarted(new ReloadOperationStartedEventArgs(operation));

                Statistics.TotalReloadsStarted++;

                // Set current operation
                lock (_lock)
                {
                    _currentOperation = operation;
                    _currentReloads++;
                }

                operation.Status = ReloadOperationStatus.Running;
                operation.StartedAt = DateTime.UtcNow;

                LogInfo($"Starting configuration reload: {operation.Id} (Trigger: {trigger})");

                // Create backup if requested
                string backupPath = null;
                if (createBackup)
                {
                    backupPath = await CreateBackupAsync(BackupReason.PreReload);
                }

                // Perform the actual reload
                var reloadResult = await PerformReloadAsync(operation);

                // Update operation status
                operation.Status = reloadResult.IsSuccess ? ReloadOperationStatus.Completed : ReloadOperationStatus.Failed;
                operation.CompletedAt = DateTime.UtcNow;
                operation.Duration = operation.CompletedAt - operation.StartedAt.Value;
                operation.ErrorMessage = reloadResult.ErrorMessage;
                operation.BackupPath = backupPath;

                // Add to history
                lock (_lock)
                {
                    _reloadHistory.Enqueue(operation);
                    while (_reloadHistory.Count > _maxHistorySize)
                    {
                        _reloadHistory.Dequeue();
                    }
                }

                // Update statistics
                if (reloadResult.IsSuccess)
                {
                    Statistics.TotalSuccessfulReloads++;
                    Statistics.LastSuccessfulReload = DateTime.UtcNow;
                }
                else
                {
                    Statistics.TotalFailedReloads++;
                }

                // Raise reload completed event
                OnReloadCompleted(reloadResult);

                if (reloadResult.IsSuccess)
                {
                    LogInfo($"Configuration reload completed successfully: {operation.Id}");
                }
                else
                {
                    LogError($"Configuration reload failed: {operation.Id} - {reloadResult.ErrorMessage}");
                    OnReloadOperationFailed(new ReloadOperationFailedEventArgs(operation, reloadResult.ErrorMessage));
                }

                return reloadResult.IsSuccess;
            }
            catch (Exception ex)
            {
                operation.Status = ReloadOperationStatus.Failed;
                operation.CompletedAt = DateTime.UtcNow;
                operation.ErrorMessage = ex.Message;
                operation.Duration = operation.CompletedAt - operation.StartedAt.Value;

                Statistics.TotalFailedReloads++;

                LogError($"Configuration reload failed with exception: {operation.Id} - {ex.Message}");
                OnReloadOperationFailed(new ReloadOperationFailedEventArgs(operation, ex.Message));

                return false;
            }
            finally
            {
                lock (_lock)
                {
                    _currentReloads--;
                    if (_currentOperation?.Id == operation.Id)
                    {
                        _currentOperation = null;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a backup of the current configuration
        /// </summary>
        public async Task<string> CreateBackupAsync(BackupReason reason)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationReloadManager));

                if (!File.Exists(_configFilePath))
                    throw new FileNotFoundException($"Configuration file not found: {_configFilePath}");
            }

            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"config_{timestamp}.bak";
                var backupPath = Path.Combine(_backupDirectory, backupFileName);

                // Copy the configuration file
                await Task.Run(() =>
                {
                    File.Copy(_configFilePath, backupPath, true);
                });

                var backup = new ConfigurationBackup
                {
                    Id = Guid.NewGuid(),
                    OriginalPath = _configFilePath,
                    BackupPath = backupPath,
                    CreatedAt = DateTime.UtcNow,
                    Reason = reason,
                    FileSize = new FileInfo(backupPath).Length,
                    Checksum = CalculateFileChecksum(backupPath)
                };

                lock (_lock)
                {
                    _backupHistory.Add(backup);
                    CleanupOldBackups();
                }

                Statistics.TotalBackupsCreated++;

                var backupArgs = new ConfigurationBackupEventArgs(
                    BackupOperationType.Create, _configFilePath, backupPath, DateTime.UtcNow, true, reason, backup.FileSize);

                OnBackupCompleted(backupArgs);

                LogInfo($"Configuration backup created: {backupPath} (Reason: {reason})");
                return backupPath;
            }
            catch (Exception ex)
            {
                Statistics.TotalFailedBackups++;

                var backupArgs = new ConfigurationBackupEventArgs(
                    BackupOperationType.Create, _configFilePath, null, DateTime.UtcNow, false, reason, 0, ex.Message);

                OnBackupCompleted(backupArgs);

                LogError($"Failed to create configuration backup: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Restores configuration from a backup
        /// </summary>
        public async Task<bool> RestoreFromBackupAsync(Guid backupId)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationReloadManager));
            }

            ConfigurationBackup backup = null;
            lock (_lock)
            {
                backup = _backupHistory.FirstOrDefault(b => b.Id == backupId);
            }

            if (backup == null)
            {
                LogError($"Backup not found: {backupId}");
                return false;
            }

            if (!File.Exists(backup.BackupPath))
            {
                LogError($"Backup file not found: {backup.BackupPath}");
                return false;
            }

            try
            {
                // Create backup of current configuration before restore
                await CreateBackupAsync(BackupReason.Emergency);

                // Restore from backup
                await Task.Run(() =>
                {
                    File.Copy(backup.BackupPath, _configFilePath, true);
                });

                // Perform reload with rollback flag
                var success = await ReloadConfigurationAsync(ReloadTrigger.Rollback, false);

                if (success)
                {
                    var backupArgs = new ConfigurationBackupEventArgs(
                        BackupOperationType.Restore, backup.BackupPath, _configFilePath, DateTime.UtcNow, true, BackupReason.Emergency);

                    OnBackupCompleted(backupArgs);

                    LogInfo($"Configuration restored from backup: {backup.BackupPath}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogError($"Failed to restore from backup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the configuration validation result
        /// </summary>
        public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(string configPath = null)
        {
            var pathToValidate = configPath ?? _configFilePath;

            if (!File.Exists(pathToValidate))
            {
                return new ConfigurationValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Configuration file not found: {pathToValidate}" }
                };
            }

            try
            {
                // Load and validate configuration
                var config = await LoadConfigurationFromFileAsync(pathToValidate);
                var errors = config.Validate();

                return new ConfigurationValidationResult
                {
                    IsValid = errors.Count == 0,
                    Errors = errors,
                    Configuration = config,
                    ValidatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Configuration validation failed: {ex.Message}" },
                    ValidatedAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Sets the reload strategy
        /// </summary>
        public void SetReloadStrategy(ReloadStrategy strategy, int intervalSeconds = 60)
        {
            lock (_lock)
            {
                _reloadStrategy = strategy;

                if (strategy == ReloadStrategy.Scheduled)
                {
                    _reloadTimer.Change(TimeSpan.FromSeconds(intervalSeconds), TimeSpan.FromSeconds(intervalSeconds));
                }
                else
                {
                    _reloadTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        private async Task<ConfigurationReloadEventArgs> PerformReloadAsync(ReloadOperation operation)
        {
            var startTime = DateTime.UtcNow;
            var oldConfig = await LoadCurrentConfigurationAsync();

            try
            {
                // Reload configuration
                var newConfig = await LoadConfigurationFromFileAsync(_configFilePath);

                // Validate the new configuration
                var validationErrors = newConfig.Validate();
                if (validationErrors.Count > 0)
                {
                    return new ConfigurationReloadEventArgs(
                        oldConfig, newConfig, validationErrors, operation.Trigger,
                        DateTime.UtcNow - startTime, startTime, DateTime.UtcNow, false, operation.BackupPath);
                }

                // Apply the new configuration
                await ApplyConfigurationAsync(newConfig);

                return new ConfigurationReloadEventArgs(
                    oldConfig, newConfig, validationErrors, operation.Trigger,
                    DateTime.UtcNow - startTime, startTime, DateTime.UtcNow, false, operation.BackupPath);
            }
            catch (Exception ex)
            {
                return new ConfigurationReloadEventArgs(
                    oldConfig, null, new List<string> { ex.Message }, operation.Trigger,
                    DateTime.UtcNow - startTime, startTime, DateTime.UtcNow, false, operation.BackupPath);
            }
        }

        private async Task<LicenseReleaseServiceSection> LoadCurrentConfigurationAsync()
        {
            // This would typically integrate with the existing ConfigurationManager
            // For now, we'll load directly from file
            return await LoadConfigurationFromFileAsync(_configFilePath);
        }

        private async Task<LicenseReleaseServiceSection> LoadConfigurationFromFileAsync(string configPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var configMap = new System.Configuration.ExeConfigurationFileMap
                    {
                        ExeConfigFilename = configPath
                    };

                    var config = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(configMap, System.Configuration.ConfigurationUserLevel.None);
                    var section = config.GetSection("licenseReleaseService") as LicenseReleaseServiceSection;

                    if (section == null)
                    {
#if !NET9_0
                        throw new ConfigurationErrorsException("licenseReleaseService section not found in configuration file");
#else
                        throw new InvalidOperationException("licenseReleaseService section not found in configuration file");
#endif
                    }

                    return section;
                }
                catch (Exception ex)
                {
#if !NET9_0
                    throw new ConfigurationErrorsException($"Failed to load configuration from {configPath}: {ex.Message}", ex);
#else
                    throw new InvalidOperationException($"Failed to load configuration from {configPath}: {ex.Message}", ex);
#endif
                }
            });
        }

        private async Task ApplyConfigurationAsync(LicenseReleaseServiceSection configuration)
        {
            await Task.Run(() =>
            {
                // This would typically update the existing ConfigurationManager
                // For now, we'll just validate that the configuration is valid
                var errors = configuration.Validate();
                if (errors.Count > 0)
                {
#if !NET9_0
                    throw new ConfigurationErrorsException($"Configuration validation failed: {string.Join(", ", errors)}");
#else
                    throw new InvalidOperationException($"Configuration validation failed: {string.Join(", ", errors)}");
#endif
                }
            });
        }

        private void LoadBackupHistory()
        {
            try
            {
                if (!Directory.Exists(_backupDirectory))
                    return;

                var backupFiles = Directory.GetFiles(_backupDirectory, "*.bak")
                    .OrderBy(f => f)
                    .Take(_maxBackupCount);

                foreach (var backupFile in backupFiles)
                {
                    var fileInfo = new FileInfo(backupFile);
                    var backup = new ConfigurationBackup
                    {
                        Id = Guid.NewGuid(),
                        OriginalPath = _configFilePath,
                        BackupPath = backupFile,
                        CreatedAt = fileInfo.CreationTimeUtc,
                        Reason = BackupReason.Scheduled,
                        FileSize = fileInfo.Length,
                        Checksum = CalculateFileChecksum(backupFile)
                    };

                    _backupHistory.Add(backup);
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load backup history: {ex.Message}");
            }
        }

        private void CleanupOldBackups()
        {
            try
            {
                while (_backupHistory.Count > _maxBackupCount)
                {
                    var oldestBackup = _backupHistory.OrderBy(b => b.CreatedAt).First();

                    if (File.Exists(oldestBackup.BackupPath))
                    {
                        File.Delete(oldestBackup.BackupPath);
                    }

                    _backupHistory.Remove(oldestBackup);

                    var backupArgs = new ConfigurationBackupEventArgs(
                        BackupOperationType.Cleanup, oldestBackup.OriginalPath, oldestBackup.BackupPath,
                        DateTime.UtcNow, true, BackupReason.Scheduled, oldestBackup.FileSize);

                    OnBackupCompleted(backupArgs);
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to cleanup old backups: {ex.Message}");
            }
        }

        private string CalculateFileChecksum(string filePath)
        {
            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    using (var sha256 = System.Security.Cryptography.SHA256.Create())
                    {
                        var hash = sha256.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch
            {
                return "unknown";
            }
        }

        private void StartTimers()
        {
            if (_reloadStrategy == ReloadStrategy.Scheduled)
            {
                _reloadTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }

            _cleanupTimer.Change(TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        private void StopTimers()
        {
            _reloadTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void OnReloadTimerElapsed(object state)
        {
            if (!_isEnabled)
                return;

            try
            {
                await ReloadConfigurationAsync(ReloadTrigger.Timer, true);
            }
            catch (Exception ex)
            {
                LogError($"Scheduled reload failed: {ex.Message}");
            }
        }

        private void OnCleanupTimerElapsed(object state)
        {
            if (!_isEnabled)
                return;

            try
            {
                lock (_lock)
                {
                    CleanupOldBackups();
                }
            }
            catch (Exception ex)
            {
                LogError($"Backup cleanup failed: {ex.Message}");
            }
        }

        protected virtual void OnReloadCompleted(ConfigurationReloadEventArgs e)
        {
            ReloadCompleted?.Invoke(this, e);
        }

        protected virtual void OnBackupCompleted(ConfigurationBackupEventArgs e)
        {
            BackupCompleted?.Invoke(this, e);
        }

        protected virtual void OnReloadOperationStarted(ReloadOperationStartedEventArgs e)
        {
            ReloadOperationStarted?.Invoke(this, e);
        }

        protected virtual void OnReloadOperationFailed(ReloadOperationFailedEventArgs e)
        {
            ReloadOperationFailed?.Invoke(this, e);
        }

        private void LogInfo(string message)
        {
            Console.WriteLine($"[ConfigurationReloadManager] {message}");
        }

        private void LogWarning(string message)
        {
            Console.WriteLine($"[ConfigurationReloadManager Warning] {message}");
        }

        private void LogError(string message)
        {
            Console.WriteLine($"[ConfigurationReloadManager Error] {message}");
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
                    Stop();
                    _reloadTimer?.Dispose();
                    _cleanupTimer?.Dispose();
                }

                _isDisposed = true;
            }
        }

        ~ConfigurationReloadManager()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Reload operation strategies
    /// </summary>
    public enum ReloadStrategy
    {
        Immediate,
        Debounced,
        Scheduled
    }

    /// <summary>
    /// Reload operation status
    /// </summary>
    public enum ReloadOperationStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Represents a configuration reload operation
    /// </summary>
    public class ReloadOperation
    {
        public Guid Id { get; set; }
        public ReloadTrigger Trigger { get; set; }
        public ReloadOperationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public string ErrorMessage { get; set; }
        public bool CreateBackup { get; set; }
        public string BackupPath { get; set; }
    }

    /// <summary>
    /// Represents a configuration backup
    /// </summary>
    public class ConfigurationBackup
    {
        public Guid Id { get; set; }
        public string OriginalPath { get; set; }
        public string BackupPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public BackupReason Reason { get; set; }
        public long FileSize { get; set; }
        public string Checksum { get; set; }
    }

    /// <summary>
    /// Configuration validation result
    /// </summary>
    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
        public LicenseReleaseServiceSection Configuration { get; set; }
        public DateTime ValidatedAt { get; set; }
    }

    /// <summary>
    /// Reload manager statistics
    /// </summary>
    public class ReloadManagerStatistics
    {
        public DateTime StartTime { get; set; }
        public DateTime StopTime { get; set; }
        public DateTime LastSuccessfulReload { get; set; }
        public long TotalReloadsStarted { get; set; }
        public long TotalSuccessfulReloads { get; set; }
        public long TotalFailedReloads { get; set; }
        public long TotalBackupsCreated { get; set; }
        public long TotalFailedBackups { get; set; }
        public TimeSpan Uptime => StopTime > StartTime ? StopTime - StartTime : DateTime.UtcNow - StartTime;
    }

    /// <summary>
    /// Event arguments for reload operation started
    /// </summary>
    public class ReloadOperationStartedEventArgs : EventArgs
    {
        public ReloadOperation Operation { get; }

        public ReloadOperationStartedEventArgs(ReloadOperation operation)
        {
            Operation = operation;
        }
    }

    /// <summary>
    /// Event arguments for reload operation failed
    /// </summary>
    public class ReloadOperationFailedEventArgs : EventArgs
    {
        public ReloadOperation Operation { get; }
        public string ErrorMessage { get; set; }

        public ReloadOperationFailedEventArgs(ReloadOperation operation, string errorMessage)
        {
            Operation = operation;
            ErrorMessage = errorMessage;
        }
    }
}