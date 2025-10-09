using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Collects detailed performance metrics for the timer execution system
    /// </summary>
    public class TimerMetricsCollector : IDisposable
    {
        private readonly ILogger _logger;
        private readonly TimerMetricsOptions _options;
        private readonly System.Threading.Timer _collectionTimer;
        private readonly Dictionary<string, TimerPerformanceSnapshot> _recentSnapshots;
        private readonly object _lock = new object();
        private readonly System.Diagnostics.PerformanceCounter _cpuCounter;
        private readonly System.Diagnostics.PerformanceCounter _memoryCounter;
        private DateTime _startTime;
        private bool _isDisposed;
        private bool _isCollecting;

        /// <summary>
        /// Occurs when metrics are collected
        /// </summary>
        public event EventHandler<TimerMetricsCollectedEventArgs>? MetricsCollected;

        /// <summary>
        /// Occurs when performance thresholds are exceeded
        /// </summary>
        public event EventHandler<TimerPerformanceThresholdExceededEventArgs>? ThresholdExceeded;

        /// <summary>
        /// Gets the current performance snapshot
        /// </summary>
        public TimerPerformanceSnapshot CurrentSnapshot { get; private set; }

        /// <summary>
        /// Gets the collection uptime
        /// </summary>
        public TimeSpan Uptime => DateTime.UtcNow - _startTime;

        /// <summary>
        /// Initializes a new instance of the TimerMetricsCollector class
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="options">The metrics collection options</param>
        public TimerMetricsCollector(ILogger logger, TimerMetricsOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _startTime = DateTime.UtcNow;
            _recentSnapshots = new Dictionary<string, TimerPerformanceSnapshot>();
            CurrentSnapshot = CreateSnapshot();

            _collectionTimer = new Timer(state => CollectMetricsAsync(), null, _options.CollectionIntervalMs, _options.CollectionIntervalMs);

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Performance counters not available, using alternative metrics collection");
                _cpuCounter = null;
                _memoryCounter = null;
            }
        }

        /// <summary>
        /// Starts collecting metrics
        /// </summary>
        public async Task StartAsync()
        {
            if (_isCollecting)
                return;

            _isCollecting = true;
            _startTime = DateTime.UtcNow;

            await CollectMetricsAsync(default);
            _logger.LogInformation("Metrics collector started with interval {Interval}ms", _options.CollectionIntervalMs);
        }

        /// <summary>
        /// Stops collecting metrics
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isCollecting)
                return;

            _isCollecting = false;
            _collectionTimer.Change(Timeout.Infinite, Timeout.Infinite);

            await CollectMetricsAsync(default);
            _logger.LogInformation("Metrics collector stopped after {Uptime}", Uptime);
        }

        /// <summary>
        /// Gets a performance snapshot by ID
        /// </summary>
        /// <param name="snapshotId">The snapshot ID</param>
        /// <returns>The performance snapshot, or null if not found</returns>
        public TimerPerformanceSnapshot? GetSnapshot(string snapshotId)
        {
            lock (_lock)
            {
                return _recentSnapshots.TryGetValue(snapshotId, out var snapshot) ? snapshot : null;
            }
        }

        /// <summary>
        /// Gets recent performance snapshots
        /// </summary>
        /// <param name="count">The number of snapshots to retrieve</param>
        /// <returns>Collection of recent snapshots</returns>
        public IEnumerable<TimerPerformanceSnapshot> GetRecentSnapshots(int count = 10)
        {
            lock (_lock)
            {
                return _recentSnapshots.Values.OrderByDescending(s => s.Timestamp).Take(count).ToList();
            }
        }

        /// <summary>
        /// Gets performance statistics for a time range
        /// </summary>
        /// <param name="startTime">The start time</param>
        /// <param name="endTime">The end time</param>
        /// <returns>Performance statistics</returns>
        public TimerPerformanceStatistics GetStatistics(DateTime startTime, DateTime endTime)
        {
            lock (_lock)
            {
                var snapshots = _recentSnapshots.Values
                    .Where(s => s.Timestamp >= startTime && s.Timestamp <= endTime)
                    .ToList();

                if (!snapshots.Any())
                    return new TimerPerformanceStatistics();

                return new TimerPerformanceStatistics
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    SnapshotCount = snapshots.Count,
                    AverageCpuUsage = snapshots.Average(s => s.CpuUsagePercent),
                    AverageMemoryUsage = snapshots.Average(s => s.MemoryUsagePercent),
                    AverageThreadCount = snapshots.Average(s => s.ThreadCount),
                    MaxCpuUsage = snapshots.Max(s => s.CpuUsagePercent),
                    MaxMemoryUsage = snapshots.Max(s => s.MemoryUsagePercent),
                    MaxThreadCount = snapshots.Max(s => s.ThreadCount),
                    MinCpuUsage = snapshots.Min(s => s.CpuUsagePercent),
                    MinMemoryUsage = snapshots.Min(s => s.MemoryUsagePercent),
                    MinThreadCount = snapshots.Min(s => s.ThreadCount),
                    PerformanceTrend = CalculatePerformanceTrend(snapshots)
                };
            }
        }

        /// <summary>
        /// Forces immediate metrics collection
        /// </summary>
        public async Task ForceCollectionAsync()
        {
            await CollectMetricsAsync(default);
        }

        /// <summary>
        /// Gets current system metrics
        /// </summary>
        /// <returns>System metrics</returns>
        public TimerSystemMetrics GetSystemMetrics()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();

            return new TimerSystemMetrics
            {
                CpuUsagePercent = cpuUsage,
                MemoryUsagePercent = memoryUsage,
                AvailableMemoryMB = GetAvailableMemoryMB(),
                TotalMemoryMB = GetTotalMemoryMB(),
                ActiveThreads = process.Threads.Count,
                ActiveTimerExecutions = GetActiveTimerExecutions(),
                Timestamp = DateTime.UtcNow,
                DiskUsagePercent = GetDiskUsagePercent(),
                NetworkIoRate = GetNetworkIoRate(),
                ProcessId = process.Id,
                ProcessStartTime = process.StartTime,
                WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                HandleCount = process.HandleCount
            };
        }

        public async Task<TimerMetrics> CollectMetricsAsync(CancellationToken cancellationToken = default)
        {
            if (!_isCollecting)
                return CreateEmptyMetrics();

            try
            {
                var snapshot = CreateSnapshot();
                CurrentSnapshot = snapshot;

                lock (_lock)
                {
                    _recentSnapshots[snapshot.SnapshotId] = snapshot;

                    // Keep only recent snapshots
                    while (_recentSnapshots.Count > _options.MaxSnapshotsToKeep)
                    {
                        var oldestKey = _recentSnapshots.OrderBy(kvp => kvp.Value.Timestamp).First().Key;
                        _recentSnapshots.Remove(oldestKey);
                    }
                }

                // Check thresholds
                CheckThresholds(snapshot);

                // Raise event
                MetricsCollected?.Invoke(this, new TimerMetricsCollectedEventArgs(snapshot));

                _logger.LogDebug("Metrics collected: CPU {CPU}%, Memory {Memory}%, Threads {Threads}",
                    snapshot.CpuUsagePercent, snapshot.MemoryUsagePercent, snapshot.ThreadCount);

                return ConvertSnapshotToMetrics(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
                return CreateEmptyMetrics();
            }
        }

        private TimerPerformanceSnapshot CreateSnapshot()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var snapshot = new TimerPerformanceSnapshot
            {
                SnapshotId = Guid.NewGuid().ToString("N")[..8],
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = GetCpuUsage(),
                MemoryUsagePercent = GetMemoryUsage(),
                ThreadCount = process.Threads.Count,
                WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                HandleCount = process.HandleCount,
                AvailableMemoryMB = GetAvailableMemoryMB(),
                DiskUsagePercent = GetDiskUsagePercent(),
                NetworkIoRate = GetNetworkIoRate(),
                ActiveTimerExecutions = GetActiveTimerExecutions(),
                GcCollectionCount0 = GC.CollectionCount(0),
                GcCollectionCount1 = GC.CollectionCount(1),
                GcCollectionCount2 = GC.CollectionCount(2),
                TotalMemoryAllocated = GC.GetTotalMemory(false),
                Uptime = Uptime
            };

            return snapshot;
        }

        private double GetCpuUsage()
        {
            if (_cpuCounter != null)
            {
                try
                {
                    return _cpuCounter.NextValue();
                }
                catch
                {
                    // Fallback to process CPU calculation
                }
            }

            // Fallback: calculate CPU usage from process
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var cpuTime = process.TotalProcessorTime.TotalMilliseconds;
            var totalTime = (DateTime.UtcNow - process.StartTime).TotalMilliseconds;

            return totalTime > 0 ? Math.Min(100, (cpuTime / totalTime / Environment.ProcessorCount) * 100) : 0;
        }

        private double GetMemoryUsage()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var totalMemory = GetTotalMemoryMB();

            return totalMemory > 0 ? (process.WorkingSet64 / (1024.0 * 1024.0) / totalMemory) * 100 : 0;
        }

        private long GetAvailableMemoryMB()
        {
            if (_memoryCounter != null)
            {
                try
                {
                    return (long)_memoryCounter.NextValue();
                }
                catch
                {
                    // Fallback to GC memory info
                }
            }

            // Fallback
            return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
        }

        private long GetTotalMemoryMB()
        {
            try
            {
                var gcInfo = GC.GetGCMemoryInfo();
                return gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            }
            catch
            {
                return 8192; // Default to 8GB
            }
        }

        private double GetDiskUsagePercent()
        {
            try
            {
                var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.CurrentDirectory));
                if (drive.IsReady)
                {
                    var totalSpace = drive.TotalSize;
                    var freeSpace = drive.AvailableFreeSpace;
                    return totalSpace > 0 ? ((totalSpace - freeSpace) * 100.0 / totalSpace) : 0;
                }
            }
            catch
            {
                // Ignore disk metrics if not available
            }

            return 0;
        }

        private double GetNetworkIoRate()
        {
            // Simplified network I/O calculation
            // In a real implementation, you would track network interface statistics
            return 0;
        }

        private int GetActiveTimerExecutions()
        {
            // This would need to be integrated with the actual timer execution tracking
            // For now, return a placeholder value
            return 0;
        }

        private void CheckThresholds(TimerPerformanceSnapshot snapshot)
        {
            var exceededThresholds = new List<TimerPerformanceThreshold>();

            if (snapshot.CpuUsagePercent > _options.CpuUsageThreshold)
            {
                exceededThresholds.Add(new TimerPerformanceThreshold
                {
                    MetricType = TimerPerformanceMetricType.CpuUsage,
                    ThresholdValue = _options.CpuUsageThreshold,
                    ActualValue = snapshot.CpuUsagePercent,
                    Severity = GetSeverity(_options.CpuUsageThreshold, snapshot.CpuUsagePercent)
                });
            }

            if (snapshot.MemoryUsagePercent > _options.MemoryUsageThreshold)
            {
                exceededThresholds.Add(new TimerPerformanceThreshold
                {
                    MetricType = TimerPerformanceMetricType.MemoryUsage,
                    ThresholdValue = _options.MemoryUsageThreshold,
                    ActualValue = snapshot.MemoryUsagePercent,
                    Severity = GetSeverity(_options.MemoryUsageThreshold, snapshot.MemoryUsagePercent)
                });
            }

            if (snapshot.ThreadCount > _options.ThreadCountThreshold)
            {
                exceededThresholds.Add(new TimerPerformanceThreshold
                {
                    MetricType = TimerPerformanceMetricType.ThreadCount,
                    ThresholdValue = _options.ThreadCountThreshold,
                    ActualValue = snapshot.ThreadCount,
                    Severity = GetSeverity(_options.ThreadCountThreshold, snapshot.ThreadCount)
                });
            }

            if (exceededThresholds.Any())
            {
                var args = new TimerPerformanceThresholdExceededEventArgs(snapshot, exceededThresholds);
                ThresholdExceeded?.Invoke(this, args);
                _logger.LogWarning("Performance thresholds exceeded: {Thresholds}",
                    string.Join(", ", exceededThresholds.Select(t => $"{t.MetricType}: {t.ActualValue:F1}%")));
            }
        }

        private TimerPerformanceThresholdSeverity GetSeverity(double threshold, double actual)
        {
            var ratio = actual / threshold;
            if (ratio >= 2.0) return TimerPerformanceThresholdSeverity.Critical;
            if (ratio >= 1.5) return TimerPerformanceThresholdSeverity.High;
            return TimerPerformanceThresholdSeverity.Medium;
        }

        private TimerPerformanceTrend CalculatePerformanceTrend(List<TimerPerformanceSnapshot> snapshots)
        {
            if (snapshots.Count < 2)
                return TimerPerformanceTrend.Stable;

            var recent = snapshots.Skip(Math.Max(0, snapshots.Count - 5)).ToList();
            var earlier = snapshots.Take(snapshots.Count - 5).ToList();

            if (!recent.Any() || !earlier.Any())
                return TimerPerformanceTrend.Stable;

            var recentCpu = recent.Average(s => s.CpuUsagePercent);
            var earlierCpu = earlier.Average(s => s.CpuUsagePercent);
            var recentMemory = recent.Average(s => s.MemoryUsagePercent);
            var earlierMemory = earlier.Average(s => s.MemoryUsagePercent);

            var cpuChange = recentCpu - earlierCpu;
            var memoryChange = recentMemory - earlierMemory;

            if (cpuChange > 10 || memoryChange > 10)
                return TimerPerformanceTrend.Degrading;
            if (cpuChange < -10 || memoryChange < -10)
                return TimerPerformanceTrend.Improving;
            return TimerPerformanceTrend.Stable;
        }

        private TimerMetrics ConvertSnapshotToMetrics(TimerPerformanceSnapshot snapshot)
        {
            return new TimerMetrics
            {
                CpuUsagePercent = snapshot.CpuUsagePercent,
                MemoryUsagePercent = snapshot.MemoryUsagePercent,
                ThreadCount = snapshot.ThreadCount,
                WorkingSetMB = snapshot.WorkingSetMB,
                PrivateMemoryMB = snapshot.PrivateMemoryMB,
                HandleCount = snapshot.HandleCount,
                Timestamp = snapshot.Timestamp,
                SnapshotId = snapshot.SnapshotId
            };
        }

        private TimerMetrics CreateEmptyMetrics()
        {
            return new TimerMetrics
            {
                CpuUsagePercent = 0,
                MemoryUsagePercent = 0,
                ThreadCount = 0,
                WorkingSetMB = 0,
                PrivateMemoryMB = 0,
                HandleCount = 0,
                Timestamp = DateTime.UtcNow,
                SnapshotId = Guid.NewGuid().ToString("N")[..8]
            };
        }

        /// <summary>
        /// Resets all collected statistics
        /// </summary>
        public void ResetStatistics()
        {
            lock (_lock)
            {
                _recentSnapshots.Clear();
                _startTime = DateTime.UtcNow;
                CurrentSnapshot = CreateSnapshot();

                _logger.LogDebug("Timer metrics collector statistics reset");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isCollecting = false;
            _collectionTimer?.Dispose();
            // PerformanceCounter doesn't implement IDisposable in .NET Framework 4.8
            // No need to dispose counters

            GC.SuppressFinalize(this);
        }
    }
}