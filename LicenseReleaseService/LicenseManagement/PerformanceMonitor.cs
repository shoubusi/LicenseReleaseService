using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents a performance threshold configuration
    /// </summary>
    public class PerformanceThreshold
    {
        /// <summary>
        /// Gets or sets the threshold name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the warning threshold value
        /// </summary>
        public double WarningThreshold { get; set; }

        /// <summary>
        /// Gets or sets the critical threshold value
        /// </summary>
        public double CriticalThreshold { get; set; }

        /// <summary>
        /// Gets or sets the metric type (e.g., "ExecutionTime", "MemoryUsage", "SuccessRate")
        /// </summary>
        public string MetricType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the operation type this threshold applies to
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the evaluation window (time period to evaluate over)
        /// </summary>
        public TimeSpan EvaluationWindow { get; set; }

        /// <summary>
        /// Gets or sets whether this threshold is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the comparison operator ("greater", "less", "equals")
        /// </summary>
        public string ComparisonOperator { get; set; } = "greater";
    }

    /// <summary>
    /// Represents a performance alert
    /// </summary>
    public class PerformanceAlert
    {
        /// <summary>
        /// Gets or sets the alert ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the alert timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the alert severity (Info, Warning, Critical)
        /// </summary>
        public string Severity { get; set; } = "Warning";

        /// <summary>
        /// Gets or sets the alert title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the alert message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the metric name
        /// </summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the metric type
        /// </summary>
        public string MetricType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current value
        /// </summary>
        public double CurrentValue { get; set; }

        /// <summary>
        /// Gets or sets the threshold value
        /// </summary>
        public double ThresholdValue { get; set; }

        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server address
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the alert tags
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>
        /// Gets or sets whether the alert is acknowledged
        /// </summary>
        public bool Acknowledged { get; set; }

        /// <summary>
        /// Gets or sets the acknowledgment timestamp
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>
        /// Gets or sets the acknowledgment user
        /// </summary>
        public string AcknowledgedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents system performance counters
    /// </summary>
    public class SystemPerformanceCounters
    {
        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// Gets or sets the available memory in bytes
        /// </summary>
        public long AvailableMemory { get; set; }

        /// <summary>
        /// Gets or sets the total memory in bytes
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets or sets the memory usage percentage
        /// </summary>
        public double MemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the number of active processes
        /// </summary>
        public int ActiveProcesses { get; set; }

        /// <summary>
        /// Gets or sets the number of active threads
        /// </summary>
        public int ActiveThreads { get; set; }

        /// <summary>
        /// Gets or sets the handle count
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// Gets or sets the disk usage percentage
        /// </summary>
        public double DiskUsage { get; set; }

        /// <summary>
        /// Gets or sets the network I/O bytes
        /// </summary>
        public long NetworkBytes { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the measurement
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Monitors system and application performance in real-time
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly ProcessMetrics _processMetrics;
        private readonly List<PerformanceThreshold> _thresholds;
        private readonly ConcurrentBag<PerformanceAlert> _alerts;
        private readonly ConcurrentDictionary<string, SystemPerformanceCounters> _systemMetrics;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _monitoringTask;
        private readonly TimeSpan _monitoringInterval;
        private readonly TimeSpan _systemMetricsInterval;
        private bool _disposed;

        /// <summary>
        /// Event raised when a performance alert is triggered
        /// </summary>
        public event EventHandler<PerformanceAlert>? AlertRaised;

        /// <summary>
        /// Initializes a new instance of the PerformanceMonitor class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="processMetrics">The process metrics tracker</param>
        /// <param name="thresholds">Performance thresholds</param>
        /// <param name="monitoringInterval">Interval for performance monitoring</param>
        /// <param name="systemMetricsInterval">Interval for system metrics collection</param>
        public PerformanceMonitor(
            ILogger<PerformanceMonitor> logger,
            ProcessMetrics processMetrics,
            IEnumerable<PerformanceThreshold>? thresholds = null,
            TimeSpan? monitoringInterval = null,
            TimeSpan? systemMetricsInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processMetrics = processMetrics ?? throw new ArgumentNullException(nameof(processMetrics));
            _thresholds = thresholds?.ToList() ?? GetDefaultThresholds();
            _alerts = new ConcurrentBag<PerformanceAlert>();
            _systemMetrics = new ConcurrentDictionary<string, SystemPerformanceCounters>();
            _cancellationTokenSource = new CancellationTokenSource();
            _monitoringInterval = monitoringInterval ?? TimeSpan.FromSeconds(30);
            _systemMetricsInterval = systemMetricsInterval ?? TimeSpan.FromMinutes(1);

            // Start the monitoring task
            _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cancellationTokenSource.Token));

            _logger.LogInformation("Performance monitor initialized with {ThresholdCount} thresholds", _thresholds.Count);
        }

        /// <summary>
        /// Gets the current performance alerts
        /// </summary>
        /// <returns>List of active alerts</returns>
        public IReadOnlyList<PerformanceAlert> GetCurrentAlerts()
        {
            return _alerts.Where(a => !a.Acknowledged).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets all performance alerts
        /// </summary>
        /// <returns>List of all alerts</returns>
        public IReadOnlyList<PerformanceAlert> GetAllAlerts()
        {
            return _alerts.ToList().AsReadOnly();
        }

        /// <summary>
        /// Acknowledges an alert
        /// </summary>
        /// <param name="alertId">The alert ID</param>
        /// <param name="user">The user acknowledging the alert</param>
        public void AcknowledgeAlert(string alertId, string user)
        {
            var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
            if (alert != null)
            {
                alert.Acknowledged = true;
                alert.AcknowledgedAt = DateTime.Now;
                alert.AcknowledgedBy = user;
                _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alertId, user);
            }
        }

        /// <summary>
        /// Clears acknowledged alerts
        /// </summary>
        public void ClearAcknowledgedAlerts()
        {
            var clearedCount = _alerts.Where(a => a.Acknowledged).Count();
            _alerts.Clear();
            _logger.LogInformation("Cleared {Count} acknowledged alerts", clearedCount);
        }

        /// <summary>
        /// Gets the current system performance counters
        /// </summary>
        /// <returns>System performance counters</returns>
        public SystemPerformanceCounters GetSystemPerformanceCounters()
        {
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var counters = new SystemPerformanceCounters
                {
                    Timestamp = DateTime.Now,
                    ActiveProcesses = System.Diagnostics.Process.GetProcesses().Length,
                    ActiveThreads = process.Threads.Count,
                    HandleCount = process.HandleCount
                };

                // Get memory information
                var memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                counters.AvailableMemory = (long)(memoryCounter.NextValue() * 1024 * 1024);

                // Get CPU usage
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call returns 0, need to call again
                Thread.Sleep(1000);
                counters.CpuUsage = cpuCounter.NextValue();

                // Calculate memory usage percentage
                var totalMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
                counters.TotalMemory = (long)totalMemory;
                counters.MemoryUsage = ((totalMemory - (ulong)counters.AvailableMemory) / (double)totalMemory) * 100;

                return counters;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system performance counters");
                return new SystemPerformanceCounters();
            }
        }

        /// <summary>
        /// Adds a new performance threshold
        /// </summary>
        /// <param name="threshold">The threshold to add</param>
        public void AddThreshold(PerformanceThreshold threshold)
        {
            _thresholds.Add(threshold);
            _logger.LogInformation("Added performance threshold: {Name} for {OperationType}", threshold.Name, threshold.OperationType);
        }

        /// <summary>
        /// Removes a performance threshold
        /// </summary>
        /// <param name="thresholdName">The threshold name to remove</param>
        public void RemoveThreshold(string thresholdName)
        {
            var threshold = _thresholds.FirstOrDefault(t => t.Name == thresholdName);
            if (threshold != null)
            {
                _thresholds.Remove(threshold);
                _logger.LogInformation("Removed performance threshold: {Name}", thresholdName);
            }
        }

        /// <summary>
        /// Gets historical system metrics
        /// </summary>
        /// <param name="startTime">Start time of the range</param>
        /// <param name="endTime">End time of the range</param>
        /// <returns>List of system metrics</returns>
        public List<SystemPerformanceCounters> GetHistoricalSystemMetrics(DateTime startTime, DateTime endTime)
        {
            return _systemMetrics.Values
                .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
                .OrderBy(m => m.Timestamp)
                .ToList();
        }

        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Collect system metrics
                    CollectSystemMetrics();

                    // Evaluate performance thresholds
                    await EvaluateThresholdsAsync(cancellationToken);

                    // Wait for the next monitoring interval
                    await Task.Delay(_monitoringInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Task was cancelled, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in performance monitoring loop");
                    await Task.Delay(_monitoringInterval, cancellationToken);
                }
            }
        }

        private void CollectSystemMetrics()
        {
            try
            {
                var counters = GetSystemPerformanceCounters();
                var key = counters.Timestamp.ToString("yyyyMMddHHmmss");
                _systemMetrics[key] = counters;

                // Keep only recent metrics (last 24 hours)
                var cutoffTime = DateTime.Now.AddHours(-24);
                var keysToRemove = _systemMetrics.Keys
                    .Where(k => DateTime.TryParseExact(k, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var dt) && dt < cutoffTime)
                    .ToList();

                foreach (var keyToRemove in keysToRemove)
                {
                    _systemMetrics.TryRemove(keyToRemove, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect system metrics");
            }
        }

        private async Task EvaluateThresholdsAsync(CancellationToken cancellationToken)
        {
            foreach (var threshold in _thresholds.Where(t => t.Enabled))
            {
                try
                {
                    await EvaluateThresholdAsync(threshold, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to evaluate threshold {ThresholdName}", threshold.Name);
                }
            }
        }

        private async Task EvaluateThresholdAsync(PerformanceThreshold threshold, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var startTime = now - threshold.EvaluationWindow;

            // Get recent metrics for the operation type
            var recentMetrics = await _processMetrics.GetMetricsByTimeRangeAsync(startTime, now);
            var operationMetrics = recentMetrics.Where(m => m.OperationType == threshold.OperationType).ToList();

            if (operationMetrics.Count == 0)
            {
                return; // No metrics to evaluate
            }

            double currentValue = 0;
            switch (threshold.MetricType.ToLower())
            {
                case "executiontime":
                    currentValue = operationMetrics.Average(m => m.ExecutionDuration.TotalMilliseconds);
                    break;
                case "memoryusage":
                    currentValue = operationMetrics.Average(m => m.PeakMemoryUsage);
                    break;
                case "successrate":
                    currentValue = operationMetrics.Count(m => m.Success) / (double)operationMetrics.Count * 100;
                    break;
                case "timeoutrate":
                    currentValue = operationMetrics.Count(m => m.TimedOut) / (double)operationMetrics.Count * 100;
                    break;
                default:
                    _logger.LogWarning("Unknown metric type: {MetricType}", threshold.MetricType);
                    return;
            }

            // Check against thresholds
            bool thresholdExceeded = threshold.ComparisonOperator.ToLower() switch
            {
                "greater" => currentValue > threshold.CriticalThreshold,
                "less" => currentValue < threshold.CriticalThreshold,
                "equals" => Math.Abs(currentValue - threshold.CriticalThreshold) < 0.001,
                _ => currentValue > threshold.CriticalThreshold
            };

            bool warningExceeded = threshold.ComparisonOperator.ToLower() switch
            {
                "greater" => currentValue > threshold.WarningThreshold,
                "less" => currentValue < threshold.WarningThreshold,
                "equals" => Math.Abs(currentValue - threshold.WarningThreshold) < 0.001,
                _ => currentValue > threshold.WarningThreshold
            };

            if (thresholdExceeded)
            {
                var alert = new PerformanceAlert
                {
                    Severity = "Critical",
                    Title = $"{threshold.MetricType} threshold exceeded for {threshold.OperationType}",
                    Message = $"{threshold.MetricType} ({currentValue:F2}) has exceeded critical threshold ({threshold.CriticalThreshold})",
                    MetricName = threshold.MetricType,
                    CurrentValue = currentValue,
                    ThresholdValue = threshold.CriticalThreshold,
                    OperationType = threshold.OperationType,
                    Tags = new Dictionary<string, string>
                    {
                        { "ThresholdName", threshold.Name },
                        { "MetricType", threshold.MetricType },
                        { "EvaluationWindow", threshold.EvaluationWindow.ToString() }
                    }
                };

                _alerts.Add(alert);
                AlertRaised?.Invoke(this, alert);

                _logger.LogWarning("Critical performance alert: {AlertMessage}", alert.Message);
            }
            else if (warningExceeded)
            {
                var alert = new PerformanceAlert
                {
                    Severity = "Warning",
                    Title = $"{threshold.MetricType} warning threshold exceeded for {threshold.OperationType}",
                    Message = $"{threshold.MetricType} ({currentValue:F2}) has exceeded warning threshold ({threshold.WarningThreshold})",
                    MetricName = threshold.MetricType,
                    CurrentValue = currentValue,
                    ThresholdValue = threshold.WarningThreshold,
                    OperationType = threshold.OperationType,
                    Tags = new Dictionary<string, string>
                    {
                        { "ThresholdName", threshold.Name },
                        { "MetricType", threshold.MetricType },
                        { "EvaluationWindow", threshold.EvaluationWindow.ToString() }
                    }
                };

                _alerts.Add(alert);
                AlertRaised?.Invoke(this, alert);

                _logger.LogInformation("Warning performance alert: {AlertMessage}", alert.Message);
            }
        }

        private List<PerformanceThreshold> GetDefaultThresholds()
        {
            return new List<PerformanceThreshold>
            {
                new PerformanceThreshold
                {
                    Name = "HighExecutionTime",
                    MetricType = "ExecutionTime",
                    OperationType = "lmstat",
                    WarningThreshold = 5000, // 5 seconds
                    CriticalThreshold = 10000, // 10 seconds
                    EvaluationWindow = TimeSpan.FromMinutes(5),
                    ComparisonOperator = "greater"
                },
                new PerformanceThreshold
                {
                    Name = "LowSuccessRate",
                    MetricType = "SuccessRate",
                    OperationType = "lmstat",
                    WarningThreshold = 90, // 90%
                    CriticalThreshold = 80, // 80%
                    EvaluationWindow = TimeSpan.FromMinutes(15),
                    ComparisonOperator = "less"
                },
                new PerformanceThreshold
                {
                    Name = "HighTimeoutRate",
                    MetricType = "TimeoutRate",
                    OperationType = "lmstat",
                    WarningThreshold = 5, // 5%
                    CriticalThreshold = 10, // 10%
                    EvaluationWindow = TimeSpan.FromMinutes(10),
                    ComparisonOperator = "greater"
                }
            };
        }

        /// <summary>
        /// Disposes the performance monitor
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    _monitoringTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                    // Ignore cancellation exceptions
                }
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }
    }
}