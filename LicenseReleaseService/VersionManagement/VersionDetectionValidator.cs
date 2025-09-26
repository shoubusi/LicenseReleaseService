using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Provides comprehensive validation for SolidWorks version detection results
    /// </summary>
    public class VersionDetectionValidator
    {
        private readonly ILogger<VersionDetectionValidator> _logger;
        private readonly VersionValidationConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the VersionDetectionValidator class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configuration">Validation configuration</param>
        public VersionDetectionValidator(ILogger<VersionDetectionValidator> logger, VersionValidationConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Validates a version detection result
        /// </summary>
        /// <param name="detectionResult">Detection result to validate</param>
        /// <returns>Validation result</returns>
        public async Task<VersionValidationResult> ValidateDetectionResultAsync(VersionDetectionResult detectionResult)
        {
            if (detectionResult == null)
                throw new ArgumentNullException(nameof(detectionResult));

            var result = new VersionValidationResult();

            try
            {
                _logger.LogInformation("Starting validation of version detection result");

                // Validate basic detection success
                ValidateDetectionSuccess(detectionResult, result);

                // Validate version completeness
                await ValidateVersionCompletenessAsync(detectionResult, result);

                // Validate version health
                await ValidateVersionHealthAsync(detectionResult, result);

                // Validate version support
                ValidateVersionSupport(detectionResult, result);

                // Validate performance metrics
                ValidatePerformanceMetrics(detectionResult, result);

                // Validate consistency across detection methods
                await ValidateDetectionConsistencyAsync(detectionResult, result);

                // Generate recommendations
                GenerateRecommendations(detectionResult, result);

                _logger.LogInformation("Version detection validation completed. Issues found: {ErrorCount} errors, {WarningCount} warnings",
                    result.Errors.Count, result.Warnings.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during version detection validation");
                result.Errors.Add($"Validation failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Validates a specific SolidWorks version
        /// </summary>
        /// <param name="versionInfo">Version information to validate</param>
        /// <returns>Validation result for the specific version</returns>
        public async Task<SingleVersionValidationResult> ValidateVersionAsync(SolidWorksVersionInfo versionInfo)
        {
            if (versionInfo == null)
                throw new ArgumentNullException(nameof(versionInfo));

            var result = new SingleVersionValidationResult { Version = versionInfo.Version };

            try
            {
                _logger.LogDebug("Validating SolidWorks version {Version}", versionInfo.Version);

                // Validate basic version information
                ValidateBasicVersionInfo(versionInfo, result);

                // Validate installation paths
                await ValidateInstallationPathsAsync(versionInfo, result);

                // Validate executable accessibility
                await ValidateExecutableAccessibilityAsync(versionInfo, result);

                // Validate license manager accessibility
                await ValidateLicenseManagerAccessibilityAsync(versionInfo, result);

                // Validate version compatibility
                ValidateVersionCompatibility(versionInfo, result);

                // Validate registry consistency
                await ValidateRegistryConsistencyAsync(versionInfo, result);

                // Calculate overall health score
                result.HealthScore = CalculateHealthScore(result);

                _logger.LogDebug("Version {Version} validation completed. Health score: {HealthScore}",
                    versionInfo.Version, result.HealthScore);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating version {Version}", versionInfo.Version);
                result.Errors.Add($"Validation failed: {ex.Message}");
                result.HealthScore = 0;
                return result;
            }
        }

        /// <summary>
        /// Validates multiple versions at once
        /// </summary>
        /// <param name="versions">List of versions to validate</param>
        /// <returns>Aggregated validation result</returns>
        public async Task<MultiVersionValidationResult> ValidateVersionsAsync(IEnumerable<SolidWorksVersionInfo> versions)
        {
            if (versions == null)
                throw new ArgumentNullException(nameof(versions));

            var versionList = versions.ToList();
            var result = new MultiVersionValidationResult();

            try
            {
                _logger.LogInformation("Starting validation of {Count} SolidWorks versions", versionList.Count);

                // Validate each version
                var validationTasks = versionList.Select(async version =>
                {
                    var singleResult = await ValidateVersionAsync(version);
                    return new { Version = version, Result = singleResult };
                });

                var validationResults = await Task.WhenAll(validationTasks);

                // Aggregate results
                foreach (var validation in validationResults)
                {
                    result.VersionResults.Add(validation.Result);

                    if (validation.Result.Errors.Count > 0)
                    {
                        result.Errors.AddRange(validation.Result.Errors.Select(e => $"{validation.Version.Version}: {e}"));
                    }

                    if (validation.Result.Warnings.Count > 0)
                    {
                        result.Warnings.AddRange(validation.Result.Warnings.Select(w => $"{validation.Version.Version}: {w}"));
                    }

                    result.HealthScores[validation.Version.Version] = validation.Result.HealthScore;
                }

                // Validate overall multi-version consistency
                ValidateMultiVersionConsistency(versionList, result);

                // Calculate overall statistics
                CalculateOverallStatistics(result);

                _logger.LogInformation("Multi-version validation completed. Overall health: {OverallHealthScore}",
                    result.OverallHealthScore);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during multi-version validation");
                result.Errors.Add($"Multi-version validation failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Validates that detection was successful
        /// </summary>
        /// <param name="detectionResult">Detection result</param>
        /// <param name="validationResult">Validation result</param>
        private void ValidateDetectionSuccess(VersionDetectionResult detectionResult, VersionValidationResult validationResult)
        {
            if (!detectionResult.Success)
            {
                validationResult.Errors.Add($"Detection failed: {detectionResult.Error}");
            }

            if (detectionResult.TotalVersions == 0)
            {
                validationResult.Errors.Add("No SolidWorks versions detected. This may indicate a detection problem.");
            }

            if (detectionResult.DetectionTime > _configuration.MaximumDetectionTime)
            {
                validationResult.Warnings.Add($"Detection took longer than expected: {detectionResult.DetectionTime.TotalMilliseconds:F0}ms");
            }
        }

        /// <summary>
        /// Validates the completeness of detected versions
        /// </summary>
        /// <param name="detectionResult">Detection result</param>
        /// <param name="validationResult">Validation result</param>
        private async Task ValidateVersionCompletenessAsync(VersionDetectionResult detectionResult, VersionValidationResult validationResult)
        {
            // This would require access to the detected versions list
            // For now, we'll validate based on the counts in the detection result
            if (detectionResult.AvailableVersions == 0 && detectionResult.TotalVersions > 0)
            {
                validationResult.Warnings.Add("Detected versions but none are available. Check installation health.");
            }

            if (detectionResult.SupportedVersions == 0 && detectionResult.TotalVersions > 0)
            {
                validationResult.Warnings.Add("Detected versions but none are supported. Check version compatibility.");
            }
        }

        /// <summary>
        /// Validates the health of detected versions
        /// </summary>
        /// <param name="detectionResult">Detection result</param>
        /// <param name="validationResult">Validation result</param>
        private async Task ValidateVersionHealthAsync(VersionDetectionResult detectionResult, VersionValidationResult validationResult)
        {
            // Check if we have a good ratio of available to total versions
            if (detectionResult.TotalVersions > 0)
            {
                var availabilityRatio = (double)detectionResult.AvailableVersions / detectionResult.TotalVersions;
                if (availabilityRatio < _configuration.MinimumAvailabilityRatio)
                {
                    validationResult.Warnings.Add($"Low availability ratio: {availabilityRatio:P0}. Many versions may have issues.");
                }
            }
        }

        /// <summary>
        /// Validates version support compatibility
        /// </summary>
        /// <param name="detectionResult">Detection result</param>
        /// <param name="validationResult">Validation result</param>
        private void ValidateVersionSupport(VersionDetectionResult detectionResult, VersionValidationResult validationResult)
        {
            if (detectionResult.TotalVersions > 0)
            {
                var supportRatio = (double)detectionResult.SupportedVersions / detectionResult.TotalVersions;
                if (supportRatio < _configuration.MinimumSupportRatio)
                {
                    validationResult.Warnings.Add($"Low support ratio: {supportRatio:P0}. Many versions may be outdated.");
                }
            }
        }

        /// <summary>
        /// Validates performance metrics of the detection
        /// </summary>
        /// <param name="detectionResult">Detection result</param>
        /// <param name="validationResult">Validation result</param>
        private void ValidatePerformanceMetrics(VersionDetectionResult detectionResult, VersionValidationResult validationResult)
        {
            if (detectionResult.DetectionTime > _configuration.MaximumDetectionTime)
            {
                validationResult.Warnings.Add($"Detection performance is poor: {detectionResult.DetectionTime.TotalMilliseconds:F0}ms");
            }

            if (detectionResult.TotalVersions > _configuration.MaximumExpectedVersions)
            {
                validationResult.Warnings.Add($"Unusually high number of versions detected: {detectionResult.TotalVersions}");
            }
        }

        /// <summary>
        /// Validates consistency across different detection methods
        /// </summary>
        /// <param name="detectionResult">Detection result</param>
        /// <param name="validationResult">Validation result</param>
        private async Task ValidateDetectionConsistencyAsync(VersionDetectionResult detectionResult, VersionValidationResult validationResult)
        {
            // This would require access to the individual detection method results
            // For now, we'll add a placeholder for future implementation
            validationResult.Warnings.Add("Detection method consistency validation not yet implemented");
        }

        /// <summary>
        /// Generates recommendations based on validation results
        /// </summary>
        /// <param name="detectionResult">Detection result</param>
        /// <param name="validationResult">Validation result</param>
        private void GenerateRecommendations(VersionDetectionResult detectionResult, VersionValidationResult validationResult)
        {
            if (detectionResult.AvailableVersions == 0)
            {
                validationResult.Recommendations.Add("Check SolidWorks installations and ensure they are properly configured.");
            }

            if (detectionResult.SupportedVersions < detectionResult.TotalVersions)
            {
                validationResult.Recommendations.Add("Consider upgrading unsupported SolidWorks versions to ensure compatibility.");
            }

            if (detectionResult.Warnings.Count > _configuration.WarningThreshold)
            {
                validationResult.Recommendations.Add("Review and address the warnings to improve detection reliability.");
            }
        }

        /// <summary>
        /// Validates basic version information
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result</param>
        private void ValidateBasicVersionInfo(SolidWorksVersionInfo versionInfo, SingleVersionValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(versionInfo.Version))
            {
                result.Errors.Add("Version identifier is missing");
            }
            else if (!IsValidVersionFormat(versionInfo.Version))
            {
                result.Errors.Add($"Invalid version format: {versionInfo.Version}");
            }

            if (string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
            {
                result.Errors.Add("Installation path is missing");
            }

            if (string.IsNullOrWhiteSpace(versionInfo.ExecutablePath))
            {
                result.Warnings.Add("Executable path is not specified");
            }
        }

        /// <summary>
        /// Validates installation paths
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result</param>
        private async Task ValidateInstallationPathsAsync(SolidWorksVersionInfo versionInfo, SingleVersionValidationResult result)
        {
            if (!string.IsNullOrWhiteSpace(versionInfo.InstallationPath))
            {
                if (!Directory.Exists(versionInfo.InstallationPath))
                {
                    result.Errors.Add($"Installation directory does not exist: {versionInfo.InstallationPath}");
                }
                else
                {
                    // Check directory permissions
                    try
                    {
                        var testFile = Path.Combine(versionInfo.InstallationPath, "validation_test.tmp");
                        File.WriteAllText(testFile, "validation");
                        File.Delete(testFile);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cannot write to installation directory: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Validates executable accessibility
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result</param>
        private async Task ValidateExecutableAccessibilityAsync(SolidWorksVersionInfo versionInfo, SingleVersionValidationResult result)
        {
            if (!string.IsNullOrWhiteSpace(versionInfo.ExecutablePath))
            {
                if (!File.Exists(versionInfo.ExecutablePath))
                {
                    result.Errors.Add($"Executable file does not exist: {versionInfo.ExecutablePath}");
                }
                else
                {
                    // Check file properties
                    try
                    {
                        var fileInfo = new FileInfo(versionInfo.ExecutablePath);
                        if (fileInfo.Length == 0)
                        {
                            result.Errors.Add($"Executable file is empty: {versionInfo.ExecutablePath}");
                        }
                        else if (fileInfo.Length < _configuration.MinimumExecutableSize)
                        {
                            result.Warnings.Add($"Executable file is unusually small: {fileInfo.Length} bytes");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cannot access executable file properties: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Validates license manager accessibility
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result</param>
        private async Task ValidateLicenseManagerAccessibilityAsync(SolidWorksVersionInfo versionInfo, SingleVersionValidationResult result)
        {
            if (!string.IsNullOrWhiteSpace(versionInfo.LmutilPath))
            {
                if (!File.Exists(versionInfo.LmutilPath))
                {
                    result.Warnings.Add($"License manager utility does not exist: {versionInfo.LmutilPath}");
                }
                else
                {
                    // Check if we can execute the license manager
                    try
                    {
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
                        await Task.Run(() => process.WaitForExit(5000)); // 5 second timeout

                        if (process.ExitCode != 0)
                        {
                            result.Warnings.Add($"License manager utility returned error code: {process.ExitCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cannot execute license manager utility: {ex.Message}");
                    }
                }
            }
            else
            {
                result.Warnings.Add("License manager utility path is not specified");
            }
        }

        /// <summary>
        /// Validates version compatibility
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result</param>
        private void ValidateVersionCompatibility(SolidWorksVersionInfo versionInfo, SingleVersionValidationResult result)
        {
            if (!versionInfo.IsSupported)
            {
                result.Warnings.Add($"Version {versionInfo.Version} is not in the supported range (2020-2025)");
            }

            // Check if version is too old
            if (int.TryParse(versionInfo.Version, out int year) && year < 2020)
            {
                result.Warnings.Add($"Version {versionInfo.Version} is outdated and may not be fully supported");
            }

            // Check if version is too new
            if (year > DateTime.Now.Year + 1)
            {
                result.Warnings.Add($"Version {versionInfo.Version} is newer than expected and may not be fully supported");
            }
        }

        /// <summary>
        /// Validates registry consistency
        /// </summary>
        /// <param name="versionInfo">Version information</param>
        /// <param name="result">Validation result</param>
        private async Task ValidateRegistryConsistencyAsync(SolidWorksVersionInfo versionInfo, SingleVersionValidationResult result)
        {
            if (!string.IsNullOrWhiteSpace(versionInfo.RegistryKeyPath))
            {
                try
                {
                    // Try to access the registry key
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(versionInfo.RegistryKeyPath))
                    {
                        if (key == null)
                        {
                            result.Warnings.Add($"Registry key not accessible: {versionInfo.RegistryKeyPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Cannot access registry key: {ex.Message}");
                }
            }
            else
            {
                result.Warnings.Add("Registry key path is not specified");
            }
        }

        /// <summary>
        /// Validates consistency across multiple versions
        /// </summary>
        /// <param name="versions">List of versions</param>
        /// <param name="result">Validation result</param>
        private void ValidateMultiVersionConsistency(List<SolidWorksVersionInfo> versions, MultiVersionValidationResult result)
        {
            // Check for duplicate versions
            var versionGroups = versions.GroupBy(v => v.Version).Where(g => g.Count() > 1);
            foreach (var group in versionGroups)
            {
                result.Warnings.Add($"Duplicate version detected: {group.Key} appears {group.Count()} times");
            }

            // Check for overlapping installation paths
            var pathGroups = versions.GroupBy(v => v.InstallationPath).Where(g => g.Count() > 1);
            foreach (var group in pathGroups)
            {
                result.Warnings.Add($"Overlapping installation path detected: {group.Key}");
            }
        }

        /// <summary>
        /// Calculates overall statistics for multi-version validation
        /// </summary>
        /// <param name="result">Validation result</param>
        private void CalculateOverallStatistics(MultiVersionValidationResult result)
        {
            if (result.VersionResults.Count == 0)
                return;

            var totalHealthScore = result.VersionResults.Sum(r => r.HealthScore);
            result.OverallHealthScore = totalHealthScore / result.VersionResults.Count;

            var healthyVersions = result.VersionResults.Count(r => r.HealthScore >= _configuration.HealthyThreshold);
            result.HealthyVersionCount = healthyVersions;

            result.TotalVersionCount = result.VersionResults.Count;
        }

        /// <summary>
        /// Calculates health score for a version
        /// </summary>
        /// <param name="result">Validation result</param>
        /// <returns>Health score (0-100)</returns>
        private int CalculateHealthScore(SingleVersionValidationResult result)
        {
            if (result.Errors.Count > 0)
                return 0;

            int score = 100;

            // Deduct points for warnings
            score -= result.Warnings.Count * _configuration.WarningScoreDeduction;

            // Additional deductions for specific issues
            if (!result.HasExecutable)
                score -= 30;

            if (!result.HasLicenseManager)
                score -= 20;

            if (!result.IsAccessible)
                score -= 40;

            return Math.Max(0, score);
        }

        /// <summary>
        /// Validates that a version string is in the correct format
        /// </summary>
        /// <param name="version">Version string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private static bool IsValidVersionFormat(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(version, @"^20[0-9]{2}$");
        }
    }

    /// <summary>
    /// Configuration for version validation
    /// </summary>
    public class VersionValidationConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum allowed detection time
        /// </summary>
        public TimeSpan MaximumDetectionTime { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the minimum availability ratio (0.0-1.0)
        /// </summary>
        public double MinimumAvailabilityRatio { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets the minimum support ratio (0.0-1.0)
        /// </summary>
        public double MinimumSupportRatio { get; set; } = 0.8;

        /// <summary>
        /// Gets or sets the maximum expected number of versions
        /// </summary>
        public int MaximumExpectedVersions { get; set; } = 10;

        /// <summary>
        /// Gets or sets the warning threshold for number of warnings
        /// </summary>
        public int WarningThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets the minimum executable size in bytes
        /// </summary>
        public long MinimumExecutableSize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Gets or sets the score deduction per warning
        /// </summary>
        public int WarningScoreDeduction { get; set; } = 10;

        /// <summary>
        /// Gets or sets the health threshold score
        /// </summary>
        public int HealthyThreshold { get; set; } = 70;
    }

    /// <summary>
    /// Result of validating a version detection operation
    /// </summary>
    public class VersionValidationResult
    {
        /// <summary>
        /// Gets the list of validation errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the list of validation warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets the list of recommendations
        /// </summary>
        public List<string> Recommendations { get; }

        /// <summary>
        /// Gets a value indicating whether validation passed
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets a value indicating whether validation has any issues
        /// </summary>
        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Initializes a new instance of the VersionValidationResult class
        /// </summary>
        public VersionValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            Recommendations = new List<string>();
        }
    }

    /// <summary>
    /// Result of validating a single version
    /// </summary>
    public class SingleVersionValidationResult
    {
        /// <summary>
        /// Gets or sets the version that was validated
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets the list of validation errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the list of validation warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets or sets the health score (0-100)
        /// </summary>
        public int HealthScore { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version has an accessible executable
        /// </summary>
        public bool HasExecutable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version has an accessible license manager
        /// </summary>
        public bool HasLicenseManager { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the version is accessible
        /// </summary>
        public bool IsAccessible { get; set; }

        /// <summary>
        /// Gets a value indicating whether validation passed
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets a value indicating whether validation has any issues
        /// </summary>
        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Initializes a new instance of the SingleVersionValidationResult class
        /// </summary>
        public SingleVersionValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            HealthScore = 100;
            HasExecutable = false;
            HasLicenseManager = false;
            IsAccessible = false;
        }
    }

    /// <summary>
    /// Result of validating multiple versions
    /// </summary>
    public class MultiVersionValidationResult
    {
        /// <summary>
        /// Gets the list of individual version results
        /// </summary>
        public List<SingleVersionValidationResult> VersionResults { get; }

        /// <summary>
        /// Gets the overall health score (0-100)
        /// </summary>
        public double OverallHealthScore { get; set; }

        /// <summary>
        /// Gets the number of healthy versions
        /// </summary>
        public int HealthyVersionCount { get; set; }

        /// <summary>
        /// Gets the total number of versions
        /// </summary>
        public int TotalVersionCount { get; set; }

        /// <summary>
        /// Gets the list of validation errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the list of validation warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets the health scores by version
        /// </summary>
        public Dictionary<string, int> HealthScores { get; }

        /// <summary>
        /// Gets a value indicating whether validation passed
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets a value indicating whether validation has any issues
        /// </summary>
        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Initializes a new instance of the MultiVersionValidationResult class
        /// </summary>
        public MultiVersionValidationResult()
        {
            VersionResults = new List<SingleVersionValidationResult>();
            Errors = new List<string>();
            Warnings = new List<string>();
            HealthScores = new Dictionary<string, int>();
            OverallHealthScore = 0;
        }
    }
}