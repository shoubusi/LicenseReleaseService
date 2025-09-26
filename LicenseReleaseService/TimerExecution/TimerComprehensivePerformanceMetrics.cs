using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Represents comprehensive performance metrics for the timer execution system
    /// </summary>
    public class TimerComprehensivePerformanceMetrics
    {
        /// <summary>
        /// Gets or sets the timer execution metrics
        /// </summary>
        public TimerPerformanceMetrics TimerMetrics { get; set; }

        /// <summary>
        /// Gets or sets the system metrics
        /// </summary>
        public TimerSystemMetrics SystemMetrics { get; set; }

        /// <summary>
        /// Gets or sets the optimization status
        /// </summary>
        public TimerOptimizationStatus OptimizationStatus { get; set; }

        /// <summary>
        /// Gets or sets the tuning statistics
        /// </summary>
        public TimerTuningStatistics TuningStatistics { get; set; }

        /// <summary>
        /// Gets or sets the memory statistics
        /// </summary>
        public TimerMemoryStatistics MemoryStatistics { get; set; }

        /// <summary>
        /// Gets or sets the thread pool metrics
        /// </summary>
        public TimerThreadPoolMetrics ThreadPoolMetrics { get; set; }

        /// <summary>
        /// Gets or sets the cache metrics
        /// </summary>
        public TimerCacheMetrics CacheMetrics { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the metrics were collected
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerComprehensivePerformanceMetrics class
        /// </summary>
        public TimerComprehensivePerformanceMetrics()
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
            builder.AppendLine($"Comprehensive Performance Metrics ({Timestamp:yyyy-MM-dd HH:mm:ss}):");
            builder.AppendLine();

            builder.AppendLine("Timer Metrics:");
            builder.AppendLine(TimerMetrics?.ToString() ?? "Not available");
            builder.AppendLine();

            if (SystemMetrics != null)
            {
                builder.AppendLine("System Metrics:");
                builder.AppendLine(SystemMetrics.ToString());
                builder.AppendLine();
            }

            if (OptimizationStatus != null)
            {
                builder.AppendLine("Optimization Status:");
                builder.AppendLine(OptimizationStatus.ToString());
                builder.AppendLine();
            }

            if (TuningStatistics != null)
            {
                builder.AppendLine("Tuning Statistics:");
                builder.AppendLine(TuningStatistics.ToString());
                builder.AppendLine();
            }

            if (MemoryStatistics != null)
            {
                builder.AppendLine("Memory Statistics:");
                builder.AppendLine(MemoryStatistics.ToString());
                builder.AppendLine();
            }

            if (ThreadPoolMetrics != null)
            {
                builder.AppendLine("Thread Pool Metrics:");
                builder.AppendLine(ThreadPoolMetrics.ToString());
                builder.AppendLine();
            }

            if (CacheMetrics != null)
            {
                builder.AppendLine("Cache Metrics:");
                builder.AppendLine(CacheMetrics.ToString());
            }

            return builder.ToString();
        }
    }
}