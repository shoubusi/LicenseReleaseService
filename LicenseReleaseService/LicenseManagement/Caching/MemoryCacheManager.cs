using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.LicenseManagement.Caching
{
    /// <summary>
    /// Implements a thread-safe memory cache manager with comprehensive monitoring and statistics
    /// </summary>
    public class MemoryCacheManager : ICacheManager
    {
        private readonly ObjectCache _cache;
        private readonly CacheStatistics _statistics;
        private readonly Timer _cleanupTimer;
        private readonly Timer _statisticsTimer;
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// Gets or sets the cache options
        /// </summary>
        public CacheOptions Options { get; set; }

        /// <summary>
        /// Gets the current cache statistics
        /// </summary>
        public CacheStatistics Statistics => _statistics;

        /// <summary>
        /// Initializes a new instance of the MemoryCacheManager class
        /// </summary>
        public MemoryCacheManager() : this(new CacheOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the MemoryCacheManager class with custom options
        /// </summary>
        /// <param name="options">Cache options</param>
        public MemoryCacheManager(CacheOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));

            var validationErrors = options.Validate();
            if (validationErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid cache options: {string.Join(", ", validationErrors)}", nameof(options));
            }

            _cache = MemoryCache.Default;
            _statistics = new CacheStatistics();

            if (options.EnableBackgroundCleanup)
            {
                _cleanupTimer = new Timer(CleanupExpiredItems, null,
                    options.BackgroundCleanupInterval, options.BackgroundCleanupInterval);
            }

            if (options.EnableStatistics)
            {
                _statisticsTimer = new Timer(UpdateStatistics, null,
                    options.StatisticsUpdateInterval, options.StatisticsUpdateInterval);
            }
        }

        /// <summary>
        /// Gets a value from the cache
        /// </summary>
        /// <typeparam name="T">Type of the value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <returns>Cached value or default if not found</returns>
        public T Get<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));

            var stopwatch = Stopwatch.StartNew();
            var fullKey = GetFullKey(key);
            var cacheType = GetCacheType(key);

            try
            {
                var value = _cache.Get(fullKey);
                stopwatch.Stop();

                if (value != null)
                {
                    _statistics.RecordHit(key, stopwatch.Elapsed, cacheType);
                    return (T)value;
                }

                _statistics.RecordMiss(key, stopwatch.Elapsed, cacheType);
                return default(T);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordMiss(key, stopwatch.Elapsed, cacheType);
                LogError($"Error getting value from cache for key '{key}': {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Gets a value from the cache asynchronously
        /// </summary>
        /// <typeparam name="T">Type of the value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached value or default if not found</returns>
        public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Get<T>(key), cancellationToken);
        }

        /// <summary>
        /// Sets a value in the cache with default expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        public void Set<T>(string key, T value)
        {
            Set(key, value, Options.DefaultExpiration);
        }

        /// <summary>
        /// Sets a value in the cache with specified expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Expiration time</param>
        public void Set<T>(string key, T value, TimeSpan expiration)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (expiration <= TimeSpan.Zero)
                throw new ArgumentException("Expiration must be positive", nameof(expiration));

            var fullKey = GetFullKey(key);
            var cacheType = GetCacheType(key);
            var sizeBytes = CalculateSize(value);

            try
            {
                var policy = CreateCachePolicy(expiration, key);
                var existingValue = _cache.Get(fullKey);

                _cache.Set(fullKey, value, policy);

                if (existingValue == null)
                {
                    _statistics.RecordAddition(key, sizeBytes, cacheType);
                }
                else
                {
                    var existingSize = CalculateSize(existingValue);
                    _statistics.RecordRemoval(key, existingSize, cacheType);
                    _statistics.RecordAddition(key, sizeBytes, cacheType);
                }

                if (Options.EnableDetailedLogging)
                {
                    LogInfo($"Set value in cache for key '{key}' with expiration {expiration}");
                }

                CheckMemoryPressure();
            }
            catch (Exception ex)
            {
                LogError($"Error setting value in cache for key '{key}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sets a value in the cache asynchronously with default expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Set(key, value), cancellationToken);
        }

        /// <summary>
        /// Sets a value in the cache asynchronously with specified expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Expiration time</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Set(key, value, expiration), cancellationToken);
        }

        /// <summary>
        /// Removes a value from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True if the value was removed, false if it didn't exist</returns>
        public bool Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));

            var fullKey = GetFullKey(key);
            var cacheType = GetCacheType(key);

            try
            {
                var value = _cache.Get(fullKey);
                var removed = _cache.Remove(fullKey);

                if (removed != null && value != null)
                {
                    var sizeBytes = CalculateSize(value);
                    _statistics.RecordRemoval(key, sizeBytes, cacheType);
                }

                if (Options.EnableDetailedLogging)
                {
                    LogInfo($"Removed value from cache for key '{key}'");
                }

                return removed != null;
            }
            catch (Exception ex)
            {
                LogError($"Error removing value from cache for key '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes a value from the cache asynchronously
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the value was removed, false if it didn't exist</returns>
        public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Remove(key), cancellationToken);
        }

        /// <summary>
        /// Checks if a key exists in the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool Contains(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));

            var fullKey = GetFullKey(key);

            try
            {
                return _cache.Contains(fullKey);
            }
            catch (Exception ex)
            {
                LogError($"Error checking if key exists in cache for key '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a key exists in the cache asynchronously
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Contains(key), cancellationToken);
        }

        /// <summary>
        /// Gets a value from the cache or creates it using the factory function if not found
        /// </summary>
        /// <typeparam name="T">Type of the value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create the value if not found</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <returns>Cached or newly created value</returns>
        public T GetOrCreate<T>(string key, Func<T> factory, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var value = Get<T>(key);
            if (value != null)
                return value;

            var createdValue = factory();
            if (createdValue != null)
            {
                var exp = expiration ?? Options.DefaultExpiration;
                Set(key, createdValue, exp);
            }

            return createdValue;
        }

        /// <summary>
        /// Gets a value from the cache or creates it using the factory function if not found (async)
        /// </summary>
        /// <typeparam name="T">Type of the value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create the value if not found</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached or newly created value</returns>
        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var value = await GetAsync<T>(key, cancellationToken);
            if (value != null)
                return value;

            var createdValue = await factory();
            if (createdValue != null)
            {
                var exp = expiration ?? Options.DefaultExpiration;
                await SetAsync(key, createdValue, exp, cancellationToken);
            }

            return createdValue;
        }

        /// <summary>
        /// Gets all license information for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Dictionary of all license information</returns>
        public Dictionary<string, LicenseInfo> GetLicenseInfo(string server, int port)
        {
            var key = GenerateLicenseInfoKey(server, port);
            return Get<Dictionary<string, LicenseInfo>>(key);
        }

        /// <summary>
        /// Gets all license information for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of all license information</returns>
        public Task<Dictionary<string, LicenseInfo>> GetLicenseInfoAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var key = GenerateLicenseInfoKey(server, port);
            return GetAsync<Dictionary<string, LicenseInfo>>(key, cancellationToken);
        }

        /// <summary>
        /// Sets license information for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="licenseInfo">Dictionary of license information</param>
        /// <param name="expiration">Expiration time (optional)</param>
        public void SetLicenseInfo(string server, int port, Dictionary<string, LicenseInfo> licenseInfo, TimeSpan? expiration = null)
        {
            var key = GenerateLicenseInfoKey(server, port);
            var exp = expiration ?? Options.LicenseInfoExpiration;
            Set(key, licenseInfo, exp);
        }

        /// <summary>
        /// Sets license information for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="licenseInfo">Dictionary of license information</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task SetLicenseInfoAsync(string server, int port, Dictionary<string, LicenseInfo> licenseInfo, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var key = GenerateLicenseInfoKey(server, port);
            var exp = expiration ?? Options.LicenseInfoExpiration;
            return SetAsync(key, licenseInfo, exp, cancellationToken);
        }

        /// <summary>
        /// Gets license feature information for a specific server and feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <returns>License feature information</returns>
        public LicenseFeature GetLicenseFeature(string server, int port, string feature)
        {
            var key = GenerateLicenseFeatureKey(server, port, feature);
            return Get<LicenseFeature>(key);
        }

        /// <summary>
        /// Gets license feature information for a specific server and feature asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License feature information</returns>
        public Task<LicenseFeature> GetLicenseFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
        {
            var key = GenerateLicenseFeatureKey(server, port, feature);
            return GetAsync<LicenseFeature>(key, cancellationToken);
        }

        /// <summary>
        /// Sets license feature information for a specific server and feature
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="licenseFeature">License feature information</param>
        /// <param name="expiration">Expiration time (optional)</param>
        public void SetLicenseFeature(string server, int port, string feature, LicenseFeature licenseFeature, TimeSpan? expiration = null)
        {
            var key = GenerateLicenseFeatureKey(server, port, feature);
            var exp = expiration ?? Options.LicenseFeatureExpiration;
            Set(key, licenseFeature, exp);
        }

        /// <summary>
        /// Sets license feature information for a specific server and feature asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="licenseFeature">License feature information</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task SetLicenseFeatureAsync(string server, int port, string feature, LicenseFeature licenseFeature, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var key = GenerateLicenseFeatureKey(server, port, feature);
            var exp = expiration ?? Options.LicenseFeatureExpiration;
            return SetAsync(key, licenseFeature, exp, cancellationToken);
        }

        /// <summary>
        /// Gets license server status for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>License server status</returns>
        public LicenseServerStatus GetServerStatus(string server, int port)
        {
            var key = GenerateServerStatusKey(server, port);
            return Get<LicenseServerStatus>(key);
        }

        /// <summary>
        /// Gets license server status for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License server status</returns>
        public Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var key = GenerateServerStatusKey(server, port);
            return GetAsync<LicenseServerStatus>(key, cancellationToken);
        }

        /// <summary>
        /// Sets license server status for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="serverStatus">License server status</param>
        /// <param name="expiration">Expiration time (optional)</param>
        public void SetServerStatus(string server, int port, LicenseServerStatus serverStatus, TimeSpan? expiration = null)
        {
            var key = GenerateServerStatusKey(server, port);
            var exp = expiration ?? Options.ServerStatusExpiration;
            Set(key, serverStatus, exp);
        }

        /// <summary>
        /// Sets license server status for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="serverStatus">License server status</param>
        /// <param name="expiration">Expiration time (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task SetServerStatusAsync(string server, int port, LicenseServerStatus serverStatus, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var key = GenerateServerStatusKey(server, port);
            var exp = expiration ?? Options.ServerStatusExpiration;
            return SetAsync(key, serverStatus, exp, cancellationToken);
        }

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        public void Clear()
        {
            try
            {
                var keys = _cache.Select(kvp => kvp.Key).ToList();
                foreach (var key in keys)
                {
                    _cache.Remove(key);
                }

                if (Options.EnableDetailedLogging)
                {
                    LogInfo($"Cleared {keys.Count} items from cache");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error clearing cache: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Clears all items from the cache asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Clear(), cancellationToken);
        }

        /// <summary>
        /// Removes expired items from the cache
        /// </summary>
        /// <returns>Number of items removed</returns>
        public int RemoveExpired()
        {
            var removedCount = 0;
            var keys = _cache.Select(kvp => kvp.Key).ToList();

            foreach (var key in keys)
            {
                if (_cache.Contains(key))
                {
                    var policy = _cache.GetCacheItem(key)?.Policy;
                    if (policy?.AbsoluteExpiration.HasValue == true && policy.AbsoluteExpiration.Value <= DateTimeOffset.Now)
                    {
                        if (_cache.Remove(key))
                        {
                            removedCount++;
                            var simpleKey = GetSimpleKey(key);
                            var cacheType = GetCacheType(simpleKey);
                            _statistics.RecordExpiration(simpleKey, 0, cacheType);
                        }
                    }
                }
            }

            if (Options.EnableDetailedLogging && removedCount > 0)
            {
                LogInfo($"Removed {removedCount} expired items from cache");
            }

            return removedCount;
        }

        /// <summary>
        /// Removes expired items from the cache asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of items removed</returns>
        public Task<int> RemoveExpiredAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => RemoveExpired(), cancellationToken);
        }

        /// <summary>
        /// Generates a cache key for license information
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Cache key</returns>
        public string GenerateLicenseInfoKey(string server, int port)
        {
            return $"LicenseInfo_{server}_{port}";
        }

        /// <summary>
        /// Generates a cache key for license feature information
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <returns>Cache key</returns>
        public string GenerateLicenseFeatureKey(string server, int port, string feature)
        {
            return $"LicenseFeature_{server}_{port}_{feature}";
        }

        /// <summary>
        /// Generates a cache key for server status information
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>Cache key</returns>
        public string GenerateServerStatusKey(string server, int port)
        {
            return $"ServerStatus_{server}_{port}";
        }

        /// <summary>
        /// Invalidates all cache entries for a specific server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        public void InvalidateServer(string server, int port)
        {
            var keys = _cache.Select(kvp => kvp.Key).ToList();
            var serverPrefix = $"{server}_{port}";

            foreach (var key in keys)
            {
                if (key.Contains(serverPrefix))
                {
                    _cache.Remove(key);
                }
            }

            if (Options.EnableDetailedLogging)
            {
                LogInfo($"Invalidated all cache entries for server {server}:{port}");
            }
        }

        /// <summary>
        /// Invalidates all cache entries for a specific server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task InvalidateServerAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => InvalidateServer(server, port), cancellationToken);
        }

        /// <summary>
        /// Invalidates cache entries for a specific feature on a server
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        public void InvalidateFeature(string server, int port, string feature)
        {
            var licenseInfoKey = GenerateLicenseInfoKey(server, port);
            var licenseFeatureKey = GenerateLicenseFeatureKey(server, port, feature);

            _cache.Remove(licenseInfoKey);
            _cache.Remove(licenseFeatureKey);

            if (Options.EnableDetailedLogging)
            {
                LogInfo($"Invalidated cache entries for feature {feature} on server {server}:{port}");
            }
        }

        /// <summary>
        /// Invalidates cache entries for a specific feature on a server asynchronously
        /// </summary>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="feature">License feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task InvalidateFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => InvalidateFeature(server, port, feature), cancellationToken);
        }

        /// <summary>
        /// Disposes the cache manager
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cleanupTimer?.Dispose();
                    _statisticsTimer?.Dispose();
                }
                _disposed = true;
            }
        }

        private CacheItemPolicy CreateCachePolicy(TimeSpan expiration, string key)
        {
            var policy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.Now.Add(expiration),
                RemovedCallback = CacheItemRemoved
            };

            if (Options.EnableSlidingExpiration)
            {
                policy.SlidingExpiration = Options.SlidingExpirationWindow;
            }

            return policy;
        }

        private void CacheItemRemoved(CacheEntryRemovedArguments arguments)
        {
            var simpleKey = GetSimpleKey(arguments.CacheItem.Key);
            var cacheType = GetCacheType(simpleKey);
            var sizeBytes = CalculateSize(arguments.CacheItem.Value);

            switch (arguments.RemovedReason)
            {
                case CacheEntryRemovedReason.Expired:
                    _statistics.RecordExpiration(simpleKey, sizeBytes, cacheType);
                    break;
                case CacheEntryRemovedReason.Evicted:
                    _statistics.RecordEviction(simpleKey, sizeBytes, cacheType);
                    break;
                case CacheEntryRemovedReason.Removed:
                    _statistics.RecordRemoval(simpleKey, sizeBytes, cacheType);
                    break;
            }
        }

        private void CleanupExpiredItems(object state)
        {
            try
            {
                RemoveExpired();
            }
            catch (Exception ex)
            {
                LogError($"Error during cache cleanup: {ex.Message}");
            }
        }

        private void UpdateStatistics(object state)
        {
            try
            {
                _statistics.UpdateItemCount(_cache.GetCount());
            }
            catch (Exception ex)
            {
                LogError($"Error updating cache statistics: {ex.Message}");
            }
        }

        private void CheckMemoryPressure()
        {
            if (!Options.EnableMemoryPressureMonitoring)
                return;

            try
            {
                var memoryPressure = GetMemoryPressurePercentage();
                if (memoryPressure > Options.MemoryPressureThreshold)
                {
                    var keys = _cache.Select(kvp => kvp.Key).ToList();
                    var itemsToRemove = (int)(keys.Count * Options.MemoryPressureCleanupPercentage / 100.0);

                    for (int i = 0; i < itemsToRemove && i < keys.Count; i++)
                    {
                        _cache.Remove(keys[i]);
                    }

                    LogInfo($"Memory pressure cleanup: removed {itemsToRemove} items due to {memoryPressure:F1}% memory pressure");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error checking memory pressure: {ex.Message}");
            }
        }

        private double GetMemoryPressurePercentage()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                var usedMemory = process.WorkingSet64;
                return (double)usedMemory / totalMemory * 100;
            }
            catch
            {
                return 0;
            }
        }

        private string GetFullKey(string key)
        {
            return $"{Options.KeyPrefix}{key}";
        }

        private string GetSimpleKey(string fullKey)
        {
            return fullKey.StartsWith(Options.KeyPrefix)
                ? fullKey.Substring(Options.KeyPrefix.Length)
                : fullKey;
        }

        private string GetCacheType(string key)
        {
            if (key.StartsWith("LicenseInfo_"))
                return "LicenseInfo";
            if (key.StartsWith("LicenseFeature_"))
                return "LicenseFeature";
            if (key.StartsWith("ServerStatus_"))
                return "ServerStatus";
            return "default";
        }

        private long CalculateSize(object value)
        {
            if (value == null)
                return 0;

            try
            {
                if (value is string str)
                    return str.Length * sizeof(char);
                if (value is byte[] bytes)
                    return bytes.Length;
                if (value is Dictionary<string, LicenseInfo> licenseInfo)
                    return licenseInfo.Count * 500; // Estimate 500 bytes per license info
                if (value is LicenseFeature licenseFeature)
                    return 1000; // Estimate 1KB per license feature
                if (value is LicenseServerStatus serverStatus)
                    return 500; // Estimate 500 bytes per server status

                // Fallback estimate
                return 100;
            }
            catch
            {
                return 0;
            }
        }

        private void LogInfo(string message)
        {
            if (Options.EnableDetailedLogging)
            {
                System.Diagnostics.Debug.WriteLine($"[Cache] {message}");
            }
        }

        private void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[Cache Error] {message}");
        }
    }
}