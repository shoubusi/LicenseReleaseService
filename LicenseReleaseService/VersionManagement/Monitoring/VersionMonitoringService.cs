using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.VersionManagement.Monitoring
{
    /// <summary>
    /// Provides comprehensive monitoring services for multi-version SolidWorks deployments including
    /// health monitoring, performance tracking, availability monitoring, and integration with existing monitoring infrastructure
    /// </summary>
    public class VersionMonitoringService : IDisposable
    {
        private readonly ILogger<VersionMonitoringService> _logger;
        private readonly SolidWorksVersionDetector _versionDetector;
        private readonly VersionHealthMonitor _healthMonitor;
        private readonly VersionMetricsCollector _metricsCollector;
        private readonly VersionAlertManager _alertManager;
        private readonly VersionMonitoringConfiguration _configuration;
        private readonly Dictionary<string, VersionMonitoringContext> _monitoringContexts;
        private readonly System.Timers.Timer _monitoringTimer;
        private readonly SemaphoreSlim _monitoringSemaphore;
        private readonly object _serviceLock = new object();
        private bool _disposed;
        private bool _isMonitoring;

        /// <summary>
        /// Gets the current monitoring status for all versions
        /// </summary>
        public IReadOnlyDictionary<string, VersionMonitoringStatus> MonitoringStatus
        {
            get
            {
                lock (_serviceLock)
                {
                    return new Dictionary<string, VersionMonitoringStatus>(
                        _monitoringContexts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Status));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether monitoring is currently active
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        /// <summary>
        /// Event raised when monitoring status changes
        /// </summary>
        public event EventHandler<VersionMonitoringEventArgs> MonitoringStatusChanged;

        /// <summary>
        /// Event raised when a health issue is detected
        /// </summary>
        public event EventHandler<VersionHealthIssueEventArgs> HealthIssueDetected;

        /// <summary>
        /// Event raised when a performance threshold is exceeded
        /// </summary>
        public event EventHandler<VersionPerformanceEventArgs> PerformanceThresholdExceeded;

        /// <summary>
        /// Event raised when availability changes
        /// </summary>
        public event EventHandler<VersionAvailabilityEventArgs> AvailabilityChanged;

        /// <summary>
        /// Initializes a new instance of the VersionMonitoringService class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="versionDetector">Version detector instance</param>
        /// <param name="healthMonitor">Health monitor instance</param>
        /// <param name="metricsCollector">Metrics collector instance</param>
        /// <param name="alertManager">Alert manager instance</param>
        /// <param name="configuration">Monitoring configuration</param>
        public VersionMonitoringService(
            ILogger<VersionMonitoringService> logger,
            SolidWorksVersionDetector versionDetector,
            VersionHealthMonitor healthMonitor,
            VersionMetricsCollector metricsCollector,
            VersionAlertManager alertManager,
            VersionMonitoringConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionDetector = versionDetector ?? throw new ArgumentNullException(nameof(versionDetector));
            _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _alertManager = alertManager ?? throw new ArgumentNullException(nameof(alertManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _monitoringContexts = new Dictionary<string, VersionMonitoringContext>();
            _monitoringTimer = new System.Timers.Timer(_configuration.MonitoringInterval.TotalMilliseconds);
            _monitoringTimer.Elapsed += MonitoringTimer_Elapsed;
            _monitoringSemaphore = new SemaphoreSlim(_configuration.MaxConcurrentMonitoringOperations);

            // Wire up event handlers
            _healthMonitor.HealthStatusChanged += HealthMonitor_HealthStatusChanged;
            _healthMonitor.CriticalHealthIssueDetected += HealthMonitor_CriticalHealthIssueDetected;
            _alertManager.AlertRaised += AlertManager_AlertRaised;
        }

        /// <summary>
        /// Starts monitoring for all detected versions
        /// </summary>
        /// <returns>Monitoring start result</returns>
        public async Task<VersionMonitoringStartResult> StartMonitoringAsync()
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Version monitoring is already active");
                return new VersionMonitoringStartResult
                {
                    Success = false,
                    Errors = { "Monitoring is already active" }
                };
            }

            try
            {
                _logger.LogInformation("Starting version monitoring service");

                var result = new VersionMonitoringStartResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Start individual monitors
                await StartHealthMonitoringAsync(result);
                await StartMetricsCollectionAsync(result);
                await StartAlertManagementAsync(result);

                // Start the monitoring timer
                _monitoringTimer.Start();
                _isMonitoring = true;

                stopwatch.Stop();
                result.StartTime = stopwatch.Elapsed;
                result.MonitoredVersionCount = _monitoringContexts.Count;

                _logger.LogInformation("Version monitoring service started successfully in {Duration}ms. Monitoring {Count} versions",
                    stopwatch.ElapsedMilliseconds, result.MonitoredVersionCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start version monitoring service");
                return new VersionMonitoringStartResult
                {
                    Success = false,
                    Errors = { $"Start failed: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Stops monitoring for all versions
        /// </summary>
        /// <returns>Monitoring stop result</returns>
        public async Task<VersionMonitoringStopResult> StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                _logger.LogWarning("Version monitoring is not active");
                return new VersionMonitoringStopResult
                {
                    Success = false,
                    Errors = { "Monitoring is not active" }
                };
            }

            try
            {
                _logger.LogInformation("Stopping version monitoring service");

                var result = new VersionMonitoringStopResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Stop the monitoring timer
                _monitoringTimer.Stop();
                _isMonitoring = false;

                // Stop individual monitors
                await StopHealthMonitoringAsync(result);
                await StopMetricsCollectionAsync(result);
                await StopAlertManagementAsync(result);

                stopwatch.Stop();
                result.StopTime = stopwatch.Elapsed;
                result.StoppedVersionCount = _monitoringContexts.Count;

                _logger.LogInformation("Version monitoring service stopped successfully in {Duration}ms. Stopped monitoring {Count} versions",
                    stopwatch.ElapsedMilliseconds, result.StoppedVersionCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop version monitoring service");
                return new VersionMonitoringStopResult
                {
                    Success = false,
                    Errors = { $"Stop failed: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Forces an immediate monitoring cycle for all versions
        /// </summary>
        /// <returns>Monitoring cycle result</returns>
        public async Task<VersionMonitoringCycleResult> ForceMonitoringCycleAsync()
        {
            await _monitoringSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Forcing immediate monitoring cycle");

                var result = new VersionMonitoringCycleResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    await PerformMonitoringCycleAsync(result);
                    stopwatch.Stop();
                    result.CycleTime = stopwatch.Elapsed;

                    _logger.LogDebug("Forced monitoring cycle completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    result.CycleTime = stopwatch.Elapsed;
                    result.Success = false;
                    result.Error = ex.Message;
                    _logger.LogError(ex, "Error during forced monitoring cycle");
                    return result;
                }
            }
            finally
            {
                _monitoringSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets monitoring status for a specific version
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <returns>Monitoring status</returns>
        public VersionMonitoringStatus GetMonitoringStatus(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            lock (_serviceLock)
            {
                if (_monitoringContexts.TryGetValue(version, out var context))
                {
                    return context.Status;
                }

                return new VersionMonitoringStatus
                {
                    Version = version,
                    IsMonitored = false,
                    MonitoringState = MonitoringState.NotMonitored
                };
            }
        }

        /// <summary>
        /// Gets monitoring history for a specific version
        /// </summary>
        /// <param name="version">Version to get history for</param>
        /// <param name="maxEntries">Maximum number of entries to return</param>
        /// <returns>Monitoring history</returns>
        public List<VersionMonitoringHistoryRecord> GetMonitoringHistory(string version, int maxEntries = 100)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            lock (_serviceLock)
            {
                if (_monitoringContexts.TryGetValue(version, out var context))
                {
                    return context.GetMonitoringHistory(maxEntries);
                }
                return new List<VersionMonitoringHistoryRecord>();
            }
        }

        /// <summary>
        /// Gets monitoring statistics for all versions
        /// </summary>
        /// <returns>Monitoring statistics</returns>
        public VersionMonitoringStatistics GetMonitoringStatistics()
        {
            lock (_serviceLock)
            {
                var stats = new VersionMonitoringStatistics();

                foreach (var context in _monitoringContexts.Values)
                {
                    stats.TotalVersions++;

                    if (context.Status.IsMonitored)
                        stats.MonitoredVersions++;

                    if (context.Status.IsHealthy)
                        stats.HealthyVersions++;

                    if (context.Status.IsAvailable)
                        stats.AvailableVersions++;

                    if (context.Status.HasActiveAlerts)
                        stats.VersionsWithAlerts++;

                    stats.TotalMonitoringTime += context.Status.TotalMonitoringTime;
                    stats.TotalHealthChecks += context.Status.HealthCheckCount;
                    stats.TotalMetricsCollected += context.Status.MetricsCollectedCount;
                    stats.TotalAlertsRaised += context.Status.AlertsRaisedCount;
                }

                if (stats.MonitoredVersions > 0)
                {
                    stats.AverageMonitoringTime = TimeSpan.FromTicks(stats.TotalMonitoringTime.Ticks / stats.MonitoredVersions);
                    stats.HealthPercentage = (stats.HealthyVersions * 100.0) / stats.MonitoredVersions;
                    stats.AvailabilityPercentage = (stats.AvailableVersions * 100.0) / stats.MonitoredVersions;
                }

                return stats;
            }
        }

        /// <summary>
        /// Configures monitoring for a specific version
        /// </summary>
        /// <param name="version">Version to configure</param>
        /// <param name="monitoringConfig">Monitoring configuration</param>
        /// <returns>Configuration result</returns>
        public async Task<VersionMonitoringConfigurationResult> ConfigureVersionMonitoringAsync(
            string version,
            VersionMonitoringConfig monitoringConfig)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (monitoringConfig == null)
                throw new ArgumentNullException(nameof(monitoringConfig));

            try
            {
                _logger.LogDebug("Configuring monitoring for version {Version}", version);

                var result = new VersionMonitoringConfigurationResult { Version = version };

                lock (_serviceLock)
                {
                    if (!_monitoringContexts.TryGetValue(version, out var context))
                    {
                        result.Success = false;
                        result.Errors.Add($"No monitoring context found for version {version}");
                        return result;
                    }

                    context.Configure(monitoringConfig);
                }

                // Apply configuration changes
                await ApplyMonitoringConfigurationAsync(version, monitoringConfig, result);

                result.Success = result.Errors.Count == 0;

                _logger.LogDebug("Monitoring configuration completed for version {Version}. Success: {Success}",
                    version, result.Success);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring monitoring for version {Version}", version);
                return new VersionMonitoringConfigurationResult
                {
                    Version = version,
                    Success = false,
                    Errors = { $"Configuration error: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Gets the current health status for all versions
        /// </summary>
        /// <returns>Health status result</returns>
        public async Task<VersionHealthStatusResult> GetHealthStatusAsync()
        {
            try
            {
                var healthResult = await _healthMonitor.GetHealthStatusAsync();
                var result = new VersionHealthStatusResult();

                foreach (var versionHealth in healthResult.VersionHealth)
                {
                    result.VersionHealthStatuses[versionHealth.Version] = new VersionHealthSummary
                    {
                        Version = versionHealth.Version,
                        Health = versionHealth.Health,
                        IsHealthy = versionHealth.IsHealthy,
                        IsAvailable = versionHealth.IsAvailable,
                        LastCheck = versionHealth.LastCheck,
                        UptimePercentage = versionHealth.UptimePercentage,
                        HasIssues = versionHealth.HasIssues
                    };
                }

                result.OverallHealth = healthResult.OverallHealth;
                result.HealthyVersionCount = healthResult.HealthyVersionCount;
                result.TotalVersionCount = healthResult.TotalVersionCount;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health status");
                return new VersionHealthStatusResult
                {
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets the current metrics for all versions
        /// </summary>
        /// <returns>Metrics result</returns>
        public async Task<VersionMetricsResult> GetMetricsAsync()
        {
            try
            {
                var metricsResult = await _metricsCollector.GetMetricsAsync();
                var result = new VersionMetricsResult();

                foreach (var versionMetrics in metricsResult.VersionMetrics)
                {
                    result.VersionMetrics[versionMetrics.Key] = new VersionMetricSummary
                    {
                        Version = versionMetrics.Key,
                        Metrics = versionMetrics.Value,
                        Timestamp = DateTime.UtcNow,
                        Summary = GenerateMetricsSummary(versionMetrics.Value)
                    };
                }

                result.SystemMetrics = metricsResult.SystemMetrics;
                result.Timestamp = DateTime.UtcNow;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metrics");
                return new VersionMetricsResult
                {
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets the current alerts for all versions
        /// </summary>
        /// <returns>Alerts result</returns>
        public async Task<VersionAlertsResult> GetAlertsAsync()
        {
            try
            {
                var alertsResult = await _alertManager.GetAlertsAsync();
                var result = new VersionAlertsResult();

                foreach (var versionAlerts in alertsResult.VersionAlerts)
                {
                    result.VersionAlerts[versionAlerts.Key] = new VersionAlertSummary
                    {
                        Version = versionAlerts.Key,
                        Alerts = versionAlerts.Value,
                        Timestamp = DateTime.UtcNow,
                        ActiveAlertCount = versionAlerts.Value.Count(a => a.IsActive),
                        CriticalAlertCount = versionAlerts.Value.Count(a => a.Severity == AlertSeverity.Critical)
                    };
                }

                result.SystemAlerts = alertsResult.SystemAlerts;
                result.Timestamp = DateTime.UtcNow;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alerts");
                return new VersionAlertsResult
                {
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Disposes the monitoring service
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the monitoring service
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_isMonitoring)
                    {
                        StopMonitoringAsync().Wait();
                    }

                    _monitoringTimer?.Dispose();
                    _monitoringSemaphore?.Dispose();

                    // Unwire event handlers
                    _healthMonitor.HealthStatusChanged -= HealthMonitor_HealthStatusChanged;
                    _healthMonitor.CriticalHealthIssueDetected -= HealthMonitor_CriticalHealthIssueDetected;
                    _alertManager.AlertRaised -= AlertManager_AlertRaised;

                    lock (_serviceLock)
                    {
                        foreach (var context in _monitoringContexts.Values)
                        {
                            context.Dispose();
                        }
                        _monitoringContexts.Clear();
                    }
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Timer event handler for periodic monitoring
        /// </summary>
        private async void MonitoringTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (_monitoringSemaphore.Wait(0)) // Non-blocking wait
                {
                    try
                    {
                        var result = new VersionMonitoringCycleResult();
                        await PerformMonitoringCycleAsync(result);
                    }
                    finally
                    {
                        _monitoringSemaphore.Release();
                    }
                }
                else
                {
                    _logger.LogWarning("Monitoring cycle skipped - previous cycle still running");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled monitoring cycle");
            }
        }

        /// <summary>
        /// Performs a monitoring cycle for all versions
        /// </summary>
        /// <param name="result">Monitoring cycle result to update</param>
        private async Task PerformMonitoringCycleAsync(VersionMonitoringCycleResult result)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var versions = _versionDetector.GetAvailableVersions().ToList();
                var monitoringTasks = versions.Select(v => MonitorVersionAsync(v));

                var monitoringResults = await Task.WhenAll(monitoringTasks);

                // Aggregate results
                foreach (var monitoringResult in monitoringResults)
                {
                    result.VersionResults.Add(monitoringResult);

                    if (monitoringResult.HasIssues)
                    {
                        result.VersionsWithIssues++;
                    }

                    if (monitoringResult.HasAlerts)
                    {
                        result.VersionsWithAlerts++;
                    }

                    if (monitoringResult.IsUnhealthy)
                    {
                        result.UnhealthyVersions++;
                    }

                    if (!monitoringResult.IsAvailable)
                    {
                        result.UnavailableVersions++;
                    }
                }

                stopwatch.Stop();
                result.CycleTime = stopwatch.Elapsed;
                result.Success = true;

                _logger.LogDebug("Monitoring cycle completed in {Duration}ms. Issues: {IssuesCount}, Alerts: {AlertsCount}, Unhealthy: {UnhealthyCount}, Unavailable: {UnavailableCount}",
                    stopwatch.ElapsedMilliseconds, result.VersionsWithIssues, result.VersionsWithAlerts, result.UnhealthyVersions, result.UnavailableVersions);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.CycleTime = stopwatch.Elapsed;
                result.Success = false;
                result.Error = ex.Message;
                _logger.LogError(ex, "Error during monitoring cycle");
            }
        }

        /// <summary>
        /// Monitors a specific version
        /// </summary>
        /// <param name="version">Version to monitor</param>
        /// <returns>Version monitoring result</returns>
        private async Task<VersionMonitoringResult> MonitorVersionAsync(SolidWorksVersionInfo version)
        {
            var result = new VersionMonitoringResult { Version = version.Version };

            try
            {
                // Get or create monitoring context
                var context = await GetOrCreateMonitoringContextAsync(version);
                if (context == null)
                {
                    result.Success = false;
                    result.Errors.Add($"Failed to create monitoring context for version {version.Version}");
                    return result;
                }

                // Update monitoring status
                UpdateMonitoringStatus(version.Version, MonitoringState.Monitoring);

                // Perform health monitoring
                await MonitorVersionHealthAsync(version, context, result);

                // Collect metrics
                await CollectVersionMetricsAsync(version, context, result);

                // Check alerts
                await CheckVersionAlertsAsync(version, context, result);

                // Update monitoring status
                var finalState = result.IsAvailable && result.IsHealthy ? MonitoringState.Healthy : MonitoringState.IssuesDetected;
                UpdateMonitoringStatus(version.Version, finalState);

                result.Success = result.Errors.Count == 0;

                // Update monitoring history
                await UpdateMonitoringHistoryAsync(version, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring version {Version}", version.Version);
                result.Success = false;
                result.Errors.Add($"Monitoring error: {ex.Message}");
                UpdateMonitoringStatus(version.Version, MonitoringState.Error);
                return result;
            }
        }

        /// <summary>
        /// Monitors version health
        /// </summary>
        /// <param name="version">Version to monitor</param>
        /// <param name="context">Monitoring context</param>
        /// <param name="result">Monitoring result to update</param>
        private async Task MonitorVersionHealthAsync(SolidWorksVersionInfo version, VersionMonitoringContext context, VersionMonitoringResult result)
        {
            try
            {
                var healthResult = await _healthMonitor.ForceHealthCheckAsync(version.Version);

                result.IsHealthy = healthResult.IsHealthy;
                result.Health = healthResult.Health;
                result.IsAvailable = healthResult.IsAvailable;
                result.HasIssues = healthResult.HasIssues;

                if (healthResult.HasIssues)
                {
                    result.Issues.AddRange(healthResult.Issues);
                }

                if (healthResult.IsCritical)
                {
                    result.HasAlerts = true;
                    result.AlertLevel = AlertSeverity.Critical;
                }

                // Update context health status
                context.Status.Health = healthResult.Health;
                context.Status.IsHealthy = healthResult.IsHealthy;
                context.Status.IsAvailable = healthResult.IsAvailable;
                context.Status.HasIssues = healthResult.HasIssues;
                context.Status.HealthCheckCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring health for version {Version}", version.Version);
                result.Issues.Add($"Health monitoring error: {ex.Message}");
            }
        }

        /// <summary>
        /// Collects version metrics
        /// </summary>
        /// <param name="version">Version to collect metrics for</param>
        /// <param name="context">Monitoring context</param>
        /// <param name="result">Monitoring result to update</param>
        private async Task CollectVersionMetricsAsync(SolidWorksVersionInfo version, VersionMonitoringContext context, VersionMonitoringResult result)
        {
            try
            {
                var metrics = await _metricsCollector.CollectVersionMetricsAsync(version.Version);

                result.Metrics = metrics;
                result.PerformanceScore = CalculatePerformanceScore(metrics);

                // Check performance thresholds
                await CheckPerformanceThresholdsAsync(version, metrics, result);

                // Update context metrics
                context.Status.MetricsCollectedCount++;
                context.Status.LastMetricsCollected = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics for version {Version}", version.Version);
                result.Issues.Add($"Metrics collection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks version alerts
        /// </summary>
        /// <param name="version">Version to check alerts for</param>
        /// <param name="context">Monitoring context</param>
        /// <param name="result">Monitoring result to update</param>
        private async Task CheckVersionAlertsAsync(SolidWorksVersionInfo version, VersionMonitoringContext context, VersionMonitoringResult result)
        {
            try
            {
                var alerts = await _alertManager.GetVersionAlertsAsync(version.Version);

                result.HasAlerts = alerts.Any(a => a.IsActive);
                result.AlertCount = alerts.Count(a => a.IsActive);

                if (result.HasAlerts)
                {
                    var criticalAlerts = alerts.Where(a => a.IsActive && a.Severity == AlertSeverity.Critical).ToList();
                    if (criticalAlerts.Any())
                    {
                        result.AlertLevel = AlertSeverity.Critical;
                        result.HasCriticalAlerts = true;
                    }
                    else
                    {
                        result.AlertLevel = AlertSeverity.Warning;
                    }
                }

                // Update context alerts
                context.Status.HasActiveAlerts = result.HasAlerts;
                context.Status.AlertsRaisedCount += result.AlertCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alerts for version {Version}", version.Version);
                result.Issues.Add($"Alert checking error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks performance thresholds
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <param name="metrics">Metrics to check</param>
        /// <param name="result">Monitoring result to update</param>
        private async Task CheckPerformanceThresholdsAsync(SolidWorksVersionInfo version, Dictionary<string, object> metrics, VersionMonitoringResult result)
        {
            try
            {
                var thresholdExceeded = false;

                // Check CPU usage
                if (metrics.TryGetValue("CpuUsage", out var cpuUsageObj) && cpuUsageObj is double cpuUsage)
                {
                    if (cpuUsage > _configuration.CpuUsageThresholdPercentage)
                    {
                        result.Issues.Add($"High CPU usage: {cpuUsage:F1}%");
                        thresholdExceeded = true;
                    }
                }

                // Check memory usage
                if (metrics.TryGetValue("MemoryUsageMB", out var memoryUsageObj) && memoryUsageObj is double memoryUsage)
                {
                    if (memoryUsage > _configuration.MemoryUsageThresholdMB)
                    {
                        result.Issues.Add($"High memory usage: {memoryUsage:F1}MB");
                        thresholdExceeded = true;
                    }
                }

                // Check disk usage
                if (metrics.TryGetValue("DiskUsagePercentage", out var diskUsageObj) && diskUsageObj is double diskUsage)
                {
                    if (diskUsage > _configuration.DiskUsageThresholdPercentage)
                    {
                        result.Issues.Add($"High disk usage: {diskUsage:F1}%");
                        thresholdExceeded = true;
                    }
                }

                if (thresholdExceeded)
                {
                    await RaisePerformanceThresholdEventAsync(version.Version, metrics, result.Issues);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking performance thresholds for version {Version}", version.Version);
            }
        }

        /// <summary>
        /// Updates monitoring status for a version
        /// </summary>
        /// <param name="version">Version to update</param>
        /// <param name="state">New monitoring state</param>
        private void UpdateMonitoringStatus(string version, MonitoringState state)
        {
            lock (_serviceLock)
            {
                if (_monitoringContexts.TryGetValue(version, out var context))
                {
                    var previousState = context.Status.MonitoringState;
                    context.Status.MonitoringState = state;
                    context.Status.LastUpdated = DateTime.UtcNow;

                    // Raise event if status changed
                    if (previousState != state)
                    {
                        var eventArgs = new VersionMonitoringEventArgs
                        {
                            Version = version,
                            PreviousState = previousState,
                            CurrentState = state,
                            Timestamp = DateTime.UtcNow
                        };

                        MonitoringStatusChanged?.Invoke(this, eventArgs);
                    }
                }
            }
        }

        /// <summary>
        /// Updates monitoring history for a version
        /// </summary>
        /// <param name="version">Version to update</param>
        /// <param name="result">Monitoring result</param>
        private async Task UpdateMonitoringHistoryAsync(SolidWorksVersionInfo version, VersionMonitoringResult result)
        {
            try
            {
                lock (_serviceLock)
                {
                    if (_monitoringContexts.TryGetValue(version.Version, out var context))
                    {
                        var historyRecord = new VersionMonitoringHistoryRecord
                        {
                            Version = version.Version,
                            Timestamp = DateTime.UtcNow,
                            IsHealthy = result.IsHealthy,
                            IsAvailable = result.IsAvailable,
                            HasIssues = result.HasIssues,
                            HasAlerts = result.HasAlerts,
                            Health = result.Health,
                            PerformanceScore = result.PerformanceScore,
                            AlertLevel = result.AlertLevel,
                            Issues = result.Issues.ToList(),
                            Metrics = result.Metrics
                        };

                        context.AddMonitoringHistory(historyRecord);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating monitoring history for version {Version}", version.Version);
            }
        }

        /// <summary>
        /// Gets or creates monitoring context for a version
        /// </summary>
        /// <param name="version">Version to get context for</param>
        /// <returns>Monitoring context</returns>
        private async Task<VersionMonitoringContext> GetOrCreateMonitoringContextAsync(SolidWorksVersionInfo version)
        {
            lock (_serviceLock)
            {
                if (_monitoringContexts.TryGetValue(version.Version, out var context))
                {
                    return context;
                }
            }

            try
            {
                var context = new VersionMonitoringContext(version, _configuration);
                lock (_serviceLock)
                {
                    _monitoringContexts[version.Version] = context;
                }
                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating monitoring context for version {Version}", version.Version);
                return null;
            }
        }

        /// <summary>
        /// Starts health monitoring
        /// </summary>
        /// <param name="result">Start result to update</param>
        private async Task StartHealthMonitoringAsync(VersionMonitoringStartResult result)
        {
            try
            {
                await _healthMonitor.StartMonitoringAsync();
                _logger.LogDebug("Health monitoring started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start health monitoring");
                result.Errors.Add($"Health monitoring start failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts metrics collection
        /// </summary>
        /// <param name="result">Start result to update</param>
        private async Task StartMetricsCollectionAsync(VersionMonitoringStartResult result)
        {
            try
            {
                await _metricsCollector.StartCollectionAsync();
                _logger.LogDebug("Metrics collection started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start metrics collection");
                result.Errors.Add($"Metrics collection start failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts alert management
        /// </summary>
        /// <param name="result">Start result to update</param>
        private async Task StartAlertManagementAsync(VersionMonitoringStartResult result)
        {
            try
            {
                await _alertManager.StartAlertManagementAsync();
                _logger.LogDebug("Alert management started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start alert management");
                result.Errors.Add($"Alert management start failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops health monitoring
        /// </summary>
        /// <param name="result">Stop result to update</param>
        private async Task StopHealthMonitoringAsync(VersionMonitoringStopResult result)
        {
            try
            {
                _healthMonitor.StopMonitoring();
                _logger.LogDebug("Health monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop health monitoring");
                result.Errors.Add($"Health monitoring stop failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops metrics collection
        /// </summary>
        /// <param name="result">Stop result to update</param>
        private async Task StopMetricsCollectionAsync(VersionMonitoringStopResult result)
        {
            try
            {
                await _metricsCollector.StopCollectionAsync();
                _logger.LogDebug("Metrics collection stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop metrics collection");
                result.Errors.Add($"Metrics collection stop failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops alert management
        /// </summary>
        /// <param name="result">Stop result to update</param>
        private async Task StopAlertManagementAsync(VersionMonitoringStopResult result)
        {
            try
            {
                await _alertManager.StopAlertManagementAsync();
                _logger.LogDebug("Alert management stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop alert management");
                result.Errors.Add($"Alert management stop failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies monitoring configuration
        /// </summary>
        /// <param name="version">Version to configure</param>
        /// <param name="config">Configuration to apply</param>
        /// <param name="result">Configuration result to update</param>
        private async Task ApplyMonitoringConfigurationAsync(string version, VersionMonitoringConfig config, VersionMonitoringConfigurationResult result)
        {
            try
            {
                // Apply health monitoring configuration
                if (config.HealthMonitoringConfig != null)
                {
                    await _healthMonitor.ConfigureVersionMonitoringAsync(version, config.HealthMonitoringConfig);
                }

                // Apply metrics collection configuration
                if (config.MetricsCollectionConfig != null)
                {
                    await _metricsCollector.ConfigureVersionMetricsAsync(version, config.MetricsCollectionConfig);
                }

                // Apply alert management configuration
                if (config.AlertManagementConfig != null)
                {
                    await _alertManager.ConfigureVersionAlertsAsync(version, config.AlertManagementConfig);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying monitoring configuration for version {Version}", version);
                result.Errors.Add($"Configuration application error: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates performance score from metrics
        /// </summary>
        /// <param name="metrics">Metrics to calculate score from</param>
        /// <returns>Performance score (0-100)</returns>
        private double CalculatePerformanceScore(Dictionary<string, object> metrics)
        {
            try
            {
                var score = 100.0;
                var factors = 0;

                // CPU usage impact
                if (metrics.TryGetValue("CpuUsage", out var cpuUsageObj) && cpuUsageObj is double cpuUsage)
                {
                    score -= Math.Max(0, cpuUsage - 50) * 0.5; // Reduce score for CPU > 50%
                    factors++;
                }

                // Memory usage impact
                if (metrics.TryGetValue("MemoryUsagePercentage", out var memoryUsageObj) && memoryUsageObj is double memoryUsage)
                {
                    score -= Math.Max(0, memoryUsage - 70) * 0.3; // Reduce score for memory > 70%
                    factors++;
                }

                // Disk usage impact
                if (metrics.TryGetValue("DiskUsagePercentage", out var diskUsageObj) && diskUsageObj is double diskUsage)
                {
                    score -= Math.Max(0, diskUsage - 80) * 0.2; // Reduce score for disk > 80%
                    factors++;
                }

                return Math.Max(0, Math.Min(100, score));
            }
            catch
            {
                return 50.0; // Neutral score on error
            }
        }

        /// <summary>
        /// Generates metrics summary
        /// </summary>
        /// <param name="metrics">Metrics to summarize</param>
        /// <returns>Metrics summary</returns>
        private string GenerateMetricsSummary(Dictionary<string, object> metrics)
        {
            try
            {
                var summary = new System.Text.StringBuilder();

                if (metrics.TryGetValue("CpuUsage", out var cpuUsageObj) && cpuUsageObj is double cpuUsage)
                {
                    summary.Append($"CPU: {cpuUsage:F1}% ");
                }

                if (metrics.TryGetValue("MemoryUsageMB", out var memoryUsageObj) && memoryUsageObj is double memoryUsage)
                {
                    summary.Append($"Memory: {memoryUsage:F1}MB ");
                }

                if (metrics.TryGetValue("DiskUsagePercentage", out var diskUsageObj) && diskUsageObj is double diskUsage)
                {
                    summary.Append($"Disk: {diskUsage:F1}% ");
                }

                return summary.ToString().Trim();
            }
            catch
            {
                return "Metrics summary unavailable";
            }
        }

        // Event handler methods
        private void HealthMonitor_HealthStatusChanged(object sender, VersionHealthChangedEventArgs e)
        {
            try
            {
                UpdateMonitoringStatus(e.Version, MapHealthToMonitoringState(e.CurrentHealth));

                var eventArgs = new VersionHealthIssueEventArgs
                {
                    Version = e.Version,
                    Health = e.CurrentHealth,
                    Timestamp = e.Timestamp,
                    Issues = e.Issues,
                    CriticalIssues = e.CriticalIssues,
                    IsCritical = e.CriticalIssues.Count > 0
                };

                HealthIssueDetected?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling health status changed event");
            }
        }

        private void HealthMonitor_CriticalHealthIssueDetected(object sender, VersionHealthCriticalEventArgs e)
        {
            try
            {
                var eventArgs = new VersionHealthIssueEventArgs
                {
                    Version = e.Version,
                    Health = e.Health,
                    Timestamp = e.Timestamp,
                    Issues = e.AllIssues,
                    CriticalIssues = e.CriticalIssues,
                    IsCritical = true
                };

                HealthIssueDetected?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling critical health issue event");
            }
        }

        private void AlertManager_AlertRaised(object sender, VersionAlertEventArgs e)
        {
            try
            {
                UpdateMonitoringStatus(e.Version, MapAlertToMonitoringState(e));

                var eventArgs = new VersionAvailabilityEventArgs
                {
                    Version = e.Version,
                    IsAvailable = e.AlertType != AlertType.Unavailable,
                    Timestamp = e.Timestamp,
                    Reason = e.Message,
                    Severity = e.Severity
                };

                AvailabilityChanged?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling alert raised event");
            }
        }

        private async Task RaisePerformanceThresholdEventAsync(string version, Dictionary<string, object> metrics, List<string> issues)
        {
            try
            {
                var eventArgs = new VersionPerformanceEventArgs
                {
                    Version = version,
                    Timestamp = DateTime.UtcNow,
                    Metrics = metrics,
                    ThresholdIssues = issues,
                    Severity = issues.Count > 2 ? AlertSeverity.Critical : AlertSeverity.Warning
                };

                PerformanceThresholdExceeded?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising performance threshold event");
            }
        }

        // Mapping methods
        private MonitoringState MapHealthToMonitoringState(VersionHealth health)
        {
            return health switch
            {
                VersionHealth.Healthy => MonitoringState.Healthy,
                VersionHealth.Degraded => MonitoringState.Degraded,
                VersionHealth.PerformanceIssues => MonitoringState.PerformanceIssues,
                VersionHealth.Unavailable or VersionHealth.Corrupted => MonitoringState.Unavailable,
                _ => MonitoringState.IssuesDetected
            };
        }

        private MonitoringState MapAlertToMonitoringState(VersionAlertEventArgs alertArgs)
        {
            return alertArgs.AlertType switch
            {
                AlertType.Unavailable => MonitoringState.Unavailable,
                AlertType.Performance => MonitoringState.PerformanceIssues,
                AlertType.Security => MonitoringState.SecurityIssue,
                _ => MonitoringState.IssuesDetected
            };
        }
    }

    /// <summary>
    /// Represents monitoring configuration for a version
    /// </summary>
    public class VersionMonitoringConfig
    {
        /// <summary>
        /// Gets or sets the health monitoring configuration
        /// </summary>
        public VersionHealthMonitoringConfig HealthMonitoringConfig { get; set; }

        /// <summary>
        /// Gets or sets the metrics collection configuration
        /// </summary>
        public VersionMetricsCollectionConfig MetricsCollectionConfig { get; set; }

        /// <summary>
        /// Gets or sets the alert management configuration
        /// </summary>
        public VersionAlertManagementConfig AlertManagementConfig { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether monitoring is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the monitoring priority
        /// </summary>
        public MonitoringPriority Priority { get; set; } = MonitoringPriority.Normal;
    }

    /// <summary>
    /// Represents monitoring states
    /// </summary>
    public enum MonitoringState
    {
        /// <summary>
        /// Version is not monitored
        /// </summary>
        NotMonitored,

        /// <summary>
        /// Version is healthy
        /// </summary>
        Healthy,

        /// <summary>
        /// Version is degraded
        /// </summary>
        Degraded,

        /// <summary>
        /// Version has performance issues
        /// </summary>
        PerformanceIssues,

        /// <summary>
        /// Version is unavailable
        /// </summary>
        Unavailable,

        /// <summary>
        /// Version has security issues
        /// </summary>
        SecurityIssue,

        /// <summary>
        /// Issues detected
        /// </summary>
        IssuesDetected,

        /// <summary>
        /// Monitoring is in progress
        /// </summary>
        Monitoring,

        /// <summary>
        /// Monitoring error
        /// </summary>
        Error
    }

    /// <summary>
    /// Represents monitoring priority levels
    /// </summary>
    public enum MonitoringPriority
    {
        /// <summary>
        /// Low priority monitoring
        /// </summary>
        Low,

        /// <summary>
        /// Normal priority monitoring
        /// </summary>
        Normal,

        /// <summary>
        /// High priority monitoring
        /// </summary>
        High,

        /// <summary>
        /// Critical priority monitoring
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents monitoring status for a version
    /// </summary>
    public class VersionMonitoringStatus
    {
        /// <summary>
        /// Gets or sets the version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the monitoring state
        /// </summary>
        public MonitoringState MonitoringState { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is monitored
        /// </summary>
        public bool IsMonitored { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is available
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version has issues
        /// </summary>
        public bool HasIssues { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version has active alerts
        /// </summary>
        public bool HasActiveAlerts { get; set; }

        /// <summary>
        /// Gets or sets the health status
        /// </summary>
        public VersionHealth Health { get; set; }

        /// <summary>
        /// Gets or sets the last updated timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the total monitoring time
        /// </summary>
        public TimeSpan TotalMonitoringTime { get; set; }

        /// <summary>
        /// Gets or sets the health check count
        /// </summary>
        public int HealthCheckCount { get; set; }

        /// <summary>
        /// Gets or sets the metrics collected count
        /// </summary>
        public int MetricsCollectedCount { get; set; }

        /// <summary>
        /// Gets or sets the alerts raised count
        /// </summary>
        public int AlertsRaisedCount { get; set; }

        /// <summary>
        /// Gets or sets the last metrics collected timestamp
        /// </summary>
        public DateTime? LastMetricsCollected { get; set; }

        /// <summary>
        /// Initializes a new instance of the VersionMonitoringStatus class
        /// </summary>
        public VersionMonitoringStatus()
        {
            LastUpdated = DateTime.UtcNow;
            TotalMonitoringTime = TimeSpan.Zero;
            Health = VersionHealth.Unknown;
        }
    }

    /// <summary>
    /// Represents monitoring context for a version
    /// </summary>
    public class VersionMonitoringContext : IDisposable
    {
        private readonly SolidWorksVersionInfo _versionInfo;
        private readonly VersionMonitoringConfiguration _configuration;
        private readonly Queue<VersionMonitoringHistoryRecord> _monitoringHistory;
        private readonly object _contextLock = new object();
        private bool _disposed;

        /// <summary>
        /// Gets the version information
        /// </summary>
        public SolidWorksVersionInfo VersionInfo => _versionInfo;

        /// <summary>
        /// Gets the monitoring status
        /// </summary>
        public VersionMonitoringStatus Status { get; }

        /// <summary>
        /// Initializes a new instance of the VersionMonitoringContext class
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="configuration">Monitoring configuration</param>
        public VersionMonitoringContext(SolidWorksVersionInfo versionInfo, VersionMonitoringConfiguration configuration)
        {
            _versionInfo = versionInfo ?? throw new ArgumentNullException(nameof(versionInfo));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            Status = new VersionMonitoringStatus
            {
                Version = versionInfo.Version,
                IsMonitored = true
            };

            _monitoringHistory = new Queue<VersionMonitoringHistoryRecord>();
        }

        /// <summary>
        /// Configures monitoring for this context
        /// </summary>
        /// <param name="config">Monitoring configuration</param>
        public void Configure(VersionMonitoringConfig config)
        {
            lock (_contextLock)
            {
                // Apply configuration changes
                Status.IsMonitored = config.IsEnabled;
            }
        }

        /// <summary>
        /// Adds monitoring history record
        /// </summary>
        /// <param name="record">History record to add</param>
        public void AddMonitoringHistory(VersionMonitoringHistoryRecord record)
        {
            lock (_contextLock)
            {
                _monitoringHistory.Enqueue(record);
                while (_monitoringHistory.Count > _configuration.MaxMonitoringHistoryRecords)
                {
                    _monitoringHistory.Dequeue();
                }
            }
        }

        /// <summary>
        /// Gets monitoring history
        /// </summary>
        /// <param name="maxEntries">Maximum number of entries to return</param>
        /// <returns>List of history records</returns>
        public List<VersionMonitoringHistoryRecord> GetMonitoringHistory(int maxEntries)
        {
            lock (_contextLock)
            {
                return _monitoringHistory.TakeLast(maxEntries).ToList();
            }
        }

        /// <summary>
        /// Disposes the monitoring context
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the monitoring context
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up resources
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents monitoring history record
    /// </summary>
    public class VersionMonitoringHistoryRecord
    {
        /// <summary>
        /// Gets or sets the version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is available
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version has issues
        /// </summary>
        public bool HasIssues { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version has alerts
        /// </summary>
        public bool HasAlerts { get; set; }

        /// <summary>
        /// Gets or sets the health status
        /// </summary>
        public VersionHealth Health { get; set; }

        /// <summary>
        /// Gets or sets the performance score
        /// </summary>
        public double PerformanceScore { get; set; }

        /// <summary>
        /// Gets or sets the alert level
        /// </summary>
        public AlertSeverity? AlertLevel { get; set; }

        /// <summary>
        /// Gets or sets the list of issues
        /// </summary>
        public List<string> Issues { get; set; }

        /// <summary>
        /// Gets or sets the metrics
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; }

        /// <summary>
        /// Initializes a new instance of the VersionMonitoringHistoryRecord class
        /// </summary>
        public VersionMonitoringHistoryRecord()
        {
            Issues = new List<string>();
            Metrics = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Configuration for version monitoring
    /// </summary>
    public class VersionMonitoringConfiguration
    {
        /// <summary>
        /// Gets or sets the monitoring interval
        /// </summary>
        public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the maximum number of concurrent monitoring operations
        /// </summary>
        public int MaxConcurrentMonitoringOperations { get; set; } = 3;

        /// <summary>
        /// Gets or sets the maximum number of monitoring history records to keep
        /// </summary>
        public int MaxMonitoringHistoryRecords { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the CPU usage threshold percentage
        /// </summary>
        public double CpuUsageThresholdPercentage { get; set; } = 80.0;

        /// <summary>
        /// Gets or sets the memory usage threshold in MB
        /// </summary>
        public double MemoryUsageThresholdMB { get; set; } = 1024.0;

        /// <summary>
        /// Gets or sets the disk usage threshold percentage
        /// </summary>
        public double DiskUsageThresholdPercentage { get; set; } = 85.0;

        /// <summary>
        /// Gets or sets a value indicating whether to enable automatic alert resolution
        /// </summary>
        public bool EnableAutomaticAlertResolution { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable performance optimization
        /// </summary>
        public bool EnablePerformanceOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable predictive monitoring
        /// </summary>
        public bool EnablePredictiveMonitoring { get; set; } = false;

        /// <summary>
        /// Gets or sets the alert escalation timeout
        /// </summary>
        public TimeSpan AlertEscalationTimeout { get; set; } = TimeSpan.FromMinutes(30);
    }

    // Result classes
    public class VersionMonitoringStartResult
    {
        public bool Success { get; set; } = true;
        public TimeSpan StartTime { get; set; }
        public int MonitoredVersionCount { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }

    public class VersionMonitoringStopResult
    {
        public bool Success { get; set; } = true;
        public TimeSpan StopTime { get; set; }
        public int StoppedVersionCount { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }

    public class VersionMonitoringCycleResult
    {
        public bool Success { get; set; } = true;
        public TimeSpan CycleTime { get; set; }
        public int VersionsWithIssues { get; set; }
        public int VersionsWithAlerts { get; set; }
        public int UnhealthyVersions { get; set; }
        public int UnavailableVersions { get; set; }
        public string Error { get; set; }
        public List<VersionMonitoringResult> VersionResults { get; } = new List<VersionMonitoringResult>();
    }

    public class VersionMonitoringResult
    {
        public string Version { get; set; }
        public bool Success { get; set; } = true;
        public bool IsHealthy { get; set; }
        public bool IsAvailable { get; set; }
        public bool HasIssues { get; set; }
        public bool HasAlerts { get; set; }
        public bool HasCriticalAlerts { get; set; }
        public bool IsUnhealthy { get; set; }
        public VersionHealth Health { get; set; }
        public double PerformanceScore { get; set; }
        public AlertSeverity? AlertLevel { get; set; }
        public int AlertCount { get; set; }
        public List<string> Issues { get; } = new List<string>();
        public Dictionary<string, object> Metrics { get; } = new Dictionary<string, object>();
    }

    public class VersionMonitoringConfigurationResult
    {
        public string Version { get; set; }
        public bool Success { get; set; } = true;
        public List<string> Errors { get; } = new List<string>();
    }

    public class VersionMonitoringStatistics
    {
        public int TotalVersions { get; set; }
        public int MonitoredVersions { get; set; }
        public int HealthyVersions { get; set; }
        public int AvailableVersions { get; set; }
        public int VersionsWithAlerts { get; set; }
        public TimeSpan TotalMonitoringTime { get; set; }
        public TimeSpan AverageMonitoringTime { get; set; }
        public int TotalHealthChecks { get; set; }
        public int TotalMetricsCollected { get; set; }
        public int TotalAlertsRaised { get; set; }
        public double HealthPercentage { get; set; }
        public double AvailabilityPercentage { get; set; }
    }

    public class VersionHealthStatusResult
    {
        public Dictionary<string, VersionHealthSummary> VersionHealthStatuses { get; } = new Dictionary<string, VersionHealthSummary>();
        public MultiVersionHealthLevel OverallHealth { get; set; }
        public int HealthyVersionCount { get; set; }
        public int TotalVersionCount { get; set; }
        public string Error { get; set; }
    }

    public class VersionHealthSummary
    {
        public string Version { get; set; }
        public VersionHealth Health { get; set; }
        public bool IsHealthy { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime LastCheck { get; set; }
        public double UptimePercentage { get; set; }
        public bool HasIssues { get; set; }
    }

    public class VersionMetricsResult
    {
        public Dictionary<string, VersionMetricSummary> VersionMetrics { get; } = new Dictionary<string, VersionMetricSummary>();
        public Dictionary<string, object> SystemMetrics { get; } = new Dictionary<string, object>();
        public DateTime Timestamp { get; set; }
        public string Error { get; set; }
    }

    public class VersionMetricSummary
    {
        public string Version { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
        public DateTime Timestamp { get; set; }
        public string Summary { get; set; }
    }

    public class VersionAlertsResult
    {
        public Dictionary<string, VersionAlertSummary> VersionAlerts { get; } = new Dictionary<string, VersionAlertSummary>();
        public List<VersionAlert> SystemAlerts { get; } = new List<VersionAlert>();
        public DateTime Timestamp { get; set; }
        public string Error { get; set; }
    }

    public class VersionAlertSummary
    {
        public string Version { get; set; }
        public List<VersionAlert> Alerts { get; set; }
        public DateTime Timestamp { get; set; }
        public int ActiveAlertCount { get; set; }
        public int CriticalAlertCount { get; set; }
    }

    // Event argument classes
    public class VersionMonitoringEventArgs : EventArgs
    {
        public string Version { get; set; }
        public MonitoringState PreviousState { get; set; }
        public MonitoringState CurrentState { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class VersionHealthIssueEventArgs : EventArgs
    {
        public string Version { get; set; }
        public VersionHealth Health { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Issues { get; set; }
        public List<string> CriticalIssues { get; set; }
        public bool IsCritical { get; set; }
    }

    public class VersionPerformanceEventArgs : EventArgs
    {
        public string Version { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
        public List<string> ThresholdIssues { get; set; }
        public AlertSeverity Severity { get; set; }
    }

    public class VersionAvailabilityEventArgs : EventArgs
    {
        public string Version { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime Timestamp { get; set; }
        public string Reason { get; set; }
        public AlertSeverity Severity { get; set; }
    }

    // Configuration classes for individual monitoring components
    public class VersionHealthMonitoringConfig
    {
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableFileSystemChecks { get; set; } = true;
        public bool EnableRegistryChecks { get; set; } = true;
        public bool EnablePerformanceChecks { get; set; } = true;
    }

    public class VersionMetricsCollectionConfig
    {
        public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromMinutes(1);
        public bool EnableCpuMetrics { get; set; } = true;
        public bool EnableMemoryMetrics { get; set; } = true;
        public bool EnableDiskMetrics { get; set; } = true;
        public bool EnableNetworkMetrics { get; set; } = true;
    }

    public class VersionAlertManagementConfig
    {
        public bool EnableAlerts { get; set; } = true;
        public AlertSeverity MinimumAlertSeverity { get; set; } = AlertSeverity.Warning;
        public TimeSpan AlertSuppressionDuration { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableAutoResolution { get; set; } = true;
    }


    public enum AlertType
    {
        Health,
        Performance,
        Availability,
        Security,
        Configuration,
        Unavailable
    }


}