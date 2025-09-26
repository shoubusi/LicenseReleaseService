using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Central coordinator for timer performance optimization
    /// </summary>
    public class TimerPerformanceOptimizer : IDisposable
    {
        private readonly ILogger<TimerPerformanceOptimizer> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly TimerMemoryManager _memoryManager;
        private readonly TimerThreadPoolManager _threadPoolManager;
        private readonly TimerCacheManager _cacheManager;
        private readonly TimerMetricsCollector _metricsCollector;
        private readonly TimerPerformanceTuner _performanceTuner;

        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Stopwatch _uptimeStopwatch;
        private Task _optimizationTask;
        private bool _isDisposed;
        private bool _isRunning;

        /// <summary>
        /// Gets whether the optimizer is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the current optimization metrics
        /// </summary>
        public TimerOptimizationMetrics Metrics { get; private set; }

        /// <summary>
        /// Event raised when optimization cycle starts
        /// </summary>
        public event EventHandler<TimerOptimizationEventArgs> OptimizationStarted;

        /// <summary>
        /// Event raised when optimization cycle completes
        /// </summary>
        public event EventHandler<TimerOptimizationEventArgs> OptimizationCompleted;

        /// <summary>
        /// Event raised when performance tuning is applied
        /// </summary>
        public event EventHandler<TimerPerformanceTuningEventArgs> PerformanceTuningApplied;

        /// <summary>
        /// Initializes a new instance of the TimerPerformanceOptimizer class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Timer execution options</param>
        /// <param name="memoryManager">Memory manager instance</param>
        /// <param name="threadPoolManager">Thread pool manager instance</param>
        /// <param name="cacheManager">Cache manager instance</param>
        /// <param name="metricsCollector">Metrics collector instance</param>
        /// <param name="performanceTuner">Performance tuner instance</param>
        public TimerPerformanceOptimizer(
            ILogger<TimerPerformanceOptimizer> logger,
            TimerExecutionOptions options,
            TimerMemoryManager memoryManager,
            TimerThreadPoolManager threadPoolManager,
            TimerCacheManager cacheManager,
            TimerMetricsCollector metricsCollector,
            TimerPerformanceTuner performanceTuner)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _threadPoolManager = threadPoolManager ?? throw new ArgumentNullException(nameof(threadPoolManager));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _performanceTuner = performanceTuner ?? throw new ArgumentNullException(nameof(performanceTuner));

            _cancellationTokenSource = new CancellationTokenSource();
            _uptimeStopwatch = new Stopwatch();
            Metrics = new TimerOptimizationMetrics();

            // Subscribe to component events
            _memoryManager.MemoryPressureDetected += OnMemoryPressureDetected;
            _threadPoolManager.ThreadPoolAdjusted += OnThreadPoolAdjusted;
            _cacheManager.CacheOptimized += OnCacheOptimized;
            _metricsCollector.MetricsCollected += OnMetricsCollected;
            _performanceTuner.TuningApplied += OnTuningApplied;
        }

        /// <summary>
        /// Starts the performance optimizer
        /// </summary>
        /// <returns>Task representing the start operation</returns>
        public async Task StartAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TimerPerformanceOptimizer));

            if (_isRunning)
            {
                _logger.LogWarning("Performance optimizer is already running");
                return;
            }

            lock (_lock)
            {
                if (_isRunning)
                    return;

                _isRunning = true;
                _uptimeStopwatch.Start();
                Metrics.StartTime = DateTime.UtcNow;
            }

            try
            {
                // Start all components
                await _memoryManager.StartAsync(_cancellationTokenSource.Token);
                await _threadPoolManager.StartAsync(_cancellationTokenSource.Token);
                await _cacheManager.StartAsync(_cancellationTokenSource.Token);
                await _metricsCollector.StartAsync(_cancellationTokenSource.Token);
                await _performanceTuner.StartAsync(_cancellationTokenSource.Token);

                // Start the optimization loop
                _optimizationTask = Task.Run(() => OptimizationLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                _logger.LogInformation("Timer performance optimizer started");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.LogError(ex, "Failed to start performance optimizer");
                throw;
            }
        }

        /// <summary>
        /// Stops the performance optimizer gracefully
        /// </summary>
        /// <returns>Task representing the stop operation</returns>
        public async Task StopAsync()
        {
            if (!_isRunning || _isDisposed)
                return;

            lock (_lock)
            {
                if (!_isRunning)
                    return;

                _isRunning = false;
            }

            try
            {
                // Cancel optimization task
                _cancellationTokenSource.Cancel();

                // Wait for optimization task to complete
                if (_optimizationTask != null && !_optimizationTask.IsCompleted)
                {
                    try
                    {
                        await Task.WhenAny(_optimizationTask, Task.Delay(TimeSpan.FromSeconds(5)));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error while waiting for optimization task to stop");
                    }
                }

                // Stop all components
                await _performanceTuner.StopAsync();
                await _metricsCollector.StopAsync();
                await _cacheManager.StopAsync();
                await _threadPoolManager.StopAsync();
                await _memoryManager.StopAsync();

                _uptimeStopwatch.Stop();
                Metrics.Uptime = _uptimeStopwatch.Elapsed;
                Metrics.StopTime = DateTime.UtcNow;

                _logger.LogInformation("Timer performance optimizer stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping performance optimizer");
            }
        }

        /// <summary>
        /// Triggers an immediate optimization cycle
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the optimization operation</returns>
        public async Task TriggerOptimizationAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
            {
                _logger.LogWarning("Performance optimizer is not running");
                return;
            }

            await ExecuteOptimizationCycleAsync(cancellationToken);
        }

        /// <summary>
        /// Gets current performance optimization status
        /// </summary>
        /// <returns>Optimization status</returns>
        public TimerOptimizationStatus GetStatus()
        {
            lock (_lock)
            {
                return new TimerOptimizationStatus
                {
                    IsRunning = _isRunning,
                    Uptime = _uptimeStopwatch.Elapsed,
                    LastOptimizationTime = Metrics.LastOptimizationTime,
                    OptimizationCount = Metrics.OptimizationCount,
                    MemoryPressureEvents = Metrics.MemoryPressureEvents,
                    ThreadPoolAdjustments = Metrics.ThreadPoolAdjustments,
                    CacheOptimizations = Metrics.CacheOptimizations,
                    PerformanceTunings = Metrics.PerformanceTunings
                };
            }
        }

        /// <summary>
        /// Gets detailed performance metrics from all components
        /// </summary>
        /// <returns>Comprehensive performance metrics</returns>
        public TimerOptimizationDetails GetDetailedMetrics()
        {
            return new TimerOptimizationDetails
            {
                OptimizerMetrics = Metrics,
                MemoryMetrics = _memoryManager.GetMetrics(),
                ThreadPoolMetrics = _threadPoolManager.GetMetrics(),
                CacheMetrics = _cacheManager.GetMetrics(),
                SystemMetrics = _metricsCollector.GetSystemMetrics(),
                TuningMetrics = _performanceTuner.GetMetrics()
            };
        }

        /// <summary>
        /// Resets optimization statistics and metrics
        /// </summary>
        public void ResetStatistics()
        {
            lock (_lock)
            {
                Metrics.Reset();
                _memoryManager.ResetStatistics();
                _threadPoolManager.ResetStatistics();
                _cacheManager.ResetStatistics();
                _metricsCollector.ResetStatistics();
                _performanceTuner.ResetStatistics();

                _logger.LogInformation("Performance optimizer statistics reset");
            }
        }

        /// <summary>
        /// Optimization loop that runs periodically
        /// </summary>
        private async Task OptimizationLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    // Wait for the optimization interval
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                    if (cancellationToken.IsCancellationRequested || !_isRunning)
                        break;

                    // Execute optimization cycle
                    await ExecuteOptimizationCycleAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in optimization loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Executes a single optimization cycle
        /// </summary>
        private async Task ExecuteOptimizationCycleAsync(CancellationToken cancellationToken)
        {
            var optimizationId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;

            try
            {
                // Raise optimization started event
                var startedArgs = new TimerOptimizationEventArgs(optimizationId, startTime);
                OptimizationStarted?.Invoke(this, startedArgs);

                _logger.LogDebug("Starting optimization cycle {OptimizationId}", optimizationId);

                // Collect current metrics
                var systemMetrics = await _metricsCollector.CollectMetricsAsync(cancellationToken);

                // Analyze performance and determine optimizations needed
                var optimizationRecommendations = await _performanceTuner.AnalyzePerformanceAsync(systemMetrics, cancellationToken);

                // Apply optimizations
                if (optimizationRecommendations.Count > 0)
                {
                    await ApplyOptimizationsAsync(optimizationRecommendations, cancellationToken);
                }

                // Update metrics
                var duration = DateTime.UtcNow - startTime;
                Metrics.LastOptimizationTime = DateTime.UtcNow;
                Metrics.LastOptimizationDuration = duration;
                Metrics.OptimizationCount++;

                // Raise optimization completed event
                var completedArgs = new TimerOptimizationEventArgs(optimizationId, startTime)
                {
                    EndTime = DateTime.UtcNow,
                    Duration = duration,
                    Success = true,
                    OptimizationsApplied = optimizationRecommendations.Count
                };

                OptimizationCompleted?.Invoke(this, completedArgs);

                _logger.LogDebug("Optimization cycle {OptimizationId} completed in {Duration}ms with {Count} optimizations applied",
                    optimizationId, duration.TotalMilliseconds, optimizationRecommendations.Count);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                Metrics.LastOptimizationTime = DateTime.UtcNow;
                Metrics.LastOptimizationDuration = duration;
                Metrics.OptimizationCount++;
                Metrics.FailedOptimizations++;

                _logger.LogError(ex, "Optimization cycle {OptimizationId} failed", optimizationId);
            }
        }

        /// <summary>
        /// Applies the recommended optimizations
        /// </summary>
        private async Task ApplyOptimizationsAsync(List<TimerOptimizationRecommendation> recommendations, CancellationToken cancellationToken)
        {
            foreach (var recommendation in recommendations)
            {
                try
                {
                    switch (recommendation.Type)
                    {
                        case TimerOptimizationType.Memory:
                            await _memoryManager.ApplyOptimizationAsync(recommendation, cancellationToken);
                            break;

                        case TimerOptimizationType.ThreadPool:
                            await _threadPoolManager.ApplyOptimizationAsync(recommendation, cancellationToken);
                            break;

                        case TimerOptimizationType.Cache:
                            await _cacheManager.ApplyOptimizationAsync(recommendation, cancellationToken);
                            break;

                        case TimerOptimizationType.PerformanceTuning:
                            await _performanceTuner.ApplyOptimizationAsync(recommendation, cancellationToken);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply optimization {OptimizationType}", recommendation.Type);
                }
            }
        }

        #region Event Handlers

        private void OnMemoryPressureDetected(object sender, TimerMemoryPressureEventArgs e)
        {
            Metrics.MemoryPressureEvents++;
            _logger.LogWarning("Memory pressure detected: {PressureLevel}, {UsedMemory}MB used", e.PressureLevel, e.UsedMemoryMB);
        }

        private void OnThreadPoolAdjusted(object sender, TimerThreadPoolEventArgs e)
        {
            Metrics.ThreadPoolAdjustments++;
            _logger.LogDebug("Thread pool adjusted: {OldSize} -> {NewSize}, Reason: {Reason}", e.OldSize, e.NewSize, e.Reason);
        }

        private void OnCacheOptimized(object sender, TimerCacheEventArgs e)
        {
            Metrics.CacheOptimizations++;
            _logger.LogDebug("Cache optimized: {CacheName}, Hit rate: {HitRate:P2}", e.CacheName, e.HitRate);
        }

        private void OnMetricsCollected(object sender, TimerSystemMetricsEventArgs e)
        {
            Metrics.LastSystemMetrics = e.Metrics;
        }

        private void OnTuningApplied(object sender, TimerPerformanceTuningEventArgs e)
        {
            Metrics.PerformanceTunings++;
            PerformanceTuningApplied?.Invoke(this, e);
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
        /// Disposes the performance optimizer
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Stop the optimizer
                    StopAsync().GetAwaiter().GetResult();

                    // Dispose managed resources
                    _cancellationTokenSource?.Dispose();
                    _uptimeStopwatch?.Stop();

                    // Unsubscribe from events
                    _memoryManager.MemoryPressureDetected -= OnMemoryPressureDetected;
                    _threadPoolManager.ThreadPoolAdjusted -= OnThreadPoolAdjusted;
                    _cacheManager.CacheOptimized -= OnCacheOptimized;
                    _metricsCollector.MetricsCollected -= OnMetricsCollected;
                    _performanceTuner.TuningApplied -= OnTuningApplied;

                    // Dispose components
                    (_memoryManager as IDisposable)?.Dispose();
                    (_threadPoolManager as IDisposable)?.Dispose();
                    (_cacheManager as IDisposable)?.Dispose();
                    (_metricsCollector as IDisposable)?.Dispose();
                    (_performanceTuner as IDisposable)?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerPerformanceOptimizer()
        {
            Dispose(false);
        }

        #endregion
    }
}