using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.TimerExecution;
using LicenseReleaseService.IdleDetection.Models;
using LicenseReleaseService.IdleDetection.Events;
using LicenseReleaseService.IdleDetection.Collections;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Integrates idle detection with the timer execution service
    /// </summary>
    public class TimerExecutionIntegration : IDisposable
    {
        private readonly ITimerExecutionService _timerExecutionService;
        private readonly ConfigurationIntegration _configurationIntegration;
        private readonly object _lock = new object();
        private bool _isDisposed;
        private Dictionary<string, TimerRegistration> _timerRegistrations;
        private Queue<IdleDetectionTask> _taskQueue;
        private Task _processingTask;
        private CancellationTokenSource _processingCancellationTokenSource;
        private bool _isProcessing;

        /// <summary>
        /// Event raised when idle detection timer is started
        /// </summary>
        public event EventHandler<IdleDetectionTimerEventArgs> TimerStarted;

        /// <summary>
        /// Event raised when idle detection timer is stopped
        /// </summary>
        public event EventHandler<IdleDetectionTimerEventArgs> TimerStopped;

        /// <summary>
        /// Event raised when idle detection task is executed
        /// </summary>
        public event EventHandler<IdleDetectionTaskEventArgs> TaskExecuted;

        /// <summary>
        /// Event raised when idle detection task execution fails
        /// </summary>
        public event EventHandler<IdleDetectionTaskErrorEventArgs> TaskExecutionFailed;

        /// <summary>
        /// Event raised when timer execution state changes
        /// </summary>
        public event EventHandler<TimerStateChangedEventArgs> TimerStateChanged;

        /// <summary>
        /// Gets whether timer execution integration is enabled
        /// </summary>
        public bool IsEnabled => _configurationIntegration?.IsIdleDetectionEnabled ?? false;

        /// <summary>
        /// Gets the timer execution service
        /// </summary>
        public ITimerExecutionService TimerExecutionService => _timerExecutionService;

        /// <summary>
        /// Gets the current timer state
        /// </summary>
        public TimerState TimerState => _timerExecutionService?.State ?? TimerState.Stopped;

        /// <summary>
        /// Gets whether timers are currently running
        /// </summary>
        public bool IsRunning => _timerExecutionService?.IsRunning ?? false;

        /// <summary>
        /// Gets the registered timer registrations
        /// </summary>
        public IReadOnlyDictionary<string, TimerRegistration> TimerRegistrations
        {
            get
            {
                lock (_lock)
                {
                    return new System.Collections.ObjectModel.ReadOnlyDictionary<string, TimerRegistration>(_timerRegistrations);
                }
            }
        }

        /// <summary>
        /// Gets the current task queue
        /// </summary>
        public IReadOnlyQueue<IdleDetectionTask> TaskQueue
        {
            get
            {
                lock (_lock)
                {
                    return new ReadOnlyQueue<IdleDetectionTask>(_taskQueue);
                }
            }
        }

        /// <summary>
        /// Gets the number of pending tasks
        /// </summary>
        public int PendingTaskCount
        {
            get
            {
                lock (_lock)
                {
                    return _taskQueue?.Count ?? 0;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the TimerExecutionIntegration class
        /// </summary>
        public TimerExecutionIntegration(ITimerExecutionService timerExecutionService, ConfigurationIntegration configurationIntegration)
        {
            _timerExecutionService = timerExecutionService ?? throw new ArgumentNullException(nameof(timerExecutionService));
            _configurationIntegration = configurationIntegration ?? throw new ArgumentNullException(nameof(configurationIntegration));

            _timerRegistrations = new Dictionary<string, TimerRegistration>();
            _taskQueue = new Queue<IdleDetectionTask>();
            _processingCancellationTokenSource = new CancellationTokenSource();

            // Subscribe to timer execution events
            _timerExecutionService.ExecutionStarted += OnTimerExecutionStarted;
            _timerExecutionService.ExecutionCompleted += OnTimerExecutionCompleted;
            _timerExecutionService.ExecutionError += OnTimerExecutionError;
            _timerExecutionService.StateChanged += OnTimerStateChanged;

            // Start task processing
            StartTaskProcessing();
        }

        /// <summary>
        /// Starts idle detection timers based on configuration
        /// </summary>
        public async Task StartIdleDetectionTimersAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return;
            }

            try
            {
                var detectorConfigs = _configurationIntegration.GetEnabledDetectorConfigurations();
                if (!detectorConfigs.Any())
                {
                    return;
                }

                var startTasks = new List<Task>();

                foreach (var config in detectorConfigs)
                {
                    var startTask = StartDetectorTimerAsync(config, cancellationToken);
                    startTasks.Add(startTask);
                }

                await Task.WhenAll(startTasks);

                // Start the main idle detection timer
                var mainInterval = _configurationIntegration.DetectionInterval;
                if (mainInterval > TimeSpan.Zero)
                {
                    await _timerExecutionService.StartAsync(mainInterval, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error starting idle detection timers: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stops all idle detection timers
        /// </summary>
        public async Task StopIdleDetectionTimersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Stop the main timer
                if (_timerExecutionService.IsRunning)
                {
                    await _timerExecutionService.StopAsync();
                }

                // Cancel all timer registrations
                var registrationsToStop = new List<TimerRegistration>();

                lock (_lock)
                {
                    registrationsToStop = _timerRegistrations.Values.ToList();
                }

                foreach (var registration in registrationsToStop)
                {
                    try
                    {
                        await StopDetectorTimerAsync(registration.DetectorName, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await LogErrorAsync($"Error stopping timer for detector {registration.DetectorName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error stopping idle detection timers: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Starts a timer for a specific detector
        /// </summary>
        public async Task StartDetectorTimerAsync(IdleDetectorConfiguration config, CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!IsEnabled)
            {
                return;
            }

            try
            {
                var timerName = $"IdleDetection_{config.DetectorName}";
                var interval = config.DetectionInterval > 0 ?
                    TimeSpan.FromSeconds(config.DetectionInterval) :
                    _configurationIntegration.DetectionInterval;

                // Create timer registration
                var registration = new TimerRegistration
                {
                    DetectorName = config.DetectorName,
                    TimerName = timerName,
                    Interval = interval,
                    Configuration = config,
                    IsEnabled = true,
                    StartedAt = DateTime.UtcNow,
                    LastExecution = null,
                    ExecutionCount = 0,
                    ErrorCount = 0
                };

                // Register timer
                lock (_lock)
                {
                    if (_timerRegistrations.ContainsKey(timerName))
                    {
                        // Timer already exists, update it
                        _timerRegistrations[timerName] = registration;
                    }
                    else
                    {
                        _timerRegistrations[timerName] = registration;
                    }
                }

                // Start the timer using one-time execution
                await _timerExecutionService.StartOneTimeAsync(interval, cancellationToken);

                // Raise timer started event
                OnTimerStarted(new IdleDetectionTimerEventArgs(registration, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error starting timer for detector {config.DetectorName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stops a timer for a specific detector
        /// </summary>
        public async Task StopDetectorTimerAsync(string detectorName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(detectorName))
            {
                throw new ArgumentException("Detector name cannot be null or whitespace", nameof(detectorName));
            }

            try
            {
                var timerName = $"IdleDetection_{detectorName}";
                TimerRegistration registration = null;

                lock (_lock)
                {
                    if (_timerRegistrations.TryGetValue(timerName, out var reg))
                    {
                        registration = reg.Clone();
                        registration.IsEnabled = false;
                        registration.StoppedAt = DateTime.UtcNow;
                        _timerRegistrations[timerName] = registration;
                    }
                }

                if (registration != null)
                {
                    // Raise timer stopped event
                    OnTimerStopped(new IdleDetectionTimerEventArgs(registration, DateTime.UtcNow));
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error stopping timer for detector {detectorName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Schedules an idle detection task for execution
        /// </summary>
        public async Task ScheduleIdleDetectionTaskAsync(IdleDetectionTask task, CancellationToken cancellationToken = default)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (!IsEnabled)
            {
                return;
            }

            try
            {
                lock (_lock)
                {
                    _taskQueue.Enqueue(task);
                }

                // If processing is not running, start it
                if (!_isProcessing)
                {
                    StartTaskProcessing();
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error scheduling idle detection task: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Schedules multiple idle detection tasks for execution
        /// </summary>
        public async Task ScheduleIdleDetectionTasksAsync(IEnumerable<IdleDetectionTask> tasks, CancellationToken cancellationToken = default)
        {
            if (tasks == null)
            {
                throw new ArgumentNullException(nameof(tasks));
            }

            if (!IsEnabled)
            {
                return;
            }

            try
            {
                var tasksList = tasks.ToList();
                if (!tasksList.Any())
                {
                    return;
                }

                lock (_lock)
                {
                    foreach (var task in tasksList)
                    {
                        _taskQueue.Enqueue(task);
                    }
                }

                // If processing is not running, start it
                if (!_isProcessing)
                {
                    StartTaskProcessing();
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error scheduling idle detection tasks: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes an idle detection task immediately
        /// </summary>
        public async Task ExecuteIdleDetectionTaskAsync(IdleDetectionTask task, CancellationToken cancellationToken = default)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (!IsEnabled)
            {
                return;
            }

            try
            {
                var startTime = DateTime.UtcNow;
                var result = new IdleDetectionTaskResult
                {
                    TaskId = task.TaskId,
                    DetectorName = task.DetectorName,
                    SessionId = task.SessionId,
                    StartTime = startTime,
                    Success = false
                };

                try
                {
                    // Execute the task
                    if (task.TaskFunc != null)
                    {
                        var taskResult = await task.TaskFunc(cancellationToken);
                        result.Results = taskResult;
                        result.Success = true;
                    }
                    else
                    {
                        result.ErrorMessage = "Task function is null";
                        result.Success = false;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = ex.Message;
                    result.Exception = ex;
                    result.Success = false;

                    // Raise task execution failed event
                    OnTaskExecutionFailed(new IdleDetectionTaskErrorEventArgs(task, ex, DateTime.UtcNow));
                }
                finally
                {
                    result.EndTime = DateTime.UtcNow;
                    result.Duration = result.EndTime - result.StartTime;

                    // Update timer registration statistics
                    UpdateTimerRegistrationStatistics(task.DetectorName, result.Success, result.Duration);

                    // Raise task executed event
                    OnTaskExecuted(new IdleDetectionTaskEventArgs(task, result, DateTime.UtcNow));
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error executing idle detection task: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets timer execution statistics
        /// </summary>
        public TimerExecutionStatistics GetTimerExecutionStatistics()
        {
            try
            {
                var stats = new TimerExecutionStatistics
                {
                    IsEnabled = IsEnabled,
                    IsRunning = IsRunning,
                    TimerState = TimerState,
                    PendingTaskCount = PendingTaskCount,
                    TotalTimerRegistrations = _timerRegistrations.Count,
                    EnabledTimerRegistrations = _timerRegistrations.Values.Count(r => r.IsEnabled),
                    TotalExecutions = _timerRegistrations.Values.Sum(r => r.ExecutionCount),
                    TotalErrors = _timerRegistrations.Values.Sum(r => r.ErrorCount),
                    AverageExecutionTime = TimeSpan.Zero,
                    LastExecutionTime = null
                };

                // Calculate average execution time
                var totalExecutionTime = _timerRegistrations.Values
                    .Where(r => r.TotalExecutionTime > TimeSpan.Zero)
                    .Sum(r => r.TotalExecutionTime.TotalMilliseconds);

                var completedExecutions = _timerRegistrations.Values.Sum(r => r.ExecutionCount);
                if (completedExecutions > 0)
                {
                    stats.AverageExecutionTime = TimeSpan.FromMilliseconds(totalExecutionTime / completedExecutions);
                }

                // Get last execution time
                var lastExecution = _timerRegistrations.Values
                    .Where(r => r.LastExecution.HasValue)
                    .OrderByDescending(r => r.LastExecution.Value)
                    .FirstOrDefault();

                if (lastExecution != null)
                {
                    stats.LastExecutionTime = lastExecution.LastExecution;
                }

                // Add timer service metrics
                if (_timerExecutionService != null)
                {
                    var timerMetrics = _timerExecutionService.GetMetrics();
                    stats.TimerMetrics = timerMetrics;
                }

                return stats;
            }
            catch (Exception ex)
            {
                return new TimerExecutionStatistics
                {
                    IsEnabled = IsEnabled,
                    IsRunning = IsRunning,
                    TimerState = TimerState,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets timer registration by detector name
        /// </summary>
        public TimerRegistration GetTimerRegistration(string detectorName)
        {
            if (string.IsNullOrWhiteSpace(detectorName))
            {
                throw new ArgumentException("Detector name cannot be null or whitespace", nameof(detectorName));
            }

            var timerName = $"IdleDetection_{detectorName}";

            lock (_lock)
            {
                return _timerRegistrations.TryGetValue(timerName, out var registration) ?
                    registration.Clone() : null;
            }
        }

        /// <summary>
        /// Updates timer registration for a detector
        /// </summary>
        public async Task UpdateTimerRegistrationAsync(string detectorName, TimeSpan newInterval, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(detectorName))
            {
                throw new ArgumentException("Detector name cannot be null or whitespace", nameof(detectorName));
            }

            try
            {
                var timerName = $"IdleDetection_{detectorName}";

                lock (_lock)
                {
                    if (_timerRegistrations.TryGetValue(timerName, out var registration))
                    {
                        registration.Interval = newInterval;
                        registration.LastUpdated = DateTime.UtcNow;
                    }
                    else
                    {
                        // Create new registration
                        var config = _configurationIntegration.GetDetectorConfiguration(detectorName);
                        if (config != null)
                        {
                            await StartDetectorTimerAsync(config, cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error updating timer registration for detector {detectorName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles timer execution started events
        /// </summary>
        private void OnTimerExecutionStarted(object sender, TimerExecutionEventArgs e)
        {
            try
            {
                // Schedule idle detection task
                var task = new IdleDetectionTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    DetectorName = "Main",
                    TaskType = IdleDetectionTaskType.PeriodicDetection,
                    ScheduledTime = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    Priority = 10,
                    Timeout = _configurationIntegration?.MaxDetectionTime ?? TimeSpan.FromSeconds(30)
                };

                // Set task function
                task.TaskFunc = async (cancellationToken) =>
                {
                    // This would be implemented to call the idle detection engine
                    return new List<IdleDetectionResult>();
                };

                _ = ScheduleIdleDetectionTaskAsync(task);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error handling timer execution started: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles timer execution completed events
        /// </summary>
        private void OnTimerExecutionCompleted(object sender, TimerExecutionEventArgs e)
        {
            try
            {
                // Update timer registration statistics
                foreach (var registration in _timerRegistrations.Values.Where(r => r.IsEnabled))
                {
                    if (registration.LastExecution == null ||
                        DateTime.UtcNow - registration.LastExecution.Value > registration.Interval)
                    {
                        // Schedule next execution
                        _ = ScheduleNextExecutionAsync(registration);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error handling timer execution completed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles timer execution error events
        /// </summary>
        private void OnTimerExecutionError(object sender, TimerExecutionErrorEventArgs e)
        {
            try
            {
                await LogErrorAsync($"Timer execution error: {e.Exception.Message}");

                // Update error statistics
                foreach (var registration in _timerRegistrations.Values)
                {
                    registration.ErrorCount++;
                    registration.LastError = e.Exception.Message;
                    registration.LastErrorTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error handling timer execution error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles timer state changed events
        /// </summary>
        private void OnTimerStateChanged(object sender, TimerStateChangedEventArgs e)
        {
            try
            {
                OnTimerStateChanged(new TimerStateChangedEventArgs(e.OldState, e.NewState, e.Timestamp));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error handling timer state changed: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts task processing
        /// </summary>
        private void StartTaskProcessing()
        {
            if (_isProcessing)
            {
                return;
            }

            _isProcessing = true;
            _processingCancellationTokenSource = new CancellationTokenSource();

            _processingTask = Task.Run(() => ProcessTasksAsync(_processingCancellationTokenSource.Token));
        }

        /// <summary>
        /// Stops task processing
        /// </summary>
        private async Task StopTaskProcessingAsync()
        {
            if (!_isProcessing)
            {
                return;
            }

            try
            {
                _isProcessing = false;

                if (_processingCancellationTokenSource != null)
                {
                    _processingCancellationTokenSource.Cancel();
                    _processingCancellationTokenSource.Dispose();
                    _processingCancellationTokenSource = null;
                }

                if (_processingTask != null)
                {
                    await _processingTask;
                    _processingTask = null;
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error stopping task processing: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes queued tasks
        /// </summary>
        private async Task ProcessTasksAsync(CancellationToken cancellationToken)
        {
            while (_isProcessing && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    IdleDetectionTask task = null;

                    lock (_lock)
                    {
                        if (_taskQueue.Count > 0)
                        {
                            task = _taskQueue.Dequeue();
                        }
                    }

                    if (task != null)
                    {
                        await ExecuteIdleDetectionTaskAsync(task, cancellationToken);
                    }
                    else
                    {
                        // No tasks in queue, wait a bit
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Task was cancelled, exit processing loop
                    break;
                }
                catch (Exception ex)
                {
                    await LogErrorAsync($"Error processing task: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Wait before retrying
                }
            }
        }

        /// <summary>
        /// Schedules the next execution for a timer registration
        /// </summary>
        private async Task ScheduleNextExecutionAsync(TimerRegistration registration)
        {
            try
            {
                var task = new IdleDetectionTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    DetectorName = registration.DetectorName,
                    TaskType = IdleDetectionTaskType.DetectorSpecific,
                    ScheduledTime = DateTime.UtcNow + registration.Interval,
                    CreatedAt = DateTime.UtcNow,
                    Priority = registration.Configuration?.Priority ?? 10,
                    Timeout = registration.Configuration?.MaxDetectionTime > 0 ?
                        TimeSpan.FromMilliseconds(registration.Configuration.MaxDetectionTime) :
                        TimeSpan.FromSeconds(30)
                };

                // Set task function
                task.TaskFunc = async (cancellationToken) =>
                {
                    // This would call the specific detector
                    return new List<IdleDetectionResult>();
                };

                await ScheduleIdleDetectionTaskAsync(task);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Error scheduling next execution for {registration.DetectorName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates timer registration statistics
        /// </summary>
        private void UpdateTimerRegistrationStatistics(string detectorName, bool success, TimeSpan duration)
        {
            try
            {
                var timerName = $"IdleDetection_{detectorName}";

                lock (_lock)
                {
                    if (_timerRegistrations.TryGetValue(timerName, out var registration))
                    {
                        registration.ExecutionCount++;
                        registration.LastExecution = DateTime.UtcNow;
                        registration.TotalExecutionTime += duration;

                        if (!success)
                        {
                            registration.ErrorCount++;
                            registration.LastError = "Task execution failed";
                            registration.LastErrorTime = DateTime.UtcNow;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error updating timer registration statistics: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        private async Task LogErrorAsync(string message)
        {
            // In a real implementation, this would use the logging system
            System.Diagnostics.Trace.WriteLine($"[TimerExecutionIntegration Error] {message}");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Raises the TimerStarted event
        /// </summary>
        protected virtual void OnTimerStarted(IdleDetectionTimerEventArgs e)
        {
            TimerStarted?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the TimerStopped event
        /// </summary>
        protected virtual void OnTimerStopped(IdleDetectionTimerEventArgs e)
        {
            TimerStopped?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the TaskExecuted event
        /// </summary>
        protected virtual void OnTaskExecuted(IdleDetectionTaskEventArgs e)
        {
            TaskExecuted?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the TaskExecutionFailed event
        /// </summary>
        protected virtual void OnTaskExecutionFailed(IdleDetectionTaskErrorEventArgs e)
        {
            TaskExecutionFailed?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the TimerStateChanged event
        /// </summary>
        protected virtual void OnTimerStateChanged(TimerStateChangedEventArgs e)
        {
            TimerStateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the timer execution integration
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the timer execution integration
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Stop task processing
                    StopTaskProcessingAsync().Wait();

                    // Unsubscribe from timer execution events
                    if (_timerExecutionService != null)
                    {
                        _timerExecutionService.ExecutionStarted -= OnTimerExecutionStarted;
                        _timerExecutionService.ExecutionCompleted -= OnTimerExecutionCompleted;
                        _timerExecutionService.ExecutionError -= OnTimerExecutionError;
                        _timerExecutionService.StateChanged -= OnTimerStateChanged;
                    }

                    // Dispose cancellation token source
                    _processingCancellationTokenSource?.Dispose();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerExecutionIntegration()
        {
            Dispose(false);
        }
    }

    
    /// <summary>
    /// Represents a timer registration
    /// </summary>
    public class TimerRegistration
    {
        /// <summary>
        /// Gets or sets the detector name
        /// </summary>
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets or sets the timer name
        /// </summary>
        public string TimerName { get; set; }

        /// <summary>
        /// Gets or sets the timer interval
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Gets or sets the detector configuration
        /// </summary>
        public IdleDetectorConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets or sets whether the timer is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets when the timer was started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Gets or sets when the timer was stopped
        /// </summary>
        public DateTime? StoppedAt { get; set; }

        /// <summary>
        /// Gets or sets the last execution time
        /// </summary>
        public DateTime? LastExecution { get; set; }

        /// <summary>
        /// Gets or sets when the registration was last updated
        /// </summary>
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the execution count
        /// </summary>
        public int ExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the error count
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the total execution time
        /// </summary>
        public TimeSpan TotalExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the last error message
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// Gets or sets the last error time
        /// </summary>
        public DateTime? LastErrorTime { get; set; }

        /// <summary>
        /// Clones the timer registration
        /// </summary>
        public TimerRegistration Clone()
        {
            return new TimerRegistration
            {
                DetectorName = DetectorName,
                TimerName = TimerName,
                Interval = Interval,
                Configuration = Configuration,
                IsEnabled = IsEnabled,
                StartedAt = StartedAt,
                StoppedAt = StoppedAt,
                LastExecution = LastExecution,
                LastUpdated = LastUpdated,
                ExecutionCount = ExecutionCount,
                ErrorCount = ErrorCount,
                TotalExecutionTime = TotalExecutionTime,
                LastError = LastError,
                LastErrorTime = LastErrorTime
            };
        }
    }
}