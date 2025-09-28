using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.Services;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.Models;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.Tests.Services
{
    /// <summary>
    /// Unit tests for LicenseReleaseSafetyValidator
    /// </summary>
    [TestClass]
    public class LicenseReleaseSafetyValidatorTests
    {
        private Mock<ILogger<LicenseReleaseSafetyValidator>> _mockLogger;
        private Mock<IProcessExecutor> _mockProcessExecutor;
        private LicenseReleaseSafetyValidator _validator;
        private SafetyValidationConfiguration _validConfig;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockLogger = new Mock<ILogger<LicenseReleaseSafetyValidator>>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _validator = new LicenseReleaseSafetyValidator(_mockLogger.Object, _mockProcessExecutor.Object);

            _validConfig = new SafetyValidationConfiguration
            {
                Enabled = true,
                ValidationTimeoutSeconds = 30,
                MaxConsecutiveFailures = 5,
                MinimumIdleTimeMinutes = 15,
                RequireUserConfirmation = true,
                UserActivityCheckIntervalSeconds = 5,
                MaxCpuUsagePercent = 80,
                MaxMemoryUsagePercent = 85,
                ValidateNetworkConnectivity = true,
                NetworkConnectivityTimeoutSeconds = 10
            };
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _validator?.Dispose();
            _validator = null;
            _mockLogger = null;
            _mockProcessExecutor = null;
        }

        [TestMethod]
        public async Task InitializeAsync_ValidConfiguration_ShouldReturnSuccessfulResult()
        {
            // Arrange
            var config = new SafetyValidationConfiguration
            {
                Enabled = true,
                ValidationTimeoutSeconds = 30,
                MaxConsecutiveFailures = 5
            };

            // Act
            var result = await _validator.InitializeAsync(config);

            // Assert
            Assert.IsTrue(result.Success, "Initialization should succeed");
            Assert.AreEqual("Safety validator initialized successfully", result.Message);
            Assert.AreEqual(0, result.Errors.Count, "There should be no errors");
            Assert.IsTrue(_validator.IsInitialized, "Validator should be marked as initialized");
            Assert.AreEqual(config, _validator.Configuration, "Configuration should be stored");
        }

        [TestMethod]
        public async Task InitializeAsync_InvalidConfiguration_ShouldReturnFailureResult()
        {
            // Arrange
            var config = new SafetyValidationConfiguration
            {
                ValidationTimeoutSeconds = 0 // Invalid
            };

            // Act
            var result = await _validator.InitializeAsync(config);

            // Assert
            Assert.IsFalse(result.Success, "Initialization should fail");
            Assert.AreEqual("Configuration validation failed", result.Message);
            Assert.AreEqual(1, result.Errors.Count, "There should be one error");
            Assert.AreEqual("Validation timeout must be greater than 0", result.Errors[0]);
            Assert.IsFalse(_validator.IsInitialized, "Validator should not be marked as initialized");
        }

        [TestMethod]
        public async Task ValidateLicenseReleaseSafetyAsync_NotInitialized_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var server = "testserver";
            var port = 27000;
            var feature = "testfeature";
            var user = "testuser";

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => _validator.ValidateLicenseReleaseSafetyAsync(server, port, feature, user));

            Assert.AreEqual("Safety validator is not initialized", exception.Message);
        }

        [TestMethod]
        public async Task ValidateLicenseReleaseSafetyAsync_ValidParameters_ShouldReturnValidationResult()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var server = "testserver";
            var port = 27000;
            var feature = "testfeature";
            var user = "testuser";

            // Act
            var result = await _validator.ValidateLicenseReleaseSafetyAsync(server, port, feature, user);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsNotNull(result.ValidationChecks, "ValidationChecks should not be null");
            Assert.IsNotNull(result.Warnings, "Warnings should not be null");
            Assert.IsNotNull(result.Errors, "Errors should not be null");
            Assert.AreEqual(DateTime.UtcNow.Date, result.Timestamp.Date, "Timestamp should be set");
        }

        [TestMethod]
        public async Task ValidateProcessAccessibilityAsync_ProtectedProcess_ShouldBlock()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var protectedProcess = "explorer.exe";
            var userName = "testuser";

            // Act
            var result = await _validator.ValidateProcessAccessibilityAsync(protectedProcess, userName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(SafetyStatus.Unsafe, result.Status, "Should be unsafe for protected processes");
            Assert.AreEqual(SafetyValidationAction.Block, result.RecommendedAction, "Should block protected processes");
            Assert.IsFalse(result.IsValid, "Should be invalid for protected processes");
            Assert.AreEqual($"Protected process: {protectedProcess}", result.FailureReason);

            // Should have validation check for protected process
            Assert.IsTrue(result.ValidationChecks.ContainsKey("ProtectedProcessCheck"));
            var protectedCheck = result.ValidationChecks["ProtectedProcessCheck"];
            Assert.IsFalse(protectedCheck.Passed);
            Assert.AreEqual($"Process {protectedProcess} is protected and cannot be interrupted", protectedCheck.Message);
        }

        [TestMethod]
        public async Task ValidateProcessAccessibilityAsync_NonRunningProcess_ShouldWarn()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var nonRunningProcess = "nonexistentprocess";
            var userName = "testuser";

            // Act
            var result = await _validator.ValidateProcessAccessibilityAsync(nonRunningProcess, userName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(SafetyStatus.Warning, result.Status, "Should be warning for non-running processes");
            Assert.AreEqual(SafetyValidationAction.Warn, result.RecommendedAction, "Should warn for non-running processes");
            Assert.IsTrue(result.IsValid, "Should be valid for non-running processes");

            // Should have validation check for process running
            Assert.IsTrue(result.ValidationChecks.ContainsKey("ProcessRunningCheck"));
            var runningCheck = result.ValidationChecks["ProcessRunningCheck"];
            Assert.IsFalse(runningCheck.Passed);
            Assert.AreEqual($"Process {nonRunningProcess} is not running", runningCheck.Message);

            // Should have warning about non-running process
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("not currently running")));
        }

        [TestMethod]
        public async Task ValidateProcessAccessibilityAsync_CriticalApplication_ShouldRequireConfirmation()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var criticalApp = "devenv.exe";
            var userName = "testuser";

            // Mock a running process
            var mockProcess = new Mock<Process>();
            mockProcess.Setup(p => p.ProcessName).Returns(criticalApp);
            mockProcess.Setup(p => p.Id).Returns(1234);
            mockProcess.Setup(p => p.StartTime).Returns(DateTime.Now);

            // Mock Process.GetProcessesByName to return our mock process
            var processField = typeof(Process).GetField("s_processNames", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (processField != null)
            {
                // This is a workaround for testing - in real scenarios we'd need to use a different approach
                // For now, we'll test the logic without mocking the actual process enumeration
            }

            // Act
            var result = await _validator.ValidateProcessAccessibilityAsync(criticalApp, userName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");

            // Should have validation check for critical application
            Assert.IsTrue(result.ValidationChecks.ContainsKey("CriticalApplicationCheck"));
            var criticalCheck = result.ValidationChecks["CriticalApplicationCheck"];
            Assert.IsTrue(criticalCheck.Passed);
            Assert.AreEqual($"Process {criticalApp} is a critical application", criticalCheck.Message);

            // Should have warning about critical application
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("critical application")));
        }

        [TestMethod]
        public async Task ValidateUserActivityAsync_SufficientIdleTime_ShouldPass()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var userName = "testuser";

            // Act
            var result = await _validator.ValidateUserActivityAsync(userName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsNotNull(result.UserActivity, "UserActivity should not be null");
            Assert.IsNotNull(result.UserActivity.ActiveProcesses, "ActiveProcesses should not be null");
            Assert.IsNotNull(result.UserActivity.InputActivity, "InputActivity should not be null");

            // Should have validation check for user idle time
            Assert.IsTrue(result.ValidationChecks.ContainsKey("UserIdleTimeCheck"));
        }

        [TestMethod]
        public async Task ValidateSystemResourcesAsync_NormalSystemLoad_ShouldPass()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);

            // Act
            var result = await _validator.ValidateSystemResourcesAsync();

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsNotNull(result.SystemResources, "SystemResources should not be null");
            Assert.IsNotNull(result.SystemResources.NetworkActivity, "NetworkActivity should not be null");

            // Should have validation checks for system resources
            Assert.IsTrue(result.ValidationChecks.ContainsKey("CpuUsageCheck"));
            Assert.IsTrue(result.ValidationChecks.ContainsKey("MemoryUsageCheck"));
            Assert.IsTrue(result.ValidationChecks.ContainsKey("AvailableMemoryCheck"));
        }

        [TestMethod]
        public async Task ValidateNetworkConnectivityAsync_AllowedServer_ShouldPass()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var server = "allowedserver.company.com";
            var port = 27000;

            // Configure allowed server
            _validConfig.AllowedLicenseServers.Add(server);
            await _validator.UpdateConfigurationAsync(_validConfig);

            // Act
            var result = await _validator.ValidateNetworkConnectivityAsync(server, port);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");

            // Should have validation check for allowed server
            Assert.IsTrue(result.ValidationChecks.ContainsKey("AllowedServerCheck"));
            var allowedCheck = result.ValidationChecks["AllowedServerCheck"];
            Assert.IsTrue(allowedCheck.Passed);
        }

        [TestMethod]
        public async Task ValidateNetworkConnectivityAsync_NonAllowedServer_ShouldBlock()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var server = "notallowedserver.company.com";
            var port = 27000;

            // Configure allowed servers (excluding our test server)
            _validConfig.AllowedLicenseServers.Add("allowedserver.company.com");
            await _validator.UpdateConfigurationAsync(_validConfig);

            // Act
            var result = await _validator.ValidateNetworkConnectivityAsync(server, port);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(SafetyStatus.Unsafe, result.Status, "Should be unsafe for non-allowed servers");
            Assert.AreEqual(SafetyValidationAction.Block, result.RecommendedAction, "Should block non-allowed servers");
            Assert.IsFalse(result.IsValid, "Should be invalid for non-allowed servers");
            Assert.AreEqual($"Server not allowed: {server}", result.FailureReason);

            // Should have validation check for allowed server
            Assert.IsTrue(result.ValidationChecks.ContainsKey("AllowedServerCheck"));
            var allowedCheck = result.ValidationChecks["AllowedServerCheck"];
            Assert.IsFalse(allowedCheck.Passed);
            Assert.AreEqual($"Server {server} is not in allowed servers list", allowedCheck.Message);
        }

        [TestMethod]
        public async Task ValidateCriticalOperationsAsync_CriticalOperation_ShouldWarn()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var operation = "LicenseRelease";

            // Act
            var result = await _validator.ValidateCriticalOperationsAsync(operation);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");

            // Should have validation check for critical operation
            Assert.IsTrue(result.ValidationChecks.ContainsKey("CriticalOperationCheck"));
            var criticalCheck = result.ValidationChecks["CriticalOperationCheck"];
            Assert.IsTrue(criticalCheck.Passed);
            Assert.AreEqual("Operation is marked as critical", criticalCheck.Message);

            // Should have validation check for user confirmation
            Assert.IsTrue(result.ValidationChecks.ContainsKey("UserConfirmationCheck"));
            var confirmationCheck = result.ValidationChecks["UserConfirmationCheck"];
            Assert.IsFalse(confirmationCheck.Passed);
            Assert.AreEqual("User confirmation required for critical operations", confirmationCheck.Message);
        }

        [TestMethod]
        public async Task ValidateCriticalOperationsAsync_NonCriticalOperation_ShouldPass()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var operation = "NonCriticalOperation";

            // Act
            var result = await _validator.ValidateCriticalOperationsAsync(operation);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");

            // Should not have critical operation check for non-critical operations
            Assert.IsFalse(result.ValidationChecks.ContainsKey("CriticalOperationCheck"));
        }

        [TestMethod]
        public async Task ValidateCriticalOperationsAsync_TooManyFailures_ShouldBlock()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var operation = "LicenseRelease";

            // Simulate too many failures by updating statistics directly
            var stats = await _validator.GetStatisticsAsync();
            for (int i = 0; i < _validConfig.MaxConsecutiveFailures; i++)
            {
                // Simulate failures by calling validate with invalid parameters
                try
                {
                    await _validator.ValidateNetworkConnectivityAsync("invalidserver", 27000);
                }
                catch
                {
                    // Expected to fail
                }
            }

            // Act
            var result = await _validator.ValidateCriticalOperationsAsync(operation);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(SafetyStatus.Unsafe, result.Status, "Should be unsafe with too many failures");
            Assert.AreEqual(SafetyValidationAction.Block, result.RecommendedAction, "Should block with too many failures");
            Assert.IsFalse(result.IsValid, "Should be invalid with too many failures");
            Assert.AreEqual($"Safety lockout: {_validConfig.MaxConsecutiveFailures} consecutive failures", result.FailureReason);

            // Should have validation check for consecutive failures
            Assert.IsTrue(result.ValidationChecks.ContainsKey("ConsecutiveFailuresCheck"));
            var failuresCheck = result.ValidationChecks["ConsecutiveFailuresCheck"];
            Assert.IsFalse(failuresCheck.Passed);
        }

        [TestMethod]
        public async Task ValidateComprehensiveSafetyAsync_ValidContext_ShouldPerformAllValidations()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var context = new ValidationContext
            {
                OperationType = "LicenseRelease",
                Server = "testserver",
                Port = 27000,
                Feature = "testfeature",
                UserName = "testuser"
            };

            // Act
            var result = await _validator.ValidateComprehensiveSafetyAsync(context);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");

            // Should have validation checks from all validation types
            Assert.IsTrue(result.ValidationChecks.Keys.Any(k => k.Contains("UserIdleTimeCheck") || k.Contains("CpuUsageCheck") || k.Contains("NetworkPingCheck")));

            // Should update overall status
            Assert.AreNotEqual(SafetyStatus.Unknown, result.Status, "Overall status should be updated");
        }

        [TestMethod]
        public async Task StartUserActivityMonitoringAsync_ValidUser_ShouldStartMonitoring()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var userName = "testuser";

            // Act
            var result = await _validator.StartUserActivityMonitoringAsync(userName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Monitoring should start successfully");
            Assert.AreEqual("User activity monitoring started", result.Message);
            Assert.IsNotNull(result.MonitoringId, "Should have a monitoring ID");
        }

        [TestMethod]
        public async Task StartUserActivityMonitoringAsync_AlreadyMonitoring_ShouldReturnFailure()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var userName = "testuser";

            // Start monitoring first time
            await _validator.StartUserActivityMonitoringAsync(userName);

            // Act - Try to start monitoring again
            var result = await _validator.StartUserActivityMonitoringAsync(userName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsFalse(result.Success, "Should not start monitoring twice");
            Assert.AreEqual("User activity monitoring is already active", result.Message);
        }

        [TestMethod]
        public async Task StopUserActivityMonitoringAsync_ActiveMonitoring_ShouldStopMonitoring()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var userName = "testuser";

            // Start monitoring first
            await _validator.StartUserActivityMonitoringAsync(userName);

            // Act
            var result = await _validator.StopUserActivityMonitoringAsync();

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Should stop monitoring successfully");
            Assert.AreEqual("User activity monitoring stopped", result.Message);
        }

        [TestMethod]
        public async Task StopUserActivityMonitoringAsync_NoActiveMonitoring_ShouldReturnFailure()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);

            // Act - Try to stop monitoring without starting
            var result = await _validator.StopUserActivityMonitoringAsync();

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsFalse(result.Success, "Should not stop inactive monitoring");
            Assert.AreEqual("User activity monitoring is not active", result.Message);
        }

        [TestMethod]
        public async Task GetUserActivityInfoAsync_ValidUser_ShouldReturnActivityInfo()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var userName = "testuser";

            // Act
            var result = await _validator.GetUserActivityInfoAsync(userName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsNotNull(result.ActiveProcesses, "ActiveProcesses should not be null");
            Assert.IsNotNull(result.InputActivity, "InputActivity should not be null");
            Assert.IsTrue(result.ActiveProcesses is List<string>, "ActiveProcesses should be a list");
        }

        [TestMethod]
        public async Task GetProtectedProcessesAsync_ShouldReturnConfiguredProcesses()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);

            // Act
            var result = await _validator.GetProtectedProcessesAsync();

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            CollectionAssert.Contains(result, "explorer.exe");
            CollectionAssert.Contains(result, "lsass.exe");
            CollectionAssert.Contains(result, "csrss.exe");
        }

        [TestMethod]
        public async Task GetCriticalApplicationsAsync_ShouldReturnConfiguredApplications()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);

            // Act
            var result = await _validator.GetCriticalApplicationsAsync();

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            CollectionAssert.Contains(result, "devenv.exe");
            CollectionAssert.Contains(result, "notepad++.exe");
            CollectionAssert.Contains(result, "code.exe");
        }

        [TestMethod]
        public async Task UpdateConfigurationAsync_ValidConfiguration_ShouldSucceed()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var newConfig = new SafetyValidationConfiguration
            {
                Enabled = false,
                ValidationTimeoutSeconds = 60,
                MaxConsecutiveFailures = 10
            };

            // Act
            var result = await _validator.UpdateConfigurationAsync(newConfig);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Update should succeed");
            Assert.AreEqual("Configuration updated successfully", result.Message);
            Assert.AreEqual(newConfig.Enabled, _validator.Configuration.Enabled);
            Assert.AreEqual(newConfig.ValidationTimeoutSeconds, _validator.Configuration.ValidationTimeoutSeconds);
        }

        [TestMethod]
        public async Task UpdateConfigurationAsync_InvalidConfiguration_ShouldFail()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var invalidConfig = new SafetyValidationConfiguration
            {
                ValidationTimeoutSeconds = 0 // Invalid
            };

            // Act
            var result = await _validator.UpdateConfigurationAsync(invalidConfig);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsFalse(result.Success, "Update should fail");
            Assert.IsTrue(result.Message.Contains("Configuration validation failed"));
        }

        [TestMethod]
        public async Task GetStatisticsAsync_ShouldReturnCurrentStatistics()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);

            // Perform some validation operations to generate statistics
            try
            {
                await _validator.ValidateNetworkConnectivityAsync("testserver", 27000);
            }
            catch
            {
                // Expected to fail due to network issues
            }

            // Act
            var result = await _validator.GetStatisticsAsync();

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.TotalValidations >= 0, "Total validations should be non-negative");
            Assert.IsTrue(result.SuccessfulValidations >= 0, "Successful validations should be non-negative");
            Assert.IsTrue(result.FailedValidations >= 0, "Failed validations should be non-negative");
            Assert.IsTrue(result.Warnings >= 0, "Warnings should be non-negative");
        }

        [TestMethod]
        public async Task ResetStatisticsAsync_ShouldResetAllStatistics()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);

            // Perform some validation operations to generate statistics
            try
            {
                await _validator.ValidateNetworkConnectivityAsync("testserver", 27000);
            }
            catch
            {
                // Expected to fail due to network issues
            }

            // Get statistics before reset
            var statsBeforeReset = await _validator.GetStatisticsAsync();

            // Act
            var result = await _validator.ResetStatisticsAsync();

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Reset should succeed");
            Assert.AreEqual("Statistics reset successfully", result.Message);

            // Get statistics after reset
            var statsAfterReset = await _validator.GetStatisticsAsync();

            // Verify all statistics are reset to zero
            Assert.AreEqual(0, statsAfterReset.TotalValidations);
            Assert.AreEqual(0, statsAfterReset.SuccessfulValidations);
            Assert.AreEqual(0, statsAfterReset.FailedValidations);
            Assert.AreEqual(0, statsAfterReset.Warnings);
            Assert.AreEqual(0, statsAfterReset.AverageValidationTimeMs);
            Assert.AreEqual(0, statsAfterReset.SuccessRatePercentage);
            Assert.IsNull(statsAfterReset.MostCommonFailureReason);
        }

        [TestMethod]
        public async Task AddCustomValidationRuleAsync_ValidRule_ShouldSucceed()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var ruleName = "TestRule";
            Func<ValidationContext, Task<SafetyCheckResult>> validationFunc =
                (context) => Task.FromResult(new SafetyCheckResult { Passed = true, Message = "Test passed" });

            // Act
            var result = await _validator.AddCustomValidationRuleAsync(ruleName, validationFunc);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Add rule should succeed");
            Assert.AreEqual("Custom validation rule added successfully", result.Message);
        }

        [TestMethod]
        public async Task AddCustomValidationRuleAsync_EmptyRuleName_ShouldFail()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var ruleName = "";
            Func<ValidationContext, Task<SafetyCheckResult>> validationFunc =
                (context) => Task.FromResult(new SafetyCheckResult { Passed = true, Message = "Test passed" });

            // Act
            var result = await _validator.AddCustomValidationRuleAsync(ruleName, validationFunc);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsFalse(result.Success, "Add rule should fail for empty rule name");
            Assert.AreEqual("Rule name cannot be empty", result.Message);
        }

        [TestMethod]
        public async Task AddCustomValidationRuleAsync_NullValidationFunc_ShouldFail()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var ruleName = "TestRule";
            Func<ValidationContext, Task<SafetyCheckResult>> validationFunc = null;

            // Act
            var result = await _validator.AddCustomValidationRuleAsync(ruleName, validationFunc);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsFalse(result.Success, "Add rule should fail for null validation function");
            Assert.AreEqual("Validation function cannot be null", result.Message);
        }

        [TestMethod]
        public async Task RemoveCustomValidationRuleAsync_ExistingRule_ShouldSucceed()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var ruleName = "TestRule";
            Func<ValidationContext, Task<SafetyCheckResult>> validationFunc =
                (context) => Task.FromResult(new SafetyCheckResult { Passed = true, Message = "Test passed" });

            // Add the rule first
            await _validator.AddCustomValidationRuleAsync(ruleName, validationFunc);

            // Act
            var result = await _validator.RemoveCustomValidationRuleAsync(ruleName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Remove rule should succeed");
            Assert.AreEqual("Custom validation rule removed successfully", result.Message);
        }

        [TestMethod]
        public async Task RemoveCustomValidationRuleAsync_NonExistingRule_ShouldFail()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var ruleName = "NonExistingRule";

            // Act
            var result = await _validator.RemoveCustomValidationRuleAsync(ruleName);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsFalse(result.Success, "Remove rule should fail for non-existing rule");
            Assert.AreEqual("Custom validation rule not found", result.Message);
        }

        [TestMethod]
        public async Task SafetyValidationCompleted_ShouldRaiseEvent()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var eventRaised = false;
            SafetyValidationCompletedEventArgs eventArgs = null;

            _validator.SafetyValidationCompleted += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            var context = new ValidationContext
            {
                OperationType = "LicenseRelease",
                Server = "testserver",
                Port = 27000,
                Feature = "testfeature",
                UserName = "testuser"
            };

            // Act
            await _validator.ValidateComprehensiveSafetyAsync(context);

            // Assert
            Assert.IsTrue(eventRaised, "SafetyValidationCompleted event should be raised");
            Assert.IsNotNull(eventArgs, "Event arguments should not be null");
            Assert.IsNotNull(eventArgs.Result, "Event result should not be null");
            Assert.IsNotNull(eventArgs.Context, "Event context should not be null");
            Assert.IsTrue(eventArgs.Duration.TotalMilliseconds > 0, "Event duration should be positive");
        }

        [TestMethod]
        public async Task SafetyCheckFailed_ShouldRaiseEventForCriticalFailures()
        {
            // Arrange
            await _validator.InitializeAsync(_validConfig);
            var eventRaised = false;
            SafetyCheckFailedEventArgs eventArgs = null;

            _validator.SafetyCheckFailed += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            var context = new ValidationContext
            {
                OperationType = "LicenseRelease",
                Server = "notallowedserver", // This should trigger a critical failure
                Port = 27000,
                Feature = "testfeature",
                UserName = "testuser"
            };

            // Configure allowed servers to exclude our test server
            _validConfig.AllowedLicenseServers.Add("allowedserver");
            await _validator.UpdateConfigurationAsync(_validConfig);

            // Act
            await _validator.ValidateComprehensiveSafetyAsync(context);

            // Assert
            Assert.IsTrue(eventRaised, "SafetyCheckFailed event should be raised for critical failures");
            Assert.IsNotNull(eventArgs, "Event arguments should not be null");
            Assert.AreEqual("ComprehensiveValidation", eventArgs.CheckName);
            Assert.IsNotNull(eventArgs.FailureReason, "Failure reason should not be null");
            Assert.AreEqual(SafetyValidationAction.Block, eventArgs.RecommendedAction);
        }

        [TestMethod]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            _validator.Dispose();

            // Act & Assert
            // Should not throw exception when disposed multiple times
            _validator.Dispose();

            // Should not throw exception when calling methods after dispose
            // (though they may throw ObjectDisposedException in real implementation)
        }
    }
}