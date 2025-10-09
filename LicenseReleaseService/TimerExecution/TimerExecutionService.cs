using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Implements a robust timer-based execution service with comprehensive error handling, monitoring, and performance optimization
    /// </summary>
    public class TimerExecutionService : ITimerExecutionService, IDisposable
    {
        private readonly ILogger<TimerExecutionService> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly object _lock = new object();
        private System.Timers.Timer _timer;
        private TimerState _state;
        private TimeSpan _currentInterval;
        private bool _isExecuting;
        private int _consecutiveErrors;
        private DateTime _startTime;
        private DateTime _lastExecutionTime;
        private readonly List<TimerExecutionEventArgs> _executionHistory;
        private readonly TimerPerformanceMetrics _metrics;
        private readonly Stopwatch _uptimeStopwatch;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _executionSemaphore;
        private Guid? _currentExecutionId;
        private Task _currentExecutionTask;
        private bool _isDisposed;

        // Performance optimization components
        private TimerPerformanceOptimizer _performanceOptimizer;
        private TimerMemoryManager _memoryManager;
        private TimerThreadPoolManager _threadPoolManager;
        private TimerCacheManager _cacheManager;
        private TimerMetricsCollector _metricsCollector;
        private TimerPerformanceTuner _performanceTuner;
        private bool _performanceOptimizationEnabled;

        #region Events

        /// <inheritdoc/>
        public event EventHandler<TimerExecutionEventArgs> ExecutionStarted;

        /// <inheritdoc/>
        public event EventHandler<TimerExecutionEventArgs> ExecutionCompleted;

        /// <inheritdoc/>
        public event EventHandler<TimerExecutionErrorEventArgs> ExecutionError;

        /// <inheritdoc/>
        public event EventHandler<TimerStateChangedEventArgs> StateChanged;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public TimerState State => _state;

        /// <inheritdoc/>
        public bool IsRunning => _state == TimerState.Running;

        /// <inheritdoc/>
        public bool IsExecuting => _isExecuting;

        /// <inheritdoc/>
        public TimeSpan CurrentInterval => _currentInterval;

        #endregion

        /// <summary>
        /// Initializes a new instance of the TimerExecutionService class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The timer execution options</param>
        public TimerExecutionService(ILogger<TimerExecutionService> logger, TimerExecutionOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            // Validate options
            var validationErrors = _options.Validate();
            if (validationErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid timer execution options: {string.Join(", ", validationErrors)}");
            }

            _state = TimerState.Stopped;
            _currentInterval = _options.DefaultInterval;
            _executionHistory = new List<TimerExecutionEventArgs>();
            _metrics = new TimerPerformanceMetrics();
            _uptimeStopwatch = new Stopwatch();
            _executionSemaphore = new SemaphoreSlim(_options.MaxConcurrentExecutions);

            // Initialize performance optimization components
            InitializePerformanceOptimization();
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionService class with performance optimization
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The timer execution options</param>
        /// <param name="enablePerformanceOptimization">Whether to enable performance optimization</param>
        public TimerExecutionService(ILogger<TimerExecutionService> logger, TimerExecutionOptions options, bool enablePerformanceOptimization)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            // Validate options
            var validationErrors = _options.Validate();
            if (validationErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid timer execution options: {string.Join(", ", validationErrors)}");
            }

            _state = TimerState.Stopped;
            _currentInterval = _options.DefaultInterval;
            _executionHistory = new List<TimerExecutionEventArgs>();
            _metrics = new TimerPerformanceMetrics();
            _uptimeStopwatch = new Stopwatch();
            _executionSemaphore = new SemaphoreSlim(_options.MaxConcurrentExecutions);
            _performanceOptimizationEnabled = enablePerformanceOptimization;

            if (_performanceOptimizationEnabled)
            {
                InitializePerformanceOptimization();
            }
        }

        /// <inheritdoc/>
        public async Task StartAsync(TimeSpan interval, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw TimerExecutionException.TimerDisposed();

            ValidateInterval(interval);

            bool shouldStartPerformanceOptimization = false;

            lock (_lock)
            {
                if (_state != TimerState.Stopped)
                {
                    throw TimerExecutionException.InvalidStateOperation("start", _state, TimerState.Stopped);
                }

                try
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    if (cancellationToken != CancellationToken.None)
                    {
                        cancellationToken.Register(() => _cancellationTokenSource.Cancel());
                    }

                    _currentInterval = interval;
                    _timer = new System.Timers.Timer(interval.TotalMilliseconds);
                    _timer.Elapsed += OnTimerElapsed;
                    _timer.AutoReset = true;
                    _timer.Enabled = true;

                    ChangeState(TimerState.Running, "Timer started");
                    _startTime = DateTime.UtcNow;
                    _uptimeStopwatch.Start();
                    _metrics.StartTime = _startTime;

                    // Check if performance optimization should be started
                    shouldStartPerformanceOptimization = _performanceOptimizationEnabled;

                    _logger.LogInformation("Timer started with interval {Interval}ms", interval.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    ChangeState(TimerState.Error, $"Failed to start timer: {ex.Message}");
                    throw TimerExecutionException.StartFailure(ex, interval);
                }
            }

            // Start performance optimization outside the lock
            if (shouldStartPerformanceOptimization)
            {
                await StartPerformanceOptimizationAsync();
            }
        }

        /// <inheritdoc/>
        public async Task StartOneTimeAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw TimerExecutionException.TimerDisposed();

            ValidateInterval(delay);

            bool shouldStartPerformanceOptimization = false;
            lock (_lock)
            {
                if (_state != TimerState.Stopped)
                {
                    throw TimerExecutionException.InvalidStateOperation("start one-time", _state, TimerState.Stopped);
                }

                try
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    if (cancellationToken != CancellationToken.None)
                    {
                        cancellationToken.Register(() => _cancellationTokenSource.Cancel());
                    }

                    _currentInterval = delay;
                    _timer = new System.Timers.Timer(delay.TotalMilliseconds);
                    _timer.Elapsed += OnTimerElapsed;
                    _timer.AutoReset = false; // One-time execution
                    _timer.Enabled = true;

                    ChangeState(TimerState.Running, "One-time timer started");
                    _startTime = DateTime.UtcNow;
                    _uptimeStopwatch.Start();
                    _metrics.StartTime = _startTime;

                    // Check if we should start performance optimization
                    shouldStartPerformanceOptimization = _performanceOptimizationEnabled;

                    _logger.LogInformation("One-time timer started with delay {Delay}ms", delay.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    ChangeState(TimerState.Error, $"Failed to start one-time timer: {ex.Message}");
                    throw TimerExecutionException.StartFailure(ex, delay);
                }
            }

            // Start performance optimization outside the lock
            if (shouldStartPerformanceOptimization)
            {
                await StartPerformanceOptimizationAsync();
            }
        }

        /// <inheritdoc/>
        public async Task StopAsync()
        {
            if (_isDisposed)
                return;

            bool shouldStopPerformanceOptimization = false;
            lock (_lock)
            {
                if (_state == TimerState.Stopped || _state == TimerState.Disposed)
                    return;

                var previousState = _state;
                ChangeState(TimerState.Stopping, "Timer stopping");

                try
                {
                    // Cancel any current execution
                    _cancellationTokenSource?.Cancel();

                    // Wait for graceful shutdown
                    if (_currentExecutionTask != null && !_currentExecutionTask.IsCompleted)
                    {
                        try
                        {
                            var timeoutTask = Task.Delay(_options.DisposalGracePeriod);
                            var completedTask = Task.WhenAny(_currentExecutionTask, timeoutTask).Result;

                            if (completedTask == timeoutTask)
                            {
                                _logger.LogWarning("Timer execution did not complete gracefully within grace period");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error while waiting for graceful shutdown");
                        }
                    }

                    // Stop and dispose timer
                    if (_timer != null)
                    {
                        _timer.Stop();
                        _timer.Elapsed -= OnTimerElapsed;
                        _timer.Dispose();
                        _timer = null;
                    }

                    // Dispose cancellation token
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;

                    // Update metrics
                    _uptimeStopwatch.Stop();
                    _metrics.Uptime = _uptimeStopwatch.Elapsed;
                    _metrics.StopTime = DateTime.UtcNow;

                    // Check if performance optimization should be stopped
                    shouldStopPerformanceOptimization = _performanceOptimizationEnabled;

                    ChangeState(TimerState.Stopped, "Timer stopped");
                    _logger.LogInformation("Timer stopped successfully");
                }
                catch (Exception ex)
                {
                    ChangeState(TimerState.Error, $"Failed to stop timer: {ex.Message}");
                    throw TimerExecutionException.StopFailure(ex);
                }
            }

            // Stop performance optimization outside the lock
            if (shouldStopPerformanceOptimization)
            {
                await StopPerformanceOptimizationAsync();
            }
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (_isDisposed)
                throw TimerExecutionException.TimerDisposed();

            lock (_lock)
            {
                if (_state != TimerState.Running)
                {
                    throw TimerExecutionException.InvalidStateOperation("pause", _state, TimerState.Running);
                }

                try
                {
                    _timer?.Stop();
                    ChangeState(TimerState.Paused, "Timer paused");
                    _logger.LogInformation("Timer paused");
                }
                catch (Exception ex)
                {
                    ChangeState(TimerState.Error, $"Failed to pause timer: {ex.Message}");
                    throw new TimerExecutionException($"Failed to pause timer: {ex.Message}", _state, null, 1, false, _currentInterval, ex);
                }
            }
        }

        /// <inheritdoc/>
        public void Resume()
        {
            if (_isDisposed)
                throw TimerExecutionException.TimerDisposed();

            lock (_lock)
            {
                if (_state != TimerState.Paused)
                {
                    throw TimerExecutionException.InvalidStateOperation("resume", _state, TimerState.Paused);
                }

                try
                {
                    _timer?.Start();
                    ChangeState(TimerState.Running, "Timer resumed");
                    _logger.LogInformation("Timer resumed");
                }
                catch (Exception ex)
                {
                    ChangeState(TimerState.Error, $"Failed to resume timer: {ex.Message}");
                    throw new TimerExecutionException($"Failed to resume timer: {ex.Message}", _state, null, 1, false, _currentInterval, ex);
                }
            }
        }

        /// <inheritdoc/>
        public void UpdateInterval(TimeSpan newInterval)
        {
            if (_isDisposed)
                throw TimerExecutionException.TimerDisposed();

            ValidateInterval(newInterval);

            lock (_lock)
            {
                if (_state != TimerState.Running && _state != TimerState.Paused)
                {
                    throw TimerExecutionException.InvalidStateOperation("update interval", _state, TimerState.Running);
                }

                try
                {
                    var oldInterval = _currentInterval;
                    _currentInterval = newInterval;

                    if (_timer != null)
                    {
                        var wasRunning = _timer.Enabled;
                        _timer.Stop();
                        _timer.Interval = newInterval.TotalMilliseconds;
                        if (wasRunning || _state == TimerState.Running)
                        {
                            _timer.Start();
                        }
                    }

                    _logger.LogInformation("Timer interval updated from {OldInterval}ms to {NewInterval}ms",
                        oldInterval.TotalMilliseconds, newInterval.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    ChangeState(TimerState.Error, $"Failed to update timer interval: {ex.Message}");
                    throw new TimerExecutionException($"Failed to update timer interval: {ex.Message}", _state, null, 1, false, _currentInterval, ex);
                }
            }
        }

        /// <inheritdoc/>
        public async Task ExecuteNowAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw TimerExecutionException.TimerDisposed();

            await ExecuteOperationAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public TimerPerformanceMetrics GetMetrics()
        {
            lock (_lock)
            {
                var metrics = _metrics.Clone();
                metrics.Uptime = _uptimeStopwatch.Elapsed;
                metrics.CurrentConsecutiveErrors = _consecutiveErrors;
                return metrics;
            }
        }

        /// <inheritdoc/>
        public void ResetStatistics()
        {
            lock (_lock)
            {
                _consecutiveErrors = 0;
                _metrics.CurrentConsecutiveErrors = 0;
                _metrics.TotalExecutions = 0;
                _metrics.SuccessfulExecutions = 0;
                _metrics.FailedExecutions = 0;
                _metrics.AverageExecutionDuration = TimeSpan.Zero;
                _metrics.MinExecutionDuration = TimeSpan.Zero;
                _metrics.MaxExecutionDuration = TimeSpan.Zero;
                _metrics.MaxConsecutiveErrors = 0;
                _metrics.LastExecutionTime = null;

                _executionHistory.Clear();

                _logger.LogInformation("Timer statistics reset");
            }
        }

        #region Performance Optimization Methods

        /// <summary>
        /// Initializes performance optimization components
        /// </summary>
        private void InitializePerformanceOptimization()
        {
            try
            {
                var memoryOptions = TimerMemoryOptions.ServerDefaults();
                var threadPoolOptions = TimerThreadPoolOptions.ServerDefaults();
                var cacheOptions = TimerCacheOptions.ServerDefaults();
                var metricsOptions = TimerMetricsOptions.ServerDefaults();
                var tunerOptions = TimerPerformanceTunerOptions.ServerDefaults();

                _memoryManager = new TimerMemoryManager(new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<TimerMemoryManager>(), _options, memoryOptions);
                _threadPoolManager = new TimerThreadPoolManager(new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<TimerThreadPoolManager>(), _options, threadPoolOptions);
                _cacheManager = new TimerCacheManager(new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<TimerCacheManager>(), _options, cacheOptions);
                _metricsCollector = new TimerMetricsCollector(new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<TimerMetricsCollector>(), metricsOptions);
                _performanceOptimizer = new TimerPerformanceOptimizer(new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<TimerPerformanceOptimizer>(), _options, _memoryManager, _threadPoolManager, _cacheManager);
                _performanceTuner = new TimerPerformanceTuner(new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<TimerPerformanceTuner>(), tunerOptions, _metricsCollector, _performanceOptimizer);

                // Subscribe to performance optimization events
                SubscribeToPerformanceEvents();

                _performanceOptimizationEnabled = true;
                _logger.LogInformation("Performance optimization components initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize performance optimization components");
                _performanceOptimizationEnabled = false;
            }
        }

        /// <summary>
        /// Subscribes to performance optimization events
        /// </summary>
        private void SubscribeToPerformanceEvents()
        {
            if (!_performanceOptimizationEnabled)
                return;

            _performanceOptimizer.OptimizationStarted += OnPerformanceOptimizationStarted;
            _performanceOptimizer.OptimizationCompleted += OnPerformanceOptimizationCompleted;
            _performanceTuner.TuningApplied += OnPerformanceTuningApplied;
            _metricsCollector.ThresholdExceeded += OnPerformanceThresholdExceeded;
        }

        /// <summary>
        /// Starts performance optimization components
        /// </summary>
        private async Task StartPerformanceOptimizationAsync()
        {
            if (!_performanceOptimizationEnabled)
                return;

            try
            {
                await _memoryManager.StartAsync();
                await _threadPoolManager.StartAsync();
                await _cacheManager.StartAsync();
                await _metricsCollector.StartAsync();
                await _performanceOptimizer.StartAsync();
                await _performanceTuner.StartAsync();

                _logger.LogInformation("Performance optimization components started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start performance optimization components");
            }
        }

        /// <summary>
        /// Stops performance optimization components
        /// </summary>
        private async Task StopPerformanceOptimizationAsync()
        {
            if (!_performanceOptimizationEnabled)
                return;

            try
            {
                await _performanceTuner.StopAsync();
                await _performanceOptimizer.StopAsync();
                await _metricsCollector.StopAsync();
                await _cacheManager.StopAsync();
                await _threadPoolManager.StopAsync();
                await _memoryManager.StopAsync();

                _logger.LogInformation("Performance optimization components stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop performance optimization components");
            }
        }

        /// <summary>
        /// Handles performance optimization started events
        /// </summary>
        private void OnPerformanceOptimizationStarted(object sender, EventArgs e)
        {
            _logger.LogDebug("Performance optimization cycle started");
        }

        /// <summary>
        /// Handles performance optimization completed events
        /// </summary>
        private void OnPerformanceOptimizationCompleted(object sender, EventArgs e)
        {
            _logger.LogDebug("Performance optimization cycle completed");
        }

        /// <summary>
        /// Handles performance tuning applied events
        /// </summary>
        private void OnPerformanceTuningApplied(object sender, TimerTuningAppliedEventArgs e)
        {
            _logger.LogInformation("Performance tuning applied: {Action} - {Reason}", e.TuningAction.Action, e.TuningAction.Reason);
        }

        /// <summary>
        /// Handles performance threshold exceeded events
        /// </summary>
        private void OnPerformanceThresholdExceeded(object sender, TimerPerformanceThresholdExceededEventArgs e)
        {
            _logger.LogWarning("Performance threshold exceeded: {Thresholds}",
                string.Join(", ", e.ExceededThresholds.Select(t => t.ToString())));
        }

        /// <summary>
        /// Gets performance optimization status
        /// </summary>
        /// <returns>Performance optimization status</returns>
        public TimerPerformanceOptimizationStatus GetPerformanceOptimizationStatus()
        {
            if (!_performanceOptimizationEnabled)
            {
                return new TimerPerformanceOptimizationStatus
                {
                    IsEnabled = false,
                    Status = "Disabled"
                };
            }

            return new TimerPerformanceOptimizationStatus
            {
                IsEnabled = true,
                Status = "Active",
                MemoryManagerStatus = _memoryManager?.IsRunning == true ? "Running" : "Stopped",
                ThreadPoolManagerStatus = _threadPoolManager?.IsRunning == true ? "Running" : "Stopped",
                CacheManagerStatus = _cacheManager?.IsRunning == true ? "Running" : "Stopped",
                MetricsCollectorStatus = "Running", // Always running once initialized
                PerformanceOptimizerStatus = _performanceOptimizer?.IsRunning == true ? "Running" : "Stopped",
                PerformanceTunerStatus = "Running" // Always running once started
            };
        }

        /// <summary>
        /// Gets comprehensive performance metrics
        /// </summary>
        /// <returns>Comprehensive performance metrics</returns>
        public async Task<TimerComprehensivePerformanceMetrics> GetComprehensivePerformanceMetricsAsync()
        {
            var metrics = new TimerComprehensivePerformanceMetrics
            {
                TimerMetrics = GetMetrics(),
                Timestamp = DateTime.UtcNow
            };

            if (_performanceOptimizationEnabled)
            {
                try
                {
                    metrics.SystemMetrics = _metricsCollector?.GetSystemMetrics();
                    metrics.OptimizationStatus = _performanceOptimizer?.GetOptimizationStatus();
                    metrics.TuningStatistics = _performanceTuner?.TuningStatistics;
                    metrics.MemoryStatistics = await _memoryManager?.GetMemoryStatisticsAsync();
                    metrics.ThreadPoolMetrics = await _threadPoolManager?.GetThreadPoolMetricsAsync();
                    metrics.CacheMetrics = await _cacheManager?.GetCacheMetricsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to collect comprehensive performance metrics");
                }
            }

            return metrics;
        }

        #endregion

        #region Backward Compatibility Methods

        /// <inheritdoc/>
        public void Start(TimeSpan interval, CancellationToken cancellationToken = default)
        {
            StartAsync(interval, cancellationToken).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public void StartOneTime(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            StartOneTimeAsync(delay, cancellationToken).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            StopAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region Private Methods

        private async void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isDisposed || _cancellationTokenSource?.IsCancellationRequested == true)
                return;

            if (_options.PreventExecutionOverlap && _isExecuting)
            {
                _logger.LogWarning("Timer execution already in progress, skipping due to overlap prevention");
                return;
            }

            await ExecuteOperationAsync(_cancellationTokenSource.Token);
        }

        private async Task ExecuteOperationAsync(CancellationToken cancellationToken)
        {
            if (!await _executionSemaphore.WaitAsync(0, cancellationToken))
            {
                if (_options.MaxConcurrentExecutions > 1)
                {
                    _logger.LogWarning("Maximum concurrent executions reached, waiting");
                    await _executionSemaphore.WaitAsync(cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Timer execution already in progress, skipping");
                    return;
                }
            }

            var executionId = Guid.NewGuid();
            _currentExecutionId = executionId;
            _isExecuting = true;

            try
            {
                ChangeState(TimerState.Executing, $"Starting execution {executionId}");

                var startTime = DateTime.UtcNow;
                var executionArgs = new TimerExecutionEventArgs(executionId, startTime, 1);

                ExecutionStarted?.Invoke(this, executionArgs);

                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug("Starting timer execution {ExecutionId}", executionId);
                }

                var stopwatch = Stopwatch.StartNew();

                try
                {
                    // Execute the actual operation
                    await ExecuteCoreOperationAsync(executionArgs, cancellationToken);

                    stopwatch.Stop();
                    executionArgs.Success = true;
                    executionArgs.EndTime = DateTime.UtcNow;
                    executionArgs.Duration = stopwatch.Elapsed;
                    executionArgs.OperationsProcessed = GetOperationsProcessedCount(executionArgs);

                    // Update metrics
                    UpdateExecutionMetrics(executionArgs, true);

                    // Clear consecutive errors on success
                    _consecutiveErrors = 0;

                    if (_options.EnableDetailedLogging)
                    {
                        _logger.LogDebug("Timer execution {ExecutionId} completed successfully in {Duration}ms",
                            executionId, executionArgs.Duration.TotalMilliseconds);
                    }

                    ExecutionCompleted?.Invoke(this, executionArgs);
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();
                    executionArgs.Success = false;
                    executionArgs.EndTime = DateTime.UtcNow;
                    executionArgs.Duration = stopwatch.Elapsed;
                    executionArgs.ErrorMessage = "Execution was cancelled";

                    _logger.LogInformation("Timer execution {ExecutionId} was cancelled", executionId);
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    executionArgs.Success = false;
                    executionArgs.EndTime = DateTime.UtcNow;
                    executionArgs.Duration = stopwatch.Elapsed;
                    executionArgs.ErrorMessage = ex.Message;

                    await HandleExecutionErrorAsync(ex, executionId);
                    throw;
                }
            }
            finally
            {
                _isExecuting = false;
                _currentExecutionId = null;

                // Restore previous state if we're still running
                if (_state == TimerState.Executing && !_cancellationTokenSource?.IsCancellationRequested == true)
                {
                    ChangeState(TimerState.Running, "Execution completed");
                }

                _executionSemaphore.Release();
            }
        }

        private async Task ExecuteCoreOperationAsync(TimerExecutionEventArgs executionArgs, CancellationToken cancellationToken)
        {
            // This is a placeholder for the actual timer operation
            // In a real implementation, this would coordinate with the license management system

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Executing core timer operation for {ExecutionId}", executionArgs.ExecutionId);
            }

            // Simulate some work - in real implementation, this would be:
            // - Query license status
            // - Process license information
            // - Release idle licenses if needed
            await Task.Delay(100, cancellationToken);

            // Add some metadata for demonstration
            executionArgs.Metadata["OperationType"] = "LicenseCheck";
            executionArgs.Metadata["Server"] = "localhost";
            executionArgs.Metadata["Port"] = 1057;

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Core timer operation completed for {ExecutionId}", executionArgs.ExecutionId);
            }
        }

        private int GetOperationsProcessedCount(TimerExecutionEventArgs executionArgs)
        {
            // This would return the actual count of operations processed
            // For now, return a placeholder value
            return 1;
        }

        private async Task HandleExecutionErrorAsync(Exception ex, Guid executionId)
        {
            _consecutiveErrors++;

            var shouldTriggerCircuitBreaker = _options.EnableCircuitBreaker &&
                                             _consecutiveErrors >= _options.MaxConsecutiveErrors;

            var errorArgs = new TimerExecutionErrorEventArgs(
                ex,
                _consecutiveErrors,
                _state,
                executionId)
            {
                ShouldTriggerCircuitBreaker = shouldTriggerCircuitBreaker
            };

            ExecutionError?.Invoke(this, errorArgs);

            // Update metrics
            _metrics.FailedExecutions++;
            _metrics.CurrentConsecutiveErrors = _consecutiveErrors;
            if (_consecutiveErrors > _metrics.MaxConsecutiveErrors)
            {
                _metrics.MaxConsecutiveErrors = _consecutiveErrors;
            }

            _logger.LogError(ex, "Timer execution {ExecutionId} failed (consecutive errors: {Count})",
                executionId, _consecutiveErrors);

            if (shouldTriggerCircuitBreaker)
            {
                await HandleCircuitBreakerAsync(executionId);
            }
            else if (_options.StopOnUnhandledException)
            {
                Stop();
            }
        }

        private async Task HandleCircuitBreakerAsync(Guid executionId)
        {
            _logger.LogWarning("Circuit breaker triggered for execution {ExecutionId} after {Count} consecutive errors",
                executionId, _consecutiveErrors);

            ChangeState(TimerState.CircuitBreaker, $"Circuit breaker triggered after {_consecutiveErrors} errors");

            try
            {
                // Stop the timer
                Stop();

                if (_options.EnableAutoRestart)
                {
                    _logger.LogInformation("Waiting for circuit breaker cooldown period of {Cooldown}",
                        _options.CircuitBreakerCooldown);

                    await Task.Delay(_options.CircuitBreakerCooldown);

                    // Reset error count and attempt to restart
                    _consecutiveErrors = 0;
                    _metrics.CurrentConsecutiveErrors = 0;

                    try
                    {
                        Start(_currentInterval);
                        _logger.LogInformation("Timer restarted after circuit breaker cooldown");
                    }
                    catch (Exception restartEx)
                    {
                        _logger.LogError(restartEx, "Failed to restart timer after circuit breaker cooldown");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during circuit breaker handling for execution {ExecutionId}", executionId);
            }
        }

        private void UpdateExecutionMetrics(TimerExecutionEventArgs executionArgs, bool success)
        {
            lock (_lock)
            {
                _metrics.TotalExecutions++;
                _metrics.LastExecutionTime = executionArgs.EndTime;

                if (success)
                {
                    _metrics.SuccessfulExecutions++;
                }

                // Update duration metrics
                if (_metrics.TotalExecutions == 1)
                {
                    _metrics.AverageExecutionDuration = executionArgs.Duration;
                    _metrics.MinExecutionDuration = executionArgs.Duration;
                    _metrics.MaxExecutionDuration = executionArgs.Duration;
                }
                else
                {
                    var totalDuration = _metrics.AverageExecutionDuration * (_metrics.TotalExecutions - 1) + executionArgs.Duration;
                    _metrics.AverageExecutionDuration = TimeSpan.FromTicks(totalDuration.Ticks / _metrics.TotalExecutions);

                    if (executionArgs.Duration < _metrics.MinExecutionDuration)
                    {
                        _metrics.MinExecutionDuration = executionArgs.Duration;
                    }

                    if (executionArgs.Duration > _metrics.MaxExecutionDuration)
                    {
                        _metrics.MaxExecutionDuration = executionArgs.Duration;
                    }
                }

                // Add to execution history
                if (_options.MaxExecutionHistory > 0)
                {
                    _executionHistory.Add(executionArgs);
                    if (_executionHistory.Count > _options.MaxExecutionHistory)
                    {
                        _executionHistory.RemoveAt(0);
                    }
                }
            }
        }

        private void ChangeState(TimerState newState, string reason)
        {
            if (_state == newState)
                return;

            var previousState = _state;
            _state = newState;

            try
            {
                StateChanged?.Invoke(this, new TimerStateChangedEventArgs(previousState, newState, reason));

                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug("Timer state changed from {PreviousState} to {NewState}: {Reason}",
                        previousState, newState, reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in state change event handler");
            }
        }

        private void ValidateInterval(TimeSpan interval)
        {
            if (interval < _options.MinInterval)
            {
                throw TimerExecutionException.InvalidInterval(_options.MinInterval);
            }

            if (interval > _options.MaxInterval)
            {
                throw new ArgumentException($"Interval cannot exceed maximum interval of {_options.MaxInterval}", nameof(interval));
            }
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
        /// Disposes the timer execution service
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Stop the timer first
                    Stop();

                    // Stop performance optimization components
                    if (_performanceOptimizationEnabled)
                    {
                        try
                        {
                            StopPerformanceOptimizationAsync().GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to stop performance optimization components during disposal");
                        }
                    }

                    // Dispose managed resources
                    _timer?.Dispose();
                    _cancellationTokenSource?.Dispose();
                    _executionSemaphore?.Dispose();
                    _uptimeStopwatch?.Stop();

                    // Dispose performance optimization components
                    _memoryManager?.Dispose();
                    _threadPoolManager?.Dispose();
                    _cacheManager?.Dispose();
                    _metricsCollector?.Dispose();
                    _performanceOptimizer?.Dispose();
                    _performanceTuner?.Dispose();
                }

                _isDisposed = true;
                ChangeState(TimerState.Disposed, "Timer disposed");
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerExecutionService()
        {
            Dispose(false);
        }

        #endregion
    }
}