using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents health check status levels
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// System is healthy
        /// </summary>
        Healthy,

        /// <summary>
        /// System has some issues but is operational
        /// </summary>
        Warning,

        /// <summary>
        /// System has serious issues and may not function properly
        /// </summary>
        Critical,

        /// <summary>
        /// System is not operational
        /// </summary>
        Unhealthy
    }

    /// <summary>
    /// Represents a health check result
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// Gets or sets the health check name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the health status
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the description of the health check
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detailed message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the duration of the health check
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the health check
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets any additional data
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        /// <summary>
        /// Gets or sets the exception if the health check failed
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets a value indicating whether the health check passed
        /// </summary>
        public bool IsHealthy => Status == HealthStatus.Healthy;
    }

    /// <summary>
    /// Represents overall system health
    /// </summary>
    public class SystemHealth
    {
        /// <summary>
        /// Gets or sets the overall health status
        /// </summary>
        public HealthStatus OverallStatus { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the health check
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the individual health check results
        /// </summary>
        public List<HealthCheckResult> CheckResults { get; set; } = new();

        /// <summary>
        /// Gets or sets the service uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets or sets the version information
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the environment information
        /// </summary>
        public string Environment { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server name
        /// </summary>
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional system information
        /// </summary>
        public Dictionary<string, object> SystemInfo { get; set; } = new();
    }

    /// <summary>
    /// Represents a health check configuration
    /// </summary>
    public class HealthCheckConfiguration
    {
        /// <summary>
        /// Gets or sets the check name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the check interval
        /// </summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the timeout for the check
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether the check is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the criticality (whether a failed check makes the system unhealthy)
        /// </summary>
        public bool IsCritical { get; set; } = false;

        /// <summary>
        /// Gets or sets the failure threshold (number of consecutive failures before alerting)
        /// </summary>
        public int FailureThreshold { get; set; } = 3;

        /// <summary>
        /// Gets or sets the recovery threshold (number of consecutive successes before recovering)
        /// </summary>
        public int RecoveryThreshold { get; set; } = 1;
    }

    /// <summary>
    /// Performs comprehensive health checks for the license release service
    /// </summary>
    public class HealthChecker : IDisposable
    {
        private readonly ILogger<HealthChecker> _logger;
        private readonly ILicenseManager _licenseManager;
        private readonly ProcessMetrics _processMetrics;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly List<HealthCheckConfiguration> _configurations;
        private readonly ConcurrentDictionary<string, int> _failureCounts;
        private readonly ConcurrentDictionary<string, int> _successCounts;
        private readonly ConcurrentDictionary<string, HealthStatus> _lastKnownStatus;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _healthCheckTask;
        private readonly DateTime _startTime;
        private bool _disposed;

        /// <summary>
        /// Event raised when health status changes
        /// </summary>
        public event EventHandler<SystemHealth>? HealthStatusChanged;

        /// <summary>
        /// Initializes a new instance of the HealthChecker class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="licenseManager">The license manager instance</param>
        /// <param name="processMetrics">The process metrics tracker</param>
        /// <param name="performanceMonitor">The performance monitor</param>
        /// <param name="configurations">Health check configurations</param>
        public HealthChecker(
            ILogger<HealthChecker> logger,
            ILicenseManager licenseManager,
            ProcessMetrics processMetrics,
            PerformanceMonitor performanceMonitor,
            IEnumerable<HealthCheckConfiguration>? configurations = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _licenseManager = licenseManager ?? throw new ArgumentNullException(nameof(licenseManager));
            _processMetrics = processMetrics ?? throw new ArgumentNullException(nameof(processMetrics));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _configurations = configurations?.ToList() ?? GetDefaultConfigurations();
            _failureCounts = new ConcurrentDictionary<string, int>();
            _successCounts = new ConcurrentDictionary<string, int>();
            _lastKnownStatus = new ConcurrentDictionary<string, HealthStatus>();
            _cancellationTokenSource = new CancellationTokenSource();
            _startTime = DateTime.Now;

            // Initialize status tracking
            foreach (var config in _configurations)
            {
                _failureCounts[config.Name] = 0;
                _successCounts[config.Name] = 0;
                _lastKnownStatus[config.Name] = HealthStatus.Healthy;
            }

            // Start the health check task
            _healthCheckTask = Task.Run(() => HealthCheckLoopAsync(_cancellationTokenSource.Token));

            _logger.LogInformation("Health checker initialized with {CheckCount} health checks", _configurations.Count);
        }

        /// <summary>
        /// Gets the current system health
        /// </summary>
        /// <returns>Current system health</returns>
        public async Task<SystemHealth> GetSystemHealthAsync()
        {
            var health = new SystemHealth
            {
                Timestamp = DateTime.Now,
                Uptime = DateTime.Now - _startTime,
                Version = GetVersion(),
                Environment = GetEnvironment(),
                ServerName = Environment.MachineName,
                SystemInfo = await GetSystemInfoAsync()
            };

            // Run all health checks
            var tasks = _configurations.Where(c => c.Enabled).Select(config => RunHealthCheckAsync(config));
            var results = await Task.WhenAll(tasks);

            health.CheckResults = results.ToList();

            // Determine overall status
            if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            {
                health.OverallStatus = HealthStatus.Unhealthy;
            }
            else if (results.Any(r => r.Status == HealthStatus.Critical))
            {
                health.OverallStatus = HealthStatus.Critical;
            }
            else if (results.Any(r => r.Status == HealthStatus.Warning))
            {
                health.OverallStatus = HealthStatus.Warning;
            }
            else
            {
                health.OverallStatus = HealthStatus.Healthy;
            }

            return health;
        }

        /// <summary>
        /// Gets the health status for a specific check
        /// </summary>
        /// <param name="checkName">The health check name</param>
        /// <returns>Health check result</returns>
        public async Task<HealthCheckResult?> GetHealthCheckResultAsync(string checkName)
        {
            var config = _configurations.FirstOrDefault(c => c.Name == checkName);
            if (config == null)
            {
                return null;
            }

            return await RunHealthCheckAsync(config);
        }

        /// <summary>
        /// Gets the health check history
        /// </summary>
        /// <param name="checkName">The health check name</param>
        /// <param name="count">Number of historical results to retrieve</param>
        /// <returns>List of historical health check results</returns>
        public List<HealthCheckResult> GetHealthCheckHistory(string checkName, int count = 100)
        {
            // This would typically retrieve from a persistent store
            // For now, return empty list as we don't persist history
            return new List<HealthCheckResult>();
        }

        /// <summary>
        /// Adds a new health check configuration
        /// </summary>
        /// <param name="configuration">The health check configuration</param>
        public void AddHealthCheck(HealthCheckConfiguration configuration)
        {
            _configurations.Add(configuration);
            _failureCounts[configuration.Name] = 0;
            _successCounts[configuration.Name] = 0;
            _lastKnownStatus[configuration.Name] = HealthStatus.Healthy;
            _logger.LogInformation("Added health check: {Name}", configuration.Name);
        }

        /// <summary>
        /// Removes a health check configuration
        /// </summary>
        /// <param name="checkName">The health check name</param>
        public void RemoveHealthCheck(string checkName)
        {
            var config = _configurations.FirstOrDefault(c => c.Name == checkName);
            if (config != null)
            {
                _configurations.Remove(config);
                _failureCounts.TryRemove(checkName, out _);
                _successCounts.TryRemove(checkName, out _);
                _lastKnownStatus.TryRemove(checkName, out _);
                _logger.LogInformation("Removed health check: {Name}", checkName);
            }
        }

        private async Task HealthCheckLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Calculate next check time (find the minimum interval)
                    var nextInterval = _configurations
                        .Where(c => c.Enabled)
                        .Min(c => c.Interval);

                    // Run health checks that are due
                    var now = DateTime.Now;
                    var dueChecks = _configurations.Where(c => c.Enabled && ShouldRunCheck(c, now));

                    if (dueChecks.Any())
                    {
                        var tasks = dueChecks.Select(config => RunHealthCheckAsync(config));
                        var results = await Task.WhenAll(tasks);

                        // Check if overall health status changed
                        var previousOverallStatus = _lastKnownStatus.Values.DefaultIfEmpty().Max();
                        var currentOverallStatus = DetermineOverallStatus(results);

                        if (previousOverallStatus != currentOverallStatus)
                        {
                            var systemHealth = await GetSystemHealthAsync();
                            HealthStatusChanged?.Invoke(this, systemHealth);
                            _logger.LogInformation("Overall health status changed from {Previous} to {Current}",
                                previousOverallStatus, currentOverallStatus);
                        }
                    }

                    // Wait for the next check cycle
                    await Task.Delay(nextInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Task was cancelled, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health check loop");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
        }

        private bool ShouldRunCheck(HealthCheckConfiguration config, DateTime now)
        {
            // For now, we'll run based on interval
            // In a real implementation, you might want to track last run time
            return true;
        }

        private async Task<HealthCheckResult> RunHealthCheckAsync(HealthCheckConfiguration config)
        {
            var startTime = DateTime.Now;
            var result = new HealthCheckResult
            {
                Name = config.Name,
                Description = GetHealthCheckDescription(config.Name),
                Timestamp = startTime
            };

            try
            {
                // Run the specific health check
                var status = await ExecuteHealthCheck(config);
                result.Status = status.Status;
                result.Message = status.Message;
                result.Data = status.Data;
                result.Exception = status.Exception;

                // Update tracking counters
                if (result.IsHealthy)
                {
                    _successCounts[config.Name]++;
                    _failureCounts[config.Name] = 0;
                }
                else
                {
                    _failureCounts[config.Name]++;
                    _successCounts[config.Name] = 0;
                }

                // Determine if we should alert
                var shouldAlert = false;
                var previousStatus = _lastKnownStatus[config.Name];

                if (result.IsHealthy && previousStatus != HealthStatus.Healthy)
                {
                    // Recovered from failure
                    if (_successCounts[config.Name] >= config.RecoveryThreshold)
                    {
                        shouldAlert = true;
                        _lastKnownStatus[config.Name] = HealthStatus.Healthy;
                    }
                }
                else if (!result.IsHealthy && _failureCounts[config.Name] >= config.FailureThreshold)
                {
                    // Failed threshold reached
                    shouldAlert = true;
                    _lastKnownStatus[config.Name] = result.Status;
                }

                if (shouldAlert)
                {
                    _logger.LogWarning("Health check {Name} status changed: {Status} - {Message}",
                        config.Name, result.Status, result.Message);
                }
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Message = $"Health check failed: {ex.Message}";
                result.Exception = ex;
                _failureCounts[config.Name]++;
                _successCounts[config.Name] = 0;

                _logger.LogError(ex, "Health check {Name} failed", config.Name);
            }

            result.Duration = DateTime.Now - startTime;
            return result;
        }

        private async Task<(HealthStatus Status, string Message, Dictionary<string, object> Data, Exception? Exception)> ExecuteHealthCheck(HealthCheckConfiguration config)
        {
            switch (config.Name.ToLower())
            {
                case "licensemanager":
                    return await CheckLicenseManagerHealthAsync(config);
                case "systemresources":
                    return CheckSystemResources();
                case "networkconnectivity":
                    return CheckNetworkConnectivity();
                case "diskusage":
                    return CheckDiskUsage();
                case "processmetrics":
                    return CheckProcessMetrics();
                case "performancealerts":
                    return CheckPerformanceAlerts();
                default:
                    return (HealthStatus.Healthy, "Unknown health check", new Dictionary<string, object>(), null);
            }
        }

        private async Task<(HealthStatus Status, string Message, Dictionary<string, object> Data, Exception? Exception)> CheckLicenseManagerHealthAsync(HealthCheckConfiguration config)
        {
            try
            {
                // This would typically check a test license server
                // For now, we'll simulate with the metrics
                var recentMetrics = await _processMetrics.GetRecentMetricsAsync(10);
                var lmstatMetrics = recentMetrics.Where(m => m.OperationType == "lmstat").ToList();

                var data = new Dictionary<string, object>
                {
                    { "RecentOperations", lmstatMetrics.Count },
                    { "SuccessRate", lmstatMetrics.Count > 0 ? lmstatMetrics.Count(m => m.Success) / (double)lmstatMetrics.Count : 0 },
                    { "AverageExecutionTime", lmstatMetrics.Count > 0 ? lmstatMetrics.Average(m => m.ExecutionDuration.TotalMilliseconds) : 0 }
                };

                if (lmstatMetrics.Count == 0)
                {
                    return (HealthStatus.Warning, "No recent license manager operations", data, null);
                }

                var successRate = lmstatMetrics.Count(m => m.Success) / (double)lmstatMetrics.Count;
                if (successRate < 0.8)
                {
                    return (HealthStatus.Critical, $"Low success rate: {successRate:P2}", data, null);
                }

                if (successRate < 0.95)
                {
                    return (HealthStatus.Warning, $"Moderate success rate: {successRate:P2}", data, null);
                }

                return (HealthStatus.Healthy, $"License manager is healthy (success rate: {successRate:P2})", data, null);
            }
            catch (Exception ex)
            {
                return (HealthStatus.Unhealthy, $"License manager health check failed: {ex.Message}", new Dictionary<string, object>(), ex);
            }
        }

        private (HealthStatus Status, string Message, Dictionary<string, object> Data, Exception? Exception) CheckSystemResources()
        {
            try
            {
                var counters = _performanceMonitor.GetSystemPerformanceCounters();
                var data = new Dictionary<string, object>
                {
                    { "CpuUsage", counters.CpuUsage },
                    { "MemoryUsage", counters.MemoryUsage },
                    { "AvailableMemory", counters.AvailableMemory },
                    { "TotalMemory", counters.TotalMemory },
                    { "ActiveProcesses", counters.ActiveProcesses },
                    { "ActiveThreads", counters.ActiveThreads }
                };

                var status = HealthStatus.Healthy;
                var message = "System resources are healthy";

                if (counters.CpuUsage > 90)
                {
                    status = HealthStatus.Critical;
                    message = $"High CPU usage: {counters.CpuUsage:F1}%";
                }
                else if (counters.CpuUsage > 80)
                {
                    status = HealthStatus.Warning;
                    message = $"Moderate CPU usage: {counters.CpuUsage:F1}%";
                }

                if (counters.MemoryUsage > 90)
                {
                    status = HealthStatus.Critical;
                    message = $"High memory usage: {counters.MemoryUsage:F1}%";
                }
                else if (counters.MemoryUsage > 80)
                {
                    status = HealthStatus.Warning;
                    message = $"Moderate memory usage: {counters.MemoryUsage:F1}%";
                }

                return (status, message, data, null);
            }
            catch (Exception ex)
            {
                return (HealthStatus.Unhealthy, $"System resources check failed: {ex.Message}", new Dictionary<string, object>(), ex);
            }
        }

        private (HealthStatus Status, string Message, Dictionary<string, object> Data, Exception? Exception) CheckNetworkConnectivity()
        {
            try
            {
                var data = new Dictionary<string, object>();
                var connectedInterfaces = 0;
                var totalInterfaces = 0;

                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus == OperationalStatus.Up)
                    {
                        connectedInterfaces++;
                    }
                    totalInterfaces++;
                }

                data["ConnectedInterfaces"] = connectedInterfaces;
                data["TotalInterfaces"] = totalInterfaces;

                if (connectedInterfaces == 0)
                {
                    return (HealthStatus.Unhealthy, "No network interfaces available", data, null);
                }

                return (HealthStatus.Healthy, $"Network connectivity is healthy ({connectedInterfaces}/{totalInterfaces} interfaces up)", data, null);
            }
            catch (Exception ex)
            {
                return (HealthStatus.Unhealthy, $"Network connectivity check failed: {ex.Message}", new Dictionary<string, object>(), ex);
            }
        }

        private (HealthStatus Status, string Message, Dictionary<string, object> Data, Exception? Exception) CheckDiskUsage()
        {
            try
            {
                var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory));
                var data = new Dictionary<string, object>
                {
                    { "DriveName", drive.Name },
                    { "TotalSize", drive.TotalSize },
                    { "AvailableFreeSpace", drive.AvailableFreeSpace },
                    { "UsedSpace", drive.TotalSize - drive.AvailableFreeSpace },
                    { "UsagePercentage", ((drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize) * 100 }
                };

                var usagePercentage = ((drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize) * 100;

                if (usagePercentage > 95)
                {
                    return (HealthStatus.Critical, $"Critical disk usage: {usagePercentage:F1}%", data, null);
                }

                if (usagePercentage > 90)
                {
                    return (HealthStatus.Warning, $"High disk usage: {usagePercentage:F1}%", data, null);
                }

                return (HealthStatus.Healthy, $"Disk usage is healthy: {usagePercentage:F1}%", data, null);
            }
            catch (Exception ex)
            {
                return (HealthStatus.Unhealthy, $"Disk usage check failed: {ex.Message}", new Dictionary<string, object>(), ex);
            }
        }

        private (HealthStatus Status, string Message, Dictionary<string, object> Data, Exception? Exception) CheckProcessMetrics()
        {
            try
            {
                var recentMetrics = _processMetrics.GetRecentMetricsAsync(50).Result;
                var data = new Dictionary<string, object>
                {
                    { "TotalMetrics", recentMetrics.Count },
                    { "SuccessfulOperations", recentMetrics.Count(m => m.Success) },
                    { "FailedOperations", recentMetrics.Count(m => !m.Success) },
                    { "AverageExecutionTime", recentMetrics.Count > 0 ? recentMetrics.Average(m => m.ExecutionDuration.TotalMilliseconds) : 0 },
                    { "TimeoutCount", recentMetrics.Count(m => m.TimedOut) }
                };

                if (recentMetrics.Count == 0)
                {
                    return (HealthStatus.Warning, "No process metrics available", data, null);
                }

                var successRate = recentMetrics.Count(m => m.Success) / (double)recentMetrics.Count;
                if (successRate < 0.8)
                {
                    return (HealthStatus.Critical, $"Low process success rate: {successRate:P2}", data, null);
                }

                return (HealthStatus.Healthy, $"Process metrics are healthy (success rate: {successRate:P2})", data, null);
            }
            catch (Exception ex)
            {
                return (HealthStatus.Unhealthy, $"Process metrics check failed: {ex.Message}", new Dictionary<string, object>(), ex);
            }
        }

        private (HealthStatus Status, string Message, Dictionary<string, object> Data, Exception? Exception) CheckPerformanceAlerts()
        {
            try
            {
                var alerts = _performanceMonitor.GetCurrentAlerts();
                var data = new Dictionary<string, object>
                {
                    { "ActiveAlerts", alerts.Count },
                    { "CriticalAlerts", alerts.Count(a => a.Severity == "Critical") },
                    { "WarningAlerts", alerts.Count(a => a.Severity == "Warning") }
                };

                if (alerts.Any(a => a.Severity == "Critical"))
                {
                    return (HealthStatus.Critical, $"{alerts.Count(a => a.Severity == "Critical")} critical performance alerts active", data, null);
                }

                if (alerts.Count > 5)
                {
                    return (HealthStatus.Warning, $"{alerts.Count} performance alerts active", data, null);
                }

                return (HealthStatus.Healthy, $"Performance alerts are healthy ({alerts.Count} active)", data, null);
            }
            catch (Exception ex)
            {
                return (HealthStatus.Unhealthy, $"Performance alerts check failed: {ex.Message}", new Dictionary<string, object>(), ex);
            }
        }

        private HealthStatus DetermineOverallStatus(HealthCheckResult[] results)
        {
            if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            {
                return HealthStatus.Unhealthy;
            }
            if (results.Any(r => r.Status == HealthStatus.Critical))
            {
                return HealthStatus.Critical;
            }
            if (results.Any(r => r.Status == HealthStatus.Warning))
            {
                return HealthStatus.Warning;
            }
            return HealthStatus.Healthy;
        }

        private string GetHealthCheckDescription(string checkName)
        {
            return checkName.ToLower() switch
            {
                "licensemanager" => "License manager health check",
                "systemresources" => "System resources health check",
                "networkconnectivity" => "Network connectivity health check",
                "diskusage" => "Disk usage health check",
                "processmetrics" => "Process metrics health check",
                "performancealerts" => "Performance alerts health check",
                _ => "Generic health check"
            };
        }

        private async Task<Dictionary<string, object>> GetSystemInfoAsync()
        {
            var process = Process.GetCurrentProcess();
            var counters = _performanceMonitor.GetSystemPerformanceCounters();

            return new Dictionary<string, object>
            {
                { "ProcessId", process.Id },
                { "ProcessStartTime", process.StartTime },
                { "WorkingSet", process.WorkingSet64 },
                { "VirtualMemory", process.VirtualMemorySize64 },
                { "Threads", process.Threads.Count },
                { "Handles", process.HandleCount },
                { "CpuUsage", counters.CpuUsage },
                { "MemoryUsage", counters.MemoryUsage },
                { "AvailableMemory", counters.AvailableMemory },
                { "TotalMemory", counters.TotalMemory },
                { "ActiveProcesses", counters.ActiveProcesses },
                { "OSVersion", Environment.OSVersion.ToString() },
                { "MachineName", Environment.MachineName },
                { "ProcessorCount", Environment.ProcessorCount },
                { "Is64BitProcess", Environment.Is64BitProcess },
                { "Is64BitOperatingSystem", Environment.Is64BitOperatingSystem }
            };
        }

        private string GetVersion()
        {
            // This would typically get the assembly version
            return "1.0.0.0";
        }

        private string GetEnvironment()
        {
            // This would typically get the environment name
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        }

        private List<HealthCheckConfiguration> GetDefaultConfigurations()
        {
            return new List<HealthCheckConfiguration>
            {
                new HealthCheckConfiguration
                {
                    Name = "LicenseManager",
                    Interval = TimeSpan.FromMinutes(1),
                    Timeout = TimeSpan.FromSeconds(30),
                    IsCritical = true,
                    FailureThreshold = 3,
                    RecoveryThreshold = 2
                },
                new HealthCheckConfiguration
                {
                    Name = "SystemResources",
                    Interval = TimeSpan.FromMinutes(2),
                    Timeout = TimeSpan.FromSeconds(15),
                    IsCritical = true,
                    FailureThreshold = 3,
                    RecoveryThreshold = 1
                },
                new HealthCheckConfiguration
                {
                    Name = "NetworkConnectivity",
                    Interval = TimeSpan.FromMinutes(5),
                    Timeout = TimeSpan.FromSeconds(10),
                    IsCritical = false,
                    FailureThreshold = 2,
                    RecoveryThreshold = 1
                },
                new HealthCheckConfiguration
                {
                    Name = "DiskUsage",
                    Interval = TimeSpan.FromMinutes(10),
                    Timeout = TimeSpan.FromSeconds(5),
                    IsCritical = false,
                    FailureThreshold = 3,
                    RecoveryThreshold = 1
                },
                new HealthCheckConfiguration
                {
                    Name = "ProcessMetrics",
                    Interval = TimeSpan.FromMinutes(3),
                    Timeout = TimeSpan.FromSeconds(10),
                    IsCritical = false,
                    FailureThreshold = 2,
                    RecoveryThreshold = 1
                },
                new HealthCheckConfiguration
                {
                    Name = "PerformanceAlerts",
                    Interval = TimeSpan.FromMinutes(1),
                    Timeout = TimeSpan.FromSeconds(5),
                    IsCritical = false,
                    FailureThreshold = 1,
                    RecoveryThreshold = 1
                }
            };
        }

        /// <summary>
        /// Disposes the health checker
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    _healthCheckTask.Wait(TimeSpan.FromSeconds(10));
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