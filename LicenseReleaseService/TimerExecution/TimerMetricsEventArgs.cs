using System;
using System.Collections.Generic;
using System.Linq;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for metrics collection events
    /// </summary>
    public class TimerMetricsCollectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the performance snapshot
        /// </summary>
        public TimerPerformanceSnapshot Snapshot { get; }

        /// <summary>
        /// Gets the timestamp when metrics were collected
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerMetricsCollectedEventArgs class
        /// </summary>
        /// <param name="snapshot">The performance snapshot</param>
        public TimerMetricsCollectedEventArgs(TimerPerformanceSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for performance threshold exceeded events
    /// </summary>
    public class TimerPerformanceThresholdExceededEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the performance snapshot
        /// </summary>
        public TimerPerformanceSnapshot Snapshot { get; }

        /// <summary>
        /// Gets the exceeded thresholds
        /// </summary>
        public IReadOnlyList<TimerPerformanceThreshold> ExceededThresholds { get; }

        /// <summary>
        /// Gets the timestamp when thresholds were exceeded
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerPerformanceThresholdExceededEventArgs class
        /// </summary>
        /// <param name="snapshot">The performance snapshot</param>
        /// <param name="exceededThresholds">The exceeded thresholds</param>
        public TimerPerformanceThresholdExceededEventArgs(TimerPerformanceSnapshot snapshot, IEnumerable<TimerPerformanceThreshold> exceededThresholds)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            ExceededThresholds = exceededThresholds?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(exceededThresholds));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a performance snapshot
    /// </summary>
    public class TimerPerformanceSnapshot
    {
        /// <summary>
        /// Gets the snapshot ID
        /// </summary>
        public string SnapshotId { get; set; }

        /// <summary>
        /// Gets the timestamp when the snapshot was taken
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Gets the memory usage percentage
        /// </summary>
        public double MemoryUsagePercent { get; set; }

        /// <summary>
        /// Gets the thread count
        /// </summary>
        public int ThreadCount { get; set; }

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
        /// Gets the available memory in MB
        /// </summary>
        public long AvailableMemoryMB { get; set; }

        /// <summary>
        /// Gets the disk usage percentage
        /// </summary>
        public double DiskUsagePercent { get; set; }

        /// <summary>
        /// Gets the network I/O rate in KB/s
        /// </summary>
        public double NetworkIoRate { get; set; }

        /// <summary>
        /// Gets the number of active timer executions
        /// </summary>
        public int ActiveTimerExecutions { get; set; }

        /// <summary>
        /// Gets the GC collection count for generation 0
        /// </summary>
        public long GcCollectionCount0 { get; set; }

        /// <summary>
        /// Gets the GC collection count for generation 1
        /// </summary>
        public long GcCollectionCount1 { get; set; }

        /// <summary>
        /// Gets the GC collection count for generation 2
        /// </summary>
        public long GcCollectionCount2 { get; set; }

        /// <summary>
        /// Gets the total memory allocated
        /// </summary>
        public long TotalMemoryAllocated { get; set; }

        /// <summary>
        /// Gets the uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Returns a string representation of the snapshot
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"Snapshot {SnapshotId}: CPU {CpuUsagePercent:F1}%, Memory {MemoryUsagePercent:F1}%, Threads {ThreadCount}, Time {Timestamp}";
        }
    }

    /// <summary>
    /// Represents a performance threshold
    /// </summary>
    public class TimerPerformanceThreshold
    {
        /// <summary>
        /// Gets the metric type
        /// </summary>
        public TimerPerformanceMetricType MetricType { get; set; }

        /// <summary>
        /// Gets the threshold value
        /// </summary>
        public double ThresholdValue { get; set; }

        /// <summary>
        /// Gets the actual value
        /// </summary>
        public double ActualValue { get; set; }

        /// <summary>
        /// Gets the severity of the threshold violation
        /// </summary>
        public TimerPerformanceThresholdSeverity Severity { get; set; }

        /// <summary>
        /// Returns a string representation of the threshold
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"{MetricType}: {ActualValue:F1} > {ThresholdValue:F1} ({Severity})";
        }
    }

    /// <summary>
    /// Performance statistics for a time range
    /// </summary>
    public class TimerPerformanceStatistics
    {
        /// <summary>
        /// Gets the start time of the statistics
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets the end time of the statistics
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets the number of snapshots included
        /// </summary>
        public int SnapshotCount { get; set; }

        /// <summary>
        /// Gets the average CPU usage
        /// </summary>
        public double AverageCpuUsage { get; set; }

        /// <summary>
        /// Gets the average memory usage
        /// </summary>
        public double AverageMemoryUsage { get; set; }

        /// <summary>
        /// Gets the average thread count
        /// </summary>
        public double AverageThreadCount { get; set; }

        /// <summary>
        /// Gets the maximum CPU usage
        /// </summary>
        public double MaxCpuUsage { get; set; }

        /// <summary>
        /// Gets the maximum memory usage
        /// </summary>
        public double MaxMemoryUsage { get; set; }

        /// <summary>
        /// Gets the maximum thread count
        /// </summary>
        public double MaxThreadCount { get; set; }

        /// <summary>
        /// Gets the minimum CPU usage
        /// </summary>
        public double MinCpuUsage { get; set; }

        /// <summary>
        /// Gets the minimum memory usage
        /// </summary>
        public double MinMemoryUsage { get; set; }

        /// <summary>
        /// Gets the minimum thread count
        /// </summary>
        public double MinThreadCount { get; set; }

        /// <summary>
        /// Gets the performance trend
        /// </summary>
        public TimerPerformanceTrend PerformanceTrend { get; set; }

        /// <summary>
        /// Gets the duration of the statistics
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Returns a string representation of the statistics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"Performance Statistics ({StartTime:yyyy-MM-dd HH:mm:ss} - {EndTime:yyyy-MM-dd HH:mm:ss}):");
            builder.AppendLine($"  Snapshots: {SnapshotCount}");
            builder.AppendLine($"  Duration: {Duration}");
            builder.AppendLine($"  CPU Usage: Avg {AverageCpuUsage:F1}%, Min {MinCpuUsage:F1}%, Max {MaxCpuUsage:F1}%");
            builder.AppendLine($"  Memory Usage: Avg {AverageMemoryUsage:F1}%, Min {MinMemoryUsage:F1}%, Max {MaxMemoryUsage:F1}%");
            builder.AppendLine($"  Thread Count: Avg {AverageThreadCount:F1}, Min {MinThreadCount:F1}, Max {MaxThreadCount:F1}");
            builder.AppendLine($"  Trend: {PerformanceTrend}");

            return builder.ToString();
        }
    }

    /// <summary>
    /// Performance metric types
    /// </summary>
    public enum TimerPerformanceMetricType
    {
        /// <summary>
        /// CPU usage metric
        /// </summary>
        CpuUsage,

        /// <summary>
        /// Memory usage metric
        /// </summary>
        MemoryUsage,

        /// <summary>
        /// Thread count metric
        /// </summary>
        ThreadCount,

        /// <summary>
        /// Disk usage metric
        /// </summary>
        DiskUsage,

        /// <summary>
        /// Network I/O metric
        /// </summary>
        NetworkIo,

        /// <summary>
        /// Timer execution count metric
        /// </summary>
        TimerExecutionCount,

        /// <summary>
        /// GC collection count metric
        /// </summary>
        GcCollectionCount,

        /// <summary>
        /// Memory allocation metric
        /// </summary>
        MemoryAllocation
    }

    /// <summary>
    /// Performance threshold severity levels
    /// </summary>
    public enum TimerPerformanceThresholdSeverity
    {
        /// <summary>
        /// Low severity
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity
        /// </summary>
        Medium,

        /// <summary>
        /// High severity
        /// </summary>
        High,

        /// <summary>
        /// Critical severity
        /// </summary>
        Critical
    }

    /// <summary>
    /// Performance trend indicators
    /// </summary>
    public enum TimerPerformanceTrend
    {
        /// <summary>
        /// Performance is improving
        /// </summary>
        Improving,

        /// <summary>
        /// Performance is stable
        /// </summary>
        Stable,

        /// <summary>
        /// Performance is degrading
        /// </summary>
        Degrading,

        /// <summary>
        /// Performance is unknown
        /// </summary>
        Unknown
    }
}