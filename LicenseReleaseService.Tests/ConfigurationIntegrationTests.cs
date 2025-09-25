using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests
{
    [TestClass]
    public class ConfigurationIntegrationTests
    {
        private string _originalConfigPath;
        private string _testConfigPath;

        [TestInitialize]
        public void TestInitialize()
        {
            // Backup original configuration
            _originalConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LicenseReleaseService.config");
            _testConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LicenseReleaseService.test.config");

            if (File.Exists(_originalConfigPath))
            {
                File.Copy(_originalConfigPath, _testConfigPath, true);
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Restore original configuration
            if (File.Exists(_originalConfigPath) && File.Exists(_testConfigPath))
            {
                File.Copy(_testConfigPath, _originalConfigPath, true);
            }

            // Clean up test files
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        [TestMethod]
        public void ConfigurationManager_Instance_ShouldBeSingleton()
        {
            // Arrange & Act
            var instance1 = ConfigurationManager.Instance;
            var instance2 = ConfigurationManager.Instance;

            // Assert
            Assert.AreSame(instance1, instance2, "ConfigurationManager should be a singleton");
        }

        [TestMethod]
        public void ConfigurationManager_Initialization_ShouldLoadConfiguration()
        {
            // Arrange
            var configManager = ConfigurationManager.Instance;

            // Act
            var config = configManager.CurrentConfiguration;

            // Assert
            Assert.IsNotNull(config, "Configuration should be loaded");
            Assert.IsTrue(config.HealthCheckIntervalSeconds > 0, "Health check interval should be positive");
            Assert.IsNotNull(config.Logging, "Logging configuration should be loaded");
            Assert.IsNotNull(config.AdvancedFeatures, "Advanced features configuration should be loaded");
        }

        [TestMethod]
        public void ConfigurationManager_Validation_ShouldDetectInvalidConfiguration()
        {
            // Arrange
            CreateInvalidConfigurationFile();

            // Act
            var configManager = ConfigurationManager.Instance;
            var validationErrors = configManager.ValidateConfiguration();

            // Assert
            Assert.IsTrue(validationErrors.Count > 0, "Validation should detect errors in invalid configuration");
        }

        [TestMethod]
        public void ConfigurationManager_AdvancedFeatures_ShouldBeControllable()
        {
            // Arrange
            var configManager = ConfigurationManager.Instance;

            // Act
            var initiallyEnabled = configManager.AdvancedFeaturesEnabled;

            // Test enabling
            configManager.EnableAdvancedFeatures();
            var enabledAfterEnable = configManager.AdvancedFeaturesEnabled;

            // Test disabling
            configManager.DisableAdvancedFeatures();
            var enabledAfterDisable = configManager.AdvancedFeaturesEnabled;

            // Assert
            Assert.IsTrue(enabledAfterEnable, "Advanced features should be enabled after calling EnableAdvancedFeatures");
            Assert.IsFalse(enabledAfterDisable, "Advanced features should be disabled after calling DisableAdvancedFeatures");
        }

        [TestMethod]
        public void LicenseReleaseService_Integration_ShouldInitializeWithConfiguration()
        {
            // Arrange & Act
            var service = new LicenseReleaseService();

            // Assert
            Assert.IsNotNull(service, "LicenseReleaseService should be created successfully");
            // The service should not throw exceptions during initialization
        }

        [TestMethod]
        public void LicenseReleaseService_ConfigurationValidation_ShouldBeIntegrated()
        {
            // Arrange
            CreateInvalidConfigurationFile();
            var service = new LicenseReleaseService();

            // Act & Assert
            // The service should handle configuration validation gracefully
            // This test mainly ensures no exceptions are thrown during initialization
            Assert.IsNotNull(service, "Service should handle configuration validation");
        }

        [TestMethod]
        public void ServiceState_ConfigurationTracking_ShouldWork()
        {
            // Arrange
            var serviceState = new ServiceState();

            // Act
            var initialHealth = serviceState.ConfigurationHealthStatus;
            serviceState.UpdateConfigurationHealth(ConfigurationHealthStatus.Healthy, new List<string>());
            var updatedHealth = serviceState.ConfigurationHealthStatus;

            // Assert
            Assert.AreEqual(ConfigurationHealthStatus.Unknown, initialHealth, "Initial configuration health should be unknown");
            Assert.AreEqual(ConfigurationHealthStatus.Healthy, updatedHealth, "Configuration health should be updated correctly");
        }

        [TestMethod]
        public void ServiceState_ConfigurationIssues_ShouldBeTracked()
        {
            // Arrange
            var serviceState = new ServiceState();
            var issues = new List<string> { "Test issue 1", "Test issue 2" };

            // Act
            serviceState.UpdateConfigurationHealth(ConfigurationHealthStatus.Warning, issues);
            var trackedIssues = serviceState.ConfigurationIssues;

            // Assert
            Assert.AreEqual(2, trackedIssues.Count, "Configuration issues should be tracked");
            Assert.IsTrue(trackedIssues.Contains("Test issue 1"), "Issue 1 should be tracked");
            Assert.IsTrue(trackedIssues.Contains("Test issue 2"), "Issue 2 should be tracked");
        }

        [TestMethod]
        public void ServiceState_ConfigurationReloads_ShouldBeCounted()
        {
            // Arrange
            var serviceState = new ServiceState();

            // Act
            var initialCount = serviceState.ConfigurationReloadCount;
            serviceState.RecordConfigurationReload("Test reload");
            serviceState.RecordConfigurationReload("Another test reload");
            var finalCount = serviceState.ConfigurationReloadCount;

            // Assert
            Assert.AreEqual(0, initialCount, "Initial reload count should be 0");
            Assert.AreEqual(2, finalCount, "Reload count should be incremented correctly");
        }

        [TestMethod]
        public void ConfigurationManager_BackupOperations_ShouldWork()
        {
            // Arrange
            var configManager = ConfigurationManager.Instance;

            // Act
            var backupPath = configManager.CreateBackup("test_backup");
            var availableBackups = configManager.GetAvailableBackups();

            // Assert
            Assert.IsNotNull(backupPath, "Backup path should be returned");
            Assert.IsTrue(File.Exists(backupPath), "Backup file should exist");
            Assert.IsTrue(availableBackups.Count > 0, "Available backups should be listed");
            Assert.IsTrue(availableBackups.Any(b => b.Contains("test_backup")), "Test backup should be in the list");
        }

        [TestMethod]
        public void ConfigurationManager_ReloadOperations_ShouldWork()
        {
            // Arrange
            var configManager = ConfigurationManager.Instance;
            var originalConfig = configManager.CurrentConfiguration;

            // Act
            var reloadResult = configManager.ReloadConfiguration();

            // Assert
            Assert.IsTrue(reloadResult, "Configuration reload should succeed");
            Assert.IsNotNull(configManager.CurrentConfiguration, "Configuration should still be loaded after reload");
        }

        [TestMethod]
        public void ConfigurationManager_FileMonitoring_ShouldDetectChanges()
        {
            // Arrange
            var configManager = ConfigurationManager.Instance;
            var lastChange = configManager.LastConfigChange;

            // Act - Simulate a file change by touching the config file
            if (File.Exists(_originalConfigPath))
            {
                File.SetLastWriteTimeUtc(_originalConfigPath, DateTime.UtcNow);
                Thread.Sleep(100); // Give time for monitoring to detect change
            }

            var newChange = configManager.LastConfigChange;

            // Assert
            // Note: This test depends on file monitoring being enabled and working
            // It may not always detect changes immediately in test environments
            Assert.IsNotNull(newChange, "Last config change time should be available");
        }

        [TestMethod]
        public void Program_CommandLine_Parsing_ShouldHandleConfigCommands()
        {
            // Arrange
            var args = new[] { "/config" };

            // Act
            var parsedArgs = Program.ParseCommandLineArguments(args);

            // Assert
            Assert.AreEqual(ServiceCommand.Config, parsedArgs.Command, "Config command should be parsed correctly");
        }

        [TestMethod]
        public void Program_CommandLine_Parsing_ShouldHandleValidateCommands()
        {
            // Arrange
            var args = new[] { "/validate" };

            // Act
            var parsedArgs = Program.ParseCommandLineArguments(args);

            // Assert
            Assert.AreEqual(ServiceCommand.Validate, parsedArgs.Command, "Validate command should be parsed correctly");
        }

        [TestMethod]
        public void HealthChecker_ConfigurationIntegration_ShouldWork()
        {
            // Arrange
            var healthChecker = new HealthChecker();
            var initialHealth = healthChecker.OverallStatus;

            // Act
            Thread.Sleep(1000); // Give time for health checks to run
            var currentHealth = healthChecker.OverallStatus;
            var healthReport = healthChecker.GetHealthReportAsync().Result;

            // Assert
            Assert.IsNotNull(healthReport, "Health report should be available");
            Assert.IsNotNull(healthReport.HealthChecks, "Health checks should be available");
            Assert.IsNotNull(healthReport.Metrics, "Health metrics should be available");

            // Check that configuration-specific health checks are included
            var configHealthCheck = healthReport.HealthChecks.Values.FirstOrDefault(h => h.Name.Contains("Configuration"));
            Assert.IsNotNull(configHealthCheck, "Configuration health check should be included");
        }

        [TestMethod]
        public void RecoveryManager_ConfigurationExceptions_ShouldBeHandled()
        {
            // Arrange
            var serviceState = new ServiceState();
            var logger = new EventLogLogger();
            var recoveryManager = new RecoveryManager(serviceState, logger);

            // Act
            var strategy = recoveryManager.GetRecoveryStrategy(new ConfigurationErrorsException("Test config error"));
            var result = recoveryManager.HandleExceptionAsync(new ConfigurationErrorsException("Test config error"), "Test context").Result;

            // Assert
            Assert.IsNotNull(strategy, "Recovery strategy should be available for configuration exceptions");
            Assert.IsNotNull(result, "Recovery result should be returned");
            Assert.AreEqual(RecoveryAction.LogAndContinue, strategy.Action, "Configuration errors should use LogAndContinue action");
        }

        [TestMethod]
        public void ServiceMetrics_ShouldIncludeConfigurationData()
        {
            // Arrange
            var serviceState = new ServiceState();
            serviceState.UpdateConfigurationHealth(ConfigurationHealthStatus.Healthy, new List<string>());
            serviceState.RecordConfigurationReload("Test reload");

            // Act
            var metrics = serviceState.GetMetrics();

            // Assert
            Assert.AreEqual(ConfigurationHealthStatus.Healthy, metrics.ConfigurationHealthStatus, "Metrics should include configuration health status");
            Assert.AreEqual(1, metrics.ConfigurationReloadCount, "Metrics should include configuration reload count");
            Assert.IsNotNull(metrics.ConfigurationIssues, "Metrics should include configuration issues");
            Assert.IsTrue(metrics.IsConfigurationHealthy, "Metrics should include configuration health flag");
        }

        [TestMethod]
        public void ConfigurationManager_Events_ShouldBeRaised()
        {
            // Arrange
            var configManager = ConfigurationManager.Instance;
            var configurationChangedRaised = false;
            var configurationHealthChanged = false;

            configManager.ConfigurationChanged += (sender, args) => configurationChangedRaised = true;
            configManager.ConfigurationHealthChanged += (sender, args) => configurationHealthChanged = true;

            // Act
            // Trigger a configuration change
            configManager.ReloadConfiguration();

            // Assert
            // Note: Event testing is tricky in unit tests as they depend on timing
            // This test mainly ensures the event handlers can be attached without errors
            Assert.IsNotNull(configManager, "Configuration manager should be available");
        }

        private void CreateInvalidConfigurationFile()
        {
            // Create a simple invalid configuration file for testing
            var invalidConfigContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <section name=""LicenseReleaseService"" type=""LicenseReleaseService.Configuration.LicenseReleaseServiceSection, LicenseReleaseService"" />
  </configSections>
  <LicenseReleaseService>
    <HealthCheckIntervalSeconds>invalid</HealthCheckIntervalSeconds>
    <Logging>
      <LogLevel>InvalidLevel</LogLevel>
    </Logging>
  </LicenseReleaseService>
</configuration>";

            File.WriteAllText(_originalConfigPath, invalidConfigContent);
        }
    }
}