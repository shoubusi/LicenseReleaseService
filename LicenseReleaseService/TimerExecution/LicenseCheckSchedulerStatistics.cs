using System;
using System.Collections.Generic;
using System.Linq;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Statistics for the license check queue
    /// </summary>
    public class LicenseCheckQueueStatistics
    {
        /// <summary>
        /// Gets the total number of operations processed
        /// </summary>
        public long TotalOperationsProcessed { get; set; }

        /// <summary>
        /// Gets the number of operations currently in the queue
        /// </summary>
        public int CurrentQueueSize { get; set; }

        /// <summary>
        /// Gets the maximum queue size observed
        /// </summary>
        public int MaxQueueSizeObserved { get; set; }

        /// <summary>
        /// Gets the number of successful operations
        /// </summary>
        public long SuccessfulOperations { get; set; }

        /// <summary>
        /// Gets the number of failed operations
        /// </summary>
        public long FailedOperations { get; set; }

        /// <summary>
        /// Gets the number of cancelled operations
        /// </summary>
        public long CancelledOperations { get; set; }

        /// <summary>
        /// Gets the number of retried operations
        /// </summary>
        public long RetriedOperations { get; set; }

        /// <summary>
        /// Gets the number of timed out operations
        /// </summary>
        public long TimedOutOperations { get; set; }

        /// <summary>
        /// Gets the number of operations by priority
        /// </summary>
        public IReadOnlyDictionary<LicenseCheckPriority, long> OperationsByPriority { get; set; }

        /// <summary>
        /// Gets the number of operations by type
        /// </summary>
        public IReadOnlyDictionary<LicenseCheckOperationType, long> OperationsByType { get; set; }

        /// <summary>
        /// Gets the average queue wait time
        /// </summary>
        public TimeSpan AverageQueueWaitTime { get; set; }

        /// <summary>
        /// Gets the maximum queue wait time
        /// </summary>
        public TimeSpan MaxQueueWaitTime { get; set; }

        /// <summary>
        /// Gets the average execution time
        /// </summary>
        public TimeSpan AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets the total execution time
        /// </summary>
        public TimeSpan TotalExecutionTime { get; set; }

        /// <summary>
        /// Gets the timestamp when statistics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets the success rate (0.0 to 1.0)
        /// </summary>
        public double SuccessRate
        {
            get
            {
                if (TotalOperationsProcessed == 0)
                    return 0.0;

                return (double)SuccessfulOperations / TotalOperationsProcessed;
            }
        }

        /// <summary>
        /// Gets the failure rate (0.0 to 1.0)
        /// </summary>
        public double FailureRate
        {
            get
            {
                if (TotalOperationsProcessed == 0)
                    return 0.0;

                return (double)FailedOperations / TotalOperationsProcessed;
            }
        }

        /// <summary>
        /// Gets the retry rate (0.0 to 1.0)
        /// </summary>
        public double RetryRate
        {
            get
            {
                if (TotalOperationsProcessed == 0)
                    return 0.0;

                return (double)RetriedOperations / TotalOperationsProcessed;
            }
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckQueueStatistics class
        /// </summary>
        public LicenseCheckQueueStatistics()
        {
            OperationsByPriority = new Dictionary<LicenseCheckPriority, long>().AsReadOnly();
            OperationsByType = new Dictionary<LicenseCheckOperationType, long>().AsReadOnly();
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a summary string of the statistics
        /// </summary>
        /// <returns>Summary string</returns>
        public string ToSummaryString()
        {
            return $"Processed: {TotalOperationsProcessed}, Success: {SuccessRate:P1}, Current Queue: {CurrentQueueSize}";
        }

        /// <summary>
        /// Returns a detailed string of the statistics
        /// </summary>
        /// <returns>Detailed string</returns>
        public string ToDetailedString()
        {
            var priorityStats = string.Join(", ", OperationsByPriority.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var typeStats = string.Join(", ", OperationsByType.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            return $@"LicenseCheckQueueStatistics:
  Total Operations Processed: {TotalOperationsProcessed}
  Current Queue Size: {CurrentQueueSize}
  Max Queue Size Observed: {MaxQueueSizeObserved}
  Successful Operations: {SuccessfulOperations}
  Failed Operations: {FailedOperations}
  Cancelled Operations: {CancelledOperations}
  Retried Operations: {RetriedOperations}
  Timed Out Operations: {TimedOutOperations}
  Success Rate: {SuccessRate:P1}
  Failure Rate: {FailureRate:P1}
  Retry Rate: {RetryRate:P1}
  Average Queue Wait Time: {AverageQueueWaitTime.TotalMilliseconds:F0}ms
  Max Queue Wait Time: {MaxQueueWaitTime.TotalMilliseconds:F0}ms
  Average Execution Time: {AverageExecutionTime.TotalMilliseconds:F0}ms
  Total Execution Time: {TotalExecutionTime.TotalHours:F1} hours
  Operations by Priority: {priorityStats}
  Operations by Type: {typeStats}
  Last Updated: {LastUpdated:yyyy-MM-dd HH:mm:ss UTC}";
        }
    }

    /// <summary>
    /// Metrics for the license check scheduler
    /// </summary>
    public class LicenseCheckSchedulerMetrics
    {
        /// <summary>
        /// Gets the scheduler start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets the last activity timestamp
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets the total uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the total number of checks performed
        /// </summary>
        public long TotalChecksPerformed { get; set; }

        /// <summary>
        /// Gets the number of checks in the current interval
        /// </summary>
        public long CurrentIntervalChecks { get; set; }

        /// <summary>
        /// Gets the average time between checks
        /// </summary>
        public TimeSpan AverageTimeBetweenChecks { get; set; }

        /// <summary>
        /// Gets the total memory usage
        /// </summary>
        public long TotalMemoryUsage { get; set; }

        /// <summary>
        /// Gets the peak memory usage
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Gets the current CPU usage percentage
        /// </summary>
        public double CurrentCpuUsage { get; set; }

        /// <summary>
        /// Gets the average CPU usage percentage
        /// </summary>
        public double AverageCpuUsage { get; set; }

        /// <summary>
        /// Gets the number of active timers
        /// </summary>
        public int ActiveTimers { get; set; }

        /// <summary>
        /// Gets the number of consecutive errors
        /// </summary>
        public int ConsecutiveErrors { get; set; }

        /// <summary>
        /// Gets the total number of errors
        /// </summary>
        public long TotalErrors { get; set; }

        /// <summary>
        /// Gets the error rate (0.0 to 1.0)
        /// </summary>
        public double ErrorRate
        {
            get
            {
                if (TotalChecksPerformed == 0)
                    return 0.0;

                return (double)TotalErrors / TotalChecksPerformed;
            }
        }

        /// <summary>
        /// Gets the checks per minute rate
        /// </summary>
        public double ChecksPerMinute
        {
            get
            {
                if (Uptime.TotalMinutes == 0)
                    return 0.0;

                return TotalChecksPerformed / Uptime.TotalMinutes;
            }
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckSchedulerMetrics class
        /// </summary>
        public LicenseCheckSchedulerMetrics()
        {
            StartTime = DateTime.UtcNow;
            LastActivity = DateTime.UtcNow;
            Uptime = TimeSpan.Zero;
        }

        /// <summary>
        /// Updates the metrics with current activity
        /// </summary>
        public void UpdateActivity()
        {
            LastActivity = DateTime.UtcNow;
            Uptime = LastActivity - StartTime;
        }

        /// <summary>
        /// Updates the metrics with memory usage
        /// </summary>
        /// <param name="currentMemoryUsage">Current memory usage in bytes</param>
        public void UpdateMemoryUsage(long currentMemoryUsage)
        {
            TotalMemoryUsage = currentMemoryUsage;
            if (currentMemoryUsage > PeakMemoryUsage)
            {
                PeakMemoryUsage = currentMemoryUsage;
            }
        }

        /// <summary>
        /// Updates the metrics with CPU usage
        /// </summary>
        /// <param name="cpuUsage">Current CPU usage percentage</param>
        public void UpdateCpuUsage(double cpuUsage)
        {
            CurrentCpuUsage = cpuUsage;
        }

        /// <summary>
        /// Returns a summary string of the metrics
        /// </summary>
        /// <returns>Summary string</returns>
        public string ToSummaryString()
        {
            return $"Checks: {TotalChecksPerformed}, Errors: {TotalErrors}, Uptime: {Uptime.TotalHours:F1}h, Rate: {ChecksPerMinute:F1}/min";
        }

        /// <summary>
        /// Returns a detailed string of the metrics
        /// </summary>
        /// <returns>Detailed string</returns>
        public string ToDetailedString()
        {
            return $@"LicenseCheckSchedulerMetrics:
  Start Time: {StartTime:yyyy-MM-dd HH:mm:ss UTC}
  Last Activity: {LastActivity:yyyy-MM-dd HH:mm:ss UTC}
  Uptime: {Uptime.TotalHours:F1} hours
  Total Checks Performed: {TotalChecksPerformed}
  Current Interval Checks: {CurrentIntervalChecks}
  Average Time Between Checks: {AverageTimeBetweenChecks.TotalMilliseconds:F0}ms
  Checks Per Minute: {ChecksPerMinute:F1}
  Total Memory Usage: {TotalMemoryUsage / (1024 * 1024):F1} MB
  Peak Memory Usage: {PeakMemoryUsage / (1024 * 1024):F1} MB
  Current CPU Usage: {CurrentCpuUsage:F1}%
  Average CPU Usage: {AverageCpuUsage:F1}%
  Active Timers: {ActiveTimers}
  Consecutive Errors: {ConsecutiveErrors}
  Total Errors: {TotalErrors}
  Error Rate: {ErrorRate:P1}";
        }
    }

    /// <summary>
    /// Detailed status information for the license check scheduler
    /// </summary>
    public class LicenseCheckSchedulerStatusInfo
    {
        /// <summary>
        /// Gets the current scheduler status
        /// </summary>
        public LicenseCheckSchedulerStatus Status { get; set; }

        /// <summary>
        /// Gets the current check interval
        /// </summary>
        public TimeSpan CurrentInterval { get; set; }

        /// <summary>
        /// Gets the start time
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets the last check time
        /// </summary>
        public DateTime? LastCheckTime { get; set; }

        /// <summary>
        /// Gets the next scheduled check time
        /// </summary>
        public DateTime? NextScheduledCheck { get; set; }

        /// <summary>
        /// Gets the total number of checks performed
        /// </summary>
        public long TotalChecksPerformed { get; set; }

        /// <summary>
        /// Gets the current queue size
        /// </summary>
        public int CurrentQueueSize { get; set; }

        /// <summary>
        /// Gets the consecutive error count
        /// </summary>
        public int ConsecutiveErrorCount { get; set; }

        /// <summary>
        /// Gets the scheduler metrics
        /// </summary>
        public LicenseCheckSchedulerMetrics Metrics { get; set; }

        /// <summary>
        /// Gets the queue statistics
        /// </summary>
        public LicenseCheckQueueStatistics QueueStatistics { get; set; }

        /// <summary>
        /// Gets the configuration summary
        /// </summary>
        public string ConfigurationSummary { get; set; }

        /// <summary>
        /// Gets the timestamp when this status was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Gets a value indicating whether the scheduler is healthy
        /// </summary>
        public bool IsHealthy
        {
            get
            {
                return Status == LicenseCheckSchedulerStatus.Running &&
                       ConsecutiveErrorCount < 5 &&
                       (CurrentQueueSize < 1000); // Arbitrary threshold
            }
        }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckSchedulerStatusInfo class
        /// </summary>
        public LicenseCheckSchedulerStatusInfo()
        {
            GeneratedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a summary string of the status
        /// </summary>
        /// <returns>Summary string</returns>
        public string ToSummaryString()
        {
            return $"Status: {Status}, Queue: {CurrentQueueSize}, Errors: {ConsecutiveErrorCount}, Healthy: {IsHealthy}";
        }

        /// <summary>
        /// Returns a detailed string of the status
        /// </summary>
        /// <returns>Detailed string</returns>
        public string ToDetailedString()
        {
            var nextCheckText = NextScheduledCheck.HasValue ?
                $"\n  Next Scheduled Check: {NextScheduledCheck.Value:yyyy-MM-dd HH:mm:ss UTC}" :
                "\n  Next Scheduled Check: Not scheduled";

            return $@"LicenseCheckSchedulerStatusInfo:
  Status: {Status}
  Current Interval: {CurrentInterval.TotalMinutes:F1} minutes
  Start Time: {StartTime?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Not started"}
  Last Check Time: {LastCheckTime?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Never"}
  Total Checks Performed: {TotalChecksPerformed}
  Current Queue Size: {CurrentQueueSize}
  Consecutive Error Count: {ConsecutiveErrorCount}
  Is Healthy: {IsHealthy}{nextCheckText}
  Generated At: {GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}

  Configuration Summary: {ConfigurationSummary}

  Metrics:
{Metrics.ToDetailedString()}

  Queue Statistics:
{QueueStatistics.ToDetailedString()}";
        }
    }

    /// <summary>
    /// Diagnostic information for troubleshooting the license check scheduler
    /// </summary>
    public class LicenseCheckSchedulerDiagnostics
    {
        /// <summary>
        /// Gets the scheduler status information
        /// </summary>
        public LicenseCheckSchedulerStatusInfo Status { get; set; }

        /// <summary>
        /// Gets the configuration details
        /// </summary>
        public LicenseCheckSchedulerConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets the recent execution history
        /// </summary>
        public IReadOnlyList<LicenseCheckEventArgs> RecentExecutionHistory { get; set; }

        /// <summary>
        /// Gets the recent errors
        /// </summary>
        public IReadOnlyList<LicenseCheckErrorInfo> RecentErrors { get; set; }

        /// <summary>
        /// Gets the system information
        /// </summary>
        public SystemInfo System { get; set; }

        /// <summary>
        /// Gets the timestamp when diagnostics were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the LicenseCheckSchedulerDiagnostics class
        /// </summary>
        public LicenseCheckSchedulerDiagnostics()
        {
            GeneratedAt = DateTime.UtcNow;
            RecentExecutionHistory = new List<LicenseCheckEventArgs>().AsReadOnly();
            RecentErrors = new List<LicenseCheckErrorInfo>().AsReadOnly();
            System = new SystemInfo();
        }

        /// <summary>
        /// Returns a detailed string of the diagnostics
        /// </summary>
        /// <returns>Detailed string</returns>
        public string ToDetailedString()
        {
            var errorCount = RecentErrors?.Count ?? 0;
            var historyCount = RecentExecutionHistory?.Count ?? 0;

            return $@"LicenseCheckSchedulerDiagnostics:
  Generated At: {GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}
  Recent Execution History Items: {historyCount}
  Recent Errors: {errorCount}

  System Information:
{System.ToDetailedString()}

  Status:
{Status.ToDetailedString()}";
        }
    }

    /// <summary>
    /// System information for diagnostics
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// Gets the operating system version
        /// </summary>
        public string OSVersion { get; set; }

        /// <summary>
        /// Gets the .NET runtime version
        /// </summary>
        public string RuntimeVersion { get; set; }

        /// <summary>
        /// Gets the processor count
        /// </summary>
        public int ProcessorCount { get; set; }

        /// <summary>
        /// Gets the working set size in bytes
        /// </summary>
        public long WorkingSet { get; set; }

        /// <summary>
        /// Gets the system uptime
        /// </summary>
        public TimeSpan SystemUptime { get; set; }

        /// <summary>
        /// Gets the available memory in bytes
        /// </summary>
        public long AvailableMemory { get; set; }

        /// <summary>
        /// Gets the total memory in bytes
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets the current process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets the current thread count
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Initializes a new instance of the SystemInfo class
        /// </summary>
        public SystemInfo()
        {
            try
            {
                OSVersion = Environment.OSVersion.ToString();
                RuntimeVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                ProcessorCount = Environment.ProcessorCount;
                WorkingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
                ProcessId = Environment.ProcessId;
                ThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;

                // Get system uptime
                var uptimeMs = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
                SystemUptime = TimeSpan.FromMilliseconds(uptimeMs.TotalMilliseconds);

                // Get memory information
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                AvailableMemory = gcMemoryInfo.MemoryLoadBytes > 0 ? gcMemoryInfo.TotalAvailableMemoryBytes - gcMemoryInfo.MemoryLoadBytes : gcMemoryInfo.TotalAvailableMemoryBytes;
                TotalMemory = gcMemoryInfo.TotalAvailableMemoryBytes;
            }
            catch
            {
                // Fallback values if system information cannot be retrieved
                OSVersion = "Unknown";
                RuntimeVersion = "Unknown";
                ProcessorCount = 1;
                WorkingSet = 0;
                SystemUptime = TimeSpan.Zero;
                AvailableMemory = 0;
                TotalMemory = 0;
                ProcessId = Environment.ProcessId;
                ThreadCount = 1;
            }
        }

        /// <summary>
        /// Returns a detailed string of the system information
        /// </summary>
        /// <returns>Detailed string</returns>
        public string ToDetailedString()
        {
            return $@"SystemInfo:
  OS Version: {OSVersion}
  Runtime Version: {RuntimeVersion}
  Processor Count: {ProcessorCount}
  Process ID: {ProcessId}
  Thread Count: {ThreadCount}
  Working Set: {WorkingSet / (1024 * 1024):F1} MB
  Available Memory: {AvailableMemory / (1024 * 1024):F1} MB
  Total Memory: {TotalMemory / (1024 * 1024):F1} MB
  System Uptime: {SystemUptime.TotalHours:F1} hours";
        }
    }
}