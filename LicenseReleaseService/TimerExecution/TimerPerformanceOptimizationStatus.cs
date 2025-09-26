using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Represents the status of performance optimization components
    /// </summary>
    public class TimerPerformanceOptimizationStatus
    {
        /// <summary>
        /// Gets or sets whether performance optimization is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the overall status
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the memory manager status
        /// </summary>
        public string MemoryManagerStatus { get; set; }

        /// <summary>
        /// Gets or sets the thread pool manager status
        /// </summary>
        public string ThreadPoolManagerStatus { get; set; }

        /// <summary>
        /// Gets or sets the cache manager status
        /// </summary>
        public string CacheManagerStatus { get; set; }

        /// <summary>
        /// Gets or sets the metrics collector status
        /// </summary>
        public string MetricsCollectorStatus { get; set; }

        /// <summary>
        /// Gets or sets the performance optimizer status
        /// </summary>
        public string PerformanceOptimizerStatus { get; set; }

        /// <summary>
        /// Gets or sets the performance tuner status
        /// </summary>
        public string PerformanceTunerStatus { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the status was collected
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerPerformanceOptimizationStatus class
        /// </summary>
        public TimerPerformanceOptimizationStatus()
        {
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the status
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"Performance Optimization Status ({Timestamp:yyyy-MM-dd HH:mm:ss}):");
            builder.AppendLine($"  Enabled: {IsEnabled}");
            builder.AppendLine($"  Status: {Status}");
            if (IsEnabled)
            {
                builder.AppendLine($"  Memory Manager: {MemoryManagerStatus}");
                builder.AppendLine($"  Thread Pool Manager: {ThreadPoolManagerStatus}");
                builder.AppendLine($"  Cache Manager: {CacheManagerStatus}");
                builder.AppendLine($"  Metrics Collector: {MetricsCollectorStatus}");
                builder.AppendLine($"  Performance Optimizer: {PerformanceOptimizerStatus}");
                builder.AppendLine($"  Performance Tuner: {PerformanceTunerStatus}");
            }

            return builder.ToString();
        }
    }
}