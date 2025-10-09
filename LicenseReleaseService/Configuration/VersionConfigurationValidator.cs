using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Provides comprehensive validation for version-specific configurations
    /// </summary>
    public class VersionConfigurationValidator
    {
        private readonly ILogger<VersionConfigurationValidator> _logger;
        private readonly VersionConfigurationManager _configManager;
        private readonly VersionValidationConfiguration _validationConfig;

        /// <summary>
        /// Initializes a new instance of the VersionConfigurationValidator class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="configManager">Version configuration manager</param>
        /// <param name="validationConfig">Validation configuration</param>
        public VersionConfigurationValidator(ILogger<VersionConfigurationValidator> logger,
            VersionConfigurationManager configManager, VersionValidationConfiguration validationConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _validationConfig = validationConfig ?? throw new ArgumentNullException(nameof(validationConfig));
        }

        /// <summary>
        /// Validates a version configuration comprehensively
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <returns>Comprehensive validation result</returns>
        public async Task<ComprehensiveVersionValidationResult> ValidateVersionConfigurationAsync(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return new ComprehensiveVersionValidationResult
                {
                    Version = version,
                    Errors = { "Version cannot be null or empty" },
                    ValidationScore = 0
                };
            }

            try
            {
                _logger.LogInformation("Starting comprehensive validation of version {Version}", version);

                var result = new ComprehensiveVersionValidationResult { Version = version };

                // Basic configuration validation
                await ValidateBasicConfigurationAsync(version, result);

                // Feature mapping validation
                await ValidateFeatureMappingsAsync(version, result);

                // Network connectivity validation
                await ValidateNetworkConnectivityAsync(version, result);

                // License server accessibility validation
                await ValidateLicenseServerAccessibilityAsync(version, result);

                // Configuration consistency validation
                await ValidateConfigurationConsistencyAsync(version, result);

                // Performance validation
                await ValidatePerformanceCharacteristicsAsync(version, result);

                // Security validation
                await ValidateSecuritySettingsAsync(version, result);

                // Calculate overall validation score
                result.ValidationScore = CalculateValidationScore(result);

                // Generate recommendations
                GenerateRecommendations(result);

                _logger.LogInformation("Version {Version} validation completed. Score: {Score}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                    version, result.ValidationScore, result.Errors.Count, result.Warnings.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during comprehensive validation of version {Version}", version);
                return new ComprehensiveVersionValidationResult
                {
                    Version = version,
                    Errors = { $"Validation error: {ex.Message}" },
                    ValidationScore = 0
                };
            }
        }

        /// <summary>
        /// Validates all version configurations comprehensively
        /// </summary>
        /// <returns>Dictionary of version validation results</returns>
        public async Task<Dictionary<string, ComprehensiveVersionValidationResult>> ValidateAllConfigurationsAsync()
        {
            _logger.LogInformation("Starting comprehensive validation of all version configurations");

            var results = new Dictionary<string, ComprehensiveVersionValidationResult>();
            var enabledVersions = _configManager.GetEnabledVersions();

            var validationTasks = enabledVersions.Select(async version =>
            {
                var result = await ValidateVersionConfigurationAsync(version);
                return new { Version = version, Result = result };
            });

            var validationResults = await Task.WhenAll(validationTasks);

            foreach (var validation in validationResults)
            {
                results[validation.Version] = validation.Result;
            }

            // Perform cross-version validation
            await ValidateCrossVersionConsistencyAsync(results);

            _logger.LogInformation("Comprehensive validation of all version configurations completed. Processed {Count} versions", results.Count);

            return results;
        }

        /// <summary>
        /// Validates basic configuration settings
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateBasicConfigurationAsync(string version, ComprehensiveVersionValidationResult result)
        {
            try
            {
                var config = _configManager.GetVersionConfigurationWithInheritance(version);
                if (config == null)
                {
                    result.Errors.Add($"Configuration not found for version {version}");
                    return;
                }

                // Validate version format and range
                if (!IsValidVersionFormat(version))
                {
                    result.Errors.Add($"Invalid version format: {version}");
                }
                else if (int.TryParse(version, out int year))
                {
                    if (year < 2020 || year > 2025)
                    {
                        result.Errors.Add($"Version {version} is outside the supported range (2020-2025)");
                    }
                }

                // Validate required settings
                if (string.IsNullOrWhiteSpace(config.LicenseServer))
                {
                    result.Warnings.Add($"License server not specified for version {version}");
                }

                if (config.Timeout <= 0)
                {
                    result.Errors.Add($"Invalid timeout value: {config.Timeout}");
                }

                if (config.MaxConcurrentLicenses <= 0)
                {
                    result.Errors.Add($"Invalid max concurrent licenses: {config.MaxConcurrentLicenses}");
                }

                // Validate timeout relationships
                if (config.CommandTimeout < config.Timeout)
                {
                    result.Warnings.Add($"Command timeout ({config.CommandTimeout}s) should be greater than or equal to timeout ({config.Timeout}s)");
                }

                // Validate retry logic
                if (config.RetryCount > 0 && config.Timeout <= 0)
                {
                    result.Warnings.Add($"Retry count is specified but timeout is invalid");
                }

                // Validate path settings
                if (!string.IsNullOrWhiteSpace(config.LmutilPath))
                {
                    if (!System.IO.File.Exists(config.LmutilPath))
                    {
                        result.Warnings.Add($"lmutil.exe not found at: {config.LmutilPath}");
                    }
                }

                // Validate feature codes
                var featureCodes = config.ParsedFeatureCodes;
                if (featureCodes.Count == 0)
                {
                    result.Warnings.Add($"No feature codes specified for version {version}");
                }
                else
                {
                    foreach (var featureCode in featureCodes)
                    {
                        if (!IsValidFeatureCodeFormat(featureCode))
                        {
                            result.Warnings.Add($"Invalid feature code format: {featureCode}");
                        }
                    }
                }

                // Validate inheritance configuration
                if (!string.IsNullOrWhiteSpace(config.InheritFrom))
                {
                    var parentConfig = _configManager.GetVersionConfiguration(config.InheritFrom);
                    if (parentConfig == null)
                    {
                        result.Errors.Add($"Parent version {config.InheritFrom} not found for inheritance");
                    }
                    else if (!parentConfig.Enabled)
                    {
                        result.Warnings.Add($"Parent version {config.InheritFrom} is disabled");
                    }
                }

                result.BasicConfigurationValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating basic configuration for version {Version}", version);
                result.Errors.Add($"Basic configuration validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates feature mappings
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateFeatureMappingsAsync(string version, ComprehensiveVersionValidationResult result)
        {
            try
            {
                var config = _configManager.GetVersionConfigurationWithInheritance(version);
                if (config == null)
                    return;

                var featureCodes = config.ParsedFeatureCodes;
                var validMappings = 0;
                var totalMappings = 0;

                foreach (var featureCode in featureCodes)
                {
                    totalMappings++;

                    // Check if feature mapping exists
                    var featureMapping = _configManager.CurrentConfiguration.GetVersionConfiguration(featureCode);
                    if (featureMapping == null)
                    {
                        result.Warnings.Add($"No feature mapping found for feature code {featureCode} in version {version}");
                        continue;
                    }

                    // Validate feature mapping configuration
                    var mappingErrors = featureMapping.Validate();
                    if (mappingErrors.Count > 0)
                    {
                        result.Warnings.AddRange(mappingErrors.Select(e => $"Feature {featureCode}: {e}"));
                        continue;
                    }

                    // Check inheritance for feature mapping
                    if (!string.IsNullOrWhiteSpace(featureMapping.InheritFrom))
                    {
                        var parentMapping = _configManager.CurrentConfiguration.GetVersionConfiguration(featureMapping.InheritFrom);
                        if (parentMapping == null)
                        {
                            result.Warnings.Add($"Parent feature mapping {featureMapping.InheritFrom} not found for feature {featureCode}");
                        }
                    }

                    validMappings++;
                }

                // Calculate feature mapping health
                result.FeatureMappingHealth = totalMappings > 0 ? (double)validMappings / totalMappings : 0;

                if (result.FeatureMappingHealth < _validationConfig.MinimumFeatureMappingHealth)
                {
                    result.Warnings.Add($"Low feature mapping health: {result.FeatureMappingHealth:P0}");
                }

                result.FeatureMappingsValid = validMappings == totalMappings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating feature mappings for version {Version}", version);
                result.Errors.Add($"Feature mapping validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates network connectivity
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateNetworkConnectivityAsync(string version, ComprehensiveVersionValidationResult result)
        {
            try
            {
                var config = _configManager.GetVersionConfigurationWithInheritance(version);
                if (config == null || string.IsNullOrWhiteSpace(config.LicenseServer))
                    return;

                // Test basic network connectivity
                var connectivityResult = await TestNetworkConnectivityAsync(config.LicenseServer, config.Port);
                if (!connectivityResult.IsConnected)
                {
                    result.Warnings.Add($"Cannot connect to license server {config.LicenseServer}:{config.Port} - {connectivityResult.ErrorMessage}");
                }

                // Test DNS resolution
                var dnsResult = await TestDnsResolutionAsync(config.LicenseServer);
                if (!dnsResult.IsResolved)
                {
                    result.Warnings.Add($"DNS resolution failed for {config.LicenseServer} - {dnsResult.ErrorMessage}");
                }

                // Test network latency
                var latencyResult = await TestNetworkLatencyAsync(config.LicenseServer, config.Port);
                if (latencyResult.LatencyMs > _validationConfig.MaximumNetworkLatencyMs)
                {
                    result.Warnings.Add($"High network latency to {config.LicenseServer}: {latencyResult.LatencyMs}ms");
                }

                result.NetworkConnectivityValid = connectivityResult.IsConnected && dnsResult.IsResolved;
                result.NetworkLatencyMs = latencyResult.LatencyMs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating network connectivity for version {Version}", version);
                result.Errors.Add($"Network connectivity validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates license server accessibility
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateLicenseServerAccessibilityAsync(string version, ComprehensiveVersionValidationResult result)
        {
            try
            {
                var config = _configManager.GetVersionConfigurationWithInheritance(version);
                if (config == null || !config.EnableHealthCheck || string.IsNullOrWhiteSpace(config.LicenseServer))
                    return;

                // Test license server accessibility
                var accessibilityResult = await TestLicenseServerAccessibilityAsync(config.LicenseServer, config.Port);
                if (!accessibilityResult.IsAccessible)
                {
                    result.Warnings.Add($"License server {config.LicenseServer}:{config.Port} is not accessible - {accessibilityResult.ErrorMessage}");
                }

                // Test license manager functionality if lmutil path is specified
                LicenseManagerFunctionalityResult lmutilResult = null;
                if (!string.IsNullOrWhiteSpace(config.LmutilPath) && System.IO.File.Exists(config.LmutilPath))
                {
                    lmutilResult = await TestLicenseManagerFunctionalityAsync(config.LmutilPath, config.LicenseServer, config.Port);
                    if (!lmutilResult.IsFunctional)
                    {
                        result.Warnings.Add($"License manager functionality test failed - {lmutilResult.ErrorMessage}");
                    }
                }

                result.LicenseServerAccessible = accessibilityResult.IsAccessible;
                result.LicenseManagerFunctional = lmutilResult?.IsFunctional ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating license server accessibility for version {Version}", version);
                result.Errors.Add($"License server accessibility validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates configuration consistency
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateConfigurationConsistencyAsync(string version, ComprehensiveVersionValidationResult result)
        {
            try
            {
                var config = _configManager.GetVersionConfigurationWithInheritance(version);
                if (config == null)
                    return;

                // Check for consistency with other versions
                var allVersions = _configManager.GetEnabledVersions();
                var versionConfigs = allVersions.Select(v => _configManager.GetVersionConfigurationWithInheritance(v))
                    .Where(c => c != null)
                    .ToList();

                // Check for duplicate license servers
                var duplicateServers = versionConfigs
                    .Where(c => !string.IsNullOrWhiteSpace(c.LicenseServer))
                    .GroupBy(c => c.LicenseServer)
                    .Where(g => g.Count() > 1)
                    .ToList();

                foreach (var group in duplicateServers)
                {
                    if (group.Any(c => c.Version == version))
                    {
                        result.Warnings.Add($"License server {group.Key} is used by multiple versions: {string.Join(", ", group.Select(c => c.Version))}");
                    }
                }

                // Check for inconsistent timeout values
                var timeoutVariations = versionConfigs
                    .Select(c => c.Timeout)
                    .Distinct()
                    .Count();

                if (timeoutVariations > 1)
                {
                    result.Warnings.Add("Timeout values vary across versions");
                }

                // Check inheritance consistency
                if (!string.IsNullOrWhiteSpace(config.InheritFrom))
                {
                    var parentConfig = _configManager.GetVersionConfigurationWithInheritance(config.InheritFrom);
                    if (parentConfig != null)
                    {
                        var conflicts = FindInheritanceConflicts(config, parentConfig);
                        if (conflicts.Count > 0)
                        {
                            result.Warnings.AddRange(conflicts.Select(c => $"Inheritance conflict: {c}"));
                        }
                    }
                }

                result.ConfigurationConsistent = result.Warnings.Count(w => w.Contains("conflict") || w.Contains("inconsistent")) == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration consistency for version {Version}", version);
                result.Errors.Add($"Configuration consistency validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates performance characteristics
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidatePerformanceCharacteristicsAsync(string version, ComprehensiveVersionValidationResult result)
        {
            try
            {
                var config = _configManager.GetVersionConfigurationWithInheritance(version);
                if (config == null)
                    return;

                // Validate timeout settings
                if (config.Timeout > _validationConfig.MaximumTimeout)
                {
                    result.Warnings.Add($"Timeout value {config.Timeout}s exceeds recommended maximum {_validationConfig.MaximumTimeout}s");
                }

                if (config.CommandTimeout > _validationConfig.MaximumCommandTimeout)
                {
                    result.Warnings.Add($"Command timeout value {config.CommandTimeout}s exceeds recommended maximum {_validationConfig.MaximumCommandTimeout}s");
                }

                // Validate retry settings
                if (config.RetryCount > _validationConfig.MaximumRetryCount)
                {
                    result.Warnings.Add($"Retry count {config.RetryCount} exceeds recommended maximum {_validationConfig.MaximumRetryCount}");
                }

                // Validate concurrent license settings
                if (config.MaxConcurrentLicenses > _validationConfig.MaximumConcurrentLicenses)
                {
                    result.Warnings.Add($"Max concurrent licenses {config.MaxConcurrentLicenses} exceeds recommended maximum {_validationConfig.MaximumConcurrentLicenses}");
                }

                // Validate release delay
                if (config.ReleaseDelay > _validationConfig.MaximumReleaseDelay)
                {
                    result.Warnings.Add($"Release delay {config.ReleaseDelay}ms exceeds recommended maximum {_validationConfig.MaximumReleaseDelay}ms");
                }

                result.PerformanceCharacteristicsValid = result.Warnings.Count(w => w.Contains("exceeds")) == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating performance characteristics for version {Version}", version);
                result.Errors.Add($"Performance characteristics validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates security settings
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="result">Validation result to update</param>
        private async Task ValidateSecuritySettingsAsync(string version, ComprehensiveVersionValidationResult result)
        {
            try
            {
                var config = _configManager.GetVersionConfigurationWithInheritance(version);
                if (config == null)
                    return;

                // Validate license server address security
                if (!string.IsNullOrWhiteSpace(config.LicenseServer))
                {
                    if (config.LicenseServer.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                        config.LicenseServer.Equals("127.0.0.1"))
                    {
                        result.Warnings.Add("License server is configured to use localhost - this may indicate a development configuration");
                    }

                    if (config.LicenseServer.StartsWith("192.168.") || config.LicenseServer.StartsWith("10."))
                    {
                        result.Warnings.Add($"License server {config.LicenseServer} uses a private IP address - ensure this is intended");
                    }
                }

                // Validate port security
                if (config.Port < 1024)
                {
                    result.Warnings.Add($"License server port {config.Port} is a privileged port - ensure this is intended");
                }

                if (config.Port > 49151)
                {
                    result.Warnings.Add($"License server port {config.Port} is in the dynamic/private range - ensure this is intended");
                }

                // Validate path security
                if (!string.IsNullOrWhiteSpace(config.LmutilPath))
                {
                    if (config.LmutilPath.Contains(" "))
                    {
                        result.Warnings.Add("lmutil path contains spaces - ensure proper quoting is used");
                    }

                    if (config.LmutilPath.ToLower().Contains("temp") || config.LmutilPath.ToLower().Contains("tmp"))
                    {
                        result.Warnings.Add("lmutil path appears to be in a temporary directory - this may not be secure");
                    }
                }

                result.SecuritySettingsValid = result.Warnings.Count(w => w.Contains("security") || w.Contains("secure")) == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating security settings for version {Version}", version);
                result.Errors.Add($"Security settings validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates cross-version consistency
        /// </summary>
        /// <param name="results">Dictionary of validation results</param>
        private async Task ValidateCrossVersionConsistencyAsync(Dictionary<string, ComprehensiveVersionValidationResult> results)
        {
            try
            {
                var enabledVersions = _configManager.GetEnabledVersions();
                var versionConfigs = enabledVersions.Select(v => _configManager.GetVersionConfigurationWithInheritance(v))
                    .Where(c => c != null)
                    .ToList();

                // Check for consistent license servers across versions
                var licenseServers = versionConfigs.Select(c => c.LicenseServer).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (licenseServers.Count > 3)
                {
                    var warning = $"Multiple license servers detected across versions: {string.Join(", ", licenseServers.Take(3))}...";
                    foreach (var result in results.Values)
                    {
                        result.Warnings.Add(warning);
                    }
                }

                // Check for version gaps
                var versionYears = enabledVersions.Select(v => int.Parse(v)).OrderBy(v => v).ToList();
                for (int i = 1; i < versionYears.Count; i++)
                {
                    if (versionYears[i] - versionYears[i - 1] > 2)
                    {
                        var gapWarning = $"Version gap detected between {versionYears[i - 1]} and {versionYears[i]}";
                        foreach (var result in results.Values)
                        {
                            result.Warnings.Add(gapWarning);
                        }
                    }
                }

                // Check for consistent feature codes
                var allFeatureCodes = versionConfigs.SelectMany(c => c.ParsedFeatureCodes).Distinct().ToList();
                var featureCodeCoverage = new Dictionary<string, List<string>>();
                foreach (var featureCode in allFeatureCodes)
                {
                    var versionsWithFeature = versionConfigs
                        .Where(c => c.ParsedFeatureCodes.Contains(featureCode))
                        .Select(c => c.Version)
                        .ToList();
                    featureCodeCoverage[featureCode] = versionsWithFeature;
                }

                var inconsistentFeatures = featureCodeCoverage
                    .Where(kvp => kvp.Value.Count < enabledVersions.Count)
                    .ToList();

                if (inconsistentFeatures.Count > 0)
                {
                    var coverageWarning = $"Inconsistent feature code coverage across versions: {string.Join(", ", inconsistentFeatures.Take(3).Select(kvp => kvp.Key))}...";
                    foreach (var result in results.Values)
                    {
                        result.Warnings.Add(coverageWarning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating cross-version consistency");
                foreach (var result in results.Values)
                {
                    result.Errors.Add($"Cross-version consistency validation error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Generates recommendations based on validation results
        /// </summary>
        /// <param name="result">Validation result</param>
        private void GenerateRecommendations(ComprehensiveVersionValidationResult result)
        {
            try
            {
                if (result.Errors.Count > 0)
                {
                    result.Recommendations.Add("Address all validation errors before deploying this configuration");
                }

                if (result.Warnings.Count > 5)
                {
                    result.Recommendations.Add("Consider addressing warnings to improve configuration quality");
                }

                if (!result.NetworkConnectivityValid)
                {
                    result.Recommendations.Add("Check network connectivity to license servers");
                }

                if (!result.LicenseServerAccessible)
                {
                    result.Recommendations.Add("Verify license server availability and configuration");
                }

                if (result.FeatureMappingHealth < 0.8)
                {
                    result.Recommendations.Add("Review and complete feature mapping configurations");
                }

                if (result.NetworkLatencyMs > _validationConfig.MaximumNetworkLatencyMs / 2)
                {
                    result.Recommendations.Add("Consider optimizing network infrastructure for better performance");
                }

                if (result.ValidationScore < 70)
                {
                    result.Recommendations.Add("Configuration quality is below acceptable threshold - consider comprehensive review");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recommendations for version {Version}", result.Version);
            }
        }

        /// <summary>
        /// Calculates the overall validation score
        /// </summary>
        /// <param name="result">Validation result</param>
        /// <returns>Validation score (0-100)</returns>
        private int CalculateValidationScore(ComprehensiveVersionValidationResult result)
        {
            try
            {
                int score = 100;

                // Deduct points for errors
                score -= result.Errors.Count * _validationConfig.ErrorScoreDeduction;

                // Deduct points for warnings
                score -= result.Warnings.Count * _validationConfig.WarningScoreDeduction;

                // Bonus for good feature mapping health
                if (result.FeatureMappingHealth > 0.8)
                {
                    score += 10;
                }

                // Bonus for good network connectivity
                if (result.NetworkConnectivityValid && result.NetworkLatencyMs < _validationConfig.MaximumNetworkLatencyMs / 2)
                {
                    score += 5;
                }

                // Bonus for good license server accessibility
                if (result.LicenseServerAccessible)
                {
                    score += 5;
                }

                return Math.Max(0, Math.Min(100, score));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating validation score for version {Version}", result.Version);
                return 0;
            }
        }

        /// <summary>
        /// Tests network connectivity to a server
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <returns>Network connectivity result</returns>
        private async Task<NetworkConnectivityResult> TestNetworkConnectivityAsync(string server, int port)
        {
            try
            {
                using (var tcpClient = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(server, port);
                    var timeoutTask = Task.Delay(_validationConfig.NetworkTimeout);

                    // Wait for connection with timeout using WaitAsync
                    try
                    {
                        using var timeoutCts = new CancellationTokenSource(_validationConfig.NetworkTimeout);
                        await connectTask.WaitAsync(timeoutCts.Token);
                        return new NetworkConnectivityResult { IsConnected = true };
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout occurred
                        return new NetworkConnectivityResult { IsConnected = false, ErrorMessage = "Connection timeout" };
                    }
                }
            }
            catch (Exception ex)
            {
                return new NetworkConnectivityResult { IsConnected = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Tests DNS resolution for a server
        /// </summary>
        /// <param name="server">Server address</param>
        /// <returns>DNS resolution result</returns>
        private async Task<DnsResolutionResult> TestDnsResolutionAsync(string server)
        {
            try
            {
                var hostEntry = await System.Net.Dns.GetHostEntryAsync(server);
                return new DnsResolutionResult { IsResolved = true, IpAddresses = hostEntry.AddressList };
            }
            catch (Exception ex)
            {
                return new DnsResolutionResult { IsResolved = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Tests network latency to a server
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <returns>Network latency result</returns>
        private async Task<NetworkLatencyResult> TestNetworkLatencyAsync(string server, int port)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                using (var tcpClient = new System.Net.Sockets.TcpClient())
                {
                    await tcpClient.ConnectAsync(server, port);
                    stopwatch.Stop();
                    return new NetworkLatencyResult { LatencyMs = (int)stopwatch.ElapsedMilliseconds };
                }
            }
            catch (Exception ex)
            {
                return new NetworkLatencyResult { LatencyMs = -1, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Tests license server accessibility
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <returns>License server accessibility result</returns>
        private async Task<LicenseServerAccessibilityResult> TestLicenseServerAccessibilityAsync(string server, int port)
        {
            try
            {
                // This is a simplified implementation
                // In a real implementation, you would test actual license server functionality
                var connectivityResult = await TestNetworkConnectivityAsync(server, port);
                return new LicenseServerAccessibilityResult
                {
                    IsAccessible = connectivityResult.IsConnected,
                    ErrorMessage = connectivityResult.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                return new LicenseServerAccessibilityResult { IsAccessible = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Tests license manager functionality
        /// </summary>
        /// <param name="lmutilPath">Path to lmutil.exe</param>
        /// <param name="server">License server address</param>
        /// <param name="port">License server port</param>
        /// <returns>License manager functionality result</returns>
        private async Task<LicenseManagerFunctionalityResult> TestLicenseManagerFunctionalityAsync(string lmutilPath, string server, int port)
        {
            try
            {
                // This is a simplified implementation
                // In a real implementation, you would test actual license manager functionality
                if (!System.IO.File.Exists(lmutilPath))
                {
                    return new LicenseManagerFunctionalityResult { IsFunctional = false, ErrorMessage = "lmutil.exe not found" };
                }

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = lmutilPath,
                        Arguments = "-help",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await Task.Run(() => process.WaitForExit(5000));

                if (process.ExitCode == 0)
                {
                    return new LicenseManagerFunctionalityResult { IsFunctional = true };
                }
                else
                {
                    return new LicenseManagerFunctionalityResult { IsFunctional = false, ErrorMessage = $"lmutil.exe returned error code {process.ExitCode}" };
                }
            }
            catch (Exception ex)
            {
                return new LicenseManagerFunctionalityResult { IsFunctional = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Finds inheritance conflicts between configurations
        /// </summary>
        /// <param name="childConfig">Child configuration</param>
        /// <param name="parentConfig">Parent configuration</param>
        /// <returns>List of conflict descriptions</returns>
        private List<string> FindInheritanceConflicts(VersionConfigurationElement childConfig, VersionConfigurationElement parentConfig)
        {
            var conflicts = new List<string>();

            // Check for conflicting license servers
            if (!string.IsNullOrWhiteSpace(childConfig.LicenseServer) &&
                !string.IsNullOrWhiteSpace(parentConfig.LicenseServer) &&
                !childConfig.LicenseServer.Equals(parentConfig.LicenseServer, StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add($"License server conflict: child={childConfig.LicenseServer}, parent={parentConfig.LicenseServer}");
            }

            // Check for conflicting ports
            if (childConfig.Port != 27000 && parentConfig.Port != 27000 && childConfig.Port != parentConfig.Port)
            {
                conflicts.Add($"Port conflict: child={childConfig.Port}, parent={parentConfig.Port}");
            }

            // Check for conflicting timeouts
            if (childConfig.Timeout != 30 && parentConfig.Timeout != 30 && childConfig.Timeout < parentConfig.Timeout)
            {
                conflicts.Add($"Timeout conflict: child={childConfig.Timeout}s, parent={parentConfig.Timeout}s");
            }

            return conflicts;
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

        /// <summary>
        /// Validates that a feature code is in the correct format
        /// </summary>
        /// <param name="featureCode">Feature code to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private static bool IsValidFeatureCodeFormat(string featureCode)
        {
            if (string.IsNullOrWhiteSpace(featureCode))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(featureCode, @"^[A-Za-z0-9_\-]+$");
        }

        /// <summary>
        /// Configuration for version validation
        /// </summary>
        public class VersionValidationConfiguration
        {
            /// <summary>
            /// Gets or sets the minimum feature mapping health (0.0-1.0)
            /// </summary>
            public double MinimumFeatureMappingHealth { get; set; } = 0.8;

            /// <summary>
            /// Gets or sets the maximum network latency in milliseconds
            /// </summary>
            public int MaximumNetworkLatencyMs { get; set; } = 1000;

            /// <summary>
            /// Gets or sets the maximum timeout in seconds
            /// </summary>
            public int MaximumTimeout { get; set; } = 120;

            /// <summary>
            /// Gets or sets the maximum command timeout in seconds
            /// </summary>
            public int MaximumCommandTimeout { get; set; } = 300;

            /// <summary>
            /// Gets or sets the maximum retry count
            /// </summary>
            public int MaximumRetryCount { get; set; } = 5;

            /// <summary>
            /// Gets or sets the maximum concurrent licenses
            /// </summary>
            public int MaximumConcurrentLicenses { get; set; } = 50;

            /// <summary>
            /// Gets or sets the maximum release delay in milliseconds
            /// </summary>
            public int MaximumReleaseDelay { get; set; } = 5000;

            /// <summary>
            /// Gets or sets the network timeout
            /// </summary>
            public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromSeconds(5);

            /// <summary>
            /// Gets or sets the score deduction per error
            /// </summary>
            public int ErrorScoreDeduction { get; set; } = 20;

            /// <summary>
            /// Gets or sets the score deduction per warning
            /// </summary>
            public int WarningScoreDeduction { get; set; } = 5;
        }

        /// <summary>
        /// Result of network connectivity test
        /// </summary>
        public class NetworkConnectivityResult
        {
            /// <summary>
            /// Gets or sets whether the connection was successful
            /// </summary>
            public bool IsConnected { get; set; }

            /// <summary>
            /// Gets or sets the error message if connection failed
            /// </summary>
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// Result of DNS resolution test
        /// </summary>
        public class DnsResolutionResult
        {
            /// <summary>
            /// Gets or sets whether the resolution was successful
            /// </summary>
            public bool IsResolved { get; set; }

            /// <summary>
            /// Gets or sets the resolved IP addresses
            /// </summary>
            public System.Net.IPAddress[] IpAddresses { get; set; }

            /// <summary>
            /// Gets or sets the error message if resolution failed
            /// </summary>
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// Result of network latency test
        /// </summary>
        public class NetworkLatencyResult
        {
            /// <summary>
            /// Gets or sets the latency in milliseconds
            /// </summary>
            public int LatencyMs { get; set; }

            /// <summary>
            /// Gets or sets the error message if test failed
            /// </summary>
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// Result of license server accessibility test
        /// </summary>
        public class LicenseServerAccessibilityResult
        {
            /// <summary>
            /// Gets or sets whether the server is accessible
            /// </summary>
            public bool IsAccessible { get; set; }

            /// <summary>
            /// Gets or sets the error message if not accessible
            /// </summary>
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// Result of license manager functionality test
        /// </summary>
        public class LicenseManagerFunctionalityResult
        {
            /// <summary>
            /// Gets or sets whether the license manager is functional
            /// </summary>
            public bool IsFunctional { get; set; }

            /// <summary>
            /// Gets or sets the error message if not functional
            /// </summary>
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// Comprehensive result of version configuration validation
        /// </summary>
        public class ComprehensiveVersionValidationResult
        {
            /// <summary>
            /// Gets or sets the version that was validated
            /// </summary>
            public string Version { get; set; }

            /// <summary>
            /// Gets or sets the list of validation errors
            /// </summary>
            public List<string> Errors { get; set; }

            /// <summary>
            /// Gets or sets the list of validation warnings
            /// </summary>
            public List<string> Warnings { get; set; }

            /// <summary>
            /// Gets or sets the list of recommendations
            /// </summary>
            public List<string> Recommendations { get; set; }

            /// <summary>
            /// Gets or sets the overall validation score (0-100)
            /// </summary>
            public int ValidationScore { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether basic configuration is valid
            /// </summary>
            public bool BasicConfigurationValid { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether feature mappings are valid
            /// </summary>
            public bool FeatureMappingsValid { get; set; }

            /// <summary>
            /// Gets or sets the feature mapping health score (0.0-1.0)
            /// </summary>
            public double FeatureMappingHealth { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether network connectivity is valid
            /// </summary>
            public bool NetworkConnectivityValid { get; set; }

            /// <summary>
            /// Gets or sets the network latency in milliseconds
            /// </summary>
            public int NetworkLatencyMs { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether license server is accessible
            /// </summary>
            public bool LicenseServerAccessible { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether license manager is functional
            /// </summary>
            public bool LicenseManagerFunctional { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether configuration is consistent
            /// </summary>
            public bool ConfigurationConsistent { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether performance characteristics are valid
            /// </summary>
            public bool PerformanceCharacteristicsValid { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether security settings are valid
            /// </summary>
            public bool SecuritySettingsValid { get; set; }

            /// <summary>
            /// Gets a value indicating whether validation passed
            /// </summary>
            public bool IsValid => Errors.Count == 0;

            /// <summary>
            /// Gets a value indicating whether validation has any issues
            /// </summary>
            public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

            /// <summary>
            /// Initializes a new instance of the ComprehensiveVersionValidationResult class
            /// </summary>
            public ComprehensiveVersionValidationResult()
            {
                Errors = new List<string>();
                Warnings = new List<string>();
                Recommendations = new List<string>();
                ValidationScore = 100;
                FeatureMappingHealth = 1.0;
            }

            /// <summary>
            /// Gets a summary string of the validation result
            /// </summary>
            /// <returns>Summary string</returns>
            public string GetSummary()
            {
                var summary = new System.Text.StringBuilder();
                summary.AppendLine($"Version {Version} Validation Summary:");
                summary.AppendLine($"  Overall Score: {ValidationScore}/100 ({(IsValid ? "Valid" : "Invalid")})");
                summary.AppendLine($"  Errors: {Errors.Count}, Warnings: {Warnings.Count}, Recommendations: {Recommendations.Count}");
                summary.AppendLine($"  Basic Configuration: {(BasicConfigurationValid ? "Valid" : "Invalid")}");
                summary.AppendLine($"  Feature Mappings: {(FeatureMappingsValid ? "Valid" : "Invalid")} (Health: {FeatureMappingHealth:P0})");
                summary.AppendLine($"  Network Connectivity: {(NetworkConnectivityValid ? "Valid" : "Invalid")} (Latency: {NetworkLatencyMs}ms)");
                summary.AppendLine($"  License Server: {(LicenseServerAccessible ? "Accessible" : "Inaccessible")}");
                summary.AppendLine($"  Configuration Consistency: {(ConfigurationConsistent ? "Consistent" : "Inconsistent")}");
                summary.AppendLine($"  Performance: {(PerformanceCharacteristicsValid ? "Valid" : "Invalid")}");
                summary.AppendLine($"  Security: {(SecuritySettingsValid ? "Valid" : "Invalid")}");

                if (HasIssues)
                {
                    summary.AppendLine();
                    summary.AppendLine("Issues:");
                    foreach (var error in Errors.Take(3))
                    {
                        summary.AppendLine($"  Error: {error}");
                    }
                    foreach (var warning in Warnings.Take(3))
                    {
                        summary.AppendLine($"  Warning: {warning}");
                    }
                    if (Errors.Count > 3 || Warnings.Count > 3)
                    {
                        summary.AppendLine($"  ... and {Errors.Count + Warnings.Count - 6} more issues");
                    }
                }

                return summary.ToString();
            }
        }
    }
}