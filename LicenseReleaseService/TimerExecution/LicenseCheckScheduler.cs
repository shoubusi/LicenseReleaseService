using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Implements the license check scheduler that integrates with the timer framework for periodic license checks
    /// </summary>
    public class LicenseCheckScheduler : ILicenseCheckScheduler, IDisposable
    {
        private readonly ITimerExecutionService _timerExecutionService;
        private readonly ILogger<LicenseCheckScheduler> _logger;
        private readonly LicenseCheckSchedulerConfiguration _configuration;
        private readonly LicenseCheckQueue _queue;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _operationCancellationTokens;
        private readonly ConcurrentQueue<LicenseCheckEventArgs> _executionHistory;
        private readonly ReaderWriterLockSlim _lock;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly System.Threading.Timer _cleanupTimer;

        private LicenseCheckSchedulerStatus _status;
        private TimeSpan _currentInterval;
        private DateTime _startTime;
        private long _totalOperationsCompleted;
        private long _totalOperationsFailed;
        private long _totalConsecutiveErrors;

        #region Events

        /// <summary>
        /// Event raised when license check execution starts
        /// </summary>
        public event EventHandler<LicenseCheckEventArgs> CheckStarted;

        /// <summary>
        /// Event raised when license check execution completes
        /// </summary>
        public event EventHandler<LicenseCheckEventArgs> CheckCompleted;

        /// <summary>
        /// Event raised when license check encounters an error
        /// </summary>
        public event EventHandler<LicenseCheckErrorEventArgs> CheckError;

        /// <summary>
        /// Event raised when license check queue status changes
        /// </summary>
        public event EventHandler<LicenseCheckQueueEventArgs> QueueStatusChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current status of the license check scheduler
        /// </summary>
        public LicenseCheckSchedulerStatus Status => _status;

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently running
        /// </summary>
        public bool IsRunning => _status == LicenseCheckSchedulerStatus.Running;

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently executing a license check
        /// </summary>
        public bool IsExecuting => _queue.ExecutingCount > 0;

        /// <summary>
        /// Gets the current check interval
        /// </summary>
        public TimeSpan CurrentInterval => _currentInterval;

        /// <summary>
        /// Gets the number of pending operations in the queue
        /// </summary>
        public int PendingOperationsCount => _queue.QueueSize;

        /// <summary>
        /// Gets the license check queue statistics
        /// </summary>
        public LicenseCheckQueueStatistics QueueStatistics => _queue.GetStatistics();

        /// <summary>
        /// Gets the scheduler performance metrics
        /// </summary>
        public LicenseCheckSchedulerMetrics Metrics => GetMetrics();

        /// <summary>
        /// Gets the total number of operations completed
        /// </summary>
        public long TotalOperationsCompleted => Interlocked.Read(ref _totalOperationsCompleted);

        #endregion

        /// <summary>
        /// Initializes a new instance of the LicenseCheckScheduler class
        /// </summary>
        /// <param name="timerExecutionService">Timer execution service</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="configuration">Scheduler configuration</param>
        public LicenseCheckScheduler(
            ITimerExecutionService timerExecutionService,
            ILogger<LicenseCheckScheduler> logger,
            LicenseCheckSchedulerConfiguration configuration)
        {
            _timerExecutionService = timerExecutionService ?? throw new ArgumentNullException(nameof(timerExecutionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _queue = new LicenseCheckQueue();
            _operationCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
            _executionHistory = new ConcurrentQueue<LicenseCheckEventArgs>();
            _lock = new ReaderWriterLockSlim();
            _cancellationTokenSource = new CancellationTokenSource();
            _cleanupTimer = new Timer(CleanupExecutionHistory, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _status = LicenseCheckSchedulerStatus.Created;
            _currentInterval = _configuration.DefaultInterval;
            _startTime = DateTime.Now;

            // Subscribe to timer events
            _timerExecutionService.ExecutionStarted += OnTimerExecutionStarted;
            _timerExecutionService.ExecutionCompleted += OnTimerExecutionCompleted;
            _timerExecutionService.ExecutionError += OnTimerExecutionError;

            // Subscribe to queue events
            _queue.OperationStarted += OnQueueOperationStarted;
            _queue.OperationCompleted += OnQueueOperationCompleted;
            _queue.OperationFailed += OnQueueOperationFailed;
            _queue.QueueStatusChanged += OnQueueStatusChanged;

            _logger.LogDebug("LicenseCheckScheduler initialized with interval {Interval}", _currentInterval);
        }

        #region Lifecycle Methods

        /// <summary>
        /// Starts the license check scheduler with the specified interval
        /// </summary>
        /// <param name="interval">The interval between license checks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        public async Task StartAsync(TimeSpan interval, CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                _logger.LogWarning("License check scheduler is already running");
                return;
            }

            await SetStatusAsync(LicenseCheckSchedulerStatus.Starting, "Starting scheduler");

            try
            {
                _currentInterval = interval;
                await _timerExecutionService.StartAsync(interval, cancellationToken);

                await SetStatusAsync(LicenseCheckSchedulerStatus.Running, "Scheduler started successfully");
                _logger.LogInformation("License check scheduler started with interval {Interval}", interval);
            }
            catch (Exception ex)
            {
                await SetStatusAsync(LicenseCheckSchedulerStatus.Faulted, $"Failed to start scheduler: {ex.Message}");
                _logger.LogError(ex, "Failed to start license check scheduler");
                throw;
            }
        }

        /// <summary>
        /// Stops the license check scheduler
        /// </summary>
        /// <returns>Task representing the stop operation</returns>
        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                _logger.LogWarning("License check scheduler is not running");
                return;
            }

            await SetStatusAsync(LicenseCheckSchedulerStatus.Stopping, "Stopping scheduler");

            try
            {
                // Cancel all operations
                await _queue.CancelAllOperationsAsync();

                // Stop the timer
                await _timerExecutionService.StopAsync();

                await SetStatusAsync(LicenseCheckSchedulerStatus.Stopped, "Scheduler stopped successfully");
                _logger.LogInformation("License check scheduler stopped");
            }
            catch (Exception ex)
            {
                await SetStatusAsync(LicenseCheckSchedulerStatus.Faulted, $"Failed to stop scheduler: {ex.Message}");
                _logger.LogError(ex, "Failed to stop license check scheduler");
                throw;
            }
        }

        /// <summary>
        /// Pauses the license check scheduler
        /// </summary>
        /// <returns>Task representing the pause operation</returns>
        public async Task PauseAsync()
        {
            if (!IsRunning)
            {
                _logger.LogWarning("License check scheduler is not running");
                return;
            }

            await SetStatusAsync(LicenseCheckSchedulerStatus.Paused, "Scheduler paused");
            _logger.LogInformation("License check scheduler paused");
        }

        /// <summary>
        /// Resumes the license check scheduler
        /// </summary>
        /// <returns>Task representing the resume operation</returns>
        public async Task ResumeAsync()
        {
            if (_status != LicenseCheckSchedulerStatus.Paused)
            {
                _logger.LogWarning("License check scheduler is not paused");
                return;
            }

            await SetStatusAsync(LicenseCheckSchedulerStatus.Running, "Scheduler resumed");
            _logger.LogInformation("License check scheduler resumed");
        }

        /// <summary>
        /// Updates the check interval while running
        /// </summary>
        /// <param name="newInterval">The new interval between license checks</param>
        /// <returns>Task representing the interval update operation</returns>
        public async Task UpdateIntervalAsync(TimeSpan newInterval)
        {
            if (!IsRunning)
            {
                _logger.LogWarning("License check scheduler is not running");
                return;
            }

            if (newInterval <= TimeSpan.Zero)
            {
                throw new ArgumentException("Interval must be greater than zero", nameof(newInterval));
            }

            try
            {
                _currentInterval = newInterval;
                // Note: TimerExecutionService might need UpdateInterval method
                _logger.LogInformation("License check scheduler interval updated to {Interval}", newInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update scheduler interval");
                throw;
            }
        }

        /// <summary>
        /// Executes license check immediately
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the immediate execution operation</returns>
        public async Task ExecuteNowAsync(CancellationToken cancellationToken = default)
        {
            var operation = LicenseCheckOperationFactory.CreateComprehensiveCheck(
                _configuration.DefaultServer,
                _configuration.DefaultPort);

            await EnqueueOperationAsync(operation, LicenseCheckPriority.High);
        }

        #endregion

        #region Queue Management Methods

        /// <summary>
        /// Adds a license check operation to the queue
        /// </summary>
        /// <param name="operation">The license check operation to add</param>
        /// <param name="priority">The priority of the operation</param>
        /// <returns>Task representing the queue operation</returns>
        public async Task EnqueueOperationAsync(LicenseCheckOperation operation, LicenseCheckPriority priority = LicenseCheckPriority.Normal)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await _queue.EnqueueAsync(operation, priority);
            _logger.LogDebug("Enqueued operation {OperationId} with priority {Priority}", operation.OperationId, priority);
        }

        /// <summary>
        /// Adds multiple license check operations to the queue
        /// </summary>
        /// <param name="operations">The license check operations to add</param>
        /// <param name="priority">The priority of the operations</param>
        /// <returns>Task representing the batch queue operation</returns>
        public async Task EnqueueOperationsAsync(IEnumerable<LicenseCheckOperation> operations, LicenseCheckPriority priority = LicenseCheckPriority.Normal)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            foreach (var operation in operations)
            {
                await EnqueueOperationAsync(operation, priority);
            }
        }

        /// <summary>
        /// Removes a license check operation from the queue
        /// </summary>
        /// <param name="operationId">The ID of the operation to remove</param>
        /// <returns>Task representing the dequeue operation</returns>
        public async Task<bool> DequeueOperationAsync(string operationId)
        {
            var result = await _queue.RemoveAsync(operationId);
            if (result)
            {
                _logger.LogDebug("Dequeued operation {OperationId}", operationId);
            }
            return result;
        }

        /// <summary>
        /// Clears all pending operations from the queue
        /// </summary>
        /// <returns>Task representing the clear operation</returns>
        public async Task ClearQueueAsync()
        {
            await _queue.ClearAsync();
            _logger.LogDebug("Cleared all pending operations");
        }

        /// <summary>
        /// Gets all pending operations in the queue
        /// </summary>
        /// <returns>List of pending operations</returns>
        public async Task<IReadOnlyList<LicenseCheckOperation>> GetPendingOperationsAsync()
        {
            return await _queue.GetPendingOperationsAsync();
        }

        #endregion

        #region Configuration Methods

        /// <summary>
        /// Updates the license check scheduler configuration
        /// </summary>
        /// <param name="configuration">The new configuration</param>
        /// <returns>Task representing the configuration update operation</returns>
        public async Task UpdateConfigurationAsync(LicenseCheckSchedulerConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _configuration.DefaultServer = configuration.DefaultServer ?? _configuration.DefaultServer;
            _configuration.DefaultPort = configuration.DefaultPort > 0 ? configuration.DefaultPort : _configuration.DefaultPort;
            _configuration.DefaultInterval = configuration.DefaultInterval > TimeSpan.Zero ? configuration.DefaultInterval : _configuration.DefaultInterval;

            await _queue.UpdateConfigurationAsync(configuration.QueueConfiguration);
            _logger.LogDebug("Updated scheduler configuration");
        }

        /// <summary>
        /// Validates the current scheduler configuration
        /// </summary>
        /// <returns>List of validation errors, if any</returns>
        public IList<string> ValidateConfiguration()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_configuration.DefaultServer))
            {
                errors.Add("Default server address is required");
            }

            if (_configuration.DefaultPort <= 0 || _configuration.DefaultPort > 65535)
            {
                errors.Add("Default port must be between 1 and 65535");
            }

            if (_configuration.DefaultInterval <= TimeSpan.Zero)
            {
                errors.Add("Default interval must be greater than zero");
            }

            return errors;
        }

        #endregion

        #region Information Methods

        /// <summary>
        /// Gets detailed scheduler status information
        /// </summary>
        /// <returns>Detailed status information</returns>
        public async Task<LicenseCheckSchedulerStatusInfo> GetStatusInfoAsync()
        {
            var queueStatus = await _queue.GetStatusAsync();
            var uptime = DateTime.Now - _startTime;

            return new LicenseCheckSchedulerStatusInfo
            {
                Status = _status,
                IsRunning = IsRunning,
                IsExecuting = IsExecuting,
                CurrentInterval = _currentInterval,
                Uptime = uptime,
                TotalOperationsCompleted = Interlocked.Read(ref _totalOperationsCompleted),
                TotalOperationsFailed = Interlocked.Read(ref _totalOperationsFailed),
                PendingOperationsCount = PendingOperationsCount,
                QueueStatistics = QueueStatistics,
                LastUpdated = DateTime.Now
            };
        }

        /// <summary>
        /// Gets the license check execution history
        /// </summary>
        /// <param name="maxItems">Maximum number of items to return</param>
        /// <returns>List of recent license check events</returns>
        public async Task<IReadOnlyList<LicenseCheckEventArgs>> GetExecutionHistoryAsync(int maxItems = 10)
        {
            return _executionHistory.Take(maxItems).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets diagnostic information for troubleshooting
        /// </summary>
        /// <returns>Diagnostic information</returns>
        public async Task<LicenseCheckSchedulerDiagnostics> GetDiagnosticsAsync()
        {
            var statusInfo = await GetStatusInfoAsync();
            var validationErrors = ValidateConfiguration();

            return new LicenseCheckSchedulerDiagnostics
            {
                StatusInfo = statusInfo,
                ConfigurationErrors = validationErrors,
                IsHealthy = validationErrors.Count == 0 && _status != LicenseCheckSchedulerStatus.Faulted,
                MemoryUsage = GC.GetTotalMemory(false),
                ThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
                LastDiagnosticsCheck = DateTime.Now
            };
        }

        /// <summary>
        /// Resets the scheduler statistics and error count
        /// </summary>
        /// <returns>Task representing the reset operation</returns>
        public async Task ResetStatisticsAsync()
        {
            Interlocked.Exchange(ref _totalOperationsCompleted, 0);
            Interlocked.Exchange(ref _totalOperationsFailed, 0);
            Interlocked.Exchange(ref _totalConsecutiveErrors, 0);
            _startTime = DateTime.Now;

            await _queue.ResetStatisticsAsync();
            _executionHistory.Clear();

            _logger.LogDebug("Scheduler statistics reset");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles timer execution started events
        /// </summary>
        private async void OnTimerExecutionStarted(object sender, TimerExecutionEventArgs e)
        {
            if (!IsRunning)
                return;

            _logger.LogDebug("Timer execution started: {ExecutionId}", e.ExecutionId);

            // Execute default license check
            var operation = LicenseCheckOperationFactory.CreateComprehensiveCheck(
                _configuration.DefaultServer,
                _configuration.DefaultPort);

            await EnqueueOperationAsync(operation, LicenseCheckPriority.Normal);
        }

        /// <summary>
        /// Handles timer execution completed events
        /// </summary>
        private void OnTimerExecutionCompleted(object sender, TimerExecutionEventArgs e)
        {
            _logger.LogDebug("Timer execution completed: {ExecutionId}, Duration: {Duration}ms",
                e.ExecutionId, e.Duration.TotalMilliseconds);

            // Reset consecutive error counter on successful execution
            Interlocked.Exchange(ref _totalConsecutiveErrors, 0);
        }

        /// <summary>
        /// Handles timer execution error events
        /// </summary>
        private async void OnTimerExecutionError(object sender, TimerExecutionErrorEventArgs e)
        {
            _logger.LogError(e.Error, "Timer execution error: {ExecutionId}", e.ExecutionId);

            Interlocked.Increment(ref _totalConsecutiveErrors);
            Interlocked.Increment(ref _totalOperationsFailed);

            // Check if we need to trigger circuit breaker
            if (_totalConsecutiveErrors >= _configuration.MaxConsecutiveErrors)
            {
                await SetStatusAsync(LicenseCheckSchedulerStatus.Faulted,
                    $"Too many consecutive errors: {_totalConsecutiveErrors}");
            }

            // Raise error event
            var errorArgs = new LicenseCheckErrorEventArgs(
                $"Timer execution error: {e.Error.Message}",
                e.Error,
                DateTime.Now);
            CheckError?.Invoke(this, errorArgs);
        }

        /// <summary>
        /// Handles queue operation started events
        /// </summary>
        private void OnQueueOperationStarted(object sender, LicenseCheckEventArgs e)
        {
            var eventArgs = new LicenseCheckEventArgs(
                e.OperationId,
                e.OperationType,
                e.TargetVersion,
                e.Server,
                e.Port,
                LicenseCheckEventType.Started,
                null, // result
                null, // additionalData
                null, // errorMessage
                null, // exception
                e.InitiatingUser,
                e.ComputerName);

            CheckStarted?.Invoke(this, eventArgs);
            _executionHistory.Enqueue(eventArgs);
        }

        /// <summary>
        /// Handles queue operation completed events
        /// </summary>
        private void OnQueueOperationCompleted(object sender, LicenseCheckEventArgs e)
        {
            Interlocked.Increment(ref _totalOperationsCompleted);

            var eventArgs = new LicenseCheckEventArgs(
                operationId: e.OperationId,
                operationType: e.OperationType,
                targetVersion: e.TargetVersion,
                licenseServer: e.Server,
                port: e.Port,
                eventType: LicenseCheckEventType.Completed,
                initiatingUser: null,
                computerName: null);

            CheckCompleted?.Invoke(this, eventArgs);
            _executionHistory.Enqueue(eventArgs);
        }

        /// <summary>
        /// Handles queue operation failed events
        /// </summary>
        private void OnQueueOperationFailed(object sender, LicenseCheckEventArgs e)
        {
            Interlocked.Increment(ref _totalOperationsFailed);

            var eventArgs = new LicenseCheckEventArgs(
                e.OperationId,
                e.OperationType,
                e.TargetVersion,
                e.Server,
                e.Port,
                LicenseCheckEventType.Error,
                null,
                null,
                e.ErrorMessage,
                e.Exception);

            var errorArgs = new LicenseCheckErrorEventArgs(
                e.ErrorMessage ?? "Operation failed",
                e.Exception,
                DateTime.Now);
            CheckError?.Invoke(this, errorArgs);
            _executionHistory.Enqueue(eventArgs);
        }

        /// <summary>
        /// Handles queue status changed events
        /// </summary>
        private void OnQueueStatusChanged(object sender, LicenseCheckQueueEventArgs e)
        {
            QueueStatusChanged?.Invoke(this, e);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Sets the scheduler status
        /// </summary>
        private async Task SetStatusAsync(LicenseCheckSchedulerStatus newStatus, string reason)
        {
            _status = newStatus;
            _logger.LogDebug("Scheduler status changed to {NewStatus}: {Reason}", newStatus, reason);
        }

        /// <summary>
        /// Cleans up old execution history entries
        /// </summary>
        private void CleanupExecutionHistory(object state)
        {
            try
            {
                const int maxHistoryItems = 100;
                while (_executionHistory.Count > maxHistoryItems && _executionHistory.TryDequeue(out _))
                {
                    // Remove old entries
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up execution history");
            }
        }

        /// <summary>
        /// Gets scheduler performance metrics
        /// </summary>
        private LicenseCheckSchedulerMetrics GetMetrics()
        {
            var uptime = DateTime.Now - _startTime;
            var queueStats = QueueStatistics;

            return new LicenseCheckSchedulerMetrics
            {
                StartTime = _startTime,
                Uptime = uptime,
                TotalOperations = queueStats.TotalProcessed,
                SuccessfulOperations = queueStats.CompletedCount,
                FailedOperations = queueStats.FailedCount,
                CancelledOperations = queueStats.CancelledCount,
                TimedOutOperations = queueStats.TimedOutCount,
                AverageExecutionTime = queueStats.AverageExecutionTime,
                OperationsPerSecond = uptime.TotalSeconds > 0 ? (double)queueStats.TotalProcessed / uptime.TotalSeconds : 0,
                QueueSize = PendingOperationsCount,
                IsRunning = IsRunning,
                IsHealthy = _status != LicenseCheckSchedulerStatus.Faulted,
                ConsecutiveErrors = (int)_totalConsecutiveErrors,
                Timestamp = DateTime.Now
            };
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the scheduler and releases resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the scheduler and releases resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cancel all operations
                _cancellationTokenSource.Cancel();

                // Stop the timer
                _timerExecutionService.StopAsync().Wait();

                // Dispose the queue
                _queue?.Dispose();

                // Dispose the cleanup timer
                _cleanupTimer?.Dispose();

                // Dispose all cancellation tokens
                foreach (var cts in _operationCancellationTokens.Values)
                {
                    cts.Dispose();
                }

                // Unsubscribe from events
                _timerExecutionService.ExecutionStarted -= OnTimerExecutionStarted;
                _timerExecutionService.ExecutionCompleted -= OnTimerExecutionCompleted;
                _timerExecutionService.ExecutionError -= OnTimerExecutionError;

                _queue.OperationStarted -= OnQueueOperationStarted;
                _queue.OperationCompleted -= OnQueueOperationCompleted;
                _queue.OperationFailed -= OnQueueOperationFailed;
                _queue.QueueStatusChanged -= OnQueueStatusChanged;

                // Dispose resources
                _cancellationTokenSource?.Dispose();
                _lock?.Dispose();
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~LicenseCheckScheduler()
        {
            Dispose(false);
        }

        #endregion
    }
}