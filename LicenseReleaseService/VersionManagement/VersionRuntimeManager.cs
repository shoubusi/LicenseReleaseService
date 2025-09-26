using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Process;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Manages runtime operations for multi-version SolidWorks license management including dynamic version selection,
    /// resource coordination, and performance optimization
    /// </summary>
    public class VersionRuntimeManager : IDisposable
    {
        private readonly ILogger<VersionRuntimeManager> _logger;
        private readonly MultiVersionLicenseManager _licenseManager;
        private readonly SolidWorksVersionDetector _versionDetector;
        private readonly IProcessExecutor _processExecutor;
        private readonly VersionResourceManager _resourceManager;
        private readonly VersionPerformanceOptimizer _performanceOptimizer;
        private readonly VersionScheduler _scheduler;
        private readonly RuntimeManagementConfiguration _configuration;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly object _managerLock = new object();
        private readonly Dictionary<string, VersionRuntimeContext> _runtimeContexts;
        private readonly Timer _healthCheckTimer;
        private bool _disposed;

        /// <summary>
        /// Gets the current runtime management configuration
        /// </summary>
        public RuntimeManagementConfiguration Configuration => _configuration;

        /// <summary>
        /// Gets the list of actively managed versions
        /// </summary>
        public IReadOnlyList<SolidWorksVersionInfo> ActiveVersions
        {
            get
            {
                lock (_managerLock)
                {
                    return _runtimeContexts.Values
                        .Where(c => c.IsActive && c.VersionInfo.IsAvailable)
                        .Select(c => c.VersionInfo)
                        .ToList()
                        .AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the VersionRuntimeManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="licenseManager">Multi-version license manager</param>
        /// <param name="versionDetector">Version detection service</param>
        /// <param name="processExecutor">Process execution service</param>
        /// <param name="resourceManager">Resource manager</param>
        /// <param name="performanceOptimizer">Performance optimizer</param>
        /// <param name="scheduler">Version scheduler</param>
        /// <param name="configuration">Runtime management configuration</param>
        public VersionRuntimeManager(
            ILogger<VersionRuntimeManager> logger,
            MultiVersionLicenseManager licenseManager,
            SolidWorksVersionDetector versionDetector,
            IProcessExecutor processExecutor,
            VersionResourceManager resourceManager,
            VersionPerformanceOptimizer performanceOptimizer,
            VersionScheduler scheduler,
            RuntimeManagementConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _licenseManager = licenseManager ?? throw new ArgumentNullException(nameof(licenseManager));
            _versionDetector = versionDetector ?? throw new ArgumentNullException(nameof(versionDetector));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _performanceOptimizer = performanceOptimizer ?? throw new ArgumentNullException(nameof(performanceOptimizer));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _runtimeContexts = new Dictionary<string, VersionRuntimeContext>();
            _operationSemaphore = new SemaphoreSlim(configuration.MaxConcurrentOperations);

            // Initialize health check timer
            _healthCheckTimer = new Timer(HealthCheckCallback, null,
                TimeSpan.FromMinutes(configuration.HealthCheckIntervalMinutes),
                TimeSpan.FromMinutes(configuration.HealthCheckIntervalMinutes));
        }

        /// <summary>
        /// Initializes the runtime manager and sets up version contexts
        /// </summary>
        /// <returns>Initialization result</returns>
        public async Task<RuntimeInitializationResult> InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing version runtime manager");

                var result = new RuntimeInitializationResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Initialize license manager first
                var licenseInitResult = await _licenseManager.InitializeAsync();
                if (!licenseInitResult.Errors.Any())
                {
                    result.LicenseManagerInitialized = true;
                }
                else
                {
                    result.Errors.AddRange(licenseInitResult.Errors);
                }

                // Get available versions and create runtime contexts
                var availableVersions = _licenseManager.ManagedVersions.ToList();
                foreach (var version in availableVersions)
                {
                    await CreateRuntimeContextAsync(version, result);
                }

                // Start resource manager
                await _resourceManager.StartAsync();

                // Start performance optimizer
                await _performanceOptimizer.StartAsync();

                // Start scheduler
                await _scheduler.StartAsync();

                // Apply initial performance optimizations
                await ApplyInitialOptimizationsAsync();

                stopwatch.Stop();
                result.InitializationTime = stopwatch.Elapsed;
                result.ActiveVersionCount = ActiveVersions.Count;
                result.TotalVersionCount = availableVersions.Count;

                _logger.LogInformation("Version runtime manager initialized in {Duration}ms. {ActiveCount}/{TotalCount} versions active",
                    stopwatch.ElapsedMilliseconds, result.ActiveVersionCount, result.TotalVersionCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing version runtime manager");
                var result = new RuntimeInitializationResult();
                result.Errors.Add($"Initialization failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Dynamically selects the best version for a license operation based on current conditions
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="requirements">Operation requirements</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Selected version with selection criteria</returns>
        public async Task<VersionSelectionResult> SelectBestVersionAsync(
            VersionOperationType operationType,
            VersionOperationRequirements requirements,
            CancellationToken cancellationToken = default)
        {
            if (requirements == null)
                throw new ArgumentNullException(nameof(requirements));

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Selecting best version for {OperationType} operation", operationType);

                var candidates = await GetVersionCandidatesAsync(operationType, requirements, cancellationToken);
                if (!candidates.Any())
                {
                    return new VersionSelectionResult
                    {
                        Success = false,
                        ErrorMessage = "No suitable versions available for operation"
                    };
                }

                // Apply selection strategy
                var selectedVersion = await ApplySelectionStrategyAsync(candidates, operationType, requirements, cancellationToken);

                if (selectedVersion == null)
                {
                    return new VersionSelectionResult
                    {
                        Success = false,
                        ErrorMessage = "Version selection strategy failed to select a version"
                    };
                }

                // Get runtime context for selected version
                var runtimeContext = GetRuntimeContext(selectedVersion.Version);
                if (runtimeContext == null)
                {
                    return new VersionSelectionResult
                    {
                        Success = false,
                        ErrorMessage = $"Runtime context not found for version {selectedVersion.Version}"
                    };
                }

                // Update selection metrics
                runtimeContext.RecordSelection(operationType, DateTime.UtcNow);

                return new VersionSelectionResult
                {
                    Success = true,
                    SelectedVersion = selectedVersion,
                    RuntimeContext = runtimeContext,
                    SelectionCriteria = GetSelectionCriteria(selectedVersion, candidates, requirements),
                    SelectionTime = DateTime.UtcNow
                };
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a version-specific operation with dynamic resource allocation
        /// </summary>
        /// <param name="operation">Version operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        public async Task<VersionOperationResult> ExecuteOperationAsync(
            VersionOperation operation,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Executing {OperationType} operation on version {Version}",
                    operation.OperationType, operation.Version);

                var result = new VersionOperationResult
                {
                    OperationId = operation.OperationId,
                    OperationType = operation.OperationType,
                    Version = operation.Version,
                    StartTime = DateTime.UtcNow
                };

                // Validate runtime context
                var runtimeContext = GetRuntimeContext(operation.Version);
                if (runtimeContext == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Runtime context not found for version {operation.Version}";
                    return result;
                }

                // Allocate resources for the operation
                var resourceAllocation = await _resourceManager.AllocateResourcesAsync(
                    operation.Version, operation.Requirements, cancellationToken);

                if (!resourceAllocation.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Resource allocation failed: {resourceAllocation.ErrorMessage}";
                    return result;
                }

                try
                {
                    // Apply performance optimizations
                    await _performanceOptimizer.OptimizeForOperationAsync(operation, runtimeContext);

                    // Execute the operation
                    var operationResult = await ExecuteCoreOperationAsync(operation, runtimeContext, cancellationToken);

                    result.Success = operationResult.Success;
                    result.ErrorMessage = operationResult.ErrorMessage;
                    result.ResultData = operationResult.ResultData;
                    result.Metadata = operationResult.Metadata;

                    if (result.Success)
                    {
                        // Update runtime context metrics
                        runtimeContext.RecordSuccessfulOperation(operation.OperationType, DateTime.UtcNow);
                    }
                    else
                    {
                        runtimeContext.RecordFailedOperation(operation.OperationType, DateTime.UtcNow);
                    }
                }
                finally
                {
                    // Release resources
                    await _resourceManager.ReleaseResourcesAsync(resourceAllocation);
                }

                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the current health status of all runtime contexts
        /// </summary>
        /// <returns>Health status result</returns>
        public async Task<RuntimeHealthResult> GetHealthStatusAsync()
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Getting runtime health status");

                var result = new RuntimeHealthResult();
                var healthTasks = new List<Task<VersionRuntimeHealth>>();

                lock (_managerLock)
                {
                    // Collect health data from all runtime contexts
                    foreach (var context in _runtimeContexts.Values)
                    {
                        healthTasks.Add(context.GetHealthStatusAsync());
                    }
                }

                var healthResults = await Task.WhenAll(healthTasks);
                result.VersionHealth.AddRange(healthResults);

                // Calculate overall health metrics
                result.HealthyVersionCount = healthResults.Count(h => h.IsHealthy);
                result.TotalVersionCount = healthResults.Length;
                result.OverallHealth = result.HealthyVersionCount == result.TotalVersionCount ?
                    RuntimeHealthLevel.Healthy :
                    result.HealthyVersionCount > 0 ?
                    RuntimeHealthLevel.Degraded :
                    RuntimeHealthLevel.Unhealthy;

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
        /// Refreshes version detection and updates runtime contexts
        /// </summary>
        /// <returns>Refresh result</returns>
        public async Task<RuntimeRefreshResult> RefreshVersionsAsync()
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                _logger.LogInformation("Refreshing runtime version detection");

                var result = new RuntimeRefreshResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Refresh license manager versions
                var licenseRefreshResult = await _licenseManager.RefreshVersionsAsync();
                if (!licenseRefreshResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = $"License manager refresh failed: {licenseRefreshResult.ErrorMessage}";
                    return result;
                }

                // Get current and new versions
                var currentVersions = ActiveVersions.ToList();
                var newVersions = _licenseManager.ManagedVersions.ToList();
                var newVersionSet = new HashSet<string>(newVersions.Select(v => v.Version));
                var currentVersionSet = new HashSet<string>(currentVersions.Select(v => v.Version));

                // Detect version changes
                result.AddedVersions = newVersions.Where(v => !currentVersionSet.Contains(v.Version)).ToList();
                result.RemovedVersions = currentVersions.Where(v => !newVersionSet.Contains(v.Version)).ToList();

                // Create runtime contexts for new versions
                foreach (var addedVersion in result.AddedVersions)
                {
                    await CreateRuntimeContextAsync(addedVersion, result);
                }

                // Remove runtime contexts for old versions
                foreach (var removedVersion in result.RemovedVersions)
                {
                    if (_runtimeContexts.TryGetValue(removedVersion.Version, out var context))
                    {
                        context.Dispose();
                        _runtimeContexts.Remove(removedVersion.Version);
                    }
                }

                // Update existing contexts
                foreach (var existingVersion in newVersions.Where(v => currentVersionSet.Contains(v.Version)))
                {
                    if (_runtimeContexts.TryGetValue(existingVersion.Version, out var context))
                    {
                        context.UpdateVersionInfo(existingVersion);
                    }
                }

                // Apply optimizations to new/updated contexts
                await ApplyOptimizationsToContextsAsync();

                stopwatch.Stop();
                result.RefreshTime = stopwatch.Elapsed;
                result.Success = true;

                _logger.LogInformation("Runtime version refresh completed in {Duration}ms. Added: {AddedCount}, Removed: {RemovedCount}",
                    stopwatch.ElapsedMilliseconds, result.AddedVersions.Count, result.RemovedVersions.Count);

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets runtime metrics for all managed versions
        /// </summary>
        /// <returns>Runtime metrics result</returns>
        public async Task<RuntimeMetricsResult> GetRuntimeMetricsAsync()
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Getting runtime metrics");

                var result = new RuntimeMetricsResult();
                var metricsTasks = new List<Task<VersionRuntimeMetrics>>();

                lock (_managerLock)
                {
                    foreach (var context in _runtimeContexts.Values)
                    {
                        metricsTasks.Add(context.GetMetricsAsync());
                    }
                }

                var metricsResults = await Task.WhenAll(metricsTasks);
                result.VersionMetrics.AddRange(metricsResults);

                // Calculate aggregate metrics
                result.TotalOperations = metricsResults.Sum(m => m.TotalOperations);
                result.SuccessfulOperations = metricsResults.Sum(m => m.SuccessfulOperations);
                result.FailedOperations = metricsResults.Sum(m => m.FailedOperations);
                result.AverageOperationTime = metricsResults.Any() ?
                    TimeSpan.FromTicks((long)metricsResults.Average(m => m.AverageOperationTime.Ticks)) : TimeSpan.Zero;
                result.PeakMemoryUsage = metricsResults.Max(m => m.PeakMemoryUsage);
                result.TotalCpuTime = metricsResults.Sum(m => m.TotalCpuTime);

                // Calculate success rate
                result.SuccessRate = result.TotalOperations > 0 ?
                    (double)result.SuccessfulOperations / result.TotalOperations * 100 : 0;

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Disposes the runtime manager and cleans up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the runtime manager and cleans up resources
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
                        // Dispose all runtime contexts
                        foreach (var context in _runtimeContexts.Values)
                        {
                            context.Dispose();
                        }
                        _runtimeContexts.Clear();

                        // Stop services
                        _scheduler?.Dispose();
                        _performanceOptimizer?.Dispose();
                        _resourceManager?.Dispose();

                        // Dispose timers and semaphores
                        _healthCheckTimer?.Dispose();
                        _operationSemaphore?.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        #region Private Methods

        /// <summary>
        /// Creates a runtime context for a version
        /// </summary>
        /// <param name="version">Version to create context for</param>
        /// <param name="result">Result to update with errors/warnings</param>
        private async Task CreateRuntimeContextAsync(SolidWorksVersionInfo version, RuntimeInitializationResult result)
        {
            try
            {
                var context = new VersionRuntimeContext(version, _configuration);
                await context.InitializeAsync();

                _runtimeContexts[version.Version] = context;
                result.InitializedVersions.Add(version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating runtime context for version {Version}", version.Version);
                result.Errors.Add($"Failed to create runtime context for version {version.Version}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets version candidates for selection based on operation requirements
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="requirements">Operation requirements</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of candidate versions</returns>
        private async Task<List<SolidWorksVersionInfo>> GetVersionCandidatesAsync(
            VersionOperationType operationType,
            VersionOperationRequirements requirements,
            CancellationToken cancellationToken)
        {
            var candidates = new List<SolidWorksVersionInfo>();
            var activeVersions = ActiveVersions.ToList();

            foreach (var version in activeVersions)
            {
                var context = GetRuntimeContext(version.Version);
                if (context != null && await context.IsSuitableForOperationAsync(operationType, requirements))
                {
                    candidates.Add(version);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Applies selection strategy to choose the best version
        /// </summary>
        /// <param name="candidates">Candidate versions</param>
        /// <param name="operationType">Type of operation</param>
        /// <param name="requirements">Operation requirements</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Selected version</returns>
        private async Task<SolidWorksVersionInfo> ApplySelectionStrategyAsync(
            List<SolidWorksVersionInfo> candidates,
            VersionOperationType operationType,
            VersionOperationRequirements requirements,
            CancellationToken cancellationToken)
        {
            switch (_configuration.VersionSelectionStrategy)
            {
                case VersionSelectionStrategy.NewestFirst:
                    return candidates.OrderByDescending(v => v.Version).FirstOrDefault();

                case VersionSelectionStrategy.MostStable:
                    return await SelectMostStableVersionAsync(candidates, cancellationToken);

                case VersionSelectionStrategy.BestPerformance:
                    return await SelectBestPerformanceVersionAsync(candidates, cancellationToken);

                case VersionSelectionStrategy.LoadBalanced:
                    return await SelectLoadBalancedVersionAsync(candidates, cancellationToken);

                case VersionSelectionStrategy.ResourceOptimized:
                    return await SelectResourceOptimizedVersionAsync(candidates, requirements, cancellationToken);

                default:
                    return candidates.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets the runtime context for a version
        /// </summary>
        /// <param name="version">Version string</param>
        /// <returns>Runtime context or null if not found</returns>
        private VersionRuntimeContext GetRuntimeContext(string version)
        {
            lock (_managerLock)
            {
                return _runtimeContexts.TryGetValue(version, out var context) ? context : null;
            }
        }

        /// <summary>
        /// Gets selection criteria for logging and debugging
        /// </summary>
        /// <param name="selectedVersion">Selected version</param>
        /// <param name="candidates">All candidate versions</param>
        /// <param name="requirements">Operation requirements</param>
        /// <returns>Selection criteria dictionary</returns>
        private Dictionary<string, object> GetSelectionCriteria(
            SolidWorksVersionInfo selectedVersion,
            List<SolidWorksVersionInfo> candidates,
            VersionOperationRequirements requirements)
        {
            return new Dictionary<string, object>
            {
                ["Strategy"] = _configuration.VersionSelectionStrategy.ToString(),
                ["SelectedVersion"] = selectedVersion.Version,
                ["TotalCandidates"] = candidates.Count,
                ["OperationType"] = requirements.OperationType.ToString(),
                ["Priority"] = requirements.Priority.ToString(),
                ["SelectionTimestamp"] = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes the core operation
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        /// <param name="runtimeContext">Runtime context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        private async Task<CoreOperationResult> ExecuteCoreOperationAsync(
            VersionOperation operation,
            VersionRuntimeContext runtimeContext,
            CancellationToken cancellationToken)
        {
            // This would integrate with the actual license management operations
            // For now, we'll simulate the operation

            await Task.Delay(100, cancellationToken); // Simulate work

            return new CoreOperationResult
            {
                Success = true,
                ResultData = new Dictionary<string, object>
                {
                    ["OperationCompleted"] = true,
                    ["Version"] = operation.Version,
                    ["OperationType"] = operation.OperationType.ToString()
                }
            };
        }

        /// <summary>
        /// Applies initial performance optimizations
        /// </summary>
        private async Task ApplyInitialOptimizationsAsync()
        {
            try
            {
                foreach (var context in _runtimeContexts.Values)
                {
                    await _performanceOptimizer.OptimizeContextAsync(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying initial performance optimizations");
            }
        }

        /// <summary>
        /// Applies optimizations to all contexts after version refresh
        /// </summary>
        private async Task ApplyOptimizationsToContextsAsync()
        {
            try
            {
                var optimizationTasks = _runtimeContexts.Values
                    .Select(context => _performanceOptimizer.OptimizeContextAsync(context));

                await Task.WhenAll(optimizationTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying optimizations to contexts after version refresh");
            }
        }

        /// <summary>
        /// Health check timer callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private async void HealthCheckCallback(object state)
        {
            try
            {
                await PerformHealthChecksAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check callback");
            }
        }

        /// <summary>
        /// Performs health checks on all runtime contexts
        /// </summary>
        private async Task PerformHealthChecksAsync()
        {
            try
            {
                var healthCheckTasks = _runtimeContexts.Values
                    .Select(context => context.PerformHealthCheckAsync());

                await Task.WhenAll(healthCheckTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health checks");
            }
        }

        /// <summary>
        /// Detects system-wide issues affecting all versions
        /// </summary>
        /// <param name="result">Health result to update</param>
        private async Task DetectSystemWideIssuesAsync(RuntimeHealthResult result)
        {
            try
            {
                // Check for common issues across versions
                var commonIssues = result.VersionHealth
                    .Where(h => !h.IsHealthy)
                    .GroupBy(h => h.Status)
                    .Where(g => g.Count() > result.TotalVersionCount / 2)
                    .ToList();

                foreach (var issueGroup in commonIssues)
                {
                    result.SystemWideIssues.Add(new SystemWideRuntimeIssue
                    {
                        IssueType = issueGroup.Key,
                        AffectedVersionCount = issueGroup.Count(),
                        Severity = issueGroup.Count() == result.TotalVersionCount ?
                            RuntimeIssueSeverity.Critical : RuntimeIssueSeverity.Warning,
                        Description = $"{issueGroup.Count()} versions reporting: {issueGroup.Key}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting system-wide runtime issues");
            }
        }

        /// <summary>
        /// Selects the most stable version based on historical performance
        /// </summary>
        private async Task<SolidWorksVersionInfo> SelectMostStableVersionAsync(
            List<SolidWorksVersionInfo> candidates,
            CancellationToken cancellationToken)
        {
            var stabilityTasks = candidates.Select(async version =>
            {
                var context = GetRuntimeContext(version.Version);
                var metrics = await context?.GetMetricsAsync();
                return new { Version = version, StabilityScore = metrics?.StabilityScore ?? 0 };
            });

            var stabilityResults = await Task.WhenAll(stabilityTasks);
            return stabilityResults.OrderByDescending(r => r.StabilityScore).FirstOrDefault()?.Version;
        }

        /// <summary>
        /// Selects the version with best performance
        /// </summary>
        private async Task<SolidWorksVersionInfo> SelectBestPerformanceVersionAsync(
            List<SolidWorksVersionInfo> candidates,
            CancellationToken cancellationToken)
        {
            var performanceTasks = candidates.Select(async version =>
            {
                var context = GetRuntimeContext(version.Version);
                var metrics = await context?.GetMetricsAsync();
                return new { Version = version, PerformanceScore = metrics?.PerformanceScore ?? 0 };
            });

            var performanceResults = await Task.WhenAll(performanceTasks);
            return performanceResults.OrderByDescending(r => r.PerformanceScore).FirstOrDefault()?.Version;
        }

        /// <summary>
        /// Selects a version using load balancing strategy
        /// </summary>
        private async Task<SolidWorksVersionInfo> SelectLoadBalancedVersionAsync(
            List<SolidWorksVersionInfo> candidates,
            CancellationToken cancellationToken)
        {
            var loadTasks = candidates.Select(async version =>
            {
                var context = GetRuntimeContext(version.Version);
                var metrics = await context?.GetMetricsAsync();
                return new { Version = version, LoadScore = metrics?.CurrentLoad ?? 0 };
            });

            var loadResults = await Task.WhenAll(loadTasks);
            return loadResults.OrderBy(r => r.LoadScore).FirstOrDefault()?.Version;
        }

        /// <summary>
        /// Selects a version optimized for resource usage
        /// </summary>
        private async Task<SolidWorksVersionInfo> SelectResourceOptimizedVersionAsync(
            List<SolidWorksVersionInfo> candidates,
            VersionOperationRequirements requirements,
            CancellationToken cancellationToken)
        {
            var resourceTasks = candidates.Select(async version =>
            {
                var context = GetRuntimeContext(version.Version);
                var metrics = await context?.GetMetricsAsync();
                var resourceScore = CalculateResourceScore(metrics, requirements);
                return new { Version = version, ResourceScore = resourceScore };
            });

            var resourceResults = await Task.WhenAll(resourceTasks);
            return resourceResults.OrderByDescending(r => r.ResourceScore).FirstOrDefault()?.Version;
        }

        /// <summary>
        /// Calculates resource optimization score
        /// </summary>
        /// <param name="metrics">Version metrics</param>
        /// <param name="requirements">Operation requirements</param>
        /// <returns>Resource score</returns>
        private double CalculateResourceScore(VersionRuntimeMetrics metrics, VersionOperationRequirements requirements)
        {
            if (metrics == null) return 0;

            var memoryScore = requirements.MemoryRequirements > 0 ?
                1.0 - (metrics.MemoryUsage / (double)requirements.MemoryRequirements) : 1.0;

            var cpuScore = requirements.CpuRequirements > 0 ?
                1.0 - (metrics.CpuUsage / (double)requirements.CpuRequirements) : 1.0;

            return Math.Max(0, (memoryScore + cpuScore) / 2);
        }

        #endregion
    }

    /// <summary>
    /// Configuration for runtime management
    /// </summary>
    public class RuntimeManagementConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum concurrent operations
        /// </summary>
        public int MaxConcurrentOperations { get; set; } = 10;

        /// <summary>
        /// Gets or sets the version selection strategy
        /// </summary>
        public VersionSelectionStrategy VersionSelectionStrategy { get; set; } = VersionSelectionStrategy.NewestFirst;

        /// <summary>
        /// Gets or sets the health check interval in minutes
        /// </summary>
        public int HealthCheckIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets the operation timeout
        /// </summary>
        public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets whether to enable automatic resource cleanup
        /// </summary>
        public bool EnableAutomaticCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets the cleanup interval in minutes
        /// </summary>
        public int CleanupIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum operation history size
        /// </summary>
        public int MaxOperationHistory { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether to enable detailed logging
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;
    }

    /// <summary>
    /// Version selection strategies
    /// </summary>
    public enum VersionSelectionStrategy
    {
        /// <summary>
        /// Select newest versions first
        /// </summary>
        NewestFirst,

        /// <summary>
        /// Select most stable versions based on historical data
        /// </summary>
        MostStable,

        /// <summary>
        /// Select versions with best performance
        /// </summary>
        BestPerformance,

        /// <summary>
        /// Balance load across versions
        /// </summary>
        LoadBalanced,

        /// <summary>
        /// Optimize for resource usage
        /// </summary>
        ResourceOptimized
    }

    /// <summary>
    /// Types of version operations
    /// </summary>
    public enum VersionOperationType
    {
        /// <summary>
        /// License query operation
        /// </summary>
        Query,

        /// <summary>
        /// License allocation operation
        /// </summary>
        Allocate,

        /// <summary>
        /// License release operation
        /// </summary>
        Release,

        /// <summary>
        /// Health check operation
        /// </summary>
        HealthCheck,

        /// <summary>
        /// Maintenance operation
        /// </summary>
        Maintenance
    }

    /// <summary>
    /// Runtime health levels
    /// </summary>
    public enum RuntimeHealthLevel
    {
        /// <summary>
        /// All versions healthy
        /// </summary>
        Healthy,

        /// <summary>
        /// Some versions have issues
        /// </summary>
        Degraded,

        /// <summary>
        /// Most or all versions have issues
        /// </summary>
        Unhealthy
    }

    /// <summary>
    /// Runtime issue severity levels
    /// </summary>
    public enum RuntimeIssueSeverity
    {
        /// <summary>
        /// Informational issue
        /// </summary>
        Info,

        /// <summary>
        /// Warning level issue
        /// </summary>
        Warning,

        /// <summary>
        /// Critical issue
        /// </summary>
        Critical
    }
}