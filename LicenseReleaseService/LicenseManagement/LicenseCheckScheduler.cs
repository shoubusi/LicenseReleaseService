using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.TimerExecution;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Implements the license check scheduler that coordinates periodic license monitoring operations
    /// </summary>
    public class LicenseCheckScheduler : ILicenseCheckScheduler, IDisposable
    {
        private readonly ITimerExecutionService _timerExecutionService;
        private readonly ILogger<LicenseCheckScheduler> _logger;
        private readonly ILicenseManager _licenseManager;
        private readonly ILicenseQueryEngine _licenseQueryEngine;
        private readonly TimerExecutionOptions _options;
        private readonly LicenseCheckQueue _queue;
        private readonly ConcurrentDictionary<Guid, LicenseCheckOperation> _recurringOperations;
        private readonly ConcurrentDictionary<Guid, DateTime> _recurringOperationLastExecution;
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _operationCancellationTokens;
        private readonly ReaderWriterLockSlim _lock;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private SchedulerStatus _status;
        private TimeSpan _currentInterval;
        private long _totalOperationsCompleted;
        private long _totalOperationsFailed;
        private DateTime _startTime;
        private DateTime _lastExecutionTime;

        /// <summary>
        /// Gets the current status of the license check scheduler
        /// </summary>
        public SchedulerStatus Status => _status;

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently running
        /// </summary>
        public bool IsRunning => _status == SchedulerStatus.Running;

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently executing operations
        /// </summary>
        public bool IsExecuting => _queue.ExecutingCount > 0;

        /// <summary>
        /// Gets the current scheduler interval
        /// </summary>
        public TimeSpan CurrentInterval => _currentInterval;

        /// <summary>
        /// Gets the number of operations currently in the queue
        /// </summary>
        public int QueuedOperations => _queue.QueueSize;

        /// <summary>
        /// Gets the number of operations currently executing
        /// </summary>
        public int ExecutingOperations => _queue.ExecutingCount;

        /// <summary>
        /// Gets the total number of operations completed
        /// </summary>
        public long TotalOperationsCompleted => Interlocked.Read(ref _totalOperationsCompleted);

        /// <summary>
        /// Gets the total number of operations failed
        /// </summary>
        public long TotalOperationsFailed => Interlocked.Read(ref _totalOperationsFailed);

        /// <summary>
        /// Event raised when license check scheduler starts
        /// </summary>
        public event EventHandler<LicenseCheckSchedulerEventArgs> SchedulerStarted;

        /// <summary>
        /// Event raised when license check scheduler stops
        /// </summary>
        public event EventHandler<LicenseCheckSchedulerEventArgs> SchedulerStopped;

        /// <summary>
        /// Event raised when a license check operation starts
        /// </summary>
        public event EventHandler<LicenseCheckOperationEventArgs> OperationStarted;

        /// <summary>
        /// Event raised when a license check operation completes
        /// </summary>
        public event EventHandler<LicenseCheckOperationEventArgs> OperationCompleted;

        /// <summary>
        /// Event raised when a license check operation fails
        /// </summary>
        public event EventHandler<LicenseCheckOperationEventArgs> OperationFailed;

        /// <summary>
        /// Event raised when scheduler status changes
        /// </summary>
        public event EventHandler<SchedulerStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// Initializes a new instance of the LicenseCheckScheduler class
        /// </summary>
        /// <param name="timerExecutionService">Timer execution service</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="licenseManager">License manager instance</param>
        /// <param name="licenseQueryEngine">License query engine instance</param>
        /// <param name="options">Timer execution options</param>
        public LicenseCheckScheduler(
            ITimerExecutionService timerExecutionService,
            ILogger<LicenseCheckScheduler> logger,
            ILicenseManager licenseManager,
            ILicenseQueryEngine licenseQueryEngine,
            TimerExecutionOptions options)
        {
            _timerExecutionService = timerExecutionService ?? throw new ArgumentNullException(nameof(timerExecutionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _licenseManager = licenseManager ?? throw new ArgumentNullException(nameof(licenseManager));
            _licenseQueryEngine = licenseQueryEngine ?? throw new ArgumentNullException(nameof(licenseQueryEngine));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _queue = new LicenseCheckQueue(_logger, _options);
            _recurringOperations = new ConcurrentDictionary<Guid, LicenseCheckOperation>();
            _recurringOperationLastExecution = new ConcurrentDictionary<Guid, DateTime>();
            _operationCancellationTokens = new ConcurrentDictionary<Guid, CancellationTokenSource>();
            _lock = new ReaderWriterLockSlim();
            _cancellationTokenSource = new CancellationTokenSource();

            _status = SchedulerStatus.Stopped;
            _currentInterval = _options.DefaultInterval;
            _startTime = DateTime.Now;

            // Subscribe to timer events
            _timerExecutionService.ExecutionStarted += OnTimerExecutionStarted;
            _timerExecutionService.ExecutionCompleted += OnTimerExecutionCompleted;
            _timerExecutionService.ExecutionError += OnTimerExecutionError;
            _timerExecutionService.StateChanged += OnTimerStateChanged;

            // Subscribe to queue events
            _queue.OperationStarted += OnQueueOperationStarted;
            _queue.OperationCompleted += OnQueueOperationCompleted;
            _queue.OperationFailed += OnQueueOperationFailed;

            _logger.LogDebug("LicenseCheckScheduler initialized with interval {Interval}", _currentInterval);
        }

        /// <summary>
        /// Starts the license check scheduler with the default interval
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await StartAsync(_currentInterval, cancellationToken);
        }

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

            var previousStatus = _status;
            await SetStatusAsync(SchedulerStatus.Starting, "Starting scheduler");

            try
            {
                _currentInterval = interval;
                _timerExecutionService.Start(interval, cancellationToken);

                await SetStatusAsync(SchedulerStatus.Running, "Scheduler started successfully");

                var eventArgs = new LicenseCheckSchedulerEventArgs(_status, previousStatus, "Scheduler started");
                SchedulerStarted?.Invoke(this, eventArgs);

                _logger.LogInformation("License check scheduler started with interval {Interval}", interval);

                // Start recurring operations processing
                _ = Task.Run(() => ProcessRecurringOperationsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                await SetStatusAsync(SchedulerStatus.Error, $"Failed to start scheduler: {ex.Message}");
                _logger.LogError(ex, "Failed to start license check scheduler");
                throw;
            }
        }

        /// <summary>
        /// Stops the license check scheduler gracefully
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the stop operation</returns>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRunning)
            {
                _logger.LogWarning("License check scheduler is not running");
                return;
            }

            var previousStatus = _status;
            await SetStatusAsync(SchedulerStatus.Stopping, "Stopping scheduler");

            try
            {
                // Cancel all operations
                await _queue.CancelAllOperationsAsync(cancellationToken);

                // Cancel all recurring operations
                foreach (var cts in _operationCancellationTokens.Values)
                {
                    cts.Cancel();
                }

                // Stop the timer
                _timerExecutionService.Stop();

                await SetStatusAsync(SchedulerStatus.Stopped, "Scheduler stopped successfully");

                var eventArgs = new LicenseCheckSchedulerEventArgs(_status, previousStatus, "Scheduler stopped");
                SchedulerStopped?.Invoke(this, eventArgs);

                _logger.LogInformation("License check scheduler stopped");
            }
            catch (Exception ex)
            {
                await SetStatusAsync(SchedulerStatus.Error, $"Failed to stop scheduler: {ex.Message}");
                _logger.LogError(ex, "Failed to stop license check scheduler");
                throw;
            }
        }

        /// <summary>
        /// Pauses the license check scheduler
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the pause operation</returns>
        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRunning)
            {
                _logger.LogWarning("License check scheduler is not running");
                return;
            }

            var previousStatus = _status;
            await SetStatusAsync(SchedulerStatus.Pausing, "Pausing scheduler");

            try
            {
                _timerExecutionService.Pause();
                await SetStatusAsync(SchedulerStatus.Paused, "Scheduler paused successfully");

                _logger.LogInformation("License check scheduler paused");
            }
            catch (Exception ex)
            {
                await SetStatusAsync(SchedulerStatus.Error, $"Failed to pause scheduler: {ex.Message}");
                _logger.LogError(ex, "Failed to pause license check scheduler");
                throw;
            }
        }

        /// <summary>
        /// Resumes the license check scheduler
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the resume operation</returns>
        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            if (_status != SchedulerStatus.Paused)
            {
                _logger.LogWarning("License check scheduler is not paused");
                return;
            }

            var previousStatus = _status;
            await SetStatusAsync(SchedulerStatus.Resuming, "Resuming scheduler");

            try
            {
                _timerExecutionService.Resume();
                await SetStatusAsync(SchedulerStatus.Running, "Scheduler resumed successfully");

                _logger.LogInformation("License check scheduler resumed");
            }
            catch (Exception ex)
            {
                await SetStatusAsync(SchedulerStatus.Error, $"Failed to resume scheduler: {ex.Message}");
                _logger.LogError(ex, "Failed to resume license check scheduler");
                throw;
            }
        }

        /// <summary>
        /// Updates the scheduler interval while running
        /// </summary>
        /// <param name="newInterval">The new interval between license checks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the update operation</returns>
        public async Task UpdateIntervalAsync(TimeSpan newInterval, CancellationToken cancellationToken = default)
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

            if (newInterval < _options.MinInterval || newInterval > _options.MaxInterval)
            {
                throw new ArgumentException($"Interval must be between {_options.MinInterval} and {_options.MaxInterval}", nameof(newInterval));
            }

            try
            {
                _currentInterval = newInterval;
                _timerExecutionService.UpdateInterval(newInterval);

                _logger.LogInformation("License check scheduler interval updated to {Interval}", newInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update scheduler interval");
                throw;
            }
        }

        /// <summary>
        /// Executes a license check operation immediately
        /// </summary>
        /// <param name="operationType">Type of operation to execute</param>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the execution operation</returns>
        public Task<LicenseCheckResult> ExecuteNowAsync(LicenseCheckOperationType operationType, string server, int port, CancellationToken cancellationToken = default)
        {
            var operation = new LicenseCheckOperation(operationType, server, port)
            {
                Priority = LicenseCheckOperationPriority.High,
                Description = $"Immediate {operationType} for {server}:{port}"
            };

            return ExecuteNowAsync(operation, cancellationToken);
        }

        /// <summary>
        /// Executes a license check operation immediately with custom parameters
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the execution operation</returns>
        public async Task<LicenseCheckResult> ExecuteNowAsync(LicenseCheckOperation operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            // Set high priority for immediate execution
            operation.Priority = LicenseCheckOperationPriority.High;
            operation.ScheduleAfter(TimeSpan.Zero);

            // Create completion source for the result
            var tcs = new TaskCompletionSource<LicenseCheckResult>();

            // Subscribe to queue events for this operation
            EventHandler<LicenseCheckOperationEventArgs> onCompleted = null;
            EventHandler<LicenseCheckOperationEventArgs> onFailed = null;

            onCompleted = (sender, args) =>
            {
                if (args.OperationId == operation.OperationId)
                {
                    _queue.OperationCompleted -= onCompleted;
                    _queue.OperationFailed -= onFailed;
                    tcs.TrySetResult(args.Result);
                }
            };

            onFailed = (sender, args) =>
            {
                if (args.OperationId == operation.OperationId)
                {
                    _queue.OperationCompleted -= onCompleted;
                    _queue.OperationFailed -= onFailed;
                    var result = LicenseCheckResult.CreateFailure(operation, args.ErrorMessage, args.Exception);
                    tcs.TrySetResult(result);
                }
            };

            _queue.OperationCompleted += onCompleted;
            _queue.OperationFailed += onFailed;

            // Enqueue the operation
            await _queue.EnqueueAsync(operation, cancellationToken);

            // Return the task that will complete when the operation finishes
            return tcs.Task;
        }

        /// <summary>
        /// Schedules a one-time license check operation
        /// </summary>
        /// <param name="operation">The operation to schedule</param>
        /// <param name="delay">Delay before executing the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the scheduling operation</returns>
        public async Task ScheduleOneTimeAsync(LicenseCheckOperation operation, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            operation.ScheduleAfter(delay);
            await _queue.EnqueueAsync(operation, cancellationToken);

            _logger.LogDebug("Scheduled one-time operation {OperationId} with delay {Delay}", operation.OperationId, delay);
        }

        /// <summary>
        /// Adds a recurring license check operation to the scheduler
        /// </summary>
        /// <param name="operation">The operation to add</param>
        /// <param name="interval">The interval for recurring execution</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the add operation</returns>
        public async Task AddRecurringOperationAsync(LicenseCheckOperation operation, TimeSpan interval, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentException("Interval must be greater than zero", nameof(interval));
            }

            operation.IsRecurring = true;
            operation.RecurringInterval = interval;

            _recurringOperations.TryAdd(operation.OperationId, operation);
            _recurringOperationLastExecution.TryAdd(operation.OperationId, DateTime.Now);

            _logger.LogDebug("Added recurring operation {OperationId} with interval {Interval}", operation.OperationId, interval);
        }

        /// <summary>
        /// Removes a recurring license check operation from the scheduler
        /// </summary>
        /// <param name="operationId">The ID of the operation to remove</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the remove operation</returns>
        public async Task RemoveRecurringOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
        {
            _recurringOperations.TryRemove(operationId, out _);
            _recurringOperationLastExecution.TryRemove(operationId, out _);

            if (_operationCancellationTokens.TryRemove(operationId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            _logger.LogDebug("Removed recurring operation {OperationId}", operationId);
        }

        /// <summary>
        /// Gets all scheduled operations
        /// </summary>
        /// <returns>Collection of scheduled operations</returns>
        public IEnumerable<LicenseCheckOperation> GetScheduledOperations()
        {
            return _recurringOperations.Values.Concat(_queue.GetQueuedOperations()).ToArray();
        }

        /// <summary>
        /// Gets the operation history
        /// </summary>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>Collection of completed operations</returns>
        public IEnumerable<LicenseCheckResult> GetOperationHistory(int maxResults = 100)
        {
            // This would need to be implemented with a proper history storage
            // For now, return empty collection
            return Array.Empty<LicenseCheckResult>();
        }

        /// <summary>
        /// Gets performance metrics for the scheduler
        /// </summary>
        /// <returns>Scheduler performance metrics</returns>
        public LicenseCheckSchedulerMetrics GetMetrics()
        {
            var uptime = DateTime.Now - _startTime;
            var queueStatus = _queue.GetStatus();

            return new LicenseCheckSchedulerMetrics
            {
                StartTime = _startTime,
                Uptime = uptime,
                TotalOperations = queueStatus.TotalProcessed,
                SuccessfulOperations = queueStatus.CompletedCount,
                FailedOperations = queueStatus.FailedCount,
                CancelledOperations = queueStatus.CancelledCount,
                TimedOutOperations = queueStatus.TimedOutCount,
                ActiveTimers = _timerExecutionService.IsRunning ? 1 : 0,
                RecurringOperations = _recurringOperations.Count,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Resets scheduler statistics and counters
        /// </summary>
        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _totalOperationsCompleted, 0);
            Interlocked.Exchange(ref _totalOperationsFailed, 0);
            _startTime = DateTime.Now;
            _lastExecutionTime = DateTime.Now;

            _timerExecutionService.ResetStatistics();

            _logger.LogDebug("Scheduler statistics reset");
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();

            if (_currentInterval <= TimeSpan.Zero)
            {
                errors.Add("Current interval must be greater than zero");
            }

            if (_currentInterval < _options.MinInterval)
            {
                errors.Add($"Current interval is less than minimum allowed interval ({_options.MinInterval})");
            }

            if (_currentInterval > _options.MaxInterval)
            {
                errors.Add($"Current interval is greater than maximum allowed interval ({_options.MaxInterval})");
            }

            return errors;
        }

        /// <summary>
        /// Gets the current queue status
        /// </summary>
        /// <returns>Queue status information</returns>
        public LicenseCheckQueueStatus GetQueueStatus()
        {
            return _queue.GetStatus();
        }

        /// <summary>
        /// Handles timer execution started events
        /// </summary>
        private async void OnTimerExecutionStarted(object sender, TimerExecutionEventArgs e)
        {
            if (!IsRunning)
                return;

            _logger.LogDebug("Timer execution started: {ExecutionId}", e.ExecutionId);

            // Execute default license check
            var operation = LicenseCheckOperation.CreateComprehensiveCheck("localhost", 1057);
            await ExecuteNowAsync(operation, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Handles timer execution completed events
        /// </summary>
        private async void OnTimerExecutionCompleted(object sender, TimerExecutionEventArgs e)
        {
            if (!IsRunning)
                return;

            _lastExecutionTime = DateTime.Now;
            _logger.LogDebug("Timer execution completed: {ExecutionId}, Duration: {Duration}ms", e.ExecutionId, e.Duration.TotalMilliseconds);
        }

        /// <summary>
        /// Handles timer execution error events
        /// </summary>
        private async void OnTimerExecutionError(object sender, TimerExecutionErrorEventArgs e)
        {
            _logger.LogError(e.Error, "Timer execution error: {ExecutionId}", e.ExecutionId);

            // Update status to error if it's a critical error
            if (e.ConsecutiveErrors >= _options.MaxConsecutiveErrors)
            {
                await SetStatusAsync(SchedulerStatus.Error, $"Timer execution failed {e.ConsecutiveErrors} times consecutively");
            }
        }

        /// <summary>
        /// Handles timer state change events
        /// </summary>
        private async void OnTimerStateChanged(object sender, TimerStateChangedEventArgs e)
        {
            _logger.LogDebug("Timer state changed from {PreviousState} to {NewState}", e.PreviousState, e.NewState);

            // Update scheduler status based on timer state
            if (e.NewState == TimerState.Running && _status == SchedulerStatus.Paused)
            {
                await SetStatusAsync(SchedulerStatus.Running, "Timer resumed");
            }
        }

        /// <summary>
        /// Handles queue operation started events
        /// </summary>
        private void OnQueueOperationStarted(object sender, LicenseCheckOperationEventArgs e)
        {
            OperationStarted?.Invoke(this, e);
        }

        /// <summary>
        /// Handles queue operation completed events
        /// </summary>
        private void OnQueueOperationCompleted(object sender, LicenseCheckOperationEventArgs e)
        {
            Interlocked.Increment(ref _totalOperationsCompleted);
            OperationCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// Handles queue operation failed events
        /// </summary>
        private void OnQueueOperationFailed(object sender, LicenseCheckOperationEventArgs e)
        {
            Interlocked.Increment(ref _totalOperationsFailed);
            OperationFailed?.Invoke(this, e);
        }

        /// <summary>
        /// Processes recurring operations asynchronously
        /// </summary>
        private async Task ProcessRecurringOperationsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;

                    foreach (var kvp in _recurringOperations.ToList())
                    {
                        var operationId = kvp.Key;
                        var operation = kvp.Value;

                        if (_recurringOperationLastExecution.TryGetValue(operationId, out var lastExecution))
                        {
                            var nextExecution = lastExecution + operation.RecurringInterval.Value;

                            if (now >= nextExecution)
                            {
                                // Check if we've reached the maximum execution count
                                if (operation.MaxRecurringExecutions.HasValue &&
                                    operation.RecurringExecutionCount >= operation.MaxRecurringExecutions.Value)
                                {
                                    // Remove the operation
                                    await RemoveRecurringOperationAsync(operationId, cancellationToken);
                                    continue;
                                }

                                // Create a new operation for this execution
                                var executionOperation = operation.Clone();
                                executionOperation.OperationId = Guid.NewGuid();
                                executionOperation.RecurringExecutionCount = operation.RecurringExecutionCount + 1;
                                executionOperation.MarkAsExecuting();

                                // Update the last execution time
                                _recurringOperationLastExecution.TryUpdate(operationId, now, lastExecution);

                                // Execute the operation
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        var result = await ExecuteOperationAsync(executionOperation, cancellationToken);
                                        await _queue.EnqueueAsync(executionOperation, cancellationToken);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error executing recurring operation {OperationId}", operationId);
                                    }
                                }, cancellationToken);
                            }
                        }
                    }

                    // Wait before checking again
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing recurring operations");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Executes a license check operation
        /// </summary>
        private async Task<LicenseCheckResult> ExecuteOperationAsync(LicenseCheckOperation operation, CancellationToken cancellationToken)
        {
            var result = new LicenseCheckResult(operation);

            try
            {
                result.StartTime = DateTime.Now;

                switch (operation.OperationType)
                {
                    case LicenseCheckOperationType.ServerStatusCheck:
                        await ExecuteServerStatusCheckAsync(operation, result, cancellationToken);
                        break;

                    case LicenseCheckOperationType.ServerHealthCheck:
                        await ExecuteServerHealthCheckAsync(operation, result, cancellationToken);
                        break;

                    case LicenseCheckOperationType.FeatureStatusCheck:
                        await ExecuteFeatureStatusCheckAsync(operation, result, cancellationToken);
                        break;

                    case LicenseCheckOperationType.UserStatusCheck:
                        await ExecuteUserStatusCheckAsync(operation, result, cancellationToken);
                        break;

                    case LicenseCheckOperationType.IdleLicenseCheck:
                        await ExecuteIdleLicenseCheckAsync(operation, result, cancellationToken);
                        break;

                    case LicenseCheckOperationType.ComprehensiveCheck:
                        await ExecuteComprehensiveCheckAsync(operation, result, cancellationToken);
                        break;

                    default:
                        result.ErrorMessage = $"Unsupported operation type: {operation.OperationType}";
                        result.Success = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.Success = false;
                _logger.LogError(ex, "Error executing operation {OperationId}", operation.OperationId);
            }
            finally
            {
                result.EndTime = DateTime.Now;
                result.Duration = result.EndTime - result.StartTime;
            }

            return result;
        }

        /// <summary>
        /// Executes a server status check
        /// </summary>
        private async Task ExecuteServerStatusCheckAsync(LicenseCheckOperation operation, LicenseCheckResult result, CancellationToken cancellationToken)
        {
            var serverStatus = await _licenseQueryEngine.QueryLicenseStatusAsync(operation.Server, operation.Port, cancellationToken);
            result.CompleteWithServerStatus(serverStatus);
        }

        /// <summary>
        /// Executes a server health check
        /// </summary>
        private async Task ExecuteServerHealthCheckAsync(LicenseCheckOperation operation, LicenseCheckResult result, CancellationToken cancellationToken)
        {
            var health = await _licenseQueryEngine.CheckServerHealthAsync(operation.Server, operation.Port, cancellationToken);
            result.Success = health.IsHealthy;
            result.ErrorMessage = health.ErrorMessage;
        }

        /// <summary>
        /// Executes a feature status check
        /// </summary>
        private async Task ExecuteFeatureStatusCheckAsync(LicenseCheckOperation operation, LicenseCheckResult result, CancellationToken cancellationToken)
        {
            var feature = await _licenseQueryEngine.QueryFeatureAsync(operation.Server, operation.Port, operation.Feature, cancellationToken);
            result.Success = feature != null;
            result.FeaturesProcessed = 1;
            result.LicensesChecked = feature?.TotalLicenses ?? 0;
        }

        /// <summary>
        /// Executes a user status check
        /// </summary>
        private async Task ExecuteUserStatusCheckAsync(LicenseCheckOperation operation, LicenseCheckResult result, CancellationToken cancellationToken)
        {
            var userLicenses = await _licenseQueryEngine.QueryUsersAsync(operation.Server, operation.Port, new[] { operation.User }, cancellationToken);
            result.Success = userLicenses.ContainsKey(operation.User);
            result.UsersProcessed = userLicenses.Count;
            result.LicensesChecked = userLicenses.Values.Sum(licenses => licenses.Count);
        }

        /// <summary>
        /// Executes an idle license check
        /// </summary>
        private async Task ExecuteIdleLicenseCheckAsync(LicenseCheckOperation operation, LicenseCheckResult result, CancellationToken cancellationToken)
        {
            var idleLicenses = await _licenseQueryEngine.QueryIdleLicensesAsync(operation.Server, operation.Port, cancellationToken);
            result.AddIdleLicenses(idleLicenses);
            result.Success = true;
        }

        /// <summary>
        /// Executes a comprehensive license check
        /// </summary>
        private async Task ExecuteComprehensiveCheckAsync(LicenseCheckOperation operation, LicenseCheckResult result, CancellationToken cancellationToken)
        {
            var serverStatus = await _licenseQueryEngine.QueryLicenseStatusAsync(operation.Server, operation.Port, cancellationToken);
            result.CompleteWithServerStatus(serverStatus);

            // Also check for idle and borrowed licenses
            var idleLicenses = await _licenseQueryEngine.QueryIdleLicensesAsync(operation.Server, operation.Port, cancellationToken);
            var borrowedLicenses = await _licenseQueryEngine.QueryBorrowedLicensesAsync(operation.Server, operation.Port, cancellationToken);

            result.AddIdleLicenses(idleLicenses);
            result.AddBorrowedLicenses(borrowedLicenses);

            // Get usage statistics
            var statistics = await _licenseQueryEngine.QueryUsageStatisticsAsync(operation.Server, operation.Port, cancellationToken);
            result.SetUsageStatistics(statistics);
        }

        /// <summary>
        /// Sets the scheduler status and raises events
        /// </summary>
        private async Task SetStatusAsync(SchedulerStatus newStatus, string reason)
        {
            var previousStatus = _status;
            _status = newStatus;

            var eventArgs = new SchedulerStatusChangedEventArgs(previousStatus, newStatus, reason);
            StatusChanged?.Invoke(this, eventArgs);

            _logger.LogDebug("Scheduler status changed from {PreviousStatus} to {NewStatus}: {Reason}", previousStatus, newStatus, reason);
        }

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
                _timerExecutionService.Stop();

                // Dispose the queue
                _queue?.Dispose();

                // Dispose all cancellation tokens
                foreach (var cts in _operationCancellationTokens.Values)
                {
                    cts.Dispose();
                }

                // Unsubscribe from events
                _timerExecutionService.ExecutionStarted -= OnTimerExecutionStarted;
                _timerExecutionService.ExecutionCompleted -= OnTimerExecutionCompleted;
                _timerExecutionService.ExecutionError -= OnTimerExecutionError;
                _timerExecutionService.StateChanged -= OnTimerStateChanged;

                _queue.OperationStarted -= OnQueueOperationStarted;
                _queue.OperationCompleted -= OnQueueOperationCompleted;
                _queue.OperationFailed -= OnQueueOperationFailed;

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
    }
}