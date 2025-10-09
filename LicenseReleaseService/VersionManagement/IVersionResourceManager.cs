using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Defines the contract for managing resources in multi-version SolidWorks license management
    /// </summary>
    public interface IVersionResourceManager
    {
        /// <summary>
        /// Gets the current status of the resource manager
        /// </summary>
        VersionResourceManagerStatus Status { get; }

        /// <summary>
        /// Gets a value indicating whether the manager is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the total number of managed resources
        /// </summary>
        int TotalResourceCount { get; }

        /// <summary>
        /// Gets the number of available resources
        /// </summary>
        int AvailableResourceCount { get; }

        /// <summary>
        /// Event raised when resource manager status changes
        /// </summary>
        event EventHandler<VersionResourceManagerStatusEventArgs> StatusChanged;

        /// <summary>
        /// Event raised when a resource is allocated
        /// </summary>
        event EventHandler<VersionResourceEventArgs> ResourceAllocated;

        /// <summary>
        /// Event raised when a resource is released
        /// </summary>
        event EventHandler<VersionResourceEventArgs> ResourceReleased;

        /// <summary>
        /// Starts the resource manager
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the start operation</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the resource manager
        /// </summary>
        /// <returns>Task representing the stop operation</returns>
        Task StopAsync();

        /// <summary>
        /// Allocates resources for a specific version
        /// </summary>
        /// <param name="version">The version to allocate resources for</param>
        /// <param name="request">The allocation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Allocation result</returns>
        Task<VersionAllocationResult> AllocateResourcesAsync(string version, VersionAllocationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases resources for a specific version
        /// </summary>
        /// <param name="version">The version to release resources for</param>
        /// <param name="request">The release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Release result</returns>
        Task<VersionReleaseResult> ReleaseResourcesAsync(string version, VersionReleaseRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current resource allocation for a version
        /// </summary>
        /// <param name="version">The version to query</param>
        /// <returns>Current resource allocation</returns>
        Task<VersionResourceAllocation> GetResourceAllocationAsync(string version);

        /// <summary>
        /// Gets resource health information for a version
        /// </summary>
        /// <param name="version">The version to check</param>
        /// <returns>Health information</returns>
        Task<VersionHealthResult> GetResourceHealthAsync(string version);

        /// <summary>
        /// Gets resource metrics and performance data
        /// </summary>
        /// <returns>Resource metrics</returns>
        Task<VersionResourceMetrics> GetMetricsAsync();
    }

    /// <summary>
    /// Defines status values for version resource manager
    /// </summary>
    public enum VersionResourceManagerStatus
    {
        Stopped = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Error = 4
    }

    /// <summary>
    /// Event arguments for version resource manager status changes
    /// </summary>
    public class VersionResourceManagerStatusEventArgs : EventArgs
    {
        public VersionResourceManagerStatus OldStatus { get; set; }
        public VersionResourceManagerStatus NewStatus { get; set; }
        public DateTime Timestamp { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Event arguments for version resource events
    /// </summary>
    public class VersionResourceEventArgs : EventArgs
    {
        public string Version { get; set; }
        public string ResourceId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    /// <summary>
    /// Resource allocation request for a specific version
    /// </summary>
    public class VersionAllocationRequest
    {
        public string RequestId { get; set; }
        public string Version { get; set; }
        public int ResourceCount { get; set; }
        public TimeSpan Timeout { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    /// <summary>
    /// Multi-version allocation request
    /// </summary>
    public class MultiVersionAllocationRequest
    {
        public string RequestId { get; set; }
        public Dictionary<string, int> VersionResourceCounts { get; set; }
        public TimeSpan Timeout { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string FeatureName { get; set; }
        public int RequiredLicenses { get; set; }
    }

    /// <summary>
    /// Resource release request for a specific version
    /// </summary>
    public class VersionReleaseRequest
    {
        public string RequestId { get; set; }
        public string Version { get; set; }
        public List<string> ResourceIds { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    /// <summary>
    /// Multi-version release request
    /// </summary>
    public class MultiVersionReleaseRequest
    {
        public string RequestId { get; set; }
        public Dictionary<string, List<string>> VersionResourceIds { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string FeatureName { get; set; }
        public string User { get; set; }
        public string Version { get; set; }
    }

    /// <summary>
    /// Result of a version resource allocation
    /// </summary>
    public class VersionAllocationResult
    {
        public string RequestId { get; set; }
        public string Version { get; set; }
        public bool Success { get; set; }
        public List<string> AllocatedResourceIds { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }

        // Additional properties for license allocation
        public int AllocatedLicenses { get; set; }
        public int RemainingLicenses { get; set; }
        public string AllocationId { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Result of a version resource release
    /// </summary>
    public class VersionReleaseResult
    {
        public string RequestId { get; set; }
        public string Version { get; set; }
        public bool Success { get; set; }
        public List<string> ReleasedResourceIds { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Errors { get; set; }
        public List<ReleasedLicense> ReleasedLicenses { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Health result for version resources
    /// </summary>
    public class VersionHealthResult
    {
        public string Version { get; set; }
        public bool IsHealthy { get; set; }
        public double HealthScore { get; set; }
        public List<string> Issues { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; }
        public DateTime LastCheck { get; set; }
    }

    /// <summary>
    /// Multi-version health result
    /// </summary>
    public class MultiVersionHealthResult
    {
        public Dictionary<string, VersionHealthResult> VersionHealthResults { get; set; }
        public double OverallHealthScore { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, VersionHealth> VersionHealth { get; set; }
        public int TotalVersionCount { get; set; }
        public List<SystemWideIssue> SystemWideIssues { get; set; }
        public int HealthyVersionCount { get; set; }
        public MultiVersionHealthLevel OverallHealth { get; set; }
    }

    /// <summary>
    /// Represents a system-wide issue affecting multiple versions
    /// </summary>
    public class SystemWideIssue
    {
        public string IssueId { get; set; }
        public string Description { get; set; }
        public SystemWideIssueSeverity Severity { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<string> AffectedVersions { get; set; }
        public string IssueType { get; set; }
        public int AffectedVersionCount { get; set; }
    }

    /// <summary>
    /// Severity levels for system-wide issues
    /// </summary>
    public enum SystemWideIssueSeverity
    {
        Low,
        Medium,
        High,
        Critical,
        Warning
    }

    /// <summary>
    /// Multi-version health levels
    /// </summary>
    public enum MultiVersionHealthLevel
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    /// <summary>
    /// Query result for version-specific operations
    /// </summary>
    public class VersionSpecificQueryResult
    {
        public string QueryId { get; set; }
        public string Version { get; set; }
        public bool Success { get; set; }
        public object Result { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime QueryTimestamp { get; set; }
        public int LicenseCount { get; set; }
        public int AvailableLicenses { get; set; }
        public int UsedLicenses { get; set; }
        public int TotalLicenses { get; set; }
        public List<string> Users { get; set; }
        public bool Cached { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }
    }

    /// <summary>
    /// Current resource allocation for a version
    /// </summary>
    public class VersionResourceAllocation
    {
        public string Version { get; set; }
        public int AllocatedCount { get; set; }
        public int AvailableCount { get; set; }
        public List<string> AllocatedResourceIds { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Resource metrics for version management
    /// </summary>
    public class VersionResourceMetrics
    {
        public int TotalAllocations { get; set; }
        public int TotalReleases { get; set; }
        public double AverageAllocationTime { get; set; }
        public double AllocationSuccessRate { get; set; }
        public int CurrentActiveAllocations { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Multi-version initialization result
    /// </summary>
    public class MultiVersionInitializationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> InitializedVersions { get; set; } = new Dictionary<string, string>();
        public DateTime Timestamp { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public TimeSpan InitializationTime { get; set; }
        public int ManagedVersionCount { get; set; }
        public int TotalVersionCount { get; set; }
        public List<string> Conflicts { get; set; } = new List<string>();
    }

    /// <summary>
    /// Multi-version query result
    /// </summary>
    public class MultiVersionQueryResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> Results { get; set; } = new Dictionary<string, object>();
        public DateTime Timestamp { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();

        // Additional properties for MultiVersionLicenseManager compatibility
        public List<VersionSpecificQueryResult> VersionResults { get; set; } = new List<VersionSpecificQueryResult>();
        public int SuccessfulVersionCount { get; set; }
        public int FailedVersionCount { get; set; }
        public int TotalLicenseCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<VersionConflict> Conflicts { get; set; } = new List<VersionConflict>();
        public List<VersionConflict> ResolvedConflicts { get; set; } = new List<VersionConflict>();
        public TimeSpan QueryTime { get; set; }
    }

    /// <summary>
    /// Multi-version allocation result
    /// </summary>
    public class MultiVersionAllocationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, VersionAllocationResult> VersionResults { get; set; } = new Dictionary<string, VersionAllocationResult>();
        public DateTime Timestamp { get; set; }

        // Additional properties for MultiVersionLicenseManager compatibility
        public string AllocatedVersion { get; set; }
        public VersionAllocationResult AllocationDetails { get; set; }
        public List<VersionAllocationAttempt> FailedAttempts { get; set; } = new List<VersionAllocationAttempt>();
        public TimeSpan AllocationTime { get; set; }
    }

    /// <summary>
    /// Multi-version release result
    /// </summary>
    public class MultiVersionReleaseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, VersionReleaseResult> VersionResults { get; set; } = new Dictionary<string, VersionReleaseResult>();
        public DateTime Timestamp { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public TimeSpan ReleaseTime { get; set; }
        public List<ReleasedLicense> ReleasedLicenses { get; set; } = new List<ReleasedLicense>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Multi-version refresh result
    /// </summary>
    public class MultiVersionRefreshResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, bool> RefreshResults { get; set; } = new Dictionary<string, bool>();
        public DateTime Timestamp { get; set; }
        public List<string> AddedVersions { get; set; } = new List<string>();
        public List<string> RemovedVersions { get; set; } = new List<string>();
        public DateTime RefreshTime { get; set; }
    }
}