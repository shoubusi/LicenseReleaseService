using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.TimerExecution;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Main orchestrator for idle detection, coordinating multiple detectors and consensus logic
    /// </summary>
    public class IdleDetectionEngine : IDisposable
    {
        private readonly object _lock = new object();
        private readonly ILogger<IdleDetectionEngine> _logger;
        private readonly ITimerExecutionService _timerService;
        private readonly SessionStateManager _sessionStateManager;
        private readonly DetectionConsensusEngine _consensusEngine;
        private readonly IdleDetectionConfigurationElement _configuration;
        private readonly List<IIdleDetector> _detectors;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly TimeSpan _detectionInterval;
        private bool _isDisposed;
        private bool _isInitialized;
        private bool _isRunning;
        private DateTime _lastExecutionTime;
        private long _totalDetections;
        private long _successfulDetections;

        #region Events

        /// <summary>
        /// Event raised when idle state is detected by consensus
        /// </summary>
        public event EventHandler<IdleDetectionEventArgs> IdleDetected;

        /// <summary>
        /// Event raised when activity is detected by consensus
        /// </summary>
        public event EventHandler<IdleDetectionEventArgs> ActivityDetected;

        /// <summary>
        /// Event raised when engine state changes
        /// </summary>
        public event EventHandler<EngineStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Event raised when engine encounters an error
        /// </summary>
        public event EventHandler<EngineErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Event raised when detection cycle completes
        /// </summary>
        public event EventHandler<DetectionCycleCompletedEventArgs> DetectionCycleCompleted;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the engine status
        /// </summary>
        public EngineStatus Status { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the engine is running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets a value indicating whether the engine is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the configuration
        /// </summary>
        public IdleDetectionConfigurationElement Configuration => _configuration;

        /// <summary>
        /// Gets the registered detectors
        /// </summary>
        public IReadOnlyList<IIdleDetector> Detectors => _detectors.AsReadOnly();

        /// <summary>
        /// Gets the last execution time
        /// </summary>
        public DateTime LastExecutionTime => _lastExecutionTime;

        /// <summary>
        /// Gets the total number of detections performed
        /// </summary>
        public long TotalDetections => _totalDetections;

        /// <summary>
        /// Gets the number of successful detections
        /// </summary>
        public long SuccessfulDetections => _successfulDetections;

        /// <summary>
        /// Gets the detection success rate
        /// </summary>
        public double SuccessRate => _totalDetections > 0 ? (double)_successfulDetections / _totalDetections : 0.0;

        #endregion

        /// <summary>
        /// Initializes a new instance of the IdleDetectionEngine class
        /// </summary>
        /// <param name="timerService">The timer execution service</param>
        /// <param name="sessionStateManager">The session state manager</param>
        /// <param name="consensusEngine">The consensus engine</param>
        /// <param name="configuration">The configuration</param>
        /// <param name="logger">The logger</param>
        public IdleDetectionEngine(
            ITimerExecutionService timerService,
            SessionStateManager sessionStateManager,
            DetectionConsensusEngine consensusEngine,
            IdleDetectionConfigurationElement configuration,
            ILogger<IdleDetectionEngine> logger)
        {
            _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
            _sessionStateManager = sessionStateManager ?? throw new ArgumentNullException(nameof(sessionStateManager));
            _consensusEngine = consensusEngine ?? throw new ArgumentNullException(nameof(consensusEngine));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _detectors = new List<IIdleDetector>();
            _cancellationTokenSource = new CancellationTokenSource();
            _detectionInterval = TimeSpan.FromMinutes(_configuration.DetectionInterval);
            Status = EngineStatus.NotInitialized;
            _lastExecutionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes the engine and its components
        /// </summary>
        /// <returns>Task representing the initialization operation</returns>
        public async Task InitializeAsync()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(IdleDetectionEngine));

                if (_isInitialized)
                    return;
            }

            try
            {
                _logger.LogInformation("Initializing IdleDetectionEngine");

                // Initialize session state manager
                await _sessionStateManager.InitializeAsync(_cancellationTokenSource.Token);

                // Initialize consensus engine
                await _consensusEngine.InitializeAsync(_cancellationTokenSource.Token);

                // Wire up timer events
                _timerService.ExecutionStarted += OnTimerExecutionStarted;
                _timerService.ExecutionCompleted += OnTimerExecutionCompleted;
                _timerService.ExecutionError += OnTimerExecutionError;
                _timerService.StateChanged += OnTimerStateChanged;

                // Wire up consensus engine events
                _consensusEngine.IdleDetected += OnConsensusIdleDetected;
                _consensusEngine.ActivityDetected += OnConsensusActivityDetected;
                _consensusEngine.ErrorOccurred += OnConsensusErrorOccurred;

                _isInitialized = true;
                Status = EngineStatus.Initialized;

                OnStateChanged(EngineStatus.NotInitialized, EngineStatus.Initialized, "Engine initialized successfully");

                _logger.LogInformation("IdleDetectionEngine initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing IdleDetectionEngine");
                Status = EngineStatus.Error;
                OnErrorOccurred("InitializeAsync", ex, ErrorSeverity.High);
                throw;
            }
        }

        /// <summary>
        /// Starts the idle detection engine
        /// </summary>
        /// <returns>Task representing the start operation</returns>
        public async Task StartAsync()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(IdleDetectionEngine));

                if (!_isInitialized)
                    throw new InvalidOperationException("Engine must be initialized before starting");

                if (_isRunning)
                    return;
            }

            try
            {
                _logger.LogInformation("Starting IdleDetectionEngine");

                // Start all detectors
                foreach (var detector in _detectors.Where(d => d.IsEnabled))
                {
                    try
                    {
                        await detector.StartAsync(_cancellationTokenSource.Token);
                        _logger.LogDebug("Started detector: {DetectorName}", detector.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to start detector: {DetectorName}", detector.Name);
                        OnErrorOccurred($"Start detector {detector.Name}", ex, ErrorSeverity.Medium);
                    }
                }

                // Start the timer service
                await _timerService.StartAsync(_detectionInterval, _cancellationTokenSource.Token);

                _isRunning = true;
                Status = EngineStatus.Running;

                OnStateChanged(EngineStatus.Initialized, EngineStatus.Running, "Engine started successfully");

                _logger.LogInformation("IdleDetectionEngine started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting IdleDetectionEngine");
                Status = EngineStatus.Error;
                OnErrorOccurred("StartAsync", ex, ErrorSeverity.High);
                throw;
            }
        }

        /// <summary>
        /// Stops the idle detection engine
        /// </summary>
        /// <returns>Task representing the stop operation</returns>
        public async Task StopAsync()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(IdleDetectionEngine));

                if (!_isRunning)
                    return;
            }

            try
            {
                _logger.LogInformation("Stopping IdleDetectionEngine");

                // Stop the timer service
                await _timerService.StopAsync();

                // Stop all detectors
                foreach (var detector in _detectors)
                {
                    try
                    {
                        await detector.StopAsync(_cancellationTokenSource.Token);
                        _logger.LogDebug("Stopped detector: {DetectorName}", detector.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to stop detector: {DetectorName}", detector.Name);
                        OnErrorOccurred($"Stop detector {detector.Name}", ex, ErrorSeverity.Low);
                    }
                }

                _isRunning = false;
                Status = EngineStatus.Stopped;

                OnStateChanged(EngineStatus.Running, EngineStatus.Stopped, "Engine stopped successfully");

                _logger.LogInformation("IdleDetectionEngine stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping IdleDetectionEngine");
                Status = EngineStatus.Error;
                OnErrorOccurred("StopAsync", ex, ErrorSeverity.High);
                throw;
            }
        }

        /// <summary>
        /// Registers an idle detector with the engine
        /// </summary>
        /// <param name="detector">The detector to register</param>
        /// <returns>Task representing the registration operation</returns>
        public async Task RegisterDetectorAsync(IIdleDetector detector)
        {
            if (detector == null)
                throw new ArgumentNullException(nameof(detector));

            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(IdleDetectionEngine));

                if (_detectors.Any(d => d.Name == detector.Name))
                    throw new InvalidOperationException($"Detector with name '{detector.Name}' is already registered");
            }

            try
            {
                _logger.LogInformation("Registering detector: {DetectorName}", detector.Name);

                // Initialize detector
                var detectorConfig = ConvertToIdleDetectorConfiguration(_configuration);
                await detector.InitializeAsync(detectorConfig, _cancellationTokenSource.Token);

                // Wire up detector events
                detector.IdleDetected += OnDetectorIdleDetected;
                detector.ActivityDetected += OnDetectorActivityDetected;
                detector.ErrorOccurred += OnDetectorErrorOccurred;

                lock (_lock)
                {
                    _detectors.Add(detector);
                    _detectors.Sort((d1, d2) => d2.Priority.CompareTo(d1.Priority));
                }

                // Add detector to consensus engine
                _consensusEngine.AddDetector(detector);

                // Start detector if engine is running
                if (_isRunning && detector.IsEnabled)
                {
                    await detector.StartAsync(_cancellationTokenSource.Token);
                }

                _logger.LogInformation("Detector registered successfully: {DetectorName}", detector.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering detector: {DetectorName}", detector.Name);
                OnErrorOccurred($"Register detector {detector.Name}", ex, ErrorSeverity.High);
                throw;
            }
        }

        /// <summary>
        /// Unregisters an idle detector from the engine
        /// </summary>
        /// <param name="detectorName">The name of the detector to unregister</param>
        /// <returns>Task representing the unregistration operation</returns>
        public async Task UnregisterDetectorAsync(string detectorName)
        {
            if (string.IsNullOrWhiteSpace(detectorName))
                throw new ArgumentNullException(nameof(detectorName));

            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(IdleDetectionEngine));
            }

            IIdleDetector detector = null;
            lock (_lock)
            {
                detector = _detectors.FirstOrDefault(d => d.Name == detectorName);
                if (detector == null)
                    return;
            }

            try
            {
                _logger.LogInformation("Unregistering detector: {DetectorName}", detectorName);

                // Stop detector
                await detector.StopAsync(_cancellationTokenSource.Token);

                // Remove detector event handlers
                detector.IdleDetected -= OnDetectorIdleDetected;
                detector.ActivityDetected -= OnDetectorActivityDetected;
                detector.ErrorOccurred -= OnDetectorErrorOccurred;

                lock (_lock)
                {
                    _detectors.Remove(detector);
                }

                // Remove detector from consensus engine
                _consensusEngine.RemoveDetector(detectorName);

                _logger.LogInformation("Detector unregistered successfully: {DetectorName}", detectorName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering detector: {DetectorName}", detectorName);
                OnErrorOccurred($"Unregister detector {detectorName}", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        /// <summary>
        /// Performs idle detection for a specific process
        /// </summary>
        /// <param name="processId">The process identifier</param>
        /// <param name="userName">The user name</param>
        /// <param name="computerName">The computer name</param>
        /// <returns>Idle detection result</returns>
        public async Task<IdleDetectionResult> DetectIdleAsync(int processId, string userName, string computerName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrWhiteSpace(computerName))
                throw new ArgumentNullException(nameof(computerName));

            if (!_isInitialized || !_isRunning)
                throw new InvalidOperationException("Engine is not running");

            try
            {
                _logger.LogDebug("Performing idle detection for process {ProcessId}", processId);

                var sessionId = GenerateSessionId(processId, userName, computerName);
                var processInfo = new ProcessInfo(processId, string.Empty, userName, computerName);

                // Use consensus engine to get result
                var result = await _consensusEngine.DetectIdleAsync(processInfo, _cancellationTokenSource.Token);

                Interlocked.Increment(ref _totalDetections);
                if (result != null)
                {
                    Interlocked.Increment(ref _successfulDetections);
                }

                _lastExecutionTime = DateTime.UtcNow;

                return result ?? new IdleDetectionResult
                {
                    ProcessId = processId,
                    UserName = userName,
                    ComputerName = computerName,
                    IsIdle = false,
                    Confidence = 0.0,
                    DetectionMethod = "Consensus",
                    Reason = "No detection result available"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting idle state for process {ProcessId}", processId);
                Interlocked.Increment(ref _totalDetections);
                OnErrorOccurred($"DetectIdle for process {processId}", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        /// <summary>
        /// Forces an immediate detection cycle
        /// </summary>
        /// <returns>Task representing the forced detection operation</returns>
        public async Task ForceDetectionAsync()
        {
            if (!_isInitialized || !_isRunning)
                throw new InvalidOperationException("Engine is not running");

            try
            {
                _logger.LogInformation("Forcing immediate detection cycle");

                // Execute detection cycle immediately
                await _timerService.ExecuteNowAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing detection cycle");
                OnErrorOccurred("ForceDetection", ex, ErrorSeverity.Medium);
                throw;
            }
        }

        /// <summary>
        /// Gets engine statistics
        /// </summary>
        /// <returns>Engine statistics</returns>
        public EngineStatistics GetStatistics()
        {
            return new EngineStatistics
            {
                Status = Status,
                IsRunning = _isRunning,
                IsInitialized = _isInitialized,
                TotalDetections = _totalDetections,
                SuccessfulDetections = _successfulDetections,
                SuccessRate = SuccessRate,
                LastExecutionTime = _lastExecutionTime,
                DetectorCount = _detectors.Count,
                EnabledDetectorCount = _detectors.Count(d => d.IsEnabled),
                Uptime = DateTime.UtcNow - _lastExecutionTime,
                DetectionInterval = _detectionInterval
            };
        }

        /// <summary>
        /// Gets detailed health information
        /// </summary>
        /// <returns>Health information</returns>
        public async Task<EngineHealth> GetHealthAsync()
        {
            var health = new EngineHealth
            {
                EngineName = nameof(IdleDetectionEngine),
                Status = Status,
                IsHealthy = Status == EngineStatus.Running,
                CheckTimestamp = DateTime.UtcNow,
                Statistics = GetStatistics()
            };

            // Check timer service health
            try
            {
                var timerMetrics = _timerService.GetMetrics();
                health.TimerServiceHealth = new ComponentHealth
                {
                    ComponentName = "TimerService",
                    IsHealthy = _timerService.IsRunning,
                    StatusMessage = _timerService.IsRunning ? "Running" : "Not running",
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["CurrentInterval"] = _timerService.CurrentInterval,
                        ["State"] = _timerService.State,
                        ["Metrics"] = timerMetrics
                    }
                };
            }
            catch (Exception ex)
            {
                health.TimerServiceHealth = new ComponentHealth
                {
                    ComponentName = "TimerService",
                    IsHealthy = false,
                    StatusMessage = $"Error checking health: {ex.Message}",
                    LastError = ex
                };
            }

            // Check detectors health
            health.DetectorHealth = new List<ComponentHealth>();
            foreach (var detector in _detectors)
            {
                try
                {
                    var detectorHealth = await detector.GetHealthAsync();
                    health.DetectorHealth.Add(new ComponentHealth
                    {
                        ComponentName = detector.Name,
                        IsHealthy = detectorHealth.IsHealthy,
                        StatusMessage = detectorHealth.StatusMessage,
                        LastError = detectorHealth.LastError,
                        AdditionalInfo = detectorHealth.AdditionalInfo
                    });
                }
                catch (Exception ex)
                {
                    health.DetectorHealth.Add(new ComponentHealth
                    {
                        ComponentName = detector.Name,
                        IsHealthy = false,
                        StatusMessage = $"Error checking health: {ex.Message}",
                        LastError = ex
                    });
                }
            }

            // Overall health assessment
            var allHealthy = health.TimerServiceHealth.IsHealthy &&
                           health.DetectorHealth.All(d => d.IsHealthy);
            health.IsHealthy = allHealthy && Status == EngineStatus.Running;
            health.StatusMessage = health.IsHealthy ? "Healthy" : "Unhealthy";

            return health;
        }

        #region Event Handlers

        private async void OnTimerExecutionStarted(object sender, TimerExecutionEventArgs e)
        {
            try
            {
                _logger.LogDebug("Timer execution started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in timer execution started handler");
            }
        }

        private async void OnTimerExecutionCompleted(object sender, TimerExecutionEventArgs e)
        {
            try
            {
                _logger.LogDebug("Timer execution completed");

                // Raise detection cycle completed event
                var args = new DetectionCycleCompletedEventArgs
                {
                    ExecutionId = e.ExecutionId,
                    Timestamp = DateTime.UtcNow,
                    Success = true,
                    Duration = e.Duration,
                    ProcessedCount = 0 // TODO: Track actual processed count
                };
                OnDetectionCycleCompleted(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in timer execution completed handler");
                OnErrorOccurred("TimerExecutionCompleted", ex, ErrorSeverity.Low);
            }
        }

        private void OnTimerExecutionError(object sender, TimerExecutionErrorEventArgs e)
        {
            _logger.LogError(e.Error, "Timer execution error: {Message}", e.FormattedMessage);
            OnErrorOccurred("TimerExecution", e.Error, ErrorSeverity.High);
        }

        private void OnTimerStateChanged(object sender, TimerStateChangedEventArgs e)
        {
            _logger.LogDebug("Timer state changed: {OldState} -> {NewState}", e.PreviousState, e.NewState);
        }

        private void OnConsensusIdleDetected(object sender, IdleDetectionEventArgs e)
        {
            _logger.LogInformation("Consensus idle detected: {SessionId}, Process: {ProcessId}, IdleTime: {IdleTime}",
                e.SessionId, e.ProcessId, e.IdleTime);
            OnIdleDetected(e);
        }

        private void OnConsensusActivityDetected(object sender, IdleDetectionEventArgs e)
        {
            _logger.LogDebug("Consensus activity detected: {SessionId}, Process: {ProcessId}",
                e.SessionId, e.ProcessId);
            OnActivityDetected(e);
        }

        private void OnConsensusErrorOccurred(object sender, ConsensusErrorEventArgs e)
        {
            _logger.LogError(e.Error, "Consensus engine error: {DetectorName}, {Operation}",
                e.DetectorName, e.Operation);
            OnErrorOccurred($"Consensus {e.Operation}", e.Error, e.Severity);
        }

        private void OnDetectorIdleDetected(object sender, IdleDetectionEventArgs e)
        {
            _logger.LogDebug("Detector idle detected: {DetectorName}, {SessionId}",
                (sender as IIdleDetector)?.Name, e.SessionId);
        }

        private void OnDetectorActivityDetected(object sender, IdleDetectionEventArgs e)
        {
            _logger.LogDebug("Detector activity detected: {DetectorName}, {SessionId}",
                (sender as IIdleDetector)?.Name, e.SessionId);
        }

        private void OnDetectorErrorOccurred(object sender, DetectorErrorEventArgs e)
        {
            _logger.LogError(e.Error, "Detector error: {DetectorName}, {Operation}",
                e.DetectorName, e.Operation);
            OnErrorOccurred($"Detector {e.Operation}", e.Error, e.Severity);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Converts a configuration element to an IdleDetectorConfiguration
        /// </summary>
        private IdleDetectorConfiguration ConvertToIdleDetectorConfiguration(IdleDetectionConfigurationElement element)
        {
            return new IdleDetectorConfiguration
            {
                DetectorName = element.DetectorName,
                IsEnabled = element.IsEnabled,
                DetectionIntervalSeconds = element.DetectionIntervalSeconds,
                IdleThresholdSeconds = element.IdleThresholdSeconds,
                ConfidenceThreshold = element.ConfidenceThreshold,
                Priority = element.Priority,
                MaxDetectionTimeMs = element.MaxDetectionTimeMs,
                TimeoutSeconds = element.TimeoutSeconds,
                RetryCount = element.RetryCount,
                RetryDelayMs = element.RetryDelayMs,
                MaxConcurrentOperations = element.MaxConcurrentOperations,
                CustomParameters = ParseCustomParameters(element.CustomParameters)
            };
        }

        /// <summary>
        /// Parses custom parameters from a string
        /// </summary>
        /// <param name="customParams">Custom parameters string</param>
        /// <returns>Dictionary of parsed parameters</returns>
        private Dictionary<string, object> ParseCustomParameters(string customParams)
        {
            var parameters = new Dictionary<string, object>();

            if (string.IsNullOrWhiteSpace(customParams))
                return parameters;

            try
            {
                // Simple key=value parsing separated by semicolons or commas
                var pairs = customParams.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var keyValue = pair.Split(new[] { '=' }, 2);
                    if (keyValue.Length == 2)
                    {
                        var key = keyValue[0].Trim();
                        var value = keyValue[1].Trim();

                        // Try to parse as different types
                        if (bool.TryParse(value, out var boolValue))
                            parameters[key] = boolValue;
                        else if (int.TryParse(value, out var intValue))
                            parameters[key] = intValue;
                        else if (double.TryParse(value, out var doubleValue))
                            parameters[key] = doubleValue;
                        else
                            parameters[key] = value; // Keep as string
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing custom parameters: {CustomParameters}", customParams);
            }

            return parameters;
        }

        #endregion

        #region Event Raisers

        protected virtual void OnIdleDetected(IdleDetectionEventArgs e)
        {
            IdleDetected?.Invoke(this, e);
        }

        protected virtual void OnActivityDetected(IdleDetectionEventArgs e)
        {
            ActivityDetected?.Invoke(this, e);
        }

        protected virtual void OnStateChanged(EngineStatus previousStatus, EngineStatus newStatus, string reason)
        {
            StateChanged?.Invoke(this, new EngineStateChangedEventArgs(previousStatus, newStatus, reason));
        }

        protected virtual void OnErrorOccurred(string operation, Exception error, ErrorSeverity severity)
        {
            ErrorOccurred?.Invoke(this, new EngineErrorEventArgs(operation, error, severity));
        }

        protected virtual void OnDetectionCycleCompleted(DetectionCycleCompletedEventArgs e)
        {
            DetectionCycleCompleted?.Invoke(this, e);
        }

        #endregion

        #region Helper Methods

        private static string GenerateSessionId(int processId, string userName, string computerName)
        {
            return $"{processId}_{userName}_{computerName}".Replace(" ", "_").Replace("\\", "_");
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
                    // Stop the engine if running
                    if (_isRunning)
                    {
                        try
                        {
                            StopAsync().Wait();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error stopping engine during disposal");
                        }
                    }

                    // Cancel any pending operations
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();

                    // Dispose detectors
                    foreach (var detector in _detectors)
                    {
                        try
                        {
                            detector.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error disposing detector: {DetectorName}", detector.Name);
                        }
                    }

                    // Unwire event handlers
                    _timerService.ExecutionStarted -= OnTimerExecutionStarted;
                    _timerService.ExecutionCompleted -= OnTimerExecutionCompleted;
                    _timerService.ExecutionError -= OnTimerExecutionError;
                    _timerService.StateChanged -= OnTimerStateChanged;

                    _consensusEngine.IdleDetected -= OnConsensusIdleDetected;
                    _consensusEngine.ActivityDetected -= OnConsensusActivityDetected;
                    _consensusEngine.ErrorOccurred -= OnConsensusErrorOccurred;
                }

                _isDisposed = true;
                Status = EngineStatus.Disposed;
            }
        }

        #endregion
    }

    /// <summary>
    /// Defines the engine status
    /// </summary>
    public enum EngineStatus
    {
        /// <summary>
        /// Engine is not initialized
        /// </summary>
        NotInitialized,

        /// <summary>
        /// Engine is initialized but not running
        /// </summary>
        Initialized,

        /// <summary>
        /// Engine is running normally
        /// </summary>
        Running,

        /// <summary>
        /// Engine is paused
        /// </summary>
        Paused,

        /// <summary>
        /// Engine is stopped
        /// </summary>
        Stopped,

        /// <summary>
        /// Engine is in error state
        /// </summary>
        Error,

        /// <summary>
        /// Engine is disposed
        /// </summary>
        Disposed
    }

    /// <summary>
    /// Event arguments for engine state changes
    /// </summary>
    public class EngineStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous status
        /// </summary>
        public EngineStatus PreviousStatus { get; }

        /// <summary>
        /// Gets the new status
        /// </summary>
        public EngineStatus NewStatus { get; }

        /// <summary>
        /// Gets the reason for the state change
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the EngineStateChangedEventArgs class
        /// </summary>
        /// <param name="previousStatus">The previous status</param>
        /// <param name="newStatus">The new status</param>
        /// <param name="reason">The reason for the change</param>
        public EngineStateChangedEventArgs(EngineStatus previousStatus, EngineStatus newStatus, string reason)
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the state change event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"EngineStateChanged: {PreviousStatus} -> {NewStatus}, Reason: {Reason}";
        }
    }

    /// <summary>
    /// Event arguments for engine errors
    /// </summary>
    public class EngineErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the operation that failed
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Gets the error exception
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Gets the error severity
        /// </summary>
        public ErrorSeverity Severity { get; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the EngineErrorEventArgs class
        /// </summary>
        /// <param name="operation">The operation that failed</param>
        /// <param name="error">The error exception</param>
        /// <param name="severity">The error severity</param>
        public EngineErrorEventArgs(string operation, Exception error, ErrorSeverity severity)
        {
            Operation = operation;
            Error = error;
            Severity = severity;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the error event
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"EngineError: {Operation}, {Severity}, Error: {Error.Message}";
        }
    }

    /// <summary>
    /// Event arguments for detection cycle completion
    /// </summary>
    public class DetectionCycleCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the execution identifier
        /// </summary>
        public Guid ExecutionId { get; set; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets a value indicating whether the cycle was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the cycle duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets the number of processes processed
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// Gets additional information
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; }

        /// <summary>
        /// Initializes a new instance of the DetectionCycleCompletedEventArgs class
        /// </summary>
        public DetectionCycleCompletedEventArgs()
        {
            AdditionalInfo = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Represents engine statistics
    /// </summary>
    public class EngineStatistics
    {
        /// <summary>
        /// Gets the engine status
        /// </summary>
        public EngineStatus Status { get; set; }

        /// <summary>
        /// Gets a value indicating whether the engine is running
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Gets a value indicating whether the engine is initialized
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// Gets the total number of detections
        /// </summary>
        public long TotalDetections { get; set; }

        /// <summary>
        /// Gets the number of successful detections
        /// </summary>
        public long SuccessfulDetections { get; set; }

        /// <summary>
        /// Gets the success rate
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets the last execution time
        /// </summary>
        public DateTime LastExecutionTime { get; set; }

        /// <summary>
        /// Gets the number of registered detectors
        /// </summary>
        public int DetectorCount { get; set; }

        /// <summary>
        /// Gets the number of enabled detectors
        /// </summary>
        public int EnabledDetectorCount { get; set; }

        /// <summary>
        /// Gets the uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the detection interval
        /// </summary>
        public TimeSpan DetectionInterval { get; set; }
    }

    /// <summary>
    /// Represents engine health information
    /// </summary>
    public class EngineHealth
    {
        /// <summary>
        /// Gets the engine name
        /// </summary>
        public string EngineName { get; set; }

        /// <summary>
        /// Gets the engine status
        /// </summary>
        public EngineStatus Status { get; set; }

        /// <summary>
        /// Gets a value indicating whether the engine is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets the health check timestamp
        /// </summary>
        public DateTime CheckTimestamp { get; set; }

        /// <summary>
        /// Gets the health status message
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Gets the timer service health
        /// </summary>
        public ComponentHealth TimerServiceHealth { get; set; }

        /// <summary>
        /// Gets the detector health information
        /// </summary>
        public List<ComponentHealth> DetectorHealth { get; set; }

        /// <summary>
        /// Gets the engine statistics
        /// </summary>
        public EngineStatistics Statistics { get; set; }

        /// <summary>
        /// Initializes a new instance of the EngineHealth class
        /// </summary>
        public EngineHealth()
        {
            DetectorHealth = new List<ComponentHealth>();
            CheckTimestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents component health information
    /// </summary>
    public class ComponentHealth
    {
        /// <summary>
        /// Gets the component name
        /// </summary>
        public string ComponentName { get; set; }

        /// <summary>
        /// Gets a value indicating whether the component is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets the health status message
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Gets the last error, if any
        /// </summary>
        public Exception LastError { get; set; }

        /// <summary>
        /// Gets additional information
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; }

        /// <summary>
        /// Initializes a new instance of the ComponentHealth class
        /// </summary>
        public ComponentHealth()
        {
            AdditionalInfo = new Dictionary<string, object>();
        }
    }
}