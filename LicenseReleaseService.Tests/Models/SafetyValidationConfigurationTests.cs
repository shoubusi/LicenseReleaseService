using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Models;

namespace LicenseReleaseService.Tests.Models
{
    /// <summary>
    /// Unit tests for SafetyValidationConfiguration
    /// </summary>
    [TestClass]
    public class SafetyValidationConfigurationTests
    {
        private SafetyValidationConfiguration _config;

        [TestInitialize]
        public void TestInitialize()
        {
            _config = new SafetyValidationConfiguration();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _config = null;
        }

        [TestMethod]
        public void Constructor_DefaultValues_ShouldSetReasonableDefaults()
        {
            // Assert
            Assert.IsTrue(_config.Enabled, "Safety validation should be enabled by default");
            Assert.AreEqual(30, _config.ValidationTimeoutSeconds, "Default validation timeout should be 30 seconds");
            Assert.AreEqual(5, _config.MaxConsecutiveFailures, "Default max consecutive failures should be 5");
            Assert.AreEqual(60, _config.SafetyLockoutDurationMinutes, "Default safety lockout duration should be 60 minutes");
            Assert.AreEqual(15, _config.MinimumIdleTimeMinutes, "Default minimum idle time should be 15 minutes");
            Assert.IsTrue(_config.RequireUserConfirmation, "User confirmation should be required by default");
            Assert.AreEqual(5, _config.UserActivityCheckIntervalSeconds, "Default user activity check interval should be 5 seconds");
            Assert.AreEqual(80, _config.MaxCpuUsagePercent, "Default max CPU usage should be 80%");
            Assert.AreEqual(85, _config.MaxMemoryUsagePercent, "Default max memory usage should be 85%");
            Assert.IsTrue(_config.ValidateNetworkConnectivity, "Network connectivity validation should be enabled by default");
            Assert.AreEqual(10, _config.NetworkConnectivityTimeoutSeconds, "Default network connectivity timeout should be 10 seconds");
            Assert.IsFalse(_config.EnableDebugLogging, "Debug logging should be disabled by default");
            Assert.AreEqual(SafetyValidationAction.Block, _config.FailureAction, "Default failure action should be Block");
            Assert.AreEqual(SafetyValidationAction.Warn, _config.WarningAction, "Default warning action should be Warn");
        }

        [TestMethod]
        public void Constructor_ProtectedProcesses_ShouldIncludeEssentialProcesses()
        {
            // Assert
            CollectionAssert.Contains(_config.ProtectedProcesses, "explorer.exe");
            CollectionAssert.Contains(_config.ProtectedProcesses, "lsass.exe");
            CollectionAssert.Contains(_config.ProtectedProcesses, "csrss.exe");
            CollectionAssert.Contains(_config.ProtectedProcesses, "winlogon.exe");
            CollectionAssert.Contains(_config.ProtectedProcesses, "services.exe");
            CollectionAssert.Contains(_config.ProtectedProcesses, "svchost.exe");
        }

        [TestMethod]
        public void Constructor_CriticalApplications_ShouldIncludeCommonApplications()
        {
            // Assert
            CollectionAssert.Contains(_config.CriticalApplications, "devenv.exe");
            CollectionAssert.Contains(_config.CriticalApplications, "notepad++.exe");
            CollectionAssert.Contains(_config.CriticalApplications, "code.exe");
            CollectionAssert.Contains(_config.CriticalApplications, "winword.exe");
            CollectionAssert.Contains(_config.CriticalApplications, "excel.exe");
        }

        [TestMethod]
        public void Validate_ValidConfiguration_ShouldReturnValidResult()
        {
            // Arrange
            _config.ValidationTimeoutSeconds = 30;
            _config.MaxConsecutiveFailures = 5;
            _config.SafetyLockoutDurationMinutes = 60;
            _config.MinimumIdleTimeMinutes = 15;
            _config.UserActivityCheckIntervalSeconds = 5;
            _config.MaxCpuUsagePercent = 80;
            _config.MaxMemoryUsagePercent = 85;
            _config.NetworkConnectivityTimeoutSeconds = 10;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsTrue(result.IsValid, "Configuration should be valid");
            Assert.AreEqual(0, result.Errors.Count, "There should be no validation errors");
        }

