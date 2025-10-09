using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Manages intelligent caching strategies for timer execution operations
    /// </summary>
    public class TimerCacheManager : IDisposable
    {
        private readonly ILogger<TimerCacheManager> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly TimerCacheOptions _cacheOptions;
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Stopwatch _uptimeStopwatch;
        private readonly System.Timers.Timer _monitorTimer;
        private readonly System.Timers.Timer _cleanupTimer;
        private readonly Dictionary<string, TimerCache> _caches;
        private readonly Queue<TimerCacheEvent> _optimizationHistory;

        private bool _isRunning;
        private bool _isDisposed;
        private long _totalCacheHits;
        private long _totalCacheMisses;
        private long _totalCacheItems;
        private long _totalCacheEvictions;
        private long _cacheOptimizationsPerformed;
        private double _overallHitRate;

        /// <summary>
        /// Gets whether the cache manager is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the total number of cache hits
        /// </summary>
        public long TotalCacheHits => Interlocked.Read(ref _totalCacheHits);

        /// <summary>
        /// Gets the total number of cache misses
        /// </summary>
        public long TotalCacheMisses => Interlocked.Read(ref _totalCacheMisses);

        /// <summary>
        /// Gets the total number of cache items
        /// </summary>
        public long TotalCacheItems => Interlocked.Read(ref _totalCacheItems);

        /// <summary>
        /// Gets the total number of cache evictions
        /// </summary>
        public long TotalCacheEvictions => Interlocked.Read(ref _totalCacheEvictions);

        /// <summary>
        /// Gets the current cache metrics asynchronously
        /// </summary>
        /// <returns>Cache metrics</returns>
        public Task<TimerCacheMetrics> GetCacheMetricsAsync()
        {
            return Task.FromResult(new TimerCacheMetrics
            {
                IsRunning = _isRunning,
                TotalCacheHits = _totalCacheHits,
                TotalCacheMisses = _totalCacheMisses,
                TotalCacheItems = _totalCacheItems,
                TotalCacheEvictions = _totalCacheEvictions,
                CacheOptimizationsPerformed = _cacheOptimizationsPerformed,
                OverallHitRate = _overallHitRate,
                TotalCaches = _caches.Count,
                Uptime = _uptimeStopwatch.Elapsed,
                GeneratedAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Gets the overall cache hit rate
        /// </summary>
        public double OverallHitRate => _overallHitRate;

        /// <summary>
        /// Event raised when cache is optimized
        /// </summary>
        public event EventHandler<TimerCacheEventArgs> CacheOptimized;

        /// <summary>
        /// Event raised when cache item is accessed
        /// </summary>
        public event EventHandler<TimerCacheItemEventArgs> CacheItemAccessed;

        /// <summary>
        /// Event raised when cache item is evicted
        /// </summary>
        public event EventHandler<TimerCacheItemEventArgs> CacheItemEvicted;

        /// <summary>
        /// Initializes a new instance of the TimerCacheManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Timer execution options</param>
        /// <param name="cacheOptions">Cache options</param>
        public TimerCacheManager(
            ILogger<TimerCacheManager> logger,
            TimerExecutionOptions options,
            TimerCacheOptions cacheOptions = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _cacheOptions = cacheOptions ?? new TimerCacheOptions();

            _cancellationTokenSource = new CancellationTokenSource();
            _uptimeStopwatch = new Stopwatch();
            _monitorTimer = new System.Timers.Timer(_cacheOptions.MonitorIntervalMs);
            _cleanupTimer = new System.Timers.Timer(_cacheOptions.CleanupIntervalMs);
            _caches = new Dictionary<string, TimerCache>();
            _optimizationHistory = new Queue<TimerCacheEvent>(50);
        }

        /// <summary>
        /// Starts the cache manager
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TimerCacheManager));

            if (_isRunning)
            {
                _logger.LogWarning("Cache manager is already running");
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
                // Start cache monitoring
                _monitorTimer.Elapsed += OnMonitorTimerElapsed;
                _monitorTimer.Start();

                // Start cache cleanup
                _cleanupTimer.Elapsed += OnCleanupTimerElapsed;
                _cleanupTimer.Start();

                // Initialize default caches
                await InitializeDefaultCachesAsync(cancellationToken);

                _logger.LogInformation("Timer cache manager started with monitor interval {MonitorInterval}ms and cleanup interval {CleanupInterval}ms",
                    _cacheOptions.MonitorIntervalMs, _cacheOptions.CleanupIntervalMs);
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.LogError(ex, "Failed to start cache manager");
                throw;
            }
        }

        /// <summary>
        /// Stops the cache manager
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
                // Stop monitoring and cleanup
                _monitorTimer.Stop();
                _monitorTimer.Elapsed -= OnMonitorTimerElapsed;
                _cleanupTimer.Stop();
                _cleanupTimer.Elapsed -= OnCleanupTimerElapsed;

                // Cancel pending operations
                _cancellationTokenSource.Cancel();

                // Clear all caches
                await ClearAllCachesAsync();

                _uptimeStopwatch.Stop();

                _logger.LogInformation("Timer cache manager stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping cache manager");
            }
        }

        /// <summary>
        /// Creates a new cache
        /// </summary>
        /// <param name="cacheName">Name of the cache</param>
        /// <param name="maxSize">Maximum size of the cache</param>
        /// <param name="expirationPolicy">Expiration policy for the cache</param>
        /// <param name="evictionPolicy">Eviction policy for the cache</param>
        /// <returns>Cache instance</returns>
        public TimerCache CreateCache(string cacheName, int maxSize, TimerCacheExpirationPolicy expirationPolicy = null, TimerCacheEvictionPolicy evictionPolicy = null)
        {
            if (string.IsNullOrEmpty(cacheName))
                throw new ArgumentException("Cache name cannot be null or empty", nameof(cacheName));

            if (maxSize < 1)
                throw new ArgumentException("Cache size must be at least 1", nameof(maxSize));

            lock (_lock)
            {
                if (_caches.ContainsKey(cacheName))
                {
                    throw new ArgumentException($"Cache '{cacheName}' already exists", nameof(cacheName));
                }

                var cache = new TimerCache(cacheName, maxSize, expirationPolicy, evictionPolicy);
                cache.ItemAccessed += OnCacheItemAccessed;
                cache.ItemEvicted += OnCacheItemEvicted;

                _caches.Add(cacheName, cache);

                _logger.LogDebug("Created cache: {CacheName}, Size: {MaxSize}", cacheName, maxSize);

                return cache;
            }
        }

        /// <summary>
        /// Gets a cache by name
        /// </summary>
        /// <param name="cacheName">Name of the cache</param>
        /// <returns>Cache instance or null if not found</returns>
        public TimerCache GetCache(string cacheName)
        {
            lock (_lock)
            {
                return _caches.TryGetValue(cacheName, out var cache) ? cache : null;
            }
        }

        /// <summary>
        /// Removes a cache
        /// </summary>
        /// <param name="cacheName">Name of the cache to remove</param>
        /// <returns>True if the cache was removed successfully</returns>
        public bool RemoveCache(string cacheName)
        {
            lock (_lock)
            {
                if (_caches.TryGetValue(cacheName, out var cache))
                {
                    cache.ItemAccessed -= OnCacheItemAccessed;
                    cache.ItemEvicted -= OnCacheItemEvicted;
                    cache.Dispose();
                    _caches.Remove(cacheName);

                    _logger.LogDebug("Removed cache: {CacheName}", cacheName);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets all cache names
        /// </summary>
        /// <returns>Collection of cache names</returns>
        public IEnumerable<string> GetCacheNames()
        {
            lock (_lock)
            {
                return _caches.Keys.ToArray();
            }
        }

        /// <summary>
        /// Gets current cache metrics
        /// </summary>
        /// <returns>Cache metrics</returns>
        public TimerCacheMetrics GetMetrics()
        {
            lock (_lock)
            {
                var cacheMetrics = _caches.Values.Select(c => c.GetMetrics()).ToList();
                var totalSize = cacheMetrics.Sum(m => m.Size);
                var totalMemoryUsage = cacheMetrics.Sum(m => m.MemoryUsageBytes);

                return new TimerCacheMetrics
                {
                    Uptime = _uptimeStopwatch.Elapsed,
                    TotalCaches = _caches.Count,
                    TotalCacheItems = TotalCacheItems,
                    TotalCacheHits = TotalCacheHits,
                    TotalCacheMisses = TotalCacheMisses,
                    TotalCacheEvictions = TotalCacheEvictions,
                    OverallHitRate = OverallHitRate,
                    TotalSize = totalSize,
                    TotalMemoryUsage = totalMemoryUsage,
                    CacheOptimizationsPerformed = _cacheOptimizationsPerformed,
                    AverageHitRate = cacheMetrics.Count > 0 ? cacheMetrics.Average(m => m.HitRate) : 0,
                    AverageEvictionRate = cacheMetrics.Count > 0 ? cacheMetrics.Average(m => m.EvictionRate) : 0,
                    MemoryPressureLevel = DetermineMemoryPressureLevel(totalMemoryUsage)
                };
            }
        }

        /// <summary>
        /// Applies a cache optimization recommendation
        /// </summary>
        /// <param name="recommendation">The optimization recommendation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the optimization operation</returns>
        public async Task ApplyOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken = default)
        {
            if (recommendation.Type != TimerOptimizationType.Cache)
                throw new ArgumentException("Optimization type must be Cache", nameof(recommendation));

            try
            {
                _logger.LogDebug("Applying cache optimization: {Description}", recommendation.Description);

                switch (recommendation.Priority)
                {
                    case TimerOptimizationPriority.Critical:
                        await ApplyCriticalCacheOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.High:
                        await ApplyHighCacheOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.Medium:
                        await ApplyMediumCacheOptimizationAsync(recommendation, cancellationToken);
                        break;

                    case TimerOptimizationPriority.Low:
                        await ApplyLowCacheOptimizationAsync(recommendation, cancellationToken);
                        break;
                }

                Interlocked.Increment(ref _cacheOptimizationsPerformed);

                _logger.LogDebug("Cache optimization applied: {Description}", recommendation.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply cache optimization: {Description}", recommendation.Description);
            }
        }

        /// <summary>
        /// Optimizes all caches based on current conditions
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the optimization operation</returns>
        public async Task OptimizeAllCachesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var metrics = GetMetrics();
                var optimizations = new List<TimerCacheOptimization>();

                foreach (var cache in _caches.Values)
                {
                    var cacheOptimization = await AnalyzeCacheForOptimizationAsync(cache, metrics, cancellationToken);
                    if (cacheOptimization != null)
                    {
                        optimizations.Add(cacheOptimization);
                    }
                }

                foreach (var optimization in optimizations)
                {
                    await ApplyCacheOptimizationAsync(optimization, cancellationToken);
                }

                if (optimizations.Count > 0)
                {
                    RecordOptimizationEvent($"Applied {optimizations.Count} cache optimizations", true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing all caches");
            }
        }

        /// <summary>
        /// Resets cache manager statistics
        /// </summary>
        public void ResetStatistics()
        {
            lock (_lock)
            {
                Interlocked.Exchange(ref _totalCacheHits, 0);
                Interlocked.Exchange(ref _totalCacheMisses, 0);
                Interlocked.Exchange(ref _totalCacheItems, 0);
                Interlocked.Exchange(ref _totalCacheEvictions, 0);
                Interlocked.Exchange(ref _cacheOptimizationsPerformed, 0);
                _overallHitRate = 0;

                _optimizationHistory.Clear();

                foreach (var cache in _caches.Values)
                {
                    cache.ResetStatistics();
                }

                _logger.LogDebug("Cache manager statistics reset");
            }
        }

        #region Private Methods

        private async Task InitializeDefaultCachesAsync(CancellationToken cancellationToken)
        {
            // Create default caches for timer operations
            CreateCache("TimerExecution", 100, new TimerCacheExpirationPolicy { DefaultExpiration = TimeSpan.FromMinutes(5) });
            CreateCache("LicenseStatus", 50, new TimerCacheExpirationPolicy { DefaultExpiration = TimeSpan.FromMinutes(1) });
            CreateCache("FeatureInfo", 200, new TimerCacheExpirationPolicy { DefaultExpiration = TimeSpan.FromMinutes(10) });
            CreateCache("UserData", 500, new TimerCacheExpirationPolicy { DefaultExpiration = TimeSpan.FromMinutes(30) });
            CreateCache("Metrics", 1000, new TimerCacheExpirationPolicy { DefaultExpiration = TimeSpan.FromSeconds(30) });

            _logger.LogDebug("Initialized {Count} default caches", _caches.Count);
        }

        private async Task ClearAllCachesAsync()
        {
            var cacheNames = _caches.Keys.ToArray();
            foreach (var cacheName in cacheNames)
            {
                RemoveCache(cacheName);
            }

            _logger.LogDebug("Cleared all caches");
        }

        private void OnMonitorTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _ = Task.Run(() => MonitorCachesAsync(_cancellationTokenSource.Token));
        }

        private void OnCleanupTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _ = Task.Run(() => CleanupCachesAsync(_cancellationTokenSource.Token));
        }

        private async Task MonitorCachesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var metrics = GetMetrics();
                var shouldOptimize = ShouldOptimizeCaches(metrics);

                if (shouldOptimize)
                {
                    await OptimizeAllCachesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cache monitoring");
            }
        }

        private async Task CleanupCachesAsync(CancellationToken cancellationToken)
        {
            try
            {
                foreach (var cache in _caches.Values.ToList())
                {
                    await cache.CleanupExpiredItemsAsync(cancellationToken);
                }

                // Update total items count
                UpdateTotalItemsCount();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cache cleanup");
            }
        }

        private bool ShouldOptimizeCaches(TimerCacheMetrics metrics)
        {
            // Check if we need to optimize based on various metrics
            var hitRate = metrics.OverallHitRate;
            var memoryPressure = metrics.MemoryPressureLevel;
            var evictionRate = metrics.AverageEvictionRate;

            // Optimize if hit rate is too low
            if (hitRate < 50 && metrics.TotalCacheItems > 0)
                return true;

            // Optimize if memory pressure is high
            if (memoryPressure >= TimerCacheMemoryPressureLevel.High)
                return true;

            // Optimize if eviction rate is too high
            if (evictionRate > 20)
                return true;

            return false;
        }

        private async Task<TimerCacheOptimization> AnalyzeCacheForOptimizationAsync(TimerCache cache, TimerCacheMetrics metrics, CancellationToken cancellationToken)
        {
            try
            {
                var cacheMetrics = cache.GetMetrics();

                // Low hit rate optimization
                if (cacheMetrics.HitRate < 40 && cacheMetrics.Size > 10)
                {
                    return new TimerCacheOptimization
                    {
                        CacheName = cache.Name,
                        Type = TimerCacheOptimizationType.ReduceSize,
                        Priority = TimerOptimizationPriority.Medium,
                        Description = $"Low hit rate ({cacheMetrics.HitRate:F1}%) - reduce cache size",
                        Parameters = new Dictionary<string, object>
                        {
                            ["CurrentSize"] = cacheMetrics.Size,
                            ["TargetSize"] = Math.Max(10, cacheMetrics.Size / 2)
                        }
                    };
                }

                // High memory pressure optimization
                if (metrics.MemoryPressureLevel >= TimerCacheMemoryPressureLevel.High)
                {
                    return new TimerCacheOptimization
                    {
                        CacheName = cache.Name,
                        Type = TimerCacheOptimizationType.ClearExpired,
                        Priority = TimerOptimizationPriority.High,
                        Description = "High memory pressure - clear expired items",
                        Parameters = new Dictionary<string, object>
                        {
                            ["ForceCleanup"] = true
                        }
                    };
                }

                // High eviction rate optimization
                if (cacheMetrics.EvictionRate > 25)
                {
                    return new TimerCacheOptimization
                    {
                        CacheName = cache.Name,
                        Type = TimerCacheOptimizationType.AdjustExpiration,
                        Priority = TimerOptimizationPriority.Medium,
                        Description = $"High eviction rate ({cacheMetrics.EvictionRate:F1}%) - adjust expiration policy",
                        Parameters = new Dictionary<string, object>
                        {
                            ["CurrentExpiration"] = cache.ExpirationPolicy.DefaultExpiration.TotalMinutes,
                            ["TargetExpiration"] = cache.ExpirationPolicy.DefaultExpiration.TotalMinutes * 0.8
                        }
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing cache {CacheName} for optimization", cache.Name);
                return null;
            }
        }

        private async Task ApplyCacheOptimizationAsync(TimerCacheOptimization optimization, CancellationToken cancellationToken)
        {
            try
            {
                var cache = GetCache(optimization.CacheName);
                if (cache == null)
                {
                    _logger.LogWarning("Cache {CacheName} not found for optimization", optimization.CacheName);
                    return;
                }

                switch (optimization.Type)
                {
                    case TimerCacheOptimizationType.ReduceSize:
                        await ApplyReduceSizeOptimizationAsync(cache, optimization);
                        break;

                    case TimerCacheOptimizationType.ClearExpired:
                        await ApplyClearExpiredOptimizationAsync(cache, optimization);
                        break;

                    case TimerCacheOptimizationType.AdjustExpiration:
                        await ApplyAdjustExpirationOptimizationAsync(cache, optimization);
                        break;

                    case TimerCacheOptimizationType.ChangeEvictionPolicy:
                        await ApplyChangeEvictionPolicyOptimizationAsync(cache, optimization);
                        break;
                }

                // Raise optimization event
                var args = new TimerCacheEventArgs(cache.Name, cache.GetMetrics().HitRate);
                CacheOptimized?.Invoke(this, args);

                _logger.LogDebug("Applied cache optimization: {Type} for {CacheName}", optimization.Type, optimization.CacheName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply cache optimization for {CacheName}", optimization.CacheName);
            }
        }

        private async Task ApplyReduceSizeOptimizationAsync(TimerCache cache, TimerCacheOptimization optimization)
        {
            if (optimization.Parameters.TryGetValue("TargetSize", out var targetSizeObj) && targetSizeObj is int targetSize)
            {
                await cache.ReduceSizeAsync(targetSize);
            }
        }

        private async Task ApplyClearExpiredOptimizationAsync(TimerCache cache, TimerCacheOptimization optimization)
        {
            var forceCleanup = optimization.Parameters.TryGetValue("ForceCleanup", out var forceObj) && forceObj is bool forceBool && forceBool;
            await cache.ClearExpiredItemsAsync(forceCleanup);
        }

        private async Task ApplyAdjustExpirationOptimizationAsync(TimerCache cache, TimerCacheOptimization optimization)
        {
            if (optimization.Parameters.TryGetValue("TargetExpiration", out var targetExpirationObj) && targetExpirationObj is double targetMinutes)
            {
                cache.AdjustExpirationPolicy(TimeSpan.FromMinutes(targetMinutes));
            }
        }

        private async Task ApplyChangeEvictionPolicyOptimizationAsync(TimerCache cache, TimerCacheOptimization optimization)
        {
            // This would involve changing the eviction policy
            // For now, just log the optimization
            _logger.LogDebug("Would change eviction policy for cache {CacheName}", cache.Name);
        }

        private void OnCacheItemAccessed(object sender, TimerCacheItemEventArgs e)
        {
            if (e.Hit)
            {
                Interlocked.Increment(ref _totalCacheHits);
            }
            else
            {
                Interlocked.Increment(ref _totalCacheMisses);
            }

            UpdateOverallHitRate();
            CacheItemAccessed?.Invoke(this, e);
        }

        private void OnCacheItemEvicted(object sender, TimerCacheItemEventArgs e)
        {
            Interlocked.Increment(ref _totalCacheEvictions);
            CacheItemEvicted?.Invoke(this, e);
        }

        private void UpdateOverallHitRate()
        {
            var totalRequests = TotalCacheHits + TotalCacheMisses;
            if (totalRequests > 0)
            {
                _overallHitRate = (TotalCacheHits * 100.0) / totalRequests;
            }
        }

        private void UpdateTotalItemsCount()
        {
            var totalItems = _caches.Values.Sum(c => c.Count);
            Interlocked.Exchange(ref _totalCacheItems, totalItems);
        }

        private TimerCacheMemoryPressureLevel DetermineMemoryPressureLevel(long totalMemoryUsage)
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            var systemMemoryMB = _cacheOptions.SystemMemoryMB != 0 ? _cacheOptions.SystemMemoryMB : 8192;
            var memoryUsagePercent = (memoryMB * 100.0) / systemMemoryMB;

            if (memoryUsagePercent >= 90)
                return TimerCacheMemoryPressureLevel.Critical;
            else if (memoryUsagePercent >= 80)
                return TimerCacheMemoryPressureLevel.High;
            else if (memoryUsagePercent >= 70)
                return TimerCacheMemoryPressureLevel.Medium;
            else
                return TimerCacheMemoryPressureLevel.Low;
        }

        private void RecordOptimizationEvent(string description, bool success)
        {
            var optimizationEvent = new TimerCacheEvent(description, success, DateTime.UtcNow);

            lock (_lock)
            {
                _optimizationHistory.Enqueue(optimizationEvent);

                // Keep only recent history
                while (_optimizationHistory.Count > 50)
                {
                    _optimizationHistory.Dequeue();
                }
            }
        }

        private async Task ApplyCriticalCacheOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            // Clear all caches under critical conditions
            await ClearAllCachesAsync();
        }

        private async Task ApplyHighCacheOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            // Optimize all caches aggressively
            await OptimizeAllCachesAsync(cancellationToken);
        }

        private async Task ApplyMediumCacheOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            // Moderate optimization - clear expired items and reduce sizes
            foreach (var cache in _caches.Values)
            {
                await cache.ClearExpiredItemsAsync(true);
                await cache.ReduceSizeAsync(Math.Max(10, cache.MaxSize / 2));
            }
        }

        private async Task ApplyLowCacheOptimizationAsync(TimerOptimizationRecommendation recommendation, CancellationToken cancellationToken)
        {
            // Minimal optimization - just clear expired items
            foreach (var cache in _caches.Values)
            {
                await cache.ClearExpiredItemsAsync(false);
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
        /// Disposes the cache manager
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
                    _cleanupTimer?.Dispose();
                    _uptimeStopwatch?.Stop();

                    // Dispose caches
                    foreach (var cache in _caches.Values)
                    {
                        cache.Dispose();
                    }
                    _caches.Clear();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerCacheManager()
        {
            Dispose(false);
        }

        #endregion
    }
}