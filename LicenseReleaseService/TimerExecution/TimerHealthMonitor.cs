using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Monitors the health of timer execution and provides proactive issue detection
    /// </summary>
    public class TimerHealthMonitor : IDisposable
    {
        private readonly ILogger<TimerHealthMonitor> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly ITimerExecutionService _timerService;
        private readonly TimerCircuitBreaker _circuitBreaker;
        private readonly System.Timers.Timer _healthCheckTimer;
        private readonly object _healthLock;
        private readonly Queue<TimerHealthCheckResult> _healthHistory;
        private readonly Dictionary<string, HealthMetric> _metrics;

        private bool _isRunning;
        private bool _isDisposed;
        private TimeSpan _healthCheckInterval;
        private TimerHealthStatus _currentStatus;
        private DateTime _lastHealthCheck;
        private DateTime _lastStatusChange;
        private int _consecutiveHealthFailures;
        private CancellationTokenSource _cancellationTokenSource;

        #region Events

        /// <summary>
        /// Occurs when a health check is completed
        /// </summary>
        public event EventHandler<TimerHealthEventArgs> HealthCheckCompleted;

        /// <summary>
        /// Occurs when the health status changes
        /// </summary>
        public event EventHandler<TimerHealthStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// Occurs when a health issue is detected
        /// </summary>
        public event EventHandler<TimerHealthIssueEventArgs> HealthIssueDetected;

        /// <summary>
        /// Occurs when health metrics are updated
        /// </summary>
        public event EventHandler<TimerHealthMetricsEventArgs> MetricsUpdated;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current health status
        /// </summary>
        public TimerHealthStatus CurrentStatus => _currentStatus;

        /// <summary>
        /// Gets the last health check timestamp
        /// </summary>
        public DateTime LastHealthCheck => _lastHealthCheck;

        /// <summary>
        /// Gets the last status change timestamp
        /// </summary>
        public DateTime LastStatusChange => _lastStatusChange;

        /// <summary>
        /// Gets the consecutive health failure count
        /// </summary>
        public int ConsecutiveHealthFailures => _consecutiveHealthFailures;

        /// <summary>
        /// Gets the health check interval
        /// </summary>
        public TimeSpan HealthCheckInterval => _healthCheckInterval;

        /// <summary>
        /// Gets whether the health monitor is running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the current health score
        /// </summary>
        public int HealthScore => CalculateHealthScore();

        /// <summary>
        /// Gets the current health metrics
        /// </summary>
        public IReadOnlyDictionary<string, HealthMetric> Metrics => new Dictionary<string, HealthMetric>(_metrics);

        #endregion

        /// <summary>
        /// Initializes a new instance of the TimerHealthMonitor class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The timer execution options</param>
        /// <param name="timerService">The timer execution service</param>
        /// <param name="circuitBreaker">The circuit breaker instance</param>
        public TimerHealthMonitor(ILogger<TimerHealthMonitor> logger, TimerExecutionOptions options,
            ITimerExecutionService timerService, TimerCircuitBreaker circuitBreaker)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));

            _healthLock = new object();
            _healthHistory = new Queue<TimerHealthCheckResult>();
            _metrics = new Dictionary<string, HealthMetric>();
            _healthCheckInterval = TimeSpan.FromMinutes(5); // Default 5 minutes
            _currentStatus = TimerHealthStatus.Healthy;

            _healthCheckTimer = new System.Timers.Timer();
            _healthCheckTimer.Elapsed += OnHealthCheckTimerElapsed;

            InitializeMetrics();
        }

        /// <summary>
        /// Starts the health monitor
        /// </summary>
        /// <param name="healthCheckInterval">The health check interval</param>
        public void Start(TimeSpan? healthCheckInterval = null)
        {
            lock (_healthLock)
            {
                if (_isRunning)
                {
                    _logger.LogWarning("Health monitor is already running");
                    return;
                }

                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(TimerHealthMonitor));

                _healthCheckInterval = healthCheckInterval ?? TimeSpan.FromMinutes(5);
                _cancellationTokenSource = new CancellationTokenSource();

                _healthCheckTimer.Interval = _healthCheckInterval.TotalMilliseconds;
                _healthCheckTimer.Enabled = true;

                _isRunning = true;
                _currentStatus = TimerHealthStatus.Healthy;
                _lastStatusChange = DateTime.UtcNow;

                _logger.LogInformation("Health monitor started with interval {Interval}ms", _healthCheckInterval.TotalMilliseconds);

                // Perform initial health check
                Task.Run(() => PerformHealthCheckAsync(_cancellationTokenSource.Token));
            }
        }

        /// <summary>
        /// Stops the health monitor
        /// </summary>
        public void Stop()
        {
            lock (_healthLock)
            {
                if (!_isRunning)
                    return;

                _healthCheckTimer.Enabled = false;
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                _isRunning = false;
                ChangeStatus(TimerHealthStatus.Stopped, "Health monitor stopped");

                _logger.LogInformation("Health monitor stopped");
            }
        }

        /// <summary>
        /// Performs an immediate health check
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The health check result</returns>
        public async Task<TimerHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return await PerformHealthCheckAsync(cancellationToken);
        }

        /// <summary>
        /// Gets the current health status
        /// </summary>
        /// <returns>The current health status</returns>
        public TimerHealthStatusReport GetHealthStatus()
        {
            lock (_healthLock)
            {
                return new TimerHealthStatusReport
                {
                    Status = _currentStatus,
                    HealthScore = HealthScore,
                    LastHealthCheck = _lastHealthCheck,
                    LastStatusChange = _lastStatusChange,
                    ConsecutiveFailures = _consecutiveHealthFailures,
                    Metrics = new Dictionary<string, HealthMetric>(_metrics),
                    RecentHealthChecks = _healthHistory.ToList()
                };
            }
        }

        /// <summary>
        /// Gets health history for a specific time period
        /// </summary>
        /// <param name="since">The start time for the history</param>
        /// <returns>List of health check results</returns>
        public List<TimerHealthCheckResult> GetHealthHistory(DateTime since)
        {
            lock (_healthLock)
            {
                return _healthHistory.Where(h => h.Timestamp >= since).ToList();
            }
        }

        /// <summary>
        /// Updates a health metric
        /// </summary>
        /// <param name="metricName">The metric name</param>
        /// <param name="value">The metric value</param>
        /// <param name="metadata">Optional metadata</param>
        public void UpdateMetric(string metricName, object value, object metadata = null)
        {
            lock (_healthLock)
            {
                if (!_metrics.TryGetValue(metricName, out var metric))
                {
                    metric = new HealthMetric { Name = metricName };
                    _metrics[metricName] = metric;
                }

                metric.Value = value;
                metric.Timestamp = DateTime.UtcNow;
                metric.Metadata = metadata;

                // Notify metrics update
                OnMetricsUpdated(new TimerHealthMetricsEventArgs(metricName, metric));
            }
        }

        /// <summary>
        /// Gets a specific health metric
        /// </summary>
        /// <param name="metricName">The metric name</param>
        /// <returns>The health metric or null if not found</returns>
        public HealthMetric GetMetric(string metricName)
        {
            lock (_healthLock)
            {
                return _metrics.TryGetValue(metricName, out var metric) ? metric : null;
            }
        }

        #region Private Methods

        /// <summary>
        /// Handles the health check timer elapsed event
        /// </summary>
        private async void OnHealthCheckTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_cancellationTokenSource?.IsCancellationRequested == true)
                return;

            try
            {
                await PerformHealthCheckAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled health check");
            }
        }

        /// <summary>
        /// Performs a comprehensive health check
        /// </summary>
        private async Task<TimerHealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new TimerHealthCheckResult
            {
                CheckId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Status = TimerHealthStatus.Healthy,
                Issues = new List<string>(),
                Metrics = new Dictionary<string, object>(),
                Recommendations = new List<string>()
            };

            try
            {
                // Check timer service health
                await CheckTimerServiceHealthAsync(result, cancellationToken);

                // Check circuit breaker health
                await CheckCircuitBreakerHealthAsync(result, cancellationToken);

                // Check resource utilization
                CheckResourceUtilization(result);

                // Check performance metrics
                CheckPerformanceMetrics(result);

                // Check system dependencies
                await CheckSystemDependenciesAsync(result, cancellationToken);

                // Calculate overall health
                CalculateOverallHealth(result);

                // Update metrics
                UpdateHealthMetrics(result);

                // Determine status
                var newStatus = DetermineHealthStatus(result);
                if (newStatus != _currentStatus)
                {
                    ChangeStatus(newStatus, $"Health check completed with score {result.HealthScore}");
                }

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                // Store in history
                lock (_healthLock)
                {
                    _healthHistory.Enqueue(result);
                    while (_healthHistory.Count > 100) // Keep last 100 checks
                    {
                        _healthHistory.Dequeue();
                    }
                    _lastHealthCheck = DateTime.UtcNow;
                }

                // Notify completion
                OnHealthCheckCompleted(result);

                _logger.LogDebug("Health check completed in {Duration}ms with score {Score}",
                    result.Duration.TotalMilliseconds, result.HealthScore);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.Status = TimerHealthStatus.Unhealthy;
                result.HealthScore = 0;
                result.Issues.Add($"Health check failed: {ex.Message}");

                _logger.LogError(ex, "Health check failed after {Duration}ms", result.Duration.TotalMilliseconds);

                // Check failure count
                lock (_healthLock)
                {
                    _consecutiveHealthFailures++;
                    if (_consecutiveHealthFailures > 3)
                    {
                        ChangeStatus(TimerHealthStatus.Critical, $"Multiple consecutive health check failures: {_consecutiveHealthFailures}");
                    }
                }

                OnHealthCheckCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// Checks the timer service health
        /// </summary>
        private async Task CheckTimerServiceHealthAsync(TimerHealthCheckResult result, CancellationToken cancellationToken)
        {
            try
            {
                // Check if timer service is accessible
                var metrics = _timerService.GetMetrics();

                result.Metrics["TimerState"] = _timerService.State.ToString();
                result.Metrics["TimerIsRunning"] = _timerService.IsRunning;
                result.Metrics["TimerIsExecuting"] = _timerService.IsExecuting;
                result.Metrics["TimerCurrentInterval"] = _timerService.CurrentInterval;
                result.Metrics["TimerTotalExecutions"] = metrics.TotalExecutions;
                result.Metrics["TimerSuccessfulExecutions"] = metrics.SuccessfulExecutions;
                result.Metrics["TimerFailedExecutions"] = metrics.FailedExecutions;
                result.Metrics["TimerConsecutiveErrors"] = metrics.CurrentConsecutiveErrors;

                // Check for potential issues
                if (metrics.CurrentConsecutiveErrors > _options.MaxConsecutiveErrors / 2)
                {
                    result.Issues.Add($"High consecutive error count: {metrics.CurrentConsecutiveErrors}");
                }

                if (metrics.FailedExecutions > metrics.TotalExecutions * 0.1) // More than 10% failure rate
                {
                    result.Issues.Add($"High failure rate: {metrics.FailedExecutions}/{metrics.TotalExecutions}");
                }

                // Update health metrics
                UpdateMetric("Timer.SuccessRate", metrics.TotalExecutions > 0 ?
                    (double)metrics.SuccessfulExecutions / metrics.TotalExecutions : 0);
                UpdateMetric("Timer.ConsecutiveErrors", metrics.CurrentConsecutiveErrors);
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Timer service health check failed: {ex.Message}");
                result.Metrics["TimerServiceError"] = ex.Message;
            }
        }

        /// <summary>
        /// Checks the circuit breaker health
        /// </summary>
        private async Task CheckCircuitBreakerHealthAsync(TimerHealthCheckResult result, CancellationToken cancellationToken)
        {
            try
            {
                var status = await _circuitBreaker.GetStatusAsync();

                result.Metrics["CircuitBreakerState"] = status.State.ToString();
                result.Metrics["CircuitBreakerFailureCount"] = status.FailureCount;
                result.Metrics["CircuitBreakerIsAllowingOperations"] = status.IsAllowingOperations;
                result.Metrics["CircuitBreakerHealthScore"] = status.HealthScore;

                // Check for potential issues
                if (status.IsInFailureState)
                {
                    result.Issues.Add($"Circuit breaker is open with {status.FailureCount} failures");
                    result.Recommendations.Add("Monitor circuit breaker reset attempts");
                }

                if (status.FailureCount > _options.MaxConsecutiveErrors * 0.7)
                {
                    result.Issues.Add($"Circuit breaker approaching threshold: {status.FailureCount}/{_options.MaxConsecutiveErrors}");
                }

                // Update health metrics
                UpdateMetric("CircuitBreaker.State", status.State.ToString());
                UpdateMetric("CircuitBreaker.FailureCount", status.FailureCount);
                UpdateMetric("CircuitBreaker.HealthScore", status.HealthScore);
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Circuit breaker health check failed: {ex.Message}");
                result.Metrics["CircuitBreakerError"] = ex.Message;
            }
        }

        /// <summary>
        /// Checks resource utilization
        /// </summary>
        private void CheckResourceUtilization(TimerHealthCheckResult result)
        {
            try
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var memoryUsage = currentProcess.WorkingSet64;
                var cpuUsage = currentProcess.TotalProcessorTime;
                var threadCount = currentProcess.Threads.Count;

                result.Metrics["MemoryUsageMB"] = memoryUsage / (1024 * 1024);
                result.Metrics["ThreadCount"] = threadCount;
                result.Metrics["CpuTime"] = cpuUsage.TotalMilliseconds;

                // Check for potential issues
                if (memoryUsage > 500 * 1024 * 1024) // More than 500MB
                {
                    result.Issues.Add($"High memory usage: {memoryUsage / (1024 * 1024):F1}MB");
                }

                if (threadCount > 50)
                {
                    result.Issues.Add($"High thread count: {threadCount}");
                }

                // Update health metrics
                UpdateMetric("System.MemoryUsageMB", memoryUsage / (1024 * 1024));
                UpdateMetric("System.ThreadCount", threadCount);
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Resource utilization check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks performance metrics
        /// </summary>
        private void CheckPerformanceMetrics(TimerHealthCheckResult result)
        {
            try
            {
                var timerMetrics = _timerService.GetMetrics();

                if (timerMetrics.TotalExecutions > 0)
                {
                    var avgDuration = timerMetrics.AverageExecutionDuration.TotalMilliseconds;
                    var maxDuration = timerMetrics.MaxExecutionDuration.TotalMilliseconds;

                    result.Metrics["AverageExecutionDurationMs"] = avgDuration;
                    result.Metrics["MaxExecutionDurationMs"] = maxDuration;

                    // Check for potential issues
                    if (avgDuration > 5000) // More than 5 seconds average
                    {
                        result.Issues.Add($"High average execution duration: {avgDuration:F1}ms");
                    }

                    if (maxDuration > _options.ExecutionTimeout.TotalMilliseconds * 0.8)
                    {
                        result.Issues.Add($"Execution duration approaching timeout: {maxDuration:F1}ms");
                    }

                    // Update health metrics
                    UpdateMetric("Performance.AverageExecutionMs", avgDuration);
                    UpdateMetric("Performance.MaxExecutionMs", maxDuration);
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Performance metrics check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks system dependencies
        /// </summary>
        private async Task CheckSystemDependenciesAsync(TimerHealthCheckResult result, CancellationToken cancellationToken)
        {
            // This would check dependencies like license server connectivity, database connections, etc.
            // For now, we'll simulate the check
            try
            {
                // Simulate dependency check
                await Task.Delay(100, cancellationToken);

                result.Metrics["DependenciesAvailable"] = true;
                result.Metrics["DependencyCheckTime"] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                result.Issues.Add($"System dependency check failed: {ex.Message}");
                result.Metrics["DependenciesAvailable"] = false;
            }
        }

        /// <summary>
        /// Calculates the overall health score
        /// </summary>
        private void CalculateOverallHealth(TimerHealthCheckResult result)
        {
            var score = 100;

            // Deduct points for issues
            score -= result.Issues.Count * 10;

            // Deduct points for high consecutive errors
            if (result.Metrics.TryGetValue("TimerConsecutiveErrors", out var errorCountObj) &&
                errorCountObj is int errorCount)
            {
                score -= Math.Min(30, errorCount * 5);
            }

            // Deduct points for circuit breaker issues
            if (result.Metrics.TryGetValue("CircuitBreakerState", out var cbStateObj) &&
                cbStateObj is string cbState)
            {
                if (cbState == "Open")
                    score -= 50;
                else if (cbState == "HalfOpen")
                    score -= 25;
            }

            // Deduct points for high memory usage
            if (result.Metrics.TryGetValue("MemoryUsageMB", out var memoryObj) &&
                memoryObj is double memory)
            {
                if (memory > 500)
                    score -= Math.Min(20, (int)((memory - 500) / 50));
            }

            result.HealthScore = Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// Determines the health status based on the health check result
        /// </summary>
        private TimerHealthStatus DetermineHealthStatus(TimerHealthCheckResult result)
        {
            if (result.HealthScore >= 90)
                return TimerHealthStatus.Healthy;

            if (result.HealthScore >= 70)
                return TimerHealthStatus.Degraded;

            if (result.HealthScore >= 50)
                return TimerHealthStatus.Unhealthy;

            return TimerHealthStatus.Critical;
        }

        /// <summary>
        /// Updates health metrics
        /// </summary>
        private void UpdateHealthMetrics(TimerHealthCheckResult result)
        {
            UpdateMetric("Health.Score", result.HealthScore);
            UpdateMetric("Health.Status", result.Status.ToString());
            UpdateMetric("Health.IssueCount", result.Issues.Count);
            UpdateMetric("Health.LastCheck", result.Timestamp);
        }

        /// <summary>
        /// Changes the health status
        /// </summary>
        private void ChangeStatus(TimerHealthStatus newStatus, string reason)
        {
            if (_currentStatus == newStatus)
                return;

            var previousStatus = _currentStatus;
            _currentStatus = newStatus;
            _lastStatusChange = DateTime.UtcNow;
            _consecutiveHealthFailures = newStatus == TimerHealthStatus.Healthy ? 0 : _consecutiveHealthFailures;

            _logger.LogInformation("Health status changed from {Previous} to {Current}: {Reason}",
                previousStatus, newStatus, reason);

            // Notify status change
            OnStatusChanged(new TimerHealthStatusChangedEventArgs(previousStatus, newStatus, reason));
        }

        /// <summary>
        /// Initializes default metrics
        /// </summary>
        private void InitializeMetrics()
        {
            UpdateMetric("Health.MonitorStarted", DateTime.UtcNow);
            UpdateMetric("Health.Version", "1.0.0");
        }

        /// <summary>
        /// Calculates the current health score
        /// </summary>
        private int CalculateHealthScore()
        {
            // Simple calculation based on current status
            switch (_currentStatus)
            {
                case TimerHealthStatus.Healthy:
                    return 100;
                case TimerHealthStatus.Degraded:
                    return 75;
                case TimerHealthStatus.Unhealthy:
                    return 50;
                case TimerHealthStatus.Critical:
                    return 25;
                case TimerHealthStatus.Stopped:
                    return 0;
                default:
                    return 50;
            }
        }

        /// <summary>
        /// Notifies listeners of health check completion
        /// </summary>
        private void OnHealthCheckCompleted(TimerHealthCheckResult result)
        {
            var handler = HealthCheckCompleted;
            if (handler != null)
            {
                var args = new TimerHealthEventArgs
                {
                    HealthCheckId = result.CheckId,
                    Timestamp = result.Timestamp,
                    TimerState = _timerService.State,
                    IsHealthy = result.Status == TimerHealthStatus.Healthy,
                    HealthScore = result.HealthScore,
                    Issues = result.Issues,
                    Metrics = result.Metrics,
                    Recommendations = result.Recommendations
                };

                handler.Invoke(this, args);
            }

            // Notify about health issues
            if (result.Issues.Count > 0)
            {
                OnHealthIssueDetected(new TimerHealthIssueEventArgs(result.CheckId, result.Issues, result.Timestamp));
            }
        }

        /// <summary>
        /// Notifies listeners of status changes
        /// </summary>
        private void OnStatusChanged(TimerHealthStatusChangedEventArgs args)
        {
            StatusChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Notifies listeners of health issues
        /// </summary>
        private void OnHealthIssueDetected(TimerHealthIssueEventArgs args)
        {
            HealthIssueDetected?.Invoke(this, args);
        }

        /// <summary>
        /// Notifies listeners of metrics updates
        /// </summary>
        private void OnMetricsUpdated(TimerHealthMetricsEventArgs args)
        {
            MetricsUpdated?.Invoke(this, args);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the health monitor
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Stop();
                    _healthCheckTimer?.Dispose();
                    _cancellationTokenSource?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerHealthMonitor()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// Represents health status levels
    /// </summary>
    public enum TimerHealthStatus
    {
        /// <summary>
        /// System is healthy
        /// </summary>
        Healthy,

        /// <summary>
        /// System is degraded but functional
        /// </summary>
        Degraded,

        /// <summary>
        /// System is unhealthy and may have issues
        /// </summary>
        Unhealthy,

        /// <summary>
        /// System is in critical condition
        /// </summary>
        Critical,

        /// <summary>
        /// System is stopped
        /// </summary>
        Stopped
    }

    /// <summary>
    /// Represents a health metric
    /// </summary>
    public class HealthMetric
    {
        /// <summary>
        /// Gets or sets the metric name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the metric value
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the metric was updated
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public object Metadata { get; set; }
    }

    /// <summary>
    /// Represents the result of a health check
    /// </summary>
    public class TimerHealthCheckResult
    {
        /// <summary>
        /// Gets or sets the health check ID
        /// </summary>
        public Guid CheckId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the health status
        /// </summary>
        public TimerHealthStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the health score (0-100)
        /// </summary>
        public int HealthScore { get; set; }

        /// <summary>
        /// Gets or sets the duration of the health check
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the detected issues
        /// </summary>
        public List<string> Issues { get; set; }

        /// <summary>
        /// Gets or sets the metrics collected
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; }

        /// <summary>
        /// Gets or sets the recommendations
        /// </summary>
        public List<string> Recommendations { get; set; }
    }

    /// <summary>
    /// Represents a health status report
    /// </summary>
    public class TimerHealthStatusReport
    {
        /// <summary>
        /// Gets or sets the current status
        /// </summary>
        public TimerHealthStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the health score
        /// </summary>
        public int HealthScore { get; set; }

        /// <summary>
        /// Gets or sets the last health check time
        /// </summary>
        public DateTime LastHealthCheck { get; set; }

        /// <summary>
        /// Gets or sets the last status change time
        /// </summary>
        public DateTime LastStatusChange { get; set; }

        /// <summary>
        /// Gets or sets the consecutive failure count
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// Gets or sets the current metrics
        /// </summary>
        public Dictionary<string, HealthMetric> Metrics { get; set; }

        /// <summary>
        /// Gets or sets the recent health check results
        /// </summary>
        public List<TimerHealthCheckResult> RecentHealthChecks { get; set; }
    }

    /// <summary>
    /// Event arguments for health status changes
    /// </summary>
    public class TimerHealthStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the previous status
        /// </summary>
        public TimerHealthStatus PreviousStatus { get; set; }

        /// <summary>
        /// Gets or sets the new status
        /// </summary>
        public TimerHealthStatus NewStatus { get; set; }

        /// <summary>
        /// Gets or sets the reason for the change
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the change
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerHealthStatusChangedEventArgs class
        /// </summary>
        public TimerHealthStatusChangedEventArgs(TimerHealthStatus previousStatus, TimerHealthStatus newStatus, string reason)
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for health issue detection
    /// </summary>
    public class TimerHealthIssueEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the health check ID
        /// </summary>
        public Guid HealthCheckId { get; set; }

        /// <summary>
        /// Gets or sets the detected issues
        /// </summary>
        public List<string> Issues { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when issues were detected
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerHealthIssueEventArgs class
        /// </summary>
        public TimerHealthIssueEventArgs(Guid healthCheckId, List<string> issues, DateTime timestamp)
        {
            HealthCheckId = healthCheckId;
            Issues = issues ?? throw new ArgumentNullException(nameof(issues));
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Event arguments for metrics updates
    /// </summary>
    public class TimerHealthMetricsEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the metric name
        /// </summary>
        public string MetricName { get; set; }

        /// <summary>
        /// Gets or sets the metric value
        /// </summary>
        public HealthMetric Metric { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerHealthMetricsEventArgs class
        /// </summary>
        public TimerHealthMetricsEventArgs(string metricName, HealthMetric metric)
        {
            MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
            Metric = metric ?? throw new ArgumentNullException(nameof(metric));
        }
    }
}