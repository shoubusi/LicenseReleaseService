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
    /// Comprehensive activity monitoring service that coordinates file system, performance, and system activity monitoring
    /// </summary>
    public class ActivityMonitoringService : IIdleDetector, IDisposable
    {
        private readonly ILogger<ActivityMonitoringService> _logger;
        private readonly FileSystemMonitor _fileSystemMonitor;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly SystemActivityTracker _systemActivityTracker;
        private readonly ActivityMonitoringConfig _config;
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<int, ProcessActivityHistory> _processHistories = new ConcurrentDictionary<int, ProcessActivityHistory>();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitoringTask;
        private bool _isDisposed;

        #region IIdleDetector Properties

        public string Name => "ActivityMonitoringService";
        public string Description => "Comprehensive activity monitoring service integrating file system, performance, and system activity tracking";
        public Version Version => new Version(1, 0, 0, 0);
        public bool IsEnabled { get; private set; }
        public bool IsInitialized { get; private set; }
        public int Priority => 15; // Medium-high priority
        public IReadOnlyList<string> SupportedMethods => new List<string> { "FileSystem", "PerformanceCounters", "SystemActivity", "WindowsAPI", "ProcessMonitoring" }.AsReadOnly();
        public DetectorStatus Status { get; private set; }
        public IdleDetectorConfiguration Configuration { get; private set; }

        #endregion

        #region IIdleDetector Events

        public event EventHandler<IdleDetectionEventArgs> IdleDetected;
        public event EventHandler<IdleDetectionEventArgs> ActivityDetected;
        public event EventHandler<DetectorStatusChangedEventArgs> StatusChanged;
        public event EventHandler<DetectorErrorEventArgs> ErrorOccurred;

        #endregion

        #region Additional Events

        /// <summary>
        /// Event raised when file system activity is detected
        /// </summary>
        public event EventHandler<FileSystemActivityEventArgs> FileSystemActivityDetected;

        /// <summary>
        /// Event raised when performance metrics are updated
        /// </summary>
        public event EventHandler<PerformanceMetricsEventArgs> PerformanceMetricsUpdated;

        /// <summary>
        /// Event raised when system activity is tracked
        /// </summary>
        public event EventHandler<SystemActivityEvent> SystemActivityTracked;

        /// <summary>
        /// Event raised when comprehensive activity analysis is completed
        /// </summary>
        public event EventHandler<ActivityAnalysisEventArgs> ActivityAnalysisCompleted;

        #endregion

        /// <summary>
        /// Initializes a new instance of the ActivityMonitoringService class
        /// </summary>
        public ActivityMonitoringService(
            ILogger<ActivityMonitoringService> logger,
            FileSystemMonitor fileSystemMonitor,
            PerformanceMonitor performanceMonitor,
            SystemActivityTracker systemActivityTracker,
            ActivityMonitoringConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystemMonitor = fileSystemMonitor ?? throw new ArgumentNullException(nameof(fileSystemMonitor));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _systemActivityTracker = systemActivityTracker ?? throw new ArgumentNullException(nameof(systemActivityTracker));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Status = DetectorStatus.NotInitialized;
            IsEnabled = _config.IsEnabled;

            // Initialize cleanup timer for old process histories
            _cleanupTimer = new Timer(CleanupTimerCallback, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

            // Wire up monitor events
            _fileSystemMonitor.FileSystemActivityDetected += OnFileSystemActivityDetected;
            _performanceMonitor.PerformanceMetricsUpdated += OnPerformanceMetricsUpdated;
            _systemActivityTracker.SystemActivityTracked += OnSystemActivityTracked;

            _logger.LogInformation("ActivityMonitoringService initialized with configuration: {Config}", _config);
        }

        #region IIdleDetector Implementation

        public async Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing ActivityMonitoringService");

                lock (_lock)
                {
                    if (IsInitialized)
                    {
                        _logger.LogWarning("ActivityMonitoringService is already initialized");
                        return;
                    }

                    Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Validate configuration
                var validationResult = ValidateConfiguration(configuration);
                if (!validationResult.IsValid)
                {
                    var error = string.Join(", ", validationResult.Errors);
                    throw new InvalidOperationException($"Invalid configuration: {error}");
                }

                // Initialize all monitors
                await _fileSystemMonitor.InitializeAsync(configuration, cancellationToken);
                await _performanceMonitor.InitializeAsync(configuration, cancellationToken);
                await _systemActivityTracker.InitializeAsync();

                // Update status
                UpdateStatus(DetectorStatus.Initialized, "Initialization completed");

                IsInitialized = true;
                _logger.LogInformation("ActivityMonitoringService initialized successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("InitializeAsync", ex, ErrorSeverity.Critical);
                throw;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting ActivityMonitoringService");

                lock (_lock)
                {
                    if (!IsInitialized)
                    {
                        throw new InvalidOperationException("Service must be initialized before starting");
                    }

                    if (Status == DetectorStatus.Running)
                    {
                        _logger.LogWarning("ActivityMonitoringService is already running");
                        return;
                    }
                }

                // Start all monitors
                await _fileSystemMonitor.StartAsync(cancellationToken);
                await _performanceMonitor.StartAsync(cancellationToken);
                await _systemActivityTracker.StartAsync(cancellationToken);

                // Start monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                // Update status
                UpdateStatus(DetectorStatus.Running, "Service started");

                _logger.LogInformation("ActivityMonitoringService started successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("StartAsync", ex, ErrorSeverity.High);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Stopping ActivityMonitoringService");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Running)
                    {
                        _logger.LogWarning("ActivityMonitoringService is not running");
                        return;
                    }

                    Status = DetectorStatus.Stopped;
                }

                // Stop monitoring task
                _cancellationTokenSource?.Cancel();

                // Stop all monitors
                await _fileSystemMonitor.StopAsync(cancellationToken);
                await _performanceMonitor.StopAsync(cancellationToken);
                await _systemActivityTracker.StopAsync(cancellationToken);

                // Wait for monitoring task to complete
                if (_monitoringTask != null)
                {
                    // Wait for monitoring task with timeout using WaitAsync
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _monitoringTask.WaitAsync(timeoutCts.Token);
                }

                _logger.LogInformation("ActivityMonitoringService stopped successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("StopAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Pausing ActivityMonitoringService");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Running)
                    {
                        _logger.LogWarning("ActivityMonitoringService is not running");
                        return;
                    }

                    Status = DetectorStatus.Paused;
                }

                // Pause all monitors
                await _fileSystemMonitor.PauseAsync(cancellationToken);
                await _performanceMonitor.PauseAsync(cancellationToken);
                await _systemActivityTracker.PauseAsync(cancellationToken);

                // Cancel monitoring task
                _cancellationTokenSource?.Cancel();

                _logger.LogInformation("ActivityMonitoringService paused successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("PauseAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Resuming ActivityMonitoringService");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Paused)
                    {
                        _logger.LogWarning("ActivityMonitoringService is not paused");
                        return;
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                }

                // Resume all monitors
                await _fileSystemMonitor.ResumeAsync(cancellationToken);
                await _performanceMonitor.ResumeAsync(cancellationToken);
                await _systemActivityTracker.ResumeAsync(cancellationToken);

                // Restart monitoring task
                _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));

                // Update status
                UpdateStatus(DetectorStatus.Running, "Service resumed");

                _logger.LogInformation("ActivityMonitoringService resumed successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("ResumeAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task<IdleDetectionResult> DetectIdleAsync(int processId, string userName, string computerName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Detecting idle state for process {ProcessId}", processId);

                var sessionId = Guid.NewGuid().ToString("N")[..8];
                var activities = await CollectProcessActivitiesAsync(processId, cancellationToken);

                // Analyze activities to determine idle state
                var analysis = AnalyzeProcessActivities(processId, activities);

                var result = new IdleDetectionResult
                {
                    SessionId = sessionId,
                    ProcessId = processId,
                    UserName = userName,
                    ComputerName = computerName,
                    IsIdle = analysis.IsIdle,
                    IdleTime = analysis.IdleTime,
                    Confidence = analysis.Confidence,
                    DetectionMethod = "ActivityMonitoring",
                    DetectorName = Name,
                    Timestamp = DateTime.UtcNow,
                    Reason = analysis.Reason,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ActivityCount"] = activities.Count,
                        ["FileSystemActivities"] = activities.Count(a => a.Type == ActivityType.FileSystem),
                        ["PerformanceActivities"] = activities.Count(a => a.Type == ActivityType.Performance),
                        ["SystemActivities"] = activities.Count(a => a.Type == ActivityType.System),
                        ["LastActivityTime"] = analysis.LastActivityTime,
                        ["ActivityScore"] = analysis.ActivityScore
                    }
                };

                // Update process history
                UpdateProcessHistory(processId, result);

                // Raise appropriate events
                if (result.IsIdle)
                {
                    IdleDetected?.Invoke(this, new IdleDetectionEventArgs(
                        sessionId, processId, userName, computerName,
                        result.Confidence, result.IdleTime, result.DetectionMethod, result.Reason));
                }
                else
                {
                    ActivityDetected?.Invoke(this, new IdleDetectionEventArgs(
                        sessionId, processId, userName, computerName,
                        result.Confidence, result.IdleTime, result.DetectionMethod, result.Reason));
                }

                _logger.LogDebug("Idle detection completed for process {ProcessId}: IsIdle={IsIdle}, Confidence={Confidence:F2}",
                    processId, result.IsIdle, result.Confidence);

                return result;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("DetectIdleAsync", ex, ErrorSeverity.High);
                return new IdleDetectionResult
                {
                    SessionId = Guid.NewGuid().ToString("N")[..8],
                    ProcessId = processId,
                    UserName = userName,
                    ComputerName = computerName,
                    IsIdle = false,
                    Confidence = 0.0,
                    DetectionMethod = "ActivityMonitoring",
                    DetectorName = Name,
                    Timestamp = DateTime.UtcNow,
                    Reason = $"Error: {ex.Message}",
                    Metadata = new Dictionary<string, object>()
                };
            }
        }

        public async Task<IList<IdleDetectionResult>> DetectIdleAsync(IList<ProcessInfo> processes, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Detecting idle state for {Count} processes", processes.Count);

                var results = new List<IdleDetectionResult>();
                var tasks = processes.Select(p => DetectIdleAsync(p.ProcessId, p.UserName, p.ComputerName, cancellationToken));

                var detectionResults = await Task.WhenAll(tasks);
                results.AddRange(detectionResults);

                _logger.LogDebug("Idle detection completed for {Count} processes", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("DetectIdleAsync", ex, ErrorSeverity.High);
                return new List<IdleDetectionResult>();
            }
        }

        public async Task<TimeSpan> GetIdleTimeAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_processHistories.TryGetValue(processId, out var history))
                {
                    return history.GetTimeSinceLastActivity();
                }

                // If no history exists, use current time
                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("GetIdleTimeAsync", ex, ErrorSeverity.Medium);
                return TimeSpan.Zero;
            }
        }

        public async Task<bool> IsIdleAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                var idleTime = await GetIdleTimeAsync(processId, cancellationToken);
                return idleTime >= TimeSpan.FromSeconds(_config.IdleThresholdSeconds);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("IsIdleAsync", ex, ErrorSeverity.Medium);
                return false;
            }
        }

        public async Task<IdleInfo> GetIdleInfoAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                var idleTime = await GetIdleTimeAsync(processId, cancellationToken);
                var isIdle = await IsIdleAsync(processId, cancellationToken);

                var info = new IdleInfo
                {
                    ProcessId = processId,
                    IdleTime = idleTime,
                    LastActivity = DateTime.UtcNow - idleTime,
                    State = isIdle ? SessionState.Idle : SessionState.Active,
                    Confidence = isIdle ? Math.Min(1.0, idleTime.TotalSeconds / _config.IdleThresholdSeconds) : 0.0,
                    DetectionMethods = new List<string> { "ActivityMonitoring" },
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["MonitorType"] = "ActivityMonitoringService",
                        ["IdleThresholdSeconds"] = _config.IdleThresholdSeconds,
                        ["ActivityScore"] = await CalculateActivityScoreAsync(processId, cancellationToken)
                    }
                };

                return info;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("GetIdleInfoAsync", ex, ErrorSeverity.Medium);
                return new IdleInfo
                {
                    ProcessId = processId,
                    IdleTime = TimeSpan.Zero,
                    LastActivity = DateTime.UtcNow,
                    State = SessionState.Unknown,
                    Confidence = 0.0,
                    DetectionMethods = new List<string> { "ActivityMonitoring" },
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["Error"] = ex.Message
                    }
                };
            }
        }

        public async Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating ActivityMonitoringService configuration");

                lock (_lock)
                {
                    Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
                }

                // Validate new configuration
                var validationResult = ValidateConfiguration(configuration);
                if (!validationResult.IsValid)
                {
                    var error = string.Join(", ", validationResult.Errors);
                    throw new InvalidOperationException($"Invalid configuration: {error}");
                }

                // Update all monitors
                await _fileSystemMonitor.UpdateConfigurationAsync(configuration, cancellationToken);
                await _performanceMonitor.UpdateConfigurationAsync(configuration, cancellationToken);
                // For SystemActivityTracker, create a specific config from the general configuration
                var trackerConfig = new SystemActivityTrackerConfig
                {
                    EnableKeyboardTracking = true,
                    EnableMouseTracking = true,
                    EnableMousePositionTracking = false,
                    EnableSystemResourceTracking = true,
                    EnableProcessMonitoring = true,
                    EnableWindowTracking = false,
                    EnableInputTracking = true,
                    TrackingInterval = (int)configuration.DetectionInterval.TotalMilliseconds
                };
                await _systemActivityTracker.UpdateConfigurationAsync(trackerConfig);

                _logger.LogInformation("ActivityMonitoringService configuration updated successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("UpdateConfigurationAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task<DetectorHealth> GetHealthAsync()
        {
            try
            {
                var health = new DetectorHealth
                {
                    DetectorName = Name,
                    CheckTimestamp = DateTime.UtcNow,
                    StatusMessage = "Healthy"
                };

                var isHealthy = true;
                var issues = new List<string>();

                // Check service status
                if (Status != DetectorStatus.Running)
                {
                    issues.Add($"Service status: {Status}");
                    isHealthy = false;
                }

                // Check monitor health
                var fileSystemHealth = await _fileSystemMonitor.GetHealthAsync();
                var performanceHealth = await _performanceMonitor.GetHealthAsync();
                var systemActivityHealth = await _systemActivityTracker.GetHealthAsync();

                if (!fileSystemHealth.IsHealthy)
                {
                    issues.Add($"FileSystemMonitor: {fileSystemHealth.StatusMessage}");
                    isHealthy = false;
                }

                if (!performanceHealth.IsHealthy)
                {
                    issues.Add($"PerformanceMonitor: {performanceHealth.StatusMessage}");
                    isHealthy = false;
                }

                if (!systemActivityHealth.IsHealthy)
                {
                    issues.Add($"SystemActivityTracker: {systemActivityHealth.StatusMessage}");
                    isHealthy = false;
                }

                // Check process history size
                if (_processHistories.Count > 1000)
                {
                    issues.Add($"High process history count: {_processHistories.Count}");
                }

                health.IsHealthy = isHealthy;
                health.StatusMessage = isHealthy ? "Healthy" : $"Issues: {string.Join(", ", issues)}";

                // Add additional info
                health.AdditionalInfo["ProcessHistoryCount"] = _processHistories.Count;
                health.AdditionalInfo["FileSystemHealth"] = fileSystemHealth.IsHealthy;
                health.AdditionalInfo["PerformanceHealth"] = performanceHealth.IsHealthy;
                health.AdditionalInfo["SystemActivityHealth"] = systemActivityHealth.IsHealthy;

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ActivityMonitoringService health");
                return new DetectorHealth
                {
                    DetectorName = Name,
                    IsHealthy = false,
                    StatusMessage = $"Error: {ex.Message}",
                    LastError = ex
                };
            }
        }

        public async Task<DetectorStatistics> GetStatisticsAsync()
        {
            try
            {
                var fileSystemStats = await _fileSystemMonitor.GetStatisticsAsync();
                var performanceStats = await _performanceMonitor.GetStatisticsAsync();
                var systemActivityStats = await _systemActivityTracker.GetStatisticsAsync();

                return new DetectorStatistics
                {
                    DetectorName = Name,
                    TotalDetections = GetDynamicPropertyValue<long>(fileSystemStats, "TotalActivitiesMonitored") +
                                   GetDynamicPropertyValue<long>(performanceStats, "TotalMetricsCollected") +
                                   GetDynamicPropertyValue<long>(systemActivityStats, "TotalActivitiesTracked"),
                    SuccessfulDetections = GetDynamicPropertyValue<long>(fileSystemStats, "SuccessfulDetections") +
                                         GetDynamicPropertyValue<long>(performanceStats, "SuccessfulCollections") +
                                         GetDynamicPropertyValue<long>(systemActivityStats, "SuccessfulTracks"),
                    FailedDetections = GetDynamicPropertyValue<long>(fileSystemStats, "FailedDetections") +
                                       GetDynamicPropertyValue<long>(performanceStats, "FailedCollections") +
                                       GetDynamicPropertyValue<long>(systemActivityStats, "FailedTracks"),
                    IdleStatesDetected = _processHistories.Values.Count(h => h.GetTimeSinceLastActivity() >= TimeSpan.FromSeconds(_config.IdleThresholdSeconds)),
                    ActiveStatesDetected = _processHistories.Values.Count(h => h.GetTimeSinceLastActivity() < TimeSpan.FromSeconds(_config.IdleThresholdSeconds)),
                    AverageDetectionTimeMs = (GetDynamicPropertyValue<double>(fileSystemStats, "AverageDetectionTimeMs") +
                                         GetDynamicPropertyValue<double>(performanceStats, "AverageCollectionTimeMs") +
                                         GetDynamicPropertyValue<double>(systemActivityStats, "AverageTrackingTimeMs")) / 3,
                    Uptime = DateTime.UtcNow - GetDynamicPropertyValue<DateTime>(fileSystemStats, "StartTime"),
                    LastDetectionTimestamp = _processHistories.Values.Max(h => h.LastActivityTime),
                    ErrorCount = GetDynamicPropertyValue<int>(fileSystemStats, "ErrorCount") +
                               GetDynamicPropertyValue<int>(performanceStats, "ErrorCount") +
                               GetDynamicPropertyValue<int>(systemActivityStats, "ErrorCount"),
                    StatisticsStartTime = GetDynamicPropertyValue<DateTime>(fileSystemStats, "StartTime")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ActivityMonitoringService statistics");
                return new DetectorStatistics
                {
                    DetectorName = Name,
                    StatisticsStartTime = DateTime.UtcNow,
                    ErrorCount = 1
                };
            }
        }

        public async Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _fileSystemMonitor.ResetStatisticsAsync(cancellationToken);
                await _performanceMonitor.ResetStatisticsAsync(cancellationToken);
                await _systemActivityTracker.ResetStatisticsAsync();

                lock (_lock)
                {
                    _processHistories.Clear();
                }

                _logger.LogInformation("ActivityMonitoringService statistics reset successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("ResetStatisticsAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public ValidationResult ValidateConfiguration(IdleDetectorConfiguration configuration)
        {
            var result = new ValidationResult();

            try
            {
                if (configuration == null)
                {
                    result.AddError("Configuration cannot be null");
                    return result;
                }

                // Validate detection interval
                if (configuration.DetectionIntervalSeconds < 10 || configuration.DetectionIntervalSeconds > 3600)
                {
                    result.AddError("Detection interval must be between 10 and 3600 seconds");
                }

                // Validate idle threshold
                if (configuration.IdleThresholdSeconds < 30 || configuration.IdleThresholdSeconds > 3600)
                {
                    result.AddError("Idle threshold must be between 30 and 3600 seconds");
                }

                // Validate confidence threshold
                if (configuration.ConfidenceThreshold < 0.0 || configuration.ConfidenceThreshold > 1.0)
                {
                    result.AddError("Confidence threshold must be between 0.0 and 1.0");
                }

                // Validate max detection time
                if (configuration.MaxDetectionTimeMs < 1000 || configuration.MaxDetectionTimeMs > 30000)
                {
                    result.AddError("Max detection time must be between 1000 and 30000 milliseconds");
                }

                // Validate custom parameters for activity monitoring
                if (configuration.CustomParameters != null)
                {
                    if (configuration.CustomParameters.ContainsKey("EnableFileSystemMonitoring"))
                    {
                        if (!bool.TryParse(configuration.CustomParameters["EnableFileSystemMonitoring"]?.ToString(), out _))
                        {
                            result.AddError("EnableFileSystemMonitoring must be a boolean value");
                        }
                    }

                    if (configuration.CustomParameters.ContainsKey("EnablePerformanceMonitoring"))
                    {
                        if (!bool.TryParse(configuration.CustomParameters["EnablePerformanceMonitoring"]?.ToString(), out _))
                        {
                            result.AddError("EnablePerformanceMonitoring must be a boolean value");
                        }
                    }

                    if (configuration.CustomParameters.ContainsKey("EnableSystemActivityMonitoring"))
                    {
                        if (!bool.TryParse(configuration.CustomParameters["EnableSystemActivityMonitoring"]?.ToString(), out _))
                        {
                            result.AddError("EnableSystemActivityMonitoring must be a boolean value");
                        }
                    }

                    if (configuration.CustomParameters.ContainsKey("ActivityScoreThreshold"))
                    {
                        if (!double.TryParse(configuration.CustomParameters["ActivityScoreThreshold"]?.ToString(), out var threshold) ||
                            threshold < 0.0 || threshold > 1.0)
                        {
                            result.AddError("ActivityScoreThreshold must be a double between 0.0 and 1.0");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Configuration validation error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Gets a dynamic property value from an object using reflection
        /// </summary>
        private static T GetDynamicPropertyValue<T>(object obj, string propertyName)
        {
            if (obj == null)
            {
                return default(T);
            }

            try
            {
                var property = obj.GetType().GetProperty(propertyName);
                if (property != null)
                {
                    var value = property.GetValue(obj);
                    if (value is T result)
                    {
                        return result;
                    }

                    // Try to convert the value to the expected type
                    if (value != null && typeof(T) == typeof(long) && value is int intValue)
                    {
                        return (T)(object)(long)intValue;
                    }
                    if (value != null && typeof(T) == typeof(int) && value is long longValue)
                    {
                        return (T)(object)(int)longValue;
                    }
                    if (value != null && typeof(T) == typeof(double) && value is float floatValue)
                    {
                        return (T)(object)(double)floatValue;
                    }
                }

                return default(T);
            }
            catch
            {
                return default(T);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Main monitoring loop
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting activity monitoring loop");

            while (!cancellationToken.IsCancellationRequested && Status == DetectorStatus.Running)
            {
                try
                {
                    // Wait for the detection interval
                    await Task.Delay(TimeSpan.FromSeconds(Configuration.DetectionIntervalSeconds), cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Perform comprehensive activity analysis
                    await PerformComprehensiveAnalysisAsync(cancellationToken);

                    // Clean up old process histories
                    CleanupOldProcessHistories();
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    await HandleErrorAsync("MonitoringLoop", ex, ErrorSeverity.Medium);
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Delay on error
                }
            }

            _logger.LogDebug("Activity monitoring loop stopped");
        }

        /// <summary>
        /// Collects activities for a specific process from all monitors
        /// </summary>
        private async Task<List<ActivityData>> CollectProcessActivitiesAsync(int processId, CancellationToken cancellationToken)
        {
            var activities = new List<ActivityData>();

            try
            {
                // Collect file system activities
                if (_config.EnableFileSystemMonitoring)
                {
                    var fileActivities = await _fileSystemMonitor.GetProcessActivitiesAsync(processId, cancellationToken);
                    activities.AddRange(fileActivities);
                }

                // Collect performance activities
                if (_config.EnablePerformanceMonitoring)
                {
                    var perfActivities = await _performanceMonitor.GetProcessActivitiesAsync(processId, cancellationToken);
                    activities.AddRange(perfActivities);
                }

                // Collect system activities
                if (_config.EnableSystemActivityMonitoring)
                {
                    var systemActivities = await _systemActivityTracker.GetProcessActivitiesAsync(processId, cancellationToken);
                    activities.AddRange(systemActivities);
                }

                return activities.OrderByDescending(a => a.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting activities for process {ProcessId}", processId);
                return activities;
            }
        }

        /// <summary>
        /// Analyzes process activities to determine idle state
        /// </summary>
        private ActivityAnalysis AnalyzeProcessActivities(int processId, List<ActivityData> activities)
        {
            var analysis = new ActivityAnalysis();

            try
            {
                if (!activities.Any())
                {
                    analysis.IsIdle = true;
                    analysis.IdleTime = TimeSpan.FromMinutes(30); // Assume idle if no activities
                    analysis.Confidence = 0.5;
                    analysis.Reason = "No activities detected";
                    analysis.ActivityScore = 0.0;
                    analysis.LastActivityTime = DateTime.UtcNow.AddMinutes(-30);
                    return analysis;
                }

                var mostRecentActivity = activities.Max(a => a.Timestamp);
                var timeSinceActivity = DateTime.UtcNow - mostRecentActivity;

                // Calculate activity score based on recency and frequency
                analysis.ActivityScore = CalculateActivityScore(activities);
                analysis.LastActivityTime = mostRecentActivity;
                analysis.IdleTime = timeSinceActivity;

                // Determine idle state based on threshold and activity score
                var activityThreshold = _config.ActivityScoreThreshold ?? 0.3;
                analysis.IsIdle = timeSinceActivity >= TimeSpan.FromSeconds(_config.IdleThresholdSeconds) &&
                                analysis.ActivityScore <= activityThreshold;

                // Calculate confidence based on multiple factors
                var timeConfidence = Math.Min(1.0, timeSinceActivity.TotalSeconds / _config.IdleThresholdSeconds);
                var activityConfidence = 1.0 - analysis.ActivityScore;
                analysis.Confidence = (timeConfidence + activityConfidence) / 2;

                analysis.Reason = analysis.IsIdle
                    ? $"No significant activity for {timeSinceActivity.TotalMinutes:F1} minutes (score: {analysis.ActivityScore:F2})"
                    : $"Recent activity detected (score: {analysis.ActivityScore:F2}, time: {timeSinceActivity.TotalMinutes:F1}m)";

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing activities for process {ProcessId}", processId);
                return new ActivityAnalysis
                {
                    IsIdle = false,
                    IdleTime = TimeSpan.Zero,
                    Confidence = 0.0,
                    Reason = $"Analysis error: {ex.Message}",
                    ActivityScore = 0.0,
                    LastActivityTime = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Calculates activity score based on activities
        /// </summary>
        private double CalculateActivityScore(List<ActivityData> activities)
        {
            try
            {
                if (!activities.Any())
                    return 0.0;

                var now = DateTime.UtcNow;
                var totalScore = 0.0;
                var maxScore = 0.0;

                foreach (var activity in activities)
                {
                    // Calculate time decay (more recent = higher score)
                    var timeDecay = Math.Exp(-(now - activity.Timestamp).TotalMinutes / 10.0);
                    var activityScore = activity.Confidence * timeDecay;

                    totalScore += activityScore;
                    maxScore += 1.0; // Maximum possible score per activity
                }

                return maxScore > 0 ? Math.Min(1.0, totalScore / maxScore) : 0.0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating activity score");
                return 0.0;
            }
        }

        /// <summary>
        /// Updates process history with detection result
        /// </summary>
        private void UpdateProcessHistory(int processId, IdleDetectionResult result)
        {
            try
            {
                var history = _processHistories.GetOrAdd(processId, id => new ProcessActivityHistory(id));
                history.RecordDetection(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating process history for process {ProcessId}", processId);
            }
        }

        /// <summary>
        /// Performs comprehensive activity analysis across all monitored processes
        /// </summary>
        private async Task PerformComprehensiveAnalysisAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get all active SolidWorks processes
                var solidWorksProcesses = GetActiveSolidWorksProcesses();

                foreach (var process in solidWorksProcesses)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var activities = await CollectProcessActivitiesAsync(process.Id, cancellationToken);
                        var analysis = AnalyzeProcessActivities(process.Id, activities);

                        // Raise analysis completed event
                        ActivityAnalysisCompleted?.Invoke(this, new ActivityAnalysisEventArgs
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            Analysis = analysis,
                            Timestamp = DateTime.UtcNow
                        });

                        // Update process history
                        var history = _processHistories.GetOrAdd(process.Id, id => new ProcessActivityHistory(id));
                        history.RecordActivityAnalysis(analysis);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error analyzing process {ProcessId}", process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive activity analysis");
            }
        }

        /// <summary>
        /// Gets all active SolidWorks processes
        /// </summary>
        private List<System.Diagnostics.Process> GetActiveSolidWorksProcesses()
        {
            try
            {
                return System.Diagnostics.Process.GetProcessesByName("SLDWORKS")
                    .Where(p => !p.HasExited && p.Responding)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SolidWorks processes");
                return new List<System.Diagnostics.Process>();
            }
        }

        /// <summary>
        /// Calculates activity score for a process
        /// </summary>
        private async Task<double> CalculateActivityScoreAsync(int processId, CancellationToken cancellationToken)
        {
            try
            {
                if (_processHistories.TryGetValue(processId, out var history))
                {
                    return history.GetActivityScore();
                }

                // If no history, collect recent activities and calculate
                var activities = await CollectProcessActivitiesAsync(processId, cancellationToken);
                return CalculateActivityScore(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating activity score for process {ProcessId}", processId);
                return 0.0;
            }
        }

        /// <summary>
        /// Cleans up old process histories
        /// </summary>
        private void CleanupOldProcessHistories()
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
                    _logger.LogDebug("Cleaned up {Count} old process histories", toRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in process history cleanup");
            }
        }

        /// <summary>
        /// Timer callback for cleaning up old process histories
        /// </summary>
        private void CleanupTimerCallback(object state)
        {
            try
            {
                CleanupOldProcessHistories();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup timer callback");
            }
        }

        /// <summary>
        /// Updates detector status
        /// </summary>
        private void UpdateStatus(DetectorStatus newStatus, string reason)
        {
            try
            {
                var previousStatus = Status;
                Status = newStatus;

                StatusChanged?.Invoke(this, new DetectorStatusChangedEventArgs(
                    Name, previousStatus, newStatus, reason));

                _logger.LogDebug("Status changed: {PreviousStatus} -> {NewStatus}: {Reason}",
                    previousStatus, newStatus, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating detector status");
            }
        }

        /// <summary>
        /// Handles errors and raises error events
        /// </summary>
        private async Task HandleErrorAsync(string operation, Exception ex, ErrorSeverity severity)
        {
            try
            {
                _logger.LogError(ex, "Error in {Operation}", operation);

                ErrorOccurred?.Invoke(this, new DetectorErrorEventArgs(
                    Name, ex, severity, operation));

                // Update status if critical error
                if (severity == ErrorSeverity.Critical)
                {
                    UpdateStatus(DetectorStatus.Error, $"Critical error in {operation}: {ex.Message}");
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Error in error handler");
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles file system activity events
        /// </summary>
        private void OnFileSystemActivityDetected(object sender, FileSystemActivityEventArgs e)
        {
            try
            {
                FileSystemActivityDetected?.Invoke(this, e);

                // Convert to activity data and update process history
                var activity = new ActivityData
                {
                    Type = ActivityType.FileSystem,
                    Timestamp = e.Timestamp,
                    Confidence = e.Confidence,
                    Source = "FileSystemMonitor",
                    Details = new Dictionary<string, object>
                    {
                        ["FilePath"] = e.FilePath,
                        ["ChangeType"] = e.ChangeType,
                        ["FileSize"] = e.FileSize
                    }
                };

                if (_processHistories.TryGetValue(e.ProcessId, out var history))
                {
                    history.RecordActivity(activity);
                }

                _logger.LogDebug("File system activity detected: {FilePath} for process {ProcessId}", e.FilePath, e.ProcessId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file system activity event");
            }
        }

        /// <summary>
        /// Handles performance metrics update events
        /// </summary>
        private void OnPerformanceMetricsUpdated(object sender, PerformanceMetricsEventArgs e)
        {
            try
            {
                PerformanceMetricsUpdated?.Invoke(this, e);

                // Convert to activity data and update process history
                var activity = new ActivityData
                {
                    Type = ActivityType.Performance,
                    Timestamp = e.Timestamp,
                    Confidence = e.ActivityLevel,
                    Source = "PerformanceMonitor",
                    Details = new Dictionary<string, object>
                    {
                        ["CpuUsage"] = e.CpuUsage,
                        ["MemoryUsage"] = e.MemoryUsage,
                        ["DiskUsage"] = e.DiskUsage,
                        ["NetworkUsage"] = e.NetworkUsage
                    }
                };

                if (_processHistories.TryGetValue(e.ProcessId, out var history))
                {
                    history.RecordActivity(activity);
                }

                _logger.LogDebug("Performance metrics updated for process {ProcessId}: CPU={CpuUsage:F1}%, Memory={MemoryUsage:F1}%",
                    e.ProcessId, e.CpuUsage, e.MemoryUsage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling performance metrics event");
            }
        }

        /// <summary>
        /// Handles system activity tracking events
        /// </summary>
        private void OnSystemActivityTracked(object sender, SystemActivityEvent e)
        {
            try
            {
                SystemActivityTracked?.Invoke(this, e);

                // Convert to activity data and update process history
                var activity = new ActivityData
                {
                    Type = ActivityType.System,
                    Timestamp = e.Timestamp,
                    Confidence = e.Confidence,
                    Source = "SystemActivityTracker",
                    Details = new Dictionary<string, object>
                    {
                        ["ActivityType"] = e.ActivityType,
                        ["WindowTitle"] = e.WindowTitle,
                        ["IdleTime"] = e.IdleTime,
                        ["HasForeground"] = e.HasForeground
                    }
                };

                if (e.ProcessId.HasValue && _processHistories.TryGetValue(e.ProcessId.Value, out var history))
                {
                    history.RecordActivity(activity);
                }

                _logger.LogDebug("System activity tracked for process {ProcessId}: {ActivityType}", e.ProcessId, e.ActivityType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling system activity event");
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
                    _logger.LogInformation("Disposing ActivityMonitoringService");

                    try
                    {
                        StopAsync().Wait(TimeSpan.FromSeconds(5));
                        _fileSystemMonitor.Dispose();
                        _performanceMonitor.Dispose();
                        _systemActivityTracker.Dispose();
                        _cleanupTimer?.Dispose();
                        _cancellationTokenSource?.Dispose();
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
    /// Configuration for activity monitoring service
    /// </summary>
    public class ActivityMonitoringConfig
    {
        /// <summary>
        /// Gets or sets whether the service is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the idle threshold in seconds
        /// </summary>
        public int IdleThresholdSeconds { get; set; } = 300;

        /// <summary>
        /// Gets or sets the activity score threshold (0.0 to 1.0)
        /// </summary>
        public double? ActivityScoreThreshold { get; set; } = 0.3;

        /// <summary>
        /// Gets or sets whether file system monitoring is enabled
        /// </summary>
        public bool EnableFileSystemMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether performance monitoring is enabled
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether system activity monitoring is enabled
        /// </summary>
        public bool EnableSystemActivityMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets the confidence threshold (0.0 to 1.0)
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.7;

        /// <summary>
        /// Gets or sets the maximum detection time in milliseconds
        /// </summary>
        public int MaxDetectionTimeMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the monitoring priority
        /// </summary>
        public int Priority { get; set; } = 15;

        /// <summary>
        /// Gets or sets the cleanup interval in minutes
        /// </summary>
        public int CleanupIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum process history count
        /// </summary>
        public int MaxProcessHistoryCount { get; set; } = 1000;

        /// <summary>
        /// Gets or sets custom configuration parameters
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; }

        /// <summary>
        /// Initializes a new instance of the ActivityMonitoringConfig class
        /// </summary>
        public ActivityMonitoringConfig()
        {
            CustomParameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// Converts the configuration to an IdleDetectorConfiguration
        /// </summary>
        /// <returns>IdleDetectorConfiguration object</returns>
        public IdleDetectorConfiguration ToIdleDetectorConfiguration()
        {
            return new IdleDetectorConfiguration
            {
                DetectionIntervalSeconds = 60,
                IdleThresholdSeconds = IdleThresholdSeconds,
                ConfidenceThreshold = ConfidenceThreshold,
                IsEnabled = IsEnabled,
                Priority = Priority,
                MaxDetectionTimeMs = MaxDetectionTimeMs,
                TimeoutSeconds = MaxDetectionTimeMs / 1000,
                RetryCount = 3,
                RetryDelayMs = 1000,
                MaxConcurrentOperations = 5,
                CustomParameters = new Dictionary<string, object>(CustomParameters)
                {
                    ["ActivityScoreThreshold"] = ActivityScoreThreshold,
                    ["EnableFileSystemMonitoring"] = EnableFileSystemMonitoring,
                    ["EnablePerformanceMonitoring"] = EnablePerformanceMonitoring,
                    ["EnableSystemActivityMonitoring"] = EnableSystemActivityMonitoring,
                    ["CleanupIntervalMinutes"] = CleanupIntervalMinutes,
                    ["MaxProcessHistoryCount"] = MaxProcessHistoryCount
                }
            };
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"ActivityMonitoring[Enabled={IsEnabled}, IdleThreshold={IdleThresholdSeconds}s, " +
                   $"FileSystem={EnableFileSystemMonitoring}, Performance={EnablePerformanceMonitoring}, " +
                   $"SystemActivity={EnableSystemActivityMonitoring}]";
        }
    }

    /// <summary>
    /// Represents activity data from monitoring
    /// </summary>
    public class ActivityData
    {
        /// <summary>
        /// Gets or sets the activity type
        /// </summary>
        public ActivityType Type { get; set; }

        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the confidence level
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets the source monitor
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets activity details
        /// </summary>
        public Dictionary<string, object> Details { get; set; }

        /// <summary>
        /// Gets or sets the activity type (wrapper property for backward compatibility)
        /// </summary>
        public ActivityType ActivityType
        {
            get => Type;
            set => Type = value;
        }

        /// <summary>
        /// Gets or sets activity metadata (wrapper property for backward compatibility)
        /// </summary>
        public Dictionary<string, object> Metadata
        {
            get => Details;
            set => Details = value;
        }

        /// <summary>
        /// Initializes a new instance of the ActivityData class
        /// </summary>
        public ActivityData()
        {
            Timestamp = DateTime.UtcNow;
            Confidence = 1.0;
            Details = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Defines activity types for monitoring
    /// </summary>
    public enum ActivityType
    {
        /// <summary>
        /// File system activity
        /// </summary>
        FileSystem,

        /// <summary>
        /// Performance-related activity
        /// </summary>
        Performance,

        /// <summary>
        /// System-wide activity
        /// </summary>
        System,

        /// <summary>
        /// User input activity
        /// </summary>
        UserInput,

        /// <summary>
        /// Network activity
        /// </summary>
        Network,

        /// <summary>
        /// Process activity
        /// </summary>
        Process,

        /// <summary>
        /// Window activity
        /// </summary>
        Window,

        /// <summary>
        /// SolidWorks application activity
        /// </summary>
        SolidWorks,

        /// <summary>
        /// Keyboard input activity
        /// </summary>
        Keyboard,

        /// <summary>
        /// Mouse input activity
        /// </summary>
        Mouse,

        /// <summary>
        /// Window focus activity
        /// </summary>
        WindowFocus
    }

    /// <summary>
    /// Represents activity analysis results
    /// </summary>
    public class ActivityAnalysis
    {
        /// <summary>
        /// Gets or sets whether the process is idle
        /// </summary>
        public bool IsIdle { get; set; }

        /// <summary>
        /// Gets or sets the idle time duration
        /// </summary>
        public TimeSpan IdleTime { get; set; }

        /// <summary>
        /// Gets or sets the confidence level
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets the reason for the analysis
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the activity score (0.0 to 1.0)
        /// </summary>
        public double ActivityScore { get; set; }

        /// <summary>
        /// Gets or sets the last activity time
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the ActivityAnalysis class
        /// </summary>
        public ActivityAnalysis()
        {
            IdleTime = TimeSpan.Zero;
            Confidence = 0.0;
            ActivityScore = 0.0;
            LastActivityTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Maintains activity history for a specific process
    /// </summary>
    public class ProcessActivityHistory
    {
        private readonly object _lock = new object();
        private readonly Queue<ActivityData> _recentActivities = new Queue<ActivityData>(100);
        private readonly Queue<IdleDetectionResult> _detectionResults = new Queue<IdleDetectionResult>(50);
        private readonly Queue<ActivityAnalysis> _activityAnalyses = new Queue<ActivityAnalysis>(50);
        private readonly int _processId;

        public int ProcessId => _processId;
        public DateTime LastActivityTime { get; private set; }
        public int TotalActivities { get; private set; }
        public int TotalDetections { get; private set; }
        public int TotalAnalyses { get; private set; }

        public ProcessActivityHistory(int processId)
        {
            _processId = processId;
            LastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Records activity data
        /// </summary>
        public void RecordActivity(ActivityData activity)
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
        /// Records detection result
        /// </summary>
        public void RecordDetection(IdleDetectionResult result)
        {
            lock (_lock)
            {
                _detectionResults.Enqueue(result);
                if (_detectionResults.Count > 50)
                    _detectionResults.Dequeue();

                TotalDetections++;
            }
        }

        /// <summary>
        /// Records activity analysis
        /// </summary>
        public void RecordActivityAnalysis(ActivityAnalysis analysis)
        {
            lock (_lock)
            {
                _activityAnalyses.Enqueue(analysis);
                if (_activityAnalyses.Count > 50)
                    _activityAnalyses.Dequeue();

                TotalAnalyses++;
            }
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

        /// <summary>
        /// Gets the current activity score
        /// </summary>
        public double GetActivityScore()
        {
            lock (_lock)
            {
                if (!_recentActivities.Any())
                    return 0.0;

                var now = DateTime.UtcNow;
                var totalScore = 0.0;
                var maxScore = 0.0;

                foreach (var activity in _recentActivities)
                {
                    var timeDecay = Math.Exp(-(now - activity.Timestamp).TotalMinutes / 10.0);
                    totalScore += activity.Confidence * timeDecay;
                    maxScore += 1.0;
                }

                return maxScore > 0 ? Math.Min(1.0, totalScore / maxScore) : 0.0;
            }
        }

        /// <summary>
        /// Gets recent activities within the specified time window
        /// </summary>
        public List<ActivityData> GetRecentActivities(TimeSpan timeWindow)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - timeWindow;
                return _recentActivities.Where(a => a.Timestamp > cutoffTime).ToList();
            }
        }
    }

    #region Event Arguments Classes

    /// <summary>
    /// Event arguments for file system activity events
    /// </summary>
    public class FileSystemActivityEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string FilePath { get; set; }
        public string ChangeType { get; set; }
        public long FileSize { get; set; }
        public DateTime Timestamp { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, object> AdditionalInfo { get; set; }

        public FileSystemActivityEventArgs()
        {
            Timestamp = DateTime.UtcNow;
            Confidence = 1.0;
            AdditionalInfo = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Event arguments for performance metrics events
    /// </summary>
    public class PerformanceMetricsEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public double NetworkUsage { get; set; }
        public double ActivityLevel { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> AdditionalInfo { get; set; }

        public PerformanceMetricsEventArgs()
        {
            Timestamp = DateTime.UtcNow;
            ActivityLevel = 0.0;
            AdditionalInfo = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Event arguments for system activity events
    /// </summary>
    public class SystemActivityEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ActivityType { get; set; }
        public string WindowTitle { get; set; }
        public TimeSpan IdleTime { get; set; }
        public bool HasForeground { get; set; }
        public DateTime Timestamp { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, object> AdditionalInfo { get; set; }

        public SystemActivityEventArgs()
        {
            Timestamp = DateTime.UtcNow;
            Confidence = 1.0;
            AdditionalInfo = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Event arguments for activity analysis events
    /// </summary>
    public class ActivityAnalysisEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public ActivityAnalysis Analysis { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> AdditionalInfo { get; set; }

        public ActivityAnalysisEventArgs()
        {
            Timestamp = DateTime.UtcNow;
            AdditionalInfo = new Dictionary<string, object>();
        }
    }

    #endregion
}