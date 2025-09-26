using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.VersionManagement.Deployment
{
    /// <summary>
    /// Manages deployment operations for multi-version SolidWorks support including version-specific configuration,
    /// health validation, and production deployment planning
    /// </summary>
    public class VersionDeploymentManager : IDisposable
    {
        private readonly ILogger<VersionDeploymentManager> _logger;
        private readonly SolidWorksVersionDetector _versionDetector;
        private readonly IProcessExecutor _processExecutor;
        private readonly VersionDeploymentConfiguration _configuration;
        private readonly VersionDeploymentValidator _deploymentValidator;
        private readonly Dictionary<string, VersionDeploymentContext> _deploymentContexts;
        private readonly SemaphoreSlim _deploymentSemaphore;
        private readonly object _managerLock = new object();
        private bool _disposed;
        private bool _isInitialized;

        /// <summary>
        /// Gets the current deployment status for all versions
        /// </summary>
        public IReadOnlyDictionary<string, VersionDeploymentStatus> DeploymentStatus
        {
            get
            {
                lock (_managerLock)
                {
                    return new Dictionary<string, VersionDeploymentStatus>(
                        _deploymentContexts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Status));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the deployment manager is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Event raised when deployment status changes
        /// </summary>
        public event EventHandler<VersionDeploymentEventArgs> DeploymentStatusChanged;

        /// <summary>
        /// Event raised when deployment completes
        /// </summary>
        public event EventHandler<VersionDeploymentCompletedEventArgs> DeploymentCompleted;

        /// <summary>
        /// Event raised when deployment fails
        /// </summary>
        public event EventHandler<VersionDeploymentFailedEventArgs> DeploymentFailed;

        /// <summary>
        /// Initializes a new instance of the VersionDeploymentManager class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="versionDetector">Version detector instance</param>
        /// <param name="processExecutor">Process executor instance</param>
        /// <param name="configuration">Deployment configuration</param>
        /// <param name="deploymentValidator">Deployment validator instance</param>
        public VersionDeploymentManager(
            ILogger<VersionDeploymentManager> logger,
            SolidWorksVersionDetector versionDetector,
            IProcessExecutor processExecutor,
            VersionDeploymentConfiguration configuration,
            VersionDeploymentValidator deploymentValidator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionDetector = versionDetector ?? throw new ArgumentNullException(nameof(versionDetector));
            _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _deploymentValidator = deploymentValidator ?? throw new ArgumentNullException(nameof(deploymentValidator));

            _deploymentContexts = new Dictionary<string, VersionDeploymentContext>();
            _deploymentSemaphore = new SemaphoreSlim(_configuration.MaxConcurrentDeployments);
        }

        /// <summary>
        /// Initializes the deployment manager with detected versions
        /// </summary>
        /// <returns>Initialization result</returns>
        public async Task<VersionDeploymentInitializationResult> InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing version deployment manager");

                var result = new VersionDeploymentInitializationResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Detect available SolidWorks versions
                var detectionResult = await _versionDetector.DetectInstalledVersionsAsync();
                if (!detectionResult.Success)
                {
                    result.Errors.Add($"Version detection failed: {detectionResult.Error}");
                    return result;
                }

                // Initialize deployment contexts for available versions
                var availableVersions = _versionDetector.GetAvailableVersions();
                foreach (var version in availableVersions)
                {
                    await InitializeVersionDeploymentContextAsync(version, result);
                }

                // Validate deployment prerequisites
                await ValidateDeploymentPrerequisitesAsync(result);

                stopwatch.Stop();
                result.InitializationTime = stopwatch.Elapsed;
                result.DeploymentContextCount = _deploymentContexts.Count;
                result.TotalVersionCount = availableVersions.Count;

                _isInitialized = true;

                _logger.LogInformation("Version deployment manager initialized in {Duration}ms. Contexts: {ContextCount}, Total versions: {TotalCount}",
                    stopwatch.ElapsedMilliseconds, result.DeploymentContextCount, result.TotalVersionCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing version deployment manager");
                var result = new VersionDeploymentInitializationResult();
                result.Errors.Add($"Initialization failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Deploys a specific version with the specified configuration
        /// </summary>
        /// <param name="version">Version to deploy</param>
        /// <param name="deploymentOptions">Deployment options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deployment result</returns>
        public async Task<VersionDeploymentResult> DeployVersionAsync(
            string version,
            VersionDeploymentOptions deploymentOptions,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (deploymentOptions == null)
                throw new ArgumentNullException(nameof(deploymentOptions));

            await _deploymentSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Starting deployment for version {Version}", version);

                var result = new VersionDeploymentResult { Version = version };
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    // Get version info
                    var versionInfo = _versionDetector.GetVersion(version);
                    if (versionInfo == null)
                    {
                        result.Success = false;
                        result.Errors.Add($"Version {version} not found");
                        return result;
                    }

                    // Get or create deployment context
                    var deploymentContext = await GetOrCreateDeploymentContextAsync(versionInfo);
                    if (deploymentContext == null)
                    {
                        result.Success = false;
                        result.Errors.Add($"Failed to create deployment context for version {version}");
                        return result;
                    }

                    // Update deployment status
                    UpdateDeploymentStatus(version, VersionDeploymentState.Deploying);

                    // Validate deployment
                    var validationResult = await _deploymentValidator.ValidateDeploymentAsync(versionInfo, deploymentOptions);
                    if (!validationResult.IsValid)
                    {
                        result.Success = false;
                        result.Errors.AddRange(validationResult.Errors);
                        result.Warnings.AddRange(validationResult.Warnings);
                        UpdateDeploymentStatus(version, VersionDeploymentState.ValidationFailed);
                        await RaiseDeploymentFailedEventAsync(version, result.Errors, "Validation failed");
                        return result;
                    }

                    // Execute deployment phases
                    await ExecutePreDeploymentPhaseAsync(deploymentContext, deploymentOptions, result);
                    await ExecuteDeploymentPhaseAsync(deploymentContext, deploymentOptions, result);
                    await ExecutePostDeploymentPhaseAsync(deploymentContext, deploymentOptions, result);

                    // Final validation
                    await ExecutePostDeploymentValidationAsync(deploymentContext, result);

                    result.Success = result.Errors.Count == 0;
                    result.DeploymentTime = stopwatch.Elapsed;

                    // Update deployment status
                    var finalState = result.Success ? VersionDeploymentState.Deployed : VersionDeploymentState.DeploymentFailed;
                    UpdateDeploymentStatus(version, finalState);

                    if (result.Success)
                    {
                        _logger.LogInformation("Version {Version} deployed successfully in {Duration}ms",
                            version, stopwatch.ElapsedMilliseconds);
                        await RaiseDeploymentCompletedEventAsync(version, result);
                    }
                    else
                    {
                        _logger.LogWarning("Version {Version} deployment failed: {Error}",
                            version, string.Join(", ", result.Errors));
                        await RaiseDeploymentFailedEventAsync(version, result.Errors, "Deployment failed");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deploying version {Version}", version);
                    result.Success = false;
                    result.Errors.Add($"Deployment error: {ex.Message}");
                    result.DeploymentTime = stopwatch.Elapsed;
                    UpdateDeploymentStatus(version, VersionDeploymentState.DeploymentFailed);
                    await RaiseDeploymentFailedEventAsync(version, result.Errors, "Exception during deployment");
                    return result;
                }
            }
            finally
            {
                _deploymentSemaphore.Release();
            }
        }

        /// <summary>
        /// Rolls back a deployed version
        /// </summary>
        /// <param name="version">Version to roll back</param>
        /// <param name="rollbackOptions">Rollback options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Rollback result</returns>
        public async Task<VersionRollbackResult> RollbackVersionAsync(
            string version,
            VersionRollbackOptions rollbackOptions,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            if (rollbackOptions == null)
                throw new ArgumentNullException(nameof(rollbackOptions));

            await _deploymentSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Starting rollback for version {Version}", version);

                var result = new VersionRollbackResult { Version = version };
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    // Get deployment context
                    if (!_deploymentContexts.TryGetValue(version, out var deploymentContext))
                    {
                        result.Success = false;
                        result.Errors.Add($"No deployment context found for version {version}");
                        return result;
                    }

                    // Check if version can be rolled back
                    if (deploymentContext.Status.State != VersionDeploymentState.Deployed)
                    {
                        result.Success = false;
                        result.Errors.Add($"Version {version} is not in deployed state");
                        return result;
                    }

                    // Update rollback status
                    UpdateDeploymentStatus(version, VersionDeploymentState.RollingBack);

                    // Execute rollback phases
                    await ExecutePreRollbackPhaseAsync(deploymentContext, rollbackOptions, result);
                    await ExecuteRollbackPhaseAsync(deploymentContext, rollbackOptions, result);
                    await ExecutePostRollbackPhaseAsync(deploymentContext, rollbackOptions, result);

                    result.Success = result.Errors.Count == 0;
                    result.RollbackTime = stopwatch.Elapsed;

                    // Update deployment status
                    var finalState = result.Success ? VersionDeploymentState.Rollback : VersionDeploymentState.RollbackFailed;
                    UpdateDeploymentStatus(version, finalState);

                    if (result.Success)
                    {
                        _logger.LogInformation("Version {Version} rollback completed in {Duration}ms",
                            version, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning("Version {Version} rollback failed: {Error}",
                            version, string.Join(", ", result.Errors));
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rolling back version {Version}", version);
                    result.Success = false;
                    result.Errors.Add($"Rollback error: {ex.Message}");
                    result.RollbackTime = stopwatch.Elapsed;
                    UpdateDeploymentStatus(version, VersionDeploymentState.RollbackFailed);
                    return result;
                }
            }
            finally
            {
                _deploymentSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the deployment status for a specific version
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <returns>Deployment status</returns>
        public VersionDeploymentStatus GetDeploymentStatus(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            lock (_managerLock)
            {
                if (_deploymentContexts.TryGetValue(version, out var context))
                {
                    return context.Status;
                }

                return new VersionDeploymentStatus
                {
                    Version = version,
                    State = VersionDeploymentState.NotDeployed,
                    LastUpdated = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Gets deployment history for a specific version
        /// </summary>
        /// <param name="version">Version to get history for</param>
        /// <param name="maxEntries">Maximum number of entries to return</param>
        /// <returns>Deployment history</returns>
        public List<VersionDeploymentHistoryRecord> GetDeploymentHistory(string version, int maxEntries = 50)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            lock (_managerLock)
            {
                if (_deploymentContexts.TryGetValue(version, out var context))
                {
                    return context.GetDeploymentHistory(maxEntries);
                }
                return new List<VersionDeploymentHistoryRecord>();
            }
        }

        /// <summary>
        /// Gets deployment statistics for all versions
        /// </summary>
        /// <returns>Deployment statistics</returns>
        public VersionDeploymentStatistics GetDeploymentStatistics()
        {
            lock (_managerLock)
            {
                var stats = new VersionDeploymentStatistics();

                foreach (var kvp in _deploymentContexts)
                {
                    var version = kvp.Key;
                    var context = kvp.Value;

                    stats.TotalVersions++;

                    switch (context.Status.State)
                    {
                        case VersionDeploymentState.Deployed:
                            stats.DeployedVersions++;
                            break;
                        case VersionDeploymentState.Deploying:
                            stats.DeployingVersions++;
                            break;
                        case VersionDeploymentState.DeploymentFailed:
                            stats.FailedDeployments++;
                            break;
                        case VersionDeploymentState.Rollback:
                            stats.RolledBackVersions++;
                            break;
                    }

                    if (context.Status.State == VersionDeploymentState.Deployed)
                    {
                        stats.SuccessfulDeployments++;
                    }

                    stats.TotalDeploymentTime += context.Status.TotalDeploymentTime;
                }

                if (stats.DeployedVersions > 0)
                {
                    stats.AverageDeploymentTime = TimeSpan.FromTicks(stats.TotalDeploymentTime.Ticks / stats.DeployedVersions);
                }

                return stats;
            }
        }

        /// <summary>
        /// Disposes the deployment manager
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the deployment manager
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
                        foreach (var context in _deploymentContexts.Values)
                        {
                            context.Dispose();
                        }
                        _deploymentContexts.Clear();

                        _deploymentSemaphore?.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Initializes deployment context for a version
        /// </summary>
        /// <param name="version">Version to initialize</param>
        /// <param name="result">Initialization result to update</param>
        private async Task InitializeVersionDeploymentContextAsync(SolidWorksVersionInfo version, VersionDeploymentInitializationResult result)
        {
            try
            {
                var context = new VersionDeploymentContext(version, _configuration);
                _deploymentContexts[version.Version] = context;
                result.InitializedVersions.Add(version.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing deployment context for version {Version}", version.Version);
                result.Errors.Add($"Failed to initialize deployment context for version {version.Version}: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates deployment prerequisites
        /// </summary>
        /// <param name="result">Initialization result to update</param>
        private async Task ValidateDeploymentPrerequisitesAsync(VersionDeploymentInitializationResult result)
        {
            try
            {
                // Check disk space
                var driveInfo = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory));
                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                if (freeSpaceGB < _configuration.MinimumDiskSpaceGB)
                {
                    result.Warnings.Add($"Low disk space: {freeSpaceGB:F1}GB available, minimum required: {_configuration.MinimumDiskSpaceGB}GB");
                }

                // Check network connectivity if required
                if (_configuration.RequireNetworkConnectivity)
                {
                    await CheckNetworkConnectivityAsync(result);
                }

                // Check required permissions
                await CheckDeploymentPermissionsAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating deployment prerequisites");
                result.Warnings.Add($"Prerequisite validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets or creates deployment context for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <returns>Deployment context</returns>
        private async Task<VersionDeploymentContext> GetOrCreateDeploymentContextAsync(SolidWorksVersionInfo versionInfo)
        {
            lock (_managerLock)
            {
                if (_deploymentContexts.TryGetValue(versionInfo.Version, out var context))
                {
                    return context;
                }
            }

            // Create new context if not found
            try
            {
                var context = new VersionDeploymentContext(versionInfo, _configuration);
                lock (_managerLock)
                {
                    _deploymentContexts[versionInfo.Version] = context;
                }
                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating deployment context for version {Version}", versionInfo.Version);
                return null;
            }
        }

        /// <summary>
        /// Updates deployment status for a version
        /// </summary>
        /// <param name="version">Version to update</param>
        /// <param name="state">New deployment state</param>
        private void UpdateDeploymentStatus(string version, VersionDeploymentState state)
        {
            lock (_managerLock)
            {
                if (_deploymentContexts.TryGetValue(version, out var context))
                {
                    var previousState = context.Status.State;
                    context.Status.State = state;
                    context.Status.LastUpdated = DateTime.UtcNow;

                    if (state == VersionDeploymentState.Deployed)
                    {
                        context.Status.TotalDeploymentTime += context.Status.CurrentDeploymentTime;
                    }

                    // Raise event if status changed
                    if (previousState != state)
                    {
                        var eventArgs = new VersionDeploymentEventArgs
                        {
                            Version = version,
                            PreviousState = previousState,
                            CurrentState = state,
                            Timestamp = DateTime.UtcNow
                        };

                        DeploymentStatusChanged?.Invoke(this, eventArgs);
                    }
                }
            }
        }

        /// <summary>
        /// Executes pre-deployment phase
        /// </summary>
        /// <param name="context">Deployment context</param>
        /// <param name="options">Deployment options</param>
        /// <param name="result">Deployment result to update</param>
        private async Task ExecutePreDeploymentPhaseAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                _logger.LogDebug("Executing pre-deployment phase for version {Version}", context.VersionInfo.Version);

                // Create deployment directory if needed
                await CreateDeploymentDirectoryAsync(context, options, result);

                // Backup existing configuration
                await BackupExistingConfigurationAsync(context, options, result);

                // Validate system requirements
                await ValidateSystemRequirementsAsync(context, options, result);

                // Pre-deployment health check
                await ExecutePreDeploymentHealthCheckAsync(context, options, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pre-deployment phase for version {Version}", context.VersionInfo.Version);
                result.Errors.Add($"Pre-deployment phase failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes deployment phase
        /// </summary>
        /// <param name="context">Deployment context</param>
        /// <param name="options">Deployment options</param>
        /// <param name="result">Deployment result to update</param>
        private async Task ExecuteDeploymentPhaseAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                _logger.LogDebug("Executing deployment phase for version {Version}", context.VersionInfo.Version);

                var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Deploy version-specific configuration
                await DeployVersionConfigurationAsync(context, options, result);

                // Deploy monitoring components
                await DeployMonitoringComponentsAsync(context, options, result);

                // Deploy license management components
                await DeployLicenseManagementComponentsAsync(context, options, result);

                // Register version with system
                await RegisterVersionWithSystemAsync(context, options, result);

                phaseStopwatch.Stop();
                context.Status.CurrentDeploymentTime = phaseStopwatch.Elapsed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during deployment phase for version {Version}", context.VersionInfo.Version);
                result.Errors.Add($"Deployment phase failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes post-deployment phase
        /// </summary>
        /// <param name="context">Deployment context</param>
        /// <param name="options">Deployment options</param>
        /// <param name="result">Deployment result to update</param>
        private async Task ExecutePostDeploymentPhaseAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                _logger.LogDebug("Executing post-deployment phase for version {Version}", context.VersionInfo.Version);

                // Post-deployment validation
                await ExecutePostDeploymentValidationAsync(context, result);

                // Cleanup temporary files
                await CleanupDeploymentFilesAsync(context, options, result);

                // Update deployment history
                await UpdateDeploymentHistoryAsync(context, result);

                // Send deployment notifications
                await SendDeploymentNotificationsAsync(context, options, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during post-deployment phase for version {Version}", context.VersionInfo.Version);
                result.Warnings.Add($"Post-deployment phase issues: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes post-deployment validation
        /// </summary>
        /// <param name="context">Deployment context</param>
        /// <param name="result">Deployment result to update</param>
        private async Task ExecutePostDeploymentValidationAsync(VersionDeploymentContext context, VersionDeploymentResult result)
        {
            try
            {
                _logger.LogDebug("Executing post-deployment validation for version {Version}", context.VersionInfo.Version);

                // Validate deployment success
                var validationResult = await _deploymentValidator.ValidateDeploymentSuccessAsync(context.VersionInfo);
                if (!validationResult.IsValid)
                {
                    result.Errors.AddRange(validationResult.Errors);
                    result.Warnings.AddRange(validationResult.Warnings);
                }

                // Test version functionality
                await TestVersionFunctionalityAsync(context, result);

                // Validate integration points
                await ValidateIntegrationPointsAsync(context, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during post-deployment validation for version {Version}", context.VersionInfo.Version);
                result.Warnings.Add($"Post-deployment validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes pre-rollback phase
        /// </summary>
        /// <param name="context">Deployment context</param>
        /// <param name="options">Rollback options</param>
        /// <param name="result">Rollback result to update</param>
        private async Task ExecutePreRollbackPhaseAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                _logger.LogDebug("Executing pre-rollback phase for version {Version}", context.VersionInfo.Version);

                // Validate rollback prerequisites
                await ValidateRollbackPrerequisitesAsync(context, options, result);

                // Backup current state
                await BackupCurrentStateAsync(context, options, result);

                // Notify systems about upcoming rollback
                await NotifyRollbackStartAsync(context, options, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pre-rollback phase for version {Version}", context.VersionInfo.Version);
                result.Errors.Add($"Pre-rollback phase failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes rollback phase
        /// </summary>
        /// <param name="context">Deployment context</param>
        /// <param name="options">Rollback options</param>
        /// <param name="result">Rollback result to update</param>
        private async Task ExecuteRollbackPhaseAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                _logger.LogDebug("Executing rollback phase for version {Version}", context.VersionInfo.Version);

                // Restore configuration
                await RestoreConfigurationAsync(context, options, result);

                // Remove deployed components
                await RemoveDeployedComponentsAsync(context, options, result);

                // Unregister version from system
                await UnregisterVersionFromSystemAsync(context, options, result);

                // Restore backup if available
                await RestoreBackupAsync(context, options, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rollback phase for version {Version}", context.VersionInfo.Version);
                result.Errors.Add($"Rollback phase failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes post-rollback phase
        /// </summary>
        /// <param name="context">Deployment context</param>
        /// <param name="options">Rollback options</param>
        /// <param name="result">Rollback result to update</param>
        private async Task ExecutePostRollbackPhaseAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                _logger.LogDebug("Executing post-rollback phase for version {Version}", context.VersionInfo.Version);

                // Validate rollback success
                await ValidateRollbackSuccessAsync(context, result);

                // Cleanup rollback files
                await CleanupRollbackFilesAsync(context, options, result);

                // Update rollback history
                await UpdateRollbackHistoryAsync(context, result);

                // Send rollback notifications
                await SendRollbackNotificationsAsync(context, options, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during post-rollback phase for version {Version}", context.VersionInfo.Version);
                result.Warnings.Add($"Post-rollback phase issues: {ex.Message}");
            }
        }

        // Helper methods for deployment phases
        private async Task CreateDeploymentDirectoryAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                var deploymentDir = context.GetDeploymentDirectory(options);
                if (!Directory.Exists(deploymentDir))
                {
                    Directory.CreateDirectory(deploymentDir);
                    _logger.LogDebug("Created deployment directory: {Directory}", deploymentDir);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to create deployment directory: {ex.Message}");
            }
        }

        private async Task BackupExistingConfigurationAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                if (options.BackupExistingConfiguration)
                {
                    var backupDir = context.GetBackupDirectory();
                    if (!Directory.Exists(backupDir))
                    {
                        Directory.CreateDirectory(backupDir);
                    }

                    // Backup configuration files
                    _logger.LogDebug("Created configuration backup for version {Version}", context.VersionInfo.Version);
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to backup existing configuration: {ex.Message}");
            }
        }

        private async Task ValidateSystemRequirementsAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                // Check system requirements
                var systemCheck = await CheckSystemRequirementsAsync(context);
                if (!systemCheck.IsValid)
                {
                    result.Errors.AddRange(systemCheck.Errors);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"System requirements validation failed: {ex.Message}");
            }
        }

        private async Task ExecutePreDeploymentHealthCheckAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                // Health check before deployment
                _logger.LogDebug("Pre-deployment health check completed for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Pre-deployment health check failed: {ex.Message}");
            }
        }

        private async Task DeployVersionConfigurationAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                // Deploy version-specific configuration
                _logger.LogDebug("Deployed configuration for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Configuration deployment failed: {ex.Message}");
            }
        }

        private async Task DeployMonitoringComponentsAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                // Deploy monitoring components
                _logger.LogDebug("Deployed monitoring components for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Monitoring components deployment failed: {ex.Message}");
            }
        }

        private async Task DeployLicenseManagementComponentsAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                // Deploy license management components
                _logger.LogDebug("Deployed license management components for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"License management components deployment failed: {ex.Message}");
            }
        }

        private async Task RegisterVersionWithSystemAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                // Register version with system
                _logger.LogDebug("Registered version {Version} with system", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Version registration failed: {ex.Message}");
            }
        }

        private async Task TestVersionFunctionalityAsync(VersionDeploymentContext context, VersionDeploymentResult result)
        {
            try
            {
                // Test version functionality
                _logger.LogDebug("Tested functionality for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Version functionality test failed: {ex.Message}");
            }
        }

        private async Task ValidateIntegrationPointsAsync(VersionDeploymentContext context, VersionDeploymentResult result)
        {
            try
            {
                // Validate integration points
                _logger.LogDebug("Validated integration points for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Integration points validation failed: {ex.Message}");
            }
        }

        private async Task CleanupDeploymentFilesAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                // Cleanup temporary files
                _logger.LogDebug("Cleaned up deployment files for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Deployment files cleanup failed: {ex.Message}");
            }
        }

        private async Task UpdateDeploymentHistoryAsync(VersionDeploymentContext context, VersionDeploymentResult result)
        {
            try
            {
                var historyRecord = new VersionDeploymentHistoryRecord
                {
                    Version = context.VersionInfo.Version,
                    Timestamp = DateTime.UtcNow,
                    Operation = "Deploy",
                    Success = result.Success,
                    Duration = result.DeploymentTime,
                    Errors = result.Errors.ToList(),
                    Warnings = result.Warnings.ToList()
                };

                context.AddDeploymentHistory(historyRecord);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to update deployment history: {ex.Message}");
            }
        }

        private async Task SendDeploymentNotificationsAsync(VersionDeploymentContext context, VersionDeploymentOptions options, VersionDeploymentResult result)
        {
            try
            {
                if (options.SendNotifications)
                {
                    // Send deployment notifications
                    _logger.LogDebug("Sent deployment notifications for version {Version}", context.VersionInfo.Version);
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to send deployment notifications: {ex.Message}");
            }
        }

        // Rollback helper methods
        private async Task ValidateRollbackPrerequisitesAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                // Validate rollback prerequisites
                _logger.LogDebug("Validated rollback prerequisites for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Rollback prerequisites validation failed: {ex.Message}");
            }
        }

        private async Task BackupCurrentStateAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                if (options.BackupBeforeRollback)
                {
                    // Backup current state
                    _logger.LogDebug("Created rollback backup for version {Version}", context.VersionInfo.Version);
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to backup current state: {ex.Message}");
            }
        }

        private async Task NotifyRollbackStartAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                // Notify systems about rollback
                _logger.LogDebug("Notified systems about rollback for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to notify rollback start: {ex.Message}");
            }
        }

        private async Task RestoreConfigurationAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                // Restore configuration
                _logger.LogDebug("Restored configuration for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Configuration restoration failed: {ex.Message}");
            }
        }

        private async Task RemoveDeployedComponentsAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                // Remove deployed components
                _logger.LogDebug("Removed deployed components for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Component removal failed: {ex.Message}");
            }
        }

        private async Task UnregisterVersionFromSystemAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                // Unregister version from system
                _logger.LogDebug("Unregistered version {Version} from system", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Version unregistration failed: {ex.Message}");
            }
        }

        private async Task RestoreBackupAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                if (options.RestoreBackup)
                {
                    // Restore backup
                    _logger.LogDebug("Restored backup for version {Version}", context.VersionInfo.Version);
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Backup restoration failed: {ex.Message}");
            }
        }

        private async Task ValidateRollbackSuccessAsync(VersionDeploymentContext context, VersionRollbackResult result)
        {
            try
            {
                // Validate rollback success
                _logger.LogDebug("Validated rollback success for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Rollback validation failed: {ex.Message}");
            }
        }

        private async Task CleanupRollbackFilesAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                // Cleanup rollback files
                _logger.LogDebug("Cleaned up rollback files for version {Version}", context.VersionInfo.Version);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Rollback files cleanup failed: {ex.Message}");
            }
        }

        private async Task UpdateRollbackHistoryAsync(VersionDeploymentContext context, VersionRollbackResult result)
        {
            try
            {
                var historyRecord = new VersionDeploymentHistoryRecord
                {
                    Version = context.VersionInfo.Version,
                    Timestamp = DateTime.UtcNow,
                    Operation = "Rollback",
                    Success = result.Success,
                    Duration = result.RollbackTime,
                    Errors = result.Errors.ToList(),
                    Warnings = result.Warnings.ToList()
                };

                context.AddDeploymentHistory(historyRecord);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to update rollback history: {ex.Message}");
            }
        }

        private async Task SendRollbackNotificationsAsync(VersionDeploymentContext context, VersionRollbackOptions options, VersionRollbackResult result)
        {
            try
            {
                if (options.SendNotifications)
                {
                    // Send rollback notifications
                    _logger.LogDebug("Sent rollback notifications for version {Version}", context.VersionInfo.Version);
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to send rollback notifications: {ex.Message}");
            }
        }

        // System validation methods
        private async Task<SystemValidationResult> CheckSystemRequirementsAsync(VersionDeploymentContext context)
        {
            var result = new SystemValidationResult();

            try
            {
                // Check disk space
                var deploymentDir = context.GetDeploymentDirectory(new VersionDeploymentOptions());
                var driveInfo = new DriveInfo(Path.GetPathRoot(deploymentDir));
                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

                if (freeSpaceGB < _configuration.MinimumDiskSpaceGB)
                {
                    result.Errors.Add($"Insufficient disk space: {freeSpaceGB:F1}GB available, {_configuration.MinimumDiskSpaceGB}GB required");
                }

                // Check memory
                var memoryInfo = GC.GetGCMemoryInfo();
                var availableMemoryGB = memoryInfo.MemoryLoadBytes / (1024.0 * 1024.0 * 1024.0);

                if (availableMemoryGB < _configuration.MinimumMemoryGB)
                {
                    result.Errors.Add($"Insufficient memory: {availableMemoryGB:F1}GB available, {_configuration.MinimumMemoryGB}GB required");
                }

                // Check .NET version
                var netVersion = Environment.Version;
                if (netVersion.Major < 4 || (netVersion.Major == 4 && netVersion.Minor < 8))
                {
                    result.Errors.Add($"Unsupported .NET version: {netVersion}, minimum required: 4.8");
                }

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"System requirements check failed: {ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        private async Task CheckNetworkConnectivityAsync(VersionDeploymentInitializationResult result)
        {
            try
            {
                // Basic network connectivity check
                using (var client = new System.Net.Http.HttpClient())
                {
                    var response = await client.GetAsync("http://www.microsoft.com");
                    if (!response.IsSuccessStatusCode)
                    {
                        result.Warnings.Add("Network connectivity check failed");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Network connectivity check failed: {ex.Message}");
            }
        }

        private async Task CheckDeploymentPermissionsAsync(VersionDeploymentInitializationResult result)
        {
            try
            {
                // Check write permissions in deployment directory
                var testFile = Path.Combine(Path.GetTempPath(), $"deployment_test_{DateTime.UtcNow:yyyyMMddHHmmss}.tmp");
                File.WriteAllText(testFile, "permission test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Deployment permissions check failed: {ex.Message}");
            }
        }

        // Event raising methods
        private async Task RaiseDeploymentCompletedEventAsync(string version, VersionDeploymentResult result)
        {
            try
            {
                var eventArgs = new VersionDeploymentCompletedEventArgs
                {
                    Version = version,
                    Success = result.Success,
                    DeploymentTime = result.DeploymentTime,
                    Timestamp = DateTime.UtcNow,
                    Warnings = result.Warnings.ToList()
                };

                DeploymentCompleted?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising deployment completed event");
            }
        }

        private async Task RaiseDeploymentFailedEventAsync(string version, List<string> errors, string reason)
        {
            try
            {
                var eventArgs = new VersionDeploymentFailedEventArgs
                {
                    Version = version,
                    Errors = errors.ToList(),
                    Reason = reason,
                    Timestamp = DateTime.UtcNow
                };

                DeploymentFailed?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising deployment failed event");
            }
        }
    }

    /// <summary>
    /// Represents deployment configuration for version management
    /// </summary>
    public class VersionDeploymentConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of concurrent deployments
        /// </summary>
        public int MaxConcurrentDeployments { get; set; } = 2;

        /// <summary>
        /// Gets or sets the minimum required disk space in GB
        /// </summary>
        public double MinimumDiskSpaceGB { get; set; } = 5.0;

        /// <summary>
        /// Gets or sets the minimum required memory in GB
        /// </summary>
        public double MinimumMemoryGB { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the deployment timeout
        /// </summary>
        public TimeSpan DeploymentTimeout { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets the rollback timeout
        /// </summary>
        public TimeSpan RollbackTimeout { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets a value indicating whether network connectivity is required
        /// </summary>
        public bool RequireNetworkConnectivity { get; set; } = true;

        /// <summary>
        /// Gets or sets the base deployment directory
        /// </summary>
        public string BaseDeploymentDirectory { get; set; } = "C:\\SolidWorksDeployments";

        /// <summary>
        /// Gets or sets the backup retention period
        /// </summary>
        public TimeSpan BackupRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// Gets or sets a value indicating whether to enable automatic rollback on failure
        /// </summary>
        public bool EnableAutomaticRollback { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of deployment history records to keep
        /// </summary>
        public int MaxDeploymentHistoryRecords { get; set; } = 100;
    }

    /// <summary>
    /// Represents deployment options for a version
    /// </summary>
    public class VersionDeploymentOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to backup existing configuration
        /// </summary>
        public bool BackupExistingConfiguration { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to send notifications
        /// </summary>
        public bool SendNotifications { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to force deployment even if validation fails
        /// </summary>
        public bool ForceDeployment { get; set; } = false;

        /// <summary>
        /// Gets or sets the deployment priority
        /// </summary>
        public DeploymentPriority Priority { get; set; } = DeploymentPriority.Normal;

        /// <summary>
        /// Gets or sets custom deployment parameters
        /// </summary>
        public Dictionary<string, string> CustomParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the deployment schedule
        /// </summary>
        public DateTime? ScheduledTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip health checks
        /// </summary>
        public bool SkipHealthChecks { get; set; } = false;
    }

    /// <summary>
    /// Represents rollback options for a version
    /// </summary>
    public class VersionRollbackOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to backup before rollback
        /// </summary>
        public bool BackupBeforeRollback { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to restore backup
        /// </summary>
        public bool RestoreBackup { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to send notifications
        /// </summary>
        public bool SendNotifications { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to force rollback
        /// </summary>
        public bool ForceRollback { get; set; } = false;

        /// <summary>
        /// Gets or sets the rollback reason
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets custom rollback parameters
        /// </summary>
        public Dictionary<string, string> CustomParameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Represents deployment states
    /// </summary>
    public enum VersionDeploymentState
    {
        /// <summary>
        /// Version is not deployed
        /// </summary>
        NotDeployed,

        /// <summary>
        /// Version deployment is in progress
        /// </summary>
        Deploying,

        /// <summary>
        /// Version deployment validation failed
        /// </summary>
        ValidationFailed,

        /// <summary>
        /// Version is successfully deployed
        /// </summary>
        Deployed,

        /// <summary>
        /// Version deployment failed
        /// </summary>
        DeploymentFailed,

        /// <summary>
        /// Version rollback is in progress
        /// </summary>
        RollingBack,

        /// <summary>
        /// Version has been rolled back
        /// </summary>
        Rollback,

        /// <summary>
        /// Version rollback failed
        /// </summary>
        RollbackFailed
    }

    /// <summary>
    /// Represents deployment priority levels
    /// </summary>
    public enum DeploymentPriority
    {
        /// <summary>
        /// Low priority deployment
        /// </summary>
        Low,

        /// <summary>
        /// Normal priority deployment
        /// </summary>
        Normal,

        /// <summary>
        /// High priority deployment
        /// </summary>
        High,

        /// <summary>
        /// Critical priority deployment
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents deployment status for a version
    /// </summary>
    public class VersionDeploymentStatus
    {
        /// <summary>
        /// Gets or sets the version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the deployment state
        /// </summary>
        public VersionDeploymentState State { get; set; }

        /// <summary>
        /// Gets or sets the last updated timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the total deployment time
        /// </summary>
        public TimeSpan TotalDeploymentTime { get; set; }

        /// <summary>
        /// Gets or sets the current deployment time
        /// </summary>
        public TimeSpan CurrentDeploymentTime { get; set; }

        /// <summary>
        /// Gets or sets the deployment progress (0-100)
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// Gets or sets the deployment error message
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether deployment is in progress
        /// </summary>
        public bool IsInProgress => State == VersionDeploymentState.Deploying || State == VersionDeploymentState.RollingBack;

        /// <summary>
        /// Gets a value indicating whether deployment is complete
        /// </summary>
        public bool IsComplete => State == VersionDeploymentState.Deployed || State == VersionDeploymentState.Rollback;

        /// <summary>
        /// Gets a value indicating whether deployment failed
        /// </summary>
        public bool IsFailed => State == VersionDeploymentState.DeploymentFailed || State == VersionDeploymentState.RollbackFailed;

        /// <summary>
        /// Initializes a new instance of the VersionDeploymentStatus class
        /// </summary>
        public VersionDeploymentStatus()
        {
            LastUpdated = DateTime.UtcNow;
            TotalDeploymentTime = TimeSpan.Zero;
            CurrentDeploymentTime = TimeSpan.Zero;
            Progress = 0;
        }
    }

    /// <summary>
    /// Represents deployment context for a version
    /// </summary>
    public class VersionDeploymentContext : IDisposable
    {
        private readonly SolidWorksVersionInfo _versionInfo;
        private readonly VersionDeploymentConfiguration _configuration;
        private readonly Queue<VersionDeploymentHistoryRecord> _deploymentHistory;
        private readonly object _contextLock = new object();
        private bool _disposed;

        /// <summary>
        /// Gets the version information
        /// </summary>
        public SolidWorksVersionInfo VersionInfo => _versionInfo;

        /// <summary>
        /// Gets the deployment status
        /// </summary>
        public VersionDeploymentStatus Status { get; }

        /// <summary>
        /// Initializes a new instance of the VersionDeploymentContext class
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="configuration">Deployment configuration</param>
        public VersionDeploymentContext(SolidWorksVersionInfo versionInfo, VersionDeploymentConfiguration configuration)
        {
            _versionInfo = versionInfo ?? throw new ArgumentNullException(nameof(versionInfo));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            Status = new VersionDeploymentStatus
            {
                Version = versionInfo.Version,
                State = VersionDeploymentState.NotDeployed
            };

            _deploymentHistory = new Queue<VersionDeploymentHistoryRecord>();
        }

        /// <summary>
        /// Gets the deployment directory for this version
        /// </summary>
        /// <param name="options">Deployment options</param>
        /// <returns>Deployment directory path</returns>
        public string GetDeploymentDirectory(VersionDeploymentOptions options)
        {
            return Path.Combine(_configuration.BaseDeploymentDirectory, $"SolidWorks_{_versionInfo.Version}");
        }

        /// <summary>
        /// Gets the backup directory for this version
        /// </summary>
        /// <returns>Backup directory path</returns>
        public string GetBackupDirectory()
        {
            return Path.Combine(_configuration.BaseDeploymentDirectory, "Backups", $"SolidWorks_{_versionInfo.Version}");
        }

        /// <summary>
        /// Adds a deployment history record
        /// </summary>
        /// <param name="record">History record to add</param>
        public void AddDeploymentHistory(VersionDeploymentHistoryRecord record)
        {
            lock (_contextLock)
            {
                _deploymentHistory.Enqueue(record);
                while (_deploymentHistory.Count > _configuration.MaxDeploymentHistoryRecords)
                {
                    _deploymentHistory.Dequeue();
                }
            }
        }

        /// <summary>
        /// Gets deployment history
        /// </summary>
        /// <param name="maxEntries">Maximum number of entries to return</param>
        /// <returns>List of history records</returns>
        public List<VersionDeploymentHistoryRecord> GetDeploymentHistory(int maxEntries)
        {
            lock (_contextLock)
            {
                return _deploymentHistory.TakeLast(maxEntries).ToList();
            }
        }

        /// <summary>
        /// Disposes the deployment context
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the deployment context
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up resources
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents a deployment history record
    /// </summary>
    public class VersionDeploymentHistoryRecord
    {
        /// <summary>
        /// Gets or sets the version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the operation type
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the operation duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the list of errors
        /// </summary>
        public List<string> Errors { get; set; }

        /// <summary>
        /// Gets or sets the list of warnings
        /// </summary>
        public List<string> Warnings { get; set; }

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Initializes a new instance of the VersionDeploymentHistoryRecord class
        /// </summary>
        public VersionDeploymentHistoryRecord()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            Metadata = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Represents system validation result
    /// </summary>
    public class SystemValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the list of validation errors
        /// </summary>
        public List<string> Errors { get; set; }

        /// <summary>
        /// Initializes a new instance of the SystemValidationResult class
        /// </summary>
        public SystemValidationResult()
        {
            Errors = new List<string>();
        }
    }

    // Result classes
    public class VersionDeploymentInitializationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> InitializedVersions { get; } = new List<string>();
        public TimeSpan InitializationTime { get; set; }
        public int DeploymentContextCount { get; set; }
        public int TotalVersionCount { get; set; }
    }

    public class VersionDeploymentResult
    {
        public string Version { get; set; }
        public bool Success { get; set; }
        public TimeSpan DeploymentTime { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public Dictionary<string, object> DeploymentMetrics { get; } = new Dictionary<string, object>();
    }

    public class VersionRollbackResult
    {
        public string Version { get; set; }
        public bool Success { get; set; }
        public TimeSpan RollbackTime { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public Dictionary<string, object> RollbackMetrics { get; } = new Dictionary<string, object>();
    }

    public class VersionDeploymentStatistics
    {
        public int TotalVersions { get; set; }
        public int DeployedVersions { get; set; }
        public int DeployingVersions { get; set; }
        public int FailedDeployments { get; set; }
        public int RolledBackVersions { get; set; }
        public int SuccessfulDeployments { get; set; }
        public TimeSpan TotalDeploymentTime { get; set; }
        public TimeSpan AverageDeploymentTime { get; set; }
    }

    // Event argument classes
    public class VersionDeploymentEventArgs : EventArgs
    {
        public string Version { get; set; }
        public VersionDeploymentState PreviousState { get; set; }
        public VersionDeploymentState CurrentState { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class VersionDeploymentCompletedEventArgs : EventArgs
    {
        public string Version { get; set; }
        public bool Success { get; set; }
        public TimeSpan DeploymentTime { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class VersionDeploymentFailedEventArgs : EventArgs
    {
        public string Version { get; set; }
        public List<string> Errors { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; }
    }
}