using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Represents a cache with configurable expiration and eviction policies
    /// </summary>
    public class TimerCache : IDisposable
    {
        private readonly string _name;
        private readonly int _maxSize;
        private readonly object _lock = new object();
        private readonly Dictionary<string, TimerCacheItem> _items;
        private readonly Timer _expirationTimer;
        private long _hits;
        private long _misses;
        private long _evictions;
        private bool _isDisposed;

        /// <summary>
        /// Gets the name of the cache
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the maximum size of the cache
        /// </summary>
        public int MaxSize => _maxSize;

        /// <summary>
        /// Gets the current number of items in the cache
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _items.Count;
                }
            }
        }

        /// <summary>
        /// Gets the expiration policy for the cache
        /// </summary>
        public TimerCacheExpirationPolicy ExpirationPolicy { get; }

        /// <summary>
        /// Gets the eviction policy for the cache
        /// </summary>
        public TimerCacheEvictionPolicy EvictionPolicy { get; }

        /// <summary>
        /// Gets the number of cache hits
        /// </summary>
        public long Hits => Interlocked.Read(ref _hits);

        /// <summary>
        /// Gets the number of cache misses
        /// </summary>
        public long Misses => Interlocked.Read(ref _misses);

        /// <summary>
        /// Gets the number of cache evictions
        /// </summary>
        public long Evictions => Interlocked.Read(ref _evictions);

        /// <summary>
        /// Gets the hit rate as a percentage
        /// </summary>
        public double HitRate
        {
            get
            {
                var total = Hits + Misses;
                return total > 0 ? (Hits * 100.0) / total : 0;
            }
        }

        /// <summary>
        /// Gets the eviction rate as a percentage
        /// </summary>
        public double EvictionRate
        {
            get
            {
                var total = Hits + Misses;
                return total > 0 ? (Evictions * 100.0) / total : 0;
            }
        }

        /// <summary>
        /// Event raised when a cache item is accessed
        /// </summary>
        public event EventHandler<TimerCacheItemEventArgs> ItemAccessed;

        /// <summary>
        /// Event raised when a cache item is evicted
        /// </summary>
        public event EventHandler<TimerCacheItemEventArgs> ItemEvicted;

        /// <summary>
        /// Initializes a new instance of the TimerCache class
        /// </summary>
        /// <param name="name">Name of the cache</param>
        /// <param name="maxSize">Maximum size of the cache</param>
        /// <param name="expirationPolicy">Expiration policy</param>
        /// <param name="evictionPolicy">Eviction policy</param>
        public TimerCache(string name, int maxSize, TimerCacheExpirationPolicy expirationPolicy = null, TimerCacheEvictionPolicy evictionPolicy = null)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _maxSize = maxSize > 0 ? maxSize : throw new ArgumentException("Max size must be positive", nameof(maxSize));
            ExpirationPolicy = expirationPolicy ?? new TimerCacheExpirationPolicy();
            EvictionPolicy = evictionPolicy ?? new TimerCacheEvictionPolicy();
            _items = new Dictionary<string, TimerCacheItem>();
            _expirationTimer = new Timer(60000); // Check expiration every minute
            _expirationTimer.Elapsed += OnExpirationTimerElapsed;
            _expirationTimer.Start();
        }

        /// <summary>
        /// Gets a value from the cache
        /// </summary>
        /// <param name="key">The key to look up</param>
        /// <param name="value">The value if found</param>
        /// <returns>True if the value was found and is not expired</returns>
        public bool TryGet(string key, out object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            value = null;

            lock (_lock)
            {
                if (_isDisposed)
                    return false;

                if (_items.TryGetValue(key, out var item))
                {
                    if (item.IsExpired)
                    {
                        // Remove expired item
                        _items.Remove(key);
                        Interlocked.Increment(ref _evictions);
                        OnItemEvicted(key, item.Value, "Expired");
                        return false;
                    }

                    // Update access information
                    item.LastAccessed = DateTime.UtcNow;
                    item.AccessCount++;

                    value = item.Value;
                    Interlocked.Increment(ref _hits);

                    OnItemAccessed(key, true);
                    return true;
                }
            }

            Interlocked.Increment(ref _misses);
            OnItemAccessed(key, false);
            return false;
        }

        /// <summary>
        /// Sets a value in the cache
        /// </summary>
        /// <param name="key">The key to store</param>
        /// <param name="value">The value to store</param>
        /// <param name="expiration">Optional expiration time</param>
        public void Set(string key, object value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            lock (_lock)
            {
                if (_isDisposed)
                    return;

                var expirationTime = expiration ?? ExpirationPolicy.DefaultExpiration;
                var absoluteExpiration = DateTime.UtcNow.Add(expirationTime);

                // Check if we need to evict items
                if (_items.Count >= _maxSize && !_items.ContainsKey(key))
                {
                    EvictItems();
                }

                // Remove existing item if it exists
                if (_items.TryGetValue(key, out var existingItem))
                {
                    OnItemEvicted(key, existingItem.Value, "Replaced");
                }

                // Add or update the item
                var item = new TimerCacheItem
                {
                    Key = key,
                    Value = value,
                    Created = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    AbsoluteExpiration = absoluteExpiration,
                    AccessCount = 0
                };

                _items[key] = item;
            }
        }

        /// <summary>
        /// Removes a value from the cache
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <returns>True if the value was removed</returns>
        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            lock (_lock)
            {
                if (_isDisposed)
                    return false;

                if (_items.TryGetValue(key, out var item))
                {
                    _items.Remove(key);
                    OnItemEvicted(key, item.Value, "Removed");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a key exists in the cache
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key exists</returns>
        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            lock (_lock)
            {
                if (_isDisposed)
                    return false;

                return _items.ContainsKey(key);
            }
        }

        /// <summary>
        /// Gets all keys in the cache
        /// </summary>
        /// <returns>Collection of keys</returns>
        public IEnumerable<string> GetKeys()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return Enumerable.Empty<string>();

                return _items.Keys.ToArray();
            }
        }

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                var keys = _items.Keys.ToArray();
                foreach (var key in keys)
                {
                    if (_items.TryGetValue(key, out var item))
                    {
                        OnItemEvicted(key, item.Value, "Cleared");
                    }
                }

                _items.Clear();
            }
        }

        /// <summary>
        /// Gets cache metrics
        /// </summary>
        /// <returns>Cache metrics</returns>
        public TimerCacheItemMetrics GetMetrics()
        {
            lock (_lock)
            {
                var totalSize = _items.Values.Sum(item => EstimateItemSize(item));
                var expiredCount = _items.Values.Count(item => item.IsExpired);

                return new TimerCacheItemMetrics
                {
                    Name = _name,
                    Size = _items.Count,
                    MaxSize = _maxSize,
                    Hits = Hits,
                    Misses = Misses,
                    Evictions = Evictions,
                    HitRate = HitRate,
                    EvictionRate = EvictionRate,
                    MemoryUsageBytes = totalSize,
                    ExpiredItems = expiredCount,
                    AverageAccessCount = _items.Count > 0 ? _items.Values.Average(item => item.AccessCount) : 0
                };
            }
        }

        /// <summary>
        /// Reduces the cache size to the specified maximum
        /// </summary>
        /// <param name="newMaxSize">New maximum size</param>
        /// <returns>Task representing the operation</returns>
        public async Task ReduceSizeAsync(int newMaxSize)
        {
            if (newMaxSize < 0)
                throw new ArgumentException("New max size cannot be negative", nameof(newMaxSize));

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_isDisposed)
                        return;

                    if (newMaxSize < _items.Count)
                    {
                        EvictItems(_items.Count - newMaxSize);
                    }
                }
            });
        }

        /// <summary>
        /// Clears expired items from the cache
        /// </summary>
        /// <param name="forceCleanup">Whether to force cleanup of all items</param>
        /// <returns>Task representing the operation</returns>
        public async Task ClearExpiredItemsAsync(bool forceCleanup = false)
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_isDisposed)
                        return;

                    var expiredKeys = _items
                        .Where kvp => forceCleanup || kvp.Value.IsExpired)
                        .Select(kvp => kvp.Key)
                        .ToArray();

                    foreach (var key in expiredKeys)
                    {
                        if (_items.TryGetValue(key, out var item))
                        {
                            _items.Remove(key);
                            Interlocked.Increment(ref _evictions);
                            OnItemEvicted(key, item.Value, forceCleanup ? "Force cleanup" : "Expired");
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Adjusts the expiration policy
        /// </summary>
        /// <param name="newExpiration">New default expiration time</param>
        public void AdjustExpirationPolicy(TimeSpan newExpiration)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                ExpirationPolicy.DefaultExpiration = newExpiration;
            }
        }

        /// <summary>
        /// Resets cache statistics
        /// </summary>
        public void ResetStatistics()
        {
            lock (_lock)
            {
                Interlocked.Exchange(ref _hits, 0);
                Interlocked.Exchange(ref _misses, 0);
                Interlocked.Exchange(ref _evictions, 0);

                // Reset item access counts
                foreach (var item in _items.Values)
                {
                    item.AccessCount = 0;
                }
            }
        }

        private void EvictItems(int count = 1)
        {
            var itemsToEvict = SelectItemsForEviction(count);

            foreach (var item in itemsToEvict)
            {
                _items.Remove(item.Key);
                Interlocked.Increment(ref _evictions);
                OnItemEvicted(item.Key, item.Value, $"Evicted ({EvictionPolicy.Type})");
            }
        }

        private List<TimerCacheItem> SelectItemsForEviction(int count)
        {
            switch (EvictionPolicy.Type)
            {
                case TimerCacheEvictionType.LRU:
                    return _items.Values
                        .OrderBy(item => item.LastAccessed)
                        .Take(count)
                        .ToList();

                case TimerCacheEvictionType.LFU:
                    return _items.Values
                        .OrderBy(item => item.AccessCount)
                        .Take(count)
                        .ToList();

                case TimerCacheEvictionType.FIFO:
                    return _items.Values
                        .OrderBy(item => item.Created)
                        .Take(count)
                        .ToList();

                case TimerCacheEvictionType.Random:
                    return _items.Values
                        .OrderBy(_ => Guid.NewGuid())
                        .Take(count)
                        .ToList();

                default:
                    return _items.Values
                        .OrderBy(item => item.LastAccessed)
                        .Take(count)
                        .ToList();
            }
        }

        private void OnExpirationTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _ = ClearExpiredItemsAsync(false);
        }

        private void OnItemAccessed(string key, bool hit)
        {
            var args = new TimerCacheItemEventArgs(key, null, hit, "Accessed");
            ItemAccessed?.Invoke(this, args);
        }

        private void OnItemEvicted(string key, object value, string reason)
        {
            var args = new TimerCacheItemEventArgs(key, value, false, reason);
            ItemEvicted?.Invoke(this, args);
        }

        private long EstimateItemSize(TimerCacheItem item)
        {
            // Simple size estimation - in a real implementation, this would be more sophisticated
            var baseSize = 48; // Overhead for Dictionary entry, etc.
            var keySize = item.Key?.Length * 2 ?? 0;
            var valueSize = EstimateObjectSize(item.Value);
            return baseSize + keySize + valueSize;
        }

        private long EstimateObjectSize(object obj)
        {
            if (obj == null)
                return 0;

            // Simple size estimation for common types
            switch (obj)
            {
                case string s:
                    return s.Length * 2;
                case int _:
                case float _:
                case double _:
                    return 8;
                case bool _:
                case byte _:
                case sbyte _:
                case char _:
                    return 4;
                case DateTime _:
                case TimeSpan _:
                case Guid _:
                    return 16;
                default:
                    // For complex objects, use a rough estimate
                    return 64;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the cache
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Stop expiration timer
                    _expirationTimer?.Stop();
                    _expirationTimer?.Dispose();

                    // Clear all items
                    Clear();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerCache()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Represents an item in the cache
    /// </summary>
    public class TimerCacheItem
    {
        /// <summary>
        /// Gets or sets the key
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets when the item was created
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Gets or sets when the item was last accessed
        /// </summary>
        public DateTime LastAccessed { get; set; }

        /// <summary>
        /// Gets or sets the absolute expiration time
        /// </summary>
        public DateTime AbsoluteExpiration { get; set; }

        /// <summary>
        /// Gets or sets the access count
        /// </summary>
        public long AccessCount { get; set; }

        /// <summary>
        /// Gets whether the item is expired
        /// </summary>
        public bool IsExpired => DateTime.UtcNow >= AbsoluteExpiration;
    }

    /// <summary>
    /// Cache optimization types
    /// </summary>
    public enum TimerCacheOptimizationType
    {
        /// <summary>
        /// Reduce cache size
        /// </summary>
        ReduceSize,

        /// <summary>
        /// Clear expired items
        /// </summary>
        ClearExpired,

        /// <summary>
        /// Adjust expiration policy
        /// </summary>
        AdjustExpiration,

        /// <summary>
        /// Change eviction policy
        /// </summary>
        ChangeEvictionPolicy
    }

    /// <summary>
    /// Represents a cache optimization
    /// </summary>
    public class TimerCacheOptimization
    {
        /// <summary>
        /// Gets or sets the cache name
        /// </summary>
        public string CacheName { get; set; }

        /// <summary>
        /// Gets or sets the optimization type
        /// </summary>
        public TimerCacheOptimizationType Type { get; set; }

        /// <summary>
        /// Gets or sets the priority
        /// </summary>
        public TimerOptimizationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the optimization parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }
    }

    /// <summary>
    /// Represents a cache event
    /// </summary>
    public class TimerCacheEvent
    {
        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets whether the optimization was successful
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerCacheEvent class
        /// </summary>
        /// <param name="description">The description</param>
        /// <param name="success">Whether the optimization was successful</param>
        /// <param name="timestamp">The timestamp</param>
        public TimerCacheEvent(string description, bool success, DateTime timestamp)
        {
            Description = description;
            Success = success;
            Timestamp = timestamp;
        }
    }
}