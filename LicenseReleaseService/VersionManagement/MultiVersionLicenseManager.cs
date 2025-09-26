using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Manages concurrent license operations across multiple SolidWorks versions with proper resource allocation and conflict resolution
    /// </summary>
    public class MultiVersionLicenseManager : IDisposable
    {
        private readonly ILogger<MultiVersionLicenseManager> _logger;
        private readonly SolidWorksVersionDetector _versionDetector;
        private readonly IProcessExecutor _processExecutor;
        private readonly LicenseVersionAllocator _versionAllocator;
        private readonly VersionConflictResolver _conflictResolver;
        private readonly Dictionary<string, VersionSpecificLicenseQuery> _versionQueries;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly object _managerLock = new object();
        private bool _disposed;

        /// <summary>
        /// Gets the current multi-version management configuration
        /// </summary>
        public MultiVersionManagementConfiguration Configuration { get; }

        /// <summary>
        /// Gets the list of managed SolidWorks versions
        /// </summary>
        public IReadOnlyList<SolidWorksVersionInfo> ManagedVersions
        {
            get
            {
                lock (_managerLock)
                {
                    return _versionQueries.Values
                        .Select(q => q.VersionInfo)
                        .Where(v => v.IsAvailable && v.IsSupported)
                        .ToList()
                        .AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the MultiVersionLicenseManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="versionDetector">Version detection service</param>
        /// <param name="processExecutor">Process execution service</param>
        /// <param name="versionAllocator">Version resource allocator</param>
        /// <param name="conflictResolver">Conflict resolution service</param>
        /// <param name="configuration">Multi-version management configuration</param>
        public MultiVersionLicenseManager(
            ILogger<MultiVersionLicenseManager> logger,
            SolidWorksVersionDetector versionDetector,
            IProcessExecutor processExecutor,
            LicenseVersionAllocator versionAllocator,
            VersionConflictResolver conflictResolver,
            MultiVersionManagementConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionDetector = versionDetector ?? throw new ArgumentNullException(nameof(versionDetector));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _versionAllocator = versionAllocator ?? throw new ArgumentNullException(nameof(versionAllocator));
            _conflictResolver = conflictResolver ?? throw new ArgumentNullException(nameof(conflictResolver));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _versionQueries = new Dictionary<string, VersionSpecificLicenseQuery>();
            _operationSemaphore = new SemaphoreSlim(configuration.MaxConcurrentOperations);
        }

        /// <summary>
        /// Initializes the multi-version license manager with detected versions
        /// </summary>
        /// <returns>Initialization result</returns>
        public async Task<MultiVersionInitializationResult> InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing multi-version license manager");

                var result = new MultiVersionInitializationResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Detect available SolidWorks versions
                var detectionResult = await _versionDetector.DetectInstalledVersionsAsync();
                if (!detectionResult.Success)
                {
                    result.Errors.Add($"Version detection failed: {detectionResult.Error}");
                    return result;
                }

                // Initialize version-specific query services for available versions
                var availableVersions = _versionDetector.GetAvailableVersions();
                foreach (var version in availableVersions)
                {
                    await InitializeVersionAsync(version, result);
                }

                // Sort versions by priority (newest first)
                var sortedVersions = availableVersions
                    .OrderByDescending(v => v.Version)
                    .ThenByDescending(v => v.Is64Bit)
                    .ToList();

                // Validate version compatibility and detect conflicts
                await ValidateVersionCompatibilityAsync(sortedVersions, result);

                stopwatch.Stop();
                result.InitializationTime = stopwatch.Elapsed;
                result.ManagedVersionCount = _versionQueries.Count;
                result.TotalVersionCount = availableVersions.Count;

                _logger.LogInformation("Multi-version license manager initialized in {Duration}ms. Managing {ManagedCount} of {TotalCount} available versions",
                    stopwatch.ElapsedMilliseconds, result.ManagedVersionCount, result.TotalVersionCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing multi-version license manager");
                var result = new MultiVersionInitializationResult();
                result.Errors.Add($"Initialization failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Executes a license query across all managed versions
        /// </summary>
        /// <param name="queryOptions">Query options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Multi-version query result</returns>
        public async Task<MultiVersionQueryResult> QueryAllVersionsAsync(
            LicenseQueryOptions queryOptions,
            CancellationToken cancellationToken = default)
        {
            if (queryOptions == null)
                throw new ArgumentNullException(nameof(queryOptions));

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Starting multi-version license query");

                var result = new MultiVersionQueryResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Get current managed versions
                var managedVersions = ManagedVersions.ToList();
                if (managedVersions.Count == 0)
                {
                    result.Warnings.Add("No managed versions available for query");
                    return result;
                }

                // Execute queries in parallel with resource limits
                var queryTasks = new List<Task<VersionSpecificQueryResult>>();
                var versionSemaphore = new SemaphoreSlim(Configuration.MaxConcurrentVersionQueries);

                foreach (var version in managedVersions)
                {
                    await versionSemaphore.WaitAsync(cancellationToken);
                    queryTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var versionResult = await QueryVersionAsync(version, queryOptions, cancellationToken);
                            return versionResult;
                        }
                        finally
                        {
                            versionSemaphore.Release();
                        }
                    }, cancellationToken));
                }

                var queryResults = await Task.WhenAll(queryTasks);

                // Aggregate results
                foreach (var versionResult in queryResults)
                {
                    result.VersionResults.Add(versionResult);

                    if (versionResult.Success)
                    {
                        result.SuccessfulVersionCount++;
                        result.TotalLicenseCount += versionResult.LicenseCount;
                    }
                    else
                    {
                        result.FailedVersionCount++;
                        result.Errors.AddRange(versionResult.Errors.Select(e => $"{versionResult.Version}: {e}"));
                    }

                    result.Warnings.AddRange(versionResult.Warnings.Select(w => $"{versionResult.Version}: {w}"));
                }

                // Detect and resolve conflicts across versions
                var conflicts = await DetectCrossVersionConflictsAsync(queryResults.Where(r => r.Success).ToList());
                if (conflicts.Count > 0)
                {
                    result.Conflicts.AddRange(conflicts);
                    var resolvedConflicts = await _conflictResolver.ResolveConflictsAsync(conflicts);
                    result.ResolvedConflicts.AddRange(resolvedConflicts);
                }

                stopwatch.Stop();
                result.QueryTime = stopwatch.Elapsed;

                _logger.LogDebug("Multi-version query completed in {Duration}ms. {SuccessCount}/{TotalCount} versions successful",
                    stopwatch.ElapsedMilliseconds, result.SuccessfulVersionCount, managedVersions.Count);

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a license query for a specific version
        /// </summary>
        /// <param name="version">Version to query</param>
        /// <param name="queryOptions">Query options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Version-specific query result</returns>
        public async Task<VersionSpecificQueryResult> QueryVersionAsync(
            SolidWorksVersionInfo version,
            LicenseQueryOptions queryOptions,
            CancellationToken cancellationToken = default)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));
            if (queryOptions == null)
                throw new ArgumentNullException(nameof(queryOptions));

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Querying licenses for version {Version}", version.Version);

                // Check if version is managed
                if (!_versionQueries.TryGetValue(version.Version, out var versionQuery))
                {
                    return new VersionSpecificQueryResult
                    {
                        Version = version.Version,
                        Success = false,
                        Errors = { $"Version {version.Version} is not managed" }
                    };
                }

                // Allocate resources for this operation
                var allocation = await _versionAllocator.AllocateAsync(version.Version, queryOptions);
                if (!allocation.Success)
                {
                    return new VersionSpecificQueryResult
                    {
                        Version = version.Version,
                        Success = false,
                        Errors = { $"Resource allocation failed: {allocation.ErrorMessage}" }
                    };
                }

                try
                {
                    // Execute the version-specific query
                    var result = await versionQuery.QueryLicensesAsync(queryOptions, cancellationToken);
                    return result;
                }
                finally
                {
                    // Release resources
                    await _versionAllocator.ReleaseAsync(allocation);
                }
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Allocates licenses across multiple versions based on availability and priority
        /// </summary>
        /// <param name="request">License allocation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Allocation result</returns>
        public async Task<MultiVersionAllocationResult> AllocateLicensesAsync(
            MultiVersionAllocationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Starting multi-version license allocation for {FeatureName}", request.FeatureName);

                var result = new MultiVersionAllocationResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Get available versions sorted by priority
                var availableVersions = ManagedVersions
                    .OrderByDescending(v => v.Version)
                    .ThenByDescending(v => v.Is64Bit)
                    .ToList();

                if (availableVersions.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "No managed versions available";
                    return result;
                }

                // Try allocation for each version in priority order
                foreach (var version in availableVersions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Operation cancelled";
                        break;
                    }

                    var allocationResult = await TryAllocateVersionAsync(version, request, cancellationToken);
                    if (allocationResult.Success)
                    {
                        result.Success = true;
                        result.AllocatedVersion = version.Version;
                        result.AllocationDetails = allocationResult;
                        break;
                    }
                    else
                    {
                        result.FailedAttempts.Add(new VersionAllocationAttempt
                        {
                            Version = version.Version,
                            Reason = allocationResult.ErrorMessage,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }

                stopwatch.Stop();
                result.AllocationTime = stopwatch.Elapsed;

                if (result.Success)
                {
                    _logger.LogDebug("Successfully allocated {FeatureName} license from version {Version} in {Duration}ms",
                        request.FeatureName, result.AllocatedVersion, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Failed to allocate {FeatureName} license from any of {VersionCount} versions",
                        request.FeatureName, availableVersions.Count);
                }

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Releases licenses across multiple versions
        /// </summary>
        /// <param name="request">License release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Release result</returns>
        public async Task<MultiVersionReleaseResult> ReleaseLicensesAsync(
            MultiVersionReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Starting multi-version license release for {FeatureName}", request.FeatureName);

                var result = new MultiVersionReleaseResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Check if version is managed
                if (!_versionQueries.TryGetValue(request.Version, out var versionQuery))
                {
                    result.Success = false;
                    result.ErrorMessage = $"Version {request.Version} is not managed";
                    return result;
                }

                // Execute release operation
                var releaseResult = await versionQuery.ReleaseLicensesAsync(request, cancellationToken);
                result.Success = releaseResult.Success;
                result.ReleasedLicenses = releaseResult.ReleasedLicenses;
                result.Errors.AddRange(releaseResult.Errors);
                result.Warnings.AddRange(releaseResult.Warnings);

                stopwatch.Stop();
                result.ReleaseTime = stopwatch.Elapsed;

                if (result.Success)
                {
                    _logger.LogDebug("Successfully released {Count} {FeatureName} licenses from version {Version} in {Duration}ms",
                        result.ReleasedLicenses.Count, request.FeatureName, request.Version, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Failed to release {FeatureName} licenses from version {Version}: {Error}",
                        request.FeatureName, request.Version, result.ErrorMessage);
                }

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the health status of all managed versions
        /// </summary>
        /// <returns>Health status result</returns>
        public async Task<MultiVersionHealthResult> GetHealthStatusAsync()
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Checking multi-version health status");

                var result = new MultiVersionHealthResult();
                var managedVersions = ManagedVersions.ToList();

                // Check individual version health
                var healthTasks = managedVersions.Select(async version =>
                {
                    if (_versionQueries.TryGetValue(version.Version, out var versionQuery))
                    {
                        return await versionQuery.GetHealthStatusAsync();
                    }
                    return new VersionHealthResult
                    {
                        Version = version.Version,
                        IsHealthy = false,
                        Status = "Not managed"
                    };
                });

                var healthResults = await Task.WhenAll(healthTasks);
                result.VersionHealth.AddRange(healthResults);

                // Calculate overall health metrics
                result.HealthyVersionCount = healthResults.Count(h => h.IsHealthy);
                result.TotalVersionCount = healthResults.Length;
                result.OverallHealth = result.HealthyVersionCount == result.TotalVersionCount ?
                    MultiVersionHealthLevel.Healthy :
                    result.HealthyVersionCount > 0 ?
                    MultiVersionHealthLevel.Degraded :
                    MultiVersionHealthLevel.Unhealthy;

                // Detect system-wide issues
                await DetectSystemWideIssuesAsync(result);

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Refreshes version detection and updates managed versions
        /// </summary>
        /// <returns>Refresh result</returns>
        public async Task<MultiVersionRefreshResult> RefreshVersionsAsync()
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                _logger.LogInformation("Refreshing multi-version detection");

                var result = new MultiVersionRefreshResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Store current versions for comparison
                var currentVersions = ManagedVersions.ToList();

                // Refresh version detection
                var detectionResult = await _versionDetector.RefreshDetectionAsync();
                if (!detectionResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Version refresh failed: {detectionResult.Error}";
                    return result;
                }

                // Get new available versions
                var newVersions = _versionDetector.GetAvailableVersions();
                var newVersionSet = new HashSet<string>(newVersions.Select(v => v.Version));
                var currentVersionSet = new HashSet<string>(currentVersions.Select(v => v.Version));

                // Detect version changes
                result.AddedVersions = newVersions.Where(v => !currentVersionSet.Contains(v.Version)).ToList();
                result.RemovedVersions = currentVersions.Where(v => !newVersionSet.Contains(v.Version)).ToList();

                // Initialize new versions
                foreach (var addedVersion in result.AddedVersions)
                {
                    await InitializeVersionAsync(addedVersion, result);
                }

                // Remove old versions
                foreach (var removedVersion in result.RemovedVersions)
                {
                    if (_versionQueries.TryGetValue(removedVersion.Version, out var versionQuery))
                    {
                        versionQuery.Dispose();
                        _versionQueries.Remove(removedVersion.Version);
                    }
                }

                // Update existing versions
                foreach (var existingVersion in newVersions.Where(v => currentVersionSet.Contains(v.Version)))
                {
                    if (_versionQueries.TryGetValue(existingVersion.Version, out var versionQuery))
                    {
                        versionQuery.UpdateVersionInfo(existingVersion);
                    }
                }

                stopwatch.Stop();
                result.RefreshTime = stopwatch.Elapsed;
                result.Success = true;

                _logger.LogInformation("Version refresh completed in {Duration}ms. Added: {AddedCount}, Removed: {RemovedCount}",
                    stopwatch.ElapsedMilliseconds, result.AddedVersions.Count, result.RemovedVersions.Count);

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Disposes the multi-version license manager and cleans up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the multi-version license manager and cleans up resources
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_managerLock)
                    {
                        foreach (var versionQuery in _versionQueries.Values)
                        {
                            versionQuery.Dispose();
                        }
                        _versionQueries.Clear();

                        _operationSemaphore?.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Initializes a specific version for license management
        /// </summary>
        /// <param name="version">Version to initialize</param>
        /// <param name="result">Initialization result to update</param>
        private async Task InitializeVersionAsync(SolidWorksVersionInfo version, MultiVersionInitializationResult result)
        {
            try
            {
                // Create version-specific query service
                var versionQuery = new VersionSpecificLicenseQuery(
                    _logger,
                    version,
                    _processExecutor,
                    Configuration.VersionQueryConfiguration);

                // Test connectivity and health
                var healthResult = await versionQuery.GetHealthStatusAsync();
                if (!healthResult.IsHealthy)
                {
                    result.Warnings.Add($"Version {version.Version} health check failed: {healthResult.Status}");
                    return;
                }

                // Add to managed versions
                _versionQueries[version.Version] = versionQuery;
                result.InitializedVersions.Add(version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing version {Version}", version.Version);
                result.Errors.Add($"Failed to initialize version {version.Version}: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates compatibility between managed versions
        /// </summary>
        /// <param name="versions">List of versions to validate</param>
        /// <param name="result">Initialization result to update</param>
        private async Task ValidateVersionCompatibilityAsync(List<SolidWorksVersionInfo> versions, MultiVersionInitializationResult result)
        {
            try
            {
                // Check for version conflicts
                var conflicts = await _conflictResolver.DetectVersionConflictsAsync(versions);
                if (conflicts.Count > 0)
                {
                    result.Conflicts.AddRange(conflicts);
                    result.Warnings.Add($"Detected {conflicts.Count} version conflicts that may affect operation");
                }

                // Validate version ranges
                var versionYears = versions.Select(v => int.Parse(v.Version)).ToList();
                var minYear = versionYears.Min();
                var maxYear = versionYears.Max();

                if (maxYear - minYear > Configuration.MaximumVersionSpan)
                {
                    result.Warnings.Add($"Version span ({maxYear - minYear} years) exceeds recommended maximum of {Configuration.MaximumVersionSpan} years");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating version compatibility");
                result.Warnings.Add($"Version compatibility validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Tries to allocate licenses from a specific version
        /// </summary>
        /// <param name="version">Version to try</param>
        /// <param name="request">Allocation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Allocation result</returns>
        private async Task<VersionAllocationResult> TryAllocateVersionAsync(
            SolidWorksVersionInfo version,
            MultiVersionAllocationRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!_versionQueries.TryGetValue(version.Version, out var versionQuery))
                {
                    return new VersionAllocationResult
                    {
                        Success = false,
                        ErrorMessage = $"Version {version.Version} is not available"
                    };
                }

                return await versionQuery.TryAllocateAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error allocating from version {Version}", version.Version);
                return new VersionAllocationResult
                {
                    Success = false,
                    ErrorMessage = $"Allocation error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Detects conflicts across multiple versions
        /// </summary>
        /// <param name="queryResults">Query results from multiple versions</param>
        /// <returns>List of detected conflicts</returns>
        private async Task<List<VersionConflict>> DetectCrossVersionConflictsAsync(List<VersionSpecificQueryResult> queryResults)
        {
            var conflicts = new List<VersionConflict>();

            try
            {
                // Check for duplicate license usage across versions
                var userVersions = new Dictionary<string, List<string>>();
                foreach (var result in queryResults)
                {
                    foreach (var user in result.Users)
                    {
                        if (!userVersions.ContainsKey(user))
                        {
                            userVersions[user] = new List<string>();
                        }
                        userVersions[user].Add(result.Version);
                    }
                }

                // Detect users using multiple versions simultaneously
                foreach (var kvp in userVersions.Where(kvp => kvp.Value.Count > 1))
                {
                    conflicts.Add(new VersionConflict
                    {
                        ConflictType = VersionConflictType.MultipleVersionUsage,
                        User = kvp.Key,
                        AffectedVersions = kvp.Value,
                        Severity = ConflictSeverity.Warning,
                        Description = $"User {kvp.Key} is using multiple versions: {string.Join(", ", kvp.Value)}",
                        DetectedAt = DateTime.UtcNow
                    });
                }

                // Check for license exhaustion across versions
                var totalAvailable = queryResults.Sum(r => r.AvailableLicenses);
                var totalUsed = queryResults.Sum(r => r.UsedLicenses);
                var totalCapacity = queryResults.Sum(r => r.TotalLicenses);

                if (totalUsed >= totalCapacity * 0.95) // 95% threshold
                {
                    conflicts.Add(new VersionConflict
                    {
                        ConflictType = VersionConflictType.LicenseExhaustion,
                        Severity = ConflictSeverity.Critical,
                        Description = $"License exhaustion detected: {totalUsed}/{totalCapacity} licenses in use",
                        DetectedAt = DateTime.UtcNow,
                        Metrics = new Dictionary<string, object>
                        {
                            ["TotalUsed"] = totalUsed,
                            ["TotalCapacity"] = totalCapacity,
                            ["UtilizationPercentage"] = (double)totalUsed / totalCapacity * 100
                        }
                    });
                }

                return conflicts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting cross-version conflicts");
                return conflicts;
            }
        }

        /// <summary>
        /// Detects system-wide issues affecting all versions
        /// </summary>
        /// <param name="result">Health result to update</param>
        private async Task DetectSystemWideIssuesAsync(MultiVersionHealthResult result)
        {
            try
            {
                // Check if all versions are experiencing similar issues
                var commonIssues = result.VersionHealth
                    .Where(h => !h.IsHealthy)
                    .GroupBy(h => h.Status)
                    .Where(g => g.Count() > result.TotalVersionCount / 2) // Majority affected
                    .ToList();

                foreach (var issueGroup in commonIssues)
                {
                    result.SystemWideIssues.Add(new SystemWideIssue
                    {
                        IssueType = issueGroup.Key,
                        AffectedVersionCount = issueGroup.Count(),
                        Severity = issueGroup.Count() == result.TotalVersionCount ?
                            SystemWideIssueSeverity.Critical : SystemWideIssueSeverity.Warning,
                        Description = $"{issueGroup.Count()} versions reporting: {issueGroup.Key}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting system-wide issues");
            }
        }
    }

    /// <summary>
    /// Configuration for multi-version license management
    /// </summary>
    public class MultiVersionManagementConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of concurrent operations
        /// </summary>
        public int MaxConcurrentOperations { get; set; } = 5;

        /// <summary>
        /// Gets or sets the maximum number of concurrent version queries
        /// </summary>
        public int MaxConcurrentVersionQueries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the maximum allowed version span in years
        /// </summary>
        public int MaximumVersionSpan { get; set; } = 5;

        /// <summary>
        /// Gets or sets the timeout for individual operations
        /// </summary>
        public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the configuration for version-specific queries
        /// </summary>
        public VersionQueryConfiguration VersionQueryConfiguration { get; set; } = new VersionQueryConfiguration();

        /// <summary>
        /// Gets or sets the resource allocation strategy
        /// </summary>
        public ResourceAllocationStrategy AllocationStrategy { get; set; } = ResourceAllocationStrategy.NewestFirst;

        /// <summary>
        /// Gets or sets the conflict resolution strategy
        /// </summary>
        public ConflictResolutionStrategy ConflictResolutionStrategy { get; set; } = ConflictResolutionStrategy.WarnOnly;
    }

    /// <summary>
    /// Configuration for version-specific license queries
    /// </summary>
    public class VersionQueryConfiguration
    {
        /// <summary>
        /// Gets or sets the timeout for version-specific queries
        /// </summary>
        public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the retry count for failed queries
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay between retries
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets or sets the cache expiration time for query results
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the maximum cache size
        /// </summary>
        public int MaxCacheSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether to enable query caching
        /// </summary>
        public bool EnableCaching { get; set; } = true;
    }

    /// <summary>
    /// Resource allocation strategies
    /// </summary>
    public enum ResourceAllocationStrategy
    {
        /// <summary>
        /// Allocate from newest versions first
        /// </summary>
        NewestFirst,

        /// <summary>
        /// Allocate from versions with most available licenses
        /// </summary>
        MostAvailableFirst,

        /// <summary>
        /// Allocate from versions with lowest utilization
        /// </summary>
        LowestUtilizationFirst,

        /// <summary>
        /// Allocate from versions with highest performance
        /// </summary>
        HighestPerformanceFirst,

        /// <summary>
        /// Round-robin allocation across versions
        /// </summary>
        RoundRobin
    }

    /// <summary>
    /// Conflict resolution strategies
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        /// <summary>
        /// Only warn about conflicts, don't resolve
        /// </summary>
        WarnOnly,

        /// <summary>
        /// Automatically resolve conflicts when possible
        /// </summary>
        AutoResolve,

        /// <summary>
        /// Block operations that would cause conflicts
        /// </summary>
        BlockConflicts,

        /// <summary>
        /// Require manual intervention for conflicts
        /// </summary>
        ManualIntervention
    }
}