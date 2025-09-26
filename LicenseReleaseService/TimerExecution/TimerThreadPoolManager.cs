using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Manages thread pool optimization for timer execution operations
    /// </summary>
    public class TimerThreadPoolManager : IDisposable
    {
        private readonly ILogger<TimerThreadPoolManager> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly TimerThreadPoolOptions _threadPoolOptions;
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Stopwatch _uptimeStopwatch;
        private readonly Timer _monitorTimer;
        private readonly Queue<TimerThreadPoolEvent> _adjustmentHistory;
        private readonly Dictionary<string, TimerWorkItemTracker> _workItemTrackers;

        private bool _isRunning;
        private bool _isDisposed;
        private int _optimalThreadPoolSize;
        private int _minThreadPoolSize;
        private int _maxThreadPoolSize;
        private int _currentThreadPoolSize;
        private long _totalAdjustments;
        private long _successfulAdjustments;
        private long _failedAdjustments;
        private long _workItemsProcessed;
        private long _workItemsQueued;
        private long _workItemsRejected;
        private double _averageQueueTime;
        private double _averageProcessingTime;

        /// <summary>
        /// Gets whether the thread pool manager is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the current thread pool size
        /// </summary>
        public int CurrentThreadPoolSize => _currentThreadPoolSize;

        /// <summary>
        /// Gets the optimal thread pool size
        /// </summary>
        public int OptimalThreadPoolSize => _optimalThreadPoolSize;

        /// <summary>
        /// Gets the total number of thread pool adjustments
        /// </summary>
        public long TotalAdjustments => Interlocked.Read(ref _totalAdjustments);

        /// <summary>
        /// Gets the number of successful thread pool adjustments
        /// </summary>
        public long SuccessfulAdjustments => Interlocked.Read(ref _successfulAdjustments);

        /// <summary>
        /// Gets the number of failed thread pool adjustments
        /// </summary>
        public long FailedAdjustments => Interlocked.Read(ref _failedAdjustments);

        /// <summary>
        /// Gets the total number of work items processed
        /// </summary>
        public long WorkItemsProcessed => Interlocked.Read(ref _workItemsProcessed);

        /// <summary>
        /// Gets the total number of work items queued
        /// </summary>
        public long WorkItemsQueued => Interlocked.Read(ref _workItemsQueued);

        /// <summary>
        /// Gets the total number of work items rejected
        /// </summary>
        public long WorkItemsRejected => Interlocked.Read(ref _workItemsRejected);

        /// <summary>
        /// Gets the average queue time for work items
        /// </summary>
        public double AverageQueueTime => _averageQueueTime;

        /// <summary>
        /// Gets the average processing time for work items
        /// </summary>
        public double AverageProcessingTime => _averageProcessingTime;

        /// <summary>
        /// Event raised when thread pool is adjusted
        /// </summary>
        public event EventHandler<TimerThreadPoolEventArgs> ThreadPoolAdjusted;

        /// <summary>
        /// Event raised when work item is queued
        /// </summary>
        public event EventHandler<TimerWorkItemEventArgs> WorkItemQueued;

        /// <summary>
        /// Event raised when work item is processed
        /// </summary>
        public event EventHandler<TimerWorkItemEventArgs> WorkItemProcessed;

        /// <summary>
        /// Event raised when work item is rejected
        /// </summary>
        public event EventHandler<TimerWorkItemEventArgs> WorkItemRejected;

        /// <summary>
        /// Initializes a new instance of the TimerThreadPoolManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Timer execution options</param>
        /// <param name="threadPoolOptions">Thread pool options</param>
        public TimerThreadPoolManager(
            ILogger<TimerThreadPoolManager> logger,
            TimerExecutionOptions options,
            TimerThreadPoolOptions threadPoolOptions = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _threadPoolOptions = threadPoolOptions ?? new TimerThreadPoolOptions();

            _cancellationTokenSource = new CancellationTokenSource();
            _uptimeStopwatch = new Stopwatch();
            _monitorTimer = new Timer(_threadPoolOptions.MonitorIntervalMs);
            _adjustmentHistory = new Queue<TimerThreadPoolEvent>(50);
            _workItemTrackers = new Dictionary<string, TimerWorkItemTracker>();

            // Initialize thread pool settings
            InitializeThreadPoolSettings();
        }

        /// <summary>
        /// Starts the thread pool manager
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TimerThreadPoolManager));

            if (_isRunning)
            {
                _logger.LogWarning("Thread pool manager is already running");
                return;
            }

            lock (_lock)
            {
                if (_isRunning)
                    return;

                _isRunning = true;
                _uptimeStopwatch.Start();
            }

            try
            {
                // Start thread pool monitoring
                _monitorTimer.Elapsed += OnMonitorTimerElapsed;
                _monitorTimer.Start();

                // Apply initial thread pool optimization
                await OptimizeThreadPoolAsync(cancellationToken);

                _logger.LogInformation("Timer thread pool manager started with monitor interval {Interval}ms",
                    _threadPoolOptions.MonitorIntervalMs);
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.LogError(ex, "Failed to start thread pool manager");
                throw;
            }
        }

        /// <summary>
        /// Stops the thread pool manager
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
                // Stop monitoring
                _monitorTimer.Stop();
                _monitorTimer.Elapsed -= OnMonitorTimerElapsed;

                // Cancel pending operations
                _cancellationTokenSource.Cancel();

                // Reset thread pool to defaults
                ResetThreadPoolToDefaults();

                _uptimeStopwatch.Stop();

                _logger.LogInformation("Timer thread pool manager stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping thread pool manager");
            }
        }

        /// <summary>
        /// Queues a work item for execution
        /// </summary>
        /// <param name="workItemId">Unique identifier for the work item</param>
        /// <param name="workItem">The work item to execute</param>
        /// <param name="priority">Priority of the work item</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the queued work item</returns>
        public Task QueueWorkItemAsync(string workItemId, Func<Task> workItem, TimerWorkItemPriority priority = TimerWorkItemPriority.Normal, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(workItemId))
                throw new ArgumentException("Work item ID cannot be null or empty", nameof(workItemId));

            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            return QueueWorkItemInternalAsync(workItemId, workItem, priority, cancellationToken);
        }

        /// <summary>
        /// Gets current thread pool metrics
        /// </summary>
        /// <returns>Thread pool metrics</returns>
        public TimerThreadPoolMetrics GetMetrics()
        {
            lock (_lock)
            {
                var process = Process.GetCurrentProcess();
                var threadPool = ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);
                ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
                ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);

                return new TimerThreadPoolMetrics
                {
                    Uptime = _uptimeStopwatch.Elapsed,
                    CurrentThreadPoolSize = _currentThreadPoolSize,
                    OptimalThreadPoolSize = _optimalThreadPoolSize,
                    MinThreadPoolSize = _minThreadPoolSize,
                    MaxThreadPoolSize = _maxThreadPoolSize,
                    TotalAdjustments = TotalAdjustments,
                    SuccessfulAdjustments = SuccessfulAdjustments,
                    FailedAdjustments = FailedAdjustments,
                    WorkItemsProcessed = WorkItemsProcessed,
                    WorkItemsQueued = WorkItemsQueued,
                    WorkItemsRejected = WorkItemsRejected,
                    AverageQueueTime = _averageQueueTime,
                    AverageProcessingTime = _averageProcessingTime,
                    ActiveWorkerThreads = availableWorkerThreads,
                    ActiveCompletionPortThreads = availableCompletionPortThreads,
                    MaxWorkerThreads = maxWorkerThreads,
                    MaxCompletionPortThreads = maxCompletionPortThreads,
                    MinWorkerThreads = minWorkerThreads,
                    MinCompletionPortThreads = minCompletionPortThreads,
                    ProcessThreads = process.Threads.Count,
                    CPUUsagePercent = GetCpuUsagePercent(process)
                };
            }
        }

        /// <summary>
        /// Applies a thread pool optimization recommendation
        /// </summary>
        /// <param name="recommendation">The optimization recommendation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the optimization operation</returns>
        public async Task ApplyOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken = default)
        {
            if (recommendation.Type != TimerOptimizationType.ThreadPool)
                throw new ArgumentException("Optimization type must be ThreadPool", nameof(recommendation));

            try
            {
                _logger.LogDebug("Applying thread pool optimization: {Description}", recommendation.Description);

                switch (recommendation.Priority)
                {
                    case TimerOptimizationPriority.Critical:
                        await ApplyCriticalThreadPoolOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.High:
                        await ApplyHighThreadPoolOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.Medium:
                        await ApplyMediumThreadPoolOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.Low:
                        await ApplyLowThreadPoolOptimizationAsync(recommendation, cancellationToken);
                        break;
                }

                _logger.LogDebug("Thread pool optimization applied: {Description}", recommendation.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply thread pool optimization: {Description}", recommendation.Description);
            }
        }

        /// <summary>
        /// Adjusts the thread pool size
        /// </summary>
        /// <param name="newSize">New thread pool size</param>
        /// <param name="reason">Reason for the adjustment</param>
        /// <returns>True if the adjustment was successful</returns>
        public async Task<bool> AdjustThreadPoolSizeAsync(int newSize, string reason)
        {
            if (newSize < _minThreadPoolSize || newSize > _maxThreadPoolSize)
            {
                _logger.LogWarning("Invalid thread pool size {NewSize}, must be between {Min} and {Max}",
                    newSize, _minThreadPoolSize, _maxThreadPoolSize);
                return false;
            }

            lock (_lock)
            {
                if (newSize == _currentThreadPoolSize)
                    return true;

                var oldSize = _currentThreadPoolSize;
                var success = ThreadPool.SetMinThreads(newSize, newSize);

                Interlocked.Increment(ref _totalAdjustments);

                if (success)
                {
                    _currentThreadPoolSize = newSize;
                    Interlocked.Increment(ref _successfulAdjustments);

                    RecordAdjustment(oldSize, newSize, reason, true);

                    // Raise event
                    var args = new TimerThreadPoolEventArgs(oldSize, newSize, reason);
                    ThreadPoolAdjusted?.Invoke(this, args);

                    _logger.LogDebug("Thread pool adjusted: {OldSize} -> {NewSize}, Reason: {Reason}",
                        oldSize, newSize, reason);
                }
                else
                {
                    Interlocked.Increment(ref _failedAdjustments);
                    RecordAdjustment(oldSize, newSize, reason, false);

                    _logger.LogWarning("Failed to adjust thread pool: {OldSize} -> {NewSize}, Reason: {Reason}",
                        oldSize, newSize, reason);
                }

                return success;
            }
        }

        /// <summary>
        /// Resets thread pool manager statistics
        /// </summary>
        public void ResetStatistics()
        {
            lock (_lock)
            {
                Interlocked.Exchange(ref _totalAdjustments, 0);
                Interlocked.Exchange(ref _successfulAdjustments, 0);
                Interlocked.Exchange(ref _failedAdjustments, 0);
                Interlocked.Exchange(ref _workItemsProcessed, 0);
                Interlocked.Exchange(ref _workItemsQueued, 0);
                Interlocked.Exchange(ref _workItemsRejected, 0);

                _averageQueueTime = 0;
                _averageProcessingTime = 0;

                _adjustmentHistory.Clear();

                foreach (var tracker in _workItemTrackers.Values)
                {
                    tracker.Reset();
                }

                _logger.LogDebug("Thread pool manager statistics reset");
            }
        }

        #region Private Methods

        private void InitializeThreadPoolSettings()
        {
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);

            _minThreadPoolSize = Math.Max(_threadPoolOptions.MinThreadPoolSize, minWorkerThreads);
            _maxThreadPoolSize = Math.Min(_threadPoolOptions.MaxThreadPoolSize, maxWorkerThreads);
            _currentThreadPoolSize = _minThreadPoolSize;
            _optimalThreadPoolSize = CalculateOptimalThreadPoolSize();

            _logger.LogDebug("Thread pool settings initialized - Min: {Min}, Max: {Max}, Optimal: {Optimal}",
                _minThreadPoolSize, _maxThreadPoolSize, _optimalThreadPoolSize);
        }

        private int CalculateOptimalThreadPoolSize()
        {
            var processorCount = Environment.ProcessorCount;
            var baseSize = processorCount * 2;

            // Adjust based on expected workload
            if (_threadPoolOptions.ExpectedWorkload == TimerThreadPoolWorkload.Light)
                return Math.Max(_minThreadPoolSize, baseSize);
            else if (_threadPoolOptions.ExpectedWorkload == TimerThreadPoolWorkload.Medium)
                return Math.Min(_maxThreadPoolSize, baseSize * 2);
            else if (_threadPoolOptions.ExpectedWorkload == TimerThreadPoolWorkload.Heavy)
                return Math.Min(_maxThreadPoolSize, baseSize * 4);
            else // Dynamic
                return Math.Min(_maxThreadPoolSize, baseSize * 3);
        }

        private async Task QueueWorkItemInternalAsync(string workItemId, Func<Task> workItem, TimerWorkItemPriority priority, CancellationToken cancellationToken)
        {
            var tracker = GetOrCreateWorkItemTracker(workItemId);
            var queuedTime = DateTime.UtcNow;

            try
            {
                // Check if we should reject the work item
                if (ShouldRejectWorkItem(priority))
                {
                    Interlocked.Increment(ref _workItemsRejected);
                    var args = new TimerWorkItemEventArgs(workItemId, priority, "Thread pool at capacity");
                    WorkItemRejected?.Invoke(this, args);
                    return;
                }

                Interlocked.Increment(ref _workItemsQueued);

                // Track the work item
                tracker.Queue(queuedTime, priority);

                // Raise queued event
                var queuedArgs = new TimerWorkItemEventArgs(workItemId, priority, "Work item queued");
                WorkItemQueued?.Invoke(this, queuedArgs);

                // Queue the work item
                await Task.Run(async () =>
                {
                    try
                    {
                        var startTime = DateTime.UtcNow;
                        tracker.Start(startTime);

                        await workItem();

                        var endTime = DateTime.UtcNow;
                        tracker.Complete(endTime);

                        // Update metrics
                        UpdateWorkItemMetrics(tracker, startTime, queuedTime, endTime);

                        // Raise processed event
                        var processedArgs = new TimerWorkItemEventArgs(workItemId, priority, "Work item processed successfully")
                        {
                            QueueTime = startTime - queuedTime,
                            ProcessingTime = endTime - startTime
                        };
                        WorkItemProcessed?.Invoke(this, processedArgs);

                        Interlocked.Increment(ref _workItemsProcessed);
                    }
                    catch (Exception ex)
                    {
                        tracker.Fail(DateTime.UtcNow, ex.Message);

                        var failedArgs = new TimerWorkItemEventArgs(workItemId, priority, $"Work item failed: {ex.Message}");
                        WorkItemRejected?.Invoke(this, failedArgs);

                        Interlocked.Increment(ref _workItemsRejected);
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                tracker.Fail(DateTime.UtcNow, ex.Message);
                Interlocked.Increment(ref _workItemsRejected);

                _logger.LogError(ex, "Error queuing work item {WorkItemId}", workItemId);
            }
        }

        private bool ShouldRejectWorkItem(TimerWorkItemPriority priority)
        {
            var metrics = GetMetrics();
            var queuePressure = metrics.WorkItemsQueued - metrics.WorkItemsProcessed;
            var cpuUsage = metrics.CPUUsagePercent;

            // Reject low priority work items under high load
            if (priority == TimerWorkItemPriority.Low && (queuePressure > 100 || cpuUsage > 80))
                return true;

            // Reject normal priority work items under extreme load
            if (priority == TimerWorkItemPriority.Normal && (queuePressure > 200 || cpuUsage > 90))
                return true;

            // Never reject high priority work items unless absolutely critical
            if (priority == TimerWorkItemPriority.High && queuePressure > 500 && cpuUsage > 95)
                return true;

            return false;
        }

        private TimerWorkItemTracker GetOrCreateWorkItemTracker(string workItemId)
        {
            lock (_lock)
            {
                if (!_workItemTrackers.TryGetValue(workItemId, out var tracker))
                {
                    tracker = new TimerWorkItemTracker(workItemId);
                    _workItemTrackers.Add(workItemId, tracker);
                }

                return tracker;
            }
        }

        private void UpdateWorkItemMetrics(TimerWorkItemTracker tracker, DateTime startTime, DateTime queuedTime, DateTime endTime)
        {
            var queueTime = (startTime - queuedTime).TotalMilliseconds;
            var processingTime = (endTime - startTime).TotalMilliseconds;

            lock (_lock)
            {
                // Update average queue time
                _averageQueueTime = (_averageQueueTime * (WorkItemsProcessed - 1) + queueTime) / WorkItemsProcessed;

                // Update average processing time
                _averageProcessingTime = (_averageProcessingTime * (WorkItemsProcessed - 1) + processingTime) / WorkItemsProcessed;
            }
        }

        private void OnMonitorTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _ = Task.Run(() => MonitorThreadPoolAsync(_cancellationTokenSource.Token));
        }

        private async Task MonitorThreadPoolAsync(CancellationToken cancellationToken)
        {
            try
            {
                var metrics = GetMetrics();
                var shouldOptimize = ShouldOptimizeThreadPool(metrics);

                if (shouldOptimize)
                {
                    await OptimizeThreadPoolAsync(cancellationToken);
                }

                // Clean up old work item trackers
                CleanupWorkItemTrackers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in thread pool monitoring");
            }
        }

        private bool ShouldOptimizeThreadPool(TimerThreadPoolMetrics metrics)
        {
            // Check if we need to optimize based on various metrics
            var queuePressure = metrics.WorkItemsQueued - metrics.WorkItemsProcessed;
            var cpuUsage = metrics.CPUUsagePercent;
            var threadUtilization = (metrics.MaxWorkerThreads - metrics.ActiveWorkerThreads) * 100.0 / metrics.MaxWorkerThreads;

            // Optimize if CPU usage is too high
            if (cpuUsage > 85 && metrics.CurrentThreadPoolSize > metrics.MinThreadPoolSize)
                return true;

            // Optimize if queue pressure is too high
            if (queuePressure > 50 && metrics.CurrentThreadPoolSize < metrics.MaxThreadPoolSize)
                return true;

            // Optimize if thread utilization is too low
            if (threadUtilization > 70 && metrics.CurrentThreadPoolSize > metrics.MinThreadPoolSize)
                return true;

            return false;
        }

        private async Task OptimizeThreadPoolAsync(CancellationToken cancellationToken)
        {
            try
            {
                var metrics = GetMetrics();
                var newSize = CalculateOptimalSize(metrics);

                if (newSize != _currentThreadPoolSize)
                {
                    var reason = $"Optimization based on metrics - CPU: {metrics.CPUUsagePercent:F1}%, Queue: {metrics.WorkItemsQueued - metrics.WorkItemsProcessed}";
                    await AdjustThreadPoolSizeAsync(newSize, reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing thread pool");
            }
        }

        private int CalculateOptimalSize(TimerThreadPoolMetrics metrics)
        {
            var currentSize = metrics.CurrentThreadPoolSize;
            var cpuUsage = metrics.CPUUsagePercent;
            var queuePressure = metrics.WorkItemsQueued - metrics.WorkItemsProcessed;
            var threadUtilization = (metrics.MaxWorkerThreads - metrics.ActiveWorkerThreads) * 100.0 / metrics.MaxWorkerThreads;

            // If CPU is high, reduce thread pool size
            if (cpuUsage > 85)
                return Math.Max(_minThreadPoolSize, currentSize - 1);

            // If queue pressure is high, increase thread pool size
            if (queuePressure > 50 && threadUtilization < 30)
                return Math.Min(_maxThreadPoolSize, currentSize + 1);

            // If thread utilization is low, reduce size
            if (threadUtilization > 70)
                return Math.Max(_minThreadPoolSize, currentSize - 1);

            return currentSize;
        }

        private void CleanupWorkItemTrackers()
        {
            lock (_lock)
            {
                var oldTrackers = _workItemTrackers.Values
                    .Where(t => t.LastActivity < DateTime.UtcNow.AddMinutes(-5))
                    .ToList();

                foreach (var tracker in oldTrackers)
                {
                    _workItemTrackers.Remove(tracker.WorkItemId);
                }
            }
        }

        private void ResetThreadPoolToDefaults()
        {
            ThreadPool.SetMinThreads(_minThreadPoolSize, _minThreadPoolSize);
            _currentThreadPoolSize = _minThreadPoolSize;

            _logger.LogDebug("Thread pool reset to defaults");
        }

        private void RecordAdjustment(int oldSize, int newSize, string reason, bool success)
        {
            var adjustment = new TimerThreadPoolEvent(oldSize, newSize, reason, success, DateTime.UtcNow);

            lock (_lock)
            {
                _adjustmentHistory.Enqueue(adjustment);

                // Keep only recent history
                while (_adjustmentHistory.Count > 50)
                {
                    _adjustmentHistory.Dequeue();
                }
            }
        }

        private double GetCpuUsagePercent(Process process)
        {
            try
            {
                process.Refresh();
                return process.TotalProcessorTime.TotalMilliseconds / (Environment.ProcessorCount * process.TotalProcessorTime.TotalMilliseconds) * 100;
            }
            catch
            {
                return 0;
            }
        }

        private async Task ApplyCriticalThreadPoolOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            // Reduce thread pool size to minimum under critical conditions
            await AdjustThreadPoolSizeAsync(_minThreadPoolSize, "Critical optimization applied");
        }

        private async Task ApplyHighThreadPoolOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            // Optimize for current conditions
            var metrics = GetMetrics();
            var newSize = CalculateOptimalSize(metrics);
            await AdjustThreadPoolSizeAsync(newSize, "High priority optimization applied");
        }

        private async Task ApplyMediumThreadPoolOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            // Moderate optimization
            var metrics = GetMetrics();
            var newSize = Math.Max(_minThreadPoolSize, Math.Min(_maxThreadPoolSize, metrics.CurrentThreadPoolSize + 1));
            await AdjustThreadPoolSizeAsync(newSize, "Medium priority optimization applied");
        }

        private async Task ApplyLowThreadPoolOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            // Minimal optimization - just ensure optimal size
            await OptimizeThreadPoolAsync(cancellationToken);
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
        /// Disposes the thread pool manager
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Stop the manager
                    StopAsync().GetAwaiter().GetResult();

                    // Dispose managed resources
                    _cancellationTokenSource?.Dispose();
                    _monitorTimer?.Dispose();
                    _uptimeStopwatch?.Stop();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerThreadPoolManager()
        {
            Dispose(false);
        }

        #endregion
    }
}