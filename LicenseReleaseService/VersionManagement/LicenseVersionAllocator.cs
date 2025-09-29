using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Configuration;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Manages resource allocation for concurrent license operations across multiple SolidWorks versions
    /// </summary>
    public class LicenseVersionAllocator : IDisposable
    {
        private readonly ILogger<LicenseVersionAllocator> _logger;
        private readonly Dictionary<string, VersionResourcePool> _resourcePools;
        private readonly SemaphoreSlim _allocationSemaphore;
        private readonly object _poolsLock = new object();
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        /// <summary>
        /// Gets the allocation configuration
        /// </summary>
        public ResourceAllocationConfiguration Configuration { get; }

        /// <summary>
        /// Gets the current allocation statistics
        /// </summary>
        public AllocationStatistics Statistics { get; private set; }

        /// <summary>
        /// Initializes a new instance of the LicenseVersionAllocator class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configuration">Allocation configuration</param>
        public LicenseVersionAllocator(
            ILogger<LicenseVersionAllocator> logger,
            ResourceAllocationConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _resourcePools = new Dictionary<string, VersionResourcePool>();
            _allocationSemaphore = new SemaphoreSlim(configuration.MaxConcurrentAllocations);
            Statistics = new AllocationStatistics();

            // Initialize cleanup timer
            _cleanupTimer = new Timer(CleanupExpiredAllocations, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Allocates resources for a license operation on a specific version
        /// </summary>
        /// <param name="version">Version to allocate resources for</param>
        /// <param name="queryOptions">Query options that determine resource requirements</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Allocation result</returns>
        public async Task<ResourceAllocation> AllocateAsync(
            string version,
            LicenseQueryOptions queryOptions,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (queryOptions == null)
                throw new ArgumentNullException(nameof(queryOptions));

            await _allocationSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Allocating resources for version {Version}", version);

                // Calculate resource requirements based on query options
                var requirements = CalculateResourceRequirements(version, queryOptions);

                // Get or create resource pool for the version
                var resourcePool = GetOrCreateResourcePool(version, requirements);

                // Attempt to allocate resources
                var allocation = await resourcePool.AllocateAsync(requirements, cancellationToken);

                if (allocation.Success)
                {
                    // Update statistics
                    lock (_poolsLock)
                    {
                        Statistics.TotalAllocations++;
                        Statistics.ActiveAllocations++;
                        Statistics.LastAllocationTime = DateTime.UtcNow;
                    }

                    _logger.LogDebug("Successfully allocated resources for version {Version}. AllocationId: {AllocationId}",
                        version, allocation.AllocationId);
                }
                else
                {
                    _logger.LogWarning("Failed to allocate resources for version {Version}: {Error}",
                        version, allocation.ErrorMessage);
                }

                return allocation;
            }
            finally
            {
                _allocationSemaphore.Release();
            }
        }

        /// <summary>
        /// Releases a previously allocated resource
        /// </summary>
        /// <param name="allocation">Allocation to release</param>
        /// <returns>Release result</returns>
        public async Task<ResourceReleaseResult> ReleaseAsync(ResourceAllocation allocation)
        {
            if (allocation == null)
                throw new ArgumentNullException(nameof(allocation));

            try
            {
                _logger.LogDebug("Releasing resources for allocation {AllocationId}", allocation.AllocationId);

                // Find the resource pool
                VersionResourcePool resourcePool = null;
                lock (_poolsLock)
                {
                    if (_resourcePools.TryGetValue(allocation.Version, out resourcePool))
                    {
                        // Release the allocation
                        var releaseResult = await resourcePool.ReleaseAsync(allocation);

                        // Update statistics
                        Statistics.ActiveAllocations--;
                        Statistics.TotalReleases++;
                        Statistics.LastReleaseTime = DateTime.UtcNow;

                        _logger.LogDebug("Successfully released resources for allocation {AllocationId}",
                            allocation.AllocationId);

                        return releaseResult;
                    }
                }

                return new ResourceReleaseResult
                {
                    Success = false,
                    ErrorMessage = $"Resource pool for version {allocation.Version} not found"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing resources for allocation {AllocationId}", allocation.AllocationId);
                return new ResourceReleaseResult
                {
                    Success = false,
                    ErrorMessage = $"Release error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets the current resource utilization for all versions
        /// </summary>
        /// <returns>Utilization summary</returns>
        public async Task<ResourceUtilizationSummary> GetUtilizationAsync()
        {
            await _allocationSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Getting resource utilization summary");

                var summary = new ResourceUtilizationSummary();
                var utilizationTasks = new List<Task<VersionUtilization>>();

                lock (_poolsLock)
                {
                    // Collect utilization data from all pools
                    foreach (var pool in _resourcePools.Values)
                    {
                        utilizationTasks.Add(pool.GetUtilizationAsync());
                    }
                }

                // Wait for all utilization reports
                var utilizationResults = await Task.WhenAll(utilizationTasks);

                // Aggregate results
                foreach (var utilization in utilizationResults)
                {
                    summary.VersionUtilization.Add(utilization);
                }

                // Calculate overall metrics
                summary.TotalPools = _resourcePools.Count;
                summary.TotalCapacity = summary.VersionUtilization.Sum(v => v.TotalCapacity);
                summary.TotalAllocated = summary.VersionUtilization.Sum(v => v.AllocatedResources);
                summary.TotalAvailable = summary.TotalCapacity - summary.TotalAllocated;
                summary.OverallUtilization = summary.TotalCapacity > 0 ?
                    (double)summary.TotalAllocated / summary.TotalCapacity : 0;

                return summary;
            }
            finally
            {
                _allocationSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the current allocation statistics
        /// </summary>
        /// <returns>Current statistics</returns>
        public AllocationStatistics GetStatistics()
        {
            lock (_poolsLock)
            {
                return new AllocationStatistics
                {
                    TotalAllocations = Statistics.TotalAllocations,
                    TotalReleases = Statistics.TotalReleases,
                    ActiveAllocations = Statistics.ActiveAllocations,
                    FailedAllocations = Statistics.FailedAllocations,
                    AverageAllocationTime = Statistics.AverageAllocationTime,
                    LastAllocationTime = Statistics.LastAllocationTime,
                    LastReleaseTime = Statistics.LastReleaseTime,
                    PeakUtilization = Statistics.PeakUtilization
                };
            }
        }

        /// <summary>
        /// Creates a new resource pool for a version
        /// </summary>
        /// <param name="version">Version to create pool for</param>
        /// <param name="requirements">Initial resource requirements</param>
        /// <returns>True if pool was created, false if it already exists</returns>
        public bool CreateResourcePool(string version, ResourceRequirements requirements)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (requirements == null)
                throw new ArgumentNullException(nameof(requirements));

            lock (_poolsLock)
            {
                if (_resourcePools.ContainsKey(version))
                {
                    return false;
                }

                var pool = new VersionResourcePool(version, requirements, Configuration);
                _resourcePools[version] = pool;

                _logger.LogDebug("Created resource pool for version {Version} with capacity {Capacity}",
                    version, requirements.MaxConcurrentOperations);

                return true;
            }
        }

        /// <summary>
        /// Removes a resource pool for a version
        /// </summary>
        /// <param name="version">Version to remove pool for</param>
        /// <returns>True if pool was removed, false if it didn't exist</returns>
        public bool RemoveResourcePool(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            lock (_poolsLock)
            {
                if (_resourcePools.TryGetValue(version, out var pool))
                {
                    if (pool.HasActiveAllocations)
                    {
                        _logger.LogWarning("Cannot remove resource pool for version {Version}: has active allocations",
                            version);
                        return false;
                    }

                    _resourcePools.Remove(version);
                    pool.Dispose();

                    _logger.LogDebug("Removed resource pool for version {Version}", version);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Forces cleanup of all expired allocations
        /// </summary>
        /// <returns>Cleanup result</returns>
        public async Task<AllocationCleanupResult> ForceCleanupAsync()
        {
            await _allocationSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Forcing cleanup of expired allocations");

                var result = new AllocationCleanupResult();
                var cleanupTasks = new List<Task<int>>();

                lock (_poolsLock)
                {
                    foreach (var pool in _resourcePools.Values)
                    {
                        cleanupTasks.Add(pool.CleanupExpiredAllocationsAsync());
                    }
                }

                var cleanupCounts = await Task.WhenAll(cleanupTasks);
                result.TotalCleanedAllocations = cleanupCounts.Sum();

                _logger.LogDebug("Cleanup completed. Cleaned {Count} expired allocations",
                    result.TotalCleanedAllocations);

                return result;
            }
            finally
            {
                _allocationSemaphore.Release();
            }
        }

        /// <summary>
        /// Disposes the license version allocator and cleans up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the license version allocator and cleans up resources
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_poolsLock)
                    {
                        foreach (var pool in _resourcePools.Values)
                        {
                            pool.Dispose();
                        }
                        _resourcePools.Clear();

                        _allocationSemaphore?.Dispose();
                        _cleanupTimer?.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Calculates resource requirements based on query options
        /// </summary>
        /// <param name="version">Version string</param>
        /// <param name="queryOptions">Query options</param>
        /// <returns>Resource requirements</returns>
        private ResourceRequirements CalculateResourceRequirements(string version, LicenseQueryOptions queryOptions)
        {
            var requirements = new ResourceRequirements
            {
                Version = version,
                MaxConcurrentOperations = Math.Min(
                    queryOptions.MaxConcurrentQueries,
                    Configuration.DefaultMaxConcurrentOperations),
                MemoryLimit = CalculateMemoryRequirements(queryOptions),
                CpuLimit = CalculateCpuRequirements(queryOptions),
                Timeout = queryOptions.QueryTimeout,
                Priority = CalculatePriority(version, queryOptions)
            };

            // Adjust based on version characteristics
            if (int.TryParse(version, out int versionYear))
            {
                // Newer versions might require more resources
                if (versionYear >= 2024)
                {
                    requirements.MemoryLimit = (long)(requirements.MemoryLimit * 1.2);
                    requirements.CpuLimit = (int)(requirements.CpuLimit * 1.1);
                }
            }

            return requirements;
        }

        /// <summary>
        /// Gets or creates a resource pool for a version
        /// </summary>
        /// <param name="version">Version string</param>
        /// <param name="requirements">Resource requirements</param>
        /// <returns>Resource pool</returns>
        private VersionResourcePool GetOrCreateResourcePool(string version, ResourceRequirements requirements)
        {
            lock (_poolsLock)
            {
                if (!_resourcePools.TryGetValue(version, out var pool))
                {
                    pool = new VersionResourcePool(version, requirements, Configuration);
                    _resourcePools[version] = pool;

                    _logger.LogDebug("Created new resource pool for version {Version}", version);
                }

                return pool;
            }
        }

        /// <summary>
        /// Calculates memory requirements based on query options
        /// </summary>
        /// <param name="queryOptions">Query options</param>
        /// <returns>Memory limit in bytes</returns>
        private long CalculateMemoryRequirements(LicenseQueryOptions queryOptions)
        {
            var baseMemory = 50 * 1024 * 1024; // 50MB base
            var perQueryMemory = 10 * 1024 * 1024; // 10MB per concurrent query

            var totalMemory = baseMemory + (queryOptions.MaxConcurrentQueries * perQueryMemory);

            // Adjust based on output size
            if (queryOptions.MaxOutputSize > 0)
            {
                totalMemory += queryOptions.MaxOutputSize;
            }

            // Apply configuration limits
            return Math.Min(totalMemory, Configuration.MaxMemoryPerPool);
        }

        /// <summary>
        /// Calculates CPU requirements based on query options
        /// </summary>
        /// <param name="queryOptions">Query options</param>
        /// <returns>CPU limit (percentage)</returns>
        private int CalculateCpuRequirements(LicenseQueryOptions queryOptions)
        {
            var baseCpu = 10; // 10% base
            var perQueryCpu = 5; // 5% per concurrent query

            var totalCpu = baseCpu + (queryOptions.MaxConcurrentQueries * perQueryCpu);

            // Apply configuration limits
            return Math.Min(totalCpu, Configuration.MaxCpuPerPool);
        }

        /// <summary>
        /// Calculates allocation priority based on version and query options
        /// </summary>
        /// <param name="version">Version string</param>
        /// <param name="queryOptions">Query options</param>
        /// <returns>Priority level</returns>
        private AllocationPriority CalculatePriority(string version, LicenseQueryOptions queryOptions)
        {
            // Newer versions get higher priority
            if (int.TryParse(version, out int versionYear))
            {
                if (versionYear >= 2024)
                    return AllocationPriority.High;
                if (versionYear >= 2022)
                    return AllocationPriority.Medium;
            }

            // Interactive queries get higher priority
            if (queryOptions.QueryTimeout.TotalSeconds < 10)
                return AllocationPriority.High;

            return AllocationPriority.Normal;
        }

        /// <summary>
        /// Cleanup timer callback for expired allocations
        /// </summary>
        /// <param name="state">Timer state</param>
        private async void CleanupExpiredAllocations(object state)
        {
            try
            {
                await ForceCleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic cleanup of expired allocations");
            }
        }
    }


    /// <summary>
    /// Configuration for resource allocation
    /// </summary>
    public class ResourceAllocationConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum concurrent allocations
        /// </summary>
        public int MaxConcurrentAllocations { get; set; } = 10;

        /// <summary>
        /// Gets or sets the default maximum concurrent operations per version
        /// </summary>
        public int DefaultMaxConcurrentOperations { get; set; } = 3;

        /// <summary>
        /// Gets or sets the maximum memory per pool in bytes
        /// </summary>
        public long MaxMemoryPerPool { get; set; } = 512 * 1024 * 1024; // 512MB

        /// <summary>
        /// Gets or sets the maximum CPU per pool (percentage)
        /// </summary>
        public int MaxCpuPerPool { get; set; } = 50;

        /// <summary>
        /// Gets or sets the allocation timeout
        /// </summary>
        public TimeSpan AllocationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the default allocation timeout
        /// </summary>
        public TimeSpan DefaultAllocationTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the cleanup interval for expired allocations
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Resource requirements for allocation
    /// </summary>
    public class ResourceRequirements
    {
        public string Version { get; set; }
        public int MaxConcurrentOperations { get; set; }
        public long MemoryLimit { get; set; }
        public int CpuLimit { get; set; }
        public TimeSpan Timeout { get; set; }
        public AllocationPriority Priority { get; set; }
    }

    /// <summary>
    /// Resource allocation result
    /// </summary>
    public class ResourceAllocation
    {
        public string AllocationId { get; set; }
        public string Version { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime AllocatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public long MemoryLimit { get; set; }
        public int CpuLimit { get; set; }
        public AllocationPriority Priority { get; set; }
    }


    /// <summary>
    /// Allocation statistics
    /// </summary>
    public class AllocationStatistics
    {
        public long TotalAllocations { get; set; }
        public long TotalReleases { get; set; }
        public int ActiveAllocations { get; set; }
        public long FailedAllocations { get; set; }
        public TimeSpan AverageAllocationTime { get; set; }
        public DateTime LastAllocationTime { get; set; }
        public DateTime LastReleaseTime { get; set; }
        public double PeakUtilization { get; set; }
    }


    /// <summary>
    /// Version-specific utilization information
    /// </summary>
    public class VersionUtilization
    {
        public string Version { get; set; }
        public int TotalCapacity { get; set; }
        public int AllocatedResources { get; set; }
        public int AvailableResources { get; set; }
        public double UtilizationPercentage { get; set; }
        public long MemoryUsed { get; set; }
        public int CpuUsed { get; set; }
        public List<string> ActiveAllocations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Allocation cleanup result
    /// </summary>
    public class AllocationCleanupResult
    {
        public int TotalCleanedAllocations { get; set; }
        public DateTime CleanupTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Allocation priority levels
    /// </summary>
    public enum AllocationPriority
    {
        Low,
        Normal,
        Medium,
        High,
        Critical
    }
}