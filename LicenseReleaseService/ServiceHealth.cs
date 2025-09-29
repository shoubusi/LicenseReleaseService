using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService
{
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy,
        Unknown
    }

    public class HealthChecker
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, HealthCheck> _healthChecks;
        private readonly Dictionary<string, HealthMetric> _metrics;
        private readonly Timer _healthCheckTimer;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private HealthStatus _overallStatus = HealthStatus.Unknown;
        private DateTime _lastHealthCheck = DateTime.MinValue;
        private readonly int _maxMetricHistory = 100;

        public HealthChecker()
        {
            _healthChecks = new Dictionary<string, HealthCheck>();
            _metrics = new Dictionary<string, HealthMetric>();
            _healthCheckTimer = new Timer(PerformHealthChecks, null, _checkInterval, _checkInterval);

            RegisterDefaultHealthChecks();
        }

        public HealthStatus OverallStatus
        {
            get
            {
                lock (_lock)
                {
                    return _overallStatus;
                }
            }
        }

        public DateTime LastHealthCheck
        {
            get
            {
                lock (_lock)
                {
                    return _lastHealthCheck;
                }
            }
        }

        public Dictionary<string, HealthCheck> HealthChecks
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, HealthCheck>(_healthChecks);
                }
            }
        }

        public Dictionary<string, HealthMetric> Metrics
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, HealthMetric>(_metrics);
                }
            }
        }

        public void RegisterHealthCheck(string name, Func<Task<HealthCheckResult>> checkFunction, TimeSpan? timeout = null)
        {
            lock (_lock)
            {
                var healthCheck = new HealthCheck
                {
                    Name = name,
                    CheckFunction = checkFunction,
                    Timeout = timeout ?? TimeSpan.FromSeconds(30),
                    LastCheck = DateTime.MinValue,
                    Status = HealthStatus.Unknown
                };

                _healthChecks[name] = healthCheck;
            }
        }

        public void RegisterMetric(string name, Func<double> valueFunction, string unit = null, string description = null)
        {
            lock (_lock)
            {
                if (!_metrics.ContainsKey(name))
                {
                    _metrics[name] = new HealthMetric
                    {
                        Name = name,
                        Unit = unit,
                        Description = description,
                        ValueFunction = valueFunction,
                        History = new Queue<MetricValue>(),
                        LastUpdated = DateTime.MinValue
                    };
                }
            }
        }

        public async Task<HealthReport> GetHealthReportAsync()
        {
            lock (_lock)
            {
                return new HealthReport
                {
                    OverallStatus = _overallStatus,
                    LastHealthCheck = _lastHealthCheck,
                    HealthChecks = new Dictionary<string, HealthCheck>(_healthChecks),
                    Metrics = new Dictionary<string, HealthMetric>(_metrics),
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }

        public void UpdateMetric(string name, double value)
        {
            lock (_lock)
            {
                if (_metrics.TryGetValue(name, out var metric))
                {
                    metric.Value = value;
                    metric.LastUpdated = DateTime.UtcNow;

                    var metricValue = new MetricValue
                    {
                        Value = value,
                        Timestamp = DateTime.UtcNow
                    };

                    metric.History.Enqueue(metricValue);

                    while (metric.History.Count > _maxMetricHistory)
                    {
                        metric.History.Dequeue();
                    }
                }
            }
        }

        public void StartPeriodicChecks()
        {
            _healthCheckTimer.Change(TimeSpan.Zero, _checkInterval);
        }

        public void StopPeriodicChecks()
        {
            _healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void PerformHealthChecks(object state)
        {
            try
            {
                await PerformAllHealthChecks();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error performing health checks: {ex.Message}");
            }
        }

        private async Task PerformAllHealthChecks()
        {
            List<Task> checkTasks = new List<Task>();

            lock (_lock)
            {
                foreach (var healthCheck in _healthChecks.Values)
                {
                    checkTasks.Add(PerformSingleHealthCheck(healthCheck));
                }
            }

            await Task.WhenAll(checkTasks);

            UpdateOverallStatus();
            UpdateSystemMetrics();
        }

        private async Task PerformSingleHealthCheck(HealthCheck healthCheck)
        {
            try
            {
                using var cts = new CancellationTokenSource(healthCheck.Timeout);
                var result = await healthCheck.CheckFunction();

                healthCheck.Status = result.Status;
                healthCheck.LastCheck = DateTime.UtcNow;
                healthCheck.Duration = result.Duration;
                healthCheck.Description = result.Description;
                healthCheck.ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex)
            {
                healthCheck.Status = HealthStatus.Unhealthy;
                healthCheck.LastCheck = DateTime.UtcNow;
                healthCheck.ErrorMessage = ex.Message;
                healthCheck.Description = "Health check failed with exception";
            }
        }

        private void UpdateOverallStatus()
        {
            lock (_lock)
            {
                if (_healthChecks.Count == 0)
                {
                    _overallStatus = HealthStatus.Unknown;
                }
                else if (_healthChecks.Values.All(h => h.Status == HealthStatus.Healthy))
                {
                    _overallStatus = HealthStatus.Healthy;
                }
                else if (_healthChecks.Values.Any(h => h.Status == HealthStatus.Unhealthy))
                {
                    _overallStatus = HealthStatus.Unhealthy;
                }
                else
                {
                    _overallStatus = HealthStatus.Degraded;
                }

                _lastHealthCheck = DateTime.UtcNow;
            }
        }

        private void UpdateSystemMetrics()
        {
            UpdateMetric("cpu_usage_percent", GetCpuUsage());
            UpdateMetric("memory_usage_mb", GetMemoryUsage());
            UpdateMetric("available_memory_mb", GetAvailableMemory());
            UpdateMetric("thread_count", Process.GetCurrentProcess().Threads.Count);
            UpdateMetric("handle_count", Process.GetCurrentProcess().HandleCount);
        }

        private double GetCpuUsage()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;

                Thread.Sleep(100);

                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;

                return cpuUsedMs / (Environment.ProcessorCount * totalMsPassed) * 100;
            }
            catch
            {
                return 0;
            }
        }

        private double GetMemoryUsage()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                return process.WorkingSet64 / (1024.0 * 1024.0);
            }
            catch
            {
                return 0;
            }
        }

        private double GetAvailableMemory()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return memStatus.ullAvailPhys / (1024.0 * 1024.0);
                }
            }
            catch
            {
                // Fallback to process memory if system memory fails
                return GetMemoryUsage();
            }
            return 0;
        }

        private void RegisterDefaultHealthChecks()
        {
            RegisterHealthCheck("ProcessHealth", async () =>
            {
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024.0 * 1024.0);

                if (memoryMB > 1000) // 1GB threshold
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Degraded,
                        Description = $"High memory usage: {memoryMB:F1}MB"
                    };
                }

                return new HealthCheckResult
                {
                    Status = HealthStatus.Healthy,
                    Description = $"Process running normally, memory: {memoryMB:F1}MB"
                };
            });

            RegisterHealthCheck("ThreadHealth", async () =>
            {
                var process = Process.GetCurrentProcess();
                var threadCount = process.Threads.Count;

                if (threadCount > 100) // High thread count threshold
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Degraded,
                        Description = $"High thread count: {threadCount}"
                    };
                }

                return new HealthCheckResult
                {
                    Status = HealthStatus.Healthy,
                    Description = $"Thread count normal: {threadCount}"
                };
            });

            RegisterHealthCheck("ServiceResponsiveness", async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                await Task.Delay(1); // Minimal async operation
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > 100) // 100ms threshold
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Degraded,
                        Description = $"Service responsiveness degraded: {stopwatch.ElapsedMilliseconds}ms"
                    };
                }

                return new HealthCheckResult
                {
                    Status = HealthStatus.Healthy,
                    Description = $"Service responsive: {stopwatch.ElapsedMilliseconds}ms"
                };
            });

            // Configuration health checks
            RegisterHealthCheck("ConfigurationHealth", async () =>
            {
                try
                {
                    var configManager = ConfigurationManager.Instance;
                    if (configManager == null)
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Unhealthy,
                            Description = "Configuration manager is not available"
                        };
                    }

                    // Check if advanced features are enabled
                    if (!configManager.AdvancedFeaturesEnabled)
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Degraded,
                            Description = "Advanced configuration monitoring features are not enabled"
                        };
                    }

                    // Get configuration health status
                    var configHealthStatus = configManager.HealthStatus;
                    var healthReport = configManager.HealthReport;

                    if (configHealthStatus == ConfigurationHealthStatus.Error)
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Unhealthy,
                            Description = $"Configuration health check failed: {healthReport?.RecentIssues.Count ?? 0} issues"
                        };
                    }
                    else if (configHealthStatus == ConfigurationHealthStatus.Warning)
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Degraded,
                            Description = $"Configuration has warnings: {healthReport?.RecentIssues.Count ?? 0} issues"
                        };
                    }
                    else if (configHealthStatus == ConfigurationHealthStatus.Unknown)
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Degraded,
                            Description = "Configuration health status is unknown"
                        };
                    }

                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Healthy,
                        Description = $"Configuration is healthy: {healthReport?.HealthChecks?.Count ?? 0} checks passed"
                    };
                }
                catch (Exception ex)
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = $"Configuration health check failed: {ex.Message}"
                    };
                }
            });

            RegisterHealthCheck("ConfigurationFileAccess", async () =>
            {
                try
                {
                    var configManager = ConfigurationManager.Instance;
                    if (configManager == null)
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Degraded,
                            Description = "Configuration manager is not available"
                        };
                    }

                    var configPath = configManager.ConfigFilePath;
                    if (string.IsNullOrEmpty(configPath) || !System.IO.File.Exists(configPath))
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Unhealthy,
                            Description = "Configuration file is not accessible"
                        };
                    }

                    // Test file access
                    using (var stream = System.IO.File.OpenRead(configPath))
                    {
                        // Just test access
                    }

                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Healthy,
                        Description = "Configuration file is accessible"
                    };
                }
                catch (Exception ex)
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = $"Configuration file access failed: {ex.Message}"
                    };
                }
            });

            RegisterHealthCheck("ConfigurationReloadHealth", async () =>
            {
                try
                {
                    var configManager = ConfigurationManager.Instance;
                    if (configManager == null)
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Degraded,
                            Description = "Configuration manager is not available"
                        };
                    }

                    var reloadErrors = configManager.ReloadErrors;
                    var lastSuccessfulReload = configManager.LastSuccessfulReload;
                    var timeSinceLastReload = DateTime.UtcNow - lastSuccessfulReload;

                    // Check for recent reload errors
                    if (reloadErrors.Count > 3)
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Unhealthy,
                            Description = $"Too many recent reload errors: {reloadErrors.Count}"
                        };
                    }

                    // Check if last reload was too long ago
                    if (timeSinceLastReload > TimeSpan.FromHours(24))
                    {
                        return new HealthCheckResult
                        {
                            Status = HealthStatus.Degraded,
                            Description = $"Configuration hasn't been reloaded recently: {timeSinceLastReload.TotalHours:F1} hours"
                        };
                    }

                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Healthy,
                        Description = $"Configuration reload is healthy: {reloadErrors.Count} recent errors"
                    };
                }
                catch (Exception ex)
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = $"Configuration reload health check failed: {ex.Message}"
                    };
                }
            });

            // Register default metrics
            RegisterMetric("uptime_seconds", () => (DateTime.UtcNow - Process.GetCurrentProcess().StartTime).TotalSeconds, "seconds", "Service uptime");
            RegisterMetric("gc_collections_gen0", () => GC.CollectionCount(0), "count", "Generation 0 garbage collections");
            RegisterMetric("gc_collections_gen1", () => GC.CollectionCount(1), "count", "Generation 1 garbage collections");
            RegisterMetric("gc_collections_gen2", () => GC.CollectionCount(2), "count", "Generation 2 garbage collections");

            // Configuration-specific metrics
            RegisterMetric("config_health_status", () =>
            {
                var configManager = ConfigurationManager.Instance;
                if (configManager == null) return 0;

                return configManager.AdvancedFeaturesEnabled ?
                    (configManager.HealthStatus == ConfigurationHealthStatus.Healthy ? 1 :
                     configManager.HealthStatus == ConfigurationHealthStatus.Warning ? 0.5 : 0) : 0;
            }, "status", "Configuration health status (1=Healthy, 0.5=Warning, 0=Error/Unknown)");

            RegisterMetric("config_reload_errors", () =>
            {
                var configManager = ConfigurationManager.Instance;
                return configManager?.ReloadErrors.Count ?? 0;
            }, "count", "Configuration reload errors count");

            RegisterMetric("config_last_reload_seconds_ago", () =>
            {
                var configManager = ConfigurationManager.Instance;
                if (configManager == null) return 0;

                var timeSinceLastReload = DateTime.UtcNow - configManager.LastSuccessfulReload;
                return timeSinceLastReload.TotalSeconds;
            }, "seconds", "Seconds since last successful configuration reload");

            RegisterMetric("config_backup_count", () =>
            {
                var configManager = ConfigurationManager.Instance;
                return configManager?.GetAvailableBackups().Count ?? 0;
            }, "count", "Available configuration backups count");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _healthCheckTimer?.Dispose();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }
    }

    public class HealthCheck
    {
        public string Name { get; set; }
        public Func<Task<HealthCheckResult>> CheckFunction { get; set; }
        public TimeSpan Timeout { get; set; }
        public HealthStatus Status { get; set; }
        public DateTime LastCheck { get; set; }
        public TimeSpan Duration { get; set; }
        public string Description { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class HealthCheckResult
    {
        public HealthStatus Status { get; set; }
        public string Description { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class HealthMetric
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }
        public Func<double> ValueFunction { get; set; }
        public double Value { get; set; }
        public DateTime LastUpdated { get; set; }
        public Queue<MetricValue> History { get; set; }
    }

    public class MetricValue
    {
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class HealthReport
    {
        public HealthStatus OverallStatus { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public Dictionary<string, HealthCheck> HealthChecks { get; set; }
        public Dictionary<string, HealthMetric> Metrics { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public static class HealthStatusExtensions
    {
        public static string ToFriendlyString(this HealthStatus status)
        {
            return status switch
            {
                HealthStatus.Healthy => "Healthy",
                HealthStatus.Degraded => "Degraded",
                HealthStatus.Unhealthy => "Unhealthy",
                HealthStatus.Unknown => "Unknown",
                _ => "Unknown"
            };
        }

        public static bool IsOperational(this HealthStatus status)
        {
            return status == HealthStatus.Healthy || status == HealthStatus.Degraded;
        }
    }
}