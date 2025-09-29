using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Defines the contract for managing runtime operations for multi-version SolidWorks license management
    /// </summary>
    public interface IVersionRuntimeManager
    {
        /// <summary>
        /// Gets the current status of the runtime manager
        /// </summary>
        VersionRuntimeManagerStatus Status { get; }

        /// <summary>
        /// Gets a value indicating whether the manager is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the number of active runtime contexts
        /// </summary>
        int ActiveContextCount { get; }

        /// <summary>
        /// Event raised when runtime manager status changes
        /// </summary>
        event EventHandler<VersionRuntimeManagerStatusEventArgs> StatusChanged;

        /// <summary>
        /// Event raised when a runtime context is created
        /// </summary>
        event EventHandler<VersionRuntimeContextEventArgs> ContextCreated;

        /// <summary>
        /// Event raised when a runtime context is destroyed
        /// </summary>
        event EventHandler<VersionRuntimeContextEventArgs> ContextDestroyed;

        /// <summary>
        /// Starts the runtime manager
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the runtime manager
        /// </summary>
        /// <returns>Task representing the stop operation</returns>
        Task StopAsync();

        /// <summary>
        /// Creates a runtime context for a specific version
        /// </summary>
        /// <param name="version">The version to create context for</param>
        /// <param name="parameters">Additional parameters</param>
        /// <returns>Task representing the create operation</returns>
        Task<VersionRuntimeContext> CreateContextAsync(string version, Dictionary<string, object> parameters = null);

        /// <summary>
        /// Destroys a runtime context
        /// </summary>
        /// <param name="contextId">ID of the context to destroy</param>
        /// <returns>Task representing the destroy operation</returns>
        Task DestroyContextAsync(string contextId);

        /// <summary>
        /// Gets all active runtime contexts
        /// </summary>
        /// <returns>List of active runtime contexts</returns>
        Task<List<VersionRuntimeContext>> GetActiveContextsAsync();

        /// <summary>
        /// Gets runtime metrics and performance data
        /// </summary>
        /// <returns>Runtime metrics</returns>
        Task<VersionRuntimeManagerMetrics> GetMetricsAsync();
    }

    /// <summary>
    /// Defines status values for version runtime manager
    /// </summary>
    public enum VersionRuntimeManagerStatus
    {
        Stopped = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Error = 4
    }

    /// <summary>
    /// Event arguments for version runtime manager status changes
    /// </summary>
    public class VersionRuntimeManagerStatusEventArgs : EventArgs
    {
        public VersionRuntimeManagerStatus OldStatus { get; set; }
        public VersionRuntimeManagerStatus NewStatus { get; set; }
        public DateTime Timestamp { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Event arguments for version runtime context events
    /// </summary>
    public class VersionRuntimeContextEventArgs : EventArgs
    {
        public string ContextId { get; set; }
        public string Version { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    /// <summary>
    /// Runtime metrics for version management (interface version)
    /// </summary>
    public class VersionRuntimeManagerMetrics
    {
        public int TotalContextsCreated { get; set; }
        public int ActiveContextCount { get; set; }
        public double AverageContextLifetime { get; set; }
        public int TotalOperationsExecuted { get; set; }
        public double SuccessRate { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}