using System;
using System.Collections.Generic;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Represents a version operation to be executed
    /// </summary>
    public class VersionOperation
    {
        /// <summary>
        /// Gets or sets the operation ID
        /// </summary>
        public Guid OperationId { get; set; }

        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public VersionOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the version to operate on
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the operation requirements
        /// </summary>
        public VersionOperationRequirements Requirements { get; set; }

        /// <summary>
        /// Gets or sets the operation priority
        /// </summary>
        public AllocationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the operation timeout
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the VersionOperation class
        /// </summary>
        public VersionOperation()
        {
            OperationId = Guid.NewGuid();
        }

        /// <summary>
        /// Initializes a new instance of the VersionOperation class with specified parameters
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="version">Version to operate on</param>
        /// <param name="requirements">Operation requirements</param>
        public VersionOperation(
            VersionOperationType operationType,
            string version,
            VersionOperationRequirements requirements)
        {
            OperationId = Guid.NewGuid();
            OperationType = operationType;
            Version = version;
            Requirements = requirements;
        }
    }

    /// <summary>
    /// Represents requirements for a version operation
    /// </summary>
    public class VersionOperationRequirements
    {
        /// <summary>
        /// Gets or sets the type of operation
        /// </summary>
        public VersionOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the memory requirements in bytes
        /// </summary>
        public long MemoryRequirements { get; set; }

        /// <summary>
        /// Gets or sets the CPU requirements as percentage
        /// </summary>
        public int CpuRequirements { get; set; }

        /// <summary>
        /// Gets or sets the maximum concurrent operations
        /// </summary>
        public int MaxConcurrentOperations { get; set; }

        /// <summary>
        /// Gets or sets the operation priority
        /// </summary>
        public AllocationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the operation timeout
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets the expected duration
        /// </summary>
        public TimeSpan ExpectedDuration { get; set; }

        /// <summary>
        /// Gets or sets additional requirements
        /// </summary>
        public Dictionary<string, object> AdditionalRequirements { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the VersionOperationRequirements class
        /// </summary>
        public VersionOperationRequirements()
        {
            Timeout = TimeSpan.FromMinutes(5);
            ExpectedDuration = TimeSpan.FromSeconds(30);
            Priority = AllocationPriority.Normal;
            MaxConcurrentOperations = 1;
        }
    }

    /// <summary>
    /// Result of version selection
    /// </summary>
    public class VersionSelectionResult
    {
        /// <summary>
        /// Gets or sets whether the selection was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if selection failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the selected version
        /// </summary>
        public SolidWorksVersionInfo SelectedVersion { get; set; }

        /// <summary>
        /// Gets or sets the runtime context for the selected version
        /// </summary>
        public VersionRuntimeContext RuntimeContext { get; set; }

        /// <summary>
        /// Gets or sets the selection criteria
        /// </summary>
        public Dictionary<string, object> SelectionCriteria { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets when the selection was made
        /// </summary>
        public DateTime SelectionTime { get; set; }
    }

    /// <summary>
    /// Result of version operation execution
    /// </summary>
    public class VersionOperationResult
    {
        /// <summary>
        /// Gets or sets the operation ID
        /// </summary>
        public Guid OperationId { get; set; }

        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public VersionOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets when the operation started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets when the operation ended
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the operation duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the result data
        /// </summary>
        public Dictionary<string, object> ResultData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Result of core operation execution
    /// </summary>
    public class CoreOperationResult
    {
        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the result data
        /// </summary>
        public Dictionary<string, object> ResultData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Result of runtime manager initialization
    /// </summary>
    public class RuntimeInitializationResult
    {
        /// <summary>
        /// Gets or sets whether the license manager was initialized
        /// </summary>
        public bool LicenseManagerInitialized { get; set; }

        /// <summary>
        /// Gets or sets the initialization time
        /// </summary>
        public TimeSpan InitializationTime { get; set; }

        /// <summary>
        /// Gets or sets the number of active versions
        /// </summary>
        public int ActiveVersionCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of versions
        /// </summary>
        public int TotalVersionCount { get; set; }

        /// <summary>
        /// Gets or sets the initialized versions
        /// </summary>
        public List<SolidWorksVersionInfo> InitializedVersions { get; set; } = new List<SolidWorksVersionInfo>();

        /// <summary>
        /// Gets or sets the errors encountered during initialization
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of runtime health check
    /// </summary>
    public class RuntimeHealthResult
    {
        /// <summary>
        /// Gets or sets the version health information
        /// </summary>
        public List<VersionRuntimeHealth> VersionHealth { get; set; } = new List<VersionRuntimeHealth>();

        /// <summary>
        /// Gets or sets the number of healthy versions
        /// </summary>
        public int HealthyVersionCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of versions
        /// </summary>
        public int TotalVersionCount { get; set; }

        /// <summary>
        /// Gets or sets the overall health level
        /// </summary>
        public RuntimeHealthLevel OverallHealth { get; set; }

        /// <summary>
        /// Gets or sets system-wide issues
        /// </summary>
        public List<SystemWideRuntimeIssue> SystemWideIssues { get; set; } = new List<SystemWideRuntimeIssue>();
    }

    /// <summary>
    /// Result of runtime version refresh
    /// </summary>
    public class RuntimeRefreshResult
    {
        /// <summary>
        /// Gets or sets whether the refresh was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if refresh failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the refresh time
        /// </summary>
        public TimeSpan RefreshTime { get; set; }

        /// <summary>
        /// Gets or sets the added versions
        /// </summary>
        public List<SolidWorksVersionInfo> AddedVersions { get; set; } = new List<SolidWorksVersionInfo>();

        /// <summary>
        /// Gets or sets the removed versions
        /// </summary>
        public List<SolidWorksVersionInfo> RemovedVersions { get; set; } = new List<SolidWorksVersionInfo>();
    }

    /// <summary>
    /// Result of runtime metrics query
    /// </summary>
    public class RuntimeMetricsResult
    {
        /// <summary>
        /// Gets or sets the version metrics
        /// </summary>
        public List<VersionRuntimeMetrics> VersionMetrics { get; set; } = new List<VersionRuntimeMetrics>();

        /// <summary>
        /// Gets or sets the total number of operations
        /// </summary>
        public long TotalOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of successful operations
        /// </summary>
        public long SuccessfulOperations { get; set; }

        /// <summary>
        /// Gets or sets the number of failed operations
        /// </summary>
        public long FailedOperations { get; set; }

        /// <summary>
        /// Gets or sets the average operation time
        /// </summary>
        public TimeSpan AverageOperationTime { get; set; }

        /// <summary>
        /// Gets or sets the peak memory usage
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the total CPU time
        /// </summary>
        public TimeSpan TotalCpuTime { get; set; }

        /// <summary>
        /// Gets or sets the success rate
        /// </summary>
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// Represents a system-wide runtime issue
    /// </summary>
    public class SystemWideRuntimeIssue
    {
        /// <summary>
        /// Gets or sets the issue type
        /// </summary>
        public string IssueType { get; set; }

        /// <summary>
        /// Gets or sets the number of affected versions
        /// </summary>
        public int AffectedVersionCount { get; set; }

        /// <summary>
        /// Gets or sets the issue severity
        /// </summary>
        public RuntimeIssueSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the issue description
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Represents an applied optimization
    /// </summary>
    public class AppliedOptimization
    {
        /// <summary>
        /// Gets or sets the strategy name
        /// </summary>
        public string StrategyName { get; set; }

        /// <summary>
        /// Gets or sets the optimization type
        /// </summary>
        public OptimizationType Type { get; set; }

        /// <summary>
        /// Gets or sets whether the optimization was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the optimization parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets when the optimization was applied
        /// </summary>
        public DateTime AppliedAt { get; set; }

        /// <summary>
        /// Gets or sets the optimization duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the performance impact (0.0 to 1.0)
        /// </summary>
        public double PerformanceImpact { get; set; }

        /// <summary>
        /// Gets or sets the error message if optimization failed
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Represents a needed optimization
    /// </summary>
    public class NeededOptimization
    {
        /// <summary>
        /// Gets or sets the optimization type
        /// </summary>
        public OptimizationType Type { get; set; }

        /// <summary>
        /// Gets or sets the optimization priority
        /// </summary>
        public OptimizationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the reason for the optimization
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the optimization parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents an optimization recommendation
    /// </summary>
    public class OptimizationRecommendation
    {
        /// <summary>
        /// Gets or sets the optimization type
        /// </summary>
        public OptimizationType Type { get; set; }

        /// <summary>
        /// Gets or sets the recommendation priority
        /// </summary>
        public OptimizationPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the recommendation title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the recommendation description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the recommended action
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the estimated impact
        /// </summary>
        public string EstimatedImpact { get; set; }
    }

    /// <summary>
    /// Optimization types
    /// </summary>
    public enum OptimizationType
    {
        /// <summary>
        /// Performance tuning optimization
        /// </summary>
        PerformanceTuning,

        /// <summary>
        /// Memory optimization
        /// </summary>
        MemoryOptimization,

        /// <summary>
        /// CPU optimization
        /// </summary>
        CpuOptimization,

        /// <summary>
        /// Reliability improvement
        /// </summary>
        ReliabilityImprovement,

        /// <summary>
        /// Query optimization
        /// </summary>
        QueryOptimization,

        /// <summary>
        /// Resource optimization
        /// </summary>
        ResourceOptimization,

        /// <summary>
        /// Release optimization
        /// </summary>
        ReleaseOptimization,

        /// <summary>
        /// Cleanup optimization
        /// </summary>
        CleanupOptimization,

        /// <summary>
        /// Health optimization
        /// </summary>
        HealthOptimization,

        /// <summary>
        /// Monitoring optimization
        /// </summary>
        MonitoringOptimization,

        /// <summary>
        /// Maintenance optimization
        /// </summary>
        MaintenanceOptimization,

        /// <summary>
        /// Priority optimization
        /// </summary>
        PriorityOptimization,

        /// <summary>
        /// Response time optimization
        /// </summary>
        ResponseTimeOptimization,

        /// <summary>
        /// Generic optimization
        /// </summary>
        Generic
    }

    /// <summary>
    /// Optimization priority levels
    /// </summary>
    public enum OptimizationPriority
    {
        /// <summary>
        /// Low priority
        /// </summary>
        Low,

        /// <summary>
        /// Normal priority
        /// </summary>
        Normal,

        /// <summary>
        /// Medium priority
        /// </summary>
        Medium,

        /// <summary>
        /// High priority
        /// </summary>
        High,

        /// <summary>
        /// Critical priority
        /// </summary>
        Critical
    }
}