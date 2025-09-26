using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.Configuration
{
    [TestClass]
    public class TimerConfigurationProviderVersionTests
    {
        private TestConfigurationManager _testConfigManager;
        private TimerConfigurationProvider _configurationProvider;

        [TestInitialize]
        public void TestInitialize()
        {
            _testConfigManager = new TestConfigurationManager();
            _configurationProvider = new TimerConfigurationProvider(_testConfigManager);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _configurationProvider?.Dispose();
        }

        [TestMethod]
        public void IsVersionSpecificConfigEnabled_WithDisabledConfig_ShouldReturnFalse()
        {
            // Arrange
            SetupDisabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.IsVersionSpecificConfigEnabled;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsVersionSpecificConfigEnabled_WithEnabledConfig_ShouldReturnTrue()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.IsVersionSpecificConfigEnabled;

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TargetVersion_ShouldReturnConfiguredValue()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.TargetVersion;

            // Assert
            Assert.AreEqual("2024", result);
        }

        [TestMethod]
        public void DefaultVersion_ShouldReturnConfiguredValue()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.DefaultVersion;

            // Assert
            Assert.AreEqual("2023", result);
        }

        [TestMethod]
        public void SupportedVersions_ShouldReturnParsedList()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.SupportedVersions;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            CollectionAssert.Contains(result, "2023");
            CollectionAssert.Contains(result, "2024");
            CollectionAssert.Contains(result, "2025");
        }

        [TestMethod]
        public void GetVersionConfiguration_WithValidVersion_ShouldReturnConfiguration()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.GetVersionConfiguration("2024");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2024", result.TargetVersion);
        }

        [TestMethod]
        public void GetVersionConfiguration_WithNullVersion_ShouldThrowArgumentException()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _configurationProvider.GetVersionConfiguration(null));
        }

        [TestMethod]
        public void GetVersionConfiguration_WithEmptyVersion_ShouldThrowArgumentException()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _configurationProvider.GetVersionConfiguration(""));
        }

        [TestMethod]
        public void GetVersionConfiguration_ShouldCacheResults()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var result1 = _configurationProvider.GetVersionConfiguration("2024");
            var result2 = _configurationProvider.GetVersionConfiguration("2024");

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreSame(result1, result2, "Should return cached instance");
        }

        [TestMethod]
        public void GetActiveConfiguration_WithDisabledVersionSpecificConfig_ShouldReturnBaseConfiguration()
        {
            // Arrange
            SetupDisabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.GetActiveConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(string.Empty, result.TargetVersion);
        }

        [TestMethod]
        public void GetActiveConfiguration_WithEnabledConfigAndTargetVersionDetected_ShouldReturnTargetVersionConfig()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();
            _testConfigManager.DetectedVersions = new List<string> { "2023", "2024", "2025" };

            // Act
            var result = _configurationProvider.GetActiveConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2024", result.TargetVersion);
        }

        [TestMethod]
        public void GetActiveConfiguration_WithNoTargetVersionDetected_ShouldReturnLatestVersion()
        {
            // Arrange
            SetupEnabledVersionSpecificConfigNoTarget();
            _testConfigManager.DetectedVersions = new List<string> { "2023", "2024", "2025" };

            // Act
            var result = _configurationProvider.GetActiveConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2025", result.TargetVersion);
        }

        [TestMethod]
        public void GetActiveConfiguration_WithNoVersionsDetected_ShouldReturnFallbackConfiguration()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();
            _testConfigManager.DetectedVersions = new List<string>();

            // Act
            var result = _configurationProvider.GetActiveConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2023", result.TargetVersion);
        }

        [TestMethod]
        public void DetectVersions_ShouldPopulateDetectedVersions()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();
            _testConfigManager.DetectedVersions = new List<string> { "2023", "2024" };

            // Act
            _configurationProvider.ForceVersionDetection();
            var result = _configurationProvider.DetectedVersions;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            CollectionAssert.Contains(result, "2023");
            CollectionAssert.Contains(result, "2024");
        }

        [TestMethod]
        public void DetectVersions_ShouldClearVersionCacheWhenVersionsChange()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();
            _testConfigManager.DetectedVersions = new List<string> { "2023", "2024" };

            // Act - First detection
            _configurationProvider.ForceVersionDetection();
            var config1 = _configurationProvider.GetVersionConfiguration("2024");

            // Change versions
            _testConfigManager.DetectedVersions = new List<string> { "2024", "2025" };
            _configurationProvider.ForceVersionDetection();
            var config2 = _configurationProvider.GetVersionConfiguration("2024");

            // Assert
            Assert.IsNotNull(config1);
            Assert.IsNotNull(config2);
            Assert.AreNotSame(config1, config2, "Should return new instance after version change");
        }

        [TestMethod]
        public void GetVersionConfiguration_ShouldApplyVersionSpecificOverrides()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var config2024 = _configurationProvider.GetVersionConfiguration("2024");
            var config2025 = _configurationProvider.GetVersionConfiguration("2025");

            // Assert
            Assert.IsNotNull(config2024);
            Assert.IsNotNull(config2025);
            Assert.AreEqual("2024", config2024.TargetVersion);
            Assert.AreEqual("2025", config2025.TargetVersion);

            // Verify different timing based on version
            Assert.AreEqual(TimeSpan.FromMinutes(4), config2024.DefaultInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(3), config2025.DefaultInterval);
        }

        [TestMethod]
        public void GetVersionConfiguration_ShouldUseFallbackWhenVersionNotFound()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();
            _testConfigManager.DetectedVersions = new List<string> { "2023", "2025" };

            // Act
            var result = _configurationProvider.GetVersionConfiguration("2024");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("2023", result.TargetVersion);
        }

        [TestMethod]
        public void ValidateConfiguration_ShouldReturnValidationResult()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.ValidationTime > DateTime.MinValue);
        }

        [TestMethod]
        public void ValidateConfigurationStrict_WithValidConfig_ShouldNotThrow()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act & Assert
            _configurationProvider.ValidateConfigurationStrict();
        }

        [TestMethod]
        public void ValidateConfigurationStrict_WithInvalidConfig_ShouldThrowException()
        {
            // Arrange
            SetupInvalidVersionSpecificConfig();

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => _configurationProvider.ValidateConfigurationStrict());
        }

        [TestMethod]
        public void ValidateAndLogConfiguration_WithValidConfig_ShouldReturnTrue()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var result = _configurationProvider.ValidateAndLogConfiguration();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateAndLogConfiguration_WithInvalidConfig_ShouldReturnFalse()
        {
            // Arrange
            SetupInvalidVersionSpecificConfig();

            // Act
            var result = _configurationProvider.ValidateAndLogConfiguration();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GetValidationSummary_ShouldReturnFormattedSummary()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();

            // Act
            var summary = _configurationProvider.GetValidationSummary();

            // Assert
            Assert.IsNotNull(summary);
            Assert.IsTrue(summary.Contains("Version Configuration Validation Summary"));
        }

        [TestMethod]
        public void GetVersionStatistics_ShouldReturnCompleteStatistics()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();
            _testConfigManager.DetectedVersions = new List<string> { "2023", "2024", "2025" };

            // Act
            var stats = _configurationProvider.GetVersionStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsNotNull(stats.BaseStatistics);
            Assert.IsTrue(stats.IsVersionSpecificConfigEnabled);
            Assert.AreEqual("2024", stats.TargetVersion);
            Assert.AreEqual("2023", stats.DefaultVersion);
            Assert.AreEqual(3, stats.DetectedVersions.Count);
            Assert.AreEqual(3, stats.SupportedVersions.Count);
            Assert.AreEqual("2024", stats.ActiveVersion);
        }

        [TestMethod]
        public void Events_ShouldBeRaisedWhenVersionsDetected()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();
            _testConfigManager.DetectedVersions = new List<string> { "2023", "2024" };
            var eventRaised = false;

            _configurationProvider.VersionsDetected += (sender, args) =>
            {
                eventRaised = true;
                Assert.IsNotNull(args);
                Assert.IsTrue(args.DetectionTime > DateTime.MinValue);
            };

            // Act
            _configurationProvider.ForceVersionDetection();

            // Assert
            Assert.IsTrue(eventRaised, "VersionsDetected event should be raised");
        }

        [TestMethod]
        public void Events_ShouldBeRaisedWhenVersionConfigurationLoaded()
        {
            // Arrange
            SetupEnabledVersionSpecificConfig();
            var eventRaised = false;

            _configurationProvider.VersionConfigurationLoaded += (sender, args) =>
            {
                eventRaised = true;
                Assert.IsNotNull(args);
                Assert.AreEqual("2024", args.Version);
                Assert.IsNotNull(args.Configuration);
            };

            // Act
            _configurationProvider.GetVersionConfiguration("2024");

            // Assert
            Assert.IsTrue(eventRaised, "VersionConfigurationLoaded event should be raised");
        }

        #region Helper Methods

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

        private void SetupEnabledVersionSpecificConfig()
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

        private void SetupEnabledVersionSpecificConfigNoTarget()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "",
                DefaultVersion = "2023",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30),
                EnableVersionFallback = true
            };
        }

        private void SetupInvalidVersionSpecificConfig()
        {
            _testConfigManager.TimerConfiguration = new TimerConfigurationElement
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 5,
                EnableVersionSpecificConfig = true,
                TargetVersion = "invalid",
                DefaultVersion = "2023",
                SupportedVersions = "2023,2024,2025",
                VersionDetectionInterval = TimeSpan.FromMinutes(30),
                EnableVersionFallback = true
            };
        }

        #endregion

        #region Test Configuration Manager

        private class TestConfigurationManager : ConfigurationManager
        {
            public TimerConfigurationElement TimerConfiguration { get; set; }
            public List<string> DetectedVersions { get; set; }

            public TestConfigurationManager()
            {
                TimerConfiguration = CreateDefaultTimerConfiguration();
                DetectedVersions = new List<string>();
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
                // Mock app settings for version-specific overrides
                if (key == "SW2024_TimerDefaultInterval") return "240";
                if (key == "SW2025_TimerDefaultInterval") return "180";
                return null;
            }

            // Mock version detection
            public List<string> DetectAvailableVersions()
            {
                return new List<string>(DetectedVersions);
            }
        }

        #endregion
    }
}