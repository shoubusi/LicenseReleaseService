using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Defines the contract for license check scheduler that integrates with timer framework for periodic license checks
    /// </summary>
    public interface ILicenseCheckScheduler
    {
        #region Events

        /// <summary>
        /// Event raised when license check execution starts
        /// </summary>
        event EventHandler<LicenseCheckEventArgs> CheckStarted;

        /// <summary>
        /// Event raised when license check execution completes
        /// </summary>
        event EventHandler<LicenseCheckEventArgs> CheckCompleted;

        /// <summary>
        /// Event raised when license check encounters an error
        /// </summary>
        event EventHandler<LicenseCheckErrorEventArgs> CheckError;

        /// <summary>
        /// Event raised when license check queue status changes
        /// </summary>
        event EventHandler<LicenseCheckQueueEventArgs> QueueStatusChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current status of the license check scheduler
        /// </summary>
        LicenseCheckSchedulerStatus Status { get; }

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently executing a license check
        /// </summary>
        bool IsExecuting { get; }

        /// <summary>
        /// Gets the current check interval
        /// </summary>
        TimeSpan CurrentInterval { get; }

        /// <summary>
        /// Gets the number of pending operations in the queue
        /// </summary>
        int PendingOperationsCount { get; }

        /// <summary>
        /// Gets the license check queue statistics
        /// </summary>
        LicenseCheckQueueStatistics QueueStatistics { get; }

        /// <summary>
        /// Gets the scheduler performance metrics
        /// </summary>
        LicenseCheckSchedulerMetrics Metrics { get; }

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Starts the license check scheduler with the specified interval
        /// </summary>
        /// <param name="interval">The interval between license checks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync(TimeSpan interval, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the license check scheduler
        /// </summary>
        /// <returns>Task representing the stop operation</returns>
        Task StopAsync();

        /// <summary>
        /// Pauses the license check scheduler
        /// </summary>
        /// <returns>Task representing the pause operation</returns>
        Task PauseAsync();

        /// <summary>
        /// Resumes the license check scheduler
        /// </summary>
        /// <returns>Task representing the resume operation</returns>
        Task ResumeAsync();

        /// <summary>
        /// Updates the check interval while running
        /// </summary>
        /// <param name="newInterval">The new interval between license checks</param>
        /// <returns>Task representing the interval update operation</returns>
        Task UpdateIntervalAsync(TimeSpan newInterval);

        /// <summary>
        /// Executes license check immediately
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the immediate execution operation</returns>
        Task ExecuteNowAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Queue Management Methods

        /// <summary>
        /// Adds a license check operation to the queue
        /// </summary>
        /// <param name="operation">The license check operation to add</param>
        /// <param name="priority">The priority of the operation</param>
        /// <returns>Task representing the queue operation</returns>
        Task EnqueueOperationAsync(LicenseCheckOperation operation, LicenseCheckPriority priority = LicenseCheckPriority.Normal);

        /// <summary>
        /// Adds multiple license check operations to the queue
        /// </summary>
        /// <param name="operations">The license check operations to add</param>
        /// <param name="priority">The priority of the operations</param>
        /// <returns>Task representing the batch queue operation</returns>
        Task EnqueueOperationsAsync(IEnumerable<LicenseCheckOperation> operations, LicenseCheckPriority priority = LicenseCheckPriority.Normal);

        /// <summary>
        /// Removes a license check operation from the queue
        /// </summary>
        /// <param name="operationId">The ID of the operation to remove</param>
        /// <returns>Task representing the dequeue operation</returns>
        Task<bool> DequeueOperationAsync(string operationId);

        /// <summary>
        /// Clears all pending operations from the queue
        /// </summary>
        /// <returns>Task representing the clear operation</returns>
        Task ClearQueueAsync();

        /// <summary>
        /// Gets all pending operations in the queue
        /// </summary>
        /// <returns>List of pending operations</returns>
        Task<IReadOnlyList<LicenseCheckOperation>> GetPendingOperationsAsync();

        #endregion

        #region Configuration Methods

        /// <summary>
        /// Updates the license check scheduler configuration
        /// </summary>
        /// <param name="configuration">The new configuration</param>
        /// <returns>Task representing the configuration update operation</returns>
        Task UpdateConfigurationAsync(LicenseCheckSchedulerConfiguration configuration);

        /// <summary>
        /// Validates the current scheduler configuration
        /// </summary>
        /// <returns>List of validation errors, if any</returns>
        IList<string> ValidateConfiguration();

        #endregion

        #region Information Methods

        /// <summary>
        /// Gets detailed scheduler status information
        /// </summary>
        /// <returns>Detailed status information</returns>
        Task<LicenseCheckSchedulerStatusInfo> GetStatusInfoAsync();

        /// <summary>
        /// Gets the license check execution history
        /// </summary>
        /// <param name="maxItems">Maximum number of items to return</param>
        /// <returns>List of recent license check events</returns>
        Task<IReadOnlyList<LicenseCheckEventArgs>> GetExecutionHistoryAsync(int maxItems = 10);

        /// <summary>
        /// Gets diagnostic information for troubleshooting
        /// </summary>
        /// <returns>Diagnostic information</returns>
        Task<LicenseCheckSchedulerDiagnostics> GetDiagnosticsAsync();

        /// <summary>
        /// Resets the scheduler statistics and error count
        /// </summary>
        /// <returns>Task representing the reset operation</returns>
        Task ResetStatisticsAsync();

        #endregion
    }

    /// <summary>
    /// License check scheduler status enumeration
    /// </summary>
    public enum LicenseCheckSchedulerStatus
    {
        /// <summary>
        /// Scheduler is created but not started
        /// </summary>
        Created,

        /// <summary>
        /// Scheduler is starting
        /// </summary>
        Starting,

        /// <summary>
        /// Scheduler is running and processing license checks
        /// </summary>
        Running,

        /// <summary>
        /// Scheduler is paused
        /// </summary>
        Paused,

        /// <summary>
        /// Scheduler is stopping
        /// </summary>
        Stopping,

        /// <summary>
        /// Scheduler is stopped
        /// </summary>
        Stopped,

        /// <summary>
        /// Scheduler encountered an error and is in a faulted state
        /// </summary>
        Faulted,

        /// <summary>
        /// Scheduler is being disposed
        /// </summary>
        Disposing,

        /// <summary>
        /// Scheduler has been disposed
        /// </summary>
        Disposed
    }

    /// <summary>
    /// License check priority levels
    /// </summary>
    public enum LicenseCheckPriority
    {
        /// <summary>
        /// Low priority license check
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority license check
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority license check
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority license check
        /// </summary>
        Critical = 3
    }
}