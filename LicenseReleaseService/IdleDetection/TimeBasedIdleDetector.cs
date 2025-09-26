using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Time-based idle detector that implements the IIdleDetector interface
    /// </summary>
    public class TimeBasedIdleDetector : IIdleDetector
    {
        private readonly ILogger<TimeBasedIdleDetector> _logger;
        private readonly TimeBasedDetectionConfig _config;
        private readonly SystemActivityMonitor _activityMonitor;
        private readonly ActivityThresholdManager _thresholdManager;
        private readonly object _lock = new object();
        private readonly Dictionary<int, ProcessDetectionState> _processStates = new Dictionary<int, ProcessDetectionState>();
        private readonly Timer _detectionTimer;
        private bool _isDisposed;
        private CancellationTokenSource _cancellationTokenSource;

        #region IIdleDetector Properties

        public string Name => "TimeBasedIdleDetector";
        public string Description => "Time-based idle detection with adaptive thresholds and graduated detection levels";
        public Version Version => new Version(1, 0, 0, 0);
        public bool IsEnabled { get; private set; }
        public bool IsInitialized { get; private set; }
        public int Priority => 10;
        public IReadOnlyList<string> SupportedMethods => new List<string> { "TimeBased", "Adaptive", "Graduated" }.AsReadOnly();
        public DetectorStatus Status { get; private set; }
        public IdleDetectorConfiguration Configuration { get; private set; }

        #endregion

        #region IIdleDetector Events

        public event EventHandler<IdleDetectionEventArgs> IdleDetected;
        public event EventHandler<IdleDetectionEventArgs> ActivityDetected;
        public event EventHandler<DetectorStatusChangedEventArgs> StatusChanged;
        public event EventHandler<DetectorErrorEventArgs> ErrorOccurred;

        #endregion

        /// <summary>
        /// Initializes a new instance of the TimeBasedIdleDetector class
        /// </summary>
        public TimeBasedIdleDetector(
            ILogger<TimeBasedIdleDetector> logger,
            TimeBasedDetectionConfig config,
            SystemActivityMonitor activityMonitor,
            ActivityThresholdManager thresholdManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _activityMonitor = activityMonitor ?? throw new ArgumentNullException(nameof(activityMonitor));
            _thresholdManager = thresholdManager ?? throw new ArgumentNullException(nameof(thresholdManager));

            Status = DetectorStatus.NotInitialized;
            IsEnabled = _config.ToIdleDetectorConfiguration().IsEnabled;

            // Initialize detection timer
            _detectionTimer = new Timer(DetectionTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Wire up activity monitor events
            _activityMonitor.ActivityDetected += OnActivityDetected;
            _thresholdManager.ThresholdsAdapted += OnThresholdsAdapted;

            _logger.LogInformation("TimeBasedIdleDetector initialized with configuration: {Config}", _config);
        }

        #region IIdleDetector Initialization and Lifecycle

        public async Task InitializeAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Initializing TimeBasedIdleDetector");

                lock (_lock)
                {
                    if (IsInitialized)
                    {
                        _logger.LogWarning("TimeBasedIdleDetector is already initialized");
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

                // Initialize threshold manager
                _thresholdManager.Initialize();

                // Start activity monitor
                await _activityMonitor.StartAsync();

                // Update status
                UpdateStatus(DetectorStatus.Initialized, "Initialization completed");

                IsInitialized = true;
                _logger.LogInformation("TimeBasedIdleDetector initialized successfully");
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
                _logger.LogInformation("Starting TimeBasedIdleDetector");

                lock (_lock)
                {
                    if (!IsInitialized)
                    {
                        throw new InvalidOperationException("Detector must be initialized before starting");
                    }

                    if (Status == DetectorStatus.Running)
                    {
                        _logger.LogWarning("TimeBasedIdleDetector is already running");
                        return;
                    }
                }

                // Start detection timer
                var dueTime = _config.DetectionInterval;
                var period = _config.DetectionInterval;
                _detectionTimer.Change(dueTime, period);

                // Update status
                UpdateStatus(DetectorStatus.Running, "Detector started");

                _logger.LogInformation("TimeBasedIdleDetector started successfully with detection interval: {Interval}",
                    _config.DetectionInterval);
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
                _logger.LogInformation("Stopping TimeBasedIdleDetector");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Running)
                    {
                        _logger.LogWarning("TimeBasedIdleDetector is not running");
                        return;
                    }
                }

                // Stop detection timer
                _detectionTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Cancel any pending operations
                _cancellationTokenSource?.Cancel();

                // Update status
                UpdateStatus(DetectorStatus.Stopped, "Detector stopped");

                _logger.LogInformation("TimeBasedIdleDetector stopped successfully");
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
                _logger.LogInformation("Pausing TimeBasedIdleDetector");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Running)
                    {
                        _logger.LogWarning("TimeBasedIdleDetector is not running");
                        return;
                    }
                }

                // Stop detection timer
                _detectionTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Update status
                UpdateStatus(DetectorStatus.Paused, "Detector paused");

                _logger.LogInformation("TimeBasedIdleDetector paused successfully");
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
                _logger.LogInformation("Resuming TimeBasedIdleDetector");

                lock (_lock)
                {
                    if (Status != DetectorStatus.Paused)
                    {
                        _logger.LogWarning("TimeBasedIdleDetector is not paused");
                        return;
                    }
                }

                // Restart detection timer
                var dueTime = _config.DetectionInterval;
                var period = _config.DetectionInterval;
                _detectionTimer.Change(dueTime, period);

                // Update status
                UpdateStatus(DetectorStatus.Running, "Detector resumed");

                _logger.LogInformation("TimeBasedIdleDetector resumed successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("ResumeAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        #endregion

        #region IIdleDetector Detection Methods

        public async Task<IdleDetectionResult> DetectIdleAsync(int processId, string userName, string computerName, CancellationToken cancellationToken = default)
        {
            try
            {
                var sessionId = Guid.NewGuid().ToString("N")[..8];
                var startTime = DateTime.UtcNow;

                _logger.LogDebug("Starting idle detection for process {ProcessId}, user {UserName}, computer {ComputerName}",
                    processId, userName, computerName);

                // Get or create process state
                var processState = GetOrCreateProcessState(processId, userName, computerName);

                // Get current system idle time
                var systemIdleTime = _activityMonitor.GetSystemIdleTime();

                // Get user-specific thresholds
                var thresholds = _thresholdManager.GetUserThresholds(userName, computerName);

                // Calculate detection level
                var detectionLevel = _thresholdManager.GetDetectionLevel(systemIdleTime, userName, computerName);

                // Apply hysteresis
                detectionLevel = _thresholdManager.ApplyHysteresis(
                    processState.LastDetectionLevel, detectionLevel, systemIdleTime, userName, computerName);

                // Calculate confidence based on multiple factors
                var confidence = CalculateConfidence(systemIdleTime, detectionLevel, thresholds, processState);

                // Determine if the process is idle
                var isIdle = detectionLevel != DetectionLevel.Active;

                // Create result
                var result = new IdleDetectionResult
                {
                    SessionId = sessionId,
                    ProcessId = processId,
                    UserName = userName,
                    ComputerName = computerName,
                    IsIdle = isIdle,
                    IdleTime = systemIdleTime,
                    Confidence = confidence,
                    DetectionMethod = "TimeBasedAdaptive",
                    Timestamp = DateTime.UtcNow,
                    DetectorName = Name,
                    Reason = GetDetectionReason(detectionLevel, systemIdleTime, thresholds)
                };

                // Add metadata
                result.Metadata["DetectionLevel"] = detectionLevel;
                result.Metadata["SystemIdleTime"] = systemIdleTime;
                result.Metadata["EffectiveThresholds"] = thresholds;
                result.Metadata["LastActivityTime"] = _activityMonitor.LastActivity;
                result.Metadata["ActiveWindow"] = _activityMonitor.GetActiveWindowTitle();
                result.Metadata["IsSolidWorksActive"] = _activityMonitor.IsSolidWorksActive();

                // Update process state
                UpdateProcessState(processState, result, detectionLevel);

                // Process activity for threshold adaptation
                if (!isIdle)
                {
                    var activity = new ActivityData
                    {
                        ActivityType = ActivityType.System,
                        Confidence = confidence,
                        Timestamp = DateTime.UtcNow
                    };
                    _thresholdManager.ProcessActivity(activity, userName, computerName);
                }

                // Raise events
                if (isIdle && !processState.WasIdle)
                {
                    OnIdleDetected(result);
                }
                else if (!isIdle && processState.WasIdle)
                {
                    OnActivityDetected(result);
                }

                var detectionTime = DateTime.UtcNow - startTime;
                _logger.LogDebug("Idle detection completed for process {ProcessId}: IsIdle={IsIdle}, " +
                    "Level={Level}, Confidence={Confidence:F2}, Time={Time}ms",
                    processId, isIdle, detectionLevel, confidence, detectionTime.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("DetectIdleAsync", ex, ErrorSeverity.High);
                throw;
            }
        }

        public async Task<IList<IdleDetectionResult>> DetectIdleAsync(IList<ProcessInfo> processes, CancellationToken cancellationToken = default)
        {
            try
            {
                var results = new List<IdleDetectionResult>();
                var startTime = DateTime.UtcNow;

                _logger.LogDebug("Starting batch idle detection for {Count} processes", processes.Count);

                foreach (var process in processes)
                {
                    try
                    {
                        var result = await DetectIdleAsync(process.ProcessId, process.UserName, process.ComputerName, cancellationToken);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error detecting idle for process {ProcessId}", process.ProcessId);

                        // Add error result
                        results.Add(new IdleDetectionResult
                        {
                            ProcessId = process.ProcessId,
                            UserName = process.UserName,
                            ComputerName = process.ComputerName,
                            IsIdle = false,
                            Confidence = 0.0,
                            DetectionMethod = "Error",
                            Reason = ex.Message
                        });
                    }
                }

                var totalTime = DateTime.UtcNow - startTime;
                _logger.LogDebug("Batch idle detection completed: {Count} processes in {Time}ms",
                    results.Count, totalTime.TotalMilliseconds);

                return results;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("DetectIdleAsync (batch)", ex, ErrorSeverity.High);
                throw;
            }
        }

        public async Task<TimeSpan> GetIdleTimeAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                return _activityMonitor.GetSystemIdleTime();
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("GetIdleTimeAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task<bool> IsIdleAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await DetectIdleAsync(processId, "unknown", "unknown", cancellationToken);
                return result.IsIdle;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("IsIdleAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        public async Task<IdleInfo> GetIdleInfoAsync(int processId, CancellationToken cancellationToken = default)
        {
            try
            {
                var systemIdleTime = _activityMonitor.GetSystemIdleTime();
                var recentActivities = _activityMonitor.GetRecentActivities(TimeSpan.FromMinutes(30));
                var detectionLevel = _thresholdManager.GetDetectionLevel(systemIdleTime, "unknown", "unknown");

                return new IdleInfo
                {
                    ProcessId = processId,
                    IdleTime = systemIdleTime,
                    LastActivity = _activityMonitor.LastActivity,
                    State = MapDetectionLevelToSessionState(detectionLevel),
                    Confidence = CalculateIdleConfidence(systemIdleTime, detectionLevel),
                    DetectionMethods = recentActivities.Select(a => a.ActivityType.ToString()).Distinct().ToList(),
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["SystemIdleTime"] = systemIdleTime,
                        ["DetectionLevel"] = detectionLevel,
                        ["RecentActivities"] = recentActivities.Count,
                        ["LastActivityType"] = recentActivities.FirstOrDefault()?.ActivityType.ToString(),
                        ["ActivityMonitorStatus"] = _activityMonitor.IsRunning
                    }
                };
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("GetIdleInfoAsync", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        #endregion

        #region IIdleDetector Configuration and Management

        public async Task UpdateConfigurationAsync(IdleDetectorConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating TimeBasedIdleDetector configuration");

                lock (_lock)
                {
                    var validationResult = ValidateConfiguration(configuration);
                    if (!validationResult.IsValid)
                    {
                        var error = string.Join(", ", validationResult.Errors);
                        throw new InvalidOperationException($"Invalid configuration: {error}");
                    }

                    Configuration = configuration;
                }

                _logger.LogInformation("TimeBasedIdleDetector configuration updated successfully");
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

                // Check component health
                var isHealthy = true;
                var issues = new List<string>();

                // Check activity monitor
                if (!_activityMonitor.IsRunning)
                {
                    isHealthy = false;
                    issues.Add("Activity monitor is not running");
                }

                // Check threshold manager statistics
                var stats = _thresholdManager.GetStatistics();
                if (stats.AdaptationErrors > 10)
                {
                    isHealthy = false;
                    issues.Add($"High adaptation error count: {stats.AdaptationErrors}");
                }

                // Check process states
                lock (_lock)
                {
                    if (_processStates.Count > 1000)
                    {
                        issues.Add($"High process state count: {_processStates.Count}");
                    }
                }

                health.IsHealthy = isHealthy;
                health.StatusMessage = isHealthy ? "Healthy" : $"Issues: {string.Join(", ", issues)}";

                // Add additional info
                health.AdditionalInfo["ProcessStateCount"] = _processStates.Count;
                health.AdditionalInfo["ActivityMonitorRunning"] = _activityMonitor.IsRunning;
                health.AdditionalInfo["TotalAdaptations"] = stats.TotalAdaptations;
                health.AdditionalInfo["TotalActivitiesProcessed"] = stats.TotalActivitiesProcessed;
                health.AdditionalInfo["AccuracyRate"] = stats.AccuracyRate;

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detector health");
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
                var thresholdStats = _thresholdManager.GetStatistics();
                var stats = new DetectorStatistics
                {
                    DetectorName = Name,
                    TotalDetections = thresholdStats.TotalActivitiesProcessed,
                    SuccessfulDetections = thresholdStats.CorrectDetections,
                    FailedDetections = thresholdStats.TotalDetectionsRecorded - thresholdStats.CorrectDetections,
                    IdleStatesDetected = 0, // TODO: Track this
                    ActiveStatesDetected = 0, // TODO: Track this
                    AverageDetectionTimeMs = 0, // TODO: Track this
                    Uptime = DateTime.UtcNow - (Configuration?.CustomParameters?["StartTime"] as DateTime? ?? DateTime.UtcNow),
                    ErrorCount = thresholdStats.AdaptationErrors,
                    StatisticsStartTime = Configuration?.CustomParameters?["StatisticsStartTime"] as DateTime? ?? DateTime.UtcNow
                };

                lock (_lock)
                {
                    stats.LastDetectionTimestamp = _processStates.Values
                        .Select(ps => ps.LastDetectionTime)
                        .OrderByDescending(t => t)
                        .FirstOrDefault();
                }

                return stats;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("GetStatisticsAsync", ex, ErrorSeverity.Low);
                throw;
            }
        }

        public async Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_lock)
                {
                    _processStates.Clear();
                }

                _logger.LogInformation("Detector statistics reset successfully");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("ResetStatisticsAsync", ex, ErrorSeverity.Low);
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

                // Validate required settings
                if (configuration.DetectionIntervalSeconds < 10)
                {
                    result.AddError("Detection interval must be at least 10 seconds");
                }

                if (configuration.IdleThresholdSeconds < 60)
                {
                    result.AddError("Idle threshold must be at least 60 seconds");
                }

                if (configuration.ConfidenceThreshold < 0.0 || configuration.ConfidenceThreshold > 1.0)
                {
                    result.AddError("Confidence threshold must be between 0.0 and 1.0");
                }

                // Validate custom parameters
                if (configuration.CustomParameters != null)
                {
                    // Validate time-based detection specific parameters
                    if (configuration.CustomParameters.ContainsKey("WarningThresholdMinutes"))
                    {
                        var warningThreshold = Convert.ToInt32(configuration.CustomParameters["WarningThresholdMinutes"]);
                        if (warningThreshold < 1 || warningThreshold > 120)
                        {
                            result.AddError("Warning threshold must be between 1 and 120 minutes");
                        }
                    }

                    if (configuration.CustomParameters.ContainsKey("CriticalThresholdMinutes"))
                    {
                        var criticalThreshold = Convert.ToInt32(configuration.CustomParameters["CriticalThresholdMinutes"]);
                        if (criticalThreshold < 5 || criticalThreshold > 480)
                        {
                            result.AddError("Critical threshold must be between 5 and 480 minutes");
                        }
                    }
                }

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.AddError($"Configuration validation error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Private Methods

        private void DetectionTimerCallback(object state)
        {
            try
            {
                if (Status != DetectorStatus.Running)
                    return;

                // Perform periodic detection for all tracked processes
                lock (_lock)
                {
                    var processesToCheck = _processStates.Values.ToList();
                    foreach (var processState in processesToCheck)
                    {
                        try
                        {
                            // Fire and forget detection
                            _ = Task.Run(() => DetectIdleAsync(
                                processState.ProcessId,
                                processState.UserName,
                                processState.ComputerName));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in detection timer callback for process {ProcessId}",
                                processState.ProcessId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in detection timer callback");
            }
        }

        private ProcessDetectionState GetOrCreateProcessState(int processId, string userName, string computerName)
        {
            lock (_lock)
            {
                if (!_processStates.TryGetValue(processId, out var state))
                {
                    state = new ProcessDetectionState
                    {
                        ProcessId = processId,
                        UserName = userName,
                        ComputerName = computerName,
                        CreatedTime = DateTime.UtcNow,
                        LastDetectionTime = DateTime.UtcNow,
                        LastDetectionLevel = DetectionLevel.Active,
                        WasIdle = false
                    };

                    _processStates[processId] = state;
                    _logger.LogDebug("Created new process state for process {ProcessId}", processId);
                }

                return state;
            }
        }

        private void UpdateProcessState(ProcessDetectionState state, IdleDetectionResult result, DetectionLevel detectionLevel)
        {
            lock (_lock)
            {
                state.LastDetectionTime = DateTime.UtcNow;
                state.LastDetectionLevel = detectionLevel;
                state.WasIdle = result.IsIdle;
                state.TotalDetections++;
                state.LastIdleTime = result.IsIdle ? DateTime.UtcNow : state.LastIdleTime;

                if (result.IsIdle)
                {
                    state.IdleDetections++;
                }
                else
                {
                    state.ActiveDetections++;
                }
            }
        }

        private double CalculateConfidence(TimeSpan idleTime, DetectionLevel level, EffectiveThresholds thresholds, ProcessDetectionState state)
        {
            var baseConfidence = 0.0;

            switch (level)
            {
                case DetectionLevel.Active:
                    baseConfidence = 0.9;
                    break;
                case DetectionLevel.Warning:
                    var warningRatio = idleTime.TotalMinutes / thresholds.WarningThreshold.TotalMinutes;
                    baseConfidence = Math.Min(0.8, warningRatio * 0.5 + 0.3);
                    break;
                case DetectionLevel.Imminent:
                    var imminentRatio = idleTime.TotalMinutes / thresholds.ImminentThreshold.TotalMinutes;
                    baseConfidence = Math.Min(0.9, imminentRatio * 0.6 + 0.4);
                    break;
                case DetectionLevel.Critical:
                    var criticalRatio = idleTime.TotalMinutes / thresholds.CriticalThreshold.TotalMinutes;
                    baseConfidence = Math.Min(0.95, criticalRatio * 0.7 + 0.5);
                    break;
                case DetectionLevel.Release:
                    var releaseRatio = idleTime.TotalMinutes / thresholds.CriticalThreshold.TotalMinutes;
                    baseConfidence = Math.Min(1.0, releaseRatio * 0.8 + 0.6);
                    break;
            }

            // Adjust confidence based on detection consistency
            var consistencyFactor = state.TotalDetections > 0 ?
                (double)state.IdleDetections / state.TotalDetections : 0.5;

            // Apply threshold adaptation factor
            var adaptationFactor = thresholds.AdaptationFactor;

            return baseConfidence * consistencyFactor * adaptationFactor;
        }

        private double CalculateIdleConfidence(TimeSpan idleTime, DetectionLevel level)
        {
            switch (level)
            {
                case DetectionLevel.Active:
                    return 0.1;
                case DetectionLevel.Warning:
                    return 0.4;
                case DetectionLevel.Imminent:
                    return 0.7;
                case DetectionLevel.Critical:
                    return 0.9;
                case DetectionLevel.Release:
                    return 1.0;
                default:
                    return 0.0;
            }
        }

        private string GetDetectionReason(DetectionLevel level, TimeSpan idleTime, EffectiveThresholds thresholds)
        {
            switch (level)
            {
                case DetectionLevel.Active:
                    return $"System active (idle time: {idleTime.TotalMinutes:F1} minutes)";
                case DetectionLevel.Warning:
                    return $"Approaching warning threshold (idle time: {idleTime.TotalMinutes:F1}/{thresholds.WarningThreshold.TotalMinutes:F1} minutes)";
                case DetectionLevel.Imminent:
                    return $"Exceeded warning threshold (idle time: {idleTime.TotalMinutes:F1}/{thresholds.WarningThreshold.TotalMinutes:F1} minutes)";
                case DetectionLevel.Critical:
                    return $"Exceeded imminent threshold (idle time: {idleTime.TotalMinutes:F1}/{thresholds.ImminentThreshold.TotalMinutes:F1} minutes)";
                case DetectionLevel.Release:
                    return $"Exceeded critical threshold (idle time: {idleTime.TotalMinutes:F1}/{thresholds.CriticalThreshold.TotalMinutes:F1} minutes)";
                default:
                    return "Unknown detection state";
            }
        }

        private SessionState MapDetectionLevelToSessionState(DetectionLevel level)
        {
            switch (level)
            {
                case DetectionLevel.Active:
                    return SessionState.Active;
                case DetectionLevel.Warning:
                    return SessionState.Idle;
                case DetectionLevel.Imminent:
                    return SessionState.LongIdle;
                case DetectionLevel.Critical:
                case DetectionLevel.Release:
                    return SessionState.Inactive;
                default:
                    return SessionState.Unknown;
            }
        }

        private void UpdateStatus(DetectorStatus newStatus, string reason)
        {
            var oldStatus = Status;
            Status = newStatus;

            try
            {
                StatusChanged?.Invoke(this, new DetectorStatusChangedEventArgs(Name, oldStatus, newStatus, reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising StatusChanged event");
            }
        }

        private void OnIdleDetected(IdleDetectionResult result)
        {
            try
            {
                IdleDetected?.Invoke(this, new IdleDetectionEventArgs(
                    result.SessionId,
                    result.ProcessId,
                    result.UserName,
                    result.ComputerName,
                    result.Confidence,
                    result.IdleTime,
                    result.DetectionMethod,
                    result.Reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising IdleDetected event");
            }
        }

        private void OnActivityDetected(IdleDetectionResult result)
        {
            try
            {
                ActivityDetected?.Invoke(this, new IdleDetectionEventArgs(
                    result.SessionId,
                    result.ProcessId,
                    result.UserName,
                    result.ComputerName,
                    result.Confidence,
                    result.IdleTime,
                    result.DetectionMethod,
                    result.Reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising ActivityDetected event");
            }
        }

        private void OnActivityDetected(object sender, ActivityData activity)
        {
            // This handles activity detected by the activity monitor
            _logger.LogDebug("Activity detected by monitor: {ActivityType}, Confidence: {Confidence:F2}",
                activity.ActivityType, activity.Confidence);
        }

        private void OnThresholdsAdapted(object sender, ThresholdAdaptedEventArgs e)
        {
            _logger.LogDebug("Thresholds adapted for user {UserKey}: {Reason}", e.UserKey, e.AdaptationReason);
        }

        private async Task HandleErrorAsync(string operation, Exception ex, ErrorSeverity severity)
        {
            _logger.LogError(ex, "Error in {Operation}", operation);

            try
            {
                ErrorOccurred?.Invoke(this, new DetectorErrorEventArgs(Name, ex, severity, operation));
            }
            catch (Exception eventEx)
            {
                _logger.LogError(eventEx, "Error raising ErrorOccurred event");
            }

            if (severity == ErrorSeverity.Critical)
            {
                UpdateStatus(DetectorStatus.Error, $"Critical error in {operation}: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

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
                    _logger.LogInformation("Disposing TimeBasedIdleDetector");

                    // Stop detection
                    StopAsync().Wait(TimeSpan.FromSeconds(5));

                    // Stop activity monitor
                    _activityMonitor?.StopAsync().Wait(TimeSpan.FromSeconds(5));

                    // Dispose components
                    _detectionTimer?.Dispose();
                    _cancellationTokenSource?.Dispose();
                    _activityMonitor?.Dispose();
                    _thresholdManager?.Dispose();

                    // Clear process states
                    lock (_lock)
                    {
                        _processStates.Clear();
                    }

                    UpdateStatus(DetectorStatus.Disposing, "Detector disposed");
                }

                _isDisposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents the detection state for a process
    /// </summary>
    public class ProcessDetectionState
    {
        public int ProcessId { get; set; }
        public string UserName { get; set; }
        public string ComputerName { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastDetectionTime { get; set; }
        public DetectionLevel LastDetectionLevel { get; set; }
        public bool WasIdle { get; set; }
        public DateTime? LastIdleTime { get; set; }
        public long TotalDetections { get; set; }
        public long IdleDetections { get; set; }
        public long ActiveDetections { get; set; }

        public ProcessDetectionState()
        {
            CreatedTime = DateTime.UtcNow;
            LastDetectionTime = DateTime.UtcNow;
            LastDetectionLevel = DetectionLevel.Active;
            WasIdle = false;
            TotalDetections = 0;
            IdleDetections = 0;
            ActiveDetections = 0;
        }
    }
}