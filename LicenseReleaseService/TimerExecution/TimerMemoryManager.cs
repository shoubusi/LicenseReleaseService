using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Timers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Manages memory optimization for timer execution operations
    /// </summary>
    public class TimerMemoryManager : IDisposable
    {
        private readonly ILogger<TimerMemoryManager> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly TimerMemoryOptions _memoryOptions;
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Stopwatch _uptimeStopwatch;
        private readonly Queue<TimerMemoryPressureEvent> _pressureHistory;
        private readonly Dictionary<string, TimerMemoryPool> _memoryPools;
        private readonly System.Timers.Timer _memoryMonitorTimer;

        private bool _isRunning;
        private bool _isDisposed;
        private TimerMemoryPressureLevel _currentPressureLevel;
        private long _totalMemoryOptimized;
        private long _gcCollectionsForced;
        private long _memoryLeaksDetected;
        private long _memoryPoolsCreated;
        private long _memoryPoolsReleased;

        /// <summary>
        /// Gets whether the memory manager is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the current memory pressure level
        /// </summary>
        public TimerMemoryPressureLevel CurrentPressureLevel => _currentPressureLevel;

        /// <summary>
        /// Gets the total memory optimized in bytes
        /// </summary>
        public long TotalMemoryOptimized => Interlocked.Read(ref _totalMemoryOptimized);

        /// <summary>
        /// Gets the current memory statistics asynchronously
        /// </summary>
        /// <returns>Memory statistics</returns>
        public Task<TimerMemoryStatistics> GetMemoryStatisticsAsync()
        {
            return Task.FromResult(new TimerMemoryStatistics
            {
                IsRunning = _isRunning,
                CurrentPressureLevel = _currentPressureLevel,
                TotalMemoryOptimized = _totalMemoryOptimized,
                GcCollectionsForced = _gcCollectionsForced,
                MemoryLeaksDetected = _memoryLeaksDetected,
                MemoryPoolsCreated = _memoryPoolsCreated,
                MemoryPoolsReleased = _memoryPoolsReleased,
                CurrentMemoryUsage = GC.GetTotalMemory(false),
                Uptime = _uptimeStopwatch.Elapsed,
                GeneratedAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Gets the number of forced GC collections
        /// </summary>
        public long GcCollectionsForced => Interlocked.Read(ref _gcCollectionsForced);

        /// <summary>
        /// Gets the number of memory leaks detected
        /// </summary>
        public long MemoryLeaksDetected => Interlocked.Read(ref _memoryLeaksDetected);

        /// <summary>
        /// Event raised when memory pressure is detected
        /// </summary>
        public event EventHandler<TimerMemoryPressureEventArgs> MemoryPressureDetected;

        /// <summary>
        /// Event raised when memory optimization is performed
        /// </summary>
        public event EventHandler<TimerMemoryOptimizationEventArgs> MemoryOptimizationPerformed;

        /// <summary>
        /// Event raised when a memory leak is detected
        /// </summary>
        public event EventHandler<TimerMemoryLeakEventArgs> MemoryLeakDetected;

        /// <summary>
        /// Initializes a new instance of the TimerMemoryManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Timer execution options</param>
        /// <param name="memoryOptions">Memory management options</param>
        public TimerMemoryManager(
            ILogger<TimerMemoryManager> logger,
            TimerExecutionOptions options,
            TimerMemoryOptions memoryOptions = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _memoryOptions = memoryOptions ?? new TimerMemoryOptions();

            _cancellationTokenSource = new CancellationTokenSource();
            _uptimeStopwatch = new Stopwatch();
            _pressureHistory = new Queue<TimerMemoryPressureEvent>(100);
            _memoryPools = new Dictionary<string, TimerMemoryPool>();
            _memoryMonitorTimer = new System.Timers.Timer(_memoryOptions.MonitorIntervalMs);

            _currentPressureLevel = TimerMemoryPressureLevel.None;
        }

        /// <summary>
        /// Starts the memory manager
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TimerMemoryManager));

            if (_isRunning)
            {
                _logger.LogWarning("Memory manager is already running");
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
                // Start memory monitoring
                _memoryMonitorTimer.Elapsed += OnMemoryMonitorElapsed;
                _memoryMonitorTimer.Start();

                // Initialize memory pools
                await InitializeMemoryPoolsAsync(cancellationToken);

                // Set up GC notifications
                GC.RegisterForFullGCNotification(_memoryOptions.MaxGenerationThreshold, _memoryOptions.LargeObjectHeapThreshold);

                _logger.LogInformation("Timer memory manager started with monitor interval {Interval}ms", _memoryOptions.MonitorIntervalMs);
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.LogError(ex, "Failed to start memory manager");
                throw;
            }
        }

        /// <summary>
        /// Stops the memory manager
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
                // Stop memory monitoring
                _memoryMonitorTimer.Stop();
                _memoryMonitorTimer.Elapsed -= OnMemoryMonitorElapsed;

                // Cancel pending operations
                _cancellationTokenSource.Cancel();

                // Release memory pools
                await ReleaseMemoryPoolsAsync();

                _uptimeStopwatch.Stop();

                _logger.LogInformation("Timer memory manager stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping memory manager");
            }
        }

        /// <summary>
        /// Gets current memory metrics
        /// </summary>
        /// <returns>Memory metrics</returns>
        public TimerMemoryMetrics GetMetrics()
        {
            lock (_lock)
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var memoryMetrics = new TimerMemoryMetrics
                {
                    Uptime = _uptimeStopwatch.Elapsed,
                    CurrentPressureLevel = _currentPressureLevel,
                    WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                    PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                    VirtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024),
                    GCGeneration0Collections = GC.CollectionCount(0),
                    GCGeneration1Collections = GC.CollectionCount(1),
                    GCGeneration2Collections = GC.CollectionCount(2),
                    TotalMemoryOptimized = TotalMemoryOptimized,
                    GcCollectionsForced = GcCollectionsForced,
                    MemoryLeaksDetected = MemoryLeaksDetected,
                    MemoryPoolsActive = _memoryPools.Count,
                    MemoryPoolsCreated = _memoryPoolsCreated,
                    MemoryPoolsReleased = _memoryPoolsReleased,
                    IsServerGC = GCSettings.IsServerGC,
                    LatencyMode = GCSettings.LatencyMode
                };

                // Calculate memory pressure
                memoryMetrics.MemoryPressurePercent = CalculateMemoryPressurePercent(process);

                return memoryMetrics;
            }
        }

        /// <summary>
        /// Applies a memory optimization recommendation
        /// </summary>
        /// <param name="recommendation">The optimization recommendation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the optimization operation</returns>
        public async Task ApplyOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken = default)
        {
            if (recommendation.Type != TimerOptimizationType.Memory)
                throw new ArgumentException("Optimization type must be Memory", nameof(recommendation));

            try
            {
                _logger.LogDebug("Applying memory optimization: {Description}", recommendation.Description);

                switch (recommendation.Priority)
                {
                    case TimerOptimizationPriority.Critical:
                        await ApplyCriticalMemoryOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.High:
                        await ApplyHighMemoryOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.Medium:
                        await ApplyMediumMemoryOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.Low:
                        await ApplyLowMemoryOptimizationAsync(recommendation, cancellationToken);
                        break;
                }

                // Raise optimization performed event
                var args = new TimerMemoryOptimizationEventArgs(recommendation.Description, recommendation.Priority);
                MemoryOptimizationPerformed?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply memory optimization: {Description}", recommendation.Description);
            }
        }

        /// <summary>
        /// Forces garbage collection
        /// </summary>
        /// <param name="generation">GC generation to collect</param>
        /// <param name="mode">GC mode</param>
        /// <returns>Memory freed in bytes</returns>
        public long ForceGarbageCollection(int generation, GCCollectionMode mode = GCCollectionMode.Forced)
        {
            try
            {
                var beforeMemory = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;

                GC.Collect(generation, mode);
                GC.WaitForPendingFinalizers();

                var afterMemory = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
                var memoryFreed = beforeMemory - afterMemory;

                if (memoryFreed > 0)
                {
                    System.Threading.Interlocked.Add(ref _totalMemoryOptimized, memoryFreed);
                    System.Threading.Interlocked.Increment(ref _gcCollectionsForced);
                }

                _logger.LogDebug("Forced GC collection: Gen {Generation}, Mode: {Mode}, Freed: {Freed}MB",
                    generation, mode, memoryFreed / (1024 * 1024));

                return memoryFreed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing garbage collection");
                return 0;
            }
        }

        /// <summary>
        /// Forces garbage collection with default generation
        /// </summary>
        /// <param name="mode">GC mode</param>
        /// <returns>Memory freed in bytes</returns>
        public long ForceGarbageCollection(GCCollectionMode mode = GCCollectionMode.Forced)
        {
            return ForceGarbageCollection(GC.MaxGeneration, mode);
        }

        /// <summary>
        /// Forces garbage collection with specified generation and default mode
        /// </summary>
        /// <param name="generation">GC generation to collect</param>
        /// <returns>Memory freed in bytes</returns>
        public long ForceGarbageCollection(int generation)
        {
            return ForceGarbageCollection(generation, GCCollectionMode.Forced);
        }

        /// <summary>
        /// Creates a memory pool for objects of a specific type
        /// </summary>
        /// <param name="poolName">Name of the pool</param>
        /// <param name="objectSize">Size of objects in the pool</param>
        /// <param name="initialCapacity">Initial capacity</param>
        /// <returns>Memory pool instance</returns>
        public TimerMemoryPool CreateMemoryPool(string poolName, int objectSize, int initialCapacity = 10)
        {
            lock (_lock)
            {
                if (_memoryPools.ContainsKey(poolName))
                {
                    throw new ArgumentException($"Memory pool '{poolName}' already exists", nameof(poolName));
                }

                var pool = new TimerMemoryPool(poolName, objectSize, initialCapacity);
                _memoryPools.Add(poolName, pool);
                Interlocked.Increment(ref _memoryPoolsCreated);

                _logger.LogDebug("Created memory pool: {PoolName}, Size: {Size} bytes, Capacity: {Capacity}",
                    poolName, objectSize, initialCapacity);

                return pool;
            }
        }

        /// <summary>
        /// Gets a memory pool by name
        /// </summary>
        /// <param name="poolName">Name of the pool</param>
        /// <returns>Memory pool instance or null if not found</returns>
        public TimerMemoryPool GetMemoryPool(string poolName)
        {
            lock (_lock)
            {
                return _memoryPools.TryGetValue(poolName, out var pool) ? pool : null;
            }
        }

        /// <summary>
        /// Releases a memory pool
        /// </summary>
        /// <param name="poolName">Name of the pool to release</param>
        /// <returns>True if the pool was released successfully</returns>
        public bool ReleaseMemoryPool(string poolName)
        {
            lock (_lock)
            {
                if (_memoryPools.TryGetValue(poolName, out var pool))
                {
                    pool.Dispose();
                    _memoryPools.Remove(poolName);
                    Interlocked.Increment(ref _memoryPoolsReleased);

                    _logger.LogDebug("Released memory pool: {PoolName}", poolName);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Resets memory manager statistics
        /// </summary>
        public void ResetStatistics()
        {
            lock (_lock)
            {
                Interlocked.Exchange(ref _totalMemoryOptimized, 0);
                Interlocked.Exchange(ref _gcCollectionsForced, 0);
                Interlocked.Exchange(ref _memoryLeaksDetected, 0);
                Interlocked.Exchange(ref _memoryPoolsCreated, 0);
                Interlocked.Exchange(ref _memoryPoolsReleased, 0);

                _pressureHistory.Clear();

                _logger.LogDebug("Memory manager statistics reset");
            }
        }

        #region Private Methods

        private async Task InitializeMemoryPoolsAsync(CancellationToken cancellationToken)
        {
            // Create default memory pools for timer operations
            CreateMemoryPool("TimerExecution", 1024, 50); // 1KB objects
            CreateMemoryPool("LicenseCheck", 2048, 25);   // 2KB objects
            CreateMemoryPool("CacheEntry", 512, 100);     // 512B objects
            CreateMemoryPool("Metrics", 256, 200);        // 256B objects

            _logger.LogDebug("Initialized {Count} memory pools", _memoryPools.Count);
        }

        private async Task ReleaseMemoryPoolsAsync()
        {
            var poolNames = _memoryPools.Keys.ToArray();
            foreach (var poolName in poolNames)
            {
                ReleaseMemoryPool(poolName);
            }

            _logger.LogDebug("Released all memory pools");
        }

        private void OnMemoryMonitorElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _ = Task.Run(() => MonitorMemoryAsync(_cancellationTokenSource.Token));
        }

        private async Task MonitorMemoryAsync(CancellationToken cancellationToken)
        {
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var currentMemoryMB = process.WorkingSet64 / (1024 * 1024);
                var pressureLevel = DetermineMemoryPressureLevel(process);

                if (pressureLevel != _currentPressureLevel)
                {
                    await HandleMemoryPressureChangeAsync(pressureLevel, currentMemoryMB, cancellationToken);
                }

                // Check for memory leaks
                if (_memoryOptions.EnableMemoryLeakDetection)
                {
                    await CheckForMemoryLeaksAsync(cancellationToken);
                }

                // Record pressure event
                RecordPressureEvent(pressureLevel, currentMemoryMB);

                // Check for GC notifications
                CheckGCNotifications();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in memory monitoring");
            }
        }

        private TimerMemoryPressureLevel DetermineMemoryPressureLevel(System.Diagnostics.Process process)
        {
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            var totalMemoryMB = _memoryOptions.SystemMemoryMB != 0 ? _memoryOptions.SystemMemoryMB : 8192; // Default 8GB
            var memoryUsagePercent = (memoryMB * 100.0) / totalMemoryMB;

            if (memoryUsagePercent >= _memoryOptions.CriticalPressureThreshold)
                return TimerMemoryPressureLevel.Critical;
            else if (memoryUsagePercent >= _memoryOptions.HighPressureThreshold)
                return TimerMemoryPressureLevel.High;
            else if (memoryUsagePercent >= _memoryOptions.MediumPressureThreshold)
                return TimerMemoryPressureLevel.Medium;
            else if (memoryUsagePercent >= _memoryOptions.LowPressureThreshold)
                return TimerMemoryPressureLevel.Low;
            else
                return TimerMemoryPressureLevel.None;
        }

        private async Task HandleMemoryPressureChangeAsync(TimerMemoryPressureLevel newLevel, long currentMemoryMB, CancellationToken cancellationToken)
        {
            var oldLevel = _currentPressureLevel;
            _currentPressureLevel = newLevel;

            _logger.LogWarning("Memory pressure level changed from {OldLevel} to {NewLevel} ({MemoryMB}MB used)",
                oldLevel, newLevel, currentMemoryMB);

            // Raise pressure detected event
            var args = new TimerMemoryPressureEventArgs(newLevel, currentMemoryMB);
            MemoryPressureDetected?.Invoke(this, args);

            // Apply optimizations based on pressure level
            switch (newLevel)
            {
                case TimerMemoryPressureLevel.Critical:
                    await ApplyCriticalMemoryOptimizationAsync(cancellationToken);
                    break;

                case TimerMemoryPressureLevel.High:
                    await ApplyHighMemoryOptimizationAsync(cancellationToken);
                    break;

                case TimerMemoryPressureLevel.Medium:
                    await ApplyMediumMemoryOptimizationAsync(cancellationToken);
                    break;

                case TimerMemoryPressureLevel.Low:
                    await ApplyLowMemoryOptimizationAsync(cancellationToken);
                    break;
            }
        }

        private async Task ApplyCriticalMemoryOptimizationAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("Applying critical memory optimization");

            // Force full garbage collection
            var memoryFreed = ForceGarbageCollection(GC.MaxGeneration, GCCollectionMode.Forced);

            // Clear all memory pools
            await ClearAllMemoryPoolsAsync();

            // Compact LOH
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();

            _logger.LogInformation("Critical optimization applied, freed {Freed}MB", memoryFreed / (1024 * 1024));
        }

        private async Task ApplyHighMemoryOptimizationAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("Applying high memory optimization");

            // Force generation 2 collection
            var memoryFreed = ForceGarbageCollection(2, GCCollectionMode.Forced);

            // Clear non-critical memory pools
            await ClearNonCriticalMemoryPoolsAsync();

            _logger.LogInformation("High optimization applied, freed {Freed}MB", memoryFreed / (1024 * 1024));
        }

        private async Task ApplyMediumMemoryOptimizationAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Applying medium memory optimization");

            // Force generation 1 collection
            var memoryFreed = ForceGarbageCollection(1, GCCollectionMode.Optimized);

            // Reduce memory pool sizes
            await ReduceMemoryPoolSizesAsync();

            _logger.LogDebug("Medium optimization applied, freed {Freed}MB", memoryFreed / (1024 * 1024));
        }

        private async Task ApplyLowMemoryOptimizationAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Applying low memory optimization");

            // Force generation 0 collection
            var memoryFreed = ForceGarbageCollection(0, GCCollectionMode.Optimized);

            // Clean up old pressure history
            CleanupPressureHistory();

            _logger.LogDebug("Low optimization applied, freed {Freed}MB", memoryFreed / (1024 * 1024));
        }

        private async Task ApplyCriticalMemoryOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            await ApplyCriticalMemoryOptimizationAsync(cancellationToken);
        }

        private async Task ApplyHighMemoryOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            await ApplyHighMemoryOptimizationAsync(cancellationToken);
        }

        private async Task ApplyMediumMemoryOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            await ApplyMediumMemoryOptimizationAsync(cancellationToken);
        }

        private async Task ApplyLowMemoryOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            await ApplyLowMemoryOptimizationAsync(cancellationToken);
        }

        private async Task ClearAllMemoryPoolsAsync()
        {
            var pools = _memoryPools.Values.ToArray();
            foreach (var pool in pools)
            {
                pool.Clear();
            }

            _logger.LogDebug("Cleared all memory pools");
        }

        private async Task ClearNonCriticalMemoryPoolsAsync()
        {
            var nonCriticalPools = _memoryPools.Values
                .Where(p => !p.IsCritical)
                .ToArray();

            foreach (var pool in nonCriticalPools)
            {
                pool.Clear();
            }

            _logger.LogDebug("Cleared {Count} non-critical memory pools", nonCriticalPools.Length);
        }

        private async Task ReduceMemoryPoolSizesAsync()
        {
            foreach (var pool in _memoryPools.Values)
            {
                pool.ReduceSize();
            }

            _logger.LogDebug("Reduced memory pool sizes");
        }

        private async Task CheckForMemoryLeaksAsync(CancellationToken cancellationToken)
        {
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var currentMemoryMB = process.WorkingSet64 / (1024 * 1024);

                // Simple memory leak detection - check if memory keeps growing
                if (_pressureHistory.Count > 5)
                {
                    var recentEvents = _pressureHistory.Skip(Math.Max(0, _pressureHistory.Count - 5)).ToArray();
                    var isMemoryGrowing = recentEvents.All(e => e.MemoryMB > recentEvents[0].MemoryMB * 1.1);

                    if (isMemoryGrowing && currentMemoryMB > _memoryOptions.MemoryLeakThresholdMB)
                    {
                        Interlocked.Increment(ref _memoryLeaksDetected);

                        var args = new TimerMemoryLeakEventArgs(
                            currentMemoryMB,
                            recentEvents.Average(e => e.MemoryMB),
                            "Memory usage consistently growing");

                        MemoryLeakDetected?.Invoke(this, args);

                        _logger.LogWarning("Potential memory leak detected: Current {Current}MB, Average {Average}MB",
                            currentMemoryMB, args.AverageMemoryMB);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for memory leaks");
            }
        }

        private void CheckGCNotifications()
        {
            try
            {
                if (GC.WaitForFullGCApproach(100) == GCNotificationStatus.Succeeded)
                {
                    _logger.LogWarning("Full GC approaching");
                    // Pre-emptive optimization could be applied here
                }

                if (GC.WaitForFullGCComplete(100) == GCNotificationStatus.Succeeded)
                {
                    _logger.LogInformation("Full GC completed");
                    // Post-GC optimization could be applied here
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking GC notifications");
            }
        }

        private void RecordPressureEvent(TimerMemoryPressureLevel level, long memoryMB)
        {
            var eventObj = new TimerMemoryPressureEvent(level, memoryMB, DateTime.UtcNow);

            lock (_lock)
            {
                _pressureHistory.Enqueue(eventObj);

                // Keep only recent history
                while (_pressureHistory.Count > 100)
                {
                    _pressureHistory.Dequeue();
                }
            }
        }

        private void CleanupPressureHistory()
        {
            lock (_lock)
            {
                // Keep only last 50 events
                while (_pressureHistory.Count > 50)
                {
                    _pressureHistory.Dequeue();
                }
            }
        }

        private double CalculateMemoryPressurePercent(System.Diagnostics.Process process)
        {
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            var totalMemoryMB = _memoryOptions.SystemMemoryMB != 0 ? _memoryOptions.SystemMemoryMB : 8192;
            return (memoryMB * 100.0) / totalMemoryMB;
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
        /// Disposes the memory manager
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
                    _memoryMonitorTimer?.Dispose();
                    _uptimeStopwatch?.Stop();

                    // Dispose memory pools
                    foreach (var pool in _memoryPools.Values)
                    {
                        pool.Dispose();
                    }
                    _memoryPools.Clear();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerMemoryManager()
        {
            Dispose(false);
        }

        #endregion
    }
}