using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// File system monitoring for SolidWorks document activity
    /// </summary>
    public class FileSystemMonitor : IDisposable
    {
        private readonly ILogger<FileSystemMonitor> _logger;
        private readonly FileSystemMonitorConfig _config;
        private readonly object _lock = new object();
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
        private readonly ConcurrentDictionary<int, ProcessFileSystemHistory> _processHistories = new ConcurrentDictionary<int, ProcessFileSystemHistory>();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitoringTask;
        private bool _isDisposed;
        private bool _isRunning;

        #region Events

        /// <summary>
        /// Event raised when file system activity is detected
        /// </summary>
        public event EventHandler<FileSystemActivityEventArgs> FileSystemActivityDetected;

        /// <summary>
        /// Gets a value indicating whether the monitor is running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the monitor statistics
        /// </summary>
        public FileSystemMonitorStatistics Statistics { get; private set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the FileSystemMonitor class
        /// </summary>
        public FileSystemMonitor(ILogger<FileSystemMonitor> logger, FileSystemMonitorConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Statistics = new FileSystemMonitorStatistics();

            // Initialize cleanup timer for old file system histories
            _cleanupTimer = new Timer(CleanupTimerCallback, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

            _logger.LogInformation("FileSystemMonitor initialized with configuration: {Config}", _config);
        }

        /// <summary>
        /// Initializes the monitor with configuration
        /// </summary>
        public async Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing FileSystemMonitor");

                lock (_lock)
                {
                    if (_isRunning)
                    {
                        _logger.LogWarning("FileSystemMonitor is already running");
                        return;
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Clear any existing watchers and histories
                lock (_lock)
                {
                    foreach (var watcher in _watchers.Values)
                    {
                        watcher?.Dispose();
                    }
                    _watchers.Clear();
                    _processHistories.Clear();
                    Statistics.Reset();
                }

                // Setup file system watchers for SolidWorks directories
                await SetupFileSystemWatchersAsync();

                _logger.LogInformation("FileSystemMonitor initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing FileSystemMonitor");
                throw;
            }
        }

        /// <summary>
        /// Starts the file system monitoring
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting FileSystemMonitor");

                lock (_lock)
                {
                    if (_isRunning)
                    {
                        _logger.LogWarning("FileSystemMonitor is already running");
                        return;
                    }

                    _isRunning = true;
                }

                // Start file system watchers
                StartFileSystemWatchers();

                // Start monitoring task for active processes
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                _logger.LogInformation("FileSystemMonitor started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting FileSystemMonitor");
                throw;
            }
        }

        /// <summary>
        /// Stops the file system monitoring
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping FileSystemMonitor");

                lock (_lock)
                {
                    if (!_isRunning)
                    {
                        _logger.LogWarning("FileSystemMonitor is not running");
                        return;
                    }

                    _isRunning = false;
                }

                // Stop monitoring task
                _cancellationTokenSource?.Cancel();

                // Stop file system watchers
                StopFileSystemWatchers();

                // Wait for monitoring task to complete
                if (_monitoringTask != null)
                {
                    await Task.WhenAny(_monitoringTask, Task.Delay(TimeSpan.FromSeconds(5)));
                }

                _logger.LogInformation("FileSystemMonitor stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping FileSystemMonitor");
                throw;
            }
        }

        /// <summary>
        /// Pauses the file system monitoring
        /// </summary>
        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Pausing FileSystemMonitor");

                lock (_lock)
                {
                    if (!_isRunning)
                    {
                        _logger.LogWarning("FileSystemMonitor is not running");
                        return;
                    }

                    _isRunning = false;
                }

                // Pause file system watchers
                PauseFileSystemWatchers();

                _cancellationTokenSource?.Cancel();

                _logger.LogInformation("FileSystemMonitor paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing FileSystemMonitor");
                throw;
            }
        }

        /// <summary>
        /// Resumes the file system monitoring
        /// </summary>
        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Resuming FileSystemMonitor");

                lock (_lock)
                {
                    if (_isRunning)
                    {
                        _logger.LogWarning("FileSystemMonitor is already running");
                        return;
                    }

                    _isRunning = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Resume file system watchers
                ResumeFileSystemWatchers();

                // Restart monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                _logger.LogInformation("FileSystemMonitor resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming FileSystemMonitor");
                throw;
            }
        }

        /// <summary>
        /// Gets file system activities for a specific process
        /// </summary>
        public async Task<List<ActivityData>> GetProcessActivitiesAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_processHistories.TryGetValue(processId, out var history))
                {
                    var recentActivities = history.GetRecentActivities(TimeSpan.FromMinutes(30));
                    return recentActivities.Select(a => new ActivityData
                    {
                        Type = ActivityType.FileSystem,
                        Timestamp = a.Timestamp,
                        Confidence = a.Confidence,
                        Source = "FileSystemMonitor",
                        Details = new Dictionary<string, object>
                        {
                            ["FilePath"] = a.FilePath,
                            ["ChangeType"] = a.ChangeType,
                            ["FileSize"] = a.FileSize,
                            ["Operation"] = a.Operation
                        }
                    }).ToList();
                }

                return new List<ActivityData>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file system activities for process {ProcessId}", processId);
                return new List<ActivityData>();
            }
        }

        /// <summary>
        /// Updates the monitor configuration
        /// </summary>
        public async Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating FileSystemMonitor configuration");

                if (configuration?.CustomParameters != null)
                {
                    // Update file system monitoring specific parameters
                    if (configuration.CustomParameters.ContainsKey("EnableFileSystemMonitoring"))
                    {
                        _config.EnableFileSystemMonitoring = Convert.ToBoolean(configuration.CustomParameters["EnableFileSystemMonitoring"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("WatchSolidWorksDirectories"))
                    {
                        _config.WatchSolidWorksDirectories = Convert.ToBoolean(configuration.CustomParameters["WatchSolidWorksDirectories"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("WatchTempDirectories"))
                    {
                        _config.WatchTempDirectories = Convert.ToBoolean(configuration.CustomParameters["WatchTempDirectories"]);
                    }

                    if (configuration.CustomParameters.ContainsKey("MaxFileSizeToMonitorBytes"))
                    {
                        _config.MaxFileSizeToMonitorBytes = Convert.ToInt64(configuration.CustomParameters["MaxFileSizeToMonitorBytes"]);
                    }
                }

                // Re-setup watchers if configuration changed
                if (_isRunning)
                {
                    await StopAsync(cancellationToken);
                    await InitializeAsync(configuration, cancellationToken);
                    await StartAsync(cancellationToken);
                }

                _logger.LogInformation("FileSystemMonitor configuration updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating FileSystemMonitor configuration");
                throw;
            }
        }

        /// <summary>
        /// Gets the health status of the monitor
        /// </summary>
        public async Task<DetectorHealth> GetHealthAsync()
        {
            try
            {
                var health = new DetectorHealth
                {
                    DetectorName = "FileSystemMonitor",
                    CheckTimestamp = DateTime.UtcNow,
                    StatusMessage = "Healthy"
                };

                var isHealthy = true;
                var issues = new List<string>();

                // Check service status
                if (!_isRunning)
                {
                    issues.Add("Monitor is not running");
                    isHealthy = false;
                }

                // Check watcher count
                lock (_lock)
                {
                    if (_watchers.Count == 0)
                    {
                        issues.Add("No file system watchers active");
                    }

                    if (_watchers.Count > 50)
                    {
                        issues.Add($"High watcher count: {_watchers.Count}");
                    }
                }

                // Check file system history size
                if (_processHistories.Count > 1000)
                {
                    issues.Add($"High file system history count: {_processHistories.Count}");
                }

                // Check statistics for abnormal patterns
                if (Statistics.TotalActivitiesMonitored > 0)
                {
                    var errorRate = (double)Statistics.ErrorCount / Statistics.TotalActivitiesMonitored;
                    if (errorRate > 0.1)
                    {
                        issues.Add($"High error rate: {errorRate:P1}");
                    }
                }

                health.IsHealthy = isHealthy;
                health.StatusMessage = isHealthy ? "Healthy" : $"Issues: {string.Join(", ", issues)}";

                // Add additional info
                health.AdditionalInfo["WatcherCount"] = _watchers.Count;
                health.AdditionalInfo["FileSystemHistoryCount"] = _processHistories.Count;
                health.AdditionalInfo["TotalActivitiesMonitored"] = Statistics.TotalActivitiesMonitored;
                health.AdditionalInfo["SuccessfulDetections"] = Statistics.SuccessfulDetections;
                health.AdditionalInfo["FailedDetections"] = Statistics.FailedDetections;
                health.AdditionalInfo["ErrorCount"] = Statistics.ErrorCount;

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting FileSystemMonitor health");
                return new DetectorHealth
                {
                    DetectorName = "FileSystemMonitor",
                    IsHealthy = false,
                    StatusMessage = $"Error: {ex.Message}",
                    LastError = ex
                };
            }
        }

        /// <summary>
        /// Gets monitor statistics
        /// </summary>
        public async Task<FileSystemMonitorStatistics> GetStatisticsAsync()
        {
            lock (_lock)
            {
                return new FileSystemMonitorStatistics
                {
                    TotalActivitiesMonitored = Statistics.TotalActivitiesMonitored,
                    FilesCreated = Statistics.FilesCreated,
                    FilesModified = Statistics.FilesModified,
                    FilesDeleted = Statistics.FilesDeleted,
                    FilesRenamed = Statistics.FilesRenamed,
                    DirectoriesCreated = Statistics.DirectoriesCreated,
                    DirectoriesModified = Statistics.DirectoriesModified,
                    DirectoriesDeleted = Statistics.DirectoriesDeleted,
                    SuccessfulDetections = Statistics.SuccessfulDetections,
                    FailedDetections = Statistics.FailedDetections,
                    ErrorCount = Statistics.ErrorCount,
                    StartTime = Statistics.StartTime,
                    LastActivityTime = Statistics.LastActivityTime
                };
            }
        }

        /// <summary>
        /// Resets monitor statistics
        /// </summary>
        public async Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    Statistics.Reset();
                    _processHistories.Clear();
                }

                _logger.LogInformation("FileSystemMonitor statistics reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting FileSystemMonitor statistics");
                throw;
            }
        }

        #region Private Methods

        /// <summary>
        /// Sets up file system watchers for SolidWorks directories
        /// </summary>
        private async Task SetupFileSystemWatchersAsync()
        {
            try
            {
                var directoriesToWatch = new List<string>();

                // Add SolidWorks-specific directories
                if (_config.WatchSolidWorksDirectories)
                {
                    directoriesToWatch.AddRange(await GetSolidWorksDirectoriesAsync());
                }

                // Add temp directories
                if (_config.WatchTempDirectories)
                {
                    directoriesToWatch.AddRange(GetTempDirectories());
                }

                // Add custom directories from configuration
                if (_config.CustomWatchDirectories?.Any() == true)
                {
                    directoriesToWatch.AddRange(_config.CustomWatchDirectories);
                }

                // Remove duplicates and non-existent directories
                directoriesToWatch = directoriesToWatch
                    .Distinct()
                    .Where(Directory.Exists)
                    .ToList();

                // Create watchers for each directory
                foreach (var directory in directoriesToWatch)
                {
                    try
                    {
                        await CreateFileSystemWatcherAsync(directory);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error creating file system watcher for directory: {Directory}", directory);
                    }
                }

                _logger.LogInformation("File system watchers setup completed for {Count} directories", directoriesToWatch.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up file system watchers");
                throw;
            }
        }

        /// <summary>
        /// Gets SolidWorks-related directories
        /// </summary>
        private async Task<List<string>> GetSolidWorksDirectoriesAsync()
        {
            var directories = new List<string>();

            try
            {
                // Add common SolidWorks directories
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                // SolidWorks installation directories
                directories.AddRange(Directory.GetDirectories(programFiles, "SOLIDWORKS*", SearchOption.TopDirectoryOnly));
                directories.AddRange(Directory.GetDirectories(programFilesX86, "SOLIDWORKS*", SearchOption.TopDirectoryOnly));

                // SolidWorks data directories
                directories.Add(Path.Combine(commonAppData, "SOLIDWORKS"));
                directories.Add(Path.Combine(localAppData, "SOLIDWORKS"));

                // Add user documents directories
                var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                directories.Add(Path.Combine(myDocuments, "SOLIDWORKS"));

                // Look for SolidWorks processes and get their working directories
                var solidWorksProcesses = System.Diagnostics.Process.GetProcessesByName("SLDWORKS");
                foreach (var process in solidWorksProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            // Get process working directory
                            var workingDir = GetProcessWorkingDirectory(process.Id);
                            if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                            {
                                directories.Add(workingDir);
                            }

                            // Get process main module path and add its directory
                            var mainModule = process.MainModule?.FileName;
                            if (!string.IsNullOrEmpty(mainModule))
                            {
                                var moduleDir = Path.GetDirectoryName(mainModule);
                                if (!string.IsNullOrEmpty(moduleDir) && Directory.Exists(moduleDir))
                                {
                                    directories.Add(moduleDir);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error getting directory from SolidWorks process {ProcessId}", process.Id);
                    }
                }

                return directories.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SolidWorks directories");
                return directories;
            }
        }

        /// <summary>
        /// Gets temporary directories to watch
        /// </summary>
        private List<string> GetTempDirectories()
        {
            var directories = new List<string>();

            try
            {
                // System temp directory
                var tempPath = Path.GetTempPath();
                if (Directory.Exists(tempPath))
                {
                    directories.Add(tempPath);
                }

                // User temp directory
                var localTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
                if (Directory.Exists(localTemp))
                {
                    directories.Add(localTemp);
                }

                // Windows temp directory
                var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                if (Directory.Exists(windowsTemp))
                {
                    directories.Add(windowsTemp);
                }

                return directories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting temp directories");
                return directories;
            }
        }

        /// <summary>
        /// Creates a file system watcher for a specific directory
        /// </summary>
        private async Task CreateFileSystemWatcherAsync(string directory)
        {
            try
            {
                var watcher = new FileSystemWatcher(directory)
                {
                    IncludeSubdirectories = _config.IncludeSubdirectories,
                    EnableRaisingEvents = false // Start disabled
                };

                // Set filters
                watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                    NotifyFilters.LastWrite | NotifyFilters.CreationTime |
                                    NotifyFilters.Size | NotifyFilters.Attributes;

                // Filter for SolidWorks files and common document types
                watcher.Filter = _config.FileFilter;

                // Wire up event handlers
                watcher.Created += OnFileSystemChanged;
                watcher.Changed += OnFileSystemChanged;
                watcher.Deleted += OnFileSystemChanged;
                watcher.Renamed += OnFileSystemRenamed;
                watcher.Error += OnWatcherError;

                lock (_lock)
                {
                    _watchers[directory] = watcher;
                }

                _logger.LogDebug("Created file system watcher for directory: {Directory}", directory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating file system watcher for directory: {Directory}", directory);
                throw;
            }
        }

        /// <summary>
        /// Starts all file system watchers
        /// </summary>
        private void StartFileSystemWatchers()
        {
            lock (_lock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = true;
                        _logger.LogDebug("Started file system watcher for directory: {Directory}", watcher.Path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error starting file system watcher for directory: {Directory}", watcher.Path);
                    }
                }
            }
        }

        /// <summary>
        /// Stops all file system watchers
        /// </summary>
        private void StopFileSystemWatchers()
        {
            lock (_lock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        _logger.LogDebug("Stopped file system watcher for directory: {Directory}", watcher.Path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping file system watcher for directory: {Directory}", watcher.Path);
                    }
                }
            }
        }

        /// <summary>
        /// Pauses all file system watchers
        /// </summary>
        private void PauseFileSystemWatchers()
        {
            lock (_lock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        _logger.LogDebug("Paused file system watcher for directory: {Directory}", watcher.Path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error pausing file system watcher for directory: {Directory}", watcher.Path);
                    }
                }
            }
        }

        /// <summary>
        /// Resumes all file system watchers
        /// </summary>
        private void ResumeFileSystemWatchers()
        {
            StartFileSystemWatchers();
        }

        /// <summary>
        /// Main monitoring loop for active processes
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting file system monitoring loop");

            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    // Wait for the monitoring interval
                    await Task.Delay(TimeSpan.FromSeconds(_config.MonitoringIntervalSeconds), cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Monitor active SolidWorks processes
                    await MonitorActiveSolidWorksProcessesAsync(cancellationToken);

                    // Clean up old file system histories
                    CleanupOldFileSystemHistories();
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in file system monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Delay on error
                }
            }

            _logger.LogDebug("File system monitoring loop stopped");
        }

        /// <summary>
        /// Monitors active SolidWorks processes for file system activity
        /// </summary>
        private async Task MonitorActiveSolidWorksProcessesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var solidWorksProcesses = System.Diagnostics.Process.GetProcessesByName("SLDWORKS")
                    .Where(p => !p.HasExited && p.Responding)
                    .ToList();

                foreach (var process in solidWorksProcesses)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        // Check for recent file changes associated with this process
                        await CheckProcessFileActivityAsync(process, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error monitoring process {ProcessId}", process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring active SolidWorks processes");
            }
        }

        /// <summary>
        /// Checks for file activity associated with a specific process
        /// </summary>
        private async Task CheckProcessFileActivityAsync(System.Diagnostics.Process process, CancellationToken cancellationToken)
        {
            try
            {
                // Get process working directory and check for recent file changes
                var workingDir = GetProcessWorkingDirectory(process.Id);
                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    var recentFiles = GetRecentModifiedFiles(workingDir, TimeSpan.FromMinutes(5));
                    foreach (var file in recentFiles)
                    {
                        await RecordFileActivityAsync(process.Id, file.FullName, "Modified", file.Length, "ProcessWorkingDirectory", cancellationToken);
                    }
                }

                // Check process main window title for file references
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    var filePath = ExtractFilePathFromWindowTitle(process.MainWindowTitle);
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        await RecordFileActivityAsync(process.Id, filePath, "WindowReference", fileInfo.Length, "WindowTitle", cancellationToken);
                    }
                }

                // Check process command line for file arguments
                try
                {
                    var commandLine = GetProcessCommandLine(process.Id);
                    if (!string.IsNullOrEmpty(commandLine))
                    {
                        var filePaths = ExtractFilePathsFromCommandLine(commandLine);
                        foreach (var filePath in filePaths)
                        {
                            if (File.Exists(filePath))
                            {
                                var fileInfo = new FileInfo(filePath);
                                await RecordFileActivityAsync(process.Id, filePath, "CommandLineArgument", fileInfo.Length, "CommandLine", cancellationToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error getting command line for process {ProcessId}", process.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file activity for process {ProcessId}", process.Id);
            }
        }

        /// <summary>
        /// Gets files recently modified in a directory
        /// </summary>
        private List<FileInfo> GetRecentModifiedFiles(string directory, TimeSpan timeWindow)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                var extensions = _config.MonitoredFileExtensions ?? new[] { "*.sldprt", "*.sldasm", "*.slddrw" };

                var recentFiles = new List<FileInfo>();

                foreach (var extension in extensions)
                {
                    try
                    {
                        var files = Directory.GetFiles(directory, extension, SearchOption.AllDirectories)
                            .Where(f => File.GetLastWriteTimeUtc(f) > cutoffTime)
                            .Select(f => new FileInfo(f))
                            .Where(f => f.Length <= _config.MaxFileSizeToMonitorBytes)
                            .ToList();

                        recentFiles.AddRange(files);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip directories we don't have access to
                        _logger.LogDebug("Unauthorized access to directory: {Directory}", directory);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error searching for {Extension} files in {Directory}", extension, directory);
                    }
                }

                return recentFiles.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent modified files from {Directory}", directory);
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// Extracts file path from window title
        /// </summary>
        private string ExtractFilePathFromWindowTitle(string windowTitle)
        {
            try
            {
                // Look for file paths in window title
                // SolidWorks typically shows "DocumentName.sldprt - SolidWorks"
                var parts = windowTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && parts[0] != "SolidWorks")
                {
                    var possiblePath = parts[0].Trim();

                    // Check if it looks like a full path
                    if (possiblePath.Contains("\\") &&
                        (possiblePath.EndsWith(".sldprt") || possiblePath.EndsWith(".sldasm") || possiblePath.EndsWith(".slddrw")))
                    {
                        return possiblePath;
                    }

                    // Check if it's just a document name that might exist
                    if (possiblePath.EndsWith(".sldprt") || possiblePath.EndsWith(".sldasm") || possiblePath.EndsWith(".slddrw"))
                    {
                        // Try to find this file in common locations
                        var searchPaths = new[]
                        {
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                            Directory.GetCurrentDirectory()
                        };

                        foreach (var searchPath in searchPaths)
                        {
                            var fullPath = Path.Combine(searchPath, possiblePath);
                            if (File.Exists(fullPath))
                            {
                                return fullPath;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error extracting file path from window title: {Title}", windowTitle);
                return null;
            }
        }

        /// <summary>
        /// Extracts file paths from command line
        /// </summary>
        private List<string> ExtractFilePathsFromCommandLine(string commandLine)
        {
            try
            {
                var filePaths = new List<string>();
                var extensions = new[] { ".sldprt", ".sldasm", ".slddrw" };

                // Simple command line parsing to extract file paths
                var args = commandLine.Split('"');
                for (int i = 1; i < args.Length; i += 2)
                {
                    var arg = args[i].Trim();
                    if (extensions.Any(ext => arg.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (File.Exists(arg))
                        {
                            filePaths.Add(arg);
                        }
                    }
                }

                return filePaths;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error extracting file paths from command line: {Args}", commandLine);
                return new List<string>();
            }
        }

        /// <summary>
        /// Records file system activity
        /// </summary>
        private async Task RecordFileActivityAsync(int processId, string filePath, string changeType, long fileSize, string operation, CancellationToken cancellationToken)
        {
            try
            {
                var activity = new FileSystemActivityData
                {
                    ProcessId = processId,
                    FilePath = filePath,
                    ChangeType = changeType,
                    FileSize = fileSize,
                    Timestamp = DateTime.UtcNow,
                    Confidence = CalculateActivityConfidence(operation, fileSize),
                    Operation = operation
                };

                // Get or create file system history
                var history = _processHistories.GetOrAdd(processId, id => new ProcessFileSystemHistory(id));
                history.RecordActivity(activity);

                // Update statistics
                lock (_lock)
                {
                    Statistics.TotalActivitiesMonitored++;
                    Statistics.LastActivityTime = DateTime.UtcNow;

                    switch (changeType.ToLower())
                    {
                        case "created":
                            Statistics.FilesCreated++;
                            break;
                        case "modified":
                            Statistics.FilesModified++;
                            break;
                        case "deleted":
                            Statistics.FilesDeleted++;
                            break;
                        case "renamed":
                            Statistics.FilesRenamed++;
                            break;
                    }

                    Statistics.SuccessfulDetections++;
                }

                // Raise event
                FileSystemActivityDetected?.Invoke(this, new FileSystemActivityEventArgs
                {
                    ProcessId = processId,
                    FilePath = filePath,
                    ChangeType = changeType,
                    FileSize = fileSize,
                    Timestamp = DateTime.UtcNow,
                    Confidence = activity.Confidence,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["Operation"] = operation,
                        ["Extension"] = Path.GetExtension(filePath),
                        ["DirectoryName"] = Path.GetDirectoryName(filePath)
                    }
                });

                _logger.LogDebug("File system activity recorded: Process={ProcessId}, File={FilePath}, Change={ChangeType}",
                    processId, filePath, changeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording file system activity for process {ProcessId}", processId);
                lock (_lock)
                {
                    Statistics.ErrorCount++;
                    Statistics.FailedDetections++;
                }
            }
        }

        /// <summary>
        /// Calculates activity confidence based on operation and file size
        /// </summary>
        private double CalculateActivityConfidence(string operation, long fileSize)
        {
            try
            {
                var baseConfidence = 0.5;

                // Adjust confidence based on operation type
                switch (operation.ToLower())
                {
                    case "windowreference":
                        baseConfidence = 0.9;
                        break;
                    case "processworkingdirectory":
                        baseConfidence = 0.8;
                        break;
                    case "commandline":
                        baseConfidence = 0.7;
                        break;
                    case "filesystemwatcher":
                        baseConfidence = 0.6;
                        break;
                }

                // Adjust confidence based on file size (larger files = higher confidence)
                if (fileSize > 0)
                {
                    var sizeFactor = Math.Min(1.0, fileSize / 1024.0 / 1024.0); // Normalize to MB
                    baseConfidence = Math.Min(1.0, baseConfidence + sizeFactor * 0.2);
                }

                return baseConfidence;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating activity confidence");
                return 0.5;
            }
        }

        /// <summary>
        /// Gets the working directory of a process
        /// </summary>
        private string GetProcessWorkingDirectory(int processId)
        {
            try
            {
                // This is a simplified approach - in a real implementation, you might need
                // more advanced Windows API calls to get the actual working directory
                using var process = System.Diagnostics.Process.GetProcessById(processId);
                var processPath = process.MainModule?.FileName;

                if (!string.IsNullOrEmpty(processPath))
                {
                    return Path.GetDirectoryName(processPath);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting working directory for process {ProcessId}", processId);
                return null;
            }
        }

        /// <summary>
        /// Gets the command line for a process
        /// </summary>
        private string GetProcessCommandLine(int processId)
        {
            try
            {
                // This is a simplified approach - in a real implementation, you might need
                // Windows Management Instrumentation (WMI) or other APIs
                using var process = System.Diagnostics.Process.GetProcessById(processId);
                return process.StartInfo?.Arguments;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting command line for process {ProcessId}", processId);
                return null;
            }
        }

        /// <summary>
        /// Cleans up old file system histories
        /// </summary>
        private void CleanupOldFileSystemHistories()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-2);
                var toRemove = new List<int>();

                foreach (var kvp in _processHistories)
                {
                    if (kvp.Value.LastActivityTime < cutoffTime)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var processId in toRemove)
                {
                    _processHistories.TryRemove(processId, out _);
                }

                if (toRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old file system histories", toRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in file system history cleanup");
            }
        }

        /// <summary>
        /// Timer callback for cleaning up old file system histories
        /// </summary>
        private void CleanupTimerCallback(object state)
        {
            try
            {
                CleanupOldFileSystemHistories();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup timer callback");
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles file system change events
        /// </summary>
        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (!_isRunning) return;

                var fileInfo = new FileInfo(e.FullPath);
                if (fileInfo.Exists && fileInfo.Length > _config.MaxFileSizeToMonitorBytes)
                {
                    // Skip files that are too large
                    return;
                }

                // Find associated SolidWorks processes
                var solidWorksProcesses = System.Diagnostics.Process.GetProcessesByName("SLDWORKS")
                    .Where(p => !p.HasExited && p.Responding)
                    .ToList();

                foreach (var process in solidWorksProcesses)
                {
                    try
                    {
                        // Check if this file change might be related to the process
                        if (IsFileRelatedToProcess(process, e.FullPath))
                        {
                            RecordFileActivityAsync(process.Id, e.FullPath, e.ChangeType.ToString(),
                                fileInfo.Exists ? fileInfo.Length : 0, "FileSystemWatcher", CancellationToken.None).Wait();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file system change for process {ProcessId}", process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file system change event: {FullPath}, ChangeType: {ChangeType}",
                    e.FullPath, e.ChangeType);
            }
        }

        /// <summary>
        /// Handles file system rename events
        /// </summary>
        private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                if (!_isRunning) return;

                var fileInfo = new FileInfo(e.FullPath);
                if (fileInfo.Exists && fileInfo.Length > _config.MaxFileSizeToMonitorBytes)
                {
                    // Skip files that are too large
                    return;
                }

                // Find associated SolidWorks processes
                var solidWorksProcesses = System.Diagnostics.Process.GetProcessesByName("SLDWORKS")
                    .Where(p => !p.HasExited && p.Responding)
                    .ToList();

                foreach (var process in solidWorksProcesses)
                {
                    try
                    {
                        // Check if this file rename might be related to the process
                        if (IsFileRelatedToProcess(process, e.FullPath) || IsFileRelatedToProcess(process, e.OldFullPath))
                        {
                            RecordFileActivityAsync(process.Id, e.FullPath, "Renamed",
                                fileInfo.Exists ? fileInfo.Length : 0, "FileSystemWatcher", CancellationToken.None).Wait();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file system rename for process {ProcessId}", process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file system rename event: {OldPath} -> {FullPath}",
                    e.OldFullPath, e.FullPath);
            }
        }

        /// <summary>
        /// Handles file system watcher error events
        /// </summary>
        private async void OnWatcherError(object sender, ErrorEventArgs e)
        {
            try
            {
                var watcher = sender as FileSystemWatcher;
                _logger.LogError(e.GetException(), "File system watcher error for directory: {Directory}",
                    watcher?.Path ?? "unknown");

                // Try to restart the watcher
                if (watcher != null)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        await Task.Delay(1000); // Wait before restarting
                        watcher.EnableRaisingEvents = true;
                        _logger.LogInformation("Restarted file system watcher for directory: {Directory}", watcher.Path);
                    }
                    catch (Exception restartEx)
                    {
                        _logger.LogError(restartEx, "Failed to restart file system watcher for directory: {Directory}", watcher.Path);
                    }
                }

                lock (_lock)
                {
                    Statistics.ErrorCount++;
                    Statistics.FailedDetections++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file system watcher error");
            }
        }

        /// <summary>
        /// Checks if a file is related to a specific process
        /// </summary>
        private bool IsFileRelatedToProcess(System.Diagnostics.Process process, string filePath)
        {
            try
            {
                // Check if file is in process working directory
                var workingDir = GetProcessWorkingDirectory(process.Id);
                if (!string.IsNullOrEmpty(workingDir) && filePath.StartsWith(workingDir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check if file extension matches monitored types
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var monitoredExtensions = _config.MonitoredFileExtensions ??
                    new[] { "*.sldprt", "*.sldasm", "*.slddrw" };

                if (monitoredExtensions.Any(ext => ext.ToLowerInvariant().Contains(extension)))
                {
                    return true;
                }

                // Check if file is referenced in process window title
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    var fileName = Path.GetFileName(filePath);
                    return process.MainWindowTitle.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file is related to process {ProcessId}", process.Id);
                return false;
            }
        }

        #endregion

        #region IDisposable Implementation

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
                    _logger.LogInformation("Disposing FileSystemMonitor");

                    try
                    {
                        StopAsync().Wait(TimeSpan.FromSeconds(5));
                        _cleanupTimer?.Dispose();
                        _cancellationTokenSource?.Dispose();

                        foreach (var watcher in _watchers.Values)
                        {
                            watcher?.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during disposal");
                    }
                }

                _isDisposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Configuration for file system monitoring
    /// </summary>
    public class FileSystemMonitorConfig
    {
        /// <summary>
        /// Gets or sets whether file system monitoring is enabled
        /// </summary>
        public bool EnableFileSystemMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to watch SolidWorks directories
        /// </summary>
        public bool WatchSolidWorksDirectories { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to watch temporary directories
        /// </summary>
        public bool WatchTempDirectories { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to include subdirectories
        /// </summary>
        public bool IncludeSubdirectories { get; set; } = true;

        /// <summary>
        /// Gets or sets the file filter for monitoring
        /// </summary>
        public string FileFilter { get; set; } = "*.sld*";

        /// <summary>
        /// Gets or sets the monitored file extensions
        /// </summary>
        public string[] MonitoredFileExtensions { get; set; } = new[] { "*.sldprt", "*.sldasm", "*.slddrw" };

        /// <summary>
        /// Gets or sets custom directories to watch
        /// </summary>
        public string[] CustomWatchDirectories { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the maximum file size to monitor in bytes
        /// </summary>
        public long MaxFileSizeToMonitorBytes { get; set; } = 100 * 1024 * 1024; // 100MB

        /// <summary>
        /// Gets or sets the monitoring interval in seconds
        /// </summary>
        public int MonitoringIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the activity confidence threshold
        /// </summary>
        public double ActivityConfidenceThreshold { get; set; } = 0.5;

        /// <summary>
        /// Initializes a new instance of the FileSystemMonitorConfig class
        /// </summary>
        public FileSystemMonitorConfig()
        {
            CustomWatchDirectories = Array.Empty<string>();
            MonitoredFileExtensions = Array.Empty<string>();
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"FileSystemMonitor[Enabled={EnableFileSystemMonitoring}, SolidWorksDirs={WatchSolidWorksDirectories}, " +
                   $"TempDirs={WatchTempDirectories}, IncludeSubdirs={IncludeSubdirectories}]";
        }
    }

    /// <summary>
    /// Statistics for the file system monitor
    /// </summary>
    public class FileSystemMonitorStatistics
    {
        public long TotalActivitiesMonitored { get; set; }
        public long FilesCreated { get; set; }
        public long FilesModified { get; set; }
        public long FilesDeleted { get; set; }
        public long FilesRenamed { get; set; }
        public long DirectoriesCreated { get; set; }
        public long DirectoriesModified { get; set; }
        public long DirectoriesDeleted { get; set; }
        public long SuccessfulDetections { get; set; }
        public long FailedDetections { get; set; }
        public long ErrorCount { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? LastActivityTime { get; set; }

        public FileSystemMonitorStatistics()
        {
            StartTime = DateTime.UtcNow;
        }

        public void Reset()
        {
            TotalActivitiesMonitored = 0;
            FilesCreated = 0;
            FilesModified = 0;
            FilesDeleted = 0;
            FilesRenamed = 0;
            DirectoriesCreated = 0;
            DirectoriesModified = 0;
            DirectoriesDeleted = 0;
            SuccessfulDetections = 0;
            FailedDetections = 0;
            ErrorCount = 0;
            StartTime = DateTime.UtcNow;
            LastActivityTime = null;
        }
    }

    /// <summary>
    /// Represents file system activity data
    /// </summary>
    public class FileSystemActivityData
    {
        public int ProcessId { get; set; }
        public string FilePath { get; set; }
        public string ChangeType { get; set; }
        public long FileSize { get; set; }
        public DateTime Timestamp { get; set; }
        public double Confidence { get; set; }
        public string Operation { get; set; }

        public FileSystemActivityData()
        {
            Timestamp = DateTime.UtcNow;
            Confidence = 0.5;
        }
    }

    /// <summary>
    /// Maintains file system activity history for a specific process
    /// </summary>
    public class ProcessFileSystemHistory
    {
        private readonly object _lock = new object();
        private readonly Queue<FileSystemActivityData> _recentActivities = new Queue<FileSystemActivityData>(100);
        private readonly int _processId;

        public int ProcessId => _processId;
        public DateTime LastActivityTime { get; private set; }
        public int TotalActivities { get; set; }

        public ProcessFileSystemHistory(int processId)
        {
            _processId = processId;
            LastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Records file system activity
        /// </summary>
        public void RecordActivity(FileSystemActivityData activity)
        {
            lock (_lock)
            {
                _recentActivities.Enqueue(activity);
                if (_recentActivities.Count > 100)
                    _recentActivities.Dequeue();

                LastActivityTime = activity.Timestamp;
                TotalActivities++;
            }
        }

        /// <summary>
        /// Gets recent activities within the specified time window
        /// </summary>
        public List<FileSystemActivityData> GetRecentActivities(TimeSpan timeWindow)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                return _recentActivities.Where(a => a.Timestamp > cutoffTime).ToList();
            }
        }
    }
}