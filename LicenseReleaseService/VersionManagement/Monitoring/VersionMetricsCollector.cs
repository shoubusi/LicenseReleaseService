using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LicenseReleaseService.VersionManagement.Monitoring
{
    /// <summary>
    /// Represents collected version metrics
    /// </summary>
    public class VersionMetrics
    {
        public string Version { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
        public Dictionary<string, int> ResourceMetrics { get; set; } = new();
        public Dictionary<string, bool> HealthMetrics { get; set; } = new();
        public int ActiveLicenses { get; set; }
        public int TotalLicenses { get; set; }
        public double LicenseUtilization { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public Dictionary<string, int> OperationCounts { get; set; } = new();
    }

    /// <summary>
    /// Represents metrics collection options
    /// </summary>
    public class VersionMetricsCollectionOptions
    {
        public bool Enabled { get; set; } = true;
        public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxHistorySize { get; set; } = 1000;
        public bool EnablePerformanceMetrics { get; set; } = true;
        public bool EnableResourceMetrics { get; set; } = true;
        public bool EnableHealthMetrics { get; set; } = true;
        public bool EnableOperationMetrics { get; set; } = true;
        public List<string> PerformanceCounters { get; set; } = new();
        public List<string> ResourceCounters { get; set; } = new();
        public Dictionary<string, double> Thresholds { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for metrics collection events
    /// </summary>
    public class VersionMetricsCollectedEventArgs : EventArgs
    {
        public string Version { get; set; } = string.Empty;
        public VersionMetrics Metrics { get; set; } = new();
        public DateTime CollectionTime { get; set; }
    }

    /// <summary>
    /// Event arguments for metrics threshold events
    /// </summary>
    public class VersionMetricsThresholdEventArgs : EventArgs
    {
        public string Version { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double ActualValue { get; set; }
        public double Threshold { get; set; }
        public bool IsWarning { get; set; }
        public bool IsCritical { get; set; }
        public DateTime DetectionTime { get; set; }
    }

    /// <summary>
    /// Handles metrics collection for version management
    /// </summary>
    public class VersionMetricsCollector : IDisposable
    {
        private readonly ILogger<VersionMetricsCollector> _logger;
        private readonly VersionMetricsCollectionOptions _options;
        private readonly ConcurrentDictionary<string, Queue<VersionMetrics>> _metricsHistory;
        private readonly Timer _collectionTimer;
        private readonly SemaphoreSlim _collectionSemaphore;
        private readonly object _syncLock = new();
        private bool _isDisposed;
        private bool _isCollecting;

        public event EventHandler<VersionMetricsCollectedEventArgs>? MetricsCollected;
        public event EventHandler<VersionMetricsThresholdEventArgs>? ThresholdExceeded;
        public event EventHandler<EventArgs>? CollectionStarted;
        public event EventHandler<EventArgs>? CollectionStopped;
        public event EventHandler<Exception>? CollectionError;

        public VersionMetricsCollector(
            ILogger<VersionMetricsCollector> logger,
            IOptions<VersionMetricsCollectionOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _metricsHistory = new ConcurrentDictionary<string, Queue<VersionMetrics>>();
            _collectionSemaphore = new SemaphoreSlim(1, 1);
            _collectionTimer = new Timer(OnCollectionTimer, null, Timeout.Infinite, Timeout.Infinite);

            InitializeDefaultCounters();
            InitializeDefaultThresholds();
        }

        public bool IsCollecting => _isCollecting;

        public IEnumerable<string> MonitoredVersions => _metricsHistory.Keys;

        public Dictionary<string, VersionMetrics> LatestMetrics { get; } = new();

        public async Task<VersionMetricsStartResult> StartCollectionAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionMetricsCollector));

            if (_isCollecting)
                return new VersionMetricsStartResult { Success = true, Message = "Metrics collection already running" };

            try
            {
                await _collectionSemaphore.WaitAsync().ConfigureAwait(false);

                if (_isCollecting)
                    return new VersionMetricsStartResult { Success = true, Message = "Metrics collection already running" };

                _isCollecting = true;
                _collectionTimer.Change(_options.CollectionInterval, _options.CollectionInterval);

                CollectionStarted?.Invoke(this, EventArgs.Empty);

                _logger.LogInformation("Metrics collection started with interval: {Interval}", _options.CollectionInterval);

                return new VersionMetricsStartResult
                {
                    Success = true,
                    Message = "Metrics collection started successfully",
                    CollectionInterval = _options.CollectionInterval
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting metrics collection");
                CollectionError?.Invoke(this, ex);
                return new VersionMetricsStartResult { Success = false, Error = ex.Message };
            }
            finally
            {
                _collectionSemaphore.Release();
            }
        }

        public async Task<VersionMetricsStopResult> StopCollectionAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionMetricsCollector));

            if (!_isCollecting)
                return new VersionMetricsStopResult { Success = true, Message = "Metrics collection not running" };

            try
            {
                await _collectionSemaphore.WaitAsync().ConfigureAwait(false);

                if (!_isCollecting)
                    return new VersionMetricsStopResult { Success = true, Message = "Metrics collection not running" };

                _isCollecting = false;
                _collectionTimer.Change(Timeout.Infinite, Timeout.Infinite);

                CollectionStopped?.Invoke(this, EventArgs.Empty);

                _logger.LogInformation("Metrics collection stopped");

                return new VersionMetricsStopResult
                {
                    Success = true,
                    Message = "Metrics collection stopped successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping metrics collection");
                CollectionError?.Invoke(this, ex);
                return new VersionMetricsStopResult { Success = false, Error = ex.Message };
            }
            finally
            {
                _collectionSemaphore.Release();
            }
        }

        public async Task<VersionMetricsCollectResult> CollectMetricsAsync(string version)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionMetricsCollector));

            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            try
            {
                var metrics = new VersionMetrics
                {
                    Version = version,
                    Timestamp = DateTime.UtcNow
                };

                if (_options.EnablePerformanceMetrics)
                {
                    await CollectPerformanceMetricsAsync(version, metrics).ConfigureAwait(false);
                }

                if (_options.EnableResourceMetrics)
                {
                    await CollectResourceMetricsAsync(version, metrics).ConfigureAwait(false);
                }

                if (_options.EnableHealthMetrics)
                {
                    await CollectHealthMetricsAsync(version, metrics).ConfigureAwait(false);
                }

                if (_options.EnableOperationMetrics)
                {
                    await CollectOperationMetricsAsync(version, metrics).ConfigureAwait(false);
                }

                // Calculate license utilization
                metrics.LicenseUtilization = metrics.TotalLicenses > 0
                    ? (double)metrics.ActiveLicenses / metrics.TotalLicenses
                    : 0;

                // Store metrics in history
                StoreMetricsInHistory(version, metrics);

                // Update latest metrics
                lock (_syncLock)
                {
                    LatestMetrics[version] = metrics;
                }

                // Check thresholds
                CheckThresholds(version, metrics);

                // Raise event
                MetricsCollected?.Invoke(this, new VersionMetricsCollectedEventArgs
                {
                    Version = version,
                    Metrics = metrics,
                    CollectionTime = DateTime.UtcNow
                });

                _logger.LogDebug("Collected metrics for version {Version}: {Metrics}", version, metrics);

                return new VersionMetricsCollectResult
                {
                    Success = true,
                    Metrics = metrics,
                    Message = "Metrics collected successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics for version {Version}", version);
                CollectionError?.Invoke(this, ex);
                return new VersionMetricsCollectResult { Success = false, Error = ex.Message };
            }
        }

        public VersionMetricsGetHistoryResult GetMetricsHistory(string version, TimeSpan? timeRange = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionMetricsCollector));

            if (!_metricsHistory.TryGetValue(version, out var history))
                return new VersionMetricsGetHistoryResult { Success = true, Metrics = new List<VersionMetrics>() };

            var cutoffTime = timeRange.HasValue
                ? DateTime.UtcNow.Subtract(timeRange.Value)
                : DateTime.MinValue;

            var filteredMetrics = history.Where(m => m.Timestamp >= cutoffTime).ToList();

            return new VersionMetricsGetHistoryResult
            {
                Success = true,
                Metrics = filteredMetrics
            };
        }

        public VersionMetricsGetSummaryResult GetMetricsSummary(string version)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionMetricsCollector));

            if (!_metricsHistory.TryGetValue(version, out var history) || !history.Any())
                return new VersionMetricsGetSummaryResult { Success = true, Summary = new Dictionary<string, double>() };

            var summary = new Dictionary<string, double>();

            // Calculate averages for performance metrics
            var performanceKeys = history.First().PerformanceMetrics.Keys;
            foreach (var key in performanceKeys)
            {
                var values = history.Select(m => m.PerformanceMetrics[key]).Where(v => !double.IsNaN(v));
                if (values.Any())
                {
                    summary[$"{key}_avg"] = values.Average();
                    summary[$"{key}_max"] = values.Max();
                    summary[$"{key}_min"] = values.Min();
                }
            }

            // Calculate error rates
            var totalOperations = history.Sum(m => m.OperationCounts.Values.Sum());
            if (totalOperations > 0)
            {
                var totalErrors = history.Sum(m => m.ErrorCount);
                summary["error_rate"] = (double)totalErrors / totalOperations;
            }

            return new VersionMetricsGetSummaryResult
            {
                Success = true,
                Summary = summary
            };
        }

        public void ClearMetricsHistory(string? version = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VersionMetricsCollector));

            if (string.IsNullOrWhiteSpace(version))
            {
                _metricsHistory.Clear();
                lock (_syncLock)
                {
                    LatestMetrics.Clear();
                }
                _logger.LogInformation("Cleared all metrics history");
            }
            else
            {
                _metricsHistory.TryRemove(version, out _);
                lock (_syncLock)
                {
                    LatestMetrics.Remove(version);
                }
                _logger.LogInformation("Cleared metrics history for version {Version}", version);
            }
        }

        private async void OnCollectionTimer(object? state)
        {
            if (!_isCollecting)
                return;

            try
            {
                foreach (var version in _metricsHistory.Keys.ToList())
                {
                    await CollectMetricsAsync(version).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics collection timer");
                CollectionError?.Invoke(this, ex);
            }
        }

        private async Task CollectPerformanceMetricsAsync(string version, VersionMetrics metrics)
        {
            try
            {
                var process = Process.GetCurrentProcess();

                // Collect performance counters
                foreach (var counter in _options.PerformanceCounters)
                {
                    try
                    {
                        switch (counter.ToLower())
                        {
                            case "cpu":
                                metrics.PerformanceMetrics["cpu_usage"] = process.TotalProcessorTime.TotalMilliseconds;
                                break;
                            case "memory":
                                metrics.PerformanceMetrics["memory_usage_mb"] = process.WorkingSet64 / (1024.0 * 1024.0);
                                break;
                            case "handles":
                                metrics.PerformanceMetrics["handle_count"] = process.HandleCount;
                                break;
                            case "threads":
                                metrics.PerformanceMetrics["thread_count"] = process.Threads.Count;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error collecting performance counter {Counter} for version {Version}", counter, version);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting performance metrics for version {Version}", version);
                throw;
            }
        }

        private async Task CollectResourceMetricsAsync(string version, VersionMetrics metrics)
        {
            try
            {
                var process = Process.GetCurrentProcess();

                // Collect resource counters
                foreach (var counter in _options.ResourceCounters)
                {
                    try
                    {
                        switch (counter.ToLower())
                        {
                            case "gdi":
                                metrics.ResourceMetrics["gdi_objects"] = GetGdiObjectCount();
                                break;
                            case "user":
                                metrics.ResourceMetrics["user_objects"] = GetUserObjectCount();
                                break;
                            case "memory":
                                metrics.ResourceMetrics["memory_bytes"] = (int)process.WorkingSet64;
                                break;
                            case "virtual_memory":
                                metrics.ResourceMetrics["virtual_memory_bytes"] = (int)process.VirtualMemorySize64;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error collecting resource counter {Counter} for version {Version}", counter, version);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting resource metrics for version {Version}", version);
                throw;
            }
        }

        private async Task CollectHealthMetricsAsync(string version, VersionMetrics metrics)
        {
            try
            {
                // Simulate health checks - in real implementation, these would be actual health checks
                metrics.HealthMetrics["service_healthy"] = true;
                metrics.HealthMetrics["database_connected"] = true;
                metrics.HealthMetrics["license_service_available"] = true;
                metrics.HealthMetrics["file_system_accessible"] = true;

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting health metrics for version {Version}", version);
                throw;
            }
        }

        private async Task CollectOperationMetricsAsync(string version, VersionMetrics metrics)
        {
            try
            {
                // Initialize operation counts if not present
                metrics.OperationCounts["deployments"] = 0;
                metrics.OperationCounts["activations"] = 0;
                metrics.OperationCounts["deactivations"] = 0;
                metrics.OperationCounts["health_checks"] = 0;
                metrics.OperationCounts["error_recoveries"] = 0;

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting operation metrics for version {Version}", version);
                throw;
            }
        }

        private void StoreMetricsInHistory(string version, VersionMetrics metrics)
        {
            var history = _metricsHistory.GetOrAdd(version, v => new Queue<VersionMetrics>(_options.MaxHistorySize));

            lock (history)
            {
                history.Enqueue(metrics);

                // Remove old metrics if history is too large
                while (history.Count > _options.MaxHistorySize)
                {
                    history.Dequeue();
                }
            }
        }

        private void CheckThresholds(string version, VersionMetrics metrics)
        {
            foreach (var threshold in _options.Thresholds)
            {
                double actualValue = 0;
                bool metricFound = false;

                // Check performance metrics
                if (metrics.PerformanceMetrics.TryGetValue(threshold.Key, out actualValue))
                {
                    metricFound = true;
                }
                // Check resource metrics (converted to double)
                else if (metrics.ResourceMetrics.TryGetValue(threshold.Key, out var resourceValue))
                {
                    actualValue = resourceValue;
                    metricFound = true;
                }
                // Check derived metrics
                else if (threshold.Key == "license_utilization")
                {
                    actualValue = metrics.LicenseUtilization;
                    metricFound = true;
                }
                else if (threshold.Key == "error_rate")
                {
                    var totalOps = metrics.OperationCounts.Values.Sum();
                    actualValue = totalOps > 0 ? (double)metrics.ErrorCount / totalOps : 0;
                    metricFound = true;
                }

                if (metricFound && actualValue > threshold.Value)
                {
                    var isCritical = actualValue > threshold.Value * 1.5;
                    var isWarning = !isCritical && actualValue > threshold.Value * 1.2;

                    ThresholdExceeded?.Invoke(this, new VersionMetricsThresholdEventArgs
                    {
                        Version = version,
                        MetricName = threshold.Key,
                        ActualValue = actualValue,
                        Threshold = threshold.Value,
                        IsWarning = isWarning,
                        IsCritical = isCritical,
                        DetectionTime = DateTime.UtcNow
                    });

                    _logger.LogWarning(
                        isCritical ? LogLevel.Critical : LogLevel.Warning,
                        "Threshold exceeded for version {Version}: {Metric} = {Value} (threshold: {Threshold})",
                        version, threshold.Key, actualValue, threshold.Value);
                }
            }
        }

        private void InitializeDefaultCounters()
        {
            if (!_options.PerformanceCounters.Any())
            {
                _options.PerformanceCounters.AddRange(new[] { "cpu", "memory", "handles", "threads" });
            }

            if (!_options.ResourceCounters.Any())
            {
                _options.ResourceCounters.AddRange(new[] { "gdi", "user", "memory", "virtual_memory" });
            }
        }

        private void InitializeDefaultThresholds()
        {
            if (!_options.Thresholds.Any())
            {
                _options.Thresholds["cpu_usage"] = 80.0;
                _options.Thresholds["memory_usage_mb"] = 1024.0;
                _options.Thresholds["license_utilization"] = 0.9;
                _options.Thresholds["error_rate"] = 0.05;
            }
        }

        private int GetGdiObjectCount()
        {
            // This would use Windows API in real implementation
            return 0;
        }

        private int GetUserObjectCount()
        {
            // This would use Windows API in real implementation
            return 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _collectionTimer?.Dispose();
                    _collectionSemaphore?.Dispose();
                }
                _isDisposed = true;
            }
        }
    }

    // Result classes
    public class VersionMetricsStartResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public TimeSpan? CollectionInterval { get; set; }
    }

    public class VersionMetricsStopResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class VersionMetricsCollectResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public VersionMetrics? Metrics { get; set; }
    }

    public class VersionMetricsGetHistoryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public List<VersionMetrics> Metrics { get; set; } = new();
    }

    public class VersionMetricsGetSummaryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public Dictionary<string, double> Summary { get; set; } = new();
    }
}