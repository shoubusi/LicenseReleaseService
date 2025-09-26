using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Defines the contract for timer-based execution of periodic operations
    /// </summary>
    public interface ITimerExecutionService
    {
        /// <summary>
        /// Gets the current status of the timer execution service
        /// </summary>
        TimerState State { get; }

        /// <summary>
        /// Gets a value indicating whether the timer is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets a value indicating whether the timer is currently executing an operation
        /// </summary>
        bool IsExecuting { get; }

        /// <summary>
        /// Gets the current timer interval
        /// </summary>
        TimeSpan CurrentInterval { get; }

        /// <summary>
        /// Event raised when timer execution starts
        /// </summary>
        event EventHandler<TimerExecutionEventArgs> ExecutionStarted;

        /// <summary>
        /// Event raised when timer execution completes
        /// </summary>
        event EventHandler<TimerExecutionEventArgs> ExecutionCompleted;

        /// <summary>
        /// Event raised when timer execution encounters an error
        /// </summary>
        event EventHandler<TimerExecutionErrorEventArgs> ExecutionError;

        /// <summary>
        /// Event raised when timer state changes
        /// </summary>
        event EventHandler<TimerStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Starts the timer with the specified interval
        /// </summary>
        /// <param name="interval">The interval between timer executions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        void Start(TimeSpan interval, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the timer for a one-time execution
        /// </summary>
        /// <param name="delay">The delay before execution</param>
        /// <param name="cancellationToken">Cancellation token</param>
        void StartOneTime(TimeSpan delay, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the timer execution
        /// </summary>
        void Stop();

        /// <summary>
        /// Pauses the timer execution
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes the timer execution
        /// </summary>
        void Resume();

        /// <summary>
        /// Updates the timer interval while running
        /// </summary>
        /// <param name="newInterval">The new interval between timer executions</param>
        void UpdateInterval(TimeSpan newInterval);

        /// <summary>
        /// Executes the timer operation immediately
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the execution operation</returns>
        Task ExecuteNowAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets performance metrics for the timer execution
        /// </summary>
        /// <returns>Timer performance metrics</returns>
        TimerPerformanceMetrics GetMetrics();

        /// <summary>
        /// Resets the timer statistics and error count
        /// </summary>
        void ResetStatistics();
    }
}