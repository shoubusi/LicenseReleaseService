using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines recovery priority levels
    /// </summary>
    public enum RecoveryPriority
    {
        /// <summary>
        /// Low priority recovery
        /// </summary>
        Low = 1,

        /// <summary>
        /// Normal priority recovery
        /// </summary>
        Normal = 2,

        /// <summary>
        /// High priority recovery
        /// </summary>
        High = 3,

        /// <summary>
        /// Critical priority recovery
        /// </summary>
        Critical = 4
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
        /// Gets or sets the recovery priority
        /// </summary>
        public RecoveryPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the exception that triggered recovery
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the error classification
        /// </summary>
        public ErrorClassification Classification { get; set; }

        /// <summary>
        /// Gets or sets the recovery scenario to execute
        /// </summary>
        public RecoveryScenario Scenario { get; set; }

        /// <summary>
        /// Gets or sets the context information
        /// </summary>
        public object Context { get; set; }

        /// <summary>
        /// Gets or sets the original operation that failed
        /// </summary>
        public Func<Task<object>> OriginalOperation { get; set; }

        /// <summary>
        /// Gets or sets the maximum retry count
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Gets or sets the current retry count
        /// </summary>
        public int CurrentRetry { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the recovery attempt
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the request was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the request should be processed
        /// </summary>
        public DateTime ProcessAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request is currently being processed
        /// </summary>
        public bool IsProcessing { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request has completed
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets the recovery result
        /// </summary>
        public RecoveryResult Result { get; set; }

        /// <summary>
        /// Gets or sets the callback to invoke when recovery is complete
        /// </summary>
        public Action<RecoveryResult> Callback { get; set; }
    }

    /// <summary>
    /// Handles automatic recovery mechanisms with intelligent prioritization
    /// </summary>
    public class AutomaticRecoveryManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Microsoft.Extensions.DependencyInjection.IServiceProvider _serviceProvider;
        private readonly ErrorClassifier _errorClassifier;
        private readonly RecoveryManager _recoveryManager;
        private readonly ConcurrentQueue<RecoveryRequest> _recoveryQueue;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly Task _processorTask;
        private readonly SemaphoreSlim _queueLock;
        private readonly TimeSpan _processingInterval;
        private readonly int _maxConcurrentRecoveries;
        private readonly int _activeRecoveries;
        private bool _disposed;

        /// <summary>
        /// Gets the current recovery statistics
        /// </summary>
        public RecoveryManagerStatistics Statistics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the AutomaticRecoveryManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="serviceProvider">Service provider for dependency resolution</param>
        /// <param name="errorClassifier">Error classifier instance</param>
        /// <param name="recoveryManager">Recovery manager instance</param>
        public AutomaticRecoveryManager(
            ILogger logger,
            Microsoft.Extensions.DependencyInjection.IServiceProvider serviceProvider,
            ErrorClassifier errorClassifier,
            RecoveryManager recoveryManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _errorClassifier = errorClassifier ?? throw new ArgumentNullException(nameof(errorClassifier));
            _recoveryManager = recoveryManager ?? throw new ArgumentNullException(nameof(recoveryManager));

            _recoveryQueue = new ConcurrentQueue<RecoveryRequest>();
            _shutdownCts = new CancellationTokenSource();
            _queueLock = new SemaphoreSlim(1, 1);
            _processingInterval = TimeSpan.FromSeconds(1);
            _maxConcurrentRecoveries = 3;
            _activeRecoveries = 0;

            Statistics = new RecoveryManagerStatistics();
            _processorTask = Task.Run(ProcessRecoveryQueueAsync);
        }

        /// <summary>
        /// Submits a recovery request
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="context">Additional context</param>
        /// <param name="originalOperation">The original operation that failed</param>
        /// <param name="callback">Optional callback for completion</param>
        /// <returns>The recovery request ID</returns>
        public string SubmitRecoveryRequest(
            Exception exception,
            object context = null,
            Func<Task<object>> originalOperation = null,
            Action<RecoveryResult> callback = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var requestId = Guid.NewGuid().ToString("N");
            var classification = _errorClassifier.ClassifyException(exception, context);
            var scenario = FindRecoveryScenario(classification);

            var request = new RecoveryRequest
            {
                RequestId = requestId,
                Exception = exception,
                Classification = classification,
                Scenario = scenario,
                Context = context,
                OriginalOperation = originalOperation,
                Priority = GetRecoveryPriority(classification),
                MaxRetries = classification.IsRetryable ? classification.MaxRetries : 0,
                CurrentRetry = 0,
                Timeout = GetRecoveryTimeout(classification),
                CreatedAt = DateTime.Now,
                ProcessAt = CalculateProcessTime(classification),
                Callback = callback
            };

            _recoveryQueue.Enqueue(request);
            Statistics.TotalRequests++;
            Statistics.QueueSize = _recoveryQueue.Count;

            _logger.LogInformation("Submitted recovery request {RequestId} for {ErrorCategory} error with priority {Priority}",
                requestId, classification.Category, request.Priority);

            return requestId;
        }

        /// <summary>
        /// Submits a recovery request with explicit parameters
        /// </summary>
        /// <param name="classification">Error classification</param>
        /// <param name="scenario">Recovery scenario</param>
        /// <param name="context">Additional context</param>
        /// <param name="originalOperation">The original operation that failed</param>
        /// <param name="priority">Recovery priority</param>
        /// <param name="callback">Optional callback for completion</param>
        /// <returns>The recovery request ID</returns>
        public string SubmitExplicitRecoveryRequest(
            ErrorClassification classification,
            RecoveryScenario scenario,
            object context = null,
            Func<Task<object>> originalOperation = null,
            RecoveryPriority priority = RecoveryPriority.Normal,
            Action<RecoveryResult> callback = null)
        {
            if (classification == null)
                throw new ArgumentNullException(nameof(classification));

            var requestId = Guid.NewGuid().ToString("N");

            var request = new RecoveryRequest
            {
                RequestId = requestId,
                Classification = classification,
                Scenario = scenario,
                Context = context,
                OriginalOperation = originalOperation,
                Priority = priority,
                MaxRetries = classification.IsRetryable ? classification.MaxRetries : 0,
                CurrentRetry = 0,
                Timeout = GetRecoveryTimeout(classification),
                CreatedAt = DateTime.Now,
                ProcessAt = DateTime.Now, // Process immediately
                Callback = callback
            };

            _recoveryQueue.Enqueue(request);
            Statistics.TotalRequests++;
            Statistics.QueueSize = _recoveryQueue.Count;

            _logger.LogInformation("Submitted explicit recovery request {RequestId} for {ErrorCategory} error with priority {Priority}",
                requestId, classification.Category, priority);

            return requestId;
        }

        /// <summary>
        /// Processes the recovery queue asynchronously
        /// </summary>
        private async Task ProcessRecoveryQueueAsync()
        {
            while (!_shutdownCts.Token.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextRequestAsync().ConfigureAwait(false);
                    await Task.Delay(_processingInterval, _shutdownCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing recovery queue");
                    await Task.Delay(TimeSpan.FromSeconds(5), _shutdownCts.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Processes the next recovery request
        /// </summary>
        private async Task ProcessNextRequestAsync()
        {
            if (_activeRecoveries >= _maxConcurrentRecoveries)
            {
                return;
            }

            RecoveryRequest request = null;
            await _queueLock.WaitAsync(_shutdownCts.Token).ConfigureAwait(false);

            try
            {
                // Find the highest priority request that's ready to be processed
                var readyRequests = _recoveryQueue
                    .Where(r => !r.IsProcessing && !r.IsCompleted && r.ProcessAt <= DateTime.Now)
                    .OrderByDescending(r => r.Priority)
                    .ThenBy(r => r.ProcessAt)
                    .ToList();

                if (readyRequests.Any())
                {
                    request = readyRequests.First();
                    request.IsProcessing = true;
                    Interlocked.Increment(ref _activeRecoveries);
                }
            }
            finally
            {
                _queueLock.Release();
            }

            if (request != null)
            {
                _ = Task.Run(() => ProcessRecoveryRequestAsync(request));
            }
        }

        /// <summary>
        /// Processes a specific recovery request
        /// </summary>
        /// <param name="request">The recovery request to process</param>
        private async Task ProcessRecoveryRequestAsync(RecoveryRequest request)
        {
            try
            {
                _logger.LogInformation("Processing recovery request {RequestId} (attempt {CurrentRetry}/{MaxRetries})",
                    request.RequestId, request.CurrentRetry + 1, request.MaxRetries);

                var startTime = DateTime.Now;

                // Execute recovery scenario
                if (request.Scenario != null)
                {
                    request.Result = await _recoveryManager.RecoverAsync(
                        request.Exception, request.Context, _shutdownCts.Token).ConfigureAwait(false);
                }
                else
                {
                    // Fallback to retry logic
                    request.Result = await ExecuteFallbackRecoveryAsync(request).ConfigureAwait(false);
                }

                // Check if we should retry the original operation
                if (request.Result.Success && request.OriginalOperation != null)
                {
                    try
                    {
                        var operationResult = await RetryOriginalOperationAsync(request).ConfigureAwait(false);
                        if (operationResult != null)
                        {
                            request.Result.Success = true;
                            _logger.LogInformation("Original operation succeeded after recovery for request {RequestId}", request.RequestId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Original operation failed after recovery for request {RequestId}", request.RequestId);
                        request.Result.Success = false;
                        request.Result.ErrorMessage = $"Original operation failed: {ex.Message}";
                    }
                }

                // Update statistics
                var processingTime = DateTime.Now - startTime;
                UpdateStatistics(request, processingTime);

                // Invoke callback if provided
                request.Callback?.Invoke(request.Result);

                _logger.LogInformation("Recovery request {RequestId} completed with success: {Success} in {Time:F2}ms",
                    request.RequestId, request.Result.Success, processingTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recovery request {RequestId}", request.RequestId);

                // Update failure statistics
                Statistics.FailedRecoveries++;
                Statistics.FailureRate = Statistics.TotalProcessed > 0 ? (double)Statistics.FailedRecoveries / Statistics.TotalProcessed : 0;

                // Create failure result
                request.Result = new RecoveryResult
                {
                    Success = false,
                    ScenarioId = request.Scenario?.Id,
                    OriginalException = request.Exception,
                    ErrorMessage = $"Recovery processing failed: {ex.Message}",
                    CompletedAt = DateTime.Now
                };

                // Invoke callback even on failure
                request.Callback?.Invoke(request.Result);
            }
            finally
            {
                request.IsCompleted = true;
                request.IsProcessing = false;
                Interlocked.Decrement(ref _activeRecoveries);
                Statistics.QueueSize = _recoveryQueue.Count;
            }
        }

        /// <summary>
        /// Executes fallback recovery logic
        /// </summary>
        /// <param name="request">The recovery request</param>
        /// <returns>Recovery result</returns>
        private async Task<RecoveryResult> ExecuteFallbackRecoveryAsync(RecoveryRequest request)
        {
            if (!request.Classification.IsRetryable || request.CurrentRetry >= request.MaxRetries)
            {
                return new RecoveryResult
                {
                    Success = false,
                    ScenarioId = "fallback",
                    OriginalException = request.Exception,
                    ErrorMessage = "Recovery not possible - max retries exceeded or not retryable",
                    CompletedAt = DateTime.Now
                };
            }

            // Simple retry with delay
            var delay = CalculateRetryDelay(request);
            await Task.Delay(delay, _shutdownCts.Token).ConfigureAwait(false);

            if (request.OriginalOperation != null)
            {
                try
                {
                    await request.OriginalOperation().ConfigureAwait(false);
                    return new RecoveryResult
                    {
                        Success = true,
                        ScenarioId = "fallback-retry",
                        OriginalException = request.Exception,
                        CompletedAt = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    // Retry failed
                    request.CurrentRetry++;
                    if (request.CurrentRetry < request.MaxRetries)
                    {
                        // Re-enqueue for another attempt
                        request.ProcessAt = DateTime.Now + CalculateRetryDelay(request);
                        request.IsProcessing = false;
                        request.IsCompleted = false;
                        _recoveryQueue.Enqueue(request);
                    }

                    return new RecoveryResult
                    {
                        Success = false,
                        ScenarioId = "fallback-retry",
                        OriginalException = request.Exception,
                        ErrorMessage = $"Fallback retry failed: {ex.Message}",
                        CompletedAt = DateTime.Now
                    };
                }
            }

            return new RecoveryResult
            {
                Success = true,
                ScenarioId = "fallback-noop",
                OriginalException = request.Exception,
                CompletedAt = DateTime.Now
            };
        }

        /// <summary>
        /// Retries the original operation
        /// </summary>
        /// <param name="request">The recovery request</param>
        /// <returns>Operation result or null if failed</returns>
        private async Task<object> RetryOriginalOperationAsync(RecoveryRequest request)
        {
            if (request.OriginalOperation == null)
                return null;

            // Use retry policy from classification
            var retryPolicy = _errorClassifier.GetRecommendedRetryPolicy(request.Classification);
            if (retryPolicy.MaxRetries == 0)
            {
                return await request.OriginalOperation().ConfigureAwait(false);
            }

            var retryManager = new RetryPolicy(_logger, retryPolicy);
            return await retryManager.ExecuteAsync(request.OriginalOperation, _shutdownCts.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds a recovery scenario for the given classification
        /// </summary>
        /// <param name="classification">Error classification</param>
        /// <returns>Matching recovery scenario or null</returns>
        private RecoveryScenario FindRecoveryScenario(ErrorClassification classification)
        {
            var scenarios = _recoveryManager.GetRegisteredScenarios();

            // Look for scenarios that match the error category
            return scenarios.FirstOrDefault(s => s.IsEnabled && s.Id.Contains(classification.Category.ToString().ToLower()));
        }

        /// <summary>
        /// Gets the recovery priority for the given classification
        /// </summary>
        /// <param name="classification">Error classification</param>
        /// <returns>Recovery priority</returns>
        private RecoveryPriority GetRecoveryPriority(ErrorClassification classification)
        {
            return classification.Severity switch
            {
                ErrorSeverity.Critical => RecoveryPriority.Critical,
                ErrorSeverity.Error => RecoveryPriority.High,
                ErrorSeverity.Warning => RecoveryPriority.Normal,
                ErrorSeverity.Information => RecoveryPriority.Low,
                _ => RecoveryPriority.Normal
            };
        }

        /// <summary>
        /// Gets the recovery timeout for the given classification
        /// </summary>
        /// <param name="classification">Error classification</param>
        /// <returns>Recovery timeout</returns>
        private TimeSpan GetRecoveryTimeout(ErrorClassification classification)
        {
            return classification.Severity switch
            {
                ErrorSeverity.Critical => TimeSpan.FromMinutes(5),
                ErrorSeverity.Error => TimeSpan.FromMinutes(2),
                ErrorSeverity.Warning => TimeSpan.FromMinutes(1),
                ErrorSeverity.Information => TimeSpan.FromSeconds(30),
                _ => TimeSpan.FromMinutes(1)
            };
        }

        /// <summary>
        /// Calculates when the request should be processed
        /// </summary>
        /// <param name="classification">Error classification</param>
        /// <returns>Process time</returns>
        private DateTime CalculateProcessTime(ErrorClassification classification)
        {
            var delay = classification.RetryDelay;
            return DateTime.Now + delay;
        }

        /// <summary>
        /// Calculates retry delay
        /// </summary>
        /// <param name="request">Recovery request</param>
        /// <returns>Retry delay</returns>
        private TimeSpan CalculateRetryDelay(RecoveryRequest request)
        {
            var baseDelay = request.Classification.RetryDelay;
            var multiplier = Math.Pow(2, request.CurrentRetry);
            return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * multiplier);
        }

        /// <summary>
        /// Updates recovery statistics
        /// </summary>
        /// <param name="request">Recovery request</param>
        /// <param name="processingTime">Processing time</param>
        private void UpdateStatistics(RecoveryRequest request, TimeSpan processingTime)
        {
            Statistics.TotalProcessed++;
            Statistics.QueueSize = _recoveryQueue.Count;

            if (request.Result.Success)
            {
                Statistics.SuccessfulRecoveries++;
            }
            else
            {
                Statistics.FailedRecoveries++;
            }

            Statistics.SuccessRate = Statistics.TotalProcessed > 0 ? (double)Statistics.SuccessfulRecoveries / Statistics.TotalProcessed : 0;
            Statistics.FailureRate = Statistics.TotalProcessed > 0 ? (double)Statistics.FailedRecoveries / Statistics.TotalProcessed : 0;

            Statistics.AverageProcessingTime = TimeSpan.FromMilliseconds(
                (Statistics.AverageProcessingTime.TotalMilliseconds * (Statistics.TotalProcessed - 1) + processingTime.TotalMilliseconds) / Statistics.TotalProcessed);

            Statistics.LastProcessedAt = DateTime.Now;
        }

        /// <summary>
        /// Gets the current recovery queue status
        /// </summary>
        /// <returns>Queue status information</returns>
        public RecoveryQueueStatus GetQueueStatus()
        {
            var requests = _recoveryQueue.ToList();
            var activeRequests = requests.Where(r => r.IsProcessing).ToList();
            var pendingRequests = requests.Where(r => !r.IsProcessing && !r.IsCompleted).ToList();
            var completedRequests = requests.Where(r => r.IsCompleted).ToList();

            return new RecoveryQueueStatus
            {
                QueueSize = requests.Count,
                ActiveRecoveries = activeRequests.Count,
                PendingRecoveries = pendingRequests.Count,
                CompletedRecoveries = completedRequests.Count,
                HighestPriorityPending = pendingRequests.Any() ? pendingRequests.Max(r => r.Priority) : RecoveryPriority.Low,
                OldestPendingRequest = pendingRequests.Any() ? pendingRequests.Min(r => r.CreatedAt) : (DateTime?)null,
                Statistics = Statistics
            };
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _shutdownCts.Cancel();
                _processorTask?.Wait(TimeSpan.FromSeconds(5));
                _shutdownCts?.Dispose();
                _queueLock?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Recovery manager statistics
    /// </summary>
    public class RecoveryManagerStatistics
    {
        /// <summary>
        /// Gets or sets the total number of recovery requests
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the total number of processed requests
        /// </summary>
        public int TotalProcessed { get; set; }

        /// <summary>
        /// Gets or sets the number of successful recoveries
        /// </summary>
        public int SuccessfulRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the number of failed recoveries
        /// </summary>
        public int FailedRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the current queue size
        /// </summary>
        public int QueueSize { get; set; }

        /// <summary>
        /// Gets or sets the success rate
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the failure rate
        /// </summary>
        public double FailureRate { get; set; }

        /// <summary>
        /// Gets or sets the average processing time
        /// </summary>
        public TimeSpan AverageProcessingTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last processed request
        /// </summary>
        public DateTime? LastProcessedAt { get; set; }
    }

    /// <summary>
    /// Recovery queue status information
    /// </summary>
    public class RecoveryQueueStatus
    {
        /// <summary>
        /// Gets or sets the total queue size
        /// </summary>
        public int QueueSize { get; set; }

        /// <summary>
        /// Gets or sets the number of active recoveries
        /// </summary>
        public int ActiveRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the number of pending recoveries
        /// </summary>
        public int PendingRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the number of completed recoveries
        /// </summary>
        public int CompletedRecoveries { get; set; }

        /// <summary>
        /// Gets or sets the highest priority pending request
        /// </summary>
        public RecoveryPriority HighestPriorityPending { get; set; }

        /// <summary>
        /// Gets or sets the oldest pending request timestamp
        /// </summary>
        public DateTime? OldestPendingRequest { get; set; }

        /// <summary>
        /// Gets or sets the recovery statistics
        /// </summary>
        public RecoveryManagerStatistics Statistics { get; set; }
    }
}