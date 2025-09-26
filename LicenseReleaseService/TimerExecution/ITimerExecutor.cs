using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Defines the contract for timer execution with full lifecycle management
    /// </summary>
    public interface ITimerExecutor : IDisposable
    {
        #region Events

        /// <summary>
        /// Occurs when the timer starts execution
        /// </summary>
        event EventHandler<TimerExecutionEventArgs> ExecutionStarted;

        /// <summary>
        /// Occurs when the timer completes execution
        /// </summary>
        event EventHandler<TimerExecutionEventArgs> ExecutionCompleted;

        /// <summary>
        /// Occurs when the timer encounters an error during execution
        /// </summary>
        event EventHandler<TimerExecutionErrorEventArgs> ExecutionError;

        /// <summary>
        /// Occurs when the timer state changes
        /// </summary>
        event EventHandler<TimerStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Occurs when the timer is paused
        /// </summary>
        event EventHandler<TimerExecutionEventArgs> Paused;

        /// <summary>
        /// Occurs when the timer is resumed
        /// </summary>
        event EventHandler<TimerExecutionEventArgs> Resumed;

        /// <summary>
        /// Occurs when the timer is stopped
        /// </summary>
        event EventHandler<TimerExecutionEventArgs> Stopped;

        /// <summary>
        /// Occurs when the timer is disposed
        /// </summary>
        event EventHandler Disposed;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current timer status
        /// </summary>
        TimerStatus Status { get; }

        /// <summary>
        /// Gets the timer execution options
        /// </summary>
        TimerExecutionOptions Options { get; }

        /// <summary>
        /// Gets the current timer interval
        /// </summary>
        TimeSpan Interval { get; }

        /// <summary>
        /// Gets a value indicating whether the timer is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets a value indicating whether the timer is currently paused
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Gets a value indicating whether the timer has been disposed
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets the performance metrics for the timer
        /// </summary>
        TimerPerformanceMetrics Metrics { get; }

        /// <summary>
        /// Gets the number of consecutive errors encountered
        /// </summary>
        int ConsecutiveErrors { get; }

        /// <summary>
        /// Gets the last execution time
        /// </summary>
        DateTime? LastExecutionTime { get; }

        /// <summary>
        /// Gets the next scheduled execution time
        /// </summary>
        DateTime? NextExecutionTime { get; }

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Starts the timer with the default interval
        /// </summary>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync();

        /// <summary>
        /// Starts the timer with the specified interval
        /// </summary>
        /// <param name="interval">The timer interval</param>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync(TimeSpan interval);

        /// <summary>
        /// Stops the timer gracefully
        /// </summary>
        /// <returns>Task representing the stop operation</returns>
        Task StopAsync();

        /// <summary>
        /// Pauses the timer temporarily
        /// </summary>
        /// <returns>Task representing the pause operation</returns>
        Task PauseAsync();

        /// <summary>
        /// Resumes the timer from paused state
        /// </summary>
        /// <returns>Task representing the resume operation</returns>
        Task ResumeAsync();

        /// <summary>
        /// Restarts the timer with the current interval
        /// </summary>
        /// <returns>Task representing the restart operation</returns>
        Task RestartAsync();

        /// <summary>
        /// Triggers immediate execution of the timer callback
        /// </summary>
        /// <returns>Task representing the execution operation</returns>
        Task TriggerExecutionAsync();

        /// <summary>
        /// Changes the timer interval
        /// </summary>
        /// <param name="newInterval">The new timer interval</param>
        /// <returns>Task representing the interval change operation</returns>
        Task ChangeIntervalAsync(TimeSpan newInterval);

        /// <summary>
        /// Resets the timer state and clears error counts
        /// </summary>
        /// <returns>Task representing the reset operation</returns>
        Task ResetAsync();

        /// <summary>
        /// Waits for the current execution to complete
        /// </summary>
        /// <param name="timeout">The maximum time to wait</param>
        /// <returns>True if the execution completed within the timeout; otherwise, false</returns>
        Task<bool> WaitForCurrentExecutionAsync(TimeSpan timeout);

        /// <summary>
        /// Cancels the current execution if it's running
        /// </summary>
        /// <returns>Task representing the cancel operation</returns>
        Task CancelCurrentExecutionAsync();

        #endregion

        #region Configuration Methods

        /// <summary>
        /// Updates the timer execution options
        /// </summary>
        /// <param name="newOptions">The new options to apply</param>
        /// <returns>Task representing the options update operation</returns>
        Task UpdateOptionsAsync(TimerExecutionOptions newOptions);

        /// <summary>
        /// Validates the current timer configuration
        /// </summary>
        /// <returns>List of validation errors, if any</returns>
        System.Collections.Generic.List<string> ValidateConfiguration();

        #endregion

        #region Information Methods

        /// <summary>
        /// Gets detailed timer status information
        /// </summary>
        /// <returns>Detailed status information</returns>
        TimerStatusInfo GetStatusInfo();

        /// <summary>
        /// Gets the current execution history
        /// </summary>
        /// <param name="maxItems">Maximum number of items to return</param>
        /// <returns>List of recent execution events</returns>
        System.Collections.Generic.List<TimerExecutionEventArgs> GetExecutionHistory(int maxItems = 10);

        /// <summary>
        /// Gets diagnostic information for troubleshooting
        /// </summary>
        /// <returns>Diagnostic information</returns>
        TimerDiagnosticsInfo GetDiagnostics();

        #endregion
    }

    /// <summary>
    /// Detailed timer status information
    /// </summary>
    public class TimerStatusInfo
    {
        /// <summary>
        /// Gets the current timer status
        /// </summary>
        public TimerStatus Status { get; set; }

        /// <summary>
        /// Gets the current timer interval
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Gets the last execution time
        /// </summary>
        public DateTime? LastExecutionTime { get; set; }

        /// <summary>
        /// Gets the next scheduled execution time
        /// </summary>
        public DateTime? NextExecutionTime { get; set; }

        /// <summary>
        /// Gets the consecutive error count
        /// </summary>
        public int ConsecutiveErrors { get; set; }

        /// <summary>
        /// Gets the total execution count
        /// </summary>
        public long TotalExecutions { get; set; }

        /// <summary>
        /// Gets the timer uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the current state timestamp
        /// </summary>
        public DateTime StateTimestamp { get; set; }

        /// <summary>
        /// Gets the timer start time
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets the timer stop time
        /// </summary>
        public DateTime? StopTime { get; set; }
    }

    /// <summary>
    /// Diagnostic information for timer troubleshooting
    /// </summary>
    public class TimerDiagnosticsInfo
    {
        /// <summary>
        /// Gets the timer status information
        /// </summary>
        public TimerStatusInfo Status { get; set; }

        /// <summary>
        /// Gets the timer execution options
        /// </summary>
        public TimerExecutionOptions Options { get; set; }

        /// <summary>
        /// Gets the performance metrics
        /// </summary>
        public TimerPerformanceMetrics Metrics { get; set; }

        /// <summary>
        /// Gets the thread information
        /// </summary>
        public TimerThreadInfo ThreadInfo { get; set; }

        /// <summary>
        /// Gets the recent errors
        /// </summary>
        public System.Collections.Generic.List<TimerErrorInfo> RecentErrors { get; set; }

        /// <summary>
        /// Gets the memory usage information
        /// </summary>
        public TimerMemoryInfo MemoryInfo { get; set; }

        /// <summary>
        /// Gets the system information
        /// </summary>
        public TimerSystemInfo SystemInfo { get; set; }
    }

    /// <summary>
    /// Thread information for timer diagnostics
    /// </summary>
    public class TimerThreadInfo
    {
        /// <summary>
        /// Gets the managed thread ID
        /// </summary>
        public int ManagedThreadId { get; set; }

        /// <summary>
        /// Gets the thread state
        /// </summary>
        public System.Threading.ThreadState ThreadState { get; set; }

        /// <summary>
        /// Gets the thread priority
        /// </summary>
        public System.Threading.ThreadPriority ThreadPriority { get; set; }

        /// <summary>
        /// Gets whether the thread is a background thread
        /// </summary>
        public bool IsBackground { get; set; }

        /// <summary>
        /// Gets whether the thread is from the thread pool
        /// </summary>
        public bool IsThreadPoolThread { get; set; }
    }

    /// <summary>
    /// Error information for timer diagnostics
    /// </summary>
    public class TimerErrorInfo
    {
        /// <summary>
        /// Gets the error timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the error message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets the error type
        /// </summary>
        public string ErrorType { get; set; }

        /// <summary>
        /// Gets the stack trace
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Gets the execution ID if available
        /// </summary>
        public Guid? ExecutionId { get; set; }

        /// <summary>
        /// Gets the consecutive error count at the time of this error
        /// </summary>
        public int ConsecutiveErrors { get; set; }
    }

    /// <summary>
    /// Memory information for timer diagnostics
    /// </summary>
    public class TimerMemoryInfo
    {
        /// <summary>
        /// Gets the current memory usage in bytes
        /// </summary>
        public long CurrentMemoryUsage { get; set; }

        /// <summary>
        /// Gets the peak memory usage in bytes
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Gets the number of objects tracked
        /// </summary>
        public int TrackedObjects { get; set; }

        /// <summary>
        /// Gets the garbage collection information
        /// </summary>
        public string GarbageCollectionInfo { get; set; }
    }

    /// <summary>
    /// System information for timer diagnostics
    /// </summary>
    public class TimerSystemInfo
    {
        /// <summary>
        /// Gets the operating system version
        /// </summary>
        public string OSVersion { get; set; }

        /// <summary>
        /// Gets the .NET runtime version
        /// </summary>
        public string RuntimeVersion { get; set; }

        /// <summary>
        /// Gets the processor count
        /// </summary>
        public int ProcessorCount { get; set; }

        /// <summary>
        /// Gets the working set size
        /// </summary>
        public long WorkingSet { get; set; }

        /// <summary>
        /// Gets the system uptime
        /// </summary>
        public TimeSpan SystemUptime { get; set; }
    }
}