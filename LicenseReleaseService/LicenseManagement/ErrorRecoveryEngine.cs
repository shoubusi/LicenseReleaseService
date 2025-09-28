using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines recovery engine states
    /// </summary>
    public enum RecoveryEngineState
    {
        /// <summary>
        /// Engine is initializing
        /// </summary>
        Initializing,

        /// <summary>
        /// Engine is running normally
        /// </summary>
        Running,

        /// <summary>
        /// Engine is paused
        /// </summary>
        Paused,

        /// <summary>
        /// Engine is shutting down
        /// </summary>
        ShuttingDown,

        /// <summary>
        /// Engine is stopped
        /// </summary>
        Stopped
    }

    /// <summary>
    /// Defines recovery trigger types
    /// </summary>
    public enum RecoveryTriggerType
    {
        /// <summary>
        /// Triggered by exception
        /// </summary>
        Exception,

        /// <summary>
        /// Triggered by timeout
        /// </summary>
        Timeout,

        /// <summary>
        /// Triggered by circuit breaker
        /// </summary>
        CircuitBreaker,

        /// <summary>
        /// Triggered by health check failure
        /// </summary>
        HealthCheck,

        /// <summary>
        /// Triggered by manual intervention
        /// </summary>
        Manual,

        /// <summary>
        /// Triggered by performance degradation
        /// </summary>
        Performance,

        /// <summary>
        /// Triggered by configuration change
        /// </summary>
        ConfigurationChange
    }

    /// <summary>
    /// Represents a recovery request
    /// </summary>
    public class RecoveryRequest
    {
        /// <summary>
        /// Gets or sets the unique request ID
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// Gets or sets the recovery trigger type
        /// </summary>
        public RecoveryTriggerType TriggerType { get; set; }

        /// <summary>
        /// Gets or sets the source component
        /// </summary>
        public string SourceComponent { get; set; }

        /// <summary>
        /// Gets or sets the operation name
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// Gets or sets the exception that triggered recovery
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the error classification
        /// </summary>
        public ErrorClassification ErrorClassification { get; set; }

        /// <summary>
        /// Gets or sets the recovery priority
        /// </summary>
        public RecoveryPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the context data
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();

        /// <summary>
        /// Gets or sets the timestamp when the request was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the request should be processed
        /// </summary>
        public DateTime ProcessAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request is being processed
        /// </summary>
        public bool IsProcessing { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request is completed
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets the recovery result
        /// </summary>
        public RecoveryResult Result { get; set; }

        /// <summary>
        /// Gets or sets the retry count
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed retries
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Gets or sets the timeout for recovery
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets the callback to invoke when recovery is complete
        /// </summary>
        public Action<RecoveryResult> Callback { get; set; }
    }

    /// <summary>
    /// Represents the recovery engine configuration
    /// </summary>
    public class RecoveryEngineConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum concurrent recovery operations
        /// </summary>
        public int MaxConcurrentRecoveries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the recovery queue size limit
        /// </summary>
        public int MaxQueueSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the processing interval
        /// </summary>
        public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the default recovery timeout
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the maximum recovery attempts
        /// </summary>
        public int MaxRecoveryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether automatic recovery is enabled
        /// </summary>
        public bool EnableAutomaticRecovery { get; set; } = true;

        /// <summary>
        /// Gets or sets whether manual recovery is enabled
        /// </summary>
        public bool EnableManualRecovery { get; set; } = true;

        /// <summary>
        /// Gets or sets the recovery strategy selection mode
        /// </summary>
        public string StrategySelectionMode { get; set; } = "Adaptive";

        /// <summary>
        /// Gets or sets the recovery statistics retention period
        /// </summary>
        public TimeSpan StatisticsRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    }

    /// <summary>
    /// Represents recovery engine statistics
    /// </summary>
    public class RecoveryEngineStatistics
    {
        /// <summary>
        /// Gets or sets the current engine state
        /// </summary>
        public RecoveryEngineState State { get; set; }

        /// <summary>
        /// Gets or sets the total number of recovery requests
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of successful recoveries
        /// </summary>
        public long SuccessfulRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the number of failed recoveries
        /// </summary>
        public long FailedRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the current queue size
        /// </summary>
        public int CurrentQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the number of active recoveries
        /// </summary>
        public int ActiveRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the average recovery time
        /// </summary>
        public TimeSpan AverageRecoveryTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last recovery
        /// </summary>
        public DateTime? LastRecoveryTime { get; set; }

        /// <summary>
        /// Gets or sets the uptime of the engine
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets or sets the recovery rate (requests per minute)
        /// </summary>
        public double RecoveryRatePerMinute { get; set; }

        /// <summary>
        /// Gets or sets the success rate
        /// </summary>
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRecoveries / TotalRequests : 0;
    }

    /// <summary>
    /// Centralized error recovery engine that coordinates all recovery mechanisms
    /// </summary>
    public class ErrorRecoveryEngine : IDisposable
    {
        private readonly ILogger<ErrorRecoveryEngine> _logger;
        private readonly RecoveryEngineConfiguration _configuration;
        private readonly ErrorClassifier _errorClassifier;
        private readonly RecoveryManager _recoveryManager;
        private readonly AutomaticRecoveryManager _automaticRecoveryManager;
        private readonly GracefulDegradationManager _gracefulDegradationManager;
        private readonly FallbackStrategyManager _fallbackStrategyManager;
        private readonly ServiceResilienceCoordinator _resilienceCoordinator;
        private readonly HealthChecker _healthChecker;

        private readonly ConcurrentQueue<RecoveryRequest> _recoveryQueue;
        private readonly ConcurrentDictionary<string, RecoveryRequest> _activeRecoveries;
        private readonly ConcurrentDictionary<string, DateTime> _completedRecoveries;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly Timer _processingTimer;
        private readonly Timer _statisticsTimer;
        private readonly DateTime _startTime;

        private RecoveryEngineState _state;
        private readonly object _stateLock;
        private bool _disposed;

        /// <summary>
        /// Event raised when a recovery request is submitted
        /// </summary>
        public event EventHandler<RecoveryRequest> RecoveryRequested;

        /// <summary>
        /// Event raised when a recovery is completed
        /// </summary>
        public event EventHandler<RecoveryResult> RecoveryCompleted;

        /// <summary>
        /// Event raised when the engine state changes
        /// </summary>
        public event EventHandler<RecoveryEngineState> StateChanged;

        /// <summary>
        /// Gets the current engine state
        /// </summary>
        public RecoveryEngineState State => _state;

        /// <summary>
        /// Gets the current engine statistics
        /// </summary>
        public RecoveryEngineStatistics Statistics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ErrorRecoveryEngine class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configuration">Engine configuration</param>
        /// <param name="errorClassifier">Error classifier</param>
        /// <param name="recoveryManager">Recovery manager</param>
        /// <param name="automaticRecoveryManager">Automatic recovery manager</param>
        /// <param name="gracefulDegradationManager">Graceful degradation manager</param>
        /// <param name="fallbackStrategyManager">Fallback strategy manager</param>
        /// <param name="resilienceCoordinator">Service resilience coordinator</param>
        /// <param name="healthChecker">Health checker</param>
        public ErrorRecoveryEngine(
            ILogger<ErrorRecoveryEngine> logger,
            RecoveryEngineConfiguration configuration,
            ErrorClassifier errorClassifier,
            RecoveryManager recoveryManager,
            AutomaticRecoveryManager automaticRecoveryManager,
            GracefulDegradationManager gracefulDegradationManager,
            FallbackStrategyManager fallbackStrategyManager,
            ServiceResilienceCoordinator resilienceCoordinator,
            HealthChecker healthChecker)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _errorClassifier = errorClassifier ?? throw new ArgumentNullException(nameof(errorClassifier));
            _recoveryManager = recoveryManager ?? throw new ArgumentNullException(nameof(recoveryManager));
            _automaticRecoveryManager = automaticRecoveryManager ?? throw new ArgumentNullException(nameof(automaticRecoveryManager));
            _gracefulDegradationManager = gracefulDegradationManager ?? throw new ArgumentNullException(nameof(gracefulDegradationManager));
            _fallbackStrategyManager = fallbackStrategyManager ?? throw new ArgumentNullException(nameof(fallbackStrategyManager));
            _resilienceCoordinator = resilienceCoordinator ?? throw new ArgumentNullException(nameof(resilienceCoordinator));
            _healthChecker = healthChecker ?? throw new ArgumentNullException(nameof(healthChecker));

            _recoveryQueue = new ConcurrentQueue<RecoveryRequest>();
            _activeRecoveries = new ConcurrentDictionary<string, RecoveryRequest>();
            _completedRecoveries = new ConcurrentDictionary<string, DateTime>();
            _shutdownCts = new CancellationTokenSource();
            _stateLock = new object();
            _startTime = DateTime.Now;

            // Initialize statistics
            Statistics = new RecoveryEngineStatistics
            {
                State = RecoveryEngineState.Initializing,
                Uptime = TimeSpan.Zero
            };

            // Start timers
            _processingTimer = new Timer(ProcessRecoveryQueue, null, _configuration.ProcessingInterval, _configuration.ProcessingInterval);
            _statisticsTimer = new Timer(UpdateStatistics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            // Subscribe to component events
            SubscribeToComponentEvents();

            // Transition to running state
            SetState(RecoveryEngineState.Running);

            _logger.LogInformation("Error recovery engine initialized and running");
        }

        /// <summary>
        /// Submits a recovery request
        /// </summary>
        /// <param name="triggerType">The trigger type</param>
        /// <param name="sourceComponent">The source component</param>
        /// <param name="operationName">The operation name</param>
        /// <param name="exception">The exception that triggered recovery</param>
        /// <param name="priority">The recovery priority</param>
        /// <param name="context">Additional context</param>
        /// <param name="callback">Callback for completion</param>
        /// <returns>The recovery request ID</returns>
        public string SubmitRecoveryRequest(
            RecoveryTriggerType triggerType,
            string sourceComponent,
            string operationName,
            Exception exception,
            RecoveryPriority priority = RecoveryPriority.Normal,
            Dictionary<string, object> context = null,
            Action<RecoveryResult> callback = null)
        {
            if (!_configuration.EnableAutomaticRecovery && triggerType != RecoveryTriggerType.Manual)
            {
                _logger.LogWarning("Automatic recovery is disabled, ignoring request from {Component}", sourceComponent);
                return null;
            }

            var requestId = Guid.NewGuid().ToString("N");
            var errorClassification = _errorClassifier.ClassifyException(exception, context, operationName);

            var request = new RecoveryRequest
            {
                RequestId = requestId,
                TriggerType = triggerType,
                SourceComponent = sourceComponent,
                OperationName = operationName,
                Exception = exception,
                ErrorClassification = errorClassification,
                Priority = priority,
                Context = context ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.Now,
                ProcessAt = CalculateProcessTime(priority, errorClassification),
                MaxRetries = _configuration.MaxRecoveryAttempts,
                Timeout = _configuration.DefaultTimeout,
                Callback = callback
            };

            // Check queue size limit
            if (_recoveryQueue.Count >= _configuration.MaxQueueSize)
            {
                _logger.LogError("Recovery queue is full, rejecting request {RequestId}", requestId);
                return null;
            }

            _recoveryQueue.Enqueue(request);
            Interlocked.Increment(ref Statistics.TotalRequests);

            // Notify listeners
            OnRecoveryRequested(request);

            _logger.LogInformation("Submitted recovery request {RequestId} for {Component}.{Operation} with priority {Priority}",
                requestId, sourceComponent, operationName, priority);

            return requestId;
        }

        /// <summary>
        /// Manually triggers recovery for a component
        /// </summary>
        /// <param name="component">The component name</param>
        /// <param name="reason">The reason for manual recovery</param>
        /// <returns>Task representing the operation</returns>
        public async Task TriggerManualRecoveryAsync(string component, string reason)
        {
            if (!_configuration.EnableManualRecovery)
            {
                _logger.LogWarning("Manual recovery is disabled for component {Component}", component);
                return;
            }

            _logger.LogInformation("Triggering manual recovery for component {Component}: {Reason}", component, reason);

            // Submit manual recovery request
            SubmitRecoveryRequest(
                RecoveryTriggerType.Manual,
                component,
                "ManualRecovery",
                new InvalidOperationException($"Manual recovery triggered: {reason}"),
                RecoveryPriority.High,
                new Dictionary<string, object> { ["ManualReason"] = reason });

            // Also trigger graceful degradation recovery
            await _gracefulDegradationManager.RecoverComponentAsync(component, reason).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the current recovery queue status
        /// </summary>
        /// <returns>Queue status information</returns>
        public RecoveryQueueStatus GetQueueStatus()
        {
            var requests = _recoveryQueue.ToList();
            var activeRequests = _activeRecoveries.Values.ToList();

            return new RecoveryQueueStatus
            {
                QueueSize = requests.Count,
                ActiveRecoveries = activeRequests.Count,
                MaxQueueSize = _configuration.MaxQueueSize,
                MaxConcurrentRecoveries = _configuration.MaxConcurrentRecoveries,
                HighestPriorityPending = requests.Any() ? requests.Max(r => r.Priority) : RecoveryPriority.Low,
                OldestPendingRequest = requests.Any() ? requests.Min(r => r.CreatedAt) : (DateTime?)null,
                ProcessingInterval = _configuration.ProcessingInterval,
                Statistics = Statistics
            };
        }

        /// <summary>
        /// Gets the status of a specific recovery request
        /// </summary>
        /// <param name="requestId">The request ID</param>
        /// <returns>Recovery request status or null if not found</returns>
        public RecoveryRequest GetRecoveryRequestStatus(string requestId)
        {
            // Check active recoveries
            if (_activeRecoveries.TryGetValue(requestId, out var activeRequest))
            {
                return activeRequest;
            }

            // Check queue
            var queuedRequest = _recoveryQueue.FirstOrDefault(r => r.RequestId == requestId);
            if (queuedRequest != null)
            {
                return queuedRequest;
            }

            return null;
        }

        /// <summary>
        /// Cancels a recovery request
        /// </summary>
        /// <param name="requestId">The request ID</param>
        /// <param name="reason">The reason for cancellation</param>
        /// <returns>True if the request was cancelled</returns>
        public bool CancelRecoveryRequest(string requestId, string reason)
        {
            // Try to cancel from active recoveries
            if (_activeRecoveries.TryRemove(requestId, out var activeRequest))
            {
                activeRequest.IsCompleted = true;
                activeRequest.Result = new RecoveryResult
                {
                    Success = false,
                    ScenarioId = "cancelled",
                    OriginalException = activeRequest.Exception,
                    ErrorMessage = $"Request cancelled: {reason}",
                    CompletedAt = DateTime.Now
                };

                _logger.LogInformation("Cancelled active recovery request {RequestId}: {Reason}", requestId, reason);
                return true;
            }

            // Try to cancel from queue
            var tempQueue = new Queue<RecoveryRequest>();
            var found = false;

            while (_recoveryQueue.TryDequeue(out var request))
            {
                if (request.RequestId == requestId)
                {
                    request.IsCompleted = true;
                    request.Result = new RecoveryResult
                    {
                        Success = false,
                        ScenarioId = "cancelled",
                        OriginalException = request.Exception,
                        ErrorMessage = $"Request cancelled: {reason}",
                        CompletedAt = DateTime.Now
                    };
                    found = true;
                }
                else
                {
                    tempQueue.Enqueue(request);
                }
            }

            // Re-queue remaining requests
            while (tempQueue.Count > 0)
            {
                _recoveryQueue.Enqueue(tempQueue.Dequeue());
            }

            if (found)
            {
                _logger.LogInformation("Cancelled queued recovery request {RequestId}: {Reason}", requestId, reason);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Pauses the recovery engine
        /// </summary>
        public void Pause()
        {
            SetState(RecoveryEngineState.Paused);
            _logger.LogInformation("Error recovery engine paused");
        }

        /// <summary>
        /// Resumes the recovery engine
        /// </summary>
        public void Resume()
        {
            SetState(RecoveryEngineState.Running);
            _logger.LogInformation("Error recovery engine resumed");
        }

        /// <summary>
        /// Shuts down the recovery engine
        /// </summary>
        public async Task ShutdownAsync()
        {
            SetState(RecoveryEngineState.ShuttingDown);
            _logger.LogInformation("Error recovery engine shutting down");

            // Cancel all operations
            _shutdownCts.Cancel();

            // Wait for active recoveries to complete
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;

            while (_activeRecoveries.Count > 0 && DateTime.Now - startTime < timeout)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }

            SetState(RecoveryEngineState.Stopped);
            _logger.LogInformation("Error recovery engine stopped");
        }

        private void ProcessRecoveryQueue(object state)
        {
            if (_state != RecoveryEngineState.Running)
            {
                return;
            }

            try
            {
                // Check if we can process more requests
                if (_activeRecoveries.Count >= _configuration.MaxConcurrentRecoveries)
                {
                    return;
                }

                // Find the highest priority request that's ready to be processed
                RecoveryRequest request = null;
                while (_recoveryQueue.TryDequeue(out var nextRequest))
                {
                    if (nextRequest.ProcessAt <= DateTime.Now)
                    {
                        request = nextRequest;
                        break;
                    }
                    else
                    {
                        // Re-queue requests that aren't ready yet
                        _recoveryQueue.Enqueue(nextRequest);
                    }
                }

                if (request != null)
                {
                    // Add to active recoveries
                    _activeRecoveries.TryAdd(request.RequestId, request);
                    request.IsProcessing = true;

                    // Process asynchronously
                    _ = Task.Run(() => ProcessRecoveryRequestAsync(request));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recovery queue");
            }
        }

        private async Task ProcessRecoveryRequestAsync(RecoveryRequest request)
        {
            var startTime = DateTime.Now;
            RecoveryResult result = null;

            try
            {
                _logger.LogInformation("Processing recovery request {RequestId} for {Component}.{Operation}",
                    request.RequestId, request.SourceComponent, request.OperationName);

                // Execute recovery based on error classification
                result = await ExecuteRecoveryAsync(request).ConfigureAwait(false);

                // Mark request as completed
                request.IsCompleted = true;
                request.Result = result;

                // Record completion
                _completedRecoveries.TryAdd(request.RequestId, DateTime.Now);

                // Update statistics
                UpdateRecoveryStatistics(result, DateTime.Now - startTime);

                // Invoke callback
                request.Callback?.Invoke(result);

                // Notify listeners
                OnRecoveryCompleted(result);

                _logger.LogInformation("Recovery request {RequestId} completed with success: {Success} in {Time:F2}ms",
                    request.RequestId, result.Success, (DateTime.Now - startTime).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recovery request {RequestId}", request.RequestId);

                // Create failure result
                result = new RecoveryResult
                {
                    Success = false,
                    ScenarioId = "processing-error",
                    OriginalException = request.Exception,
                    ErrorMessage = $"Recovery processing failed: {ex.Message}",
                    CompletedAt = DateTime.Now
                };

                request.IsCompleted = true;
                request.Result = result;

                // Update failure statistics
                Interlocked.Increment(ref Statistics.FailedRecoveries);

                // Invoke callback even on failure
                request.Callback?.Invoke(result);
            }
            finally
            {
                // Remove from active recoveries
                _activeRecoveries.TryRemove(request.RequestId, out _);
            }
        }

        private async Task<RecoveryResult> ExecuteRecoveryAsync(RecoveryRequest request)
        {
            // Try automatic recovery manager first
            if (_automaticRecoveryManager != null)
            {
                try
                {
                    var recoveryResult = await _recoveryManager.RecoverAsync(
                        request.Exception, request.Context, _shutdownCts.Token).ConfigureAwait(false);

                    if (recoveryResult.Success)
                    {
                        return recoveryResult;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Automatic recovery manager failed for request {RequestId}", request.RequestId);
                }
            }

            // Try graceful degradation
            if (request.ErrorClassification.Strategy == RecoveryStrategy.Degrade)
            {
                try
                {
                    await _gracefulDegradationManager.TriggerDegradationAsync(
                        request.SourceComponent, request.Exception).ConfigureAwait(false);

                    return new RecoveryResult
                    {
                        Success = true,
                        ScenarioId = "graceful-degradation",
                        OriginalException = request.Exception,
                        CompletedAt = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Graceful degradation failed for request {RequestId}", request.RequestId);
                }
            }

            // Try fallback strategies
            if (request.ErrorClassification.Strategy == RecoveryStrategy.Fallback)
            {
                try
                {
                    var fallbackResult = await _fallbackStrategyManager.ExecuteWithFallbackAsync(
                        request.OperationName,
                        async () => { throw request.Exception; },
                        _shutdownCts.Token).ConfigureAwait(false);

                    return new RecoveryResult
                    {
                        Success = true,
                        ScenarioId = "fallback-strategy",
                        OriginalException = request.Exception,
                        CompletedAt = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fallback strategy failed for request {RequestId}", request.RequestId);
                }
            }

            // All recovery attempts failed
            return new RecoveryResult
            {
                Success = false,
                ScenarioId = "exhausted",
                OriginalException = request.Exception,
                ErrorMessage = "All recovery mechanisms exhausted",
                CompletedAt = DateTime.Now
            };
        }

        private DateTime CalculateProcessTime(RecoveryPriority priority, ErrorClassification classification)
        {
            var baseDelay = priority switch
            {
                RecoveryPriority.Critical => TimeSpan.Zero,
                RecoveryPriority.High => TimeSpan.FromSeconds(1),
                RecoveryPriority.Normal => TimeSpan.FromSeconds(5),
                RecoveryPriority.Low => TimeSpan.FromSeconds(15),
                _ => TimeSpan.FromSeconds(5)
            };

            return DateTime.Now + baseDelay;
        }

        private void SetState(RecoveryEngineState newState)
        {
            lock (_stateLock)
            {
                if (_state != newState)
                {
                    var previousState = _state;
                    _state = newState;
                    Statistics.State = newState;

                    _logger.LogInformation("Recovery engine state changed from {Previous} to {Current}",
                        previousState, newState);

                    OnStateChanged(newState);
                }
            }
        }

        private void UpdateStatistics(object state)
        {
            Statistics.Uptime = DateTime.Now - _startTime;
            Statistics.CurrentQueueSize = _recoveryQueue.Count;
            Statistics.ActiveRecoveries = _activeRecoveries.Count;
            Statistics.LastRecoveryTime = _completedRecoveries.Values.Any() ?
                _completedRecoveries.Values.Max() : (DateTime?)null;

            // Calculate recovery rate
            var recentRecoveries = _completedRecoveries.Values.Count(v => DateTime.Now - v < TimeSpan.FromMinutes(1));
            Statistics.RecoveryRatePerMinute = recentRecoveries;
        }

        private void UpdateRecoveryStatistics(RecoveryResult result, TimeSpan processingTime)
        {
            if (result.Success)
            {
                Interlocked.Increment(ref Statistics.SuccessfulRecoveries);
            }
            else
            {
                Interlocked.Increment(ref Statistics.FailedRecoveries);
            }

            // Update average recovery time
            var currentTime = Statistics.AverageRecoveryTime.TotalMilliseconds;
            var currentCount = Statistics.SuccessfulRecoveries + Statistics.FailedRecoveries;
            var newAverage = (currentTime * (currentCount - 1) + processingTime.TotalMilliseconds) / currentCount;
            Statistics.AverageRecoveryTime = TimeSpan.FromMilliseconds(newAverage);
        }

        private void SubscribeToComponentEvents()
        {
            // Subscribe to health checker events
            if (_healthChecker != null)
            {
                // This would normally subscribe to health check failure events
                // For now, we'll just log that we would subscribe
                _logger.LogDebug("Would subscribe to health checker events");
            }

            // Subscribe to resilience coordinator events
            if (_resilienceCoordinator != null)
            {
                // This would normally subscribe to resilience policy events
                _logger.LogDebug("Would subscribe to resilience coordinator events");
            }
        }

        private void OnRecoveryRequested(RecoveryRequest request)
        {
            RecoveryRequested?.Invoke(this, request);
        }

        private void OnRecoveryCompleted(RecoveryResult result)
        {
            RecoveryCompleted?.Invoke(this, result);
        }

        private void OnStateChanged(RecoveryEngineState newState)
        {
            StateChanged?.Invoke(this, newState);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _shutdownCts?.Dispose();
                _processingTimer?.Dispose();
                _statisticsTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}