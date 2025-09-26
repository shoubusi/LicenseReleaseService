using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace LicenseReleaseService.LicenseManagement.Caching
{
    /// <summary>
    /// Provides statistics and monitoring for cache operations
    /// </summary>
    public class CacheStatistics
    {
        private readonly object _lock = new object();
        private readonly Stopwatch _stopwatch;
        private long _totalRequests;
        private long _cacheHits;
        private long _cacheMisses;
        private long _totalItemsAdded;
        private long _totalItemsRemoved;
        private long _totalItemsEvicted;
        private long _totalExpiredItemsRemoved;
        private long _totalMemoryBytesUsed;
        private TimeSpan _totalCacheTime;
        private DateTime _lastResetTime;
        private DateTime _startTime;
        private readonly Dictionary<string, CacheTypeStatistics> _typeStatistics;
        private readonly Queue<CacheOperation> _recentOperations;
        private const int MaxRecentOperations = 1000;

        /// <summary>
        /// Gets the total number of cache requests
        /// </summary>
        public long TotalRequests
        {
            get
            {
                lock (_lock)
                {
                    return _totalRequests;
                }
            }
        }

        /// <summary>
        /// Gets the total number of cache hits
        /// </summary>
        public long CacheHits
        {
            get
            {
                lock (_lock)
                {
                    return _cacheHits;
                }
            }
        }

        /// <summary>
        /// Gets the total number of cache misses
        /// </summary>
        public long CacheMisses
        {
            get
            {
                lock (_lock)
                {
                    return _cacheMisses;
                }
            }
        }

        /// <summary>
        /// Gets the cache hit ratio (0.0 to 1.0)
        /// </summary>
        public double HitRatio
        {
            get
            {
                lock (_lock)
                {
                    return _totalRequests > 0 ? (double)_cacheHits / _totalRequests : 0.0;
                }
            }
        }

        /// <summary>
        /// Gets the total number of items added to the cache
        /// </summary>
        public long TotalItemsAdded
        {
            get
            {
                lock (_lock)
                {
                    return _totalItemsAdded;
                }
            }
        }

        /// <summary>
        /// Gets the total number of items removed from the cache
        /// </summary>
        public long TotalItemsRemoved
        {
            get
            {
                lock (_lock)
                {
                    return _totalItemsRemoved;
                }
            }
        }

        /// <summary>
        /// Gets the total number of items evicted from the cache
        /// </summary>
        public long TotalItemsEvicted
        {
            get
            {
                lock (_lock)
                {
                    return _totalItemsEvicted;
                }
            }
        }

        /// <summary>
        /// Gets the total number of expired items removed from the cache
        /// </summary>
        public long TotalExpiredItemsRemoved
        {
            get
            {
                lock (_lock)
                {
                    return _totalExpiredItemsRemoved;
                }
            }
        }

        /// <summary>
        /// Gets the estimated memory usage in bytes
        /// </summary>
        public long TotalMemoryBytesUsed
        {
            get
            {
                lock (_lock)
                {
                    return _totalMemoryBytesUsed;
                }
            }
        }

        /// <summary>
        /// Gets the average cache operation time
        /// </summary>
        public TimeSpan AverageOperationTime
        {
            get
            {
                lock (_lock)
                {
                    return _totalRequests > 0 ? TimeSpan.FromTicks(_totalCacheTime.Ticks / _totalRequests) : TimeSpan.Zero;
                }
            }
        }

        /// <summary>
        /// Gets the current number of items in the cache
        /// </summary>
        public int CurrentItemCount { get; set; }

        /// <summary>
        /// Gets the uptime of the cache
        /// </summary>
        public TimeSpan Uptime => _stopwatch.Elapsed;

        /// <summary>
        /// Gets the time when statistics were last reset
        /// </summary>
        public DateTime LastResetTime
        {
            get
            {
                lock (_lock)
                {
                    return _lastResetTime;
                }
            }
        }

        /// <summary>
        /// Gets the start time of the cache
        /// </summary>
        public DateTime StartTime => _startTime;

        /// <summary>
        /// Gets statistics by cache type
        /// </summary>
        public IReadOnlyDictionary<string, CacheTypeStatistics> TypeStatistics
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, CacheTypeStatistics>(_typeStatistics);
                }
            }
        }

        /// <summary>
        /// Gets recent cache operations
        /// </summary>
        public IReadOnlyList<CacheOperation> RecentOperations
        {
            get
            {
                lock (_lock)
                {
                    return _recentOperations.ToList();
                }
            }
        }

        /// <summary>
        /// Gets the requests per second
        /// </summary>
        public double RequestsPerSecond
        {
            get
            {
                lock (_lock)
                {
                    var elapsed = _stopwatch.Elapsed.TotalSeconds;
                    return elapsed > 0 ? _totalRequests / elapsed : 0.0;
                }
            }
        }

        /// <summary>
        /// Gets the hits per second
        /// </summary>
        public double HitsPerSecond
        {
            get
            {
                lock (_lock)
                {
                    var elapsed = _stopwatch.Elapsed.TotalSeconds;
                    return elapsed > 0 ? _cacheHits / elapsed : 0.0;
                }
            }
        }

        /// <summary>
        /// Gets the misses per second
        /// </summary>
        public double MissesPerSecond
        {
            get
            {
                lock (_lock)
                {
                    var elapsed = _stopwatch.Elapsed.TotalSeconds;
                    return elapsed > 0 ? _cacheMisses / elapsed : 0.0;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the CacheStatistics class
        /// </summary>
        public CacheStatistics()
        {
            _stopwatch = Stopwatch.StartNew();
            _startTime = DateTime.Now;
            _lastResetTime = _startTime;
            _typeStatistics = new Dictionary<string, CacheTypeStatistics>();
            _recentOperations = new Queue<CacheOperation>();
        }

        /// <summary>
        /// Records a cache hit
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="operationTime">Operation time</param>
        /// <param name="type">Cache type</param>
        public void RecordHit(string key, TimeSpan operationTime, string type = "default")
        {
            lock (_lock)
            {
                _totalRequests++;
                _cacheHits++;
                _totalCacheTime += operationTime;

                AddTypeStatistics(type, true, operationTime);
                AddRecentOperation(CacheOperationType.Hit, key, operationTime, true);
            }
        }

        /// <summary>
        /// Records a cache miss
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="operationTime">Operation time</param>
        /// <param name="type">Cache type</param>
        public void RecordMiss(string key, TimeSpan operationTime, string type = "default")
        {
            lock (_lock)
            {
                _totalRequests++;
                _cacheMisses++;
                _totalCacheTime += operationTime;

                AddTypeStatistics(type, false, operationTime);
                AddRecentOperation(CacheOperationType.Miss, key, operationTime, false);
            }
        }

        /// <summary>
        /// Records an item addition to the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="sizeBytes">Size in bytes</param>
        /// <param name="type">Cache type</param>
        public void RecordAddition(string key, long sizeBytes, string type = "default")
        {
            lock (_lock)
            {
                _totalItemsAdded++;
                _totalMemoryBytesUsed += sizeBytes;

                AddRecentOperation(CacheOperationType.Add, key, TimeSpan.Zero, true, sizeBytes);
            }
        }

        /// <summary>
        /// Records an item removal from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="sizeBytes">Size in bytes</param>
        /// <param name="type">Cache type</param>
        public void RecordRemoval(string key, long sizeBytes, string type = "default")
        {
            lock (_lock)
            {
                _totalItemsRemoved++;
                _totalMemoryBytesUsed = Math.Max(0, _totalMemoryBytesUsed - sizeBytes);

                AddRecentOperation(CacheOperationType.Remove, key, TimeSpan.Zero, true, sizeBytes);
            }
        }

        /// <summary>
        /// Records an item eviction from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="sizeBytes">Size in bytes</param>
        /// <param name="type">Cache type</param>
        public void RecordEviction(string key, long sizeBytes, string type = "default")
        {
            lock (_lock)
            {
                _totalItemsEvicted++;
                _totalMemoryBytesUsed = Math.Max(0, _totalMemoryBytesUsed - sizeBytes);

                AddRecentOperation(CacheOperationType.Evict, key, TimeSpan.Zero, true, sizeBytes);
            }
        }

        /// <summary>
        /// Records an expired item removal from the cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="sizeBytes">Size in bytes</param>
        /// <param name="type">Cache type</param>
        public void RecordExpiration(string key, long sizeBytes, string type = "default")
        {
            lock (_lock)
            {
                _totalExpiredItemsRemoved++;
                _totalMemoryBytesUsed = Math.Max(0, _totalMemoryBytesUsed - sizeBytes);

                AddRecentOperation(CacheOperationType.Expire, key, TimeSpan.Zero, true, sizeBytes);
            }
        }

        /// <summary>
        /// Updates the current item count
        /// </summary>
        /// <param name="count">Current item count</param>
        public void UpdateItemCount(int count)
        {
            lock (_lock)
            {
                CurrentItemCount = count;
            }
        }

        /// <summary>
        /// Resets all statistics
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _totalRequests = 0;
                _cacheHits = 0;
                _cacheMisses = 0;
                _totalItemsAdded = 0;
                _totalItemsRemoved = 0;
                _totalItemsEvicted = 0;
                _totalExpiredItemsRemoved = 0;
                _totalMemoryBytesUsed = 0;
                _totalCacheTime = TimeSpan.Zero;
                _lastResetTime = DateTime.Now;
                _typeStatistics.Clear();
                _recentOperations.Clear();
            }
        }

        /// <summary>
        /// Gets a summary of cache statistics
        /// </summary>
        /// <returns>Statistics summary</returns>
        public CacheStatisticsSummary GetSummary()
        {
            lock (_lock)
            {
                return new CacheStatisticsSummary
                {
                    TotalRequests = _totalRequests,
                    CacheHits = _cacheHits,
                    CacheMisses = _cacheMisses,
                    HitRatio = HitRatio,
                    TotalItemsAdded = _totalItemsAdded,
                    TotalItemsRemoved = _totalItemsRemoved,
                    TotalItemsEvicted = _totalItemsEvicted,
                    TotalExpiredItemsRemoved = _totalExpiredItemsRemoved,
                    TotalMemoryBytesUsed = _totalMemoryBytesUsed,
                    AverageOperationTime = AverageOperationTime,
                    CurrentItemCount = CurrentItemCount,
                    Uptime = Uptime,
                    RequestsPerSecond = RequestsPerSecond,
                    HitsPerSecond = HitsPerSecond,
                    MissesPerSecond = MissesPerSecond,
                    TypeStatistics = new Dictionary<string, CacheTypeStatistics>(_typeStatistics)
                };
            }
        }

        private void AddTypeStatistics(string type, bool isHit, TimeSpan operationTime)
        {
            if (!_typeStatistics.ContainsKey(type))
            {
                _typeStatistics[type] = new CacheTypeStatistics();
            }

            var typeStats = _typeStatistics[type];
            typeStats.TotalRequests++;

            if (isHit)
            {
                typeStats.CacheHits++;
            }
            else
            {
                typeStats.CacheMisses++;
            }

            typeStats.TotalOperationTime += operationTime;
        }

        private void AddRecentOperation(CacheOperationType operationType, string key, TimeSpan operationTime, bool success, long sizeBytes = 0)
        {
            var operation = new CacheOperation
            {
                OperationType = operationType,
                Key = key,
                Timestamp = DateTime.Now,
                OperationTime = operationTime,
                Success = success,
                SizeBytes = sizeBytes
            };

            _recentOperations.Enqueue(operation);

            while (_recentOperations.Count > MaxRecentOperations)
            {
                _recentOperations.Dequeue();
            }
        }
    }

    /// <summary>
    /// Represents statistics for a specific cache type
    /// </summary>
    public class CacheTypeStatistics
    {
        /// <summary>
        /// Gets or sets the total requests for this type
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the cache hits for this type
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Gets or sets the cache misses for this type
        /// </summary>
        public long CacheMisses { get; set; }

        /// <summary>
        /// Gets the hit ratio for this type
        /// </summary>
        public double HitRatio => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0.0;

        /// <summary>
        /// Gets or sets the total operation time for this type
        /// </summary>
        public TimeSpan TotalOperationTime { get; set; }

        /// <summary>
        /// Gets the average operation time for this type
        /// </summary>
        public TimeSpan AverageOperationTime => TotalRequests > 0 ? TimeSpan.FromTicks(TotalOperationTime.Ticks / TotalRequests) : TimeSpan.Zero;
    }

    /// <summary>
    /// Represents a summary of cache statistics
    /// </summary>
    public class CacheStatisticsSummary
    {
        /// <summary>
        /// Gets or sets the total requests
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the cache hits
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Gets or sets the cache misses
        /// </summary>
        public long CacheMisses { get; set; }

        /// <summary>
        /// Gets or sets the hit ratio
        /// </summary>
        public double HitRatio { get; set; }

        /// <summary>
        /// Gets or sets the total items added
        /// </summary>
        public long TotalItemsAdded { get; set; }

        /// <summary>
        /// Gets or sets the total items removed
        /// </summary>
        public long TotalItemsRemoved { get; set; }

        /// <summary>
        /// Gets or sets the total items evicted
        /// </summary>
        public long TotalItemsEvicted { get; set; }

        /// <summary>
        /// Gets or sets the total expired items removed
        /// </summary>
        public long TotalExpiredItemsRemoved { get; set; }

        /// <summary>
        /// Gets or sets the total memory bytes used
        /// </summary>
        public long TotalMemoryBytesUsed { get; set; }

        /// <summary>
        /// Gets or sets the average operation time
        /// </summary>
        public TimeSpan AverageOperationTime { get; set; }

        /// <summary>
        /// Gets or sets the current item count
        /// </summary>
        public int CurrentItemCount { get; set; }

        /// <summary>
        /// Gets or sets the uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets or sets the requests per second
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the hits per second
        /// </summary>
        public double HitsPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the misses per second
        /// </summary>
        public double MissesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the type-specific statistics
        /// </summary>
        public Dictionary<string, CacheTypeStatistics> TypeStatistics { get; set; }
    }

    /// <summary>
    /// Represents a cache operation
    /// </summary>
    public class CacheOperation
    {
        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public CacheOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the cache key
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the operation duration
        /// </summary>
        public TimeSpan OperationTime { get; set; }

        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the size in bytes (if applicable)
        /// </summary>
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Defines the types of cache operations
    /// </summary>
    public enum CacheOperationType
    {
        /// <summary>
        /// Cache hit operation
        /// </summary>
        Hit,

        /// <summary>
        /// Cache miss operation
        /// </summary>
        Miss,

        /// <summary>
        /// Add item operation
        /// </summary>
        Add,

        /// <summary>
        /// Remove item operation
        /// </summary>
        Remove,

        /// <summary>
        /// Evict item operation
        /// </summary>
        Evict,

        /// <summary>
        /// Expire item operation
        /// </summary>
        Expire
    }
}