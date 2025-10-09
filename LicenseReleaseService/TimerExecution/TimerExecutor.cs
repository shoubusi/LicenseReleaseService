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
    /// Implementation of ITimerExecutor that provides thread-safe timer execution with full lifecycle management
    /// </summary>
    public class TimerExecutor : ITimerExecutor
    {
        #region Private Fields

        private readonly object _syncLock = new object();
        private readonly System.Collections.Generic.Queue<TimerExecutionEventArgs> _executionHistory = new System.Collections.Generic.Queue<TimerExecutionEventArgs>();
        private readonly System.Collections.Generic.List<TimerErrorInfo> _recentErrors = new System.Collections.Generic.List<TimerErrorInfo>();
        private readonly TimerPerformanceMetrics _metrics = new TimerPerformanceMetrics();
        private readonly Stopwatch _uptimeStopwatch = new Stopwatch();
        private readonly SemaphoreSlim _executionSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _stateChangeSemaphore = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _globalCancellationTokenSource = new CancellationTokenSource();
        private readonly System.Collections.Generic.List<Func<Task>> _callbacks = new System.Collections.Generic.List<Func<Task>>();
        private readonly System.Collections.Generic.List<Func<TimerExecutionEventArgs, Task>> _executionCallbacks = new System.Collections.Generic.List<Func<TimerExecutionEventArgs, Task>>();

        private TimerStatus _status = TimerStatus.Created;
        private TimerExecutionOptions _options;
        private System.Threading.Timer _timer;
        private CancellationTokenSource _currentExecutionCancellationTokenSource;
        private Task _currentExecutionTask;
        private DateTime _startTime;
        private DateTime _lastExecutionTime;
        private DateTime _nextExecutionTime;
        private int _consecutiveErrors;
        private bool _disposed;
        private int _executionCount;

        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler<TimerExecutionEventArgs> ExecutionStarted;

        /// <inheritdoc />
        public event EventHandler<TimerExecutionEventArgs> ExecutionCompleted;

        /// <inheritdoc />
        public event EventHandler<TimerExecutionErrorEventArgs> ExecutionError;

        /// <inheritdoc />
        public event EventHandler<TimerStateChangedEventArgs> StateChanged;

        /// <inheritdoc />
        public event EventHandler<TimerExecutionEventArgs> Paused;

        /// <inheritdoc />
        public event EventHandler<TimerExecutionEventArgs> Resumed;

        /// <inheritdoc />
        public event EventHandler<TimerExecutionEventArgs> Stopped;

        /// <inheritdoc />
        public event EventHandler Disposed;

        #endregion

        #region Properties

        /// <inheritdoc />
        public TimerStatus Status
        {
            get
            {
                lock (_syncLock)
                {
                    return _status;
                }
            }
        }

        /// <inheritdoc />
        public TimerExecutionOptions Options
        {
            get
            {
                lock (_syncLock)
                {
                    return _options.Clone();
                }
            }
        }

        /// <inheritdoc />
        public TimeSpan Interval
        {
            get
            {
                lock (_syncLock)
                {
                    return _options.DefaultInterval;
                }
            }
        }

        /// <inheritdoc />
        public bool IsRunning => Status.IsRunning();

        /// <inheritdoc />
        public bool IsPaused => Status == TimerStatus.Paused;

        /// <inheritdoc />
        public bool IsDisposed => _disposed;

        /// <inheritdoc />
        public TimerPerformanceMetrics Metrics
        {
            get
            {
                lock (_syncLock)
                {
                    UpdateMetrics();
                    return _metrics;
                }
            }
        }

        /// <inheritdoc />
        public int ConsecutiveErrors
        {
            get
            {
                lock (_syncLock)
                {
                    return _consecutiveErrors;
                }
            }
        }

        /// <inheritdoc />
        public DateTime? LastExecutionTime
        {
            get
            {
                lock (_syncLock)
                {
                    return _lastExecutionTime == default ? null : (DateTime?)_lastExecutionTime;
                }
            }
        }

        /// <inheritdoc />
        public DateTime? NextExecutionTime
        {
            get
            {
                lock (_syncLock)
                {
                    return _nextExecutionTime == default ? null : (DateTime?)_nextExecutionTime;
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the TimerExecutor class with default options
        /// </summary>
        public TimerExecutor() : this(new TimerExecutionOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutor class with specified options
        /// </summary>
        /// <param name="options">The timer execution options</param>
        public TimerExecutor(TimerExecutionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            ValidateOptions();
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutor class with specified interval
        /// </summary>
        /// <param name="interval">The timer interval</param>
        public TimerExecutor(TimeSpan interval) : this(new TimerExecutionOptions(interval))
        {
        }

        #endregion

        #region Lifecycle Methods

        /// <inheritdoc />
        public async Task StartAsync()
        {
            await StartAsync(_options.DefaultInterval);
        }

        /// <inheritdoc />
        public async Task StartAsync(TimeSpan interval)
        {
            await _stateChangeSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TimerExecutor));

                if (!Status.CanStart())
                    throw new InvalidOperationException($"Cannot start timer from {Status} state");

                ValidateInterval(interval);

                var previousStatus = Status;
                await ChangeStatusAsync(TimerStatus.Starting, "Starting timer");

                try
                {
                    _options.DefaultInterval = interval;
                    _startTime = DateTime.UtcNow;
                    _uptimeStopwatch.Restart();
                    _consecutiveErrors = 0;

                    _timer = new System.Threading.Timer(
                        TimerCallback,
                        null,
                        interval,
                        interval);

                    await ChangeStatusAsync(TimerStatus.Running, "Timer started successfully");
                    await OnStartedAsync();
                }
                catch (Exception ex)
                {
                    await ChangeStatusAsync(previousStatus, $"Failed to start timer: {ex.Message}");
                    await HandleErrorAsync(ex, Status);
                    throw;
                }
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            await _stateChangeSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TimerExecutor));

                if (!Status.CanStop())
                    return;

                var previousStatus = Status;
                await ChangeStatusAsync(TimerStatus.Stopping, "Stopping timer");

                try
                {
                    await CancelCurrentExecutionAsync();

                    if (_timer != null)
                    {
                        _timer.Change(Timeout.Infinite, Timeout.Infinite);
                        _timer.Dispose();
                        _timer = null;
                    }

                    _uptimeStopwatch.Stop();
                    await ChangeStatusAsync(TimerStatus.Stopped, "Timer stopped successfully");
                    await OnStoppedAsync();
                }
                catch (Exception ex)
                {
                    await ChangeStatusAsync(previousStatus, $"Failed to stop timer: {ex.Message}");
                    await HandleErrorAsync(ex, Status);
                    throw;
                }
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task PauseAsync()
        {
            await _stateChangeSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TimerExecutor));

                if (!Status.CanPause())
                    throw new InvalidOperationException($"Cannot pause timer from {Status} state");

                await ChangeStatusAsync(TimerStatus.Paused, "Timer paused");

                if (_timer != null)
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }

                await OnPausedAsync();
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task ResumeAsync()
        {
            await _stateChangeSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TimerExecutor));

                if (!Status.CanResume())
                    throw new InvalidOperationException($"Cannot resume timer from {Status} state");

                await ChangeStatusAsync(TimerStatus.Running, "Timer resumed");

                if (_timer != null)
                {
                    _timer.Change(_options.DefaultInterval, _options.DefaultInterval);
                }

                await OnResumedAsync();
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task RestartAsync()
        {
            await _stateChangeSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TimerExecutor));

                if (!Status.CanRestart())
                    throw new InvalidOperationException($"Cannot restart timer from {Status} state");

                var previousStatus = Status;
                await ChangeStatusAsync(TimerStatus.Restarting, "Restarting timer");

                try
                {
                    await StopAsync();
                    await StartAsync(_options.DefaultInterval);
                }
                catch (Exception ex)
                {
                    await ChangeStatusAsync(previousStatus, $"Failed to restart timer: {ex.Message}");
                    await HandleErrorAsync(ex, Status);
                    throw;
                }
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task TriggerExecutionAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TimerExecutor));

            await ExecuteCallbacksAsync();
        }

        /// <inheritdoc />
        public async Task ChangeIntervalAsync(TimeSpan newInterval)
        {
            await _stateChangeSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TimerExecutor));

                ValidateInterval(newInterval);

                var oldInterval = _options.DefaultInterval;
                _options.DefaultInterval = newInterval;

                if (Status == TimerStatus.Running && _timer != null)
                {
                    _timer.Change(newInterval, newInterval);
                }

                _nextExecutionTime = DateTime.UtcNow.Add(newInterval);
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task ResetAsync()
        {
            await _stateChangeSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TimerExecutor));

                _consecutiveErrors = 0;
                _executionCount = 0;
                _metrics.TotalExecutions = 0;
                _metrics.SuccessfulExecutions = 0;
                _metrics.FailedExecutions = 0;
                _metrics.CurrentConsecutiveErrors = 0;
                _metrics.MaxConsecutiveErrors = 0;

                lock (_executionHistory)
                {
                    _executionHistory.Clear();
                }

                lock (_recentErrors)
                {
                    _recentErrors.Clear();
                }

                if (Status.IsError())
                {
                    await ChangeStatusAsync(TimerStatus.Stopped, "Timer reset");
                }
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> WaitForCurrentExecutionAsync(TimeSpan timeout)
        {
            if (_currentExecutionTask == null)
                return true;

            try
            {
                // Wait for execution with timeout using WaitAsync
                using var timeoutCts = new CancellationTokenSource(timeout);
                await _currentExecutionTask.WaitAsync(timeoutCts.Token);
                return true; // Task completed successfully
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task CancelCurrentExecutionAsync()
        {
            if (_currentExecutionCancellationTokenSource != null)
            {
                _currentExecutionCancellationTokenSource.Cancel();

                try
                {
                    if (_currentExecutionTask != null)
                    {
                        // Wait for cancellation with grace period using WaitAsync
                        using var gracePeriodCts = new CancellationTokenSource(_options.DisposalGracePeriod);
                        await _currentExecutionTask.WaitAsync(gracePeriodCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                finally
                {
                    _currentExecutionCancellationTokenSource.Dispose();
                    _currentExecutionCancellationTokenSource = null;
                    _currentExecutionTask = null;
                }
            }
        }

        #endregion

        #region Configuration Methods

        /// <inheritdoc />
        public async Task UpdateOptionsAsync(TimerExecutionOptions newOptions)
        {
            if (newOptions == null)
                throw new ArgumentNullException(nameof(newOptions));

            await _stateChangeSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TimerExecutor));

                var validationErrors = newOptions.Validate();
                if (validationErrors.Count > 0)
                    throw new ArgumentException($"Invalid options: {string.Join(", ", validationErrors)}");

                var oldOptions = _options.Clone();
                _options = newOptions.Clone();

                // Apply interval change if timer is running
                if (Status == TimerStatus.Running && _timer != null && oldOptions.DefaultInterval != newOptions.DefaultInterval)
                {
                    _timer.Change(newOptions.DefaultInterval, newOptions.DefaultInterval);
                }
            }
            finally
            {
                _stateChangeSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public List<string> ValidateConfiguration()
        {
            return _options.Validate();
        }

        #endregion

        #region Information Methods

        /// <inheritdoc />
        public TimerStatusInfo GetStatusInfo()
        {
            lock (_syncLock)
            {
                return new TimerStatusInfo
                {
                    Status = Status,
                    Interval = Interval,
                    LastExecutionTime = LastExecutionTime,
                    NextExecutionTime = NextExecutionTime,
                    ConsecutiveErrors = ConsecutiveErrors,
                    TotalExecutions = _metrics.TotalExecutions,
                    Uptime = _uptimeStopwatch.Elapsed,
                    StateTimestamp = DateTime.UtcNow,
                    StartTime = _metrics.StartTime,
                    StopTime = _metrics.StopTime
                };
            }
        }

        /// <inheritdoc />
        public List<TimerExecutionEventArgs> GetExecutionHistory(int maxItems = 10)
        {
            lock (_executionHistory)
            {
                return new List<TimerExecutionEventArgs>(_executionHistory.Take(Math.Min(maxItems, _executionHistory.Count)));
            }
        }

        /// <inheritdoc />
        public TimerDiagnosticsInfo GetDiagnostics()
        {
            lock (_syncLock)
            {
                return new TimerDiagnosticsInfo
                {
                    Status = GetStatusInfo(),
                    Options = Options,
                    Metrics = Metrics,
                    ThreadInfo = GetThreadInfo(),
                    RecentErrors = new List<TimerErrorInfo>(_recentErrors),
                    MemoryInfo = GetMemoryInfo(),
                    SystemInfo = GetSystemInfo()
                };
            }
        }

        #endregion

        #region Callback Registration

        /// <summary>
        /// Registers a callback function to be executed when the timer fires
        /// </summary>
        /// <param name="callback">The callback function</param>
        public void RegisterCallback(Func<Task> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (_syncLock)
            {
                _callbacks.Add(callback);
            }
        }

        /// <summary>
        /// Registers a callback function to be executed when the timer fires with execution context
        /// </summary>
        /// <param name="callback">The callback function</param>
        public void RegisterCallback(Func<TimerExecutionEventArgs, Task> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (_syncLock)
            {
                _executionCallbacks.Add(callback);
            }
        }

        /// <summary>
        /// Removes a registered callback function
        /// </summary>
        /// <param name="callback">The callback function to remove</param>
        public void UnregisterCallback(Func<Task> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (_syncLock)
            {
                _callbacks.Remove(callback);
            }
        }

        /// <summary>
        /// Removes a registered callback function
        /// </summary>
        /// <param name="callback">The callback function to remove</param>
        public void UnregisterCallback(Func<TimerExecutionEventArgs, Task> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (_syncLock)
            {
                _executionCallbacks.Remove(callback);
            }
        }

        #endregion

        #region Private Methods

        private async void TimerCallback(object state)
        {
            try
            {
                await ExecuteCallbacksAsync();
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, Status);
            }
        }

        private async Task ExecuteCallbacksAsync()
        {
            if (_disposed)
                return;

            // Prevent execution overlap if configured
            if (_options.PreventExecutionOverlap && _currentExecutionTask != null && !_currentExecutionTask.IsCompleted)
            {
                if (_options.EnableDetailedLogging)
                {
                    // Log skipped execution due to overlap
                }
                return;
            }

            await _executionSemaphore.WaitAsync();
            try
            {
                var executionId = Guid.NewGuid();
                var executionArgs = new TimerExecutionEventArgs(executionId, DateTime.UtcNow, ++_executionCount);
                var previousStatus = Status;

                await ChangeStatusAsync(TimerStatus.Executing, "Executing callbacks");
                await OnExecutionStartedAsync(executionArgs);

                _currentExecutionCancellationTokenSource = new CancellationTokenSource();
                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    _currentExecutionCancellationTokenSource.Token,
                    _globalCancellationTokenSource.Token);

                try
                {
                    _lastExecutionTime = DateTime.UtcNow;
                    _nextExecutionTime = _lastExecutionTime.Add(_options.DefaultInterval);

                    // Execute all callbacks
                    var callbackTasks = new List<Task>();

                    // Execute simple callbacks
                    lock (_syncLock)
                    {
                        foreach (var callback in _callbacks.ToList())
                        {
                            callbackTasks.Add(Task.Run(() => ExecuteCallbackSafely(callback, linkedTokenSource.Token)));
                        }

                        // Execute execution callbacks
                        foreach (var callback in _executionCallbacks.ToList())
                        {
                            callbackTasks.Add(Task.Run(() => ExecuteExecutionCallbackSafely(callback, executionArgs, linkedTokenSource.Token)));
                        }
                    }

                    // Wait for all callbacks to complete or timeout
                    var timeoutTask = Task.Delay(_options.ExecutionTimeout, linkedTokenSource.Token);
                    var allCallbacksTask = Task.WhenAll(callbackTasks);

                    // Wait for all callbacks to complete or timeout using WaitAsync
                    try
                    {
                        await allCallbacksTask.WaitAsync(linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout occurred
                        throw new TimeoutException($"Timer execution timed out after {_options.ExecutionTimeout.TotalMilliseconds:F2}ms");
                    }

                    // Check for callback failures
                    if (allCallbacksTask.IsFaulted)
                    {
                        throw allCallbacksTask.Exception ?? new Exception("Unknown callback execution error");
                    }

                    executionArgs.Success = true;
                    executionArgs.OperationsProcessed = callbackTasks.Count;
                    executionArgs.EndTime = DateTime.UtcNow;
                    executionArgs.Duration = executionArgs.EndTime - executionArgs.StartTime;

                    _consecutiveErrors = 0;
                    await OnExecutionCompletedAsync(executionArgs);
                }
                catch (Exception ex)
                {
                    executionArgs.Success = false;
                    executionArgs.ErrorMessage = ex.Message;
                    executionArgs.EndTime = DateTime.UtcNow;
                    executionArgs.Duration = executionArgs.EndTime - executionArgs.StartTime;

                    _consecutiveErrors++;
                    await HandleErrorAsync(ex, Status, executionId);
                    await OnExecutionCompletedAsync(executionArgs);

                    if (_options.EnableCircuitBreaker && _consecutiveErrors >= _options.MaxConsecutiveErrors)
                    {
                        await TriggerCircuitBreakerAsync();
                    }
                }
                finally
                {
                    _currentExecutionCancellationTokenSource.Dispose();
                    _currentExecutionCancellationTokenSource = null;
                    _currentExecutionTask = null;

                    await ChangeStatusAsync(previousStatus, "Execution completed");
                }
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        private async Task ExecuteCallbackSafely(Func<Task> callback, CancellationToken cancellationToken)
        {
            try
            {
                await callback();
            }
            catch (Exception ex)
            {
                if (_options.EnableDetailedLogging)
                {
                    // Log callback execution error
                }
                throw;
            }
        }

        private async Task ExecuteExecutionCallbackSafely(Func<TimerExecutionEventArgs, Task> callback, TimerExecutionEventArgs args, CancellationToken cancellationToken)
        {
            try
            {
                await callback(args);
            }
            catch (Exception ex)
            {
                if (_options.EnableDetailedLogging)
                {
                    // Log callback execution error
                }
                throw;
            }
        }

        private async Task ChangeStatusAsync(TimerStatus newStatus, string reason)
        {
            var previousStatus = Status;

            lock (_syncLock)
            {
                _status = newStatus;
            }

            await OnStateChangedAsync(previousStatus, newStatus, reason);
        }

        private async Task HandleErrorAsync(Exception error, TimerStatus status, Guid? executionId = null)
        {
            var errorArgs = new TimerErrorEventArgs(error, executionId, status, _consecutiveErrors);

            // Determine if circuit breaker should be triggered
            errorArgs.ShouldTriggerCircuitBreaker = _options.EnableCircuitBreaker &&
                                                    _consecutiveErrors >= _options.MaxConsecutiveErrors;

            // Determine if error is fatal
            errorArgs.IsFatal = error is OutOfMemoryException ||
                               error is StackOverflowException ||
                               (_options.StopOnUnhandledException && errorArgs.ShouldTriggerCircuitBreaker);

            await OnExecutionErrorAsync(errorArgs);

            // Add to recent errors
            lock (_recentErrors)
            {
                _recentErrors.Add(new TimerErrorInfo
                {
                    Timestamp = errorArgs.Timestamp,
                    Message = errorArgs.Error.Message,
                    ErrorType = errorArgs.Error.GetType().Name,
                    StackTrace = errorArgs.Error.StackTrace,
                    ExecutionId = errorArgs.ExecutionId,
                    ConsecutiveErrors = errorArgs.ConsecutiveErrors
                });

                // Keep only recent errors
                while (_recentErrors.Count > 50)
                {
                    _recentErrors.RemoveAt(0);
                }
            }

            // Handle fatal errors
            if (errorArgs.IsFatal)
            {
                await StopAsync();
            }
        }

        private async Task TriggerCircuitBreakerAsync()
        {
            await ChangeStatusAsync(TimerStatus.CircuitBreaker, $"Circuit breaker triggered due to {_consecutiveErrors} consecutive errors");

            if (_options.EnableAutoRestart)
            {
                // Schedule restart after cooldown
                _ = Task.Delay(_options.CircuitBreakerCooldown).ContinueWith(async _ =>
                {
                    if (!_disposed && Status == TimerStatus.CircuitBreaker)
                    {
                        await ResetAsync();
                        await StartAsync();
                    }
                });
            }
        }

        private void UpdateMetrics()
        {
            _metrics.Uptime = _uptimeStopwatch.Elapsed;
            _metrics.CurrentConsecutiveErrors = _consecutiveErrors;
            _metrics.MaxConsecutiveErrors = Math.Max(_metrics.MaxConsecutiveErrors, _consecutiveErrors);
            _metrics.LastExecutionTime = LastExecutionTime;
            _metrics.StartTime = _startTime;
        }

        private void ValidateOptions()
        {
            var errors = _options.Validate();
            if (errors.Count > 0)
                throw new ArgumentException($"Invalid timer options: {string.Join(", ", errors)}");
        }

        private void ValidateInterval(TimeSpan interval)
        {
            if (interval < _options.MinInterval)
                throw new ArgumentException($"Interval cannot be less than {_options.MinInterval.TotalMilliseconds:F2}ms", nameof(interval));

            if (interval > _options.MaxInterval)
                throw new ArgumentException($"Interval cannot be greater than {_options.MaxInterval.TotalMilliseconds:F2}ms", nameof(interval));
        }

        private TimerThreadInfo GetThreadInfo()
        {
            var currentThread = Thread.CurrentThread;
            return new TimerThreadInfo
            {
                ManagedThreadId = currentThread.ManagedThreadId,
                ThreadState = currentThread.ThreadState,
                ThreadPriority = currentThread.Priority,
                IsBackground = currentThread.IsBackground,
                IsThreadPoolThread = currentThread.IsThreadPoolThread
            };
        }

        private TimerMemoryInfo GetMemoryInfo()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            return new TimerMemoryInfo
            {
                CurrentMemoryUsage = process.WorkingSet64,
                PeakMemoryUsage = process.PeakWorkingSet64,
                TrackedObjects = (int)GC.GetTotalMemory(false),
                GarbageCollectionInfo = $"Gen0: {GC.CollectionCount(0)}, Gen1: {GC.CollectionCount(1)}, Gen2: {GC.CollectionCount(2)}"
            };
        }

        private TimerSystemInfo GetSystemInfo()
        {
            return new TimerSystemInfo
            {
                OSVersion = Environment.OSVersion.ToString(),
                RuntimeVersion = Environment.Version.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            };
        }

        #endregion

        #region Event Handlers

        private async Task OnStartedAsync()
        {
            var args = new TimerExecutionEventArgs(Guid.NewGuid(), DateTime.UtcNow, 0);
            ExecutionStarted?.Invoke(this, args);

            if (_options.EnableMetrics)
            {
                _metrics.StartTime = DateTime.UtcNow;
            }
        }

        private async Task OnStoppedAsync()
        {
            var args = new TimerExecutionEventArgs(Guid.NewGuid(), DateTime.UtcNow, 0);
            Stopped?.Invoke(this, args);

            if (_options.EnableMetrics)
            {
                _metrics.StopTime = DateTime.UtcNow;
            }
        }

        private async Task OnPausedAsync()
        {
            var args = new TimerExecutionEventArgs(Guid.NewGuid(), DateTime.UtcNow, 0);
            Paused?.Invoke(this, args);
        }

        private async Task OnResumedAsync()
        {
            var args = new TimerExecutionEventArgs(Guid.NewGuid(), DateTime.UtcNow, 0);
            Resumed?.Invoke(this, args);
        }

        private async Task OnExecutionStartedAsync(TimerExecutionEventArgs args)
        {
            ExecutionStarted?.Invoke(this, args);

            // Add to execution history
            lock (_executionHistory)
            {
                _executionHistory.Enqueue(args);

                // Keep only recent executions
                while (_executionHistory.Count > _options.MaxExecutionHistory)
                {
                    _executionHistory.Dequeue();
                }
            }
        }

        private async Task OnExecutionCompletedAsync(TimerExecutionEventArgs args)
        {
            ExecutionCompleted?.Invoke(this, args);

            if (_options.EnableMetrics)
            {
                UpdateExecutionMetrics(args);
            }
        }

        private async Task OnExecutionErrorAsync(TimerErrorEventArgs errorArgs)
        {
            // Convert to legacy event args for compatibility
            var legacyArgs = new TimerExecutionErrorEventArgs(
                errorArgs.Error,
                errorArgs.ConsecutiveErrors,
                (TimerState)errorArgs.Status,
                errorArgs.ExecutionId)
            {
                ShouldTriggerCircuitBreaker = errorArgs.ShouldTriggerCircuitBreaker
            };

            ExecutionError?.Invoke(this, legacyArgs);
        }

        private async Task OnStateChangedAsync(TimerStatus previousStatus, TimerStatus newStatus, string reason)
        {
            var args = new TimerStateChangedEventArgs((TimerState)previousStatus, (TimerState)newStatus, reason);
            StateChanged?.Invoke(this, args);
        }

        private void UpdateExecutionMetrics(TimerExecutionEventArgs args)
        {
            _metrics.TotalExecutions++;

            if (args.Success)
            {
                _metrics.SuccessfulExecutions++;
            }
            else
            {
                _metrics.FailedExecutions++;
            }

            // Update duration metrics
            if (_metrics.TotalExecutions == 1)
            {
                _metrics.AverageExecutionDuration = args.Duration;
                _metrics.MinExecutionDuration = args.Duration;
                _metrics.MaxExecutionDuration = args.Duration;
            }
            else
            {
                _metrics.AverageExecutionDuration = TimeSpan.FromMilliseconds(
                    (_metrics.AverageExecutionDuration.TotalMilliseconds * (_metrics.TotalExecutions - 1) + args.Duration.TotalMilliseconds) / _metrics.TotalExecutions);

                _metrics.MinExecutionDuration = TimeSpan.FromMilliseconds(
                    Math.Min(_metrics.MinExecutionDuration.TotalMilliseconds, args.Duration.TotalMilliseconds));

                _metrics.MaxExecutionDuration = TimeSpan.FromMilliseconds(
                    Math.Max(_metrics.MaxExecutionDuration.TotalMilliseconds, args.Duration.TotalMilliseconds));
            }
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the timer and releases all resources
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Stop the timer if it's running
                    try
                    {
                        StopAsync().Wait(_options.DisposalGracePeriod);
                    }
                    catch
                    {
                        // Ignore errors during disposal
                    }

                    // Dispose managed resources
                    _timer?.Dispose();
                    _executionSemaphore?.Dispose();
                    _stateChangeSemaphore?.Dispose();
                    _globalCancellationTokenSource?.Dispose();
                    _currentExecutionCancellationTokenSource?.Dispose();
                    _uptimeStopwatch?.Stop();
                }

                _disposed = true;
                Disposed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerExecutor()
        {
            Dispose(false);
        }

        #endregion
    }
}