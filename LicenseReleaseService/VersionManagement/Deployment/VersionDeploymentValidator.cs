using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.VersionManagement.Deployment
{
    /// <summary>
    /// Provides comprehensive validation for version deployment operations including
    /// pre-deployment checks, deployment success validation, and rollback validation
    /// </summary>
    public class VersionDeploymentValidator
    {
        private readonly ILogger<VersionDeploymentValidator> _logger;
        private readonly VersionDeploymentValidationConfiguration _configuration;
        private readonly List<IDeploymentValidator> _validators;

        /// <summary>
        /// Initializes a new instance of the VersionDeploymentValidator class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configuration">Validation configuration</param>
        public VersionDeploymentValidator(
            ILogger<VersionDeploymentValidator> logger,
            VersionDeploymentValidationConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _validators = new List<IDeploymentValidator>
            {
                new FileSystemValidator(logger, configuration),
                new RegistryValidator(logger, configuration),
                new NetworkValidator(logger, configuration),
                new SecurityValidator(logger, configuration),
                new ConfigurationValidator(logger, configuration),
                new DependencyValidator(logger, configuration),
                new PerformanceValidator(logger, configuration)
            };
        }

        /// <summary>
        /// Validates deployment prerequisites for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="deploymentOptions">Deployment options</param>
        /// <returns>Validation result</returns>
        public async Task<VersionDeploymentValidationResult> ValidateDeploymentAsync(
            SolidWorksVersionInfo versionInfo,
            VersionDeploymentOptions deploymentOptions)
        {
            if (versionInfo == null)
                throw new ArgumentNullException(nameof(versionInfo));
            if (deploymentOptions == null)
                throw new ArgumentNullException(nameof(deploymentOptions));

            _logger.LogInformation("Validating deployment for version {Version}", versionInfo.Version);

            var result = new VersionDeploymentValidationResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Execute all validators
                var validationTasks = _validators.Select(v => v.ValidateDeploymentAsync(versionInfo, deploymentOptions));
                var validationResults = await Task.WhenAll(validationTasks);

                // Aggregate results
                foreach (var validationResult in validationResults)
                {
                    result.Errors.AddRange(validationResult.Errors);
                    result.Warnings.AddRange(validationResult.Warnings);
                    result.Suggestions.AddRange(validationResult.Suggestions);

                    // Track validator-specific results
                    result.ValidatorResults[validationResult.ValidatorType] = validationResult;
                }

                // Execute custom validation rules
                await ExecuteCustomValidationRulesAsync(versionInfo, deploymentOptions, result);

                // Determine overall validity
                result.IsValid = result.Errors.Count == 0;
                result.HasWarnings = result.Warnings.Count > 0;
                result.HasSuggestions = result.Suggestions.Count > 0;

                // Calculate validation score
                result.ValidationScore = CalculateValidationScore(result);

                stopwatch.Stop();
                result.ValidationTime = stopwatch.Elapsed;

                _logger.LogDebug("Deployment validation completed for version {Version} in {Duration}ms. Score: {Score}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                    versionInfo.Version, stopwatch.ElapsedMilliseconds, result.ValidationScore, result.Errors.Count, result.Warnings.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during deployment validation for version {Version}", versionInfo.Version);
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                result.ValidationTime = stopwatch.Elapsed;
                return result;
            }
        }

        /// <summary>
        /// Validates deployment success for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <returns>Deployment success validation result</returns>
        public async Task<DeploymentSuccessValidationResult> ValidateDeploymentSuccessAsync(
            SolidWorksVersionInfo versionInfo)
        {
            if (versionInfo == null)
                throw new ArgumentNullException(nameof(versionInfo));

            _logger.LogInformation("Validating deployment success for version {Version}", versionInfo.Version);

            var result = new DeploymentSuccessValidationResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Validate deployment success criteria
                await ValidateDeploymentFilesAsync(versionInfo, result);
                await ValidateConfigurationFilesAsync(versionInfo, result);
                await ValidateExecutableFunctionalityAsync(versionInfo, result);
                await ValidateLicenseManagerIntegrationAsync(versionInfo, result);
                await ValidateMonitoringIntegrationAsync(versionInfo, result);
                await ValidatePerformanceMetricsAsync(versionInfo, result);

                // Determine overall success
                result.IsDeployedSuccessfully = result.SuccessCriteriaMet >= _configuration.MinimumSuccessCriteria;
                result.DeploymentHealth = CalculateDeploymentHealth(result);

                stopwatch.Stop();
                result.ValidationTime = stopwatch.Elapsed;

                _logger.LogDebug("Deployment success validation completed for version {Version} in {Duration}ms. Success: {Success}, Health: {Health}",
                    versionInfo.Version, stopwatch.ElapsedMilliseconds, result.IsDeployedSuccessfully, result.DeploymentHealth);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during deployment success validation for version {Version}", versionInfo.Version);
                result.IsDeployedSuccessfully = false;
                result.DeploymentHealth = DeploymentHealthLevel.Unhealthy;
                result.Errors.Add($"Success validation error: {ex.Message}");
                result.ValidationTime = stopwatch.Elapsed;
                return result;
            }
        }

        /// <summary>
        /// Validates rollback prerequisites for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="rollbackOptions">Rollback options</param>
        /// <returns>Rollback validation result</returns>
        public async Task<VersionRollbackValidationResult> ValidateRollbackAsync(
            SolidWorksVersionInfo versionInfo,
            VersionRollbackOptions rollbackOptions)
        {
            if (versionInfo == null)
                throw new ArgumentNullException(nameof(versionInfo));
            if (rollbackOptions == null)
                throw new ArgumentNullException(nameof(rollbackOptions));

            _logger.LogInformation("Validating rollback for version {Version}", versionInfo.Version);

            var result = new VersionRollbackValidationResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Validate rollback prerequisites
                await ValidateRollbackReadinessAsync(versionInfo, result);
                await ValidateBackupAvailabilityAsync(versionInfo, result);
                await ValidateSystemStateForRollbackAsync(versionInfo, result);
                await ValidateRollbackSafetyAsync(versionInfo, rollbackOptions, result);

                // Determine overall rollback readiness
                result.CanRollback = result.Errors.Count == 0;
                result.RollbackSafetyLevel = CalculateRollbackSafetyLevel(result);

                stopwatch.Stop();
                result.ValidationTime = stopwatch.Elapsed;

                _logger.LogDebug("Rollback validation completed for version {Version} in {Duration}ms. CanRollback: {CanRollback}, Safety: {Safety}",
                    versionInfo.Version, stopwatch.ElapsedMilliseconds, result.CanRollback, result.RollbackSafetyLevel);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rollback validation for version {Version}", versionInfo.Version);
                result.CanRollback = false;
                result.RollbackSafetyLevel = RollbackSafetyLevel.Unsafe;
                result.Errors.Add($"Rollback validation error: {ex.Message}");
                result.ValidationTime = stopwatch.Elapsed;
                return result;
            }
        }

        /// <summary>
        /// Executes custom validation rules
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="deploymentOptions">Deployment options</param>
        /// <param name="result">Validation result to update</param>
        private async Task ExecuteCustomValidationRulesAsync(
            SolidWorksVersionInfo versionInfo,
            VersionDeploymentOptions deploymentOptions,
            VersionDeploymentValidationResult result)
        {
            try
            {
                // Version-specific validation rules
                await ValidateVersionSpecificRulesAsync(versionInfo, result);

                // Environment-specific validation rules
                await ValidateEnvironmentSpecificRulesAsync(versionInfo, deploymentOptions, result);

                // Business-specific validation rules
                await ValidateBusinessRulesAsync(versionInfo, deploymentOptions, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing custom validation rules for version {Version}", versionInfo.Version);
                result.Warnings.Add($"Custom validation rules error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates version-specific rules
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateVersionSpecificRulesAsync(
            SolidWorksVersionInfo versionInfo,
            VersionDeploymentValidationResult result)
        {
            try
            {
                // Check version compatibility
                if (int.TryParse(versionInfo.Version, out int versionYear))
                {
                    if (versionYear < 2020)
                    {
                        result.Warnings.Add($"Version {versionInfo.Version} is below minimum supported version (2020)");
                    }
                    else if (versionYear > 2025)
                    {
                        result.Warnings.Add($"Version {versionInfo.Version} is above maximum supported version (2025)");
                    }
                }

                // Check service pack requirements
                if (!string.IsNullOrWhiteSpace(versionInfo.ServicePack))
                {
                    // Validate service pack level
                    if (versionInfo.ServicePack.Contains("SP0") && _configuration.RequireMinimumServicePack)
                    {
                        result.Warnings.Add($"Version {versionInfo.Version} has service pack {versionInfo.ServicePack}, which may not meet minimum requirements");
                    }
                }

                // Check architecture compatibility
                if (versionInfo.Is64Bit != Environment.Is64BitOperatingSystem)
                {
                    result.Warnings.Add($"Version {versionInfo.Version} architecture ({(versionInfo.Is64Bit ? "64-bit" : "32-bit")}) may not match system architecture");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating version-specific rules for version {Version}", versionInfo.Version);
                result.Warnings.Add($"Version-specific validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates environment-specific rules
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="deploymentOptions">Deployment options</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateEnvironmentSpecificRulesAsync(
            SolidWorksVersionInfo versionInfo,
            VersionDeploymentOptions deploymentOptions,
            VersionDeploymentValidationResult result)
        {
            try
            {
                // Check available disk space in installation directory
                if (!string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
                {
                    var driveInfo = new DriveInfo(Path.GetPathRoot(versionInfo.InstallationPath));
                    var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

                    if (freeSpaceGB < _configuration.MinimumDiskSpaceGB)
                    {
                        result.Errors.Add($"Insufficient disk space in installation directory: {freeSpaceGB:F1}GB available, {_configuration.MinimumDiskSpaceGB}GB required");
                    }
                }

                // Check available memory
                var memoryInfo = GC.GetGCMemoryInfo();
                var availableMemoryGB = memoryInfo.MemoryLoadBytes / (1024.0 * 1024.0 * 1024.0);

                if (availableMemoryGB < _configuration.MinimumMemoryGB)
                {
                    result.Warnings.Add($"Low available memory: {availableMemoryGB:F1}GB available, {_configuration.MinimumMemoryGB}GB recommended");
                }

                // Check .NET Framework version compatibility
                var dotNetVersion = Environment.Version;
                if (dotNetVersion.Major < 4 || (dotNetVersion.Major == 4 && dotNetVersion.Minor < 8))
                {
                    result.Errors.Add($"Incompatible .NET Framework version: {dotNetVersion}, minimum required: 4.8");
                }

                // Check Windows version compatibility
                var osVersion = Environment.OSVersion;
                if (osVersion.Version.Major < 10)
                {
                    result.Warnings.Add($"Windows version {osVersion.Version} may not be fully supported");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating environment-specific rules for version {Version}", versionInfo.Version);
                result.Warnings.Add($"Environment-specific validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates business rules
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="deploymentOptions">Deployment options</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateBusinessRulesAsync(
            SolidWorksVersionInfo versionInfo,
            VersionDeploymentOptions deploymentOptions,
            VersionDeploymentValidationResult result)
        {
            try
            {
                // Check deployment schedule
                if (deploymentOptions.ScheduledTime.HasValue)
                {
                    var scheduledTime = deploymentOptions.ScheduledTime.Value;
                    var currentTime = DateTime.UtcNow;

                    if (scheduledTime < currentTime)
                    {
                        result.Warnings.Add($"Scheduled deployment time {scheduledTime} is in the past");
                    }
                    else if ((scheduledTime - currentTime).TotalHours > 24)
                    {
                        result.Suggestions.Add($"Consider deploying sooner than {scheduledTime} for faster availability");
                    }
                }

                // Check deployment priority
                if (deploymentOptions.Priority == DeploymentPriority.Critical && !deploymentOptions.ForceDeployment)
                {
                    result.Suggestions.Add($"Critical priority deployment should be carefully reviewed before proceeding");
                }

                // Check custom parameters
                if (deploymentOptions.CustomParameters.ContainsKey("maintenance_window"))
                {
                    var maintenanceWindow = deploymentOptions.CustomParameters["maintenance_window"];
                    if (maintenanceWindow.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Suggestions.Add($"Deployment scheduled during maintenance window - proceed with caution");
                    }
                }

                // Check deployment frequency limits
                if (_configuration.EnableDeploymentFrequencyLimits)
                {
                    await ValidateDeploymentFrequencyAsync(versionInfo, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating business rules for version {Version}", versionInfo.Version);
                result.Warnings.Add($"Business rules validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates deployment frequency
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateDeploymentFrequencyAsync(
            SolidWorksVersionInfo versionInfo,
            VersionDeploymentValidationResult result)
        {
            try
            {
                // This would typically check deployment history for frequency violations
                // For now, just add a placeholder suggestion
                result.Suggestions.Add($"Consider deployment frequency limits for version {versionInfo.Version}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating deployment frequency for version {Version}", versionInfo.Version);
                result.Warnings.Add($"Deployment frequency validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates deployment files
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateDeploymentFilesAsync(
            SolidWorksVersionInfo versionInfo,
            DeploymentSuccessValidationResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
                {
                    result.Errors.Add("Installation path is not specified");
                    return;
                }

                // Check if installation directory exists
                if (!Directory.Exists(versionInfo.InstallationPath))
                {
                    result.Errors.Add($"Installation directory does not exist: {versionInfo.InstallationPath}");
                    return;
                }

                // Check for required files
                var requiredFiles = new[]
                {
                    versionInfo.ExecutablePath,
                    versionInfo.LmutilPath
                };

                foreach (var requiredFile in requiredFiles)
                {
                    if (!string.IsNullOrWhiteSpace(requiredFile) && !File.Exists(requiredFile))
                    {
                        result.Errors.Add($"Required file does not exist: {requiredFile}");
                    }
                    else if (!string.IsNullOrWhiteSpace(requiredFile) && File.Exists(requiredFile))
                    {
                        result.SuccessCriteriaMet++;
                    }
                }

                // Check for required directories
                var requiredDirectories = new[]
                {
                    Path.Combine(versionInfo.InstallationPath, "lang"),
                    Path.Combine(versionInfo.InstallationPath, "data")
                };

                foreach (var requiredDir in requiredDirectories)
                {
                    if (Directory.Exists(requiredDir))
                    {
                        result.SuccessCriteriaMet++;
                    }
                    else
                    {
                        result.Warnings.Add($"Required directory does not exist: {requiredDir}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating deployment files for version {Version}", versionInfo.Version);
                result.Errors.Add($"Deployment files validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates configuration files
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateConfigurationFilesAsync(
            SolidWorksVersionInfo versionInfo,
            DeploymentSuccessValidationResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
                {
                    result.Warnings.Add("Installation path is not specified for configuration validation");
                    return;
                }

                // Check for configuration files
                var configFiles = Directory.GetFiles(versionInfo.InstallationPath, "*.config", SearchOption.AllDirectories);
                if (configFiles.Length == 0)
                {
                    result.Warnings.Add("No configuration files found in installation directory");
                }
                else
                {
                    result.SuccessCriteriaMet++;
                }

                // Check configuration file validity
                foreach (var configFile in configFiles)
                {
                    try
                    {
                        var configContent = await File.ReadAllTextAsync(configFile);
                        if (string.IsNullOrWhiteSpace(configContent))
                        {
                            result.Warnings.Add($"Configuration file is empty: {configFile}");
                        }
                        else
                        {
                            result.SuccessCriteriaMet++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cannot read configuration file {configFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration files for version {Version}", versionInfo.Version);
                result.Errors.Add($"Configuration files validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates executable functionality
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateExecutableFunctionalityAsync(
            SolidWorksVersionInfo versionInfo,
            DeploymentSuccessValidationResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(versionInfo.ExecutablePath) || !File.Exists(versionInfo.ExecutablePath))
                {
                    result.Errors.Add("Executable file is not available for functionality validation");
                    return;
                }

                var fileInfo = new FileInfo(versionInfo.ExecutablePath);

                // Check file size
                if (fileInfo.Length < _configuration.MinimumExecutableSizeBytes)
                {
                    result.Warnings.Add($"Executable file is unusually small: {fileInfo.Length} bytes");
                }
                else
                {
                    result.SuccessCriteriaMet++;
                }

                // Check file integrity (basic check)
                try
                {
                    using (var stream = fileInfo.OpenRead())
                    {
                        var buffer = new byte[1024];
                        stream.Read(buffer, 0, buffer.Length);
                    }
                    result.SuccessCriteriaMet++;
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Executable file integrity check failed: {ex.Message}");
                }

                // Check file permissions
                try
                {
                    using (var stream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Close();
                    }
                    result.SuccessCriteriaMet++;
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Executable file permissions check failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating executable functionality for version {Version}", versionInfo.Version);
                result.Errors.Add($"Executable functionality validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates license manager integration
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateLicenseManagerIntegrationAsync(
            SolidWorksVersionInfo versionInfo,
            DeploymentSuccessValidationResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(versionInfo.LmutilPath) || !File.Exists(versionInfo.LmutilPath))
                {
                    result.Warnings.Add("License manager utility is not available for integration validation");
                    return;
                }

                // Test license manager basic functionality
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = versionInfo.LmutilPath,
                        Arguments = "-help",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await Task.Run(() => process.WaitForExit(_configuration.LicenseManagerTimeoutMs));

                if (process.ExitCode == 0)
                {
                    result.SuccessCriteriaMet++;
                }
                else
                {
                    result.Warnings.Add($"License manager utility returned error code: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating license manager integration for version {Version}", versionInfo.Version);
                result.Warnings.Add($"License manager integration validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates monitoring integration
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateMonitoringIntegrationAsync(
            SolidWorksVersionInfo versionInfo,
            DeploymentSuccessValidationResult result)
        {
            try
            {
                // Check if monitoring components are deployed
                var monitoringDir = Path.Combine(versionInfo.InstallationPath, "monitoring");
                if (Directory.Exists(monitoringDir))
                {
                    var monitoringFiles = Directory.GetFiles(monitoringDir, "*.*", SearchOption.AllDirectories);
                    if (monitoringFiles.Length > 0)
                    {
                        result.SuccessCriteriaMet++;
                    }
                    else
                    {
                        result.Warnings.Add("Monitoring directory exists but contains no files");
                    }
                }
                else
                {
                    result.Warnings.Add("Monitoring directory does not exist");
                }

                // Check health monitoring configuration
                var healthConfigFile = Path.Combine(versionInfo.InstallationPath, "health.config");
                if (File.Exists(healthConfigFile))
                {
                    result.SuccessCriteriaMet++;
                }
                else
                {
                    result.Warnings.Add("Health monitoring configuration file not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating monitoring integration for version {Version}", versionInfo.Version);
                result.Warnings.Add($"Monitoring integration validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates performance metrics
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidatePerformanceMetricsAsync(
            SolidWorksVersionInfo versionInfo,
            DeploymentSuccessValidationResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
                {
                    result.Warnings.Add("Installation path is not specified for performance validation");
                    return;
                }

                // Check file system performance
                var perfTestFile = Path.Combine(versionInfo.InstallationPath, $"perf_test_{DateTime.UtcNow:yyyyMMddHHmmss}.tmp");
                var perfStopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var testData = new byte[1024 * 1024]; // 1MB test data
                    File.WriteAllBytes(perfTestFile, testData);
                    File.Delete(perfTestFile);

                    perfStopwatch.Stop();
                    result.PerformanceMetrics["FileSystemWriteSpeedMs"] = perfStopwatch.ElapsedMilliseconds;

                    if (perfStopwatch.ElapsedMilliseconds < _configuration.MaximumFileOperationMs)
                    {
                        result.SuccessCriteriaMet++;
                    }
                    else
                    {
                        result.Warnings.Add($"Slow file system performance: {perfStopwatch.ElapsedMilliseconds}ms");
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"File system performance test failed: {ex.Message}");
                }

                // Check available disk space
                var driveInfo = new DriveInfo(Path.GetPathRoot(versionInfo.InstallationPath));
                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                result.PerformanceMetrics["AvailableDiskSpaceGB"] = freeSpaceGB;

                if (freeSpaceGB >= _configuration.MinimumDiskSpaceGB)
                {
                    result.SuccessCriteriaMet++;
                }
                else
                {
                    result.Warnings.Add($"Low disk space: {freeSpaceGB:F1}GB available");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating performance metrics for version {Version}", versionInfo.Version);
                result.Warnings.Add($"Performance metrics validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates rollback readiness
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateRollbackReadinessAsync(
            SolidWorksVersionInfo versionInfo,
            VersionRollbackValidationResult result)
        {
            try
            {
                // Check if version is in a state that can be rolled back
                // This would typically involve checking deployment status
                result.Warnings.Add("Rollback readiness check requires deployment status information");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating rollback readiness for version {Version}", versionInfo.Version);
                result.Errors.Add($"Rollback readiness validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates backup availability
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateBackupAvailabilityAsync(
            SolidWorksVersionInfo versionInfo,
            VersionRollbackValidationResult result)
        {
            try
            {
                // Check if backup files are available
                // This would typically involve checking backup directories
                result.Warnings.Add("Backup availability check requires backup directory information");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating backup availability for version {Version}", versionInfo.Version);
                result.Errors.Add($"Backup availability validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates system state for rollback
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateSystemStateForRollbackAsync(
            SolidWorksVersionInfo versionInfo,
            VersionRollbackValidationResult result)
        {
            try
            {
                // Check system state to ensure rollback can proceed safely
                // This would involve checking system resources, locks, etc.
                result.Warnings.Add("System state validation requires additional system monitoring data");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating system state for rollback for version {Version}", versionInfo.Version);
                result.Errors.Add($"System state validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates rollback safety
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="rollbackOptions">Rollback options</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateRollbackSafetyAsync(
            SolidWorksVersionInfo versionInfo,
            VersionRollbackOptions rollbackOptions,
            VersionRollbackValidationResult result)
        {
            try
            {
                // Check if rollback is safe to perform
                if (rollbackOptions.ForceRollback)
                {
                    result.Warnings.Add("Force rollback option is enabled - proceed with caution");
                }

                // Check for potential data loss
                if (rollbackOptions.RestoreBackup)
                {
                    result.Warnings.Add("Backup restoration may cause data loss - ensure backup is current");
                }

                // Check for active users or sessions
                // This would typically involve checking user sessions
                result.Warnings.Add("Active user check requires session monitoring data");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating rollback safety for version {Version}", versionInfo.Version);
                result.Errors.Add($"Rollback safety validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates validation score based on validation results
        /// </summary>
        /// <param name="result">Validation result</param>
        /// <returns>Validation score (0-100)</returns>
        private double CalculateValidationScore(VersionDeploymentValidationResult result)
        {
            if (result.Errors.Count > 0)
                return 0.0;

            var totalValidators = _validators.Count;
            var successfulValidators = totalValidators - result.Warnings.Count;

            return (successfulValidators * 100.0) / totalValidators;
        }

        /// <summary>
        /// Calculates deployment health level
        /// </summary>
        /// <param name="result">Validation result</param>
        /// <returns>Deployment health level</returns>
        private DeploymentHealthLevel CalculateDeploymentHealth(DeploymentSuccessValidationResult result)
        {
            if (result.Errors.Count > 0)
                return DeploymentHealthLevel.Unhealthy;

            if (result.Warnings.Count > 3)
                return DeploymentHealthLevel.Degraded;

            if (result.Warnings.Count > 0)
                return DeploymentHealthLevel.Good;

            return DeploymentHealthLevel.Excellent;
        }

        /// <summary>
        /// Calculates rollback safety level
        /// </summary>
        /// <param name="result">Validation result</param>
        /// <returns>Rollback safety level</returns>
        private RollbackSafetyLevel CalculateRollbackSafetyLevel(VersionRollbackValidationResult result)
        {
            if (result.Errors.Count > 0)
                return RollbackSafetyLevel.Unsafe;

            if (result.Warnings.Count > 2)
                return RollbackSafetyLevel.ModerateRisk;

            if (result.Warnings.Count > 0)
                return RollbackSafetyLevel.Safe;

            return RollbackSafetyLevel.VerySafe;
        }
    }

    /// <summary>
    /// Interface for deployment validators
    /// </summary>
    public interface IDeploymentValidator
    {
        /// <summary>
        /// Gets the validator type
        /// </summary>
        string ValidatorType { get; }

        /// <summary>
        /// Validates deployment for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="deploymentOptions">Deployment options</param>
        /// <returns>Validation result</returns>
        Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions);
    }

    /// <summary>
    /// Base class for deployment validators
    /// </summary>
    public abstract class DeploymentValidatorBase : IDeploymentValidator
    {
        protected readonly ILogger Logger;
        protected readonly VersionDeploymentValidationConfiguration Configuration;

        /// <summary>
        /// Gets the validator type
        /// </summary>
        public abstract string ValidatorType { get; }

        /// <summary>
        /// Initializes a new instance of the DeploymentValidatorBase class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configuration">Validation configuration</param>
        protected DeploymentValidatorBase(ILogger logger, VersionDeploymentValidationConfiguration configuration)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Validates deployment for a version
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="deploymentOptions">Deployment options</param>
        /// <returns>Validation result</returns>
        public abstract Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions);
    }

    /// <summary>
    /// Validates file system aspects of deployment
    /// </summary>
    public class FileSystemValidator : DeploymentValidatorBase
    {
        public override string ValidatorType => "FileSystem";

        public FileSystemValidator(ILogger logger, VersionDeploymentValidationConfiguration configuration)
            : base(logger, configuration) { }

        public override async Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions)
        {
            var result = new ValidatorResult { ValidatorType = ValidatorType };

            try
            {
                // Validate installation path
                if (string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
                {
                    result.Errors.Add("Installation path is not specified");
                    return result;
                }

                // Check if installation directory exists
                if (!Directory.Exists(versionInfo.InstallationPath))
                {
                    result.Errors.Add($"Installation directory does not exist: {versionInfo.InstallationPath}");
                    return result;
                }

                // Check directory permissions
                var testFile = Path.Combine(versionInfo.InstallationPath, $"validation_test_{DateTime.UtcNow:yyyyMMddHHmmss}.tmp");
                try
                {
                    File.WriteAllText(testFile, "validation test");
                    File.Delete(testFile);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Cannot write to installation directory: {ex.Message}");
                }

                // Check disk space
                var driveInfo = new DriveInfo(Path.GetPathRoot(versionInfo.InstallationPath));
                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

                if (freeSpaceGB < Configuration.MinimumDiskSpaceGB)
                {
                    result.Errors.Add($"Insufficient disk space: {freeSpaceGB:F1}GB available, {Configuration.MinimumDiskSpaceGB}GB required");
                }

                // Check file paths
                if (!string.IsNullOrWhiteSpace(versionInfo.ExecutablePath) && !File.Exists(versionInfo.ExecutablePath))
                {
                    result.Warnings.Add($"Executable file does not exist: {versionInfo.ExecutablePath}");
                }

                if (!string.IsNullOrWhiteSpace(versionInfo.LmutilPath) && !File.Exists(versionInfo.LmutilPath))
                {
                    result.Warnings.Add($"License manager utility does not exist: {versionInfo.LmutilPath}");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"File system validation error: {ex.Message}");
            }

            return result;
        }
    }

    /// <summary>
    /// Validates registry aspects of deployment
    /// </summary>
    public class RegistryValidator : DeploymentValidatorBase
    {
        public override string ValidatorType => "Registry";

        public RegistryValidator(ILogger logger, VersionDeploymentValidationConfiguration configuration)
            : base(logger, configuration) { }

        public override async Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions)
        {
            var result = new ValidatorResult { ValidatorType = ValidatorType };

            try
            {
                if (string.IsNullOrWhiteSpace(versionInfo.RegistryKeyPath))
                {
                    result.Warnings.Add("Registry key path is not specified");
                    return result;
                }

                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(versionInfo.RegistryKeyPath))
                {
                    if (key == null)
                    {
                        result.Warnings.Add($"Registry key not accessible: {versionInfo.RegistryKeyPath}");
                        return result;
                    }

                    // Check for required registry values
                    var requiredValues = new[] { "InstallPath", "ProductName" };
                    foreach (var valueName in requiredValues)
                    {
                        var value = key.GetValue(valueName);
                        if (value == null)
                        {
                            result.Warnings.Add($"Required registry value missing: {valueName}");
                        }
                    }

                    // Check registry permissions
                    try
                    {
                        var testValueName = $"validation_test_{DateTime.UtcNow:yyyyMMddHHmmss}";
                        key.SetValue(testValueName, "test");
                        key.DeleteValue(testValueName);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cannot write to registry key: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Registry validation error: {ex.Message}");
            }

            return result;
        }
    }

    /// <summary>
    /// Validates network aspects of deployment
    /// </summary>
    public class NetworkValidator : DeploymentValidatorBase
    {
        public override string ValidatorType => "Network";

        public NetworkValidator(ILogger logger, VersionDeploymentValidationConfiguration configuration)
            : base(logger, configuration) { }

        public override async Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions)
        {
            var result = new ValidatorResult { ValidatorType = ValidatorType };

            try
            {
                if (Configuration.RequireNetworkConnectivity)
                {
                    // Test basic network connectivity
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);
                        var response = await client.GetAsync("http://www.microsoft.com");
                        if (!response.IsSuccessStatusCode)
                        {
                            result.Warnings.Add("Network connectivity check failed");
                        }
                    }
                }

                // Check if license server is accessible (if specified)
                if (deploymentOptions.CustomParameters.ContainsKey("license_server"))
                {
                    var licenseServer = deploymentOptions.CustomParameters["license_server"];
                    try
                    {
                        using (var client = new System.Net.Http.HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(5);
                            var response = await client.GetAsync($"http://{licenseServer}");
                            if (!response.IsSuccessStatusCode)
                            {
                                result.Warnings.Add($"License server not accessible: {licenseServer}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"License server connectivity test failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Network validation error: {ex.Message}");
            }

            return result;
        }
    }

    /// <summary>
    /// Validates security aspects of deployment
    /// </summary>
    public class SecurityValidator : DeploymentValidatorBase
    {
        public override string ValidatorType => "Security";

        public SecurityValidator(ILogger logger, VersionDeploymentValidationConfiguration configuration)
            : base(logger, configuration) { }

        public override async Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions)
        {
            var result = new ValidatorResult { ValidatorType = ValidatorType };

            try
            {
                // Check file permissions
                if (!string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
                {
                    try
                    {
                        var files = Directory.GetFiles(versionInfo.InstallationPath, "*.exe", SearchOption.TopDirectoryOnly);
                        foreach (var file in files)
                        {
                            var fileInfo = new FileInfo(file);
                            if ((fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            {
                                result.Warnings.Add($"Executable file is read-only: {file}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cannot check file permissions: {ex.Message}");
                    }
                }

                // Check for potentially unsafe file locations
                if (versionInfo.InstallationPath?.Contains("Temp", StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.Warnings.Add("Installation path contains temporary directory");
                }

                // Check user permissions
                if (!Environment.IsPrivilegedProcess)
                {
                    result.Warnings.Add("Process is not running with elevated privileges");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Security validation error: {ex.Message}");
            }

            return result;
        }
    }

    /// <summary>
    /// Validates configuration aspects of deployment
    /// </summary>
    public class ConfigurationValidator : DeploymentValidatorBase
    {
        public override string ValidatorType => "Configuration";

        public ConfigurationValidator(ILogger logger, VersionDeploymentValidationConfiguration configuration)
            : base(logger, configuration) { }

        public override async Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions)
        {
            var result = new ValidatorResult { ValidatorType = ValidatorType };

            try
            {
                // Validate deployment options
                if (deploymentOptions.ForceDeployment)
                {
                    result.Warnings.Add("Force deployment option is enabled");
                }

                if (deploymentOptions.ScheduledTime.HasValue && deploymentOptions.ScheduledTime.Value < DateTime.UtcNow)
                {
                    result.Warnings.Add("Scheduled deployment time is in the past");
                }

                // Validate custom parameters
                foreach (var parameter in deploymentOptions.CustomParameters)
                {
                    if (string.IsNullOrWhiteSpace(parameter.Value))
                    {
                        result.Warnings.Add($"Custom parameter '{parameter.Key}' has empty value");
                    }
                }

                // Check for required custom parameters
                var requiredParameters = new[] { "deployment_environment", "deployment_team" };
                foreach (var requiredParam in requiredParameters)
                {
                    if (!deploymentOptions.CustomParameters.ContainsKey(requiredParam))
                    {
                        result.Suggestions.Add($"Consider adding required parameter: {requiredParam}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Configuration validation error: {ex.Message}");
            }

            return result;
        }
    }

    /// <summary>
    /// Validates dependency aspects of deployment
    /// </summary>
    public class DependencyValidator : DeploymentValidatorBase
    {
        public override string ValidatorType => "Dependency";

        public DependencyValidator(ILogger logger, VersionDeploymentValidationConfiguration configuration)
            : base(logger, configuration) { }

        public override async Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions)
        {
            var result = new ValidatorResult { ValidatorType = ValidatorType };

            try
            {
                // Check .NET Framework version
                var dotNetVersion = Environment.Version;
                if (dotNetVersion.Major < 4 || (dotNetVersion.Major == 4 && dotNetVersion.Minor < 8))
                {
                    result.Warnings.Add($".NET Framework version {dotNetVersion} may not be compatible");
                }

                // Check Windows version
                var osVersion = Environment.OSVersion;
                if (osVersion.Version.Major < 10)
                {
                    result.Warnings.Add($"Windows version {osVersion.Version} may not be fully supported");
                }

                // Check available memory
                var memoryInfo = GC.GetGCMemoryInfo();
                var availableMemoryGB = memoryInfo.MemoryLoadBytes / (1024.0 * 1024.0 * 1024.0);

                if (availableMemoryGB < Configuration.MinimumMemoryGB)
                {
                    result.Warnings.Add($"Low available memory: {availableMemoryGB:F1}GB available, {Configuration.MinimumMemoryGB}GB recommended");
                }

                // Check processor architecture
                if (versionInfo.Is64Bit && !Environment.Is64BitOperatingSystem)
                {
                    result.Warnings.Add("64-bit version deployment on 32-bit system may not work");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Dependency validation error: {ex.Message}");
            }

            return result;
        }
    }

    /// <summary>
    /// Validates performance aspects of deployment
    /// </summary>
    public class PerformanceValidator : DeploymentValidatorBase
    {
        public override string ValidatorType => "Performance";

        public PerformanceValidator(ILogger logger, VersionDeploymentValidationConfiguration configuration)
            : base(logger, configuration) { }

        public override async Task<ValidatorResult> ValidateDeploymentAsync(SolidWorksVersionInfo versionInfo, VersionDeploymentOptions deploymentOptions)
        {
            var result = new ValidatorResult { ValidatorType = ValidatorType };

            try
            {
                if (!string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
                {
                    // Check disk space
                    var driveInfo = new DriveInfo(Path.GetPathRoot(versionInfo.InstallationPath));
                    var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

                    if (freeSpaceGB < Configuration.MinimumDiskSpaceGB * 2) // Require double minimum for deployment
                    {
                        result.Warnings.Add($"Limited disk space for deployment: {freeSpaceGB:F1}GB available");
                    }

                    // Check disk performance
                    var perfTestFile = Path.Combine(versionInfo.InstallationPath, $"perf_test_{DateTime.UtcNow:yyyyMMddHHmmss}.tmp");
                    var perfStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        var testData = new byte[1024 * 1024]; // 1MB test data
                        File.WriteAllBytes(perfTestFile, testData);
                        File.Delete(perfTestFile);

                        perfStopwatch.Stop();
                        if (perfStopwatch.ElapsedMilliseconds > Configuration.MaximumFileOperationMs)
                        {
                            result.Warnings.Add($"Slow disk performance: {perfStopwatch.ElapsedMilliseconds}ms for 1MB write");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Performance test failed: {ex.Message}");
                    }
                }

                // Check CPU usage
                var cpuCounter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
                var cpuUsage = cpuCounter.NextValue();
                await Task.Delay(1000); // Wait for accurate reading
                cpuUsage = cpuCounter.NextValue();

                if (cpuUsage > 80)
                {
                    result.Warnings.Add($"High CPU usage: {cpuUsage:F1}%");
                }

                // Check memory usage
                var memoryCounter = new System.Diagnostics.PerformanceCounter("Memory", "% Committed Bytes In Use");
                var memoryUsage = memoryCounter.NextValue();

                if (memoryUsage > 80)
                {
                    result.Warnings.Add($"High memory usage: {memoryUsage:F1}%");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Performance validation error: {ex.Message}");
            }

            return result;
        }
    }

    /// <summary>
    /// Configuration for deployment validation
    /// </summary>
    public class VersionDeploymentValidationConfiguration
    {
        /// <summary>
        /// Gets or sets the minimum required disk space in GB
        /// </summary>
        public double MinimumDiskSpaceGB { get; set; } = 5.0;

        /// <summary>
        /// Gets or sets the minimum required memory in GB
        /// </summary>
        public double MinimumMemoryGB { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the minimum executable size in bytes
        /// </summary>
        public long MinimumExecutableSizeBytes { get; set; } = 1024 * 1024;

        /// <summary>
        /// Gets or sets the maximum allowed file operation time in milliseconds
        /// </summary>
        public int MaximumFileOperationMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the license manager timeout in milliseconds
        /// </summary>
        public int LicenseManagerTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the minimum success criteria count
        /// </summary>
        public int MinimumSuccessCriteria { get; set; } = 5;

        /// <summary>
        /// Gets or sets a value indicating whether network connectivity is required
        /// </summary>
        public bool RequireNetworkConnectivity { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to require minimum service pack
        /// </summary>
        public bool RequireMinimumServicePack { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable deployment frequency limits
        /// </summary>
        public bool EnableDeploymentFrequencyLimits { get; set; } = true;

        /// <summary>
        /// Gets or sets the validation timeout
        /// </summary>
        public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Represents the result of a single validator
    /// </summary>
    public class ValidatorResult
    {
        /// <summary>
        /// Gets or sets the validator type
        /// </summary>
        public string ValidatorType { get; set; }

        /// <summary>
        /// Gets or sets the list of errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets or sets the list of warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets or sets the list of suggestions
        /// </summary>
        public List<string> Suggestions { get; }

        /// <summary>
        /// Initializes a new instance of the ValidatorResult class
        /// </summary>
        public ValidatorResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            Suggestions = new List<string>();
        }
    }

    /// <summary>
    /// Represents the result of deployment validation
    /// </summary>
    public class VersionDeploymentValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there are warnings
        /// </summary>
        public bool HasWarnings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there are suggestions
        /// </summary>
        public bool HasSuggestions { get; set; }

        /// <summary>
        /// Gets or sets the validation score (0-100)
        /// </summary>
        public double ValidationScore { get; set; }

        /// <summary>
        /// Gets or sets the validation time
        /// </summary>
        public TimeSpan ValidationTime { get; set; }

        /// <summary>
        /// Gets the list of errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the list of warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets the list of suggestions
        /// </summary>
        public List<string> Suggestions { get; }

        /// <summary>
        /// Gets the validator-specific results
        /// </summary>
        public Dictionary<string, ValidatorResult> ValidatorResults { get; }

        /// <summary>
        /// Initializes a new instance of the VersionDeploymentValidationResult class
        /// </summary>
        public VersionDeploymentValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            Suggestions = new List<string>();
            ValidatorResults = new Dictionary<string, ValidatorResult>();
        }
    }

    /// <summary>
    /// Represents the result of deployment success validation
    /// </summary>
    public class DeploymentSuccessValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether deployment was successful
        /// </summary>
        public bool IsDeployedSuccessfully { get; set; }

        /// <summary>
        /// Gets or sets the deployment health level
        /// </summary>
        public DeploymentHealthLevel DeploymentHealth { get; set; }

        /// <summary>
        /// Gets or sets the number of success criteria met
        /// </summary>
        public int SuccessCriteriaMet { get; set; }

        /// <summary>
        /// Gets or sets the validation time
        /// </summary>
        public TimeSpan ValidationTime { get; set; }

        /// <summary>
        /// Gets or sets performance metrics
        /// </summary>
        public Dictionary<string, object> PerformanceMetrics { get; }

        /// <summary>
        /// Gets the list of errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the list of warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Initializes a new instance of the DeploymentSuccessValidationResult class
        /// </summary>
        public DeploymentSuccessValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            PerformanceMetrics = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Represents the result of rollback validation
    /// </summary>
    public class VersionRollbackValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether rollback can proceed
        /// </summary>
        public bool CanRollback { get; set; }

        /// <summary>
        /// Gets or sets the rollback safety level
        /// </summary>
        public RollbackSafetyLevel RollbackSafetyLevel { get; set; }

        /// <summary>
        /// Gets or sets the validation time
        /// </summary>
        public TimeSpan ValidationTime { get; set; }

        /// <summary>
        /// Gets the list of errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the list of warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Initializes a new instance of the VersionRollbackValidationResult class
        /// </summary>
        public VersionRollbackValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }

    /// <summary>
    /// Represents deployment health levels
    /// </summary>
    public enum DeploymentHealthLevel
    {
        /// <summary>
        /// Deployment is unhealthy
        /// </summary>
        Unhealthy,

        /// <summary>
        /// Deployment is degraded
        /// </summary>
        Degraded,

        /// <summary>
        /// Deployment is good
        /// </summary>
        Good,

        /// <summary>
        /// Deployment is excellent
        /// </summary>
        Excellent
    }

    /// <summary>
    /// Represents rollback safety levels
    /// </summary>
    public enum RollbackSafetyLevel
    {
        /// <summary>
        /// Rollback is unsafe
        /// </summary>
        Unsafe,

        /// <summary>
        /// Rollback has moderate risk
        /// </summary>
        ModerateRisk,

        /// <summary>
        /// Rollback is safe
        /// </summary>
        Safe,

        /// <summary>
        /// Rollback is very safe
        /// </summary>
        VerySafe
    }

    /// <summary>
    /// Extension methods for checking process privileges
    /// </summary>
    public static class ProcessExtensions
    {
        /// <summary>
        /// Gets a value indicating whether the current process is running with elevated privileges
        /// </summary>
        /// <param name="process">Process to check</param>
        /// <returns>True if process is privileged, false otherwise</returns>
        public static bool IsPrivilegedProcess(this System.Diagnostics.Process process)
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}