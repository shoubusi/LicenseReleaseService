using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService
{
    public class PerformanceCounters
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, PerformanceMetric> _counters;
        private readonly System.Threading.Timer _collectionTimer;
        private readonly TimeSpan _collectionInterval = TimeSpan.FromSeconds(30);
        private bool _isRunning;
        private readonly Queue<PerformanceSnapshot> _snapshotHistory;
        private readonly int _maxHistorySize = 1440; // 24 hours at 30-second intervals

        public PerformanceCounters()
        {
            _counters = new Dictionary<string, PerformanceMetric>();
            _snapshotHistory = new Queue<PerformanceSnapshot>();
            _collectionTimer = new Timer(CollectPerformanceData, null, Timeout.Infinite, (int)_collectionInterval.TotalMilliseconds);

            InitializeCounters();
        }

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
        }

        public Dictionary<string, double> CurrentValues
        {
            get
            {
                lock (_lock)
                {
                    var values = new Dictionary<string, double>();
                    foreach (var counter in _counters)
                    {
                        try
                        {
                            values[counter.Key] = counter.Value.CurrentValue;
                        }
                        catch
                        {
                            values[counter.Key] = 0;
                        }
                    }
                    return values;
                }
            }
        }

        public List<PerformanceSnapshot> GetSnapshotHistory(int count = 10)
        {
            lock (_lock)
            {
                return new List<PerformanceSnapshot>(_snapshotHistory.ToArray()).Take(count).ToList();
            }
        }

        public PerformanceMetrics GetCurrentMetrics()
        {
            lock (_lock)
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var snapshot = new PerformanceSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    CpuUsage = GetCpuUsage(),
                    MemoryUsageMB = process.WorkingSet64 / (1024.0 * 1024.0),
                    MemoryUsageMBPrivate = process.PrivateMemorySize64 / (1024.0 * 1024.0),
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount,
                    GcGeneration0 = GC.CollectionCount(0),
                    GcGeneration1 = GC.CollectionCount(1),
                    GcGeneration2 = GC.CollectionCount(2),
                    TotalMemoryAllocated = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                    Uptime = (DateTime.UtcNow - process.StartTime).TotalSeconds
                };

                return new PerformanceMetrics
                {
                    Current = snapshot,
                    Average = CalculateAverageMetrics(),
                    Peak = CalculatePeakMetrics(),
                    SampleCount = _snapshotHistory.Count
                };
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning)
                    return;

                _isRunning = true;
                _collectionTimer.Change(TimeSpan.Zero, _collectionInterval);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning)
                    return;

                _isRunning = false;
                _collectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        public void RegisterCustomCounter(string name, Func<double> valueFunction, string unit = null, string description = null)
        {
            lock (_lock)
            {
                var counter = new PerformanceMetric
                {
                    Name = name,
                    Unit = unit,
                    Description = description,
                    ValueFunction = valueFunction,
                    LastUpdated = DateTime.MinValue
                };

                _counters[name] = counter;
            }
        }

        public double GetCounterValue(string name)
        {
            lock (_lock)
            {
                if (_counters.TryGetValue(name, out var counter))
                {
                    try
                    {
                        return counter.ValueFunction?.Invoke() ?? 0;
                    }
                    catch
                    {
                        return 0;
                    }
                }
                return 0;
            }
        }

        private void InitializeCounters()
        {
            // System counters
            RegisterCustomCounter("cpu_usage_percent", GetCpuUsage, "%", "CPU usage percentage");
            RegisterCustomCounter("memory_usage_mb", () => System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0), "MB", "Memory usage in MB");
            RegisterCustomCounter("memory_private_mb", () => System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / (1024.0 * 1024.0), "MB", "Private memory usage in MB");
            RegisterCustomCounter("thread_count", () => System.Diagnostics.Process.GetCurrentProcess().Threads.Count, "count", "Number of threads");
            RegisterCustomCounter("handle_count", () => System.Diagnostics.Process.GetCurrentProcess().HandleCount, "count", "Number of handles");

            // Garbage collection counters
            RegisterCustomCounter("gc_gen0_collections", () => GC.CollectionCount(0), "count", "Generation 0 garbage collections");
            RegisterCustomCounter("gc_gen1_collections", () => GC.CollectionCount(1), "count", "Generation 1 garbage collections");
            RegisterCustomCounter("gc_gen2_collections", () => GC.CollectionCount(2), "count", "Generation 2 garbage collections");
            RegisterCustomCounter("gc_total_memory_mb", () => GC.GetTotalMemory(false) / (1024.0 * 1024.0), "MB", "Total allocated memory");

            // Application counters
            RegisterCustomCounter("uptime_seconds", () => (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds, "seconds", "Application uptime");
            RegisterCustomCounter("exceptions_per_minute", GetExceptionsPerMinute, "count", "Exceptions per minute");
            RegisterCustomCounter("operations_per_second", GetOperationsPerSecond, "count", "Operations per second");
        }

        private async void CollectPerformanceData(object state)
        {
            try
            {
                var snapshot = new PerformanceSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    CpuUsage = GetCpuUsage(),
                    MemoryUsageMB = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0),
                    MemoryUsageMBPrivate = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / (1024.0 * 1024.0),
                    ThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
                    HandleCount = System.Diagnostics.Process.GetCurrentProcess().HandleCount,
                    GcGeneration0 = GC.CollectionCount(0),
                    GcGeneration1 = GC.CollectionCount(1),
                    GcGeneration2 = GC.CollectionCount(2),
                    TotalMemoryAllocated = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                    Uptime = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds
                };

                lock (_lock)
                {
                    _snapshotHistory.Enqueue(snapshot);

                    while (_snapshotHistory.Count > _maxHistorySize)
                    {
                        _snapshotHistory.Dequeue();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw to prevent timer crashes
                Console.WriteLine($"Error collecting performance data: {ex.Message}");
            }
        }

        private double GetCpuUsage()
        {
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;

                Thread.Sleep(50); // Short sleep to measure CPU usage

                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;

                return cpuUsedMs / (Environment.ProcessorCount * totalMsPassed) * 100;
            }
            catch
            {
                return 0;
            }
        }

        private double GetExceptionsPerMinute()
        {
            // This would need to be implemented with actual exception tracking
            // For now, return a placeholder value
            return 0;
        }

        private double GetOperationsPerSecond()
        {
            // This would need to be implemented with actual operation tracking
            // For now, return a placeholder value
            return 0;
        }

        private PerformanceSnapshot CalculateAverageMetrics()
        {
            if (_snapshotHistory.Count == 0)
                return new PerformanceSnapshot();

            var snapshots = _snapshotHistory.ToArray();
            return new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CpuUsage = snapshots.Average(s => s.CpuUsage),
                MemoryUsageMB = snapshots.Average(s => s.MemoryUsageMB),
                MemoryUsageMBPrivate = snapshots.Average(s => s.MemoryUsageMBPrivate),
                ThreadCount = (int)snapshots.Average(s => s.ThreadCount),
                HandleCount = (int)snapshots.Average(s => s.HandleCount),
                GcGeneration0 = (int)snapshots.Average(s => s.GcGeneration0),
                GcGeneration1 = (int)snapshots.Average(s => s.GcGeneration1),
                GcGeneration2 = (int)snapshots.Average(s => s.GcGeneration2),
                TotalMemoryAllocated = snapshots.Average(s => s.TotalMemoryAllocated),
                Uptime = snapshots.Average(s => s.Uptime)
            };
        }

        private PerformanceSnapshot CalculatePeakMetrics()
        {
            if (_snapshotHistory.Count == 0)
                return new PerformanceSnapshot();

            var snapshots = _snapshotHistory.ToArray();
            return new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CpuUsage = snapshots.Max(s => s.CpuUsage),
                MemoryUsageMB = snapshots.Max(s => s.MemoryUsageMB),
                MemoryUsageMBPrivate = snapshots.Max(s => s.MemoryUsageMBPrivate),
                ThreadCount = snapshots.Max(s => s.ThreadCount),
                HandleCount = snapshots.Max(s => s.HandleCount),
                GcGeneration0 = snapshots.Max(s => s.GcGeneration0),
                GcGeneration1 = snapshots.Max(s => s.GcGeneration1),
                GcGeneration2 = snapshots.Max(s => s.GcGeneration2),
                TotalMemoryAllocated = snapshots.Max(s => s.TotalMemoryAllocated),
                Uptime = snapshots.Max(s => s.Uptime)
            };
        }
    }

    public class PerformanceMetric
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }
        public Func<double> ValueFunction { get; set; }
        public DateTime LastUpdated { get; set; }

        public double CurrentValue
        {
            get
            {
                try
                {
                    return ValueFunction?.Invoke() ?? 0;
                }
                catch
                {
                    return 0;
                }
            }
        }
    }

    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsageMB { get; set; }
        public double MemoryUsageMBPrivate { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public int GcGeneration0 { get; set; }
        public int GcGeneration1 { get; set; }
        public int GcGeneration2 { get; set; }
        public double TotalMemoryAllocated { get; set; }
        public double Uptime { get; set; }
    }

    public class PerformanceMetrics
    {
        public PerformanceSnapshot Current { get; set; }
        public PerformanceSnapshot Average { get; set; }
        public PerformanceSnapshot Peak { get; set; }
        public int SampleCount { get; set; }
    }

    public static class PerformanceExtensions
    {
        public static string FormatCounterValue(this double value, string unit = null)
        {
            if (unit == "MB" || unit == "mb")
                return $"{value:F2} MB";
            else if (unit == "%")
                return $"{value:F1}%";
            else if (unit == "seconds" || unit == "s")
                return $"{value:F1}s";
            else if (unit == "count")
                return $"{value:F0}";
            else
                return $"{value:F2}";
        }

        public static bool IsHealthyThreshold(this PerformanceSnapshot snapshot, string counterName)
        {
            return counterName switch
            {
                "cpu_usage_percent" => snapshot.CpuUsage < 80,
                "memory_usage_mb" => snapshot.MemoryUsageMB < 1000,
                "thread_count" => snapshot.ThreadCount < 100,
                "handle_count" => snapshot.HandleCount < 1000,
                _ => true
            };
        }
    }
}