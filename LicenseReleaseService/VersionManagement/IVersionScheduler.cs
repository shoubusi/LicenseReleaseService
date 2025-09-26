using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Defines the contract for multi-version scheduling with TimerExecution integration
    /// </summary>
    public interface IVersionScheduler
    {
        /// <summary>
        /// Gets the current status of the version scheduler
        /// </summary>
        VersionSchedulerStatus Status { get; }

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the number of currently scheduled operations
        /// </summary>
        int ScheduledOperationCount { get; }

        /// <summary>
        /// Gets the number of queued operations
        /// </summary>
        int QueuedOperationCount { get; }

        /// <summary>
        /// Gets the number of active version timers
        /// </summary>
        int ActiveTimerCount { get; }

        /// <summary>
        /// Event raised when an operation is scheduled
        /// </summary>
        event EventHandler<VersionScheduledEventArgs> OperationScheduled;

        /// <summary>
        /// Event raised when an operation starts execution
        /// </summary>
        event EventHandler<VersionOperationEventArgs> OperationStarted;

        /// <summary>
        /// Event raised when an operation completes
        /// </summary>
        event EventHandler<VersionOperationEventArgs> OperationCompleted;

        /// <summary>
        /// Event raised when an operation encounters an error
        /// </summary>
        event EventHandler<VersionOperationErrorEventArgs> OperationError;

        /// <summary>
        /// Event raised when scheduler status changes
        /// </summary>
        event EventHandler<VersionSchedulerStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// Starts the version scheduler
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the version scheduler
        /// </summary>
        /// <returns>Task representing the stop operation</returns>
        Task StopAsync();

        /// <summary>
        /// Schedules a recurring operation for a specific version
        /// </summary>
        /// <param name="operationType">Type of operation to schedule</param>
        /// <param name="version">Version to schedule operation for</param>
        /// <param name="interval">Interval between operations</param>
        /// <param name="parameters">Additional parameters for the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation ID for the scheduled operation</returns>
        Task<string> ScheduleOperationAsync(
            VersionOperationType operationType,
            string version,
            TimeSpan interval,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Schedules a one-time operation for a specific version
        /// </summary>
        /// <param name="operationType">Type of operation to schedule</param>
        /// <param name="version">Version to schedule operation for</param>
        /// <param name="delay">Delay before operation execution</param>
        /// <param name="parameters">Additional parameters for the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation ID for the scheduled operation</returns>
        Task<string> ScheduleOneTimeOperationAsync(
            VersionOperationType operationType,
            string version,
            TimeSpan delay,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a previously scheduled operation
        /// </summary>
        /// <param name="operationId">ID of the operation to cancel</param>
        /// <returns>True if the operation was successfully cancelled, false otherwise</returns>
        Task<bool> CancelScheduledOperationAsync(string operationId);

        /// <summary>
        /// Gets all scheduled operations, optionally filtered by version
        /// </summary>
        /// <param name="version">Optional version to filter by</param>
        /// <returns>List of scheduled operations</returns>
        Task<List<ScheduledVersionOperation>> GetScheduledOperationsAsync(string version = null);

        /// <summary>
        /// Gets scheduler metrics and performance data
        /// </summary>
        /// <returns>Scheduler metrics</returns>
        Task<VersionSchedulerMetrics> GetMetricsAsync();

        /// <summary>
        /// Executes an operation immediately for a specific version
        /// </summary>
        /// <param name="operationType">Type of operation to execute</param>
        /// <param name="version">Version to execute operation for</param>
        /// <param name="parameters">Additional parameters for the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the execution operation</returns>
        Task ExecuteOperationAsync(
            VersionOperationType operationType,
            string version,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default);
    }
}