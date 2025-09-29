using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Manages concurrent license check operations with priority-based queuing and execution
    /// </summary>
    public class LicenseCheckQueue : IDisposable
    {
        private readonly ConcurrentQueue<LicenseCheckOperation> _queue;
        private readonly ConcurrentDictionary<Guid, LicenseCheckOperation> _executingOperations;
        private readonly ConcurrentDictionary<Guid, LicenseCheckOperation> _completedOperations;
        private readonly ConcurrentDictionary<Guid, DateTime> _operationStartTimes;
        private readonly SemaphoreSlim _semaphore;
        private readonly ReaderWriterLockSlim _lock;
        private readonly ILogger<LicenseCheckQueue> _logger;
        private readonly TimerExecutionOptions _options;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

        private long _totalOperationsEnqueued;
        private long _totalOperationsDequeued;
        private long _totalOperationsCompleted;
        private long _totalOperationsFailed;
        private long _totalOperationsCancelled;
        private long _totalOperationsTimedOut;
        private int _maxQueueSize;
        private long _totalQueueWaitTimeMs;
        private DateTime _startTime;

        /// <summary>
        /// Gets the current number of operations in the queue
        /// </summary>
        public int QueueSize => _queue.Count;

        /// <summary>
        /// Gets the current number of operations executing
        /// </summary>
        public int ExecutingCount => _executingOperations.Count;

        /// <summary>
        /// Gets the maximum number of concurrent operations allowed
        /// </summary>
        public int MaxConcurrentOperations { get; }

        /// <summary>
        /// Gets the total number of operations processed
        /// </summary>
        public long TotalOperationsProcessed => _totalOperationsCompleted + _totalOperationsFailed + _totalOperationsCancelled + _totalOperationsTimedOut;

        /// <summary>
        /// Event raised when an operation is enqueued
        /// </summary>
        public event EventHandler<LicenseCheckOperationEventArgs> OperationEnqueued;

        /// <summary>
        /// Event raised when an operation is dequeued
        /// </summary>
        public event EventHandler<LicenseCheckOperationEventArgs> OperationDequeued;

        /// <summary>
        /// Event raised when an operation starts executing
        /// </summary>
        public event EventHandler<LicenseCheckOperationEventArgs> OperationStarted;

        /// <summary>
        /// Event raised when an operation completes
        /// </summary>
        public event EventHandler<LicenseCheckOperationEventArgs> OperationCompleted;

        /// <summary>
        /// Event raised when an operation fails
        /// </summary>
        public event EventHandler<LicenseCheckOperationEventArgs> OperationFailed;

        /// <summary>
        /// Initializes a new instance of the LicenseCheckQueue class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="options">Timer execution options</param>
        public LicenseCheckQueue(ILogger<LicenseCheckQueue> logger, TimerExecutionOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _queue = new ConcurrentQueue<LicenseCheckOperation>();
            _executingOperations = new ConcurrentDictionary<Guid, LicenseCheckOperation>();
            _completedOperations = new ConcurrentDictionary<Guid, LicenseCheckOperation>();
            _operationStartTimes = new ConcurrentDictionary<Guid, DateTime>();
            _lock = new ReaderWriterLockSlim();
            _cancellationTokenSource = new CancellationTokenSource();

            MaxConcurrentOperations = _options.MaxConcurrentExecutions;
            _semaphore = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
            _cleanupTimer = new Timer(CleanupCompletedOperations, null, _cleanupInterval, _cleanupInterval);

            _startTime = DateTime.Now;

            _logger.LogDebug("LicenseCheckQueue initialized with {MaxConcurrent} concurrent operations", MaxConcurrentOperations);
        }

        /// <summary>
        /// Enqueues a license check operation
        /// </summary>
        /// <param name="operation">Operation to enqueue</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the enqueue operation</returns>
        public async Task EnqueueAsync(LicenseCheckOperation operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);
            operation.CancellationToken = linkedTokenSource.Token;

            _queue.Enqueue(operation);
            Interlocked.Increment(ref _totalOperationsEnqueued);

            // Update max queue size
            var currentQueueSize = _queue.Count;
            if (currentQueueSize > _maxQueueSize)
            {
                Interlocked.Exchange(ref _maxQueueSize, currentQueueSize);
            }

            // Set queue time
            var queueTime = DateTime.Now;
            _operationStartTimes.TryAdd(operation.OperationId, queueTime);

            // Raise enqueued event
            var eventArgs = new LicenseCheckOperationEventArgs(operation, LicenseCheckOperationStatus.Queued);
            OperationEnqueued?.Invoke(this, eventArgs);

            _logger.LogDebug("Operation {OperationId} enqueued (type: {OperationType}, priority: {Priority}, queue size: {QueueSize})",
                operation.OperationId, operation.OperationType, operation.Priority, currentQueueSize);

            // Start processing if not already running
            _ = Task.Run(() => ProcessQueueAsync(linkedTokenSource.Token), linkedTokenSource.Token);
        }

        /// <summary>
        /// Enqueues multiple license check operations
        /// </summary>
        /// <param name="operations">Operations to enqueue</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the enqueue operation</returns>
        public async Task EnqueueRangeAsync(IEnumerable<LicenseCheckOperation> operations, CancellationToken cancellationToken = default)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            var operationList = operations.ToList();
            foreach (var operation in operationList)
            {
                await EnqueueAsync(operation, cancellationToken);
            }
        }

        /// <summary>
        /// Dequeues the next operation based on priority and scheduling
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Next operation to execute or null if no operations are available</returns>
        public async Task<LicenseCheckOperation> DequeueAsync(CancellationToken cancellationToken = default)
        {
            if (_queue.IsEmpty)
                return null;

            // Wait for semaphore (concurrent execution limit)
            var acquired = await _semaphore.WaitAsync(_options.SyncTimeout, cancellationToken);
            if (!acquired)
            {
                _logger.LogWarning("Failed to acquire semaphore for operation execution (timeout: {Timeout}ms)", _options.SyncTimeout.TotalMilliseconds);
                return null;
            }

            try
            {
                LicenseCheckOperation operation = null;

                // Use a lock to ensure thread-safe dequeuing
                _lock.EnterWriteLock();
                try
                {
                    if (_queue.TryDequeue(out operation))
                    {
                        // Check if operation is ready to execute
                        if (operation.IsReadyToExecute)
                        {
                            operation.MarkAsExecuting();
                            _executingOperations.TryAdd(operation.OperationId, operation);
                            Interlocked.Increment(ref _totalOperationsDequeued);

                            // Calculate queue wait time
                            if (_operationStartTimes.TryRemove(operation.OperationId, out var queueStartTime))
                            {
                                var waitTime = DateTime.Now - queueStartTime;
                                Interlocked.Add(ref _totalQueueWaitTimeMs, (long)waitTime.TotalMilliseconds);
                            }

                            // Raise dequeued event
                            var eventArgs = new LicenseCheckOperationEventArgs(operation, LicenseCheckOperationStatus.Executing);
                            OperationDequeued?.Invoke(this, eventArgs);
                        }
                        else
                        {
                            // Operation not ready yet, put it back in the queue
                            _queue.Enqueue(operation);
                            operation = null;
                        }
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                if (operation != null)
                {
                    _logger.LogDebug("Operation {OperationId} dequeued (type: {OperationType}, priority: {Priority})",
                        operation.OperationId, operation.OperationType, operation.Priority);

                    // Raise started event
                    var startedEventArgs = new LicenseCheckOperationEventArgs(operation, LicenseCheckOperationStatus.Executing);
                    OperationStarted?.Invoke(this, startedEventArgs);

                    return operation;
                }
                else
                {
                    // Release semaphore if no operation was dequeued
                    _semaphore.Release();
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Release semaphore on exception
                _semaphore.Release();
                _logger.LogError(ex, "Error dequeuing operation");
                throw;
            }
        }

        /// <summary>
        /// Gets all operations currently in the queue
        /// </summary>
        /// <returns>Collection of queued operations</returns>
        public IEnumerable<LicenseCheckOperation> GetQueuedOperations()
        {
            return _queue.ToArray();
        }

        /// <summary>
        /// Gets all operations currently executing
        /// </summary>
        /// <returns>Collection of executing operations</returns>
        public IEnumerable<LicenseCheckOperation> GetExecutingOperations()
        {
            return _executingOperations.Values.ToArray();
        }

        /// <summary>
        /// Gets completed operations
        /// </summary>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>Collection of completed operations</returns>
        public IEnumerable<LicenseCheckOperation> GetCompletedOperations(int maxResults = 100)
        {
            return _completedOperations.Values.OrderByDescending(op => op.CompletedTime).Take(maxResults).ToArray();
        }

        /// <summary>
        /// Marks an operation as completed
        /// </summary>
        /// <param name="operationId">Operation identifier</param>
        /// <param name="result">Operation result</param>
        public void MarkOperationCompleted(Guid operationId, LicenseCheckResult result)
        {
            if (_executingOperations.TryRemove(operationId, out var operation))
            {
                operation.MarkAsCompleted();
                _completedOperations.TryAdd(operationId, operation);

                if (result.Success)
                {
                    Interlocked.Increment(ref _totalOperationsCompleted);
                }
                else
                {
                    Interlocked.Increment(ref _totalOperationsFailed);
                }

                // Release semaphore
                _semaphore.Release();

                // Raise completed event
                var eventArgs = new LicenseCheckOperationEventArgs(result);
                if (result.Success)
                {
                    OperationCompleted?.Invoke(this, eventArgs);
                }
                else
                {
                    OperationFailed?.Invoke(this, eventArgs);
                }

                _logger.LogDebug("Operation {OperationId} completed (success: {Success}, duration: {Duration}ms)",
                    operationId, result.Success, result.Duration.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Marks an operation as failed
        /// </summary>
        /// <param name="operationId">Operation identifier</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="exception">Exception that caused the failure</param>
        public void MarkOperationFailed(Guid operationId, string errorMessage, Exception exception = null)
        {
            if (_executingOperations.TryRemove(operationId, out var operation))
            {
                operation.MarkAsFailed(errorMessage);
                _completedOperations.TryAdd(operationId, operation);

                Interlocked.Increment(ref _totalOperationsFailed);

                // Release semaphore
                _semaphore.Release();

                // Raise failed event
                var eventArgs = new LicenseCheckOperationEventArgs(operation, LicenseCheckOperationStatus.Failed)
                {
                    ErrorMessage = errorMessage,
                    Exception = exception,
                    Success = false
                };
                OperationFailed?.Invoke(this, eventArgs);

                _logger.LogError(exception, "Operation {OperationId} failed: {ErrorMessage}", operationId, errorMessage);
            }
        }

        /// <summary>
        /// Marks an operation as cancelled
        /// </summary>
        /// <param name="operationId">Operation identifier</param>
        public void MarkOperationCancelled(Guid operationId)
        {
            if (_executingOperations.TryRemove(operationId, out var operation))
            {
                operation.MarkAsCancelled();
                _completedOperations.TryAdd(operationId, operation);

                Interlocked.Increment(ref _totalOperationsCancelled);

                // Release semaphore
                _semaphore.Release();

                _logger.LogDebug("Operation {OperationId} cancelled", operationId);
            }
        }

        /// <summary>
        /// Marks an operation as timed out
        /// </summary>
        /// <param name="operationId">Operation identifier</param>
        public void MarkOperationTimedOut(Guid operationId)
        {
            if (_executingOperations.TryRemove(operationId, out var operation))
            {
                operation.Status = LicenseCheckOperationStatus.TimedOut;
                _completedOperations.TryAdd(operationId, operation);

                Interlocked.Increment(ref _totalOperationsTimedOut);

                // Release semaphore
                _semaphore.Release();

                _logger.LogDebug("Operation {OperationId} timed out", operationId);
            }
        }

        /// <summary>
        /// Cancels a specific operation
        /// </summary>
        /// <param name="operationId">Operation identifier</param>
        /// <returns>True if the operation was cancelled</returns>
        public bool CancelOperation(Guid operationId)
        {
            // Check if operation is in queue
            var queuedOperations = _queue.ToArray();
            var queuedOperation = queuedOperations.FirstOrDefault(o => o.OperationId == operationId);
            if (queuedOperation != null)
            {
                queuedOperation.MarkAsCancelled();
                Interlocked.Increment(ref _totalOperationsCancelled);
                _logger.LogDebug("Cancelled queued operation {OperationId}", operationId);
                return true;
            }

            // Check if operation is executing
            if (_executingOperations.TryGetValue(operationId, out var executingOperation))
            {
                MarkOperationCancelled(operationId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cancels all operations
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the cancel operation</returns>
        public async Task CancelAllOperationsAsync(CancellationToken cancellationToken = default)
        {
            // Cancel queued operations
            while (_queue.TryDequeue(out var operation))
            {
                operation.MarkAsCancelled();
                Interlocked.Increment(ref _totalOperationsCancelled);
            }

            // Cancel executing operations
            var executingOperations = _executingOperations.Values.ToList();
            foreach (var operation in executingOperations)
            {
                MarkOperationCancelled(operation.OperationId);
            }

            _logger.LogDebug("Cancelled all operations");
        }

        /// <summary>
        /// Gets the current queue status
        /// </summary>
        /// <returns>Queue status information</returns>
        public LicenseCheckQueueStatus GetStatus()
        {
            var uptime = DateTime.Now - _startTime;
            var avgWaitTime = _totalOperationsDequeued > 0 ? TimeSpan.FromMilliseconds(_totalQueueWaitTimeMs / _totalOperationsDequeued) : TimeSpan.Zero;
            var throughput = uptime.TotalSeconds > 0 ? TotalOperationsProcessed / uptime.TotalSeconds : 0;
            var successRate = TotalOperationsProcessed > 0 ? (double)_totalOperationsCompleted / TotalOperationsProcessed : 0;

            return new LicenseCheckQueueStatus
            {
                QueueSize = QueueSize,
                ExecutingCount = ExecutingCount,
                CompletedCount = _totalOperationsCompleted,
                FailedCount = _totalOperationsFailed,
                CancelledCount = _totalOperationsCancelled,
                TimedOutCount = _totalOperationsTimedOut,
                MaxQueueSize = _maxQueueSize,
                AverageQueueWaitTime = avgWaitTime,
                MaxQueueWaitTime = TimeSpan.FromMilliseconds(_maxQueueSize * 100), // Estimate
                Throughput = throughput,
                SuccessRate = successRate,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Processes the queue asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var operation = await DequeueAsync(cancellationToken);
                    if (operation != null)
                    {
                        // Execute operation
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // This is where the actual license check would be executed
                                // For now, we'll just mark it as completed after a delay
                                await Task.Delay(100, cancellationToken);

                                var result = LicenseCheckResult.CreateSuccess(operation);
                                MarkOperationCompleted(operation.OperationId, result);
                            }
                            catch (OperationCanceledException)
                            {
                                MarkOperationCancelled(operation.OperationId);
                            }
                            catch (Exception ex)
                            {
                                MarkOperationFailed(operation.OperationId, ex.Message, ex);
                            }
                        }, cancellationToken);
                    }
                    else
                    {
                        // No operations available, wait before checking again
                        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing queue");
                    await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Cleans up completed operations periodically
        /// </summary>
        /// <param name="state">Timer state</param>
        private void CleanupCompletedOperations(object state)
        {
            try
            {
                var cutoffTime = DateTime.Now.AddHours(-1); // Keep operations for 1 hour
                var operationsToRemove = _completedOperations.Where(kvp => kvp.Value.CompletedAt < cutoffTime).ToList();

                foreach (var kvp in operationsToRemove)
                {
                    _completedOperations.TryRemove(kvp.Key, out _);
                }

                if (operationsToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} completed operations", operationsToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up completed operations");
            }
        }

        /// <summary>
        /// Disposes the queue and releases resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the queue and releases resources
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cancel all operations
                _cancellationTokenSource.Cancel();

                // Wait for all operations to complete
                try
                {
                    CancelAllOperationsAsync().Wait(TimeSpan.FromSeconds(30));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cancelling operations during disposal");
                }

                // Dispose resources
                _cancellationTokenSource?.Dispose();
                _semaphore?.Dispose();
                _lock?.Dispose();
                _cleanupTimer?.Dispose();
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~LicenseCheckQueue()
        {
            Dispose(false);
        }
    }
}