        [TestMethod]
        public void Validate_InvalidValidationTimeout_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.ValidationTimeoutSeconds = 0;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Validation timeout must be greater than 0", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_NegativeValidationTimeout_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.ValidationTimeoutSeconds = -5;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Validation timeout must be greater than 0", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_ZeroMaxConsecutiveFailures_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.MaxConsecutiveFailures = 0;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Maximum consecutive failures must be greater than 0", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_ZeroSafetyLockoutDuration_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.SafetyLockoutDurationMinutes = 0;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Safety lockout duration must be greater than 0", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_NegativeMinimumIdleTime_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.MinimumIdleTimeMinutes = -10;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Minimum idle time must be non-negative", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_ZeroUserActivityCheckInterval_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.UserActivityCheckIntervalSeconds = 0;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("User activity check interval must be greater than 0", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_ZeroMaxCpuUsage_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.MaxCpuUsagePercent = 0;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Maximum CPU usage must be between 1 and 100", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_MaxCpuUsageOver100_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.MaxCpuUsagePercent = 150;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Maximum CPU usage must be between 1 and 100", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_ZeroMaxMemoryUsage_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.MaxMemoryUsagePercent = 0;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Maximum memory usage must be between 1 and 100", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_MaxMemoryUsageOver100_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.MaxMemoryUsagePercent = 120;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Maximum memory usage must be between 1 and 100", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_ZeroNetworkConnectivityTimeout_ShouldReturnInvalidResult()
        {
            // Arrange
            _config.NetworkConnectivityTimeoutSeconds = 0;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.AreEqual(1, result.Errors.Count, "There should be one validation error");
            Assert.AreEqual("Network connectivity timeout must be greater than 0", result.Errors[0]);
        }

        [TestMethod]
        public void Validate_MultipleValidationErrors_ShouldReturnAllErrors()
        {
            // Arrange
            _config.ValidationTimeoutSeconds = 0;
            _config.MaxConsecutiveFailures = 0;
            _config.MaxCpuUsagePercent = 0;

            // Act
            var result = _config.Validate();

            // Assert
            Assert.IsFalse(result.IsValid, "Configuration should be invalid");
            Assert.IsTrue(result.Errors.Count >= 3, "There should be at least 3 validation errors");
            CollectionAssert.Contains(result.Errors, "Validation timeout must be greater than 0");
            CollectionAssert.Contains(result.Errors, "Maximum consecutive failures must be greater than 0");
            CollectionAssert.Contains(result.Errors, "Maximum CPU usage must be between 1 and 100");
        }

        [TestMethod]
        public void ProtectedProcesses_CanBeModified_ShouldAllowCustomization()
        {
            // Arrange
            var originalCount = _config.ProtectedProcesses.Count;

            // Act
            _config.ProtectedProcesses.Add("customprocess.exe");
            _config.ProtectedProcesses.Remove("csrss.exe");

            // Assert
            Assert.AreEqual(originalCount, _config.ProtectedProcesses.Count, "Process list count should remain the same");
            CollectionAssert.Contains(_config.ProtectedProcesses, "customprocess.exe", "Custom process should be added");
            CollectionAssert.DoesNotContain(_config.ProtectedProcesses, "csrss.exe", "Removed process should not be in list");
        }

        [TestMethod]
        public void CriticalApplications_CanBeModified_ShouldAllowCustomization()
        {
            // Arrange
            var originalCount = _config.CriticalApplications.Count;

            // Act
            _config.CriticalApplications.Add("customapp.exe");
            _config.CriticalApplications.Remove("notepad++.exe");

            // Assert
            Assert.AreEqual(originalCount, _config.CriticalApplications.Count, "Application list count should remain the same");
            CollectionAssert.Contains(_config.CriticalApplications, "customapp.exe", "Custom application should be added");
            CollectionAssert.DoesNotContain(_config.CriticalApplications, "notepad++.exe", "Removed application should not be in list");
        }

        [TestMethod]
        public void AllowedLicenseServers_ShouldInitializeEmpty_ShouldAllowCustomServers()
        {
            // Arrange & Act
            _config.AllowedLicenseServers.Add("server1.company.com");
            _config.AllowedLicenseServers.Add("server2.company.com");

            // Assert
            Assert.AreEqual(2, _config.AllowedLicenseServers.Count, "Should have 2 allowed servers");
            CollectionAssert.Contains(_config.AllowedLicenseServers, "server1.company.com");
            CollectionAssert.Contains(_config.AllowedLicenseServers, "server2.company.com");
        }

        [TestMethod]
        public void CustomValidationRules_ShouldInitializeEmpty_ShouldAllowCustomRules()
        {
            // Arrange & Act
            _config.CustomValidationRules.Add("CustomRule1", "Custom validation logic 1");
            _config.CustomValidationRules.Add("CustomRule2", "Custom validation logic 2");

            // Assert
            Assert.AreEqual(2, _config.CustomValidationRules.Count, "Should have 2 custom rules");
            Assert.AreEqual("Custom validation logic 1", _config.CustomValidationRules["CustomRule1"]);
            Assert.AreEqual("Custom validation logic 2", _config.CustomValidationRules["CustomRule2"]);
        }

        [TestMethod]
        public void SafetyValidationActions_ShouldSupportAllRequiredActions()
        {
            // Assert - verify enum values exist
            Assert.AreEqual(0, (int)SafetyValidationAction.Allow);
            Assert.AreEqual(1, (int)SafetyValidationAction.Warn);
            Assert.AreEqual(2, (int)SafetyValidationAction.Block);
            Assert.AreEqual(3, (int)SafetyValidationAction.RequireConfirmation);
        }
    }
}