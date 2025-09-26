using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for cache events
    /// </summary>
    public class TimerCacheEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the cache name
        /// </summary>
        public string CacheName { get; }

        /// <summary>
        /// Gets the hit rate
        /// </summary>
        public double HitRate { get; }

        /// <summary>
        /// Gets the timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerCacheEventArgs class
        /// </summary>
        /// <param name="cacheName">The cache name</param>
        /// <param name="hitRate">The hit rate</param>
        public TimerCacheEventArgs(string cacheName, double hitRate)
        {
            CacheName = cacheName;
            HitRate = hitRate;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for cache item events
    /// </summary>
    public class TimerCacheItemEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the item key
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the item value
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets whether the access was a hit
        /// </summary>
        public bool Hit { get; }

        /// <summary>
        /// Gets the event description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerCacheItemEventArgs class
        /// </summary>
        /// <param name="key">The item key</param>
        /// <param name="value">The item value</param>
        /// <param name="hit">Whether the access was a hit</param>
        /// <param name="description">The event description</param>
        public TimerCacheItemEventArgs(string key, object value, bool hit, string description)
        {
            Key = key;
            Value = value;
            Hit = hit;
            Description = description;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Cache metrics
    /// </summary>
    public class TimerCacheMetrics
    {
        /// <summary>
        /// Gets the uptime of the cache manager
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the total number of caches
        /// </summary>
        public int TotalCaches { get; set; }

        /// <summary>
        /// Gets the total number of cache items
        /// </summary>
        public long TotalCacheItems { get; set; }

        /// <summary>
        /// Gets the total number of cache hits
        /// </summary>
        public long TotalCacheHits { get; set; }

        /// <summary>
        /// Gets the total number of cache misses
        /// </summary>
        public long TotalCacheMisses { get; set; }

        /// <summary>
        /// Gets the total number of cache evictions
        /// </summary>
        public long TotalCacheEvictions { get; set; }

        /// <summary>
        /// Gets the overall hit rate
        /// </summary>
        public double OverallHitRate { get; set; }

        /// <summary>
        /// Gets the total cache size
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Gets the total memory usage in bytes
        /// </summary>
        public long TotalMemoryUsage { get; set; }

        /// <summary>
        /// Gets the number of cache optimizations performed
        /// </summary>
        public long CacheOptimizationsPerformed { get; set; }

        /// <summary>
        /// Gets the average hit rate across all caches
        /// </summary>
        public double AverageHitRate { get; set; }

        /// <summary>
        /// Gets the average eviction rate across all caches
        /// </summary>
        public double AverageEvictionRate { get; set; }

        /// <summary>
        /// Gets the current memory pressure level
        /// </summary>
        public TimerCacheMemoryPressureLevel MemoryPressureLevel { get; set; }

        /// <summary>
        /// Gets the overall success rate as a percentage
        /// </summary>
        public double SuccessRate => TotalCacheHits + TotalCacheMisses > 0 ? (TotalCacheHits * 100.0 / (TotalCacheHits + TotalCacheMisses)) : 0;

        /// <summary>
        /// Gets the cache operations per minute
        /// </summary>
        public double OperationsPerMinute => Uptime.TotalMinutes > 0 ? (TotalCacheHits + TotalCacheMisses) / Uptime.TotalMinutes : 0;

        /// <summary>
        /// Initializes a new instance of the TimerCacheMetrics class
        /// </summary>
        public TimerCacheMetrics()
        {
        }

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Cache Metrics:");
            builder.AppendLine($"  Uptime: {Uptime}");
            builder.AppendLine($"  Total Caches: {TotalCaches}");
            builder.AppendLine($"  Total Items: {TotalCacheItems}");
            builder.AppendLine($"  Total Hits: {TotalCacheHits}");
            builder.AppendLine($"  Total Misses: {TotalCacheMisses}");
            builder.AppendLine($"  Total Evictions: {TotalCacheEvictions}");
            builder.AppendLine($"  Success Rate: {SuccessRate:F2}%");
            builder.AppendLine($"  Overall Hit Rate: {OverallHitRate:F2}%");
            builder.AppendLine($"  Average Hit Rate: {AverageHitRate:F2}%");
            builder.AppendLine($"  Average Eviction Rate: {AverageEvictionRate:F2}%");
            builder.AppendLine($"  Operations/Minute: {OperationsPerMinute:F2}");
            builder.AppendLine($"  Total Memory Usage: {TotalMemoryUsage / (1024 * 1024):F2}MB");
            builder.AppendLine($"  Memory Pressure: {MemoryPressureLevel}");
            builder.AppendLine($"  Optimizations Performed: {CacheOptimizationsPerformed}");

            return builder.ToString();
        }
    }

    /// <summary>
    /// Individual cache item metrics
    /// </summary>
    public class TimerCacheItemMetrics
    {
        /// <summary>
        /// Gets the cache name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the current size of the cache
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Gets the maximum size of the cache
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Gets the number of hits
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Gets the number of misses
        /// </summary>
        public long Misses { get; set; }

        /// <summary>
        /// Gets the number of evictions
        /// </summary>
        public long Evictions { get; set; }

        /// <summary>
        /// Gets the hit rate
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Gets the eviction rate
        /// </summary>
        public double EvictionRate { get; set; }

        /// <summary>
        /// Gets the memory usage in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Gets the number of expired items
        /// </summary>
        public int ExpiredItems { get; set; }

        /// <summary>
        /// Gets the average access count
        /// </summary>
        public double AverageAccessCount { get; set; }

        /// <summary>
        /// Gets the fill rate as a percentage
        /// </summary>
        public double FillRate => MaxSize > 0 ? (Size * 100.0 / MaxSize) : 0;

        /// <summary>
        /// Initializes a new instance of the TimerCacheItemMetrics class
        /// </summary>
        public TimerCacheItemMetrics()
        {
        }

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"Cache '{Name}' Metrics:");
            builder.AppendLine($"  Size: {Size}/{MaxSize} ({FillRate:F2}% full)");
            builder.AppendLine($"  Hits: {Hits}, Misses: {Misses}, Evictions: {Evictions}");
            builder.AppendLine($"  Hit Rate: {HitRate:F2}%, Eviction Rate: {EvictionRate:F2}%");
            builder.AppendLine($"  Memory Usage: {MemoryUsageBytes / 1024:F2}KB");
            builder.AppendLine($"  Expired Items: {ExpiredItems}");
            builder.AppendLine($"  Average Access Count: {AverageAccessCount:F2}");

            return builder.ToString();
        }
    }

    /// <summary>
    /// System metrics for performance monitoring
    /// </summary>
    public class TimerSystemMetrics
    {
        /// <summary>
        /// Gets the CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Gets the memory usage percentage
        /// </summary>
        public double MemoryUsagePercent { get; set; }

        /// <summary>
        /// Gets the available memory in MB
        /// </summary>
        public long AvailableMemoryMB { get; set; }

        /// <summary>
        /// Gets the total memory in MB
        /// </summary>
        public long TotalMemoryMB { get; set; }

        /// <summary>
        /// Gets the number of active threads
        /// </summary>
        public int ActiveThreads { get; set; }

        /// <summary>
        /// Gets the number of active timer executions
        /// </summary>
        public int ActiveTimerExecutions { get; set; }

        /// <summary>
        /// Gets the timestamp when metrics were collected
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the disk usage percentage
        /// </summary>
        public double DiskUsagePercent { get; set; }

        /// <summary>
        /// Gets the network I/O rate in KB/s
        /// </summary>
        public double NetworkIoRate { get; set; }

        /// <summary>
        /// Gets the process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets the process start time
        /// </summary>
        public DateTime ProcessStartTime { get; set; }

        /// <summary>
        /// Gets the working set memory in MB
        /// </summary>
        public long WorkingSetMB { get; set; }

        /// <summary>
        /// Gets the private memory in MB
        /// </summary>
        public long PrivateMemoryMB { get; set; }

        /// <summary>
        /// Gets the handle count
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerSystemMetrics class
        /// </summary>
        public TimerSystemMetrics()
        {
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("System Metrics:");
            builder.AppendLine($"  CPU Usage: {CpuUsagePercent:F2}%");
            builder.AppendLine($"  Memory Usage: {MemoryUsagePercent:F2}%");
            builder.AppendLine($"  Available Memory: {AvailableMemoryMB}MB / {TotalMemoryMB}MB");
            builder.AppendLine($"  Working Set: {WorkingSetMB}MB");
            builder.AppendLine($"  Private Memory: {PrivateMemoryMB}MB");
            builder.AppendLine($"  Active Threads: {ActiveThreads}");
            builder.AppendLine($"  Handle Count: {HandleCount}");
            builder.AppendLine($"  Active Timer Executions: {ActiveTimerExecutions}");
            builder.AppendLine($"  Disk Usage: {DiskUsagePercent:F2}%");
            builder.AppendLine($"  Network I/O: {NetworkIoRate:F2}KB/s");
            builder.AppendLine($"  Process ID: {ProcessId}");
            builder.AppendLine($"  Process Start Time: {ProcessStartTime}");
            builder.AppendLine($"  Timestamp: {Timestamp}");

            return builder.ToString();
        }
    }

    /// <summary>
    /// Event arguments for system metrics events
    /// </summary>
    public class TimerSystemMetricsEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the system metrics
        /// </summary>
        public TimerSystemMetrics Metrics { get; }

        /// <summary>
        /// Gets the timestamp when the metrics were collected
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerSystemMetricsEventArgs class
        /// </summary>
        /// <param name="metrics">The system metrics</param>
        public TimerSystemMetricsEventArgs(TimerSystemMetrics metrics)
        {
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Tuning metrics for performance optimization
    /// </summary>
    public class TimerTuningMetrics
    {
        /// <summary>
        /// Gets the number of tuning cycles performed
        /// </summary>
        public long TuningCycles { get; set; }

        /// <summary>
        /// Gets the number of successful tunings
        /// </summary>
        public long SuccessfulTunings { get; set; }

        /// <summary>
        /// Gets the number of failed tunings
        /// </summary>
        public long FailedTunings { get; set; }

        /// <summary>
        /// Gets the average performance improvement
        /// </summary>
        public double AveragePerformanceImprovement { get; set; }

        /// <summary>
        /// Gets the last tuning timestamp
        /// </summary>
        public DateTime? LastTuningTime { get; set; }

        /// <summary>
        /// Gets the uptime of the tuner
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the tuning success rate
        /// </summary>
        public double SuccessRate => TuningCycles > 0 ? (SuccessfulTunings * 100.0 / TuningCycles) : 0;

        /// <summary>
        /// Gets the tunings per hour rate
        /// </summary>
        public double TuningsPerHour => Uptime.TotalHours > 0 ? TuningCycles / Uptime.TotalHours : 0;

        /// <summary>
        /// Initializes a new instance of the TimerTuningMetrics class
        /// </summary>
        public TimerTuningMetrics()
        {
        }

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Tuning Metrics:");
            builder.AppendLine($"  Tuning Cycles: {TuningCycles}");
            builder.AppendLine($"  Successful Tunings: {SuccessfulTunings}");
            builder.AppendLine($"  Failed Tunings: {FailedTunings}");
            builder.AppendLine($"  Success Rate: {SuccessRate:F2}%");
            builder.AppendLine($"  Average Performance Improvement: {AveragePerformanceImprovement:F2}%");
            builder.AppendLine($"  Tunings/Hour: {TuningsPerHour:F2}");
            builder.AppendLine($"  Uptime: {Uptime}");

            if (LastTuningTime.HasValue)
            {
                builder.AppendLine($"  Last Tuning: {LastTuningTime.Value}");
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Event arguments for performance tuning events
    /// </summary>
    public class TimerPerformanceTuningEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the tuning type
        /// </summary>
        public TimerOptimizationType TuningType { get; }

        /// <summary>
        /// Gets the priority
        /// </summary>
        public TimerOptimizationPriority Priority { get; }

        /// <summary>
        /// Gets the description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the performance improvement percentage
        /// </summary>
        public double PerformanceImprovement { get; set; }

        /// <summary>
        /// Gets the timestamp when the tuning was applied
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the parameters used for tuning
        /// </summary>
        public Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Initializes a new instance of the TimerPerformanceTuningEventArgs class
        /// </summary>
        /// <param name="tuningType">The tuning type</param>
        /// <param name="priority">The priority</param>
        /// <param name="description">The description</param>
        public TimerPerformanceTuningEventArgs(TimerOptimizationType tuningType, TimerOptimizationPriority priority, string description)
        {
            TuningType = tuningType;
            Priority = priority;
            Description = description;
            PerformanceImprovement = 0;
            Timestamp = DateTime.UtcNow;
            Parameters = new Dictionary<string, object>();
        }
    }
}