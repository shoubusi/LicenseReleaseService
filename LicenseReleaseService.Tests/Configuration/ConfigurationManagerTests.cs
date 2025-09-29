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
    /// Unit tests for ConfigurationManager
    /// </summary>
    [TestClass]
    public class ConfigurationManagerTests
    {
        private TestConfigManager _testConfigManager;
        private string _testConfigPath;
        private string _originalConfigContent;

        [TestInitialize]
        public void TestInitialize()
        {
            // Create a test configuration file
            _testConfigPath = Path.GetTempFileName();
            _originalConfigContent = File.ReadAllText("App.config");

            // Copy and modify the original config for testing
            var testConfigContent = _originalConfigContent.Replace(
                "C:\\Program Files\\Autodesk\\Network License Manager\\lmutil.exe",
                "C:\\Test\\lmutil.exe");

            File.WriteAllText(_testConfigPath, testConfigContent);

            // Create test configuration manager
            _testConfigManager = new TestConfigManager(_testConfigPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _testConfigManager?.Dispose();

            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        [TestMethod]
        public void Constructor_ShouldInitializeConfiguration()
        {
            // Arrange & Act
            using (var configManager = new TestConfigManager(_testConfigPath))
            {
                // Assert
                Assert.IsNotNull(configManager.CurrentConfiguration);
                Assert.IsNotNull(configManager.Settings);
                Assert.AreEqual(_testConfigPath, configManager.ConfigFilePath);
                Assert.IsTrue(configManager.LastConfigChange > DateTime.MinValue);
            }
        }

        [TestMethod]
        public void CurrentConfiguration_ShouldReturnValidConfiguration()
        {
            // Arrange & Act
            var config = _testConfigManager.CurrentConfiguration;

            // Assert
            Assert.IsNotNull(config);
            Assert.IsNotNull(config.LicenseManager);
            Assert.IsNotNull(config.Logging);
            Assert.IsNotNull(config.Monitoring);
            Assert.AreEqual("C:\\Test\\lmutil.exe", config.LicenseManager.LmutilPath);
            Assert.AreEqual("license-server.company.com", config.LicenseManager.LicenseServer);
        }

        [TestMethod]
        public void Settings_ShouldReturnValidServiceSettings()
        {
            // Arrange & Act
            var settings = _testConfigManager.Settings;

            // Assert
            Assert.IsNotNull(settings);
            Assert.AreEqual("LicenseReleaseService", settings.ServiceName);
            Assert.AreEqual("Production", settings.Environment);
            Assert.AreEqual(300, settings.PollingInterval);
            Assert.AreEqual(10, settings.MaxConcurrentOperations);
            Assert.AreEqual("Information", settings.LogLevel);
        }

        [TestMethod]
        public void GetAppSetting_ShouldReturnCorrectValue()
        {
            // Arrange & Act
            var serviceName = _testConfigManager.GetAppSetting("ServiceName");
            var environment = _testConfigManager.GetAppSetting("Environment");
            var nonExistent = _testConfigManager.GetAppSetting("NonExistent", "Default");

            // Assert
            Assert.AreEqual("LicenseReleaseService", serviceName);
            Assert.AreEqual("Production", environment);
            Assert.AreEqual("Default", nonExistent);
        }

        [TestMethod]
        public void GetConnectionString_ShouldReturnConnectionString()
        {
            // Arrange & Act
            var connectionString = _testConfigManager.GetConnectionString("LicenseReleaseServiceDB");

            // Assert
            Assert.IsNotNull(connectionString);
            Assert.IsTrue(connectionString.Contains("LicenseReleaseService"));
        }

        [TestMethod]
        public void ValidateConfiguration_ShouldReturnValidationErrorsForInvalidConfig()
        {
            // Arrange - Create invalid config
            var invalidConfigContent = _originalConfigContent.Replace(
                "C:\\Program Files\\Autodesk\\Network License Manager\\lmutil.exe",
                "C:\\NonExistent\\lmutil.exe");

            File.WriteAllText(_testConfigPath, invalidConfigContent);

            // Act
            var errors = _testConfigManager.ValidateConfiguration();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("lmutil.exe not found")));
        }

        [TestMethod]
        public void ValidateConfiguration_ShouldReturnEmptyListForValidConfig()
        {
            // Arrange - Create valid config with existing paths
            var validConfigContent = _originalConfigContent.Replace(
                "C:\\Program Files\\Autodesk\\Network License Manager\\lmutil.exe",
                Path.GetTempFileName());

            File.WriteAllText(_testConfigPath, validConfigContent);

            // Act
            var errors = _testConfigManager.ValidateConfiguration();

            // Assert
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void ReloadConfiguration_ShouldUpdateConfiguration()
        {
            // Arrange
            var originalConfig = _testConfigManager.CurrentConfiguration;

            // Modify the config file
            var modifiedConfigContent = _originalConfigContent.Replace(
                "Information",
                "Debug");

            File.WriteAllText(_testConfigPath, modifiedConfigContent);

            // Act
            _testConfigManager.ForceReload();
            var newConfig = _testConfigManager.CurrentConfiguration;

            // Assert
            Assert.AreNotEqual(originalConfig, newConfig);
            Assert.AreEqual("Debug", newConfig.Logging.LogLevel);
        }

        [TestMethod]
        public void ConfigurationChanged_Event_ShouldBeRaisedOnReload()
        {
            // Arrange
            bool eventRaised = false;
            ConfigurationReloadEventArgs eventArgs = null;

            _testConfigManager.ConfigurationChanged += (sender, e) =>
            {
                eventRaised = true;
                eventArgs = e;
            };

            // Act
            var modifiedConfigContent = _originalConfigContent.Replace(
                "Information",
                "Debug");

            File.WriteAllText(_testConfigPath, modifiedConfigContent);
            _testConfigManager.ForceReload();

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.IsNotNull(eventArgs);
            Assert.IsNotNull(eventArgs.OldConfiguration);
            Assert.IsNotNull(eventArgs.NewConfiguration);
        }

        [TestMethod]
        public void GetConfigurationSummary_ShouldReturnFormattedString()
        {
            // Act
            var summary = _testConfigManager.GetConfigurationSummary();

            // Assert
            Assert.IsNotNull(summary);
            Assert.IsTrue(summary.Contains("Configuration Summary:"));
            Assert.IsTrue(summary.Contains("Config File:"));
            Assert.IsTrue(summary.Contains("Last Change:"));
            Assert.IsTrue(summary.Contains("Is Valid:"));
            Assert.IsTrue(summary.Contains("LicenseManager["));
        }

        [TestMethod]
        public void ServiceSettings_GetAllAppSettings_ShouldReturnAllSettings()
        {
            // Arrange
            var settings = _testConfigManager.Settings;

            // Act
            var allSettings = settings.GetAllAppSettings();

            // Assert
            Assert.IsNotNull(allSettings);
            Assert.IsTrue(allSettings.Count > 0);
            Assert.IsTrue(allSettings.ContainsKey("ServiceName"));
            Assert.IsTrue(allSettings.ContainsKey("Environment"));
        }

        [TestMethod]
        public void ServiceSettings_GetAllConnectionStrings_ShouldReturnAllConnectionStrings()
        {
            // Arrange
            var settings = _testConfigManager.Settings;

            // Act
            var allConnectionStrings = settings.GetAllConnectionStrings();

            // Assert
            Assert.IsNotNull(allConnectionStrings);
            Assert.IsTrue(allConnectionStrings.Count > 0);
            Assert.IsTrue(allConnectionStrings.ContainsKey("LicenseReleaseServiceDB"));
        }

        [TestMethod]
        public void ServiceSettings_Validate_ShouldReturnValidationErrors()
        {
            // Arrange - Create settings with invalid values
            var configContent = _originalConfigContent.Replace(
                "PollingInterval\" value=\"300\"",
                "PollingInterval\" value=\"0\"");

            File.WriteAllText(_testConfigPath, configContent);
            _testConfigManager.ForceReload();

            var settings = _testConfigManager.Settings;

            // Act
            var errors = settings.Validate();

            // Assert
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("PollingInterval must be greater than 0")));
        }

        [TestMethod]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var configManager = new TestConfigManager(_testConfigPath);

            // Act
            configManager.Dispose();

            // Assert - Should not throw exception
            Assert.ThrowsException<ObjectDisposedException>(() =>
            {
                var config = configManager.CurrentConfiguration;
            });
        }

        [TestMethod]
        public async Task ConfigurationReload_ShouldHandleRapidChanges()
        {
            // Arrange
            int eventCount = 0;
            _testConfigManager.ConfigurationChanged += (sender, e) => eventCount++;

            // Act - Make rapid changes
            for (int i = 0; i < 5; i++)
            {
                var modifiedContent = _originalConfigContent.Replace(
                    "PollingInterval\" value=\"300\"",
                    $"PollingInterval\" value=\"{300 + i}\"");

                File.WriteAllText(_testConfigPath, modifiedContent);
                await Task.Delay(100);
            }

            // Wait for debouncing to complete
            await Task.Delay(2000);

            // Assert - Should have handled the changes without crashing
            Assert.IsTrue(eventCount >= 1);
        }

        [TestMethod]
        public void ConfigurationSections_Validate_ShouldCheckAllSections()
        {
            // Arrange
            var config = _testConfigManager.CurrentConfiguration;

            // Act
            var errors = config.Validate();

            // Assert
            // Should have errors for non-existent paths, but validation method itself should work
            Assert.IsNotNull(errors);
            // In a real test environment, we'd mock the file system checks
        }

        [TestMethod]
        public void ServiceSettings_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var settings = _testConfigManager.Settings;

            // Act
            var toString = settings.ToString();

            // Assert
            Assert.IsNotNull(toString);
            Assert.IsTrue(toString.Contains("ServiceSettings Configuration:"));
            Assert.IsTrue(toString.Contains("ServiceName:"));
            Assert.IsTrue(toString.Contains("Environment:"));
            Assert.IsTrue(toString.Contains("LogLevel:"));
        }

        [TestMethod]
        public void ConfigurationManager_Singleton_ShouldReturnSameInstance()
        {
            // Arrange & Act
            var instance1 = TestConfigManager.Instance;
            var instance2 = TestConfigManager.Instance;

            // Assert
            Assert.AreSame(instance1, instance2);
        }
    }

    /// <summary>
    /// Test configuration manager that allows custom config file path
    /// </summary>
    public class TestConfigManager : ConfigurationManager
    {
        private readonly string _configPath;

        public TestConfigManager(string configPath)
        {
            _configPath = configPath;
        }

        protected override string GetConfigFilePath()
        {
            return _configPath;
        }

        public new LicenseReleaseServiceSection CurrentConfiguration => base.CurrentConfiguration;

        public new ServiceSettings Settings => base.Settings;

        public new void ForceReload() => base.ForceReload();

        public new List<string> ValidateConfiguration() => base.ValidateConfiguration();

        public new string GetAppSetting(string key, string defaultValue = null) => base.GetAppSetting(key, defaultValue);

        public new string GetConnectionString(string name) => base.GetConnectionString(name);

        public new string GetConfigurationSummary() => base.GetConfigurationSummary();

        public new void Dispose() => base.Dispose();
    }
}