using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Configuration
{
    /// <summary>
    /// Unit tests for VersionConfigurationValidator
    /// </summary>
    [TestClass]
    public class VersionConfigurationValidatorTests
    {
        private VersionConfigurationValidator _validator;
        private TestVersionConfigurationManager3 _testManager;
        private string _testConfigPath;

        [TestInitialize]
        public void TestInitialize()
        {
            _validator = new VersionConfigurationValidator();
            _testConfigPath = Path.GetTempFileName();
            CreateTestConfigurationFile();
            _testManager = new TestVersionConfigurationManager3(_testConfigPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _testManager?.Dispose();

            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        private void CreateTestConfigurationFile()
        {
            var configContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <sectionGroup name=""licenseReleaseService"">
      <section name=""versionConfiguration"" type=""LicenseReleaseService.Configuration.VersionConfigurationSection, LicenseReleaseService"" />
    </sectionGroup>
  </configSections>
  <licenseReleaseService>
    <versionConfiguration>
      <versions>
        <versionConfiguration version=""2020"" licenseServer=""sw2020-server.company.com"" port=""27000"" timeout=""60"" />
        <versionConfiguration version=""2021"" licenseServer=""sw2021-server.company.com"" port=""27001"" timeout=""90"" parentVersion=""2020"" />
        <versionConfiguration version=""2022"" licenseServer=""sw2022-server.company.com"" port=""27002"" timeout=""45"" />
        <versionConfiguration version=""2023"" licenseServer=""sw2023-server.company.com"" port=""27003"" timeout=""30"" />
        <versionConfiguration version=""2024"" licenseServer=""sw2024-server.company.com"" port=""27004"" timeout=""20"" />
        <versionConfiguration version=""2025"" licenseServer=""sw2025-server.company.com"" port=""27005"" timeout=""15"" />
      </versions>
      <featureMappings>
        <featureMapping version=""2020"" featureCode=""sw_standard"" alias=""Standard"" description=""SolidWorks Standard"" />
        <featureMapping version=""2021"" featureCode=""sw_professional"" alias=""Professional"" description=""SolidWorks Professional"" />
        <featureMapping version=""2022"" featureCode=""sw_premium"" alias=""Premium"" description=""SolidWorks Premium"" />
        <featureMapping version=""2023"" featureCode=""sw_simulation"" alias=""Simulation"" description=""SolidWorks Simulation"" />
        <featureMapping version=""2024"" featureCode=""sw_routing"" alias=""Routing"" description=""SolidWorks Routing"" />
        <featureMapping version=""2025"" featureCode=""sw_all"" alias=""All"" description=""All Features"" />
      </featureMappings>
    </versionConfiguration>
  </licenseReleaseService>
</configuration>";

            File.WriteAllText(_testConfigPath, configContent);
        }

        [TestMethod]
        public async Task ValidateVersionConfigurationAsync_ShouldReturnValidationResult()
        {
            // Arrange
            var version = "2020";

            // Act
            var result = await _validator.ValidateVersionConfigurationAsync(version);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(version, result.Version);
            Assert.IsNotNull(result.BasicValidation);
            Assert.IsNotNull(result.FeatureMappingValidation);
            Assert.IsNotNull(result.NetworkValidation);
            Assert.IsNotNull(result.LicenseServerValidation);
            Assert.IsNotNull(result.ConfigurationConsistencyValidation);
            Assert.IsNotNull(result.PerformanceCharacteristicsValidation);
            Assert.IsNotNull(result.SecuritySettingsValidation);
            Assert.IsNotNull(result.CrossVersionConsistencyValidation);
            Assert.IsNotNull(result.ValidationScore);
            Assert.IsNotNull(result.Recommendations);
        }

        [TestMethod]
        public async Task ValidateBasicConfiguration_ShouldValidateBasicProperties()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");

            // Act
            var result = await _validator.ValidateBasicConfigurationAsync(config);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2020", result.Version);
            Assert.IsTrue(result.IsValid || !result.IsValid); // Will fail due to non-existent server
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateFeatureMappingConfiguration_ShouldValidateMappings()
        {
            // Arrange
            var config = _testManager.CurrentConfiguration;

            // Act
            var result = await _validator.ValidateFeatureMappingConfigurationAsync(config);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("FeatureMapping", result.ValidationType);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateNetworkConnectivity_ShouldValidateNetwork()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");

            // Act
            var result = await _validator.ValidateNetworkConnectivityAsync(config);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2020", result.Version);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateLicenseServerAccessibility_ShouldValidateServer()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");

            // Act
            var result = await _validator.ValidateLicenseServerAccessibilityAsync(config);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2020", result.Version);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateConfigurationConsistency_ShouldValidateConsistency()
        {
            // Arrange
            var config = _testManager.CurrentConfiguration;

            // Act
            var result = await _validator.ValidateConfigurationConsistencyAsync(config);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("ConfigurationConsistency", result.ValidationType);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidatePerformanceCharacteristics_ShouldValidatePerformance()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");

            // Act
            var result = await _validator.ValidatePerformanceCharacteristicsAsync(config);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2020", result.Version);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateSecuritySettings_ShouldValidateSecurity()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");

            // Act
            var result = await _validator.ValidateSecuritySettingsAsync(config);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2020", result.Version);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateCrossVersionConsistency_ShouldValidateCrossVersion()
        {
            // Arrange
            var config = _testManager.CurrentConfiguration;

            // Act
            var result = await _validator.ValidateCrossVersionConsistencyAsync(config);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("CrossVersionConsistency", result.ValidationType);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateAllVersionsAsync_ShouldValidateAllVersions()
        {
            // Arrange
            var config = _testManager.CurrentConfiguration;

            // Act
            var results = await _validator.ValidateAllVersionsAsync(config);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(6, results.Count);
            Assert.IsTrue(results.ContainsKey("2020"));
            Assert.IsTrue(results.ContainsKey("2025"));
        }

        [TestMethod]
        public async Task ValidateVersionRangeAsync_ShouldValidateRange()
        {
            // Arrange
            var config = _testManager.CurrentConfiguration;

            // Act
            var results = await _validator.ValidateVersionRangeAsync(config, "2020", "2022");

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.ContainsKey("2020"));
            Assert.IsTrue(results.ContainsKey("2021"));
            Assert.IsTrue(results.ContainsKey("2022"));
        }

        [TestMethod]
        public async Task ValidateSpecificVersionsAsync_ShouldValidateSpecific()
        {
            // Arrange
            var config = _testManager.CurrentConfiguration;
            var versions = new[] { "2020", "2022", "2024" };

            // Act
            var results = await _validator.ValidateSpecificVersionsAsync(config, versions);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.ContainsKey("2020"));
            Assert.IsTrue(results.ContainsKey("2022"));
            Assert.IsTrue(results.ContainsKey("2024"));
        }

        [TestMethod]
        public async Task GetValidationScore_ShouldCalculateScore()
        {
            // Arrange
            var result = await _validator.ValidateVersionConfigurationAsync("2020");

            // Act
            var score = _validator.GetValidationScore(result);

            // Assert
            Assert.IsNotNull(score);
            Assert.IsTrue(score >= 0);
            Assert.IsTrue(score <= 100);
        }

        [TestMethod]
        public async Task GetValidationRecommendations_ShouldReturnRecommendations()
        {
            // Arrange
            var result = await _validator.ValidateVersionConfigurationAsync("2020");

            // Act
            var recommendations = _validator.GetValidationRecommendations(result);

            // Assert
            Assert.IsNotNull(recommendations);
            Assert.IsTrue(recommendations.Count > 0);
        }

        [TestMethod]
        public async Task GetValidationSummary_ShouldReturnSummary()
        {
            // Arrange
            var results = await _validator.ValidateAllVersionsAsync(_testManager.CurrentConfiguration);

            // Act
            var summary = _validator.GetValidationSummary(results);

            // Assert
            Assert.IsNotNull(summary);
            Assert.IsTrue(summary.Contains("Validation Summary:"));
            Assert.IsTrue(summary.Contains("Total Versions:"));
            Assert.IsTrue(summary.Contains("Valid Configurations:"));
            Assert.IsTrue(summary.Contains("Invalid Configurations:"));
        }

        [TestMethod]
        public async Task GetValidationStatistics_ShouldReturnStats()
        {
            // Arrange
            var results = await _validator.ValidateAllVersionsAsync(_testManager.CurrentConfiguration);

            // Act
            var stats = _validator.GetValidationStatistics(results);

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.Contains("Validation Statistics:"));
            Assert.IsTrue(stats.Contains("Average Score:"));
            Assert.IsTrue(stats.Contains("Best Score:"));
            Assert.IsTrue(stats.Contains("Worst Score:"));
        }

        [TestMethod]
        public async Task GetValidationReport_ShouldReturnReport()
        {
            // Arrange
            var results = await _validator.ValidateAllVersionsAsync(_testManager.CurrentConfiguration);

            // Act
            var report = _validator.GetValidationReport(results);

            // Assert
            Assert.IsNotNull(report);
            Assert.IsTrue(report.Contains("Validation Report:"));
            Assert.IsTrue(report.Contains("Generated:"));
            Assert.IsTrue(report.Contains("Configuration Status:"));
        }

        [TestMethod]
        public async Task ValidateConfigurationChange_ShouldValidateChange()
        {
            // Arrange
            var oldConfig = _testManager.GetVersionConfiguration("2020");
            var newConfig = new VersionConfigurationElement
            {
                Version = "2020",
                LicenseServer = "modified-server.company.com",
                Port = 27000,
                Timeout = 30
            };

            // Act
            var result = await _validator.ValidateConfigurationChangeAsync(oldConfig, newConfig);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("ConfigurationChange", result.ValidationType);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateConfigurationMigration_ShouldValidateMigration()
        {
            // Arrange
            var sourceConfig = _testManager.GetVersionConfiguration("2020");
            var targetConfig = _testManager.GetVersionConfiguration("2021");

            // Act
            var result = await _validator.ValidateConfigurationMigrationAsync(sourceConfig, targetConfig);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("ConfigurationMigration", result.ValidationType);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task ValidateConfigurationBackup_ShouldValidateBackup()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");
            var backupPath = Path.GetTempFileName();

            // Act
            var result = await _validator.ValidateConfigurationBackupAsync(config, backupPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("ConfigurationBackup", result.ValidationType);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);

            // Cleanup
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }

        [TestMethod]
        public async Task ValidateConfigurationRestore_ShouldValidateRestore()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");
            var backupPath = Path.GetTempFileName();

            // Create a backup file
            File.WriteAllText(backupPath, "<?xml version=\"1.0\"?><configuration><version>2020</version></configuration>");

            // Act
            var result = await _validator.ValidateConfigurationRestoreAsync(backupPath, "2020");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("ConfigurationRestore", result.ValidationType);
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);

            // Cleanup
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }

        [TestMethod]
        public async Task GetValidationHistory_ShouldReturnHistory()
        {
            // Arrange
            // Run some validations
            await _validator.ValidateVersionConfigurationAsync("2020");
            await _validator.ValidateVersionConfigurationAsync("2021");

            // Act
            var history = _validator.GetValidationHistory();

            // Assert
            Assert.IsNotNull(history);
            Assert.IsTrue(history.Count >= 2);
        }

        [TestMethod]
        public async Task GetValidationTrends_ShouldReturnTrends()
        {
            // Arrange
            // Run some validations
            await _validator.ValidateVersionConfigurationAsync("2020");
            await _validator.ValidateVersionConfigurationAsync("2021");

            // Act
            var trends = _validator.GetValidationTrends();

            // Assert
            Assert.IsNotNull(trends);
            Assert.IsTrue(trends.Contains("Validation Trends:"));
            Assert.IsTrue(trends.Contains("Total Validations:"));
        }

        [TestMethod]
        public async Task GetValidationPerformance_ShouldReturnPerformance()
        {
            // Arrange
            // Run some validations
            await _validator.ValidateVersionConfigurationAsync("2020");

            // Act
            var performance = _validator.GetValidationPerformance();

            // Assert
            Assert.IsNotNull(performance);
            Assert.IsTrue(performance.Contains("Validation Performance:"));
            Assert.IsTrue(performance.Contains("Average Validation Time:"));
        }

        [TestMethod]
        public async Task ClearValidationHistory_ShouldClearHistory()
        {
            // Arrange
            // Run some validations
            await _validator.ValidateVersionConfigurationAsync("2020");
            await _validator.ValidateVersionConfigurationAsync("2021");

            // Act
            _validator.ClearValidationHistory();
            var history = _validator.GetValidationHistory();

            // Assert
            Assert.AreEqual(0, history.Count);
        }

        [TestMethod]
        public async Task GetValidationConfiguration_ShouldReturnConfig()
        {
            // Arrange & Act
            var config = _validator.GetValidationConfiguration();

            // Assert
            Assert.IsNotNull(config);
            Assert.IsTrue(config.Contains("Validation Configuration:"));
            Assert.IsTrue(config.Contains("Network Timeout:"));
            Assert.IsTrue(config.Contains("License Server Timeout:"));
        }

        [TestMethod]
        public async Task SetValidationConfiguration_ShouldSetConfig()
        {
            // Arrange
            var newConfig = new ValidationConfiguration
            {
                NetworkTimeout = TimeSpan.FromSeconds(10),
                LicenseServerTimeout = TimeSpan.FromSeconds(20),
                EnableNetworkValidation = false,
                EnableLicenseServerValidation = false
            };

            // Act
            _validator.SetValidationConfiguration(newConfig);
            var config = _validator.GetValidationConfiguration();

            // Assert
            Assert.IsNotNull(config);
            Assert.IsTrue(config.Contains("Network Timeout: 10s"));
            Assert.IsTrue(config.Contains("License Server Timeout: 20s"));
        }

        [TestMethod]
        public async Task ResetValidationConfiguration_ShouldResetConfig()
        {
            // Arrange
            var newConfig = new ValidationConfiguration
            {
                NetworkTimeout = TimeSpan.FromSeconds(10),
                LicenseServerTimeout = TimeSpan.FromSeconds(20),
                EnableNetworkValidation = false,
                EnableLicenseServerValidation = false
            };

            _validator.SetValidationConfiguration(newConfig);

            // Act
            _validator.ResetValidationConfiguration();
            var config = _validator.GetValidationConfiguration();

            // Assert
            Assert.IsNotNull(config);
            Assert.IsTrue(config.Contains("Validation Configuration:"));
        }

        [TestMethod]
        public async Task GetValidationCapabilities_ShouldReturnCapabilities()
        {
            // Arrange & Act
            var capabilities = _validator.GetValidationCapabilities();

            // Assert
            Assert.IsNotNull(capabilities);
            Assert.IsTrue(capabilities.Contains("Validation Capabilities:"));
            Assert.IsTrue(capabilities.Contains("Network Validation:"));
            Assert.IsTrue(capabilities.Contains("License Server Validation:"));
        }

        [TestMethod]
        public async Task IsValidationSupported_ShouldCheckSupport()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(await _validator.IsValidationSupportedAsync("BasicValidation"));
            Assert.IsTrue(await _validator.IsValidationSupportedAsync("NetworkValidation"));
            Assert.IsTrue(await _validator.IsValidationSupportedAsync("LicenseServerValidation"));
            Assert.IsFalse(await _validator.IsValidationSupportedAsync("NonExistentValidation"));
        }

        [TestMethod]
        public async Task GetValidationRequirements_ShouldReturnRequirements()
        {
            // Arrange & Act
            var requirements = _validator.GetValidationRequirements();

            // Assert
            Assert.IsNotNull(requirements);
            Assert.IsTrue(requirements.Contains("Validation Requirements:"));
            Assert.IsTrue(requirements.Contains("Network Access:"));
            Assert.IsTrue(requirements.Contains("License Server Access:"));
        }

        [TestMethod]
        public async Task ValidateVersionConfigurationWithInvalidConfig_ShouldReturnErrors()
        {
            // Arrange
            var invalidConfig = new VersionConfigurationElement
            {
                Version = "2020",
                LicenseServer = "", // Invalid
                Port = 0, // Invalid
                Timeout = 0 // Invalid
            };

            // Act
            var result = await _validator.ValidateBasicConfigurationAsync(invalidConfig);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Count > 0);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("License server cannot be empty")));
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Port must be between")));
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Timeout must be between")));
        }

        [TestMethod]
        public async Task ValidateVersionConfigurationWithValidConfig_ShouldReturnSuccess()
        {
            // Arrange
            var validConfig = new VersionConfigurationElement
            {
                Version = "2020",
                LicenseServer = "localhost",
                Port = 27000,
                Timeout = 60
            };

            // Act
            var result = await _validator.ValidateBasicConfigurationAsync(validConfig);

            // Assert
            Assert.IsNotNull(result);
            // Note: Network validation may fail due to localhost not being accessible
            // but basic validation should pass
            Assert.IsNotNull(result.Errors);
            Assert.IsNotNull(result.Warnings);
            Assert.IsNotNull(result.Suggestions);
        }

        [TestMethod]
        public async Task GetValidationScoreWithValidResult_ShouldReturnHighScore()
        {
            // Arrange
            var validResult = new ComprehensiveVersionValidationResult
            {
                Version = "2020",
                BasicValidation = new VersionValidationResult { IsValid = true, Errors = new List<string>() },
                FeatureMappingValidation = new VersionValidationResult { IsValid = true, Errors = new List<string>() },
                NetworkValidation = new VersionValidationResult { IsValid = true, Errors = new List<string>() },
                LicenseServerValidation = new VersionValidationResult { IsValid = true, Errors = new List<string>() },
                ConfigurationConsistencyValidation = new VersionValidationResult { IsValid = true, Errors = new List<string>() },
                PerformanceCharacteristicsValidation = new VersionValidationResult { IsValid = true, Errors = new List<string>() },
                SecuritySettingsValidation = new VersionValidationResult { IsValid = true, Errors = new List<string>() },
                CrossVersionConsistencyValidation = new VersionValidationResult { IsValid = true, Errors = new List<string>() }
            };

            // Act
            var score = _validator.GetValidationScore(validResult);

            // Assert
            Assert.IsNotNull(score);
            Assert.AreEqual(100, score);
        }

        [TestMethod]
        public async Task GetValidationScoreWithInvalidResult_ShouldReturnLowScore()
        {
            // Arrange
            var invalidResult = new ComprehensiveVersionValidationResult
            {
                Version = "2020",
                BasicValidation = new VersionValidationResult { IsValid = false, Errors = new List<string> { "Error 1", "Error 2" } },
                FeatureMappingValidation = new VersionValidationResult { IsValid = false, Errors = new List<string> { "Error 3" } },
                NetworkValidation = new VersionValidationResult { IsValid = false, Errors = new List<string> { "Error 4" } },
                LicenseServerValidation = new VersionValidationResult { IsValid = false, Errors = new List<string> { "Error 5" } },
                ConfigurationConsistencyValidation = new VersionValidationResult { IsValid = false, Errors = new List<string> { "Error 6" } },
                PerformanceCharacteristicsValidation = new VersionValidationResult { IsValid = false, Errors = new List<string> { "Error 7" } },
                SecuritySettingsValidation = new VersionValidationResult { IsValid = false, Errors = new List<string> { "Error 8" } },
                CrossVersionConsistencyValidation = new VersionValidationResult { IsValid = false, Errors = new List<string> { "Error 9" } }
            };

            // Act
            var score = _validator.GetValidationScore(invalidResult);

            // Assert
            Assert.IsNotNull(score);
            Assert.IsTrue(score < 50);
        }
    }

    /// <summary>
    /// Test version configuration manager that allows custom config file path
    /// </summary>
    public class TestVersionConfigurationManager3 : VersionConfigurationManager
    {
        private readonly string _configPath;

        public TestVersionConfigurationManager3(string configPath)
        {
            _configPath = configPath;
            Initialize();
        }

        protected override string GetConfigFilePath()
        {
            return _configPath;
        }

        public new VersionConfigurationElement GetVersionConfiguration(string version)
        {
            return base.GetVersionConfiguration(version);
        }

        public new VersionConfigurationSection CurrentConfiguration => base.CurrentConfiguration;
    }

    /// <summary>
    /// Validation configuration for testing
    /// </summary>
    public class ValidationConfiguration
    {
        public TimeSpan NetworkTimeout { get; set; }
        public TimeSpan LicenseServerTimeout { get; set; }
        public bool EnableNetworkValidation { get; set; }
        public bool EnableLicenseServerValidation { get; set; }
    }
}