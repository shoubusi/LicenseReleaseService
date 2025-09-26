using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for tuning applied events
    /// </summary>
    public class TimerTuningAppliedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the tuning action that was applied
        /// </summary>
        public TimerTuningAction TuningAction { get; }

        /// <summary>
        /// Gets the timestamp when the tuning was applied
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerTuningAppliedEventArgs class
        /// </summary>
        /// <param name="tuningAction">The tuning action that was applied</param>
        public TimerTuningAppliedEventArgs(TimerTuningAction tuningAction)
        {
            TuningAction = tuningAction ?? throw new ArgumentNullException(nameof(tuningAction));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for tuning recommendation events
    /// </summary>
    public class TimerTuningRecommendationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the tuning recommendations
        /// </summary>
        public IReadOnlyList<TimerTuningRecommendation> Recommendations { get; }

        /// <summary>
        /// Gets the timestamp when the recommendations were generated
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerTuningRecommendationEventArgs class
        /// </summary>
        /// <param name="recommendations">The tuning recommendations</param>
        public TimerTuningRecommendationEventArgs(IEnumerable<TimerTuningRecommendation> recommendations)
        {
            Recommendations = recommendations?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(recommendations));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a tuning action
    /// </summary>
    public class TimerTuningAction
    {
        /// <summary>
        /// Gets the timestamp when the action was performed
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the type of action performed
        /// </summary>
        public TimerTuningActionType Action { get; set; }

        /// <summary>
        /// Gets the reason for the action
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets the recommendation that led to this action (if applicable)
        /// </summary>
        public TimerTuningRecommendation? Recommendation { get; set; }

        /// <summary>
        /// Gets the old configuration (if applicable)
        /// </summary>
        public TimerTuningConfiguration? OldConfiguration { get; set; }

        /// <summary>
        /// Gets the new configuration (if applicable)
        /// </summary>
        public TimerTuningConfiguration? NewConfiguration { get; set; }

        /// <summary>
        /// Gets whether the action was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the performance improvement percentage (if applicable)
        /// </summary>
        public double PerformanceImprovement { get; set; }

        /// <summary>
        /// Returns a string representation of the action
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var result = $"{Action}: {Reason}";
            if (!Success)
                result += " (Failed)";
            if (PerformanceImprovement > 0)
                result += $" (+{PerformanceImprovement:F1}%)";
            return result;
        }
    }

    /// <summary>
    /// Represents a tuning recommendation
    /// </summary>
    public class TimerTuningRecommendation
    {
        /// <summary>
        /// Gets the name of the rule that generated the recommendation
        /// </summary>
        public string RuleName { get; set; }

        /// <summary>
        /// Gets the action to take
        /// </summary>
        public TimerTuningActionType Action { get; set; }

        /// <summary>
        /// Gets the priority of the recommendation
        /// </summary>
        public TimerTuningPriority Priority { get; set; }

        /// <summary>
        /// Gets the reason for the recommendation
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets the estimated impact of the recommendation
        /// </summary>
        public TimerTuningImpact EstimatedImpact { get; set; }

        /// <summary>
        /// Gets the confidence level of the recommendation
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Returns a string representation of the recommendation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"{RuleName}: {Action} (Priority: {Priority}, Impact: {EstimatedImpact}, Confidence: {Confidence:F1}%)";
        }
    }

    /// <summary>
    /// Represents a tuning rule
    /// </summary>
    public class TimerTuningRule
    {
        /// <summary>
        /// Gets or sets the name of the rule
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the action to take when the rule applies
        /// </summary>
        public TimerTuningActionType Action { get; set; }

        /// <summary>
        /// Gets or sets the priority of the rule
        /// </summary>
        public TimerTuningPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the estimated impact of the action
        /// </summary>
        public TimerTuningImpact EstimatedImpact { get; set; }

        /// <summary>
        /// Gets or sets the condition function that determines if the rule should apply
        /// </summary>
        public Func<TimerPerformanceSnapshot, List<TimerPerformanceSnapshot>, bool> Condition { get; set; }

        /// <summary>
        /// Gets or sets the function that generates the reason for the rule
        /// </summary>
        public Func<TimerPerformanceSnapshot, List<TimerPerformanceSnapshot>, string> ReasonGenerator { get; set; }

        /// <summary>
        /// Gets or sets the function that calculates the confidence level
        /// </summary>
        public Func<TimerPerformanceSnapshot, List<TimerPerformanceSnapshot>, double> ConfidenceCalculator { get; set; }

        /// <summary>
        /// Gets whether the rule should apply based on the current conditions
        /// </summary>
        /// <param name="currentSnapshot">The current performance snapshot</param>
        /// <param name="recentSnapshots">Recent performance snapshots</param>
        /// <returns>True if the rule should apply, false otherwise</returns>
        public bool ShouldApply(TimerPerformanceSnapshot currentSnapshot, List<TimerPerformanceSnapshot> recentSnapshots)
        {
            return Condition?.Invoke(currentSnapshot, recentSnapshots) ?? false;
        }

        /// <summary>
        /// Gets the reason for the rule recommendation
        /// </summary>
        /// <param name="currentSnapshot">The current performance snapshot</param>
        /// <param name="recentSnapshots">Recent performance snapshots</param>
        /// <returns>The reason for the recommendation</returns>
        public string GetReason(TimerPerformanceSnapshot currentSnapshot, List<TimerPerformanceSnapshot> recentSnapshots)
        {
            return ReasonGenerator?.Invoke(currentSnapshot, recentSnapshots) ?? "Unknown reason";
        }

        /// <summary>
        /// Gets the confidence level for the rule recommendation
        /// </summary>
        /// <param name="currentSnapshot">The current performance snapshot</param>
        /// <param name="recentSnapshots">Recent performance snapshots</param>
        /// <returns>The confidence level (0-100)</returns>
        public double GetConfidence(TimerPerformanceSnapshot currentSnapshot, List<TimerPerformanceSnapshot> recentSnapshots)
        {
            return Math.Max(0, Math.Min(100, ConfidenceCalculator?.Invoke(currentSnapshot, recentSnapshots) ?? 0));
        }
    }

    /// <summary>
    /// Represents a tuning configuration
    /// </summary>
    public class TimerTuningConfiguration
    {
        /// <summary>
        /// Gets or sets the optimization interval in milliseconds
        /// </summary>
        public int OptimizationIntervalMs { get; set; } = 60000;

        /// <summary>
        /// Gets or sets the maximum thread pool size
        /// </summary>
        public int MaxThreadPoolSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the minimum thread pool size
        /// </summary>
        public int MinThreadPoolSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets the maximum cache size
        /// </summary>
        public int MaxCacheSize { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the memory pressure threshold
        /// </summary>
        public double MemoryPressureThreshold { get; set; } = 80.0;

        /// <summary>
        /// Gets or sets the CPU usage threshold
        /// </summary>
        public double CpuUsageThreshold { get; set; } = 80.0;

        /// <summary>
        /// Gets or sets whether to enable automatic optimization
        /// </summary>
        public bool EnableAutoOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable memory pressure monitoring
        /// </summary>
        public bool EnableMemoryPressureMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable thread pool optimization
        /// </summary>
        public bool EnableThreadPoolOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable cache optimization
        /// </summary>
        public bool EnableCacheOptimization { get; set; } = true;

        /// <summary>
        /// Creates a copy of the current configuration
        /// </summary>
        /// <returns>Copy of the configuration</returns>
        public TimerTuningConfiguration Clone()
        {
            return new TimerTuningConfiguration
            {
                OptimizationIntervalMs = OptimizationIntervalMs,
                MaxThreadPoolSize = MaxThreadPoolSize,
                MinThreadPoolSize = MinThreadPoolSize,
                MaxCacheSize = MaxCacheSize,
                MemoryPressureThreshold = MemoryPressureThreshold,
                CpuUsageThreshold = CpuUsageThreshold,
                EnableAutoOptimization = EnableAutoOptimization,
                EnableMemoryPressureMonitoring = EnableMemoryPressureMonitoring,
                EnableThreadPoolOptimization = EnableThreadPoolOptimization,
                EnableCacheOptimization = EnableCacheOptimization
            };
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"Tuning Configuration: OptInterval={OptimizationIntervalMs}ms, ThreadPool={MinThreadPoolSize}-{MaxThreadPoolSize}, Cache={MaxCacheSize}, MemoryThreshold={MemoryPressureThreshold:F1}%";
        }
    }

    /// <summary>
    /// Tuning statistics
    /// </summary>
    public class TimerTuningStatistics
    {
        /// <summary>
        /// Gets the total number of tuning cycles
        /// </summary>
        public long TotalTuningCycles { get; set; }

        /// <summary>
        /// Gets the number of successful tunings
        /// </summary>
        public long SuccessfulTunings { get; set; }

        /// <summary>
        /// Gets the number of failed tunings
        /// </summary>
        public long FailedTunings { get; set; }

        /// <summary>
        /// Gets the success rate as a percentage
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets the uptime of the tuner
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the last tuning time
        /// </summary>
        public DateTime LastTuningTime { get; set; }

        /// <summary>
        /// Gets the average performance improvement
        /// </summary>
        public double AveragePerformanceImprovement { get; set; }

        /// <summary>
        /// Returns a string representation of the statistics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Tuning Statistics:");
            builder.AppendLine($"  Total Cycles: {TotalTuningCycles}");
            builder.AppendLine($"  Successful: {SuccessfulTunings}, Failed: {FailedTunings}");
            builder.AppendLine($"  Success Rate: {SuccessRate:F2}%");
            builder.AppendLine($"  Uptime: {Uptime}");
            builder.AppendLine($"  Last Tuning: {LastTuningTime}");
            builder.AppendLine($"  Average Improvement: {AveragePerformanceImprovement:F2}%");

            return builder.ToString();
        }
    }

    /// <summary>
    /// Tuning action types
    /// </summary>
    public enum TimerTuningActionType
    {
        /// <summary>
        /// Increase thread pool size
        /// </summary>
        IncreaseThreadPool,

        /// <summary>
        /// Decrease thread pool size
        /// </summary>
        DecreaseThreadPool,

        /// <summary>
        /// Adjust cache size
        /// </summary>
        AdjustCacheSize,

        /// <summary>
        /// Force garbage collection
        /// </summary>
        ForceGarbageCollection,

        /// <summary>
        /// Adjust memory pressure settings
        /// </summary>
        AdjustMemoryPressure,

        /// <summary>
        /// Update configuration
        /// </summary>
        ConfigurationUpdate,

        /// <summary>
        /// Restart service
        /// </summary>
        RestartService,

        /// <summary>
        /// Enable optimization feature
        /// </summary>
        EnableOptimization,

        /// <summary>
        /// Disable optimization feature
        /// </summary>
        DisableOptimization
    }

    /// <summary>
    /// Tuning priority levels
    /// </summary>
    public enum TimerTuningPriority
    {
        /// <summary>
        /// Low priority
        /// </summary>
        Low,

        /// <summary>
        /// Medium priority
        /// </summary>
        Medium,

        /// <summary>
        /// High priority
        /// </summary>
        High,

        /// <summary>
        /// Critical priority
        /// </summary>
        Critical
    }

    /// <summary>
    /// Tuning impact levels
    /// </summary>
    public enum TimerTuningImpact
    {
        /// <summary>
        /// Low impact
        /// </summary>
        Low,

        /// <summary>
        /// Medium impact
        /// </summary>
        Medium,

        /// <summary>
        /// High impact
        /// </summary>
        High,

        /// <summary>
        /// Critical impact
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents memory statistics for performance monitoring
    /// </summary>
    public class TimerMemoryStatistics
    {
        /// <summary>
        /// Gets or sets the total memory allocated in bytes
        /// </summary>
        public long TotalMemoryAllocated { get; set; }

        /// <summary>
        /// Gets or sets the memory currently in use in bytes
        /// </summary>
        public long MemoryInUse { get; set; }

        /// <summary>
        /// Gets or sets the available memory in bytes
        /// </summary>
        public long AvailableMemory { get; set; }

        /// <summary>
        /// Gets or sets the memory pressure level
        /// </summary>
        public TimerMemoryPressureLevel MemoryPressureLevel { get; set; }

        /// <summary>
        /// Gets or sets the number of garbage collections performed
        /// </summary>
        public long GarbageCollectionsCount { get; set; }

        /// <summary>
        /// Gets or sets the time spent in garbage collection
        /// </summary>
        public TimeSpan GarbageCollectionTime { get; set; }

        /// <summary>
        /// Gets or sets the number of memory pressure events
        /// </summary>
        public long MemoryPressureEvents { get; set; }

        /// <summary>
        /// Gets or sets the number of memory optimizations performed
        /// </summary>
        public long MemoryOptimizationsPerformed { get; set; }

        /// <summary>
        /// Gets or sets the memory usage percentage
        /// </summary>
        public double MemoryUsagePercentage { get; set; }

        /// <summary>
        /// Gets or sets the last memory optimization time
        /// </summary>
        public DateTime? LastOptimizationTime { get; set; }

        /// <summary>
        /// Gets or sets the average memory usage over time
        /// </summary>
        public double AverageMemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the peak memory usage
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerMemoryStatistics class
        /// </summary>
        public TimerMemoryStatistics()
        {
            TotalMemoryAllocated = 0;
            MemoryInUse = 0;
            AvailableMemory = 0;
            MemoryPressureLevel = TimerMemoryPressureLevel.None;
            GarbageCollectionsCount = 0;
            GarbageCollectionTime = TimeSpan.Zero;
            MemoryPressureEvents = 0;
            MemoryOptimizationsPerformed = 0;
            MemoryUsagePercentage = 0;
            AverageMemoryUsage = 0;
            PeakMemoryUsage = 0;
        }

        /// <summary>
        /// Returns a string representation of the memory statistics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Memory Statistics:");
            builder.AppendLine($"  Total Memory Allocated: {TotalMemoryAllocated / (1024 * 1024):F2}MB");
            builder.AppendLine($"  Memory In Use: {MemoryInUse / (1024 * 1024):F2}MB");
            builder.AppendLine($"  Available Memory: {AvailableMemory / (1024 * 1024):F2}MB");
            builder.AppendLine($"  Memory Usage: {MemoryUsagePercentage:F2}%");
            builder.AppendLine($"  Average Memory Usage: {AverageMemoryUsage:F2}%");
            builder.AppendLine($"  Peak Memory Usage: {PeakMemoryUsage / (1024 * 1024):F2}MB");
            builder.AppendLine($"  Memory Pressure: {MemoryPressureLevel}");
            builder.AppendLine($"  Garbage Collections: {GarbageCollectionsCount}");
            builder.AppendLine($"  GC Time: {GarbageCollectionTime.TotalMilliseconds:F2}ms");
            builder.AppendLine($"  Memory Pressure Events: {MemoryPressureEvents}");
            builder.AppendLine($"  Memory Optimizations: {MemoryOptimizationsPerformed}");

            if (LastOptimizationTime.HasValue)
            {
                builder.AppendLine($"  Last Optimization: {LastOptimizationTime.Value}");
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Memory pressure levels for the timer memory manager
    /// </summary>
    public enum TimerMemoryPressureLevel
    {
        /// <summary>
        /// No memory pressure
        /// </summary>
        None,

        /// <summary>
        /// Low memory pressure
        /// </summary>
        Low,

        /// <summary>
        /// Medium memory pressure
        /// </summary>
        Medium,

        /// <summary>
        /// High memory pressure
        /// </summary>
        High,

        /// <summary>
        /// Critical memory pressure
        /// </summary>
        Critical
    }
}