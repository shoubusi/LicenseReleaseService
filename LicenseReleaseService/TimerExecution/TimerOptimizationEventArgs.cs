using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for timer optimization events
    /// </summary>
    public class TimerOptimizationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the unique identifier for this optimization cycle
        /// </summary>
        public Guid OptimizationId { get; }

        /// <summary>
        /// Gets the start time of the optimization
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// Gets the end time of the optimization
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets the duration of the optimization
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets a value indicating whether the optimization was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the error message if the optimization failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets the number of optimizations applied
        /// </summary>
        public int OptimizationsApplied { get; set; }

        /// <summary>
        /// Gets the optimization recommendations that were applied
        /// </summary>
        public List<TimerOptimizationRecommendation> AppliedOptimizations { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerOptimizationEventArgs class
        /// </summary>
        /// <param name="optimizationId">The optimization identifier</param>
        /// <param name="startTime">The start time</param>
        public TimerOptimizationEventArgs(Guid optimizationId, DateTime startTime)
        {
            OptimizationId = optimizationId;
            StartTime = startTime;
            EndTime = startTime;
            Duration = TimeSpan.Zero;
            Success = false;
            ErrorMessage = null;
            OptimizationsApplied = 0;
            AppliedOptimizations = new List<TimerOptimizationRecommendation>();
        }
    }

    /// <summary>
    /// Optimization metrics for the timer performance optimizer
    /// </summary>
    public class TimerOptimizationMetrics
    {
        /// <summary>
        /// Gets the start time of the optimizer
        /// </summary>
        public DateTime? StartTime { get; internal set; }

        /// <summary>
        /// Gets the stop time of the optimizer
        /// </summary>
        public DateTime? StopTime { get; internal set; }

        /// <summary>
        /// Gets the uptime of the optimizer
        /// </summary>
        public TimeSpan Uptime { get; internal set; }

        /// <summary>
        /// Gets the last optimization time
        /// </summary>
        public DateTime? LastOptimizationTime { get; internal set; }

        /// <summary>
        /// Gets the duration of the last optimization
        /// </summary>
        public TimeSpan LastOptimizationDuration { get; internal set; }

        /// <summary>
        /// Gets the total number of optimization cycles
        /// </summary>
        public long OptimizationCount { get; internal set; }

        /// <summary>
        /// Gets the number of failed optimizations
        /// </summary>
        public long FailedOptimizations { get; internal set; }

        /// <summary>
        /// Gets the number of memory pressure events detected
        /// </summary>
        public long MemoryPressureEvents { get; internal set; }

        /// <summary>
        /// Gets the number of thread pool adjustments
        /// </summary>
        public long ThreadPoolAdjustments { get; internal set; }

        /// <summary>
        /// Gets the number of cache optimizations
        /// </summary>
        public long CacheOptimizations { get; internal set; }

        /// <summary>
        /// Gets the number of performance tunings applied
        /// </summary>
        public long PerformanceTunings { get; internal set; }

        /// <summary>
        /// Gets the last collected system metrics
        /// </summary>
        public TimerSystemMetrics LastSystemMetrics { get; internal set; }

        /// <summary>
        /// Gets the success rate as a percentage
        /// </summary>
        public double SuccessRate => OptimizationCount > 0 ?
            ((OptimizationCount - FailedOptimizations) * 100.0 / OptimizationCount) : 0;

        /// <summary>
        /// Gets the optimizations per hour rate
        /// </summary>
        public double OptimizationsPerHour => Uptime.TotalHours > 0 ? OptimizationCount / Uptime.TotalHours : 0;

        /// <summary>
        /// Gets the optimization level
        /// </summary>
        public int OptimizationLevel { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerOptimizationMetrics class
        /// </summary>
        public TimerOptimizationMetrics()
        {
            Reset();
        }

        /// <summary>
        /// Resets all metrics to their initial values
        /// </summary>
        public void Reset()
        {
            StartTime = null;
            StopTime = null;
            Uptime = TimeSpan.Zero;
            LastOptimizationTime = null;
            LastOptimizationDuration = TimeSpan.Zero;
            OptimizationCount = 0;
            FailedOptimizations = 0;
            MemoryPressureEvents = 0;
            ThreadPoolAdjustments = 0;
            CacheOptimizations = 0;
            PerformanceTunings = 0;
            LastSystemMetrics = null;
        }

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Optimization Metrics:");
            builder.AppendLine($"  Optimizations: {OptimizationCount}");
            builder.AppendLine($"  Failed Optimizations: {FailedOptimizations}");
            builder.AppendLine($"  Success Rate: {SuccessRate:F2}%");
            builder.AppendLine($"  Optimizations/Hour: {OptimizationsPerHour:F2}");
            builder.AppendLine($"  Memory Pressure Events: {MemoryPressureEvents}");
            builder.AppendLine($"  Thread Pool Adjustments: {ThreadPoolAdjustments}");
            builder.AppendLine($"  Cache Optimizations: {CacheOptimizations}");
            builder.AppendLine($"  Performance Tunings: {PerformanceTunings}");
            builder.AppendLine($"  Uptime: {Uptime}");

            if (LastOptimizationTime.HasValue)
            {
                builder.AppendLine($"  Last Optimization: {LastOptimizationTime.Value} ({LastOptimizationDuration.TotalMilliseconds:F2}ms)");
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Status information for the timer performance optimizer
    /// </summary>
    public class TimerOptimizationStatus
    {
        /// <summary>
        /// Gets whether the optimizer is currently running
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Gets whether the optimizer is currently optimized
        /// </summary>
        public bool IsOptimized { get; set; }

        /// <summary>
        /// Gets the uptime of the optimizer
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the last optimization time
        /// </summary>
        public DateTime? LastOptimizationTime { get; set; }

        /// <summary>
        /// Gets the total number of optimization cycles
        /// </summary>
        public long OptimizationCount { get; set; }

        /// <summary>
        /// Gets the number of memory pressure events
        /// </summary>
        public long MemoryPressureEvents { get; set; }

        /// <summary>
        /// Gets the number of thread pool adjustments
        /// </summary>
        public long ThreadPoolAdjustments { get; set; }

        /// <summary>
        /// Gets the number of cache optimizations
        /// </summary>
        public long CacheOptimizations { get; set; }

        /// <summary>
        /// Gets the number of performance tunings
        /// </summary>
        public long PerformanceTunings { get; set; }

        /// <summary>
        /// Gets the optimization level
        /// </summary>
        public int OptimizationLevel { get; set; }

        /// <summary>
        /// Gets the optimization metrics
        /// </summary>
        public TimerOptimizationMetrics Metrics { get; set; }
    }

    /// <summary>
    /// Detailed metrics from all optimization components
    /// </summary>
    public class TimerOptimizationDetails
    {
        /// <summary>
        /// Gets the optimizer metrics
        /// </summary>
        public TimerOptimizationMetrics OptimizerMetrics { get; set; }

        /// <summary>
        /// Gets the memory management metrics
        /// </summary>
        public TimerMemoryMetrics MemoryMetrics { get; set; }

        /// <summary>
        /// Gets the thread pool metrics
        /// </summary>
        public TimerThreadPoolMetrics ThreadPoolMetrics { get; set; }

        /// <summary>
        /// Gets the cache metrics
        /// </summary>
        public TimerCacheMetrics CacheMetrics { get; set; }

        /// <summary>
        /// Gets the system metrics
        /// </summary>
        public TimerSystemMetrics SystemMetrics { get; set; }

        /// <summary>
        /// Gets the tuning metrics
        /// </summary>
        public TimerTuningMetrics TuningMetrics { get; set; }
    }

    /// <summary>
    /// Types of optimizations that can be applied
    /// </summary>
    public enum TimerOptimizationType
    {
        /// <summary>
        /// Memory-related optimization
        /// </summary>
        Memory,

        /// <summary>
        /// Thread pool optimization
        /// </summary>
        ThreadPool,

        /// <summary>
        /// Cache optimization
        /// </summary>
        Cache,

        /// <summary>
        /// Performance tuning optimization
        /// </summary>
        PerformanceTuning,

        /// <summary>
        /// Reduce CPU usage optimization
        /// </summary>
        ReduceCpuUsage,

        /// <summary>
        /// Reduce memory usage optimization
        /// </summary>
        ReduceMemoryUsage,

        /// <summary>
        /// Optimize thread pool optimization
        /// </summary>
        OptimizeThreadPool
    }

    /// <summary>
    /// Represents an optimization recommendation
    /// </summary>
    public class TimerOptimizationRecommendation
    {
        /// <summary>
        /// Gets the type of optimization
        /// </summary>
        public TimerOptimizationType Type { get; set; }

        /// <summary>
        /// Gets the priority of the optimization
        /// </summary>
        public TimerOptimizationPriority Priority { get; set; }

        /// <summary>
        /// Gets the description of the optimization
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets the expected impact of the optimization
        /// </summary>
        public TimerOptimizationImpact ExpectedImpact { get; set; }

        /// <summary>
        /// Gets the parameters for the optimization
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Gets the reason for the optimization
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerOptimizationRecommendation class
        /// </summary>
        public TimerOptimizationRecommendation()
        {
            Parameters = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Priority levels for optimization recommendations
    /// </summary>
    public enum TimerOptimizationPriority
    {
        /// <summary>
        /// Low priority optimization
        /// </summary>
        Low,

        /// <summary>
        /// Medium priority optimization
        /// </summary>
        Medium,

        /// <summary>
        /// High priority optimization
        /// </summary>
        High,

        /// <summary>
        /// Critical priority optimization
        /// </summary>
        Critical
    }

    /// <summary>
    /// Expected impact of optimizations
    /// </summary>
    public enum TimerOptimizationImpact
    {
        /// <summary>
        /// Minimal impact expected
        /// </summary>
        Minimal,

        /// <summary>
        /// Moderate impact expected
        /// </summary>
        Moderate,

        /// <summary>
        /// Significant impact expected
        /// </summary>
        Significant,

        /// <summary>
        /// Critical impact expected
        /// </summary>
        Critical
    }

    /// <summary>
    /// Metrics for the timer performance tuner
    /// </summary>
    public class TimerTunerMetrics
    {
        /// <summary>
        /// Gets the timestamp when metrics were collected
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
        /// Gets the number of active timers
        /// </summary>
        public int ActiveTimerCount { get; set; }

        /// <summary>
        /// Gets the average timer execution time
        /// </summary>
        public double AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets the number of optimizations applied
        /// </summary>
        public int OptimizationsApplied { get; set; }

        /// <summary>
        /// Gets the performance improvement percentage
        /// </summary>
        public double PerformanceImprovement { get; set; }

        /// <summary>
        /// Gets whether tuning is currently active
        /// </summary>
        public bool IsTuningActive { get; set; }

        /// <summary>
        /// Gets the number of tuning cycles completed
        /// </summary>
        public int TuningCyclesCompleted { get; set; }

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        public object CurrentConfiguration { get; set; }

        /// <summary>
        /// Gets the last tuning time
        /// </summary>
        public DateTime? LastTuningTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerTunerMetrics class
        /// </summary>
        public TimerTunerMetrics()
        {
            Timestamp = DateTime.UtcNow;
            TuningCyclesCompleted = 0;
            CurrentConfiguration = null;
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