using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Implements multi-version scheduling with TimerExecution integration
    /// </summary>
    public class VersionScheduler : IVersionScheduler, IDisposable
    {
        private readonly ILogger<VersionScheduler> _logger;
        private readonly VersionSchedulerOptions _options;
        private readonly IVersionRuntimeManager _runtimeManager;
        private readonly IVersionResourceManager _resourceManager;
        private readonly ITimerExecutionService _timerExecutionService;
        private readonly object _lock = new object();
        private readonly Dictionary<string, ScheduledVersionOperation> _scheduledOperations;
        private readonly Dictionary<string, ITimerExecutionService> _versionTimers;
        private readonly Queue<VersionOperationRequest> _operationQueue;
        private bool _isDisposed;
        private CancellationTokenSource _globalCancellationTokenSource;
        private readonly SemaphoreSlim _schedulingSemaphore;
        private Task _queueProcessorTask;
        private bool _isQueueProcessing;

        #region Events

        /// <inheritdoc/>
        public event EventHandler<VersionScheduledEventArgs> OperationScheduled;

        /// <inheritdoc/>
        public event EventHandler<VersionOperationEventArgs> OperationStarted;

        /// <inheritdoc/>
        public event EventHandler<VersionOperationEventArgs> OperationCompleted;

        /// <inheritdoc/>
        public event EventHandler<VersionOperationErrorEventArgs> OperationError;

        /// <inheritdoc/>
        public event EventHandler<VersionSchedulerStatusChangedEventArgs> StatusChanged;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public VersionSchedulerStatus Status { get; private set; }

        /// <inheritdoc/>
        public bool IsRunning => Status == VersionSchedulerStatus.Running;

        /// <inheritdoc/>
        public int ScheduledOperationCount => _scheduledOperations.Count;

        /// <inheritdoc/>
        public int QueuedOperationCount => _operationQueue.Count;

        /// <inheritdoc/>
        public int ActiveTimerCount => _versionTimers.Count;

        #endregion

        /// <summary>
        /// Initializes a new instance of the VersionScheduler class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The scheduler options</param>
        /// <param name="runtimeManager">The version runtime manager</param>
        /// <param name="resourceManager">The version resource manager</param>
        /// <param name="timerExecutionService">The timer execution service for global operations</param>
        public VersionScheduler(
            ILogger<VersionScheduler> logger,
            IOptions<VersionSchedulerOptions> options,
            IVersionRuntimeManager runtimeManager,
            IVersionResourceManager resourceManager,
            ITimerExecutionService timerExecutionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _timerExecutionService = timerExecutionService ?? throw new ArgumentNullException(nameof(timerExecutionService));

            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            ValidateOptions(_options);

            _scheduledOperations = new Dictionary<string, ScheduledVersionOperation>();
            _versionTimers = new Dictionary<string, ITimerExecutionService>();
            _operationQueue = new Queue<VersionOperationRequest>();
            _schedulingSemaphore = new SemaphoreSlim(_options.MaxConcurrentSchedules);
            _globalCancellationTokenSource = new CancellationTokenSource();

            Status = VersionSchedulerStatus.Stopped;

            // Subscribe to timer execution events
            SubscribeToTimerEvents();
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionScheduler));

            bool needMaintenanceTimer;
            lock (_lock)
            {
                if (Status != VersionSchedulerStatus.Stopped)
                {
                    throw new InvalidOperationException($"Scheduler must be stopped to start. Current status: {Status}");
                }

                try
                {
                    // Link cancellation tokens
                    if (cancellationToken != CancellationToken.None)
                    {
                        cancellationToken.Register(() => _globalCancellationTokenSource.Cancel());
                    }

                    ChangeStatus(VersionSchedulerStatus.Starting, "Scheduler starting");

                    // Start the operation queue processor
                    StartQueueProcessor();

                    needMaintenanceTimer = _options.EnableMaintenanceOperations;
                }
                catch (Exception ex)
                {
                    ChangeStatus(VersionSchedulerStatus.Error, $"Failed to start scheduler: {ex.Message}");
                    _logger.LogError(ex, "Failed to start version scheduler");
                    throw;
                }
            }

            // Start global timer for maintenance operations (outside lock)
            if (needMaintenanceTimer)
            {
                await StartMaintenanceTimerAsync();
            }

            lock (_lock)
            {
                ChangeStatus(VersionSchedulerStatus.Running, "Scheduler started successfully");
                _logger.LogInformation("Version scheduler started successfully");
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync()
        {
            if (_isDisposed)
                return;

            VersionSchedulerStatus currentStatus;
            lock (_lock)
            {
                if (Status == VersionSchedulerStatus.Stopped || Status == VersionSchedulerStatus.Disposed)
                    return;

                currentStatus = Status;
                ChangeStatus(VersionSchedulerStatus.Stopping, "Scheduler stopping");

                // Cancel global operations
                _globalCancellationTokenSource.Cancel();
            }

            try
            {
                // Stop all version-specific timers (outside lock)
                await StopAllVersionTimersAsync();

                // Stop global timer (outside lock)
                await _timerExecutionService.StopAsync();

                lock (_lock)
                {
                    // Stop queue processor
                    StopQueueProcessor();

                    // Clear scheduled operations and queue
                    _scheduledOperations.Clear();
                    _operationQueue.Clear();

                    // Create new cancellation token source for future use
                    _globalCancellationTokenSource = new CancellationTokenSource();

                    ChangeStatus(VersionSchedulerStatus.Stopped, "Scheduler stopped successfully");
                    _logger.LogInformation("Version scheduler stopped successfully");
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    ChangeStatus(VersionSchedulerStatus.Error, $"Failed to stop scheduler: {ex.Message}");
                }
                _logger.LogError(ex, "Failed to stop version scheduler");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string> ScheduleOperationAsync(
            VersionOperationType operationType,
            string version,
            TimeSpan interval,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionScheduler));

            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or whitespace", nameof(version));

            if (Status != VersionSchedulerStatus.Running)
            {
                throw new InvalidOperationException($"Scheduler must be running to schedule operations. Current status: {Status}");
            }

            await _schedulingSemaphore.WaitAsync(cancellationToken);

            try
            {
                var operationId = GenerateOperationId();
                var scheduledOperation = new ScheduledVersionOperation
                {
                    OperationId = operationId,
                    OperationType = operationType,
                    Version = version,
                    Interval = interval,
                    Parameters = parameters ?? new Dictionary<string, object>(),
                    ScheduledAt = DateTime.UtcNow,
                    Status = ScheduledOperationStatus.Pending
                };

                // Create or get version-specific timer
                var timerService = await GetOrCreateVersionTimerAsync(version, interval);

                // Schedule the operation
                _scheduledOperations[operationId] = scheduledOperation;

                // Raise scheduled event
                var scheduledEventArgs = new VersionScheduledEventArgs(
                    operationId,
                    operationType,
                    version,
                    interval,
                    DateTime.UtcNow);
                OperationScheduled?.Invoke(this, scheduledEventArgs);

                _logger.LogInformation("Scheduled operation {OperationId} of type {OperationType} for version {Version} with interval {Interval}ms",
                    operationId, operationType, version, interval.TotalMilliseconds);

                return operationId;
            }
            finally
            {
                _schedulingSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<string> ScheduleOneTimeOperationAsync(
            VersionOperationType operationType,
            string version,
            TimeSpan delay,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionScheduler));

            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or whitespace", nameof(version));

            if (Status != VersionSchedulerStatus.Running)
            {
                throw new InvalidOperationException($"Scheduler must be running to schedule operations. Current status: {Status}");
            }

            await _schedulingSemaphore.WaitAsync(cancellationToken);

            try
            {
                var operationId = GenerateOperationId();
                var scheduledOperation = new ScheduledVersionOperation
                {
                    OperationId = operationId,
                    OperationType = operationType,
                    Version = version,
                    Interval = delay,
                    Parameters = parameters ?? new Dictionary<string, object>(),
                    ScheduledAt = DateTime.UtcNow,
                    Status = ScheduledOperationStatus.Pending,
                    IsOneTime = true
                };

                // Create one-time timer
                var timerService = await CreateOneTimeVersionTimerAsync(version, delay, operationId);

                // Schedule the operation
                _scheduledOperations[operationId] = scheduledOperation;

                // Raise scheduled event
                var scheduledEventArgs = new VersionScheduledEventArgs(
                    operationId,
                    operationType,
                    version,
                    delay,
                    DateTime.UtcNow,
                    isOneTime: true);
                OperationScheduled?.Invoke(this, scheduledEventArgs);

                _logger.LogInformation("Scheduled one-time operation {OperationId} of type {OperationType} for version {Version} with delay {Delay}ms",
                    operationId, operationType, version, delay.TotalMilliseconds);

                return operationId;
            }
            finally
            {
                _schedulingSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> CancelScheduledOperationAsync(string operationId)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionScheduler));

            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("Operation ID cannot be null or whitespace", nameof(operationId));

            ITimerExecutionService timerService = null;

            lock (_lock)
            {
                if (!_scheduledOperations.TryGetValue(operationId, out var operation))
                {
                    _logger.LogWarning("Attempted to cancel non-existent operation {OperationId}", operationId);
                    return false;
                }

                if (operation.Status == ScheduledOperationStatus.Cancelled ||
                    operation.Status == ScheduledOperationStatus.Completed)
                {
                    _logger.LogWarning("Cannot cancel operation {OperationId} with status {Status}", operationId, operation.Status);
                    return false;
                }

                try
                {
                    // Stop the version timer
                    if (_versionTimers.TryGetValue(operation.Version, out timerService))
                    {
                        _versionTimers.Remove(operation.Version);
                    }

                    // Update operation status
                    operation.Status = ScheduledOperationStatus.Cancelled;
                    operation.CancelledAt = DateTime.UtcNow;

                    _logger.LogInformation("Cancelled scheduled operation {OperationId}", operationId);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cancel operation {OperationId}", operationId);
                    return false;
                }
            }

            // Stop the timer service outside the lock
            if (timerService != null)
            {
                await timerService.StopAsync();
            }
        }

        /// <inheritdoc/>
        public Task<List<ScheduledVersionOperation>> GetScheduledOperationsAsync(string version = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionScheduler));

            List<ScheduledVersionOperation> operations;

            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(version))
                {
                    operations = _scheduledOperations.Values.ToList();
                }
                else
                {
                    operations = _scheduledOperations.Values
                        .Where(op => op.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            return Task.FromResult(operations);
        }

        /// <inheritdoc/>
        public Task<VersionSchedulerMetrics> GetMetricsAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionScheduler));

            var metrics = new VersionSchedulerMetrics
            {
                Timestamp = DateTime.UtcNow,
                Status = Status,
                TotalScheduledOperations = _scheduledOperations.Count,
                ActiveVersionTimers = _versionTimers.Count,
                QueuedOperations = _operationQueue.Count,
                MaxConcurrentSchedules = _options.MaxConcurrentSchedules,
                OperationHistory = GetOperationHistorySummary()
            };

            // Get timer metrics
            foreach (var (version, timer) in _versionTimers)
            {
                var timerMetrics = timer.GetMetrics();
                metrics.VersionMetrics[version] = new VersionTimerMetrics
                {
                    TotalExecutions = timerMetrics.TotalExecutions,
                    SuccessfulExecutions = timerMetrics.SuccessfulExecutions,
                    FailedExecutions = timerMetrics.FailedExecutions,
                    AverageExecutionDuration = timerMetrics.AverageExecutionDuration,
                    CurrentInterval = timerMetrics.CurrentInterval,
                    State = timer.State
                };
            }

            return Task.FromResult(metrics);
        }

        /// <inheritdoc/>
        public async Task ExecuteOperationAsync(
            VersionOperationType operationType,
            string version,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionScheduler));

            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or whitespace", nameof(version));

            var operationRequest = new VersionOperationRequest
            {
                OperationType = operationType,
                Version = version,
                Parameters = parameters ?? new Dictionary<string, object>(),
                RequestedAt = DateTime.UtcNow,
                IsImmediate = true
            };

            // Add to queue for processing
            lock (_lock)
            {
                _operationQueue.Enqueue(operationRequest);
            }

            // If queue processor isn't running, start it
            if (!_isQueueProcessing)
            {
                StartQueueProcessor();
            }

            _logger.LogInformation("Queued immediate operation of type {OperationType} for version {Version}",
                operationType, version);
        }

        #region Private Methods

        private void SubscribeToTimerEvents()
        {
            // Subscribe to global timer events
            _timerExecutionService.ExecutionStarted += OnGlobalExecutionStarted;
            _timerExecutionService.ExecutionCompleted += OnGlobalExecutionCompleted;
            _timerExecutionService.ExecutionError += OnGlobalExecutionError;
            _timerExecutionService.StateChanged += OnGlobalTimerStateChanged;
        }

        private void UnsubscribeFromTimerEvents()
        {
            // Unsubscribe from global timer events
            _timerExecutionService.ExecutionStarted -= OnGlobalExecutionStarted;
            _timerExecutionService.ExecutionCompleted -= OnGlobalExecutionCompleted;
            _timerExecutionService.ExecutionError -= OnGlobalExecutionError;
            _timerExecutionService.StateChanged -= OnGlobalTimerStateChanged;
        }

        private async Task<ITimerExecutionService> GetOrCreateVersionTimerAsync(string version, TimeSpan interval)
        {
            if (_versionTimers.TryGetValue(version, out var existingTimer))
            {
                // Update interval if needed
                if (existingTimer.CurrentInterval != interval)
                {
                    existingTimer.UpdateInterval(interval);
                }
                return existingTimer;
            }

            // Create new timer for this version
            var timerOptions = CreateTimerOptionsForVersion(version, interval);
            var timerService = CreateTimerService(timerOptions);

            // Start the timer
            await timerService.StartAsync(interval, _globalCancellationTokenSource.Token);

            _versionTimers[version] = timerService;
            _logger.LogDebug("Created new timer for version {Version} with interval {Interval}ms",
                version, interval.TotalMilliseconds);

            return timerService;
        }

        private async Task<ITimerExecutionService> CreateOneTimeVersionTimerAsync(string version, TimeSpan delay, string operationId)
        {
            var timerOptions = CreateTimerOptionsForVersion(version, delay);
            var timerService = CreateTimerService(timerOptions);

            // Start one-time timer
            await timerService.StartOneTimeAsync(delay, _globalCancellationTokenSource.Token);

            // Remove timer after execution (one-time)
            timerService.ExecutionCompleted += (s, e) =>
            {
                _versionTimers.Remove(version);
                timerService.Dispose();
            };

            _versionTimers[version] = timerService;
            _logger.LogDebug("Created one-time timer for version {Version} with delay {Delay}ms (OperationId: {OperationId})",
                version, delay.TotalMilliseconds, operationId);

            return timerService;
        }

        private TimerExecutionOptions CreateTimerOptionsForVersion(string version, TimeSpan interval)
        {
            return new TimerExecutionOptions
            {
                DefaultInterval = interval,
                MinInterval = _options.MinVersionInterval,
                MaxInterval = _options.MaxVersionInterval,
                MaxConcurrentExecutions = _options.MaxConcurrentOperationsPerVersion,
                EnableCircuitBreaker = _options.EnableCircuitBreaker,
                MaxConsecutiveErrors = _options.MaxConsecutiveErrors,
                CircuitBreakerCooldown = _options.CircuitBreakerCooldown,
                EnableAutoRestart = _options.EnableAutoRestart,
                PreventExecutionOverlap = _options.PreventExecutionOverlap,
                StopOnUnhandledException = _options.StopOnUnhandledException,
                EnableDetailedLogging = _options.EnableDetailedLogging,
                MaxExecutionHistory = _options.MaxExecutionHistory,
                DisposalGracePeriod = _options.DisposalGracePeriod,
                EnablePerformanceOptimization = _options.EnableTimerPerformanceOptimization
            };
        }

        private ITimerExecutionService CreateTimerService(TimerExecutionOptions options)
        {
            // This would normally be resolved from DI container
            // For now, create a new instance
            // Cast the logger to the expected type or create a null logger as fallback
            var timerLogger = _logger as ILogger<TimerExecutionService>;
            if (timerLogger == null)
            {
                // Create a minimal logger wrapper or use a different approach
                timerLogger = NullLogger<TimerExecutionService>.Instance;
            }
            return new TimerExecutionService(timerLogger, options, _options.EnableTimerPerformanceOptimization);
        }

        private async Task StartMaintenanceTimerAsync()
        {
            var maintenanceInterval = TimeSpan.FromMinutes(_options.MaintenanceIntervalMinutes);
            await _timerExecutionService.StartAsync(maintenanceInterval, _globalCancellationTokenSource.Token);
            _logger.LogDebug("Started maintenance timer with interval {Interval}ms", maintenanceInterval.TotalMilliseconds);
        }

        private async Task StopAllVersionTimersAsync()
        {
            var stopTasks = _versionTimers.Values.Select(timer => timer.StopAsync()).ToList();
            await Task.WhenAll(stopTasks);

            // Dispose all timers
            foreach (var timer in _versionTimers.Values)
            {
                timer.Dispose();
            }

            _versionTimers.Clear();
            _logger.LogDebug("Stopped all version timers");
        }

        private void StartQueueProcessor()
        {
            if (_isQueueProcessing)
                return;

            _isQueueProcessing = true;
            _queueProcessorTask = Task.Run(ProcessOperationQueueAsync, _globalCancellationTokenSource.Token);
            _logger.LogDebug("Started operation queue processor");
        }

        private void StopQueueProcessor()
        {
            _isQueueProcessing = false;
            _globalCancellationTokenSource.Cancel();

            if (_queueProcessorTask != null && !_queueProcessorTask.IsCompleted)
            {
                try
                {
                    _queueProcessorTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                    // Ignore cancellation exceptions
                }
            }

            _logger.LogDebug("Stopped operation queue processor");
        }

        private async Task ProcessOperationQueueAsync()
        {
            while (_isQueueProcessing && !_globalCancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    VersionOperationRequest operationRequest = null;

                    lock (_lock)
                    {
                        if (_operationQueue.Count > 0)
                        {
                            operationRequest = _operationQueue.Dequeue();
                        }
                    }

                    if (operationRequest != null)
                    {
                        await ProcessOperationRequestAsync(operationRequest);
                    }
                    else
                    {
                        // No operations to process, wait a bit
                        await Task.Delay(100, _globalCancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when shutting down
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing operation queue");
                    await Task.Delay(1000, _globalCancellationTokenSource.Token); // Wait before retrying
                }
            }

            _isQueueProcessing = false;
        }

        private async Task ProcessOperationRequestAsync(VersionOperationRequest operationRequest)
        {
            var operationId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try
            {
                // Raise operation started event
                var startedEventArgs = new VersionOperationEventArgs(
                    operationId,
                    operationRequest.OperationType,
                    operationRequest.Version,
                    startTime);
                OperationStarted?.Invoke(this, startedEventArgs);

                _logger.LogDebug("Processing operation {OperationId} of type {OperationType} for version {Version}",
                    operationId, operationRequest.OperationType, operationRequest.Version);

                // Execute the operation using runtime manager
                var requirements = CreateOperationRequirements(operationRequest);
                var result = await _runtimeManager.ExecuteOperationAsync(
                    operationRequest.OperationType.ToString(),
                    operationRequest.Version,
                    requirements.ToDictionary(),
                    operationRequest.Parameters,
                    _globalCancellationTokenSource.Token);

                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;

                if (result.Success)
                {
                    _logger.LogDebug("Operation {OperationId} completed successfully in {Duration}ms",
                        operationId, duration.TotalMilliseconds);

                    // Raise operation completed event
                    var completedEventArgs = new VersionOperationEventArgs(
                        operationId,
                        operationRequest.OperationType,
                        operationRequest.Version,
                        startTime,
                        endTime,
                        duration,
                        true);
                    OperationCompleted?.Invoke(this, completedEventArgs);
                }
                else
                {
                    _logger.LogWarning("Operation {OperationId} failed: {ErrorMessage}", operationId, result.ErrorMessage);

                    // Raise operation error event
                    var errorEventArgs = new VersionOperationErrorEventArgs(
                        operationId,
                        operationRequest.OperationType,
                        operationRequest.Version,
                        startTime,
                        endTime,
                        duration,
                        result.ErrorMessage);
                    OperationError?.Invoke(this, errorEventArgs);
                }
            }
            catch (Exception ex)
            {
                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;

                _logger.LogError(ex, "Operation {OperationId} failed with exception", operationId);

                // Raise operation error event
                var errorEventArgs = new VersionOperationErrorEventArgs(
                    operationId,
                    operationRequest.OperationType,
                    operationRequest.Version,
                    startTime,
                    endTime,
                    duration,
                    ex.Message,
                    ex);
                OperationError?.Invoke(this, errorEventArgs);
            }
        }

        private VersionOperationRequirements CreateOperationRequirements(VersionOperationRequest operationRequest)
        {
            return new VersionOperationRequirements
            {
                OperationType = operationRequest.OperationType,
                Priority = AllocationPriority.Normal,
                Timeout = TimeSpan.FromMinutes(_options.DefaultOperationTimeoutMinutes),
                ExpectedDuration = TimeSpan.FromSeconds(_options.ExpectedOperationDurationSeconds),
                MaxConcurrentOperations = 1
            };
        }

        private void ChangeStatus(VersionSchedulerStatus newStatus, string reason)
        {
            if (Status == newStatus)
                return;

            var previousStatus = Status;
            Status = newStatus;

            try
            {
                StatusChanged?.Invoke(this, new VersionSchedulerStatusChangedEventArgs(previousStatus, newStatus, reason));

                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug("Scheduler status changed from {PreviousStatus} to {NewStatus}: {Reason}",
                        previousStatus, newStatus, reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in status change event handler");
            }
        }

        private string GenerateOperationId()
        {
            return $"op_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        }

        private Dictionary<string, int> GetOperationHistorySummary()
        {
            // This would return a summary of operation history by type and version
            // For now, return empty placeholder
            return new Dictionary<string, int>();
        }

        #region Timer Event Handlers

        private void OnGlobalExecutionStarted(object sender, TimerExecutionEventArgs e)
        {
            _logger.LogDebug("Global timer execution started: {ExecutionId}", e.ExecutionId);
        }

        private void OnGlobalExecutionCompleted(object sender, TimerExecutionEventArgs e)
        {
            _logger.LogDebug("Global timer execution completed: {ExecutionId}, Success: {Success}", e.ExecutionId, e.Success);
        }

        private void OnGlobalExecutionError(object sender, TimerExecutionErrorEventArgs e)
        {
            _logger.LogError("Global timer execution error: {ExecutionId}, Error: {ErrorMessage}", e.ExecutionId, e.ErrorMessage);
        }

        private void OnGlobalTimerStateChanged(object sender, TimerStateChangedEventArgs e)
        {
            _logger.LogDebug("Global timer state changed from {PreviousState} to {NewState}: {Reason}",
                e.PreviousState, e.NewState, e.Reason);
        }

        #endregion

        private void ValidateOptions(VersionSchedulerOptions options)
        {
            if (options.MaxConcurrentSchedules <= 0)
                throw new ArgumentException("MaxConcurrentSchedules must be greater than 0", nameof(options));

            if (options.MinVersionInterval <= TimeSpan.Zero)
                throw new ArgumentException("MinVersionInterval must be greater than zero", nameof(options));

            if (options.MaxVersionInterval <= TimeSpan.Zero)
                throw new ArgumentException("MaxVersionInterval must be greater than zero", nameof(options));

            if (options.MinVersionInterval > options.MaxVersionInterval)
                throw new ArgumentException("MinVersionInterval cannot be greater than MaxVersionInterval", nameof(options));

            if (options.MaintenanceIntervalMinutes <= 0)
                throw new ArgumentException("MaintenanceIntervalMinutes must be greater than 0", nameof(options));

            if (options.DefaultOperationTimeoutMinutes <= 0)
                throw new ArgumentException("DefaultOperationTimeoutMinutes must be greater than 0", nameof(options));

            if (options.ExpectedOperationDurationSeconds <= 0)
                throw new ArgumentException("ExpectedOperationDurationSeconds must be greater than 0", nameof(options));
        }

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the version scheduler
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Stop the scheduler first
                    StopAsync().GetAwaiter().GetResult();

                    // Unsubscribe from events
                    UnsubscribeFromTimerEvents();

                    // Dispose managed resources
                    _globalCancellationTokenSource?.Dispose();
                    _schedulingSemaphore?.Dispose();

                    // Dispose all timers
                    foreach (var timer in _versionTimers.Values)
                    {
                        timer.Dispose();
                    }
                }

                _isDisposed = true;
                ChangeStatus(VersionSchedulerStatus.Disposed, "Scheduler disposed");
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~VersionScheduler()
        {
            Dispose(false);
        }

        #endregion
    }
}