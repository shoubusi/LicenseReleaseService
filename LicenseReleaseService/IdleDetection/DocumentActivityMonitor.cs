using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Monitors document-related activity for SolidWorks processes
    /// </summary>
    public class DocumentActivityMonitor : IDisposable
    {
        private readonly ILogger<DocumentActivityMonitor> _logger;
        private readonly PingBasedDetectionConfig _config;
        private readonly object _lock = new object();
        private readonly Dictionary<int, DocumentActivityHistory> _activityHistories = new Dictionary<int, DocumentActivityHistory>();
        private readonly Timer _cleanupTimer;
        private readonly FileSystemWatcher[] _fileWatchers = new FileSystemWatcher[0];
        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitoringTask;
        private bool _isDisposed;

        #region Windows API Imports for Document Monitoring

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags,
            System.Text.StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IntPtr ppbc);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(uint reserved, out IntPtr prot);

        [DllImport("ole32.dll")]
        private static extern int EnumRunning(IntPtr prot, out IntPtr ppenum);

        [DllImport("ole32.dll")]
        private static extern int IEnumRunningObject_Next(IntPtr ppenum, uint celt, IMoniker[] rgelt, out uint pceltFetched);

        [DllImport("ole32.dll")]
        private static extern int IEnumRunningObject_Reset(IntPtr ppenum);

        [DllImport("ole32.dll")]
        private static extern int BindMoniker(IntPtr pmoniker, uint grfOpt, ref Guid iid, out IntPtr ppv);

        [ComImport, Guid("0000000e-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IEnumRunningObjectTable
        {
            [PreserveSig]
            int Next(uint celt, IMoniker[] rgelt, out uint pceltFetched);
            [PreserveSig]
            int Skip(uint celt);
            [PreserveSig]
            int Reset();
            [PreserveSig]
            int Clone(out IEnumRunningObjectTable ppenum);
        }

        [ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMoniker
        {
            // Simplified interface for our needs
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when document activity is detected
        /// </summary>
        public event EventHandler<DocumentActivityData> DocumentActivityDetected;

        /// <summary>
        /// Gets a value indicating whether the monitor is running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the monitor statistics
        /// </summary>
        public DocumentActivityStatistics Statistics { get; private set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the DocumentActivityMonitor class
        /// </summary>
        public DocumentActivityMonitor(ILogger<DocumentActivityMonitor> logger, PingBasedDetectionConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Statistics = new DocumentActivityStatistics();

            // Initialize cleanup timer for old activity histories
            _cleanupTimer = new Timer(CleanupTimerCallback, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

            _logger.LogInformation("DocumentActivityMonitor initialized with configuration: {Config}", config);
        }

        /// <summary>
        /// Initializes the monitor with configuration
        /// </summary>
        public async Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing DocumentActivityMonitor");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("DocumentActivityMonitor is already running");
                        return;
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Clear any existing activity histories
                lock (_lock)
                {
                    _activityHistories.Clear();
                    Statistics.Reset();
                }

                // Setup file system watchers for common SolidWorks document directories
                await SetupFileSystemWatchersAsync();

                _logger.LogInformation("DocumentActivityMonitor initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing DocumentActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Starts the document activity monitoring
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting DocumentActivityMonitor");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("DocumentActivityMonitor is already running");
                        return;
                    }

                    IsRunning = true;
                }

                // Start file system watchers
                StartFileSystemWatchers();

                // Start monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                _logger.LogInformation("DocumentActivityMonitor started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting DocumentActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Stops the document activity monitoring
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping DocumentActivityMonitor");

                lock (_lock)
                {
                    if (!IsRunning)
                    {
                        _logger.LogWarning("DocumentActivityMonitor is not running");
                        return;
                    }

                    IsRunning = false;
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

                _logger.LogInformation("DocumentActivityMonitor stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping DocumentActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Pauses the document activity monitoring
        /// </summary>
        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Pausing DocumentActivityMonitor");

                lock (_lock)
                {
                    if (!IsRunning)
                    {
                        _logger.LogWarning("DocumentActivityMonitor is not running");
                        return;
                    }

                    IsRunning = false;
                }

                // Pause file system watchers
                PauseFileSystemWatchers();

                _cancellationTokenSource?.Cancel();

                _logger.LogInformation("DocumentActivityMonitor paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing DocumentActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Resumes the document activity monitoring
        /// </summary>
        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Resuming DocumentActivityMonitor");

                lock (_lock)
                {
                    if (IsRunning)
                    {
                        _logger.LogWarning("DocumentActivityMonitor is already running");
                        return;
                    }

                    IsRunning = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Resume file system watchers
                ResumeFileSystemWatchers();

                // Restart monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                _logger.LogInformation("DocumentActivityMonitor resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming DocumentActivityMonitor");
                throw;
            }
        }

        /// <summary>
        /// Gets recent document activities for a process
        /// </summary>
        public async Task<IList<DocumentActivityData>> GetRecentActivitiesAsync(int processId, TimeSpan timeWindow, CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    if (_activityHistories.TryGetValue(processId, out var history))
                    {
                        return history.GetRecentActivities(timeWindow);
                    }
                }

                return new List<DocumentActivityData>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent document activities for process {ProcessId}", processId);
                return new List<DocumentActivityData>();
            }
        }

        /// <summary>
        /// Monitors document activity in a continuous loop
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsRunning)
            {
                try
                {
                    // Monitor active SolidWorks processes
                    await MonitorActiveSolidWorksProcessesAsync(cancellationToken);

                    // Check for document changes via COM objects
                    await CheckCOMDocumentObjectsAsync(cancellationToken);

                    // Wait before next check
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in document activity monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Delay on error
                }
            }
        }

        /// <summary>
        /// Monitors active SolidWorks processes for document activity
        /// </summary>
        private async Task MonitorActiveSolidWorksProcessesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var solidWorksProcesses = GetSolidWorksProcesses();
                var foregroundWindow = GetForegroundWindow();

                foreach (var process in solidWorksProcesses)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        // Check if this process has the foreground window
                        var hasForeground = false;
                        var foregroundWindowTitle = string.Empty;

                        if (foregroundWindow != IntPtr.Zero)
                        {
                            GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId);
                            hasForeground = foregroundProcessId == process.Id;
                            foregroundWindowTitle = GetWindowTitle(foregroundWindow);
                        }

                        // Get process-specific document information
                        var documentInfo = await GetProcessDocumentInfoAsync(process, cancellationToken);

                        // Record activity if documents are open or process has focus
                        if (documentInfo.HasOpenDocuments || hasForeground)
                        {
                            var activity = new DocumentActivityData
                            {
                                ProcessId = process.Id,
                                ActivityType = hasForeground ? "WindowFocus" : "DocumentOpen",
                                DocumentPath = documentInfo.MainDocumentPath,
                                Timestamp = DateTime.UtcNow,
                                Confidence = hasForeground ? 0.9 : 0.7,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["HasForeground"] = hasForeground,
                                    ["WindowTitle"] = foregroundWindowTitle,
                                    ["OpenDocumentCount"] = documentInfo.OpenDocumentCount,
                                    ["ProcessName"] = process.ProcessName,
                                    ["MainWindowTitle"] = process.MainWindowTitle
                                }
                            };

                            await RecordDocumentActivityAsync(activity);
                        }
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
        /// Gets all running SolidWorks processes
        /// </summary>
        private List<Process> GetSolidWorksProcesses()
        {
            try
            {
                return Process.GetProcessesByName("SLDWORKS")
                    .Where(p => !p.HasExited)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SolidWorks processes");
                return new List<Process>();
            }
        }

        /// <summary>
        /// Gets document information for a specific process
        /// </summary>
        private async Task<ProcessDocumentInfo> GetProcessDocumentInfoAsync(Process process, CancellationToken cancellationToken)
        {
            var info = new ProcessDocumentInfo();

            try
            {
                // Method 1: Check process main window title for document names
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    info.MainDocumentPath = ExtractDocumentPathFromWindowTitle(process.MainWindowTitle);
                    info.HasOpenDocuments = !string.IsNullOrEmpty(info.MainDocumentPath);
                    info.OpenDocumentCount = info.HasOpenDocuments ? 1 : 0;
                }

                // Method 2: Check process command line arguments
                if (process.StartInfo != null && !string.IsNullOrEmpty(process.StartInfo.Arguments))
                {
                    var documentFromArgs = ExtractDocumentPathFromArguments(process.StartInfo.Arguments);
                    if (!string.IsNullOrEmpty(documentFromArgs))
                    {
                        info.MainDocumentPath = documentFromArgs;
                        info.HasOpenDocuments = true;
                        info.OpenDocumentCount = Math.Max(info.OpenDocumentCount, 1);
                    }
                }

                // Method 3: Check process working directory
                try
                {
                    var workingDir = GetProcessWorkingDirectory(process.Id);
                    if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                    {
                        info.WorkingDirectory = workingDir;

                        // Look for recent document files in working directory
                        var recentDocs = GetRecentDocumentFiles(workingDir, TimeSpan.FromHours(1));
                        if (recentDocs.Any())
                        {
                            info.HasOpenDocuments = true;
                            info.OpenDocumentCount = Math.Max(info.OpenDocumentCount, recentDocs.Count);
                            info.RecentDocuments = recentDocs;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error getting process working directory for process {ProcessId}", process.Id);
                }

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document info for process {ProcessId}", process.Id);
                return info;
            }
        }

        /// <summary>
        /// Extracts document path from window title
        /// </summary>
        private string ExtractDocumentPathFromWindowTitle(string windowTitle)
        {
            try
            {
                // SolidWorks typically shows document name in title like: "Part1 - SolidWorks"
                // or "C:\\Projects\\Part1.sldprt - SolidWorks"
                var parts = windowTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && parts[0] != "SolidWorks")
                {
                    var documentPath = parts[0].Trim();

                    // Check if it looks like a full path
                    if (documentPath.Contains("\\") && (documentPath.EndsWith(".sldprt") ||
                        documentPath.EndsWith(".sldasm") || documentPath.EndsWith(".slddrw")))
                    {
                        return documentPath;
                    }

                    // Check if it's just a document name
                    if (documentPath.EndsWith(".sldprt") || documentPath.EndsWith(".sldasm") ||
                        documentPath.EndsWith(".slddrw"))
                    {
                        return documentPath;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error extracting document path from window title: {Title}", windowTitle);
                return null;
            }
        }

        /// <summary>
        /// Extracts document path from command line arguments
        /// </summary>
        private string ExtractDocumentPathFromArguments(string arguments)
        {
            try
            {
                // Look for SolidWorks document extensions in arguments
                var solidWorksExtensions = new[] { ".sldprt", ".sldasm", ".slddrw" };
                var args = arguments.Split(' ');

                foreach (var arg in args)
                {
                    var cleanArg = arg.Trim('"', ' ', '\t');
                    if (solidWorksExtensions.Any(ext => cleanArg.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (File.Exists(cleanArg))
                        {
                            return cleanArg;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error extracting document path from arguments: {Args}", arguments);
                return null;
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
                using var process = Process.GetProcessById(processId);
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
        /// Gets recent document files in a directory
        /// </summary>
        private List<string> GetRecentDocumentFiles(string directory, TimeSpan timeWindow)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                var extensions = new[] { "*.sldprt", "*.sldasm", "*.slddrw" };

                var recentFiles = new List<string>();

                foreach (var extension in extensions)
                {
                    try
                    {
                        var files = Directory.GetFiles(directory, extension)
                            .Where(f => File.GetLastWriteTimeUtc(f) > cutoffTime)
                            .ToList();

                        recentFiles.AddRange(files);
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
                _logger.LogError(ex, "Error getting recent document files from {Directory}", directory);
                return new List<string>();
            }
        }

        /// <summary>
        /// Checks for document objects via COM (simplified implementation)
        /// </summary>
        private async Task CheckCOMDocumentObjectsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // This is a placeholder for COM-based document detection
                // In a full implementation, you would:
                // 1. Connect to the Running Object Table (ROT)
                // 2. Look for SolidWorks document objects
                // 3. Query their properties and state

                // For now, we'll implement a simplified version
                var rotResult = CreateBindCtx(0, out var bindCtx);
                if (rotResult == 0) // S_OK
                {
                    // Successfully created bind context
                    // In a full implementation, you would enumerate ROT here
                    Marshal.Release(bindCtx);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking COM document objects");
            }
        }

        /// <summary>
        /// Sets up file system watchers for common document directories
        /// </summary>
        private async Task SetupFileSystemWatchersAsync()
        {
            try
            {
                // This is a placeholder for file system monitoring setup
                // In a full implementation, you would:
                // 1. Identify common document directories
                // 2. Create FileSystemWatcher instances for those directories
                // 3. Handle change events for SolidWorks document files

                _logger.LogDebug("File system watchers setup would go here");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up file system watchers");
            }
        }

        /// <summary>
        /// Starts file system watchers
        /// </summary>
        private void StartFileSystemWatchers()
        {
            // Placeholder implementation
            _logger.LogDebug("Starting file system watchers");
        }

        /// <summary>
        /// Stops file system watchers
        /// </summary>
        private void StopFileSystemWatchers()
        {
            // Placeholder implementation
            _logger.LogDebug("Stopping file system watchers");
        }

        /// <summary>
        /// Pauses file system watchers
        /// </summary>
        private void PauseFileSystemWatchers()
        {
            // Placeholder implementation
            _logger.LogDebug("Pausing file system watchers");
        }

        /// <summary>
        /// Resumes file system watchers
        /// </summary>
        private void ResumeFileSystemWatchers()
        {
            // Placeholder implementation
            _logger.LogDebug("Resuming file system watchers");
        }

        /// <summary>
        /// Records document activity
        /// </summary>
        private async Task RecordDocumentActivityAsync(DocumentActivityData activity)
        {
            try
            {
                lock (_lock)
                {
                    // Get or create activity history
                    if (!_activityHistories.TryGetValue(activity.ProcessId, out var history))
                    {
                        history = new DocumentActivityHistory(activity.ProcessId);
                        _activityHistories[activity.ProcessId] = history;
                    }

                    // Record the activity
                    history.RecordActivity(activity);

                    // Update statistics
                    Statistics.TotalActivitiesMonitored++;
                    if (activity.ActivityType == "WindowFocus")
                    {
                        Statistics.WindowFocusEvents++;
                    }
                    else
                    {
                        Statistics.DocumentAccessEvents++;
                    }
                }

                // Raise event
                DocumentActivityDetected?.Invoke(this, activity);

                _logger.LogDebug("Document activity recorded: Process={ProcessId}, Activity={Activity}, Document={Document}",
                    activity.ProcessId, activity.ActivityType, activity.DocumentPath ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording document activity for process {ProcessId}", activity.ProcessId);
            }
        }

        /// <summary>
        /// Timer callback for cleaning up old activity histories
        /// </summary>
        private void CleanupTimerCallback(object state)
        {
            try
            {
                lock (_lock)
                {
                    var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(2);
                    var toRemove = new List<int>();

                    foreach (var kvp in _activityHistories)
                    {
                        if (kvp.Value.LastActivityTime < cutoffTime)
                        {
                            toRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var processId in toRemove)
                    {
                        _activityHistories.Remove(processId);
                    }

                    if (toRemove.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} old document activity histories", toRemove.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in document activity cleanup timer");
            }
        }

        /// <summary>
        /// Updates the monitor configuration
        /// </summary>
        public async Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating DocumentActivityMonitor configuration");

                if (configuration?.CustomParameters != null)
                {
                    // Update document monitoring specific parameters
                    if (configuration.CustomParameters.ContainsKey("EnableDocumentActivityMonitoring"))
                    {
                        _config.EnableDocumentActivityMonitoring = Convert.ToBoolean(configuration.CustomParameters["EnableDocumentActivityMonitoring"]);
                    }
                }

                _logger.LogInformation("DocumentActivityMonitor configuration updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DocumentActivityMonitor configuration");
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
                    DetectorName = "DocumentActivityMonitor",
                    CheckTimestamp = DateTime.UtcNow,
                    StatusMessage = "Healthy"
                };

                var isHealthy = true;
                var issues = new List<string>();

                // Check service status
                if (!IsRunning)
                {
                    issues.Add("Monitor is not running");
                    isHealthy = false;
                }

                // Check activity history size
                lock (_lock)
                {
                    if (_activityHistories.Count > 500)
                    {
                        issues.Add($"High activity history count: {_activityHistories.Count}");
                    }
                }

                // Check statistics for abnormal patterns
                if (Statistics.TotalActivitiesMonitored > 0)
                {
                    var errorRate = (double)Statistics.ErrorCount / Statistics.TotalActivitiesMonitored;
                    if (errorRate > 0.5)
                    {
                        issues.Add($"High error rate: {errorRate:P1}");
                    }
                }

                health.IsHealthy = isHealthy;
                health.StatusMessage = isHealthy ? "Healthy" : $"Issues: {string.Join(", ", issues)}";

                // Add additional info
                health.AdditionalInfo["ActivityHistoryCount"] = _activityHistories.Count;
                health.AdditionalInfo["TotalActivitiesMonitored"] = Statistics.TotalActivitiesMonitored;
                health.AdditionalInfo["WindowFocusEvents"] = Statistics.WindowFocusEvents;
                health.AdditionalInfo["DocumentAccessEvents"] = Statistics.DocumentAccessEvents;
                health.AdditionalInfo["SuccessfulDetections"] = Statistics.SuccessfulDetections;
                health.AdditionalInfo["FailedDetections"] = Statistics.FailedDetections;

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting DocumentActivityMonitor health");
                return new DetectorHealth
                {
                    DetectorName = "DocumentActivityMonitor",
                    IsHealthy = false,
                    StatusMessage = $"Error: {ex.Message}",
                    LastError = ex
                };
            }
        }

        /// <summary>
        /// Gets monitor statistics
        /// </summary>
        public async Task<DocumentActivityStatistics> GetStatisticsAsync()
        {
            lock (_lock)
            {
                return new DocumentActivityStatistics
                {
                    TotalActivitiesMonitored = Statistics.TotalActivitiesMonitored,
                    WindowFocusEvents = Statistics.WindowFocusEvents,
                    DocumentAccessEvents = Statistics.DocumentAccessEvents,
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
                    _activityHistories.Clear();
                }

                _logger.LogInformation("DocumentActivityMonitor statistics reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting DocumentActivityMonitor statistics");
                throw;
            }
        }

        /// <summary>
        /// Disposes the monitor
        /// </summary>
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
                    _logger.LogInformation("Disposing DocumentActivityMonitor");

                    StopAsync().Wait(TimeSpan.FromSeconds(5));
                    _cleanupTimer?.Dispose();
                    _cancellationTokenSource?.Dispose();

                    // Clean up file watchers
                    foreach (var watcher in _fileWatchers)
                    {
                        watcher?.Dispose();
                    }
                }

                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Maintains document activity history for a specific process
    /// </summary>
    public class DocumentActivityHistory
    {
        private readonly object _lock = new object();
        private readonly Queue<DocumentActivityData> _recentActivities = new Queue<DocumentActivityData>(100);
        private readonly int _processId;

        public int ProcessId => _processId;
        public DateTime LastActivityTime { get; private set; }
        public int TotalActivities { get; private set; }
        public DateTime? LastWindowFocus { get; private set; }
        public DateTime? LastDocumentAccess { get; private set; }
        public string MostRecentDocument { get; private set; }

        public DocumentActivityHistory(int processId)
        {
            _processId = processId;
            LastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Records document activity
        /// </summary>
        public void RecordActivity(DocumentActivityData activity)
        {
            lock (_lock)
            {
                _recentActivities.Enqueue(activity);
                if (_recentActivities.Count > 100)
                    _recentActivities.Dequeue();

                LastActivityTime = activity.Timestamp;
                TotalActivities++;

                if (activity.ActivityType == "WindowFocus")
                {
                    LastWindowFocus = activity.Timestamp;
                }
                else
                {
                    LastDocumentAccess = activity.Timestamp;
                }

                if (!string.IsNullOrEmpty(activity.DocumentPath))
                {
                    MostRecentDocument = activity.DocumentPath;
                }
            }
        }

        /// <summary>
        /// Gets recent activities within the specified time window
        /// </summary>
        public List<DocumentActivityData> GetRecentActivities(TimeSpan timeWindow)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                return _recentActivities.Where(a => a.Timestamp > cutoffTime).ToList();
            }
        }

        /// <summary>
        /// Gets the activity count within the specified time window
        /// </summary>
        public int GetRecentActivityCount(TimeSpan timeWindow)
        {
            return GetRecentActivities(timeWindow).Count;
        }

        /// <summary>
        /// Gets the time since last activity
        /// </summary>
        public TimeSpan GetTimeSinceLastActivity()
        {
            lock (_lock)
            {
                return DateTime.UtcNow - LastActivityTime;
            }
        }
    }

    /// <summary>
    /// Information about documents associated with a process
    /// </summary>
    public class ProcessDocumentInfo
    {
        public bool HasOpenDocuments { get; set; }
        public string MainDocumentPath { get; set; }
        public int OpenDocumentCount { get; set; }
        public string WorkingDirectory { get; set; }
        public List<string> RecentDocuments { get; set; }

        public ProcessDocumentInfo()
        {
            RecentDocuments = new List<string>();
        }
    }

    /// <summary>
    /// Statistics for the document activity monitor
    /// </summary>
    public class DocumentActivityStatistics
    {
        public long TotalActivitiesMonitored { get; set; }
        public long WindowFocusEvents { get; set; }
        public long DocumentAccessEvents { get; set; }
        public long SuccessfulDetections { get; set; }
        public long FailedDetections { get; set; }
        public long ErrorCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? LastActivityTime { get; set; }

        public DocumentActivityStatistics()
        {
            StartTime = DateTime.UtcNow;
        }

        public void Reset()
        {
            TotalActivitiesMonitored = 0;
            WindowFocusEvents = 0;
            DocumentAccessEvents = 0;
            SuccessfulDetections = 0;
            FailedDetections = 0;
            ErrorCount = 0;
            StartTime = DateTime.UtcNow;
            LastActivityTime = null;
        }
    }
}