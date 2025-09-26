using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Defines the contract for the license check scheduler that coordinates periodic license monitoring operations
    /// </summary>
    public interface ILicenseCheckScheduler
    {
        /// <summary>
        /// Gets the current status of the license check scheduler
        /// </summary>
        SchedulerStatus Status { get; }

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently executing operations
        /// </summary>
        bool IsExecuting { get; }

        /// <summary>
        /// Gets the current scheduler interval
        /// </summary>
        TimeSpan CurrentInterval { get; }

        /// <summary>
        /// Gets the number of operations currently in the queue
        /// </summary>
        int QueuedOperations { get; }

        /// <summary>
        /// Gets the number of operations currently executing
        /// </summary>
        int ExecutingOperations { get; }

        /// <summary>
        /// Gets the total number of operations completed
        /// </summary>
        long TotalOperationsCompleted { get; }

        /// <summary>
        /// Gets the total number of operations failed
        /// </summary>
        long TotalOperationsFailed { get; }

        /// <summary>
        /// Event raised when license check scheduler starts
        /// </summary>
        event EventHandler<LicenseCheckSchedulerEventArgs> SchedulerStarted;

        /// <summary>
        /// Event raised when license check scheduler stops
        /// </summary>
        event EventHandler<LicenseCheckSchedulerEventArgs> SchedulerStopped;

        /// <summary>
        /// Event raised when a license check operation starts
        /// </summary>
        event EventHandler<LicenseCheckOperationEventArgs> OperationStarted;

        /// <summary>
        /// Event raised when a license check operation completes
        /// </summary>
        event EventHandler<LicenseCheckOperationEventArgs> OperationCompleted;

        /// <summary>
        /// Event raised when a license check operation fails
        /// </summary>
        event EventHandler<LicenseCheckOperationEventArgs> OperationFailed;

        /// <summary>
        /// Event raised when scheduler status changes
        /// </summary>
        event EventHandler<SchedulerStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// Starts the license check scheduler with the default interval
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the license check scheduler with the specified interval
        /// </summary>
        /// <param name="interval">The interval between license checks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync(TimeSpan interval, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the license check scheduler gracefully
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the stop operation</returns>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses the license check scheduler
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the pause operation</returns>
        Task PauseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes the license check scheduler
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the resume operation</returns>
        Task ResumeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the scheduler interval while running
        /// </summary>
        /// <param name="newInterval">The new interval between license checks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the update operation</returns>
        Task UpdateIntervalAsync(TimeSpan newInterval, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a license check operation immediately
        /// </summary>
        /// <param name="operationType">Type of operation to execute</param>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the execution operation</returns>
        Task<LicenseCheckResult> ExecuteNowAsync(LicenseCheckOperationType operationType, string server, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a license check operation immediately with custom parameters
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the execution operation</returns>
        Task<LicenseCheckResult> ExecuteNowAsync(LicenseCheckOperation operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Schedules a one-time license check operation
        /// </summary>
        /// <param name="operation">The operation to schedule</param>
        /// <param name="delay">Delay before executing the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the scheduling operation</returns>
        Task ScheduleOneTimeAsync(LicenseCheckOperation operation, TimeSpan delay, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a recurring license check operation to the scheduler
        /// </summary>
        /// <param name="operation">The operation to add</param>
        /// <param name="interval">The interval for recurring execution</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the add operation</returns>
        Task AddRecurringOperationAsync(LicenseCheckOperation operation, TimeSpan interval, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a recurring license check operation from the scheduler
        /// </summary>
        /// <param name="operationId">The ID of the operation to remove</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the remove operation</returns>
        Task RemoveRecurringOperationAsync(Guid operationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all scheduled operations
        /// </summary>
        /// <returns>Collection of scheduled operations</returns>
        IEnumerable<LicenseCheckOperation> GetScheduledOperations();

        /// <summary>
        /// Gets the operation history
        /// </summary>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>Collection of completed operations</returns>
        IEnumerable<LicenseCheckResult> GetOperationHistory(int maxResults = 100);

        /// <summary>
        /// Gets performance metrics for the scheduler
        /// </summary>
        /// <returns>Scheduler performance metrics</returns>
        LicenseCheckSchedulerMetrics GetMetrics();

        /// <summary>
        /// Resets scheduler statistics and counters
        /// </summary>
        void ResetStatistics();

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        List<string> ValidateConfiguration();

        /// <summary>
        /// Gets the current queue status
        /// </summary>
        /// <returns>Queue status information</returns>
        LicenseCheckQueueStatus GetQueueStatus();
    }
}