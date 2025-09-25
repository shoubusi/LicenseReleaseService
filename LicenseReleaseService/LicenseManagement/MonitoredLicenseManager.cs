using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.Process;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// License manager with comprehensive monitoring and metrics collection
    /// </summary>
    public class MonitoredLicenseManager : ILicenseManager, IDisposable
    {
        private readonly ILicenseManager _innerLicenseManager;
        private readonly ILogger<MonitoredLicenseManager> _logger;
        private readonly ProcessMetrics _processMetrics;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly HealthChecker _healthChecker;
        private readonly ServiceSettings _serviceSettings;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the MonitoredLicenseManager class
        /// </summary>
        /// <param name="innerLicenseManager">The inner license manager</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="processMetrics">The process metrics tracker</param>
        /// <param name="performanceMonitor">The performance monitor</param>
        /// <param name="healthChecker">The health checker</param>
        /// <param name="serviceSettings">The service settings</param>
        public MonitoredLicenseManager(
            ILicenseManager innerLicenseManager,
            ILogger<MonitoredLicenseManager> logger,
            ProcessMetrics processMetrics,
            PerformanceMonitor performanceMonitor,
            HealthChecker healthChecker,
            ServiceSettings serviceSettings)
        {
            _innerLicenseManager = innerLicenseManager ?? throw new ArgumentNullException(nameof(innerLicenseManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processMetrics = processMetrics ?? throw new ArgumentNullException(nameof(processMetrics));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _healthChecker = healthChecker ?? throw new ArgumentNullException(nameof(healthChecker));
            _serviceSettings = serviceSettings ?? throw new ArgumentNullException(nameof(serviceSettings));

            // Subscribe to performance alerts
            _performanceMonitor.AlertRaised += OnPerformanceAlertRaised;
        }

        /// <summary>
        /// Gets the status of a license server with monitoring
        /// </summary>
        public async Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var operationName = "lmstat";

            try
            {
                var result = await _innerLicenseManager.GetServerStatusAsync(server, port, cancellationToken);

                // Record metrics
                await RecordOperationMetricsAsync(operationName, server, port, startTime, result.ResponseTimeMs > 0 ?
                    TimeSpan.FromMilliseconds(result.ResponseTimeMs) : DateTime.Now - startTime, true, 0);

                return result;
            }
            catch (Exception ex)
            {
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, false, 0, ex);
                throw;
            }
        }

        /// <summary>
        /// Releases a license with monitoring
        /// </summary>
        public async Task<LicenseReleaseResult> ReleaseLicenseAsync(string server, int port, string feature, string user, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var operationName = "lmremove";

            try
            {
                var result = await _innerLicenseManager.ReleaseLicenseAsync(server, port, feature, user, cancellationToken);

                // Record metrics
                await RecordOperationMetricsAsync(operationName, server, port, startTime, result.ExecutionTime,
                    result.Success, result.ExitCode, null, feature, user);

                return result;
            }
            catch (Exception ex)
            {
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime,
                    false, -1, ex, feature, user);
                throw;
            }
        }

        /// <summary>
        /// Gets feature information with monitoring
        /// </summary>
        public async Task<LicenseFeatureInfo> GetFeatureInfoAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var operationName = "lmstat_feature";

            try
            {
                var result = await _innerLicenseManager.GetFeatureInfoAsync(server, port, feature, cancellationToken);

                // Record metrics
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, true, 0, null, feature);

                return result;
            }
            catch (Exception ex)
            {
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, false, 0, ex, feature);
                throw;
            }
        }

        /// <summary>
        /// Gets all features with monitoring
        /// </summary>
        public async Task<Dictionary<string, LicenseFeatureInfo>> GetAllFeaturesAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var operationName = "lmstat_all";

            try
            {
                var result = await _innerLicenseManager.GetAllFeaturesAsync(server, port, cancellationToken);

                // Record metrics
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, true, 0);

                return result;
            }
            catch (Exception ex)
            {
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, false, 0, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all users with monitoring
        /// </summary>
        public async Task<Dictionary<string, LicenseUserUsage>> GetUsersAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var operationName = "lmstat_users";

            try
            {
                var result = await _innerLicenseManager.GetUsersAsync(server, port, cancellationToken);

                // Record metrics
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, true, 0);

                return result;
            }
            catch (Exception ex)
            {
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, false, 0, ex);
                throw;
            }
        }

        /// <summary>
        /// Checks server availability with monitoring
        /// </summary>
        public async Task<bool> IsServerAvailableAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var operationName = "availability_check";

            try
            {
                var result = await _innerLicenseManager.IsServerAvailableAsync(server, port, cancellationToken);

                // Record metrics
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, true, 0);

                return result;
            }
            catch (Exception ex)
            {
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, false, 0, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets usage statistics with monitoring
        /// </summary>
        public async Task<LicenseUsageStatistics> GetUsageStatisticsAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var operationName = "usage_statistics";

            try
            {
                var result = await _innerLicenseManager.GetUsageStatisticsAsync(server, port, cancellationToken);

                // Record metrics
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, true, 0);

                return result;
            }
            catch (Exception ex)
            {
                await RecordOperationMetricsAsync(operationName, server, port, startTime, DateTime.Now - startTime, false, 0, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets monitoring metrics summary
        /// </summary>
        /// <param name="server">The server address</param>
        /// <param name="port">The server port</param>
        /// <returns>Metrics summary</returns>
        public async Task<Dictionary<string, ProcessMetricsSummary>> GetMetricsSummaryAsync(string server, int port)
        {
            var summaries = await _processMetrics.GetAllOperationSummariesAsync();
            var serverSummaries = new Dictionary<string, ProcessMetricsSummary>();

            foreach (var kvp in summaries)
            {
                if (kvp.Key.Contains($"{server}_{port}"))
                {
                    serverSummaries[kvp.Key] = kvp.Value;
                }
            }

            return serverSummaries;
        }

        /// <summary>
        /// Gets current performance alerts
        /// </summary>
        /// <returns>List of performance alerts</returns>
        public IReadOnlyList<PerformanceAlert> GetPerformanceAlerts()
        {
            return _performanceMonitor.GetCurrentAlerts();
        }

        /// <summary>
        /// Gets current system health
        /// </summary>
        /// <returns>System health status</returns>
        public async Task<SystemHealth> GetSystemHealthAsync()
        {
            return await _healthChecker.GetSystemHealthAsync();
        }

        /// <summary>
        /// Acknowledges a performance alert
        /// </summary>
        /// <param name="alertId">The alert ID</param>
        /// <param name="user">The user acknowledging the alert</param>
        public void AcknowledgeAlert(string alertId, string user)
        {
            _performanceMonitor.AcknowledgeAlert(alertId, user);
        }

        /// <summary>
        /// Records operation metrics
        /// </summary>
        private async Task RecordOperationMetricsAsync(
            string operationType,
            string server,
            int port,
            DateTime startTime,
            TimeSpan executionDuration,
            bool success,
            int exitCode,
            Exception? exception = null,
            string? feature = null,
            string? user = null)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var metrics = new ProcessExecutionMetrics
                {
                    OperationType = operationType,
                    Server = server,
                    Port = port,
                    StartTime = startTime,
                    EndTime = DateTime.Now,
                    ExecutionDuration = executionDuration,
                    ExitCode = exitCode,
                    Success = success,
                    PeakMemoryUsage = currentProcess.WorkingSet64,
                    RetryCount = 0, // This would be populated if we had retry info
                    ConfiguredTimeout = TimeSpan.FromSeconds(_serviceSettings.LicenseManagerTimeout),
                    Feature = feature ?? string.Empty,
                    User = user ?? string.Empty,
                    OutputSize = 0, // This would be populated from actual output
                    ErrorSize = 0, // This would be populated from actual error output
                    CpuTime = currentProcess.TotalProcessorTime,
                    ProcessId = currentProcess.Id
                };

                // Add tags based on operation type and result
                metrics.Tags["Operation"] = operationType;
                metrics.Tags["Server"] = server;
                metrics.Tags["Port"] = port.ToString();
                metrics.Tags["Success"] = success.ToString();
                metrics.Tags["Timestamp"] = startTime.ToString("yyyy-MM-dd HH:mm:ss");

                if (!string.IsNullOrEmpty(feature))
                {
                    metrics.Tags["Feature"] = feature;
                }

                if (!string.IsNullOrEmpty(user))
                {
                    metrics.Tags["User"] = user;
                }

                if (exception != null)
                {
                    metrics.Tags["ExceptionType"] = exception.GetType().Name;
                    metrics.Tags["ExceptionMessage"] = exception.Message;
                }

                await _processMetrics.RecordExecutionAsync(metrics);

                _logger.LogDebug("Recorded metrics for {OperationType} on {Server}:{Port} - Success: {Success}, Duration: {Duration}ms",
                    operationType, server, port, success, executionDuration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record operation metrics for {OperationType} on {Server}:{Port}",
                    operationType, server, port);
            }
        }

        /// <summary>
        /// Handles performance alerts
        /// </summary>
        private void OnPerformanceAlertRaised(object? sender, PerformanceAlert alert)
        {
            _logger.LogWarning("Performance alert: {Severity} - {Title} - {Message}",
                alert.Severity, alert.Title, alert.Message);

            // Here you could integrate with external notification systems
            // For example: email, Slack, Teams, PagerDuty, etc.

            // You could also trigger automatic recovery actions
            if (alert.Severity == "Critical")
            {
                _logger.LogError("Critical performance alert detected: {Alert}", alert);

                // Example: Trigger circuit breaker if too many failures
                if (alert.MetricType == "SuccessRate" && alert.CurrentValue < 50)
                {
                    _logger.LogWarning("Low success rate detected, consider taking corrective action");
                }
            }
        }

        /// <summary>
        /// Gets monitoring statistics
        /// </summary>
        /// <returns>Monitoring statistics</returns>
        public async Task<Dictionary<string, object>> GetMonitoringStatisticsAsync()
        {
            var stats = new Dictionary<string, object>();

            // Get metrics summary
            var allSummaries = await _processMetrics.GetAllOperationSummariesAsync();
            var totalExecutions = allSummaries.Values.Sum(s => s.TotalExecutions);
            var totalSuccessful = allSummaries.Values.Sum(s => s.SuccessfulExecutions);
            var overallSuccessRate = totalExecutions > 0 ? (totalSuccessful / (double)totalExecutions) * 100 : 0;

            // Get system health
            var systemHealth = await _healthChecker.GetSystemHealthAsync();

            // Get performance alerts
            var alerts = _performanceMonitor.GetCurrentAlerts();

            stats["TotalExecutions"] = totalExecutions;
            stats["TotalSuccessful"] = totalSuccessful;
            stats["OverallSuccessRate"] = overallSuccessRate;
            stats["SystemHealthStatus"] = systemHealth.OverallStatus.ToString();
            stats["ActiveHealthChecks"] = systemHealth.CheckResults.Count;
            stats["ActiveAlerts"] = alerts.Count;
            stats["CriticalAlerts"] = alerts.Count(a => a.Severity == "Critical");
            stats["WarningAlerts"] = alerts.Count(a => a.Severity == "Warning");
            stats["ServiceUptime"] = systemHealth.Uptime.ToString();
            stats["LastUpdated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Add operation-specific statistics
            var operationStats = new Dictionary<string, object>();
            foreach (var summary in allSummaries.Values)
            {
                operationStats[summary.TimeWindow.ToString()] = new
                {
                    TotalExecutions = summary.TotalExecutions,
                    SuccessRate = summary.SuccessRate,
                    AverageExecutionTime = summary.AverageExecutionTime.TotalMilliseconds,
                    TimeoutRate = summary.TimeoutRate
                };
            }
            stats["OperationStatistics"] = operationStats;

            return stats;
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Unsubscribe from events
                _performanceMonitor.AlertRaised -= OnPerformanceAlertRaised;

                // Dispose of inner manager if it implements IDisposable
                if (_innerLicenseManager is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _disposed = true;
            }
        }
    }
}