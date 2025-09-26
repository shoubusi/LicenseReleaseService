using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Manages pending license check operations with priority-based processing
    /// </summary>
    public class LicenseCheckQueue : IDisposable
    {
        #region Private Fields

        private readonly ConcurrentDictionary<string, QueuedOperation> _operations;
        private readonly ConcurrentQueue<QueuedOperation> _priorityQueue;
        private readonly ConcurrentQueue<QueuedOperation> _normalQueue;
        private readonly ConcurrentQueue<QueuedOperation> _lowQueue;
        private readonly ConcurrentDictionary<string, DateTime> _processingOperations;
        private readonly ReaderWriterLockSlim _statisticsLock;
        private readonly Timer _cleanupTimer;
        private readonly Timer _queueProcessingTimer;
        private readonly LicenseCheckQueueStatistics _statistics;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _queueProcessingInterval;
        private readonly int _maxQueueSize;
        private readonly bool _enablePrioritization;
        private readonly double _queueCapacityWarningThreshold;
        private bool _isProcessing;
        private bool _isDisposed;
        private DateTime _lastQueueProcessTime;

        #endregion

        #region Events

        /// <summary>
        /// Event raised when an operation is enqueued
        /// </summary>
        public event EventHandler<LicenseCheckQueueEventArgs> OperationEnqueued;

        /// <summary>
        /// Event raised when an operation is dequeued
        /// </summary>
        public event EventHandler<LicenseCheckQueueEventArgs> OperationDequeued;

        /// <summary>
        /// Event raised when the queue is cleared
        /// </summary>
        public event EventHandler<LicenseCheckQueueEventArgs> QueueCleared;

        /// <summary>
        /// Event raised when queue approaches capacity
        /// </summary>
        public event EventHandler<LicenseCheckQueueEventArgs> QueueCapacityWarning;

        /// <summary>
        /// Event raised when queue reaches capacity
        /// </summary>
        public event EventHandler<LicenseCheckQueueEventArgs> QueueCapacityReached;

        /// <summary>
        /// Event raised when queue processing starts
        /// </summary>
        public event EventHandler<LicenseCheckQueueEventArgs> QueueProcessingStarted;

        /// <summary>
        /// Event raised when queue processing completes
        /// </summary>
        public event EventHandler<LicenseCheckQueueEventArgs> QueueProcessingCompleted;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current number of operations in the queue
        /// </summary>
        public int Count => _operations.Count;

        /// <summary>
        /// Gets the maximum queue capacity
        /// </summary>
        public int MaxCapacity { get; }

        /// <summary>
        /// Gets the queue utilization percentage
        /// </summary>
        public double UtilizationPercentage
        {
            get
            {
                if (MaxCapacity == 0)
                    return 0.0;

                return (double)Count / MaxCapacity * 100;
            }
        }

        /// <summary>
        /// Gets the queue statistics
        /// </summary>
        public LicenseCheckQueueStatistics Statistics => CloneStatistics();

        /// <summary>
        /// Gets a value indicating whether the queue is empty
        /// </summary>
        public bool IsEmpty => Count == 0;

        /// <summary>
        /// Gets a value indicating whether the queue is full
        /// </summary>
        public bool IsFull => Count >= MaxCapacity;

        /// <summary>
        /// Gets a value indicating whether the queue is processing
        /// </summary>
        public bool IsProcessing => _isProcessing;

        #endregion

        /// <summary>
        /// Initializes a new instance of the LicenseCheckQueue class
        /// </summary>
        /// <param name="maxQueueSize">The maximum queue size</param>
        /// <param name="enablePrioritization">Whether to enable prioritization</param>
        /// <param name="queueCapacityWarningThreshold">The queue capacity warning threshold (0.0 to 1.0)</param>
        /// <param name="cleanupInterval">The cleanup interval</param>
        /// <param name="queueProcessingInterval">The queue processing interval</param>
        public LicenseCheckQueue(
            int maxQueueSize = 1000,
            bool enablePrioritization = true,
            double queueCapacityWarningThreshold = 0.8,
            TimeSpan? cleanupInterval = null,
            TimeSpan? queueProcessingInterval = null)
        {
            MaxCapacity = maxQueueSize > 0 ? maxQueueSize : throw new ArgumentException("Max queue size must be greater than 0", nameof(maxQueueSize));
            _maxQueueSize = maxQueueSize;
            _enablePrioritization = enablePrioritization;
            _queueCapacityWarningThreshold = Math.Clamp(queueCapacityWarningThreshold, 0.0, 1.0);
            _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(5);
            _queueProcessingInterval = queueProcessingInterval ?? TimeSpan.FromSeconds(30);

            _operations = new ConcurrentDictionary<string, QueuedOperation>();
            _priorityQueue = new ConcurrentQueue<QueuedOperation>();
            _normalQueue = new ConcurrentQueue<QueuedOperation>();
            _lowQueue = new ConcurrentQueue<QueuedOperation>();
            _processingOperations = new ConcurrentDictionary<string, DateTime>();
            _statisticsLock = new ReaderWriterLockSlim();
            _statistics = new LicenseCheckQueueStatistics();
            _lastQueueProcessTime = DateTime.UtcNow;

            // Initialize cleanup timer
            _cleanupTimer = new Timer(CleanupCallback, null, _cleanupInterval, _cleanupInterval);

            // Initialize queue processing timer
            _queueProcessingTimer = new Timer(QueueProcessingCallback, null, _queueProcessingInterval, _queueProcessingInterval);
        }

        /// <summary>
        /// Adds a license check operation to the queue
        /// </summary>
        /// <param name="operation">The license check operation to add</param>
        /// <param name="priority">The priority of the operation</param>
        /// <returns>Task representing the enqueue operation</returns>
        public async Task<bool> EnqueueAsync(LicenseCheckOperation operation, LicenseCheckPriority priority = LicenseCheckPriority.Normal)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (IsFull)
            {
                await RaiseQueueEventAsync(QueueCapacityReached, operation);
                return false;
            }

            var queuedOperation = new QueuedOperation
            {
                Operation = operation,
                Priority = priority,
                QueuedAt = DateTime.UtcNow,
                QueueId = Guid.NewGuid().ToString("N")[..8]
            };

            // Add to the operations dictionary
            if (!_operations.TryAdd(queuedOperation.QueueId, queuedOperation))
            {
                return false;
            }

            // Add to the appropriate priority queue
            switch (priority)
            {
                case LicenseCheckPriority.Critical:
                case LicenseCheckPriority.High:
                    _priorityQueue.Enqueue(queuedOperation);
                    break;
                case LicenseCheckPriority.Normal:
                    _normalQueue.Enqueue(queuedOperation);
                    break;
                case LicenseCheckPriority.Low:
                    _lowQueue.Enqueue(queuedOperation);
                    break;
            }

            // Update statistics
            UpdateStatistics(op => op.TotalOperationsProcessed++);
            UpdateStatistics(op => op.OperationsByPriority[priority] = (op.OperationsByPriority.TryGetValue(priority, out var count) ? count : 0) + 1);
            UpdateStatistics(op => op.OperationsByType[operation.OperationType] = (op.OperationsByType.TryGetValue(operation.OperationType, out var typeCount) ? typeCount : 0) + 1);

            // Check for capacity warning
            if (UtilizationPercentage >= _queueCapacityWarningThreshold)
            {
                await RaiseQueueEventAsync(QueueCapacityWarning, operation);
            }

            // Raise enqueue event
            await RaiseQueueEventAsync(OperationEnqueued, operation);

            return true;
        }

        /// <summary>
        /// Adds multiple license check operations to the queue
        /// </summary>
        /// <param name="operations">The license check operations to add</param>
        /// <param name="priority">The priority of the operations</param>
        /// <returns>Task representing the batch enqueue operation</returns>
        public async Task<int> EnqueueRangeAsync(IEnumerable<LicenseCheckOperation> operations, LicenseCheckPriority priority = LicenseCheckPriority.Normal)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            var operationList = operations.ToList();
            var successCount = 0;

            foreach (var operation in operationList)
            {
                if (await EnqueueAsync(operation, priority))
                {
                    successCount++;
                }
                else
                {
                    break; // Stop if queue becomes full
                }
            }

            return successCount;
        }

        /// <summary>
        /// Gets the next operation from the queue
        /// </summary>
        /// <returns>The next operation, or null if queue is empty</returns>
        public async Task<LicenseCheckOperation> DequeueAsync()
        {
            if (IsEmpty)
                return null;

            QueuedOperation queuedOperation = null;

            // Try to dequeue from priority queues in order
            if (_enablePrioritization)
            {
                _priorityQueue.TryDequeue(out queuedOperation) ||
                _normalQueue.TryDequeue(out queuedOperation) ||
                _lowQueue.TryDequeue(out queuedOperation);
            }
            else
            {
                // If prioritization is disabled, process from normal queue only
                _normalQueue.TryDequeue(out queuedOperation);
            }

            if (queuedOperation == null)
                return null;

            // Remove from operations dictionary
            _operations.TryRemove(queuedOperation.QueueId, out _);

            // Add to processing operations
            _processingOperations.TryAdd(queuedOperation.QueueId, DateTime.UtcNow);

            // Calculate queue wait time
            var waitTime = DateTime.UtcNow - queuedOperation.QueuedAt;

            // Update statistics
            UpdateStatistics(op =>
            {
                op.CurrentQueueSize = Count;
                op.MaxQueueSizeObserved = Math.Max(op.MaxQueueSizeObserved, Count);

                // Update average wait time
                if (op.TotalOperationsProcessed > 0)
                {
                    op.AverageQueueWaitTime = TimeSpan.FromMilliseconds(
                        (op.AverageQueueWaitTime.TotalMilliseconds * (op.TotalOperationsProcessed - 1) + waitTime.TotalMilliseconds) / op.TotalOperationsProcessed);
                }
                else
                {
                    op.AverageQueueWaitTime = waitTime;
                }

                op.MaxQueueWaitTime = TimeSpan.FromMilliseconds(Math.Max(op.MaxQueueWaitTime.TotalMilliseconds, waitTime.TotalMilliseconds));
            });

            // Raise dequeue event
            await RaiseQueueEventAsync(OperationDequeued, queuedOperation.Operation);

            return queuedOperation.Operation;
        }

        /// <summary>
        /// Removes a specific operation from the queue
        /// </summary>
        /// <param name="operationId">The ID of the operation to remove</param>
        /// <returns>True if the operation was found and removed</returns>
        public async Task<bool> RemoveAsync(string operationId)
        {
            if (string.IsNullOrEmpty(operationId))
                return false;

            var queuedOperation = _operations.Values.FirstOrDefault(op => op.Operation.OperationId == operationId);
            if (queuedOperation == null)
                return false;

            // Remove from operations dictionary
            if (!_operations.TryRemove(queuedOperation.QueueId, out _))
                return false;

            // Remove from appropriate queue
            var removed = false;
            switch (queuedOperation.Priority)
            {
                case LicenseCheckPriority.Critical:
                case LicenseCheckPriority.High:
                    removed = RemoveFromQueue(_priorityQueue, queuedOperation);
                    break;
                case LicenseCheckPriority.Normal:
                    removed = RemoveFromQueue(_normalQueue, queuedOperation);
                    break;
                case LicenseCheckPriority.Low:
                    removed = RemoveFromQueue(_lowQueue, queuedOperation);
                    break;
            }

            if (removed)
            {
                await RaiseQueueEventAsync(OperationDequeued, queuedOperation.Operation);
            }

            return removed;
        }

        /// <summary>
        /// Clears all operations from the queue
        /// </summary>
        /// <returns>Task representing the clear operation</returns>
        public async Task ClearAsync()
        {
            // Clear all queues
            while (_priorityQueue.TryDequeue(out _)) { }
            while (_normalQueue.TryDequeue(out _)) { }
            while (_lowQueue.TryDequeue(out _)) { }

            // Clear operations dictionary
            _operations.Clear();

            // Update statistics
            UpdateStatistics(op => op.CurrentQueueSize = 0);

            // Raise clear event
            await RaiseQueueEventAsync(QueueCleared, null);
        }

        /// <summary>
        /// Gets all operations currently in the queue
        /// </summary>
        /// <returns>List of queued operations</returns>
        public IReadOnlyList<LicenseCheckOperation> GetAllOperations()
        {
            return _operations.Values.Select(qo => qo.Operation).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets operations by priority
        /// </summary>
        /// <param name="priority">The priority to filter by</param>
        /// <returns>List of operations with the specified priority</returns>
        public IReadOnlyList<LicenseCheckOperation> GetOperationsByPriority(LicenseCheckPriority priority)
        {
            return _operations.Values
                .Where(qo => qo.Priority == priority)
                .Select(qo => qo.Operation)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Marks an operation as completed (removes from processing operations)
        /// </summary>
        /// <param name="operationId">The ID of the completed operation</param>
        public void MarkOperationCompleted(string operationId)
        {
            if (!string.IsNullOrEmpty(operationId))
            {
                _processingOperations.TryRemove(operationId, out _);
            }
        }

        /// <summary>
        /// Updates queue statistics with operation result
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        /// <param name="result">The operation result</param>
        public void UpdateOperationResult(string operationId, LicenseCheckResult result)
        {
            if (string.IsNullOrEmpty(operationId) || result == null)
                return;

            UpdateStatistics(op =>
            {
                if (result.IsSuccess)
                {
                    op.SuccessfulOperations++;
                }
                else if (result.IsFailed)
                {
                    op.FailedOperations++;
                }
                else if (result.IsCancelled)
                {
                    op.CancelledOperations++;
                }
                else if (result.IsTimeout)
                {
                    op.TimedOutOperations++;
                }

                // Update execution time statistics
                if (result.Duration.TotalMilliseconds > 0)
                {
                    if (op.TotalOperationsProcessed > 0)
                    {
                        op.AverageExecutionTime = TimeSpan.FromMilliseconds(
                            (op.AverageExecutionTime.TotalMilliseconds * (op.TotalOperationsProcessed - 1) + result.Duration.TotalMilliseconds) / op.TotalOperationsProcessed);
                    }
                    else
                    {
                        op.AverageExecutionTime = result.Duration;
                    }

                    op.TotalExecutionTime += result.Duration;
                }
            });

            MarkOperationCompleted(operationId);
        }

        /// <summary>
        /// Updates queue statistics for retried operations
        /// </summary>
        /// <param name="operationId">The operation ID</param>
        public void UpdateOperationRetried(string operationId)
        {
            if (!string.IsNullOrEmpty(operationId))
            {
                UpdateStatistics(op => op.RetriedOperations++);
            }
        }

        /// <summary>
        /// Disposes the queue and cleans up resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // Dispose timers
            _cleanupTimer?.Dispose();
            _queueProcessingTimer?.Dispose();

            // Clear all queues
            ClearAsync().GetAwaiter().GetResult();

            // Dispose lock
            _statisticsLock?.Dispose();
        }

        #region Private Methods

        private bool RemoveFromQueue(ConcurrentQueue<QueuedOperation> queue, QueuedOperation operation)
        {
            // This is a bit tricky with ConcurrentQueue - we need to dequeue until we find the right one
            var tempQueue = new ConcurrentQueue<QueuedOperation>();
            var found = false;

            while (queue.TryDequeue(out var current))
            {
                if (current.QueueId == operation.QueueId)
                {
                    found = true;
                }
                else
                {
                    tempQueue.Enqueue(current);
                }
            }

            // Re-enqueue the operations we didn't want to remove
            while (tempQueue.TryDequeue(out var current))
            {
                queue.Enqueue(current);
            }

            return found;
        }

        private void UpdateStatistics(Action<LicenseCheckQueueStatistics> updateAction)
        {
            _statisticsLock.EnterWriteLock();
            try
            {
                updateAction(_statistics);
                _statistics.LastUpdated = DateTime.UtcNow;
            }
            finally
            {
                _statisticsLock.ExitWriteLock();
            }
        }

        private LicenseCheckQueueStatistics CloneStatistics()
        {
            _statisticsLock.EnterReadLock();
            try
            {
                return new LicenseCheckQueueStatistics
                {
                    TotalOperationsProcessed = _statistics.TotalOperationsProcessed,
                    CurrentQueueSize = Count,
                    MaxQueueSizeObserved = _statistics.MaxQueueSizeObserved,
                    SuccessfulOperations = _statistics.SuccessfulOperations,
                    FailedOperations = _statistics.FailedOperations,
                    CancelledOperations = _statistics.CancelledOperations,
                    RetriedOperations = _statistics.RetriedOperations,
                    TimedOutOperations = _statistics.TimedOutOperations,
                    OperationsByPriority = new Dictionary<LicenseCheckPriority, long>(_statistics.OperationsByPriority).AsReadOnly(),
                    OperationsByType = new Dictionary<LicenseCheckOperationType, long>(_statistics.OperationsByType).AsReadOnly(),
                    AverageQueueWaitTime = _statistics.AverageQueueWaitTime,
                    MaxQueueWaitTime = _statistics.MaxQueueWaitTime,
                    AverageExecutionTime = _statistics.AverageExecutionTime,
                    TotalExecutionTime = _statistics.TotalExecutionTime,
                    LastUpdated = _statistics.LastUpdated
                };
            }
            finally
            {
                _statisticsLock.ExitReadLock();
            }
        }

        private async Task RaiseQueueEventAsync(Func<EventHandler<LicenseCheckQueueEventArgs>, LicenseCheckQueueEventArgs> eventHandler, LicenseCheckOperation operation)
        {
            var eventArgs = new LicenseCheckQueueEventArgs(
                LicenseCheckQueueEventType.OperationEnqueued,
                Count,
                MaxCapacity,
                CloneStatistics(),
                operation);

            eventHandler?.Invoke(this, eventArgs);
        }

        private void CleanupCallback(object state)
        {
            try
            {
                // Clean up stale processing operations
                var staleThreshold = DateTime.UtcNow.AddHours(-1); // Operations older than 1 hour
                var staleOperations = _processingOperations
                    .Where(kvp => kvp.Value < staleThreshold)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var operationId in staleOperations)
                {
                    _processingOperations.TryRemove(operationId, out _);
                }

                // Update statistics
                UpdateStatistics(op => op.CurrentQueueSize = Count);
            }
            catch (Exception ex)
            {
                // Log error but don't throw from timer callback
                System.Diagnostics.Debug.WriteLine($"Error in queue cleanup: {ex.Message}");
            }
        }

        private void QueueProcessingCallback(object state)
        {
            try
            {
                if (!_isProcessing && !IsEmpty)
                {
                    // Start queue processing
                    _isProcessing = true;
                    _lastQueueProcessTime = DateTime.UtcNow;

                    // Fire processing started event
                    var processingStartedArgs = new LicenseCheckQueueEventArgs(
                        LicenseCheckQueueEventType.QueueProcessingStarted,
                        Count,
                        MaxCapacity,
                        CloneStatistics());
                    QueueProcessingStarted?.Invoke(this, processingStartedArgs);

                    // Process operations (this would typically be done asynchronously)
                    // For now, we'll just update the processing state
                    _isProcessing = false;

                    // Fire processing completed event
                    var processingCompletedArgs = new LicenseCheckQueueEventArgs(
                        LicenseCheckQueueEventType.QueueProcessingCompleted,
                        Count,
                        MaxCapacity,
                        CloneStatistics());
                    QueueProcessingCompleted?.Invoke(this, processingCompletedArgs);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw from timer callback
                System.Diagnostics.Debug.WriteLine($"Error in queue processing: {ex.Message}");
                _isProcessing = false;
            }
        }

        #endregion

        #region Nested Classes

        private class QueuedOperation
        {
            public string QueueId { get; set; }
            public LicenseCheckOperation Operation { get; set; }
            public LicenseCheckPriority Priority { get; set; }
            public DateTime QueuedAt { get; set; }
        }

        #endregion
    }
}