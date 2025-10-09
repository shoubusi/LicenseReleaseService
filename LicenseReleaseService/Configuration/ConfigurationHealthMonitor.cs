using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Monitors configuration health and provides comprehensive diagnostics
    /// </summary>
    public class ConfigurationHealthMonitor : IDisposable
    {
        private readonly object _lock = new object();
        private readonly System.Threading.Timer _healthCheckTimer;
        private readonly System.Threading.Timer _metricsCollectionTimer;
        private readonly System.Threading.Timer _diagnosticTimer;
        private readonly List<ConfigurationHealthRule> _healthRules;
        private readonly Dictionary<string, ConfigurationMetric> _metrics;
        private readonly Queue<HealthCheckResult> _healthCheckHistory;
        private readonly Queue<ConfigurationDiagnostic> _diagnosticHistory;

        private bool _isDisposed;
        private bool _isRunning;
        private int _healthCheckInterval;
        private int _metricsInterval;
        private int _diagnosticInterval;
        private int _maxHistorySize;
        private int _maxDiagnosticHistory;
        private ConfigurationHealthStatus _currentHealthStatus;
        private DateTime _lastHealthCheck;
        private DateTime _lastMetricsCollection;
        private DateTime _lastDiagnostic;

        // Event handlers
        public event ConfigurationEvents.ConfigurationHealthEventHandler HealthCheckCompleted;
        public event EventHandler<ConfigurationHealthAlertEventArgs> HealthAlertRaised;
        public event EventHandler<ConfigurationDiagnosticEventArgs> DiagnosticCompleted;

        /// <summary>
        /// Gets the current health status
        /// </summary>
        public ConfigurationHealthStatus CurrentHealthStatus
        {
            get
            {
                lock (_lock)
                {
                    return _currentHealthStatus;
                }
            }
        }

        /// <summary>
        /// Gets the last health check timestamp
        /// </summary>
        public DateTime LastHealthCheck => _lastHealthCheck;

        /// <summary>
        /// Gets the health check history
        /// </summary>
        public IReadOnlyList<HealthCheckResult> HealthCheckHistory
        {
            get
            {
                lock (_lock)
                {
                    return _healthCheckHistory.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the current metrics
        /// </summary>
        public IReadOnlyDictionary<string, ConfigurationMetric> CurrentMetrics
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, ConfigurationMetric>(_metrics);
                }
            }
        }

        /// <summary>
        /// Gets the diagnostic history
        /// </summary>
        public IReadOnlyList<ConfigurationDiagnostic> DiagnosticHistory
        {
            get
            {
                lock (_lock)
                {
                    return _diagnosticHistory.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the health monitor statistics
        /// </summary>
        public HealthMonitorStatistics Statistics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationHealthMonitor class
        /// </summary>
        public ConfigurationHealthMonitor(
            int healthCheckInterval = 30000,
            int metricsInterval = 10000,
            int diagnosticInterval = 300000)
        {
            _healthCheckInterval = healthCheckInterval;
            _metricsInterval = metricsInterval;
            _diagnosticInterval = diagnosticInterval;
            _maxHistorySize = 100;
            _maxDiagnosticHistory = 50;
            _currentHealthStatus = ConfigurationHealthStatus.Unknown;
            _healthRules = new List<ConfigurationHealthRule>();
            _metrics = new Dictionary<string, ConfigurationMetric>();
            _healthCheckHistory = new Queue<HealthCheckResult>();
            _diagnosticHistory = new Queue<ConfigurationDiagnostic>();

            Statistics = new HealthMonitorStatistics();

            // Initialize timers
            _healthCheckTimer = new Timer(OnHealthCheckTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _metricsCollectionTimer = new Timer(OnMetricsTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _diagnosticTimer = new Timer(OnDiagnosticTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

            // Register default health rules
            RegisterDefaultHealthRules();

            // Register default metrics
            RegisterDefaultMetrics();
        }

        /// <summary>
        /// Starts the health monitor
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationHealthMonitor));

                if (_isRunning)
                    return;

                _isRunning = true;

                // Start timers
                _healthCheckTimer.Change(_healthCheckInterval, _healthCheckInterval);
                _metricsCollectionTimer.Change(_metricsInterval, _metricsInterval);
                _diagnosticTimer.Change(_diagnosticInterval, _diagnosticInterval);

                Statistics.StartTime = DateTime.UtcNow;
                LogInfo("Configuration health monitor started");
            }
        }

        /// <summary>
        /// Stops the health monitor
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_isDisposed || !_isRunning)
                    return;

                _isRunning = false;

                // Stop timers
                _healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _metricsCollectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _diagnosticTimer.Change(Timeout.Infinite, Timeout.Infinite);

                Statistics.StopTime = DateTime.UtcNow;
                LogInfo("Configuration health monitor stopped");
            }
        }

        /// <summary>
        /// Performs an immediate health check
        /// </summary>
        public async Task<ConfigurationHealthStatus> PerformHealthCheckAsync()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationHealthMonitor));
            }

            try
            {
                var startTime = DateTime.UtcNow;
                var healthChecks = new Dictionary<string, HealthCheckResult>();
                var allIssues = new List<string>();

                // Execute all health rules
                foreach (var rule in _healthRules)
                {
                    try
                    {
                        var result = await rule.ExecuteAsync();
                        healthChecks[rule.Name] = result;

                        if (result.Status == ConfigurationHealthStatus.Error)
                        {
                            allIssues.Add($"{rule.Name}: {result.ErrorMessage ?? result.Description}");
                        }
                        else if (result.Status == ConfigurationHealthStatus.Warning)
                        {
                            allIssues.Add($"{rule.Name}: {result.Description}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorResult = new HealthCheckResult(
                            ConfigurationHealthStatus.Error,
                            $"Health rule execution failed: {ex.Message}",
                            rule.Name,
                            DateTime.UtcNow - startTime,
                            ex.Message);

                        healthChecks[rule.Name] = errorResult;
                        allIssues.Add($"{rule.Name}: {ex.Message}");
                    }
                }

                // Determine overall health status
                var overallStatus = DetermineOverallHealthStatus(healthChecks.Values);

                // Update current status
                lock (_lock)
                {
                    _currentHealthStatus = overallStatus;
                    _lastHealthCheck = DateTime.UtcNow;

                    // Add to history
                    var overallResult = new HealthCheckResult(
                        overallStatus,
                        $"Overall health status: {overallStatus}",
                        "Overall",
                        DateTime.UtcNow - startTime,
                        allIssues.Count > 0 ? string.Join("; ", allIssues) : null);

                    _healthCheckHistory.Enqueue(overallResult);
                    while (_healthCheckHistory.Count > _maxHistorySize)
                    {
                        _healthCheckHistory.Dequeue();
                    }
                }

                // Update statistics
                Statistics.TotalHealthChecks++;
                if (overallStatus == ConfigurationHealthStatus.Healthy)
                {
                    Statistics.TotalHealthyChecks++;
                }
                else if (overallStatus == ConfigurationHealthStatus.Error)
                {
                    Statistics.TotalFailedChecks++;
                }

                // Collect current metrics
                var currentMetrics = GetCurrentMetricsSnapshot();

                // Raise health check completed event
                var healthArgs = new ConfigurationHealthEventArgs(
                    overallStatus, healthChecks, currentMetrics, DateTime.UtcNow, DateTime.UtcNow - startTime, allIssues);

                OnHealthCheckCompleted(healthArgs);

                // Check for health alerts
                if (overallStatus != ConfigurationHealthStatus.Healthy)
                {
                    var alertArgs = new ConfigurationHealthAlertEventArgs(
                        overallStatus, allIssues, DateTime.UtcNow, healthChecks);
                    OnHealthAlertRaised(alertArgs);
                }

                return overallStatus;
            }
            catch (Exception ex)
            {
                Statistics.TotalFailedChecks++;

                lock (_lock)
                {
                    _currentHealthStatus = ConfigurationHealthStatus.Error;
                    _lastHealthCheck = DateTime.UtcNow;
                }

                LogError($"Health check failed: {ex.Message}");
                return ConfigurationHealthStatus.Error;
            }
        }

        /// <summary>
        /// Adds a custom health rule
        /// </summary>
        public void AddHealthRule(ConfigurationHealthRule rule)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationHealthMonitor));

                if (rule == null)
                    throw new ArgumentNullException(nameof(rule));

                _healthRules.Add(rule);
                Statistics.TotalRulesRegistered++;
            }
        }

        /// <summary>
        /// Removes a health rule
        /// </summary>
        public bool RemoveHealthRule(string ruleName)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationHealthMonitor));

                var rule = _healthRules.FirstOrDefault(r => r.Name == ruleName);
                if (rule != null)
                {
                    _healthRules.Remove(rule);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets a health report
        /// </summary>
        public ConfigurationHealthReport GetHealthReport()
        {
            lock (_lock)
            {
                var report = new ConfigurationHealthReport
                {
                    OverallStatus = _currentHealthStatus,
                    LastHealthCheck = _lastHealthCheck,
                    GeneratedAt = DateTime.UtcNow,
                    HealthChecks = new Dictionary<string, HealthCheckResult>(),
                    Metrics = new Dictionary<string, ConfigurationMetric>(_metrics),
                    ActiveRules = _healthRules.Select(r => r.Name).ToList(),
                    RecentIssues = GetRecentIssues(),
                    Statistics = Statistics
                };

                // Add recent health check results
                foreach (var result in _healthCheckHistory.Skip(Math.Max(0, _healthCheckHistory.Count - 10)))
                {
                    report.HealthChecks[result.ComponentName] = result;
                }

                return report;
            }
        }

        /// <summary>
        /// Sets the health check interval
        /// </summary>
        public void SetHealthCheckInterval(int intervalMilliseconds)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(ConfigurationHealthMonitor));

                _healthCheckInterval = intervalMilliseconds;

                if (_isRunning)
                {
                    _healthCheckTimer.Change(intervalMilliseconds, intervalMilliseconds);
                }
            }
        }

        /// <summary>
        /// Forces a diagnostic collection
        /// </summary>
        public async Task<ConfigurationDiagnostic> ForceDiagnosticAsync()
        {
            return await CollectDiagnosticsAsync();
        }

        private async void OnHealthCheckTimerElapsed(object state)
        {
            if (!_isRunning)
                return;

            try
            {
                await PerformHealthCheckAsync();
            }
            catch (Exception ex)
            {
                LogError($"Health check timer elapsed error: {ex.Message}");
            }
        }

        private async void OnMetricsTimerElapsed(object state)
        {
            if (!_isRunning)
                return;

            try
            {
                await CollectMetricsAsync();
            }
            catch (Exception ex)
            {
                LogError($"Metrics collection timer elapsed error: {ex.Message}");
            }
        }

        private async void OnDiagnosticTimerElapsed(object state)
        {
            if (!_isRunning)
                return;

            try
            {
                await CollectDiagnosticsAsync();
            }
            catch (Exception ex)
            {
                LogError($"Diagnostic timer elapsed error: {ex.Message}");
            }
        }

        private async Task CollectMetricsAsync()
        {
            try
            {
                // Collect system metrics
                UpdateMetric("cpu_usage_percent", GetCpuUsage());
                UpdateMetric("memory_usage_mb", GetMemoryUsage());
                UpdateMetric("available_memory_mb", GetAvailableMemory());
                UpdateMetric("health_check_duration_ms", GetAverageHealthCheckDuration());
                UpdateMetric("health_checks_total", Statistics.TotalHealthChecks);
                UpdateMetric("health_checks_failed", Statistics.TotalFailedChecks);
                UpdateMetric("uptime_seconds", (DateTime.UtcNow - Statistics.StartTime).TotalSeconds);

                _lastMetricsCollection = DateTime.UtcNow;
                Statistics.TotalMetricsCollections++;
            }
            catch (Exception ex)
            {
                LogError($"Metrics collection failed: {ex.Message}");
            }
        }

        private async Task<ConfigurationDiagnostic> CollectDiagnosticsAsync()
        {
            var startTime = DateTime.UtcNow;
            var diagnostic = new ConfigurationDiagnostic
            {
                Id = Guid.NewGuid(),
                Timestamp = startTime,
                HealthStatus = _currentHealthStatus,
                Metrics = new Dictionary<string, ConfigurationMetric>(_metrics)
            };

            try
            {
                // Collect diagnostic information
                diagnostic.SystemInfo = CollectSystemInfo();
                diagnostic.ConfigurationInfo = CollectConfigurationInfo();
                diagnostic.HealthCheckSummary = CollectHealthCheckSummary();
                diagnostic.RecentErrors = CollectRecentErrors();
                diagnostic.PerformanceMetrics = CollectPerformanceMetrics();

                diagnostic.Duration = DateTime.UtcNow - startTime;
                diagnostic.IsSuccess = true;

                lock (_lock)
                {
                    _diagnosticHistory.Enqueue(diagnostic);
                    while (_diagnosticHistory.Count > _maxDiagnosticHistory)
                    {
                        _diagnosticHistory.Dequeue();
                    }
                }

                _lastDiagnostic = DateTime.UtcNow;
                Statistics.TotalDiagnosticsCollected++;

                var diagnosticArgs = new ConfigurationDiagnosticEventArgs(diagnostic);
                OnDiagnosticCompleted(diagnosticArgs);

                LogInfo($"Diagnostic collected successfully: {diagnostic.Id}");
            }
            catch (Exception ex)
            {
                diagnostic.IsSuccess = false;
                diagnostic.ErrorMessage = ex.Message;
                diagnostic.Duration = DateTime.UtcNow - startTime;

                LogError($"Diagnostic collection failed: {ex.Message}");
            }

            return diagnostic;
        }

        private Dictionary<string, object> CollectSystemInfo()
        {
            return new Dictionary<string, object>
            {
                { "MachineName", Environment.MachineName },
                { "OSVersion", Environment.OSVersion.ToString() },
                { "ProcessorCount", Environment.ProcessorCount },
                { "WorkingSet", Environment.WorkingSet },
                { "Is64BitProcess", Environment.Is64BitProcess },
                { "Is64BitOperatingSystem", Environment.Is64BitOperatingSystem },
                { "TickCount", Environment.TickCount },
                { "CurrentDirectory", Environment.CurrentDirectory }
            };
        }

        private Dictionary<string, object> CollectConfigurationInfo()
        {
            var configInfo = new Dictionary<string, object>
            {
                { "ConfigFilePath", ConfigurationManager.Instance?.ConfigFilePath },
                { "LastConfigChange", ConfigurationManager.Instance?.LastConfigChange },
                { "LastSuccessfulReload", ConfigurationManager.Instance?.LastSuccessfulReload },
                { "IsConfigurationValid", ConfigurationManager.Instance?.IsConfigurationValid },
                { "ReloadErrorsCount", ConfigurationManager.Instance?.ReloadErrors.Count ?? 0 }
            };

            return configInfo;
        }

        private Dictionary<string, object> CollectHealthCheckSummary()
        {
            var summary = new Dictionary<string, object>
            {
                { "TotalHealthChecks", Statistics.TotalHealthChecks },
                { "HealthyChecks", Statistics.TotalHealthyChecks },
                { "FailedChecks", Statistics.TotalFailedChecks },
                { "CurrentHealthStatus", _currentHealthStatus.ToString() },
                { "LastHealthCheck", _lastHealthCheck },
                { "ActiveRulesCount", _healthRules.Count },
                { "MetricsCount", _metrics.Count }
            };

            return summary;
        }

        private List<string> CollectRecentErrors()
        {
            var errors = new List<string>();

            lock (_lock)
            {
                // Get recent errors from health check history
                foreach (var result in _healthCheckHistory.Skip(Math.Max(0, _healthCheckHistory.Count - 10)))
                {
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        errors.Add($"{result.ComponentName}: {result.ErrorMessage}");
                    }
                }
            }

            return errors;
        }

        private Dictionary<string, object> CollectPerformanceMetrics()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            return new Dictionary<string, object>
            {
                { "ProcessId", process.Id },
                { "ProcessName", process.ProcessName },
                { "StartTime", process.StartTime },
                { "TotalProcessorTime", process.TotalProcessorTime },
                { "UserProcessorTime", process.UserProcessorTime },
                { "PrivilegedProcessorTime", process.PrivilegedProcessorTime },
                { "WorkingSet64", process.WorkingSet64 },
                { "VirtualMemorySize64", process.VirtualMemorySize64 },
                { "PrivateMemorySize64", process.PrivateMemorySize64 },
                { "PagedMemorySize64", process.PagedMemorySize64 },
                { "PagedSystemMemorySize64", process.PagedSystemMemorySize64 },
                { "NonpagedSystemMemorySize64", process.NonpagedSystemMemorySize64 },
                { "PeakWorkingSet64", process.PeakWorkingSet64 },
                { "PeakVirtualMemorySize64", process.PeakVirtualMemorySize64 },
                { "Threads", process.Threads.Count },
                { "Handles", process.HandleCount }
            };
        }

        private List<string> GetRecentIssues()
        {
            var issues = new List<string>();

            lock (_lock)
            {
                foreach (var result in _healthCheckHistory.Skip(Math.Max(0, _healthCheckHistory.Count - 5)))
                {
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        issues.Add($"[{result.ComponentName}] {result.ErrorMessage}");
                    }
                    else if (result.Status == ConfigurationHealthStatus.Warning)
                    {
                        issues.Add($"[{result.ComponentName}] {result.Description}");
                    }
                }
            }

            return issues;
        }

        private Dictionary<string, ConfigurationMetric> GetCurrentMetricsSnapshot()
        {
            lock (_lock)
            {
                return new Dictionary<string, ConfigurationMetric>(_metrics);
            }
        }

        private void UpdateMetric(string name, double value)
        {
            lock (_lock)
            {
                if (_metrics.TryGetValue(name, out var metric))
                {
                    metric.Value = value;
                    metric.LastUpdated = DateTime.UtcNow;
                    metric.AddHistoryValue(value);
                }
                else
                {
                    metric = new ConfigurationMetric(name, value, "", "");
                    _metrics[name] = metric;
                }
            }
        }

        private double GetCpuUsage()
        {
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
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
                var process = System.Diagnostics.Process.GetCurrentProcess();
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
                var process = System.Diagnostics.Process.GetCurrentProcess();
                return process.PrivateMemorySize64 / (1024.0 * 1024.0);
            }
            catch
            {
                return 0;
            }
        }

        private double GetAverageHealthCheckDuration()
        {
            lock (_lock)
            {
                if (_healthCheckHistory.Count == 0)
                    return 0;

                return _healthCheckHistory.Average(h => h.Duration.TotalMilliseconds);
            }
        }

        private ConfigurationHealthStatus DetermineOverallHealthStatus(IEnumerable<HealthCheckResult> healthChecks)
        {
            var hasErrors = false;
            var hasWarnings = false;
            var hasHealthy = false;

            foreach (var check in healthChecks)
            {
                switch (check.Status)
                {
                    case ConfigurationHealthStatus.Error:
                        hasErrors = true;
                        break;
                    case ConfigurationHealthStatus.Warning:
                        hasWarnings = true;
                        break;
                    case ConfigurationHealthStatus.Healthy:
                        hasHealthy = true;
                        break;
                }
            }

            if (hasErrors)
                return ConfigurationHealthStatus.Error;
            if (hasWarnings)
                return ConfigurationHealthStatus.Warning;
            if (hasHealthy)
                return ConfigurationHealthStatus.Healthy;

            return ConfigurationHealthStatus.Unknown;
        }

        private void RegisterDefaultHealthRules()
        {
            // Configuration file accessibility rule
            AddHealthRule(new ConfigurationHealthRule(
                "ConfigurationFileAccessibility",
                async () =>
                {
                    try
                    {
                        var configPath = ConfigurationManager.Instance?.ConfigFilePath;
                        if (string.IsNullOrEmpty(configPath))
                        {
                            return new HealthCheckResult(
                                ConfigurationHealthStatus.Error,
                                "Configuration file path is not available",
                                "ConfigurationFileAccessibility",
                                TimeSpan.FromMilliseconds(0),
                                "Configuration manager not initialized");
                        }

                        if (!File.Exists(configPath))
                        {
                            return new HealthCheckResult(
                                ConfigurationHealthStatus.Error,
                                "Configuration file does not exist",
                                "ConfigurationFileAccessibility",
                                TimeSpan.FromMilliseconds(0),
                                $"File not found: {configPath}");
                        }

                        // Try to read the file
                        using (var stream = File.OpenRead(configPath))
                        {
                            // Just test access
                        }

                        return new HealthCheckResult(
                            ConfigurationHealthStatus.Healthy,
                            "Configuration file is accessible",
                            "ConfigurationFileAccessibility",
                            TimeSpan.FromMilliseconds(0));
                    }
                    catch (Exception ex)
                    {
                        return new HealthCheckResult(
                            ConfigurationHealthStatus.Error,
                            "Configuration file access failed",
                            "ConfigurationFileAccessibility",
                            TimeSpan.FromMilliseconds(0),
                            ex.Message);
                    }
                }));

            // Configuration validity rule
            AddHealthRule(new ConfigurationHealthRule(
                "ConfigurationValidity",
                async () =>
                {
                    try
                    {
                        var configManager = ConfigurationManager.Instance;
                        if (configManager == null)
                        {
                            return new HealthCheckResult(
                                ConfigurationHealthStatus.Error,
                                "Configuration manager is not available",
                                "ConfigurationValidity",
                                TimeSpan.FromMilliseconds(0),
                                "Configuration manager not initialized");
                        }

                        var isValid = configManager.IsConfigurationValid;
                        var errors = configManager.ValidateConfiguration();

                        if (!isValid)
                        {
                            return new HealthCheckResult(
                                ConfigurationHealthStatus.Error,
                                $"Configuration validation failed: {errors.Count} errors",
                                "ConfigurationValidity",
                                TimeSpan.FromMilliseconds(0),
                                string.Join("; ", errors));
                        }

                        return new HealthCheckResult(
                            ConfigurationHealthStatus.Healthy,
                            "Configuration is valid",
                            "ConfigurationValidity",
                            TimeSpan.FromMilliseconds(0));
                    }
                    catch (Exception ex)
                    {
                        return new HealthCheckResult(
                            ConfigurationHealthStatus.Error,
                            "Configuration validation check failed",
                            "ConfigurationValidity",
                            TimeSpan.FromMilliseconds(0),
                            ex.Message);
                    }
                }));

            // Recent reload errors rule
            AddHealthRule(new ConfigurationHealthRule(
                "RecentReloadErrors",
                async () =>
                {
                    try
                    {
                        var configManager = ConfigurationManager.Instance;
                        if (configManager == null)
                        {
                            return new HealthCheckResult(
                                ConfigurationHealthStatus.Unknown,
                                "Configuration manager is not available",
                                "RecentReloadErrors",
                                TimeSpan.FromMilliseconds(0));
                        }

                        var recentErrors = configManager.ReloadErrors.Count;
                        if (recentErrors > 5)
                        {
                            return new HealthCheckResult(
                                ConfigurationHealthStatus.Warning,
                                $"High number of recent reload errors: {recentErrors}",
                                "RecentReloadErrors",
                                TimeSpan.FromMilliseconds(0));
                        }

                        return new HealthCheckResult(
                            ConfigurationHealthStatus.Healthy,
                            $"Recent reload errors count is normal: {recentErrors}",
                            "RecentReloadErrors",
                            TimeSpan.FromMilliseconds(0));
                    }
                    catch (Exception ex)
                    {
                        return new HealthCheckResult(
                            ConfigurationHealthStatus.Error,
                            "Recent reload errors check failed",
                            "RecentReloadErrors",
                            TimeSpan.FromMilliseconds(0),
                            ex.Message);
                    }
                }));

            // Memory usage rule
            AddHealthRule(new ConfigurationHealthRule(
                "MemoryUsage",
                async () =>
                {
                    try
                    {
                        var memoryMB = GetMemoryUsage();
                        var threshold = 500; // 500MB threshold

                        if (memoryMB > threshold * 1.5)
                        {
                            return new HealthCheckResult(
                                ConfigurationHealthStatus.Error,
                                $"Critical memory usage: {memoryMB:F1}MB",
                                "MemoryUsage",
                                TimeSpan.FromMilliseconds(0));
                        }
                        else if (memoryMB > threshold)
                        {
                            return new HealthCheckResult(
                                ConfigurationHealthStatus.Warning,
                                $"High memory usage: {memoryMB:F1}MB",
                                "MemoryUsage",
                                TimeSpan.FromMilliseconds(0));
                        }

                        return new HealthCheckResult(
                            ConfigurationHealthStatus.Healthy,
                            $"Memory usage is normal: {memoryMB:F1}MB",
                            "MemoryUsage",
                            TimeSpan.FromMilliseconds(0));
                    }
                    catch (Exception ex)
                    {
                        return new HealthCheckResult(
                            ConfigurationHealthStatus.Error,
                            "Memory usage check failed",
                            "MemoryUsage",
                            TimeSpan.FromMilliseconds(0),
                            ex.Message);
                    }
                }));
        }

        private void RegisterDefaultMetrics()
        {
            UpdateMetric("health_check_interval_ms", _healthCheckInterval);
            UpdateMetric("metrics_collection_interval_ms", _metricsInterval);
            UpdateMetric("diagnostic_interval_ms", _diagnosticInterval);
            UpdateMetric("max_history_size", _maxHistorySize);
            UpdateMetric("health_monitor_uptime", 0);
        }

        protected virtual void OnHealthCheckCompleted(ConfigurationHealthEventArgs e)
        {
            HealthCheckCompleted?.Invoke(this, e);
        }

        protected virtual void OnHealthAlertRaised(ConfigurationHealthAlertEventArgs e)
        {
            HealthAlertRaised?.Invoke(this, e);
        }

        protected virtual void OnDiagnosticCompleted(ConfigurationDiagnosticEventArgs e)
        {
            DiagnosticCompleted?.Invoke(this, e);
        }

        private void LogInfo(string message)
        {
            Console.WriteLine($"[ConfigurationHealthMonitor] {message}");
        }

        private void LogWarning(string message)
        {
            Console.WriteLine($"[ConfigurationHealthMonitor Warning] {message}");
        }

        private void LogError(string message)
        {
            Console.WriteLine($"[ConfigurationHealthMonitor Error] {message}");
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
                    Stop();
                    _healthCheckTimer?.Dispose();
                    _metricsCollectionTimer?.Dispose();
                    _diagnosticTimer?.Dispose();
                }

                _isDisposed = true;
            }
        }

        ~ConfigurationHealthMonitor()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Health rule for configuration monitoring
    /// </summary>
    public class ConfigurationHealthRule
    {
        public string Name { get; }
        public Func<Task<HealthCheckResult>> CheckFunction { get; }

        public ConfigurationHealthRule(string name, Func<Task<HealthCheckResult>> checkFunction)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            CheckFunction = checkFunction ?? throw new ArgumentNullException(nameof(checkFunction));
        }

        public async Task<HealthCheckResult> ExecuteAsync()
        {
            return await CheckFunction();
        }
    }

    /// <summary>
    /// Configuration health report
    /// </summary>
    public class ConfigurationHealthReport
    {
        public ConfigurationHealthStatus OverallStatus { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, HealthCheckResult> HealthChecks { get; set; }
        public Dictionary<string, ConfigurationMetric> Metrics { get; set; }
        public List<string> ActiveRules { get; set; }
        public List<string> RecentIssues { get; set; }
        public HealthMonitorStatistics Statistics { get; set; }
    }

    /// <summary>
    /// Configuration diagnostic information
    /// </summary>
    public class ConfigurationDiagnostic
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public ConfigurationHealthStatus HealthStatus { get; set; }
        public Dictionary<string, object> SystemInfo { get; set; }
        public Dictionary<string, object> ConfigurationInfo { get; set; }
        public Dictionary<string, object> HealthCheckSummary { get; set; }
        public Dictionary<string, ConfigurationMetric> Metrics { get; set; }
        public List<string> RecentErrors { get; set; }
        public Dictionary<string, object> PerformanceMetrics { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Health monitor statistics
    /// </summary>
    public class HealthMonitorStatistics
    {
        public DateTime StartTime { get; set; }
        public DateTime StopTime { get; set; }
        public long TotalHealthChecks { get; set; }
        public long TotalHealthyChecks { get; set; }
        public long TotalFailedChecks { get; set; }
        public long TotalMetricsCollections { get; set; }
        public long TotalDiagnosticsCollected { get; set; }
        public long TotalRulesRegistered { get; set; }
        public long TotalAlertsRaised { get; set; }
        public TimeSpan Uptime => StopTime > StartTime ? StopTime - StartTime : DateTime.UtcNow - StartTime;
    }

    /// <summary>
    /// Event arguments for configuration health alerts
    /// </summary>
    public class ConfigurationHealthAlertEventArgs : EventArgs
    {
        public ConfigurationHealthStatus AlertLevel { get; set; }
        public List<string> Issues { get; set; }
        public DateTime AlertTime { get; set; }
        public Dictionary<string, HealthCheckResult> HealthChecks { get; set; }

        public ConfigurationHealthAlertEventArgs(ConfigurationHealthStatus alertLevel, List<string> issues, DateTime alertTime, Dictionary<string, HealthCheckResult> healthChecks)
        {
            AlertLevel = alertLevel;
            Issues = issues;
            AlertTime = alertTime;
            HealthChecks = healthChecks;
        }
    }

    /// <summary>
    /// Event arguments for configuration diagnostics
    /// </summary>
    public class ConfigurationDiagnosticEventArgs : EventArgs
    {
        public ConfigurationDiagnostic Diagnostic { get; set; }

        public ConfigurationDiagnosticEventArgs(ConfigurationDiagnostic diagnostic)
        {
            Diagnostic = diagnostic;
        }
    }
}