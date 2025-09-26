using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Configuration
{
    /// <summary>
    /// Unit tests for VersionConfigurationManager
    /// </summary>
    [TestClass]
    public class VersionConfigurationManagerTests
    {
        private TestVersionConfigurationManager2 _testManager;
        private string _testConfigPath;

        [TestInitialize]
        public void TestInitialize()
        {
            // Create a test configuration file
            _testConfigPath = Path.GetTempFileName();
            CreateTestConfigurationFile();
            _testManager = new TestVersionConfigurationManager2(_testConfigPath);
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
        public void Constructor_ShouldInitializeManager()
        {
            // Arrange & Act
            using (var manager = new TestVersionConfigurationManager2(_testConfigPath))
            {
                // Assert
                Assert.IsNotNull(manager.CurrentConfiguration);
                Assert.IsNotNull(manager.ConfigurationCache);
                Assert.IsNotNull(manager.VersionHealthStatus);
                Assert.IsNotNull(manager.EventLog);
                Assert.AreEqual(_testConfigPath, manager.ConfigFilePath);
            }
        }

        [TestMethod]
        public void CurrentConfiguration_ShouldReturnValidConfiguration()
        {
            // Arrange & Act
            var config = _testManager.CurrentConfiguration;

            // Assert
            Assert.IsNotNull(config);
            Assert.IsNotNull(config.Versions);
            Assert.IsNotNull(config.FeatureMappings);
            Assert.AreEqual(6, config.Versions.Count);
            Assert.AreEqual(6, config.FeatureMappings.Count);
        }

        [TestMethod]
        public void GetVersionConfiguration_ShouldReturnCorrectConfig()
        {
            // Arrange & Act
            var config = _testManager.GetVersionConfiguration("2021");

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual("2021", config.Version);
            Assert.AreEqual("sw2021-server.company.com", config.LicenseServer);
            Assert.AreEqual(27001, config.Port);
            Assert.AreEqual(90, config.Timeout);
        }

        [TestMethod]
        public void GetVersionConfiguration_ShouldReturnNullForNonExistent()
        {
            // Arrange & Act
            var config = _testManager.GetVersionConfiguration("2019");

            // Assert
            Assert.IsNull(config);
        }

        [TestMethod]
        public void GetVersionConfigurationWithFallback_ShouldUseFallback()
        {
            // Arrange & Act
            var config = _testManager.GetVersionConfiguration("2019", "2020");

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual("2020", config.Version);
        }

        [TestMethod]
        public void GetSupportedVersions_ShouldReturnAllVersions()
        {
            // Arrange & Act
            var versions = _testManager.GetSupportedVersions();

            // Assert
            Assert.AreEqual(6, versions.Count);
            CollectionAssert.Contains(versions, "2020");
            CollectionAssert.Contains(versions, "2021");
            CollectionAssert.Contains(versions, "2022");
            CollectionAssert.Contains(versions, "2023");
            CollectionAssert.Contains(versions, "2024");
            CollectionAssert.Contains(versions, "2025");
        }

        [TestMethod]
        public void IsVersionSupported_ShouldCheckSupport()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(_testManager.IsVersionSupported("2020"));
            Assert.IsTrue(_testManager.IsVersionSupported("2025"));
            Assert.IsFalse(_testManager.IsVersionSupported("2019"));
            Assert.IsFalse(_testManager.IsVersionSupported("2026"));
        }

        [TestMethod]
        public void GetFeatureMappings_ShouldReturnMappingsForVersion()
        {
            // Arrange & Act
            var mappings = _testManager.GetFeatureMappings("2021");

            // Assert
            Assert.AreEqual(1, mappings.Count);
            Assert.AreEqual("sw_professional", mappings[0].FeatureCode);
            Assert.AreEqual("Professional", mappings[0].Alias);
        }

        [TestMethod]
        public void GetFeatureMappings_ShouldIncludeWildcards()
        {
            // Arrange - Add wildcard mapping
            var config = _testManager.CurrentConfiguration;
            config.FeatureMappings.Add(new FeatureMappingElement
            {
                Version = "*",
                FeatureCode = "sw_wildcard",
                Alias = "Wildcard"
            });

            // Act
            var mappings = _testManager.GetFeatureMappings("2021", includeWildcards: true);

            // Assert
            Assert.AreEqual(2, mappings.Count);
            Assert.IsTrue(mappings.Any(m => m.FeatureCode == "sw_professional"));
            Assert.IsTrue(mappings.Any(m => m.FeatureCode == "sw_wildcard"));
        }

        [TestMethod]
        public void ResolveFeatureCode_ShouldReturnCorrectCode()
        {
            // Arrange & Act
            var code = _testManager.ResolveFeatureCode("2021", "Professional");

            // Assert
            Assert.AreEqual("sw_professional", code);
        }

        [TestMethod]
        public void ResolveFeatureCode_ShouldReturnNullForNonExistent()
        {
            // Arrange & Act
            var code = _testManager.ResolveFeatureCode("2021", "NonExistent");

            // Assert
            Assert.IsNull(code);
        }

        [TestMethod]
        public void GetInheritanceResolvedConfigurations_ShouldResolveInheritance()
        {
            // Arrange & Act
            var resolved = _testManager.GetInheritanceResolvedConfigurations();

            // Assert
            Assert.AreEqual(6, resolved.Count);
            Assert.IsTrue(resolved.ContainsKey("2020"));
            Assert.IsTrue(resolved.ContainsKey("2021"));
            Assert.IsTrue(resolved.ContainsKey("2022"));
            Assert.IsTrue(resolved.ContainsKey("2023"));
            Assert.IsTrue(resolved.ContainsKey("2024"));
            Assert.IsTrue(resolved.ContainsKey("2025"));
        }

        [TestMethod]
        public void GetVersionHierarchy_ShouldReturnHierarchy()
        {
            // Arrange & Act
            var hierarchy = _testManager.GetVersionHierarchy();

            // Assert
            Assert.AreEqual(1, hierarchy.Count);
            Assert.IsTrue(hierarchy.ContainsKey("2020"));
            Assert.AreEqual(1, hierarchy["2020"].Count);
            Assert.AreEqual("2021", hierarchy["2020"][0]);
        }

        [TestMethod]
        public void GetCompatibilityMatrix_ShouldReturnMatrix()
        {
            // Arrange & Act
            var matrix = _testManager.GetCompatibilityMatrix();

            // Assert
            Assert.AreEqual(6, matrix.Count);
            Assert.IsTrue(matrix.ContainsKey("2020"));
            Assert.IsTrue(matrix.ContainsKey("2025"));
        }

        [TestMethod]
        public void GetConfigurationStatistics_ShouldReturnStats()
        {
            // Arrange & Act
            var stats = _testManager.GetConfigurationStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.Contains("Version Configuration Statistics:"));
            Assert.IsTrue(stats.Contains("Total Versions:"));
            Assert.IsTrue(stats.Contains("Total Feature Mappings:"));
            Assert.IsTrue(stats.Contains("Cache Size:"));
        }

        [TestMethod]
        public void RefreshConfiguration_ShouldUpdateConfiguration()
        {
            // Arrange
            var originalConfig = _testManager.CurrentConfiguration;

            // Modify the config file
            var modifiedConfigContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <sectionGroup name=""licenseReleaseService"">
      <section name=""versionConfiguration"" type=""LicenseReleaseService.Configuration.VersionConfigurationSection, LicenseReleaseService"" />
    </sectionGroup>
  </configSections>
  <licenseReleaseService>
    <versionConfiguration>
      <versions>
        <versionConfiguration version=""2020"" licenseServer=""modified-server.company.com"" port=""27000"" timeout=""60"" />
      </versions>
    </versionConfiguration>
  </licenseReleaseService>
</configuration>";

            File.WriteAllText(_testConfigPath, modifiedConfigContent);

            // Act
            _testManager.RefreshConfiguration();
            var newConfig = _testManager.CurrentConfiguration;

            // Assert
            Assert.AreNotEqual(originalConfig, newConfig);
            Assert.AreEqual("modified-server.company.com", newConfig.Versions[0].LicenseServer);
        }

        [TestMethod]
        public async Task RefreshConfigurationAsync_ShouldUpdateConfiguration()
        {
            // Arrange
            var originalConfig = _testManager.CurrentConfiguration;

            // Modify the config file
            var modifiedConfigContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <sectionGroup name=""licenseReleaseService"">
      <section name=""versionConfiguration"" type=""LicenseReleaseService.Configuration.VersionConfigurationSection, LicenseReleaseService"" />
    </sectionGroup>
  </configSections>
  <licenseReleaseService>
    <versionConfiguration>
      <versions>
        <versionConfiguration version=""2020"" licenseServer=""modified-server.company.com"" port=""27000"" timeout=""60"" />
      </versions>
    </versionConfiguration>
  </licenseReleaseService>
</configuration>";

            File.WriteAllText(_testConfigPath, modifiedConfigContent);

            // Act
            await _testManager.RefreshConfigurationAsync();
            var newConfig = _testManager.CurrentConfiguration;

            // Assert
            Assert.AreNotEqual(originalConfig, newConfig);
            Assert.AreEqual("modified-server.company.com", newConfig.Versions[0].LicenseServer);
        }

        [TestMethod]
        public void ClearCache_ShouldClearCache()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");

            // Act
            _testManager.ClearCache();

            // Assert - Should still work after cache clear
            var configAfterClear = _testManager.GetVersionConfiguration("2020");
            Assert.IsNotNull(configAfterClear);
        }

        [TestMethod]
        public void GetCacheStatistics_ShouldReturnCacheStats()
        {
            // Arrange
            var config = _testManager.GetVersionConfiguration("2020");

            // Act
            var stats = _testManager.GetCacheStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.Contains("Cache Size:"));
            Assert.IsTrue(stats.Contains("Cache Hit Rate:"));
        }

        [TestMethod]
        public void GetVersionHealthStatus_ShouldReturnHealthStatus()
        {
            // Arrange
            var version = "2020";

            // Act
            var health = _testManager.GetVersionHealthStatus(version);

            // Assert
            Assert.IsNotNull(health);
            Assert.AreEqual(version, health.Version);
            Assert.IsTrue(health.LastChecked > DateTime.MinValue);
        }

        [TestMethod]
        public async Task CheckVersionHealthAsync_ShouldUpdateHealthStatus()
        {
            // Arrange
            var version = "2020";

            // Act
            var health = await _testManager.CheckVersionHealthAsync(version);

            // Assert
            Assert.IsNotNull(health);
            Assert.AreEqual(version, health.Version);
            Assert.IsTrue(health.LastChecked > DateTime.MinValue);
        }

        [TestMethod]
        public void GetAllVersionHealthStatus_ShouldReturnAllHealthStatuses()
        {
            // Arrange & Act
            var allHealth = _testManager.GetAllVersionHealthStatus();

            // Assert
            Assert.AreEqual(6, allHealth.Count);
            Assert.IsTrue(allHealth.ContainsKey("2020"));
            Assert.IsTrue(allHealth.ContainsKey("2025"));
        }

        [TestMethod]
        public void GetHealthStatistics_ShouldReturnHealthStats()
        {
            // Arrange & Act
            var stats = _testManager.GetHealthStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.Contains("Health Statistics:"));
            Assert.IsTrue(stats.Contains("Healthy Versions:"));
            Assert.IsTrue(stats.Contains("Unhealthy Versions:"));
        }

        [TestMethod]
        public void ValidateConfiguration_ShouldReturnValidationResults()
        {
            // Arrange & Act
            var results = _testManager.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(6, results.Count);
            Assert.IsTrue(results.ContainsKey("2020"));
            Assert.IsTrue(results.ContainsKey("2025"));
        }

        [TestMethod]
        public async Task ValidateConfigurationAsync_ShouldReturnValidationResults()
        {
            // Arrange & Act
            var results = await _testManager.ValidateConfigurationAsync();

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(6, results.Count);
            Assert.IsTrue(results.ContainsKey("2020"));
            Assert.IsTrue(results.ContainsKey("2025"));
        }

        [TestMethod]
        public void GetValidationStatistics_ShouldReturnValidationStats()
        {
            // Arrange
            _testManager.ValidateConfiguration();

            // Act
            var stats = _testManager.GetValidationStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.Contains("Validation Statistics:"));
            Assert.IsTrue(stats.Contains("Valid Configurations:"));
            Assert.IsTrue(stats.Contains("Invalid Configurations:"));
        }

        [TestMethod]
        public void OptimizeConfiguration_ShouldOptimize()
        {
            // Arrange & Act
            var optimized = _testManager.OptimizeConfiguration();

            // Assert
            Assert.IsNotNull(optimized);
            Assert.IsTrue(optimized);
        }

        [TestMethod]
        public void ConfigurationChanged_Event_ShouldBeRaised()
        {
            // Arrange
            bool eventRaised = false;
            VersionConfigurationChangedEventArgs eventArgs = null;

            _testManager.ConfigurationChanged += (sender, e) =>
            {
                eventRaised = true;
                eventArgs = e;
            };

            // Act
            _testManager.RefreshConfiguration();

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.IsNotNull(eventArgs);
            Assert.IsNotNull(eventArgs.OldConfiguration);
            Assert.IsNotNull(eventArgs.NewConfiguration);
        }

        [TestMethod]
        public void VersionHealthChanged_Event_ShouldBeRaised()
        {
            // Arrange
            bool eventRaised = false;
            VersionHealthChangedEventArgs eventArgs = null;

            _testManager.VersionHealthChanged += (sender, e) =>
            {
                eventRaised = true;
                eventArgs = e;
            };

            // Act
            _testManager.CheckVersionHealthAsync("2020").Wait();

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.IsNotNull(eventArgs);
            Assert.IsNotNull(eventArgs.HealthStatus);
        }

        [TestMethod]
        public void ValidationCompleted_Event_ShouldBeRaised()
        {
            // Arrange
            bool eventRaised = false;
            ValidationCompletedEventArgs eventArgs = null;

            _testManager.ValidationCompleted += (sender, e) =>
            {
                eventRaised = true;
                eventArgs = e;
            };

            // Act
            _testManager.ValidateConfiguration();

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.IsNotNull(eventArgs);
            Assert.IsNotNull(eventArgs.ValidationResults);
        }

        [TestMethod]
        public void CacheCleared_Event_ShouldBeRaised()
        {
            // Arrange
            bool eventRaised = false;
            EventArgs eventArgs = null;

            _testManager.CacheCleared += (sender, e) =>
            {
                eventRaised = true;
                eventArgs = e;
            };

            // Act
            _testManager.ClearCache();

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.IsNotNull(eventArgs);
        }

        [TestMethod]
        public void GetEventLog_ShouldReturnEventLog()
        {
            // Arrange
            // Trigger some events
            _testManager.ClearCache();
            _testManager.ValidateConfiguration();

            // Act
            var log = _testManager.GetEventLog();

            // Assert
            Assert.IsNotNull(log);
            Assert.IsTrue(log.Count > 0);
        }

        [TestMethod]
        public void GetEventLogStatistics_ShouldReturnEventStats()
        {
            // Arrange
            // Trigger some events
            _testManager.ClearCache();
            _testManager.ValidateConfiguration();

            // Act
            var stats = _testManager.GetEventLogStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.Contains("Event Log Statistics:"));
            Assert.IsTrue(stats.Contains("Total Events:"));
        }

        [TestMethod]
        public void ClearEventLog_ShouldClearLog()
        {
            // Arrange
            // Trigger some events
            _testManager.ClearCache();
            _testManager.ValidateConfiguration();

            // Act
            _testManager.ClearEventLog();

            // Assert
            var log = _testManager.GetEventLog();
            Assert.AreEqual(0, log.Count);
        }

        [TestMethod]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var manager = new TestVersionConfigurationManager2(_testConfigPath);

            // Act
            manager.Dispose();

            // Assert - Should not throw exception
            Assert.ThrowsException<ObjectDisposedException>(() =>
            {
                var config = manager.CurrentConfiguration;
            });
        }

        [TestMethod]
        public async Task ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            // Act - Concurrent access
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var config = _testManager.GetVersionConfiguration("2020");
                        var mappings = _testManager.GetFeatureMappings("2020");
                        _testManager.ValidateConfiguration();
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void GetPerformanceMetrics_ShouldReturnMetrics()
        {
            // Arrange & Act
            var metrics = _testManager.GetPerformanceMetrics();

            // Assert
            Assert.IsNotNull(metrics);
            Assert.IsTrue(metrics.Contains("Performance Metrics:"));
            Assert.IsTrue(metrics.Contains("Cache Hit Rate:"));
            Assert.IsTrue(metrics.Contains("Average Response Time:"));
        }

        [TestMethod]
        public void ResetPerformanceMetrics_ShouldResetMetrics()
        {
            // Arrange
            var originalMetrics = _testManager.GetPerformanceMetrics();

            // Act
            _testManager.ResetPerformanceMetrics();
            var resetMetrics = _testManager.GetPerformanceMetrics();

            // Assert
            Assert.IsNotNull(resetMetrics);
            Assert.IsTrue(resetMetrics.Contains("Performance Metrics:"));
        }

        [TestMethod]
        public void GetDebugInformation_ShouldReturnDebugInfo()
        {
            // Arrange & Act
            var debugInfo = _testManager.GetDebugInformation();

            // Assert
            Assert.IsNotNull(debugInfo);
            Assert.IsTrue(debugInfo.Contains("Debug Information:"));
            Assert.IsTrue(debugInfo.Contains("Configuration Path:"));
            Assert.IsTrue(debugInfo.Contains("Cache Size:"));
            Assert.IsTrue(debugInfo.Contains("Health Status Count:"));
        }

        [TestMethod]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange & Act
            var toString = _testManager.ToString();

            // Assert
            Assert.IsNotNull(toString);
            Assert.IsTrue(toString.Contains("VersionConfigurationManager:"));
            Assert.IsTrue(toString.Contains("Configured Versions:"));
            Assert.IsTrue(toString.Contains("Cache Size:"));
        }
    }

    /// <summary>
    /// Test version configuration manager that allows custom config file path
    /// </summary>
    public class TestVersionConfigurationManager2 : VersionConfigurationManager
    {
        private readonly string _configPath;

        public TestVersionConfigurationManager2(string configPath)
        {
            _configPath = configPath;
            Initialize();
        }

        protected override string GetConfigFilePath()
        {
            return _configPath;
        }

        public new VersionConfigurationSection CurrentConfiguration => base.CurrentConfiguration;
        public new Dictionary<string, VersionConfigurationElement> ConfigurationCache => base.ConfigurationCache;
        public new Dictionary<string, VersionHealthStatus> VersionHealthStatus => base.VersionHealthStatus;
        public new List<ConfigurationEvent> EventLog => base.EventLog;
        public new string ConfigFilePath => base.ConfigFilePath;

        public new void ClearCache() => base.ClearCache();
        public new void ClearEventLog() => base.ClearEventLog();
        public new void RefreshConfiguration() => base.RefreshConfiguration();
        public new Task RefreshConfigurationAsync() => base.RefreshConfigurationAsync();
        public new void ResetPerformanceMetrics() => base.ResetPerformanceMetrics();
    }
}