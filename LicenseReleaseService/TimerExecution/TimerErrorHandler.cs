using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Central error handling coordinator for timer execution operations
    /// </summary>
    public class TimerErrorHandler : IDisposable
    {
        private readonly ILogger<TimerErrorHandler> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly ITimerExecutionService _timerService;
        private readonly TimerErrorClassifier _errorClassifier;
        private readonly TimerCircuitBreaker _circuitBreaker;
        private readonly TimerHealthMonitor _healthMonitor;
        private readonly TimerRecoveryManager _recoveryManager;
        private readonly SemaphoreSlim _handlingLock;
        private readonly object _statsLock;
        private readonly Queue<TimerErrorRecord> _errorHistory;
        private readonly Dictionary<TimerErrorCategory, int> _errorCounts;
        private readonly Dictionary<TimerErrorSeverity, int> _severityCounts;

        private bool _isDisposed;
        private bool _isHandlingErrors;
        private int _totalErrorsHandled;
        private int _successfulRecoveries;
        private int _failedRecoveries;
        private DateTime _lastErrorTime;
        private CancellationTokenSource _cancellationTokenSource;

        #region Events

        /// <summary>
        /// Occurs when an error is handled
        /// </summary>
        public event EventHandler<TimerErrorEventArgs> ErrorHandled;

        /// <summary>
        /// Occurs when an error recovery is initiated
        /// </summary>
        public event EventHandler<TimerRecoveryEventArgs> RecoveryInitiated;

        /// <summary>
        /// Occurs when error handling statistics are updated
        /// </summary>
        public event EventHandler<TimerErrorStatisticsEventArgs> StatisticsUpdated;

        /// <summary>
        /// Occurs when the error handler's state changes
        /// </summary>
        public event EventHandler<TimerErrorHandlerStateEventArgs> StateChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether the error handler is currently handling errors
        /// </summary>
        public bool IsHandlingErrors => _isHandlingErrors;

        /// <summary>
        /// Gets the total number of errors handled
        /// </summary>
        public int TotalErrorsHandled => _totalErrorsHandled;

        /// <summary>
        /// Gets the number of successful recoveries
        /// </summary>
        public int SuccessfulRecoveries => _successfulRecoveries;

        /// <summary>
        /// Gets the number of failed recoveries
        /// </summary>
        public int FailedRecoveries => _failedRecoveries;

        /// <summary>
        /// Gets the last error timestamp
        /// </summary>
        public DateTime LastErrorTime => _lastErrorTime;

        /// <summary>
        /// Gets the error history
        /// </summary>
        public IReadOnlyList<TimerErrorRecord> ErrorHistory => _errorHistory.ToList();

        /// <summary>
        /// Gets the current error counts by category
        /// </summary>
        public IReadOnlyDictionary<TimerErrorCategory, int> ErrorCounts => new Dictionary<TimerErrorCategory, int>(_errorCounts);

        /// <summary>
        /// Gets the current error counts by severity
        /// </summary>
        public IReadOnlyDictionary<TimerErrorSeverity, int> SeverityCounts => new Dictionary<TimerErrorSeverity, int>(_severityCounts);

        /// <summary>
        /// Gets the current state of the error handler
        /// </summary>
        public TimerErrorHandlerState State => GetHandlerState();

        #endregion

        /// <summary>
        /// Initializes a new instance of the TimerErrorHandler class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="options">The timer execution options</param>
        /// <param name="timerService">The timer execution service</param>
        /// <param name="errorClassifier">The error classifier instance</param>
        /// <param name="circuitBreaker">The circuit breaker instance</param>
        /// <param name="healthMonitor">The health monitor instance</param>
        /// <param name="recoveryManager">The recovery manager instance</param>
        public TimerErrorHandler(ILogger<TimerErrorHandler> logger, TimerExecutionOptions options,
            ITimerExecutionService timerService, TimerErrorClassifier errorClassifier,
            TimerCircuitBreaker circuitBreaker, TimerHealthMonitor healthMonitor,
            TimerRecoveryManager recoveryManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
            _errorClassifier = errorClassifier ?? throw new ArgumentNullException(nameof(errorClassifier));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
            _recoveryManager = recoveryManager ?? throw new ArgumentNullException(nameof(recoveryManager));

            _handlingLock = new SemaphoreSlim(1, 1);
            _statsLock = new object();
            _errorHistory = new Queue<TimerErrorRecord>();
            _errorCounts = new Dictionary<TimerErrorCategory, int>();
            _severityCounts = new Dictionary<TimerErrorSeverity, int>();

            // Initialize error counts
            foreach (TimerErrorCategory category in Enum.GetValues(typeof(TimerErrorCategory)))
            {
                _errorCounts[category] = 0;
            }

            foreach (TimerErrorSeverity severity in Enum.GetValues(typeof(TimerErrorSeverity)))
            {
                _severityCounts[severity] = 0;
            }

            // Subscribe to component events
            SubscribeToComponentEvents();
        }

        /// <summary>
        /// Starts the error handler
        /// </summary>
        public void Start()
        {
            lock (_statsLock)
            {
                if (_isHandlingErrors)
                {
                    _logger.LogWarning("Error handler is already running");
                    return;
                }

                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(TimerErrorHandler));

                _cancellationTokenSource = new CancellationTokenSource();
                _isHandlingErrors = true;

                _logger.LogInformation("Timer error handler started");
            }

            // Notify state change
            OnStateChanged(new TimerErrorHandlerStateEventArgs(TimerErrorHandlerState.Stopped, TimerErrorHandlerState.Running, "Error handler started"));
        }

        /// <summary>
        /// Stops the error handler
        /// </summary>
        public void Stop()
        {
            lock (_statsLock)
            {
                if (!_isHandlingErrors)
                    return;

                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                _isHandlingErrors = false;

                _logger.LogInformation("Timer error handler stopped");
            }

            // Notify state change
            OnStateChanged(new TimerErrorHandlerStateEventArgs(TimerErrorHandlerState.Running, TimerErrorHandlerState.Stopped, "Error handler stopped"));
        }

        /// <summary>
        /// Handles a timer execution error
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="executionId">The execution ID</param>
        /// <param name="context">Additional context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The error handling result</returns>
        public async Task<TimerErrorHandlingResult> HandleErrorAsync(Exception exception, Guid? executionId = null, object context = null, CancellationToken cancellationToken = default)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            if (!_isHandlingErrors)
            {
                _logger.LogWarning("Error handler is not running, cannot handle error");
                return new TimerErrorHandlingResult
                {
                    Success = false,
                    ErrorMessage = "Error handler is not running"
                };
            }

            await _handlingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var handlingId = Guid.NewGuid();
                var result = new TimerErrorHandlingResult
                {
                    HandlingId = handlingId,
                    Timestamp = DateTime.UtcNow,
                    Exception = exception,
                    ExecutionId = executionId,
                    Context = context
                };

                _logger.LogError(exception, "Handling timer error {HandlingId} for execution {ExecutionId}",
                    handlingId, executionId);

                // Classify the error
                var classification = _errorClassifier.ClassifyError(exception, context);
                result.Classification = classification;

                // Create error event args
                var timerStatus = MapTimerStateToStatus(_timerService.State);
                var errorEventArgs = new TimerErrorEventArgs(exception, executionId, timerStatus, 0)
                {
                    Severity = classification.Severity,
                    Category = classification.Category,
                    RecommendedAction = classification.RecoveryAction,
                    Timestamp = DateTime.UtcNow
                };

                // Add context to error event
                errorEventArgs.AddContext("Classification", classification);
                errorEventArgs.AddContext("HandlingId", handlingId);
                if (context != null)
                {
                    errorEventArgs.AddContext("Context", context);
                }

                // Update error statistics
                UpdateErrorStatistics(classification);

                // Record the error
                RecordError(handlingId, exception, classification, executionId, context);

                // Determine if automatic recovery should be attempted
                var shouldAttemptRecovery = ShouldAttemptRecovery(classification, errorEventArgs);

                if (shouldAttemptRecovery)
                {
                    // Attempt recovery
                    var recoveryResult = await AttemptRecoveryAsync(errorEventArgs, classification, context, cancellationToken).ConfigureAwait(false);
                    result.RecoveryResult = recoveryResult;
                    result.Success = recoveryResult.Success;
                    result.ErrorMessage = recoveryResult.Success ? null : recoveryResult.ErrorMessage;
                }
                else
                {
                    // Handle error without recovery
                    result.Success = await HandleErrorWithoutRecoveryAsync(errorEventArgs, classification, cancellationToken).ConfigureAwait(false);
                    result.ErrorMessage = result.Success ? null : "Error handled but recovery was not attempted";
                }

                // Update the error event
                errorEventArgs.WasHandled = result.Success;
                errorEventArgs.RecoveryAttempts = result.RecoveryResult?.Actions?.Count ?? 0;
                errorEventArgs.RecoverySuccessful = result.RecoveryResult?.Success ?? false;
                errorEventArgs.HandlingResult = result.Success ? "Successfully handled" : result.ErrorMessage;

                // Handle critical errors
                if (classification.Severity == TimerErrorSeverity.Critical)
                {
                    await HandleCriticalErrorAsync(errorEventArgs, classification, cancellationToken).ConfigureAwait(false);
                }

                // Notify error handled
                OnErrorHandled(errorEventArgs);

                // Update health metrics
                UpdateHealthMetrics(classification, result);

                _logger.LogInformation("Error handling completed for {HandlingId}: {Success}",
                    handlingId, result.Success);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during error handling for execution {ExecutionId}", executionId);

                return new TimerErrorHandlingResult
                {
                    Success = false,
                    ErrorMessage = $"Error handling failed: {ex.Message}",
                    Exception = exception
                };
            }
            finally
            {
                _handlingLock.Release();
            }
        }

        /// <summary>
        /// Handles multiple errors that occurred during the same execution
        /// </summary>
        /// <param name="exceptions">The list of exceptions</param>
        /// <param name="executionId">The execution ID</param>
        /// <param name="context">Additional context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of error handling results</returns>
        public async Task<List<TimerErrorHandlingResult>> HandleErrorsAsync(IEnumerable<Exception> exceptions, Guid? executionId = null, object context = null, CancellationToken cancellationToken = default)
        {
            if (exceptions == null)
                throw new ArgumentNullException(nameof(exceptions));

            var results = new List<TimerErrorHandlingResult>();

            foreach (var exception in exceptions)
            {
                var result = await HandleErrorAsync(exception, executionId, context, cancellationToken).ConfigureAwait(false);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Gets error handling statistics
        /// </summary>
        /// <returns>Error handling statistics</returns>
        public TimerErrorHandlingStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                var stats = new TimerErrorHandlingStatistics
                {
                    TotalErrorsHandled = _totalErrorsHandled,
                    SuccessfulRecoveries = _successfulRecoveries,
                    FailedRecoveries = _failedRecoveries,
                    LastErrorTime = _lastErrorTime,
                    RecoverySuccessRate = _totalErrorsHandled > 0 ? (double)_successfulRecoveries / _totalErrorsHandled : 0,
                    ErrorCounts = new Dictionary<TimerErrorCategory, int>(_errorCounts),
                    SeverityCounts = new Dictionary<TimerErrorSeverity, int>(_severityCounts),
                    ErrorHandlerState = State
                };

                // Add recent error rates
                var recentErrors = _errorHistory.Where(e => DateTime.UtcNow - e.Timestamp <= TimeSpan.FromHours(1)).ToList();
                stats.RecentErrorRate = recentErrors.Count > 0 ? (double)recentErrors.Count / 60 : 0; // Errors per minute

                // Add recovery statistics
                var recoveryStats = _recoveryManager.GetStatistics();
                stats.RecoveryStatistics = recoveryStats;

                return stats;
            }
        }

        /// <summary>
        /// Gets error history for a specific time period
        /// </summary>
        /// <param name="since">The start time for the history</param>
        /// <returns>List of error records</returns>
        public List<TimerErrorRecord> GetErrorHistory(DateTime since)
        {
            lock (_statsLock)
            {
                return _errorHistory.Where(e => e.Timestamp >= since).ToList();
            }
        }

        /// <summary>
        /// Clears error history
        /// </summary>
        public void ClearHistory()
        {
            lock (_statsLock)
            {
                _errorHistory.Clear();
            }
        }

        /// <summary>
        /// Resets error handling statistics
        /// </summary>
        public void ResetStatistics()
        {
            lock (_statsLock)
            {
                _totalErrorsHandled = 0;
                _successfulRecoveries = 0;
                _failedRecoveries = 0;
                _lastErrorTime = DateTime.MinValue;

                foreach (var key in _errorCounts.Keys.ToList())
                {
                    _errorCounts[key] = 0;
                }

                foreach (var kvp in _severityCounts.ToList())
                {
                    _severityCounts[kvp.Key] = 0;
                }

                _recoveryManager.ClearHistory();

                _logger.LogInformation("Error handling statistics reset");
            }

            // Notify statistics update
            OnStatisticsUpdated(new TimerErrorStatisticsEventArgs(GetStatistics()));
        }

        /// <summary>
        /// Forces a health check of the error handling system
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<TimerErrorHandlerHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var result = new TimerErrorHandlerHealthResult
            {
                CheckId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                IsHealthy = true,
                Issues = new List<string>(),
                Metrics = new Dictionary<string, object>()
            };

            try
            {
                // Check if error handler is running
                result.Metrics["IsRunning"] = _isHandlingErrors;
                if (!_isHandlingErrors)
                {
                    result.Issues.Add("Error handler is not running");
                    result.IsHealthy = false;
                }

                // Check error statistics
                var stats = GetStatistics();
                result.Metrics["TotalErrorsHandled"] = stats.TotalErrorsHandled;
                result.Metrics["RecoverySuccessRate"] = stats.RecoverySuccessRate;
                result.Metrics["RecentErrorRate"] = stats.RecentErrorRate;

                // Check for high error rates
                if (stats.RecentErrorRate > 1) // More than 1 error per minute
                {
                    result.Issues.Add($"High error rate: {stats.RecentErrorRate:F2} errors per minute");
                }

                // Check recovery success rate
                if (stats.TotalErrorsHandled > 10 && stats.RecoverySuccessRate < 0.5) // Less than 50% success rate
                {
                    result.Issues.Add($"Low recovery success rate: {stats.RecoverySuccessRate:P2}");
                }

                // Check component health
                var healthStatus = _healthMonitor.GetHealthStatus();
                result.Metrics["HealthScore"] = healthStatus.HealthScore;
                result.Metrics["HealthStatus"] = healthStatus.Status.ToString();

                if (healthStatus.HealthScore < 70)
                {
                    result.Issues.Add($"Low health score: {healthStatus.HealthScore}");
                }

                // Calculate overall health score
                result.HealthScore = CalculateHealthScore(result);
                result.IsHealthy = result.HealthScore >= 70 && result.Issues.Count == 0;

                return result;
            }
            catch (Exception ex)
            {
                result.IsHealthy = false;
                result.HealthScore = 0;
                result.Issues.Add($"Health check failed: {ex.Message}");

                _logger.LogError(ex, "Error during error handler health check");

                return result;
            }
        }

        #region Private Methods

        /// <summary>
        /// Determines if recovery should be attempted for the given error
        /// </summary>
        private bool ShouldAttemptRecovery(TimerErrorClassification classification, TimerErrorEventArgs errorEventArgs)
        {
            // Don't attempt recovery if error handler is not running
            if (!_isHandlingErrors)
                return false;

            // Don't attempt recovery for critical configuration errors
            if (classification.Category == TimerErrorCategory.Configuration && classification.Severity == TimerErrorSeverity.Critical)
                return false;

            // Don't attempt recovery if circuit breaker is open and this is a critical error
            var circuitBreakerStatus = _circuitBreaker.GetStatusAsync().Result;
            if (circuitBreakerStatus.IsInFailureState && classification.Severity >= TimerErrorSeverity.High)
                return false;

            // Check if the error allows recovery
            return classification.IsRecoverable && classification.AllowsRetry;
        }

        /// <summary>
        /// Attempts to recover from an error
        /// </summary>
        private async Task<TimerRecoveryResult> AttemptRecoveryAsync(TimerErrorEventArgs errorEventArgs, TimerErrorClassification classification, object context, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Attempting recovery for error {ErrorId}: {Category}/{Severity}",
                    errorEventArgs.ErrorId, classification.Category, classification.Severity);

                // Notify recovery initiated
                var recoveryEventArgs = new TimerRecoveryEventArgs(errorEventArgs, classification.RecoveryAction);
                OnRecoveryInitiated(recoveryEventArgs);

                // Attempt recovery through recovery manager
                var recoveryResult = await _recoveryManager.AutoRecoverAsync(errorEventArgs, context, cancellationToken).ConfigureAwait(false);

                // Update statistics
                lock (_statsLock)
                {
                    if (recoveryResult.Success)
                    {
                        Interlocked.Increment(ref _successfulRecoveries);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedRecoveries);
                    }
                }

                // Update circuit breaker based on recovery result
                if (recoveryResult.Success)
                {
                    await _circuitBreaker.RecordSuccessAsync(errorEventArgs.ExecutionId).ConfigureAwait(false);
                }
                else
                {
                    await _circuitBreaker.RecordFailureAsync(errorEventArgs.Exception, errorEventArgs.ExecutionId).ConfigureAwait(false);
                }

                _logger.LogInformation("Recovery {Success} for error {ErrorId}",
                    recoveryResult.Success ? "succeeded" : "failed", errorEventArgs.ErrorId);

                return recoveryResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery attempt failed for error {ErrorId}", errorEventArgs.ErrorId);

                lock (_statsLock)
                {
                    Interlocked.Increment(ref _failedRecoveries);
                }

                // Record failure with circuit breaker
                await _circuitBreaker.RecordFailureAsync(errorEventArgs.Exception, errorEventArgs.ExecutionId).ConfigureAwait(false);

                return new TimerRecoveryResult
                {
                    Success = false,
                    ErrorMessage = $"Recovery failed: {ex.Message}",
                    ErrorEvent = errorEventArgs
                };
            }
        }

        /// <summary>
        /// Handles an error without attempting recovery
        /// </summary>
        private async Task<bool> HandleErrorWithoutRecoveryAsync(TimerErrorEventArgs errorEventArgs, TimerErrorClassification classification, CancellationToken cancellationToken)
        {
            try
            {
                // Log the error
                _logger.LogWarning("Handling error without recovery: {Category}/{Severity}",
                    classification.Category, classification.Severity);

                // Record the error with circuit breaker
                await _circuitBreaker.RecordFailureAsync(errorEventArgs.Exception, errorEventArgs.ExecutionId).ConfigureAwait(false);

                // Add context to health monitor
                _healthMonitor.UpdateMetric("Error.NoRecoveryReason", "Automatic recovery disabled for this error type");
                _healthMonitor.UpdateMetric("Error.LastNoRecoveryError", classification.Category.ToString());

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle error without recovery");
                return false;
            }
        }

        /// <summary>
        /// Handles critical errors
        /// </summary>
        private async Task HandleCriticalErrorAsync(TimerErrorEventArgs errorEventArgs, TimerErrorClassification classification, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogCritical("Critical error detected: {Category}/{Severity} - {Message}",
                    classification.Category, classification.Severity, errorEventArgs.Exception.Message);

                // Force circuit breaker open for critical errors
                await _circuitBreaker.ForceOpenAsync($"Critical error: {classification.Category}", errorEventArgs.ExecutionId).ConfigureAwait(false);

                // Stop the timer service if necessary
                if (classification.Category == TimerErrorCategory.Resource || classification.Category == TimerErrorCategory.Configuration)
                {
                    try
                    {
                        if (_timerService.IsRunning)
                        {
                            _logger.LogWarning("Stopping timer service due to critical error");
                            _timerService.Stop();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to stop timer service during critical error handling");
                    }
                }

                // Update health metrics
                _healthMonitor.UpdateMetric("Error.CriticalErrorCount", 1);
                _healthMonitor.UpdateMetric("Error.LastCriticalError", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle critical error");
            }
        }

        /// <summary>
        /// Updates error statistics
        /// </summary>
        private void UpdateErrorStatistics(TimerErrorClassification classification)
        {
            lock (_statsLock)
            {
                Interlocked.Increment(ref _totalErrorsHandled);
                _lastErrorTime = DateTime.UtcNow;

                _errorCounts[classification.Category]++;
                _severityCounts[classification.Severity]++;
            }

            // Notify statistics update
            OnStatisticsUpdated(new TimerErrorStatisticsEventArgs(GetStatistics()));
        }

        /// <summary>
        /// Records an error in the history
        /// </summary>
        private void RecordError(Guid handlingId, Exception exception, TimerErrorClassification classification, Guid? executionId, object context)
        {
            var record = new TimerErrorRecord
            {
                HandlingId = handlingId,
                Timestamp = DateTime.UtcNow,
                Exception = exception,
                Classification = classification,
                ExecutionId = executionId,
                Context = context
            };

            lock (_statsLock)
            {
                _errorHistory.Enqueue(record);
                while (_errorHistory.Count > 1000) // Keep last 1000 errors
                {
                    _errorHistory.Dequeue();
                }
            }
        }

        /// <summary>
        /// Updates health metrics
        /// </summary>
        private void UpdateHealthMetrics(TimerErrorClassification classification, TimerErrorHandlingResult result)
        {
            try
            {
                _healthMonitor.UpdateMetric("Error.TotalHandled", _totalErrorsHandled);
                _healthMonitor.UpdateMetric("Error.RecoverySuccessRate", _totalErrorsHandled > 0 ? (double)_successfulRecoveries / _totalErrorsHandled : 0);
                _healthMonitor.UpdateMetric("Error.LastCategory", classification.Category.ToString());
                _healthMonitor.UpdateMetric("Error.LastSeverity", classification.Severity.ToString());
                _healthMonitor.UpdateMetric("Error.LastHandled", DateTime.UtcNow);

                if (result.RecoveryResult != null)
                {
                    _healthMonitor.UpdateMetric("Recovery.LastDuration", result.RecoveryResult.Duration);
                    _healthMonitor.UpdateMetric("Recovery.LastActionCount", result.RecoveryResult.Actions?.Count ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update health metrics");
            }
        }

        /// <summary>
        /// Calculates health score for the error handler
        /// </summary>
        private int CalculateHealthScore(TimerErrorHandlerHealthResult healthResult)
        {
            var score = 100;

            // Deduct points for issues
            score -= healthResult.Issues.Count * 10;

            // Deduct points for low recovery success rate
            if (healthResult.Metrics.TryGetValue("RecoverySuccessRate", out var recoveryRateObj) &&
                recoveryRateObj is double recoveryRate)
            {
                if (recoveryRate < 0.5)
                    score -= 30;
                else if (recoveryRate < 0.8)
                    score -= 15;
            }

            // Deduct points for high error rates
            if (healthResult.Metrics.TryGetValue("RecentErrorRate", out var errorRateObj) &&
                errorRateObj is double errorRate)
            {
                if (errorRate > 5) // More than 5 errors per minute
                    score -= 25;
                else if (errorRate > 1) // More than 1 error per minute
                    score -= 10;
            }

            // Deduct points for low health score
            if (healthResult.Metrics.TryGetValue("HealthScore", out var healthScoreObj) &&
                healthScoreObj is int healthScore)
            {
                if (healthScore < 50)
                    score -= 20;
                else if (healthScore < 70)
                    score -= 10;
            }

            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>
        /// Gets the current handler state
        /// </summary>
        private TimerErrorHandlerState GetHandlerState()
        {
            if (!_isHandlingErrors)
                return TimerErrorHandlerState.Stopped;

            var stats = GetStatistics();
            if (stats.RecentErrorRate > 5)
                return TimerErrorHandlerState.Degraded;

            if (stats.RecoverySuccessRate < 0.5 && stats.TotalErrorsHandled > 10)
                return TimerErrorHandlerState.Unhealthy;

            return TimerErrorHandlerState.Running;
        }

        /// <summary>
        /// Subscribes to component events
        /// </summary>
        private void SubscribeToComponentEvents()
        {
            // Subscribe to circuit breaker events
            _circuitBreaker.StateChanged += (s, e) =>
            {
                _logger.LogInformation("Circuit breaker state changed: {Old} -> {New}", e.OldState, e.NewState);
            };

            // Subscribe to health monitor events
            _healthMonitor.StatusChanged += (s, e) =>
            {
                _logger.LogInformation("Health monitor status changed: {Old} -> {New}", e.PreviousStatus, e.NewStatus);
            };

            // Subscribe to recovery manager events
            _recoveryManager.RecoveryCompleted += (s, e) =>
            {
                _logger.LogDebug("Recovery completed: {Success} in {Duration}ms",
                    e.Success, e.Duration.TotalMilliseconds);
            };
        }

        /// <summary>
        /// Notifies listeners of error handled events
        /// </summary>
        private void OnErrorHandled(TimerErrorEventArgs args)
        {
            ErrorHandled?.Invoke(this, args);
        }

        /// <summary>
        /// Notifies listeners of recovery initiated events
        /// </summary>
        private void OnRecoveryInitiated(TimerRecoveryEventArgs args)
        {
            RecoveryInitiated?.Invoke(this, args);
        }

        /// <summary>
        /// Notifies listeners of statistics updated events
        /// </summary>
        private void OnStatisticsUpdated(TimerErrorStatisticsEventArgs args)
        {
            StatisticsUpdated?.Invoke(this, args);
        }

        /// <summary>
        /// Notifies listeners of state changed events
        /// </summary>
        private void OnStateChanged(TimerErrorHandlerStateEventArgs args)
        {
            StateChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Maps TimerState to TimerStatus for compatibility
        /// </summary>
        /// <param name="state">The TimerState to map</param>
        /// <returns>Equivalent TimerStatus</returns>
        private static TimerStatus MapTimerStateToStatus(TimerState state)
        {
            return state switch
            {
                TimerState.Stopped => TimerStatus.Stopped,
                TimerState.Running => TimerStatus.Running,
                TimerState.Paused => TimerStatus.Paused,
                TimerState.Executing => TimerStatus.Executing,
                TimerState.Error => TimerStatus.Error,
                TimerState.CircuitBreaker => TimerStatus.CircuitBreaker,
                TimerState.Stopping => TimerStatus.Stopping,
                TimerState.Disposed => TimerStatus.Disposed,
                _ => TimerStatus.Error
            };
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the error handler
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
                    _handlingLock?.Dispose();
                    _cancellationTokenSource?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerErrorHandler()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// Represents the result of error handling
    /// </summary>
    public class TimerErrorHandlingResult
    {
        /// <summary>
        /// Gets or sets the error handling operation ID
        /// </summary>
        public Guid HandlingId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when handling started
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the exception that was handled
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the execution ID
        /// </summary>
        public Guid? ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the error classification
        /// </summary>
        public TimerErrorClassification Classification { get; set; }

        /// <summary>
        /// Gets or sets the recovery result
        /// </summary>
        public TimerRecoveryResult RecoveryResult { get; set; }

        /// <summary>
        /// Gets or sets additional context information
        /// </summary>
        public object Context { get; set; }

        /// <summary>
        /// Gets or sets whether handling was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if handling failed
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Represents an error record in the history
    /// </summary>
    public class TimerErrorRecord
    {
        /// <summary>
        /// Gets or sets the handling operation ID
        /// </summary>
        public Guid HandlingId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the exception
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the error classification
        /// </summary>
        public TimerErrorClassification Classification { get; set; }

        /// <summary>
        /// Gets or sets the execution ID
        /// </summary>
        public Guid? ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets additional context information
        /// </summary>
        public object Context { get; set; }
    }

    /// <summary>
    /// Represents error handling statistics
    /// </summary>
    public class TimerErrorHandlingStatistics
    {
        /// <summary>
        /// Gets or sets the total number of errors handled
        /// </summary>
        public int TotalErrorsHandled { get; set; }

        /// <summary>
        /// Gets or sets the number of successful recoveries
        /// </summary>
        public int SuccessfulRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the number of failed recoveries
        /// </summary>
        public int FailedRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the recovery success rate
        /// </summary>
        public double RecoverySuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the recent error rate (errors per minute)
        /// </summary>
        public double RecentErrorRate { get; set; }

        /// <summary>
        /// Gets or sets the last error timestamp
        /// </summary>
        public DateTime LastErrorTime { get; set; }

        /// <summary>
        /// Gets or sets the error counts by category
        /// </summary>
        public Dictionary<TimerErrorCategory, int> ErrorCounts { get; set; }

        /// <summary>
        /// Gets or sets the error counts by severity
        /// </summary>
        public Dictionary<TimerErrorSeverity, int> SeverityCounts { get; set; }

        /// <summary>
        /// Gets or sets the error handler state
        /// </summary>
        public TimerErrorHandlerState ErrorHandlerState { get; set; }

        /// <summary>
        /// Gets or sets the recovery statistics
        /// </summary>
        public TimerRecoveryStatistics RecoveryStatistics { get; set; }
    }

    /// <summary>
    /// Represents the health check result for the error handler
    /// </summary>
    public class TimerErrorHandlerHealthResult
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
        /// Gets or sets the health score (0-100)
        /// </summary>
        public int HealthScore { get; set; }

        /// <summary>
        /// Gets or sets whether the error handler is healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the detected issues
        /// </summary>
        public List<string> Issues { get; set; }

        /// <summary>
        /// Gets or sets the health metrics
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; }
    }

    /// <summary>
    /// Defines error handler states
    /// </summary>
    public enum TimerErrorHandlerState
    {
        /// <summary>
        /// Error handler is stopped
        /// </summary>
        Stopped,

        /// <summary>
        /// Error handler is running normally
        /// </summary>
        Running,

        /// <summary>
        /// Error handler is degraded but functional
        /// </summary>
        Degraded,

        /// <summary>
        /// Error handler is unhealthy
        /// </summary>
        Unhealthy
    }

    /// <summary>
    /// Event arguments for statistics updates
    /// </summary>
    public class TimerErrorStatisticsEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the current statistics
        /// </summary>
        public TimerErrorHandlingStatistics Statistics { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerErrorStatisticsEventArgs class
        /// </summary>
        public TimerErrorStatisticsEventArgs(TimerErrorHandlingStatistics statistics)
        {
            Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        }
    }

    /// <summary>
    /// Event arguments for state changes
    /// </summary>
    public class TimerErrorHandlerStateEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the previous state
        /// </summary>
        public TimerErrorHandlerState PreviousState { get; set; }

        /// <summary>
        /// Gets or sets the new state
        /// </summary>
        public TimerErrorHandlerState NewState { get; set; }

        /// <summary>
        /// Gets or sets the reason for the change
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the change
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerErrorHandlerStateEventArgs class
        /// </summary>
        public TimerErrorHandlerStateEventArgs(TimerErrorHandlerState previousState, TimerErrorHandlerState newState, string reason)
        {
            PreviousState = previousState;
            NewState = newState;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }
    }
}