using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Validator for version-specific timer configuration settings
    /// </summary>
    public class TimerVersionConfigurationValidator
    {
        private readonly TimerConfigurationProvider _configurationProvider;

        /// <summary>
        /// Initializes a new instance of the TimerVersionConfigurationValidator class
        /// </summary>
        /// <param name="configurationProvider">The configuration provider instance</param>
        public TimerVersionConfigurationValidator(TimerConfigurationProvider configurationProvider)
        {
            _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        }

        /// <summary>
        /// Validates the current version-specific configuration
        /// </summary>
        /// <returns>Validation result with errors and warnings</returns>
        public TimerVersionValidationResult ValidateConfiguration()
        {
            var result = new TimerVersionValidationResult();
            var config = _configurationProvider.CurrentConfiguration;

            // Validate basic version configuration
            ValidateVersionConfiguration(config, result);

            // Validate version detection settings
            ValidateVersionDetectionSettings(config, result);

            // Validate supported versions
            ValidateSupportedVersions(config, result);

            // Validate version fallback settings
            ValidateFallbackSettings(config, result);

            // Validate detected versions
            ValidateDetectedVersions(result);

            // Validate version-specific overrides
            ValidateVersionSpecificOverrides(result);

            return result;
        }

        /// <summary>
        /// Validates version-specific configuration settings
        /// </summary>
        /// <param name="config">Timer configuration</param>
        /// <param name="result">Validation result</param>
        private void ValidateVersionConfiguration(TimerConfigurationElement config, TimerVersionValidationResult result)
        {
            if (config.EnableVersionSpecificConfig)
            {
                // Validate that at least one version is specified
                if (string.IsNullOrEmpty(config.TargetVersion) && string.IsNullOrEmpty(config.DefaultVersion))
                {
                    result.Errors.Add("Either targetVersion or defaultVersion must be specified when version-specific configuration is enabled");
                }

                // Validate target version format
                if (!string.IsNullOrEmpty(config.TargetVersion))
                {
                    ValidateVersionFormat(config.TargetVersion, "targetVersion", result);
                }

                // Validate default version format
                if (!string.IsNullOrEmpty(config.DefaultVersion))
                {
                    ValidateVersionFormat(config.DefaultVersion, "defaultVersion", result);
                }

                // Validate that both versions are not the same (unless appropriate)
                if (!string.IsNullOrEmpty(config.TargetVersion) && !string.IsNullOrEmpty(config.DefaultVersion) &&
                    config.TargetVersion == config.DefaultVersion)
                {
                    result.Warnings.Add("targetVersion and defaultVersion are the same. Consider using only targetVersion for clarity.");
                }
            }
            else
            {
                // Validate that version-specific settings are not used when disabled
                if (!string.IsNullOrEmpty(config.TargetVersion))
                {
                    result.Warnings.Add("targetVersion is specified but version-specific configuration is disabled");
                }

                if (!string.IsNullOrEmpty(config.DefaultVersion))
                {
                    result.Warnings.Add("defaultVersion is specified but version-specific configuration is disabled");
                }

                if (!string.IsNullOrEmpty(config.SupportedVersions))
                {
                    result.Warnings.Add("supportedVersions is specified but version-specific configuration is disabled");
                }
            }
        }

        /// <summary>
        /// Validates version detection settings
        /// </summary>
        /// <param name="config">Timer configuration</param>
        /// <param name="result">Validation result</param>
        private void ValidateVersionDetectionSettings(TimerConfigurationElement config, TimerVersionValidationResult result)
        {
            if (config.EnableVersionSpecificConfig)
            {
                if (config.VersionDetectionInterval < TimeSpan.FromMinutes(1))
                {
                    result.Errors.Add("Version detection interval must be at least 1 minute");
                }

                if (config.VersionDetectionInterval > TimeSpan.FromHours(24))
                {
                    result.Warnings.Add("Version detection interval exceeds 24 hours. Consider using a shorter interval for better responsiveness.");
                }

                if (config.VersionDetectionInterval > TimeSpan.FromHours(6))
                {
                    result.Warnings.Add("Version detection interval is quite long. This may delay detection of version changes.");
                }
            }
        }

        /// <summary>
        /// Validates supported versions configuration
        /// </summary>
        /// <param name="config">Timer configuration</param>
        /// <param name="result">Validation result</param>
        private void ValidateSupportedVersions(TimerConfigurationElement config, TimerVersionValidationResult result)
        {
            if (!config.EnableVersionSpecificConfig)
                return;

            var supportedVersions = ParseSupportedVersions(config.SupportedVersions);

            if (supportedVersions.Count == 0)
            {
                result.Warnings.Add("No supported versions specified. Version detection will use all detected versions.");
            }
            else
            {
                // Validate each supported version
                foreach (var version in supportedVersions)
                {
                    ValidateVersionFormat(version, "supportedVersions", result);
                }

                // Validate target version is in supported list
                if (!string.IsNullOrEmpty(config.TargetVersion) && !supportedVersions.Contains(config.TargetVersion))
                {
                    result.Errors.Add($"Target version '{config.TargetVersion}' is not in the list of supported versions");
                }

                // Validate default version is in supported list
                if (!string.IsNullOrEmpty(config.DefaultVersion) && !supportedVersions.Contains(config.DefaultVersion))
                {
                    result.Errors.Add($"Default version '{config.DefaultVersion}' is not in the list of supported versions");
                }

                // Check for too many supported versions
                if (supportedVersions.Count > 10)
                {
                    result.Warnings.Add($"Large number of supported versions ({supportedVersions.Count}). Consider limiting to commonly used versions.");
                }
            }
        }

        /// <summary>
        /// Validates fallback settings
        /// </summary>
        /// <param name="config">Timer configuration</param>
        /// <param name="result">Validation result</param>
        private void ValidateFallbackSettings(TimerConfigurationElement config, TimerVersionValidationResult result)
        {
            if (!config.EnableVersionSpecificConfig)
                return;

            if (config.EnableVersionFallback)
            {
                if (string.IsNullOrEmpty(config.DefaultVersion))
                {
                    result.Warnings.Add("Version fallback is enabled but no default version is specified. Fallback will use base configuration.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(config.TargetVersion))
                {
                    result.Warnings.Add("Version fallback is disabled and no target version is specified. This may result in no version being selected.");
                }
            }
        }

        /// <summary>
        /// Validates detected versions
        /// </summary>
        /// <param name="result">Validation result</param>
        private void ValidateDetectedVersions(TimerVersionValidationResult result)
        {
            var detectedVersions = _configurationProvider.DetectedVersions;

            if (detectedVersions.Count == 0)
            {
                result.Warnings.Add("No SolidWorks versions detected. Version-specific features may not work correctly.");
                return;
            }

            // Check for very old versions
            var veryOldVersions = detectedVersions.Where(v => int.Parse(v) < 2018).ToList();
            if (veryOldVersions.Count > 0)
            {
                result.Warnings.Add($"Very old SolidWorks versions detected: {string.Join(", ", veryOldVersions)}. Support may be limited.");
            }

            // Check for future versions
            var futureVersions = detectedVersions.Where(v => int.Parse(v) > DateTime.Now.Year + 2).ToList();
            if (futureVersions.Count > 0)
            {
                result.Warnings.Add($"Future SolidWorks versions detected: {string.Join(", ", futureVersions)}. These may not be fully supported.");
            }
        }

        /// <summary>
        /// Validates version-specific overrides
        /// </summary>
        /// <param name="result">Validation result</param>
        private void ValidateVersionSpecificOverrides(TimerVersionValidationResult result)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;

                // Validate each version's override settings
                foreach (var version in _configurationProvider.SupportedVersions)
                {
                    ValidateVersionOverrides(version, appSettings, result);
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error validating version-specific overrides: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates overrides for a specific version
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="appSettings">Application settings</param>
        /// <param name="result">Validation result</param>
        private void ValidateVersionOverrides(string version, System.Collections.Specialized.NameValueCollection appSettings, TimerVersionValidationResult result)
        {
            var prefix = $"SW{version}_";

            // Check for timing overrides and validate ranges
            var intervalKey = $"{prefix}TimerDefaultInterval";
            if (appSettings[intervalKey] != null)
            {
                if (int.TryParse(appSettings[intervalKey], out int intervalSeconds))
                {
                    if (intervalSeconds < 10)
                    {
                        result.Warnings.Add($"Version {version} default interval ({intervalSeconds}s) is very short and may cause performance issues.");
                    }
                    if (intervalSeconds > 3600)
                    {
                        result.Warnings.Add($"Version {version} default interval ({intervalSeconds}s) is very long and may reduce responsiveness.");
                    }
                }
                else
                {
                    result.Errors.Add($"Invalid interval format for version {version}: {appSettings[intervalKey]}");
                }
            }

            var timeoutKey = $"{prefix}TimerExecutionTimeout";
            if (appSettings[timeoutKey] != null)
            {
                if (int.TryParse(appSettings[timeoutKey], out int timeoutSeconds))
                {
                    if (timeoutSeconds < 30)
                    {
                        result.Warnings.Add($"Version {version} execution timeout ({timeoutSeconds}s) is very short and may cause timeouts.");
                    }
                    if (timeoutSeconds > 1800)
                    {
                        result.Warnings.Add($"Version {version} execution timeout ({timeoutSeconds}s) is very long and may cause unresponsive behavior.");
                    }
                }
                else
                {
                    result.Errors.Add($"Invalid timeout format for version {version}: {appSettings[timeoutKey]}");
                }
            }

            var cooldownKey = $"{prefix}TimerCircuitBreakerCooldown";
            if (appSettings[cooldownKey] != null)
            {
                if (int.TryParse(appSettings[cooldownKey], out int cooldownSeconds))
                {
                    if (cooldownSeconds < 60)
                    {
                        result.Warnings.Add($"Version {version} circuit breaker cooldown ({cooldownSeconds}s) is very short and may cause frequent tripping.");
                    }
                    if (cooldownSeconds > 3600)
                    {
                        result.Warnings.Add($"Version {version} circuit breaker cooldown ({cooldownSeconds}s) is very long and may delay recovery.");
                    }
                }
                else
                {
                    result.Errors.Add($"Invalid cooldown format for version {version}: {appSettings[cooldownKey]}");
                }
            }

            var historyKey = $"{prefix}TimerMaxExecutionHistory";
            if (appSettings[historyKey] != null)
            {
                if (int.TryParse(appSettings[historyKey], out int historySize))
                {
                    if (historySize < 10)
                    {
                        result.Warnings.Add($"Version {version} max execution history ({historySize}) is very small and may limit debugging.");
                    }
                    if (historySize > 1000)
                    {
                        result.Warnings.Add($"Version {version} max execution history ({historySize}) is very large and may consume excessive memory.");
                    }
                }
                else
                {
                    result.Errors.Add($"Invalid history size format for version {version}: {appSettings[historyKey]}");
                }
            }

            var errorsKey = $"{prefix}TimerMaxConsecutiveErrors";
            if (appSettings[errorsKey] != null)
            {
                if (int.TryParse(appSettings[errorsKey], out int maxErrors))
                {
                    if (maxErrors < 1)
                    {
                        result.Errors.Add($"Version {version} max consecutive errors ({maxErrors}) must be at least 1.");
                    }
                    if (maxErrors > 20)
                    {
                        result.Warnings.Add($"Version {version} max consecutive errors ({maxErrors}) is very high and may delay circuit breaker activation.");
                    }
                }
                else
                {
                    result.Errors.Add($"Invalid max errors format for version {version}: {appSettings[errorsKey]}");
                }
            }
        }

        /// <summary>
        /// Validates version format
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="propertyName">Property name for error messages</param>
        /// <param name="result">Validation result</param>
        private void ValidateVersionFormat(string version, string propertyName, TimerVersionValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                result.Errors.Add($"{propertyName} cannot be empty");
                return;
            }

            // Validate version format (YYYY pattern for years)
            if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^20[0-9]{2}$"))
            {
                result.Errors.Add($"Invalid {propertyName} format: {version}. Version must be in YYYY format (e.g., 2023, 2024)");
                return;
            }

            // Validate version is within supported range
            if (int.TryParse(version, out int year))
            {
                if (year < 2000 || year > 2030)
                {
                    result.Warnings.Add($"{propertyName} {version} is outside the recommended range (2000-2030)");
                }
            }
            else
            {
                result.Errors.Add($"Invalid {propertyName}: {version} is not a valid year");
            }
        }

        /// <summary>
        /// Parses supported versions string into a list
        /// </summary>
        /// <param name="supportedVersions">Comma-separated versions string</param>
        /// <returns>List of supported versions</returns>
        private List<string> ParseSupportedVersions(string supportedVersions)
        {
            if (string.IsNullOrWhiteSpace(supportedVersions))
                return new List<string>();

            return supportedVersions.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();
        }
    }
}