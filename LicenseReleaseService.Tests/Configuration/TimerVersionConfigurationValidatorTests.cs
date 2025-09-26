using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.TimerExecution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LicenseReleaseService.Tests.Configuration
{
    [TestClass]
    public class TimerVersionConfigurationValidatorTests
    {
        private TimerConfigurationProvider _configurationProvider;
        private TimerVersionConfigurationValidator _validator;
        private TestConfigurationManager _testConfigManager;

        [TestInitialize]
        public void TestInitialize()
        {
            _testConfigManager = new TestConfigurationManager();
            _configurationProvider = new TimerConfigurationProvider(_testConfigManager);
            _validator = new TimerVersionConfigurationValidator(_configurationProvider);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _configurationProvider?.Dispose();
        }

        [TestMethod]
        public void ValidateConfiguration_WithValidVersionSpecificConfig_ShouldReturnValidResult()
        {
            // Arrange
            SetupValidVersionSpecificConfig();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid, "Configuration should be valid");
            Assert.AreEqual(0, result.Errors.Count, "Should have no errors");
            Assert.IsTrue(result.ValidationTime > DateTime.MinValue, "Validation time should be set");
        }

        [TestMethod]
        public void ValidateConfiguration_WithDisabledVersionSpecificConfig_ShouldReturnValidResult()
        {
            // Arrange
            SetupDisabledVersionSpecificConfig();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid, "Configuration should be valid when version-specific config is disabled");
            Assert.AreEqual(0, result.Errors.Count, "Should have no errors");
        }

        [TestMethod]
        public void ValidateConfiguration_WithMissingTargetAndDefaultVersions_ShouldReturnError()
        {
            // Arrange
            SetupVersionSpecificConfigWithoutTargetOrDefault();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("targetVersion or defaultVersion must be specified")),
                "Should have error about missing versions");
        }

        [TestMethod]
        public void ValidateConfiguration_WithInvalidTargetVersionFormat_ShouldReturnError()
        {
            // Arrange
            SetupInvalidTargetVersionFormat();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("targetVersion format") && e.Contains("YYYY format")),
                "Should have error about version format");
        }

        [TestMethod]
        public void ValidateConfiguration_WithVersionOutOfRange_ShouldReturnWarning()
        {
            // Arrange
            SetupOutOfRangeVersion();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid, "Configuration should be valid (version out of range is a warning)");
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("outside the recommended range")),
                "Should have warning about version range");
        }

        [TestMethod]
        public void ValidateConfiguration_WithTooShortDetectionInterval_ShouldReturnError()
        {
            // Arrange
            SetupTooShortDetectionInterval();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Version detection interval must be at least 1 minute")),
                "Should have error about detection interval");
        }

        [TestMethod]
        public void ValidateConfiguration_WithTooLongDetectionInterval_ShouldReturnWarning()
        {
            // Arrange
            SetupTooLongDetectionInterval();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid, "Configuration should be valid (long interval is a warning)");
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("exceeds 24 hours")),
                "Should have warning about long detection interval");
        }

        [TestMethod]
        public void ValidateConfiguration_WithTargetVersionNotInSupportedList_ShouldReturnError()
        {
            // Arrange
            SetupTargetVersionNotInSupportedList();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Target version '2026' is not in the list of supported versions")),
                "Should have error about target version not in supported list");
        }

        [TestMethod]
        public void ValidateConfiguration_WithVersionSpecificOverrides_ShouldValidateOverrideValues()
        {
            // Arrange
            SetupVersionSpecificOverrides();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid, "Configuration should be valid");

            // Should have warnings about very short intervals
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("very short and may cause performance issues")),
                "Should have warning about short intervals");
        }

        [TestMethod]
        public void ValidateConfiguration_WithInvalidOverrideFormat_ShouldReturnError()
        {
            // Arrange
            SetupInvalidOverrideFormat();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Invalid interval format for version 2024")),
                "Should have error about invalid override format");
        }

        [TestMethod]
        public void ValidateConfiguration_WithVersionSettingsWhenDisabled_ShouldReturnWarnings()
        {
            // Arrange
            SetupVersionSettingsWhenDisabled();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid, "Configuration should be valid");
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("targetVersion is specified but version-specific configuration is disabled")),
                "Should have warning about target version when disabled");
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("supportedVersions is specified but version-specific configuration is disabled")),
                "Should have warning about supported versions when disabled");
        }

        [TestMethod]
        public void ValidateConfiguration_WithNoSupportedVersions_ShouldReturnWarning()
        {
            // Arrange
            SetupNoSupportedVersions();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid, "Configuration should be valid");
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("No supported versions specified")),
                "Should have warning about no supported versions");
        }

        [TestMethod]
        public void ValidateConfiguration_WithTooManySupportedVersions_ShouldReturnWarning()
        {
            // Arrange
            SetupTooManySupportedVersions();

            // Act
            var result = _validator.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid, "Configuration should be valid");
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("Large number of supported versions")),
                "Should have warning about too many supported versions");
        }

        [TestMethod]
        public void GetSummary_ShouldReturnFormattedSummary()
        {
            // Arrange
            SetupValidVersionSpecificConfig();
            var result = _validator.ValidateConfiguration();

            // Act
            var summary = result.GetSummary();

            // Assert
            Assert.IsNotNull(summary);
            Assert.IsTrue(summary.Contains("Version Configuration Validation Summary"));
            Assert.IsTrue(summary.Contains("Total Messages:"));
            Assert.IsTrue(summary.Contains("Status: Valid"));
        }

        [TestMethod]
        public void GetStatusMessage_ShouldReturnAppropriateStatus()
        {
            // Arrange
            SetupValidVersionSpecificConfig();
            var validResult = _validator.ValidateConfiguration();

            SetupInvalidTargetVersionFormat();
            var invalidResult = _validator.ValidateConfiguration();

            // Act
            var validStatus = validResult.GetStatusMessage();
            var invalidStatus = invalidResult.GetStatusMessage();

            // Assert
            Assert.IsTrue(validStatus.Contains("Valid configuration"));
            Assert.IsTrue(invalidStatus.Contains("Invalid configuration"));
        }

        #region Helper Methods

        private void SetupValidVersionSpecificConfig()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30),
                EnableVersionFallback = true
            };
        }

        private void SetupDisabledVersionSpecificConfig()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = false,
                TargetVersion = "",
                DefaultVersion = "",
                SupportedVersions = ""
            };
        }

        private void SetupVersionSpecificConfigWithoutTargetOrDefault()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "",
                DefaultVersion = "",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30)
            };
        }

        private void SetupInvalidTargetVersionFormat()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "202a",
                DefaultVersion = "2024",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30)
            };
        }

        private void SetupOutOfRangeVersion()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "1999",
                DefaultVersion = "2024",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30)
            };
        }

        private void SetupTooShortDetectionInterval()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromSeconds(30)
            };
        }

        private void SetupTooLongDetectionInterval()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromHours(25)
            };
        }

        private void SetupTargetVersionNotInSupportedList()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "2026",
                DefaultVersion = "2024",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30)
            };
        }

        private void SetupVersionSpecificOverrides()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30)
            };

            // Add app settings for version overrides
            _testConfigManager.AppSettings["SW2024_TimerDefaultInterval"] = "5"; // Very short
            _testConfigManager.AppSettings["SW2024_TimerExecutionTimeout"] = "300";
            _testConfigManager.AppSettings["SW2024_TimerMaxConsecutiveErrors"] = "10"; // High
        }

        private void SetupInvalidOverrideFormat()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30)
            };

            _testConfigManager.AppSettings["SW2024_TimerDefaultInterval"] = "invalid";
        }

        private void SetupVersionSettingsWhenDisabled()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = false,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = "2023,2024,2025"
            };
        }

        private void SetupNoSupportedVersions()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = "",
                VersionDetectionInterval = TimeSpan.FromMinutes(30)
            };
        }

        private void SetupTooManySupportedVersions()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = "2020,2021,2022,2023,2024,2025,2026,2027,2028,2029,2030",
                VersionDetectionInterval = TimeSpan.FromMinutes(30)
            };
        }

        #endregion

        #region Test Configuration Manager

        private class TestConfigurationManager : ConfigurationManager
        {
            public TimerConfigurationElement TimerConfiguration { get; set; }
            public System.Collections.Specialized.NameValueCollection AppSettings { get; }

            public TestConfigurationManager()
            {
                TimerConfiguration = CreateDefaultTimerConfiguration();
                AppSettings = new System.Collections.Specialized.NameValueCollection();
            }

            private TimerConfigurationElement CreateDefaultTimerConfiguration()
            {
                return new TimerConfigurationElement
                {
                    DefaultInterval = TimeSpan.FromMinutes(1),
                    MaxConsecutiveErrors = 5,
                    MaxConcurrentExecutions = 1,
                    CircuitBreakerCooldown = TimeSpan.FromMinutes(5),
                    EnableAutoRestart = true,
                    EnableExecutionTimeout = true,
                    ExecutionTimeout = TimeSpan.FromMinutes(5),
                    EnableMetrics = true,
                    EnableDetailedLogging = true,
                    EnableCircuitBreaker = true,
                    MaxExecutionHistory = 100,
                    MinInterval = TimeSpan.FromSeconds(1),
                    MaxInterval = TimeSpan.FromDays(1),
                    SyncTimeout = TimeSpan.FromSeconds(30),
                    StopOnUnhandledException = false,
                    DisposalGracePeriod = TimeSpan.FromSeconds(5),
                    PreventExecutionOverlap = true,
                    StartupDelay = TimeSpan.FromSeconds(10),
                    ShutdownTimeout = TimeSpan.FromSeconds(30),
                    HealthCheckInterval = 60,
                    MetricsCollectionInterval = 30,
                    EnableAdaptiveScheduling = false,
                    AdaptiveThreshold = 80,
                    AdaptiveCooldown = TimeSpan.FromMinutes(5),
                    EnableMemoryMonitoring = true,
                    MemoryThreshold = 52428800,
                    EnableCpuMonitoring = true,
                    CpuThreshold = 80,
                    EnableThreadMonitoring = true,
                    MaxThreadPoolThreads = 10,
                    TargetVersion = "",
                    DefaultVersion = "",
                    EnableVersionSpecificConfig = false,
                    VersionDetectionInterval = TimeSpan.FromMinutes(30),
                    EnableVersionFallback = true,
                    SupportedVersions = ""
                };
            }

            public override TimerConfigurationElement GetTimerConfiguration()
            {
                return TimerConfiguration;
            }

            public override string GetAppSetting(string key)
            {
                return AppSettings[key];
            }
        }

        #endregion
    }
}