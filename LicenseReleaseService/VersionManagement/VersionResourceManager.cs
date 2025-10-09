using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Manages resource allocation, monitoring, and cleanup for multi-version SolidWorks operations
    /// </summary>
    public class VersionResourceManager : IDisposable
    {
        private readonly ILogger<VersionResourceManager> _logger;
        private readonly ResourceManagementConfiguration _configuration;
        private readonly Dictionary<string, VersionResourcePool> _resourcePools;
        private readonly SemaphoreSlim _managementSemaphore;
        private readonly object _poolsLock = new object();
        private readonly Timer _cleanupTimer;
        private readonly Timer _monitoringTimer;
        private bool _disposed;

        /// <summary>
        /// Gets the resource management configuration
        /// </summary>
        public ResourceManagementConfiguration Configuration => _configuration;

        /// <summary>
        /// Gets the current resource statistics
        /// </summary>
        public ResourceManagementStatistics Statistics { get; private set; }

        /// <summary>
        /// Gets the list of managed versions
        /// </summary>
        public IReadOnlyList<string> ManagedVersions
        {
            get
            {
                lock (_poolsLock)
                {
                    return _resourcePools.Keys.ToList().AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the VersionResourceManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configuration">Resource management configuration</param>
        public VersionResourceManager(
            ILogger<VersionResourceManager> logger,
            ResourceManagementConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _resourcePools = new Dictionary<string, VersionResourcePool>();
            _managementSemaphore = new SemaphoreSlim(configuration.MaxConcurrentManagementOperations);
            Statistics = new ResourceManagementStatistics();

            // Initialize cleanup timer
            _cleanupTimer = new Timer(CleanupCallback, null,
                TimeSpan.FromMinutes(configuration.CleanupIntervalMinutes),
                TimeSpan.FromMinutes(configuration.CleanupIntervalMinutes));

            // Initialize monitoring timer
            _monitoringTimer = new Timer(MonitoringCallback, null,
                TimeSpan.FromMinutes(configuration.MonitoringIntervalMinutes),
                TimeSpan.FromMinutes(configuration.MonitoringIntervalMinutes));
        }

        /// <summary>
        /// Starts the resource manager
        /// </summary>
        /// <returns>Start result</returns>
        public async Task<ResourceManagerStartResult> StartAsync()
        {
            try
            {
                _logger.LogInformation("Starting version resource manager");

                var result = new ResourceManagerStartResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Initialize default resource pools if configured
                if (_configuration.EnableDefaultPools)
                {
                    await InitializeDefaultPoolsAsync(result);
                }

                // Start monitoring
                StartMonitoring();

                stopwatch.Stop();
                result.StartTime = DateTime.UtcNow;
                result.StartupDuration = stopwatch.Elapsed;
                result.InitializedPoolCount = _resourcePools.Count;
                result.Success = true;

                _logger.LogInformation("Version resource manager started in {Duration}ms with {PoolCount} pools",
                    stopwatch.ElapsedMilliseconds, result.InitializedPoolCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting version resource manager");
                return new ResourceManagerStartResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Allocates resources for a version operation
        /// </summary>
        /// <param name="version">Version to allocate resources for</param>
        /// <param name="requirements">Resource requirements</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Resource allocation result</returns>
        public async Task<ResourceAllocationResult> AllocateResourcesAsync(
            string version,
            VersionOperationRequirements requirements,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (requirements == null)
                throw new ArgumentNullException(nameof(requirements));

            await _managementSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Allocating resources for version {Version}", version);

                // Get or create resource pool for the version
                var resourcePool = await GetOrCreateResourcePoolAsync(version, requirements, cancellationToken);

                // Attempt allocation
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
                    Statistics.FailedAllocations++;
                    _logger.LogWarning("Failed to allocate resources for version {Version}: {Error}",
                        version, allocation.ErrorMessage);
                }

                return allocation;
            }
            finally
            {
                _managementSemaphore.Release();
            }
        }

        /// <summary>
        /// Releases previously allocated resources
        /// </summary>
        /// <param name="allocation">Allocation to release</param>
        /// <returns>Release result</returns>
        public async Task<ResourceReleaseResult> ReleaseResourcesAsync(ResourceAllocationResult allocation)
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
                    _resourcePools.TryGetValue(allocation.Version, out resourcePool);
                }

                if (resourcePool != null)
                {
                    // Release the allocation
                    var releaseResult = await resourcePool.ReleaseAsync(allocation);

                    // Update statistics
                    lock (_poolsLock)
                    {
                        Statistics.ActiveAllocations--;
                        Statistics.TotalReleases++;
                        Statistics.LastReleaseTime = DateTime.UtcNow;
                    }

                    _logger.LogDebug("Successfully released resources for allocation {AllocationId}",
                        allocation.AllocationId);

                    return releaseResult;
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
        /// Creates a resource pool for a specific version
        /// </summary>
        /// <param name="version">Version to create pool for</param>
        /// <param name="poolConfiguration">Pool configuration</param>
        /// <returns>Creation result</returns>
        public async Task<ResourcePoolCreationResult> CreateResourcePoolAsync(
            string version,
            ResourcePoolConfiguration poolConfiguration)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (poolConfiguration == null)
                throw new ArgumentNullException(nameof(poolConfiguration));

            await _managementSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Creating resource pool for version {Version}", version);

                lock (_poolsLock)
                {
                    if (_resourcePools.ContainsKey(version))
                    {
                        return new ResourcePoolCreationResult
                        {
                            Success = false,
                            ErrorMessage = $"Resource pool for version {version} already exists"
                        };
                    }

                    var pool = new VersionResourcePool(version, poolConfiguration, _configuration);
                    _resourcePools[version] = pool;

                    _logger.LogDebug("Created resource pool for version {Version} with capacity {Capacity}",
                        version, poolConfiguration.MaxConcurrentOperations);
                }

                return new ResourcePoolCreationResult
                {
                    Success = true,
                    CreatedPool = true,
                    Version = version
                };
            }
            finally
            {
                _managementSemaphore.Release();
            }
        }

        /// <summary>
        /// Removes a resource pool for a specific version
        /// </summary>
        /// <param name="version">Version to remove pool for</param>
        /// <returns>Removal result</returns>
        public async Task<ResourcePoolRemovalResult> RemoveResourcePoolAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            await _managementSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Removing resource pool for version {Version}", version);

                lock (_poolsLock)
                {
                    if (_resourcePools.TryGetValue(version, out var pool))
                    {
                        if (pool.HasActiveAllocations)
                        {
                            return new ResourcePoolRemovalResult
                            {
                                Success = false,
                                ErrorMessage = $"Cannot remove pool for version {version}: has active allocations"
                            };
                        }

                        _resourcePools.Remove(version);
                        pool.Dispose();

                        _logger.LogDebug("Removed resource pool for version {Version}", version);

                        return new ResourcePoolRemovalResult
                        {
                            Success = true,
                            RemovedPool = true,
                            Version = version
                        };
                    }
                }

                return new ResourcePoolRemovalResult
                {
                    Success = false,
                    ErrorMessage = $"Resource pool for version {version} not found"
                };
            }
            finally
            {
                _managementSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets resource utilization summary for all managed versions
        /// </summary>
        /// <returns>Utilization summary</returns>
        public async Task<ResourceUtilizationSummary> GetUtilizationSummaryAsync()
        {
            await _managementSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Getting resource utilization summary");

                var summary = new ResourceUtilizationSummary();
                var utilizationTasks = new List<Task<VersionResourceUtilization>>();

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
                    (double)summary.TotalAllocated / summary.TotalCapacity * 100 : 0;

                // Calculate memory and CPU totals
                summary.TotalMemoryAllocated = summary.VersionUtilization.Sum(v => v.MemoryAllocated);
                summary.TotalCpuAllocated = summary.VersionUtilization.Sum(v => v.CpuAllocated);
                summary.TotalMemoryCapacity = summary.VersionUtilization.Sum(v => v.MemoryCapacity);
                summary.TotalCpuCapacity = summary.VersionUtilization.Sum(v => v.CpuCapacity);

                return summary;
            }
            finally
            {
                _managementSemaphore.Release();
            }
        }

        /// <summary>
        /// Forces cleanup of all expired allocations
        /// </summary>
        /// <returns>Cleanup result</returns>
        public async Task<ResourceCleanupResult> ForceCleanupAsync()
        {
            await _managementSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Forcing cleanup of expired allocations");

                var result = new ResourceCleanupResult();
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

                // Update statistics
                Statistics.TotalCleanupOperations++;
                Statistics.LastCleanupTime = DateTime.UtcNow;

                _logger.LogDebug("Cleanup completed. Cleaned {Count} expired allocations",
                    result.TotalCleanedAllocations);

                return result;
            }
            finally
            {
                _managementSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets current resource management statistics
        /// </summary>
        /// <returns>Current statistics</returns>
        public ResourceManagementStatistics GetStatistics()
        {
            lock (_poolsLock)
            {
                return new ResourceManagementStatistics
                {
                    TotalAllocations = Statistics.TotalAllocations,
                    TotalReleases = Statistics.TotalReleases,
                    ActiveAllocations = Statistics.ActiveAllocations,
                    FailedAllocations = Statistics.FailedAllocations,
                    TotalCleanupOperations = Statistics.TotalCleanupOperations,
                    LastAllocationTime = Statistics.LastAllocationTime,
                    LastReleaseTime = Statistics.LastReleaseTime,
                    LastCleanupTime = Statistics.LastCleanupTime,
                    PeakUtilization = Statistics.PeakUtilization,
                    ManagedPoolCount = _resourcePools.Count
                };
            }
        }

        /// <summary>
        /// Disposes the resource manager and cleans up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the resource manager and cleans up resources
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

                        _cleanupTimer?.Dispose();
                        _monitoringTimer?.Dispose();
                        _managementSemaphore?.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        #region Private Methods

        /// <summary>
        /// Initializes default resource pools
        /// </summary>
        /// <param name="result">Start result to update</param>
        private async Task InitializeDefaultPoolsAsync(ResourceManagerStartResult result)
        {
            try
            {
                // Create default pools for common SolidWorks versions
                var defaultVersions = new[] { "2024", "2023", "2022", "2021" };
                var defaultConfig = ResourcePoolConfiguration.CreateDefault();

                foreach (var version in defaultVersions)
                {
                    try
                    {
                        var pool = new VersionResourcePool(version, defaultConfig, _configuration);
                        _resourcePools[version] = pool;
                        result.InitializedPools.Add(version);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create default pool for version {Version}", version);
                        result.Warnings.Add($"Failed to create default pool for version {version}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing default resource pools");
                result.Errors.Add($"Failed to initialize default pools: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets or creates a resource pool for a version
        /// </summary>
        /// <param name="version">Version string</param>
        /// <param name="requirements">Resource requirements</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Resource pool</returns>
        private async Task<VersionResourcePool> GetOrCreateResourcePoolAsync(
            string version,
            VersionOperationRequirements requirements,
            CancellationToken cancellationToken)
        {
            lock (_poolsLock)
            {
                if (!_resourcePools.TryGetValue(version, out var pool))
                {
                    // Create pool configuration based on requirements
                    var poolConfig = ResourcePoolConfiguration.FromRequirements(requirements);
                    pool = new VersionResourcePool(version, poolConfig, _configuration);
                    _resourcePools[version] = pool;

                    _logger.LogDebug("Created new resource pool for version {Version}", version);
                }

                return pool;
            }
        }

        /// <summary>
        /// Starts resource monitoring
        /// </summary>
        private void StartMonitoring()
        {
            _logger.LogDebug("Starting resource monitoring");
            // Monitoring is handled by the timer callback
        }

        /// <summary>
        /// Cleanup timer callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private async void CleanupCallback(object state)
        {
            try
            {
                await ForceCleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic cleanup callback");
            }
        }

        /// <summary>
        /// Monitoring timer callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private async void MonitoringCallback(object state)
        {
            try
            {
                await PerformResourceMonitoringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resource monitoring callback");
            }
        }

        /// <summary>
        /// Performs resource monitoring across all pools
        /// </summary>
        private async Task PerformResourceMonitoringAsync()
        {
            try
            {
                var utilization = await GetUtilizationSummaryAsync();

                // Check for resource pressure
                if (utilization.OverallUtilization > _configuration.HighUtilizationThreshold)
                {
                    _logger.LogWarning("High resource utilization detected: {Utilization}%", utilization.OverallUtilization);
                    await HandleHighUtilizationAsync(utilization);
                }

                // Update peak utilization
                if (utilization.OverallUtilization > Statistics.PeakUtilization)
                {
                    Statistics.PeakUtilization = utilization.OverallUtilization;
                }

                // Check individual pool health
                await CheckPoolHealthAsync(utilization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resource monitoring");
            }
        }

        /// <summary>
        /// Handles high utilization scenarios
        /// </summary>
        /// <param name="utilization">Current utilization summary</param>
        private async Task HandleHighUtilizationAsync(ResourceUtilizationSummary utilization)
        {
            try
            {
                // Implement strategies to handle high utilization
                // This could include:
                // 1. Throttling new allocations
                // 2. Prioritizing existing allocations
                // 3. Requesting additional resources
                // 4. Alerting administrators

                _logger.LogInformation("Applying high utilization mitigation strategies");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling high utilization");
            }
        }

        /// <summary>
        /// Checks the health of individual resource pools
        /// </summary>
        /// <param name="utilization">Utilization summary</param>
        private async Task CheckPoolHealthAsync(ResourceUtilizationSummary utilization)
        {
            try
            {
                foreach (var versionUtil in utilization.VersionUtilization)
                {
                    if (versionUtil.UtilizationPercentage > _configuration.PoolHighUtilizationThreshold)
                    {
                        _logger.LogWarning("High utilization in pool {Version}: {Utilization}%",
                            versionUtil.Version, versionUtil.UtilizationPercentage);
                    }

                    if (versionUtil.MemoryUtilizationPercentage > _configuration.MemoryHighUtilizationThreshold)
                    {
                        _logger.LogWarning("High memory utilization in pool {Version}: {Utilization}%",
                            versionUtil.Version, versionUtil.MemoryUtilizationPercentage);
                    }

                    if (versionUtil.CpuUtilizationPercentage > _configuration.CpuHighUtilizationThreshold)
                    {
                        _logger.LogWarning("High CPU utilization in pool {Version}: {Utilization}%",
                            versionUtil.Version, versionUtil.CpuUtilizationPercentage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking pool health");
            }
        }

        #endregion
    }

    /// <summary>
    /// Manages resource allocations for a specific version
    /// </summary>
    internal class VersionResourcePool : IDisposable
    {
        private readonly string _version;
        private readonly ResourcePoolConfiguration _configuration;
        private readonly ResourceManagementConfiguration _managementConfiguration;
        private readonly Dictionary<string, ResourceAllocation> _activeAllocations;
        private readonly SemaphoreSlim _poolSemaphore;
        private readonly object _allocationsLock = new object();
        private readonly Timer _allocationExpirationTimer;
        private bool _disposed;

        public string Version => _version;
        public int TotalCapacity => _configuration.MaxConcurrentOperations;
        public int AllocatedResources => _activeAllocations.Count;
        public int AvailableResources => TotalCapacity - AllocatedResources;
        public bool HasActiveAllocations => _activeAllocations.Count > 0;

        public VersionResourcePool(
            string version,
            ResourcePoolConfiguration configuration,
            ResourceManagementConfiguration managementConfiguration)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _managementConfiguration = managementConfiguration ?? throw new ArgumentNullException(nameof(managementConfiguration));

            _activeAllocations = new Dictionary<string, ResourceAllocation>();
            _poolSemaphore = new SemaphoreSlim(configuration.MaxConcurrentOperations);

            // Initialize expiration timer
            _allocationExpirationTimer = new Timer(ExpirationCheckCallback, null,
                TimeSpan.FromMinutes(managementConfiguration.ExpirationCheckIntervalMinutes),
                TimeSpan.FromMinutes(managementConfiguration.ExpirationCheckIntervalMinutes));
        }

        public async Task<ResourceAllocationResult> AllocateAsync(
            VersionOperationRequirements requirements,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check capacity
                if (AllocatedResources >= TotalCapacity)
                {
                    return new ResourceAllocationResult
                    {
                        Success = false,
                        ErrorMessage = $"Resource pool for version {_version} is at capacity ({AllocatedResources}/{TotalCapacity})"
                    };
                }

                // Check resource requirements against pool limits
                if (!CheckResourceRequirements(requirements))
                {
                    return new ResourceAllocationResult
                    {
                        Success = false,
                        ErrorMessage = $"Resource requirements exceed pool limits for version {_version}"
                    };
                }

                // Wait for semaphore with timeout
                var semaphoreAcquired = await _poolSemaphore.WaitAsync(
                    _managementConfiguration.AllocationTimeout, cancellationToken);

                if (!semaphoreAcquired)
                {
                    return new ResourceAllocationResult
                    {
                        Success = false,
                        ErrorMessage = "Resource allocation timeout reached"
                    };
                }

                // Create allocation
                var allocation = new ResourceAllocation
                {
                    AllocationId = Guid.NewGuid().ToString(),
                    Version = _version,
                    Success = true,
                    AllocatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(requirements.Timeout),
                    MemoryLimit = requirements.MemoryRequirements,
                    CpuLimit = requirements.CpuRequirements,
                    Priority = requirements.Priority,
                    OperationType = requirements.OperationType,
                    Requirements = requirements
                };

                // Store allocation
                lock (_allocationsLock)
                {
                    _activeAllocations[allocation.AllocationId] = allocation;
                }

                // Convert to allocation result
                return new ResourceAllocationResult
                {
                    AllocationId = allocation.AllocationId,
                    Version = allocation.Version,
                    Success = allocation.Success,
                    AllocatedAt = allocation.AllocatedAt,
                    ExpiresAt = allocation.ExpiresAt,
                    MemoryLimit = allocation.MemoryLimit,
                    CpuLimit = allocation.CpuLimit,
                    Priority = allocation.Priority,
                    OperationType = allocation.OperationType,
                    Requirements = allocation.Requirements
                };
            }
            catch (OperationCanceledException)
            {
                return new ResourceAllocationResult
                {
                    Success = false,
                    ErrorMessage = "Resource allocation cancelled"
                };
            }
            catch (Exception ex)
            {
                return new ResourceAllocationResult
                {
                    Success = false,
                    ErrorMessage = $"Resource allocation error: {ex.Message}"
                };
            }
        }

        public async Task<ResourceReleaseResult> ReleaseAsync(ResourceAllocationResult allocation)
        {
            try
            {
                lock (_allocationsLock)
                {
                    if (!_activeAllocations.Remove(allocation.AllocationId))
                    {
                        return new ResourceReleaseResult
                        {
                            Success = false,
                            ErrorMessage = "Resource allocation not found"
                        };
                    }
                }

                _poolSemaphore.Release();

                return new ResourceReleaseResult
                {
                    Success = true,
                    ReleasedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ResourceReleaseResult
                {
                    Success = false,
                    ErrorMessage = $"Resource release error: {ex.Message}"
                };
            }
        }

        public async Task<VersionResourceUtilization> GetUtilizationAsync()
        {
            await Task.CompletedTask; // Async for consistency

            lock (_allocationsLock)
            {
                var activeAllocations = _activeAllocations.Values.ToList();
                var totalMemoryAllocated = activeAllocations.Sum(a => a.MemoryLimit);
                var totalCpuAllocated = activeAllocations.Sum(a => a.CpuLimit);

                return new VersionResourceUtilization
                {
                    Version = _version,
                    TotalCapacity = TotalCapacity,
                    AllocatedResources = AllocatedResources,
                    AvailableResources = AvailableResources,
                    UtilizationPercentage = TotalCapacity > 0 ?
                        (double)AllocatedResources / TotalCapacity * 100 : 0,
                    MemoryAllocated = totalMemoryAllocated,
                    MemoryCapacity = _configuration.MaxMemoryPerPool,
                    MemoryUtilizationPercentage = _configuration.MaxMemoryPerPool > 0 ?
                        (double)totalMemoryAllocated / _configuration.MaxMemoryPerPool * 100 : 0,
                    CpuAllocated = totalCpuAllocated,
                    CpuCapacity = _configuration.MaxCpuPerPool,
                    CpuUtilizationPercentage = _configuration.MaxCpuPerPool > 0 ?
                        (double)totalCpuAllocated / _configuration.MaxCpuPerPool * 100 : 0,
                    ActiveAllocations = activeAllocations.Select(a => a.AllocationId).ToList()
                };
            }
        }

        public async Task<int> CleanupExpiredAllocationsAsync()
        {
            var now = DateTime.UtcNow;
            var expiredAllocations = new List<string>();

            lock (_allocationsLock)
            {
                expiredAllocations.AddRange(
                    _activeAllocations.Where(kvp => kvp.Value.ExpiresAt <= now)
                    .Select(kvp => kvp.Key));

                foreach (var allocationId in expiredAllocations)
                {
                    _activeAllocations.Remove(allocationId);
                    _poolSemaphore.Release();
                }
            }

            return expiredAllocations.Count;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_allocationsLock)
                    {
                        foreach (var allocation in _activeAllocations.Values)
                        {
                            _poolSemaphore.Release();
                        }
                        _activeAllocations.Clear();

                        _poolSemaphore?.Dispose();
                        _allocationExpirationTimer?.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        private bool CheckResourceRequirements(VersionOperationRequirements requirements)
        {
            var currentUtilization = GetUtilizationAsync().Result;

            // Check memory requirements
            if (requirements.MemoryRequirements > 0 &&
                currentUtilization.MemoryAllocated + requirements.MemoryRequirements > _configuration.MaxMemoryPerPool)
            {
                return false;
            }

            // Check CPU requirements
            if (requirements.CpuRequirements > 0 &&
                currentUtilization.CpuAllocated + requirements.CpuRequirements > _configuration.MaxCpuPerPool)
            {
                return false;
            }

            return true;
        }

        private async void ExpirationCheckCallback(object state)
        {
            try
            {
                await CleanupExpiredAllocationsAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't let it crash the system
                Console.WriteLine($"Error in expiration check for pool {_version}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Configuration classes for resource management
    /// </summary>
    public class ResourceManagementConfiguration
    {
        public int MaxConcurrentManagementOperations { get; set; } = 10;
        public bool EnableDefaultPools { get; set; } = true;
        public int CleanupIntervalMinutes { get; set; } = 5;
        public int MonitoringIntervalMinutes { get; set; } = 1;
        public int ExpirationCheckIntervalMinutes { get; set; } = 1;
        public TimeSpan AllocationTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public double HighUtilizationThreshold { get; set; } = 80.0; // 80%
        public double PoolHighUtilizationThreshold { get; set; } = 90.0; // 90%
        public double MemoryHighUtilizationThreshold { get; set; } = 85.0; // 85%
        public double CpuHighUtilizationThreshold { get; set; } = 85.0; // 85%
        public int DefaultMaxConcurrentOperations { get; set; } = 5;
        public long DefaultMaxMemoryPerPool { get; set; } = 1024 * 1024 * 1024; // 1GB
        public int DefaultMaxCpuPerPool { get; set; } = 50; // 50%
        public int MaxConcurrentAllocations { get; set; } = 10;
        public long AllocationTimeoutMs { get; set; } = 30000;
        public long CleanupIntervalMs { get; set; } = 300000;
        public bool EnableMetrics { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
    }

    public class ResourcePoolConfiguration
    {
        public int MaxConcurrentOperations { get; set; } = 5;
        public long MaxMemoryPerPool { get; set; } = 1024 * 1024 * 1024; // 1GB
        public int MaxCpuPerPool { get; set; } = 50; // 50%
        public TimeSpan DefaultAllocationTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public long MaxMemoryUsageBytes { get; set; }
        public int MaxCpuUsagePercent { get; set; }
        public long TimeoutMs { get; set; }
        public string Version { get; set; }

        public static ResourcePoolConfiguration CreateDefault()
        {
            return new ResourcePoolConfiguration();
        }

        public static ResourcePoolConfiguration FromRequirements(VersionOperationRequirements requirements)
        {
            return new ResourcePoolConfiguration
            {
                MaxConcurrentOperations = Math.Max(requirements.Priority == AllocationPriority.High ? 10 : 5, requirements.MaxConcurrentOperations),
                MaxMemoryPerPool = Math.Max(requirements.MemoryRequirements * 2, 1024 * 1024 * 512), // At least 512MB
                MaxCpuPerPool = Math.Max(requirements.CpuRequirements * 2, 25) // At least 25%
            };
        }
    }

    /// <summary>
    /// Result classes for resource management operations
    /// </summary>
    public class ResourceManagerStartResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan StartupDuration { get; set; }
        public int InitializedPoolCount { get; set; }
        public List<string> InitializedPools { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class ResourceAllocationResult
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
        public VersionOperationType OperationType { get; set; }
        public VersionOperationRequirements Requirements { get; set; }
    }

    public class ResourceReleaseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime ReleasedAt { get; set; }
    }

    public class ResourcePoolCreationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public bool CreatedPool { get; set; }
        public string Version { get; set; }
    }

    public class ResourcePoolRemovalResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public bool RemovedPool { get; set; }
        public string Version { get; set; }
    }

    public class ResourceCleanupResult
    {
        public int TotalCleanedAllocations { get; set; }
        public DateTime CleanupTime { get; set; } = DateTime.UtcNow;
    }

    public class ResourceManagementStatistics
    {
        public long TotalAllocations { get; set; }
        public long TotalReleases { get; set; }
        public int ActiveAllocations { get; set; }
        public long FailedAllocations { get; set; }
        public long TotalCleanupOperations { get; set; }
        public DateTime LastAllocationTime { get; set; }
        public DateTime LastReleaseTime { get; set; }
        public DateTime LastCleanupTime { get; set; }
        public double PeakUtilization { get; set; }
        public int ManagedPoolCount { get; set; }
    }

    public class ResourceUtilizationSummary
    {
        public List<VersionResourceUtilization> VersionUtilization { get; set; } = new List<VersionResourceUtilization>();
        public int TotalPools { get; set; }
        public long TotalCapacity { get; set; }
        public long TotalAllocated { get; set; }
        public long TotalAvailable { get; set; }
        public double OverallUtilization { get; set; }
        public long TotalMemoryAllocated { get; set; }
        public long TotalMemoryCapacity { get; set; }
        public long TotalCpuAllocated { get; set; }
        public long TotalCpuCapacity { get; set; }
    }

    public class VersionResourceUtilization
    {
        public string Version { get; set; }
        public int TotalCapacity { get; set; }
        public int AllocatedResources { get; set; }
        public int AvailableResources { get; set; }
        public double UtilizationPercentage { get; set; }
        public long MemoryAllocated { get; set; }
        public long MemoryCapacity { get; set; }
        public double MemoryUtilizationPercentage { get; set; }
        public int CpuAllocated { get; set; }
        public int CpuCapacity { get; set; }
        public double CpuUtilizationPercentage { get; set; }
        public List<string> ActiveAllocations { get; set; } = new List<string>();

        // Additional properties for LicenseVersionAllocator
        /// <summary>
        /// Gets or sets the memory usage in MB
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }
    }
}