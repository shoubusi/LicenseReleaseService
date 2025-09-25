using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents metrics for a single process execution
    /// </summary>
    public class ProcessExecutionMetrics
    {
        /// <summary>
        /// Gets or sets the process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the command line that was executed
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start time of the process
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time of the process
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the execution duration
        /// </summary>
        public TimeSpan ExecutionDuration { get; set; }

        /// <summary>
        /// Gets or sets the exit code
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Gets or sets the output size in bytes
        /// </summary>
        public long OutputSize { get; set; }

        /// <summary>
        /// Gets or sets the error output size in bytes
        /// </summary>
        public long ErrorSize { get; set; }

        /// <summary>
        /// Gets or sets the peak memory usage in bytes
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the CPU time used by the process
        /// </summary>
        public TimeSpan CpuTime { get; set; }

        /// <summary>
        /// Gets or sets the number of retries attempted
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets whether the process timed out
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// Gets or sets whether the process was cancelled
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// Gets or sets the timeout duration that was configured
        /// </summary>
        public TimeSpan ConfiguredTimeout { get; set; }

        /// <summary>
        /// Gets or sets the operation type (e.g., "lmstat", "lmremove")
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server address (if applicable)
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server port (if applicable)
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the license feature (if applicable)
        /// </summary>
        public string Feature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user (if applicable)
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any custom tags for categorization
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>
        /// Gets a value indicating whether the execution was successful
        /// </summary>
        public bool Success => ExitCode == 0 && !TimedOut && !Cancelled;

        /// <summary>
        /// Gets the throughput ratio (execution time vs configured timeout)
        /// </summary>
        public double ThroughputRatio => ConfiguredTimeout.TotalMilliseconds > 0 ?
            ExecutionDuration.TotalMilliseconds / ConfiguredTimeout.TotalMilliseconds : 1.0;

        /// <summary>
        /// Gets the memory efficiency ratio (output size vs memory usage)
        /// </summary>
        public double MemoryEfficiency => PeakMemoryUsage > 0 ?
            (OutputSize + ErrorSize) / (double)PeakMemoryUsage : 0.0;
    }

    /// <summary>
    /// Aggregated metrics for multiple process executions
    /// </summary>
    public class ProcessMetricsSummary
    {
        /// <summary>
        /// Gets or sets the total number of executions
        /// </summary>
        public int TotalExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of successful executions
        /// </summary>
        public int SuccessfulExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of failed executions
        /// </summary>
        public int FailedExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of timeouts
        /// </summary>
        public int TimeoutCount { get; set; }

        /// <summary>
        /// Gets or sets the number of cancellations
        /// </summary>
        public int CancellationCount { get; set; }

        /// <summary>
        /// Gets or sets the average execution time
        /// </summary>
        public TimeSpan AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the minimum execution time
        /// </summary>
        public TimeSpan MinExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum execution time
        /// </summary>
        public TimeSpan MaxExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the total memory used across all executions
        /// </summary>
        public long TotalMemoryUsed { get; set; }

        /// <summary>
        /// Gets or sets the average memory used per execution
        /// </summary>
        public long AverageMemoryUsed { get; set; }

        /// <summary>
        /// Gets or sets the success rate as a percentage
        /// </summary>
        public double SuccessRate => TotalExecutions > 0 ?
            (SuccessfulExecutions / (double)TotalExecutions) * 100 : 0.0;

        /// <summary>
        /// Gets or sets the timeout rate as a percentage
        /// </summary>
        public double TimeoutRate => TotalExecutions > 0 ?
            (TimeoutCount / (double)TotalExecutions) * 100 : 0.0;

        /// <summary>
        /// Gets or sets the cancellation rate as a percentage
        /// </summary>
        public double CancellationRate => TotalExecutions > 0 ?
            (CancellationCount / (double)TotalExecutions) * 100 : 0.0;

        /// <summary>
        /// Gets or sets the time window for these metrics
        /// </summary>
        public TimeSpan TimeWindow { get; set; }

        /// <summary>
        /// Gets or sets the start time of the metric collection
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time of the metric collection
        /// </summary>
        public DateTime EndTime { get; set; }
    }

    /// <summary>
    /// Tracks and manages process execution metrics
    /// </summary>
    public class ProcessMetrics : IDisposable
    {
        private readonly ILogger<ProcessMetrics> _logger;
        private readonly List<ProcessExecutionMetrics> _recentMetrics;
        private readonly Dictionary<string, ProcessMetricsSummary> _operationSummaries;
        private readonly int _maxMetricsToKeep;
        private readonly SemaphoreSlim _lock;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the ProcessMetrics class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="maxMetricsToKeep">Maximum number of metrics to keep in memory</param>
        public ProcessMetrics(ILogger<ProcessMetrics> logger, int maxMetricsToKeep = 1000)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxMetricsToKeep = maxMetricsToKeep > 0 ? maxMetricsToKeep : 1000;
            _recentMetrics = new List<ProcessExecutionMetrics>(_maxMetricsToKeep);
            _operationSummaries = new Dictionary<string, ProcessMetricsSummary>();
            _lock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Records metrics for a process execution
        /// </summary>
        /// <param name="metrics">The process execution metrics</param>
        public async Task RecordExecutionAsync(ProcessExecutionMetrics metrics)
        {
            if (metrics == null)
            {
                throw new ArgumentNullException(nameof(metrics));
            }

            await _lock.WaitAsync();
            try
            {
                // Add to recent metrics
                _recentMetrics.Add(metrics);

                // Keep only the most recent metrics
                while (_recentMetrics.Count > _maxMetricsToKeep)
                {
                    _recentMetrics.RemoveAt(0);
                }

                // Update operation summary
                var operationKey = $"{metrics.OperationType}_{metrics.Server}_{metrics.Port}";
                if (!_operationSummaries.ContainsKey(operationKey))
                {
                    _operationSummaries[operationKey] = new ProcessMetricsSummary
                    {
                        StartTime = DateTime.Now
                    };
                }

                var summary = _operationSummaries[operationKey];
                summary.TotalExecutions++;
                summary.EndTime = DateTime.Now;

                if (metrics.Success)
                {
                    summary.SuccessfulExecutions++;
                }
                else
                {
                    summary.FailedExecutions++;
                }

                if (metrics.TimedOut)
                {
                    summary.TimeoutCount++;
                }

                if (metrics.Cancelled)
                {
                    summary.CancellationCount++;
                }

                // Update execution time statistics
                if (summary.TotalExecutions == 1)
                {
                    summary.AverageExecutionTime = metrics.ExecutionDuration;
                    summary.MinExecutionTime = metrics.ExecutionDuration;
                    summary.MaxExecutionTime = metrics.ExecutionDuration;
                }
                else
                {
                    var totalExecTime = summary.AverageExecutionTime.TotalMilliseconds * (summary.TotalExecutions - 1);
                    summary.AverageExecutionTime = TimeSpan.FromMilliseconds(
                        (totalExecTime + metrics.ExecutionDuration.TotalMilliseconds) / summary.TotalExecutions);

                    if (metrics.ExecutionDuration < summary.MinExecutionTime)
                    {
                        summary.MinExecutionTime = metrics.ExecutionDuration;
                    }

                    if (metrics.ExecutionDuration > summary.MaxExecutionTime)
                    {
                        summary.MaxExecutionTime = metrics.ExecutionDuration;
                    }
                }

                // Update memory statistics
                summary.TotalMemoryUsed += metrics.PeakMemoryUsage;
                summary.AverageMemoryUsed = summary.TotalMemoryUsed / summary.TotalExecutions;

                summary.TimeWindow = summary.EndTime - summary.StartTime;

                _logger.LogDebug("Recorded process execution metrics: {OperationType} on {Server}:{Port} - Success: {Success}, Duration: {Duration}ms",
                    metrics.OperationType, metrics.Server, metrics.Port, metrics.Success, metrics.ExecutionDuration.TotalMilliseconds);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets the most recent process execution metrics
        /// </summary>
        /// <param name="count">Number of recent metrics to retrieve</param>
        /// <returns>List of recent metrics</returns>
        public async Task<List<ProcessExecutionMetrics>> GetRecentMetricsAsync(int count = 100)
        {
            await _lock.WaitAsync();
            try
            {
                var actualCount = Math.Min(count, _recentMetrics.Count);
                return _recentMetrics.Skip(_recentMetrics.Count - actualCount).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets metrics summary for a specific operation
        /// </summary>
        /// <param name="operationType">The operation type</param>
        /// <param name="server">The server address</param>
        /// <param name="port">The server port</param>
        /// <returns>Metrics summary for the operation</returns>
        public async Task<ProcessMetricsSummary?> GetOperationSummaryAsync(string operationType, string server, int port)
        {
            await _lock.WaitAsync();
            try
            {
                var key = $"{operationType}_{server}_{port}";
                return _operationSummaries.TryGetValue(key, out var summary) ? summary : null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets all operation summaries
        /// </summary>
        /// <returns>Dictionary of operation summaries</returns>
        public async Task<Dictionary<string, ProcessMetricsSummary>> GetAllOperationSummariesAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return new Dictionary<string, ProcessMetricsSummary>(_operationSummaries);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets metrics filtered by time range
        /// </summary>
        /// <param name="startTime">Start time of the range</param>
        /// <param name="endTime">End time of the range</param>
        /// <returns>List of metrics within the time range</returns>
        public async Task<List<ProcessExecutionMetrics>> GetMetricsByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            await _lock.WaitAsync();
            try
            {
                return _recentMetrics.Where(m => m.StartTime >= startTime && m.StartTime <= endTime).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Clears all metrics and summaries
        /// </summary>
        public async Task ClearAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _recentMetrics.Clear();
                _operationSummaries.Clear();
                _logger.LogInformation("Process metrics cleared");
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Disposes the process metrics tracker
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _lock?.Dispose();
                _disposed = true;
            }
        }
    }
}