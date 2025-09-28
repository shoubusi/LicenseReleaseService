using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Models;

namespace LicenseReleaseService.Tests.Models
{
    /// <summary>
    /// Unit tests for SafetyValidationResult
    /// </summary>
    [TestClass]
    public class SafetyValidationResultTests
    {
        [TestMethod]
        public void Constructor_DefaultValues_ShouldInitializeProperly()
        {
            // Act
            var result = new SafetyValidationResult();

            // Assert
            Assert.IsNotNull(result.ValidationChecks, "ValidationChecks should not be null");
            Assert.IsNotNull(result.Warnings, "Warnings should not be null");
            Assert.IsNotNull(result.Errors, "Errors should not be null");
            Assert.AreEqual(0, result.ValidationChecks.Count, "ValidationChecks should be empty");
            Assert.AreEqual(0, result.Warnings.Count, "Warnings should be empty");
            Assert.AreEqual(0, result.Errors.Count, "Errors should be empty");
            Assert.AreEqual(DateTime.UtcNow.Date, result.Timestamp.Date, "Timestamp should be set to current UTC time");
            Assert.AreEqual(SafetyStatus.Unknown, result.Status, "Default status should be Unknown");
            Assert.IsFalse(result.IsValid, "Default IsValid should be false");
            Assert.IsNull(result.Message, "Default Message should be null");
            Assert.IsNull(result.FailureReason, "Default FailureReason should be null");
            Assert.AreEqual(0, result.ConfidenceLevel, "Default ConfidenceLevel should be 0");
        }

        [TestMethod]
        public void Success_WithDefaultMessage_ShouldCreateSuccessfulResult()
        {
            // Act
            var result = SafetyValidationResult.Success();

            // Assert
            Assert.IsTrue(result.IsValid, "Success result should be valid");
            Assert.AreEqual(SafetyStatus.Safe, result.Status, "Success result should have Safe status");
            Assert.AreEqual("Safety validation passed", result.Message, "Should have default success message");
            Assert.AreEqual(SafetyValidationAction.Allow, result.RecommendedAction, "Success result should Allow action");
            Assert.AreEqual(1.0, result.ConfidenceLevel, "Success result should have full confidence");
            Assert.IsTrue(result.CanProceed, "Success result should allow proceeding");
            Assert.IsFalse(result.RequiresUserConfirmation, "Success result should not require confirmation");
            Assert.AreEqual(0, result.Errors.Count, "Success result should have no errors");
        }

        [TestMethod]
        public void Success_WithCustomMessage_ShouldCreateSuccessfulResult()
        {
            // Arrange
            var customMessage = "Custom success message";

            // Act
            var result = SafetyValidationResult.Success(customMessage);

            // Assert
            Assert.IsTrue(result.IsValid, "Success result should be valid");
            Assert.AreEqual(SafetyStatus.Safe, result.Status, "Success result should have Safe status");
            Assert.AreEqual(customMessage, result.Message, "Should have custom success message");
            Assert.AreEqual(SafetyValidationAction.Allow, result.RecommendedAction, "Success result should Allow action");
            Assert.AreEqual(1.0, result.ConfidenceLevel, "Success result should have full confidence");
        }

        [TestMethod]
        public void Failure_WithDefaultAction_ShouldCreateFailureResult()
        {
            // Arrange
            var failureReason = "Test failure reason";

            // Act
            var result = SafetyValidationResult.Failure(failureReason);

            // Assert
            Assert.IsFalse(result.IsValid, "Failure result should be invalid");
            Assert.AreEqual(SafetyStatus.Unsafe, result.Status, "Failure result should have Unsafe status");
            Assert.AreEqual("Safety validation failed", result.Message, "Should have default failure message");
            Assert.AreEqual(failureReason, result.FailureReason, "Should have specified failure reason");
            Assert.AreEqual(SafetyValidationAction.Block, result.RecommendedAction, "Failure result should Block action");
            Assert.AreEqual(1.0, result.ConfidenceLevel, "Failure result should have full confidence");
            Assert.IsFalse(result.CanProceed, "Failure result should not allow proceeding");
            Assert.AreEqual(1, result.Errors.Count, "Failure result should have one error");
            Assert.AreEqual(failureReason, result.Errors[0], "Error should match failure reason");
        }

        [TestMethod]
        public void Failure_WithCustomAction_ShouldCreateFailureResult()
        {
            // Arrange
            var failureReason = "Test failure reason";
            var customAction = SafetyValidationAction.Warn;

            // Act
            var result = SafetyValidationResult.Failure(failureReason, customAction);

            // Assert
            Assert.IsFalse(result.IsValid, "Failure result should be invalid");
            Assert.AreEqual(SafetyStatus.Unsafe, result.Status, "Failure result should have Unsafe status");
            Assert.AreEqual(customAction, result.RecommendedAction, "Failure result should use custom action");
        }

        [TestMethod]
        public void Warning_WithDefaultValues_ShouldCreateWarningResult()
        {
            // Arrange
            var message = "Warning message";

            // Act
            var result = SafetyValidationResult.Warning(message);

            // Assert
            Assert.IsTrue(result.IsValid, "Warning result should be valid");
            Assert.AreEqual(SafetyStatus.Warning, result.Status, "Warning result should have Warning status");
            Assert.AreEqual(message, result.Message, "Should have specified message");
            Assert.AreEqual(SafetyValidationAction.Warn, result.RecommendedAction, "Warning result should Warn action");
            Assert.AreEqual(0.8, result.ConfidenceLevel, "Warning result should have 0.8 confidence");
            Assert.IsTrue(result.CanProceed, "Warning result should allow proceeding");
            Assert.AreEqual(0, result.Errors.Count, "Warning result should have no errors");
        }

        [TestMethod]
        public void Warning_WithCustomWarnings_ShouldCreateWarningResult()
        {
            // Arrange
            var message = "Warning message";
            var warnings = new List<string> { "Warning 1", "Warning 2" };

            // Act
            var result = SafetyValidationResult.Warning(message, warnings);

            // Assert
            Assert.IsTrue(result.IsValid, "Warning result should be valid");
            Assert.AreEqual(SafetyStatus.Warning, result.Status, "Warning result should have Warning status");
            Assert.AreEqual(2, result.Warnings.Count, "Warning result should have 2 warnings");
            CollectionAssert.AreEqual(warnings, result.Warnings, "Warnings should match input");
        }

        [TestMethod]
        public void AddValidationCheck_WithValidInput_ShouldAddCheck()
        {
            // Arrange
            var result = new SafetyValidationResult();
            var checkName = "TestCheck";
            var passed = true;
            var message = "Test message";
            var details = new { TestValue = 123 };

            // Act
            result.AddValidationCheck(checkName, passed, message, details);

            // Assert
            Assert.AreEqual(1, result.ValidationChecks.Count, "Should have one validation check");
            Assert.IsTrue(result.ValidationChecks.ContainsKey(checkName), "Should contain the check by name");

            var check = result.ValidationChecks[checkName];
            Assert.AreEqual(checkName, check.CheckName, "Check name should match");
            Assert.AreEqual(passed, check.Passed, "Passed status should match");
            Assert.AreEqual(message, check.Message, "Message should match");
            Assert.AreEqual(details, check.Details, "Details should match");
            Assert.AreEqual(DateTime.UtcNow.Date, check.Timestamp.Date, "Timestamp should be set");
        }

        [TestMethod]
        public void AddValidationCheck_WithoutDetails_ShouldAddCheckWithNullDetails()
        {
            // Arrange
            var result = new SafetyValidationResult();
            var checkName = "TestCheck";

            // Act
            result.AddValidationCheck(checkName, true, "Test message");

            // Assert
            Assert.AreEqual(1, result.ValidationChecks.Count, "Should have one validation check");
            var check = result.ValidationChecks[checkName];
            Assert.IsNull(check.Details, "Details should be null when not provided");
        }

        [TestMethod]
        public void AddWarning_WithValidInput_ShouldAddWarning()
        {
            // Arrange
            var result = new SafetyValidationResult();
            var warning = "Test warning message";

            // Act
            result.AddWarning(warning);

            // Assert
            Assert.AreEqual(1, result.Warnings.Count, "Should have one warning");
            Assert.AreEqual(warning, result.Warnings[0], "Warning message should match");
        }

        [TestMethod]
        public void AddWarning_MultipleWarnings_ShouldAddAll()
        {
            // Arrange
            var result = new SafetyValidationResult();
            var warnings = new[] { "Warning 1", "Warning 2", "Warning 3" };

            // Act
            foreach (var warning in warnings)
            {
                result.AddWarning(warning);
            }

            // Assert
            Assert.AreEqual(3, result.Warnings.Count, "Should have three warnings");
            CollectionAssert.AreEqual(warnings, result.Warnings, "All warnings should be added");
        }

        [TestMethod]
        public void AddError_WithValidInput_ShouldAddError()
        {
            // Arrange
            var result = new SafetyValidationResult();
            var error = "Test error message";

            // Act
            result.AddError(error);

            // Assert
            Assert.AreEqual(1, result.Errors.Count, "Should have one error");
            Assert.AreEqual(error, result.Errors[0], "Error message should match");
        }

        [TestMethod]
        public void AddError_MultipleErrors_ShouldAddAll()
        {
            // Arrange
            var result = new SafetyValidationResult();
            var errors = new[] { "Error 1", "Error 2", "Error 3" };

            // Act
            foreach (var error in errors)
            {
                result.AddError(error);
            }

            // Assert
            Assert.AreEqual(3, result.Errors.Count, "Should have three errors");
            CollectionAssert.AreEqual(errors, result.Errors, "All errors should be added");
        }

        [TestMethod]
        public void UpdateOverallStatus_AllChecksPassed_ShouldSetSafeStatus()
        {
            // Arrange
            var result = new SafetyValidationResult();
            result.AddValidationCheck("Check1", true, "Check 1 passed");
            result.AddValidationCheck("Check2", true, "Check 2 passed");
            result.AddValidationCheck("Check3", true, "Check 3 passed");

            // Act
            result.UpdateOverallStatus();

            // Assert
            Assert.AreEqual(SafetyStatus.Safe, result.Status, "Status should be Safe when all checks pass");
            Assert.AreEqual(SafetyValidationAction.Allow, result.RecommendedAction, "Should Allow when all checks pass");
            Assert.IsTrue(result.IsValid, "Should be valid when all checks pass");
        }

        [TestMethod]
        public void UpdateOverallStatus_NonCriticalFailures_ShouldSetWarningStatus()
        {
            // Arrange
            var result = new SafetyValidationResult();
            result.AddValidationCheck("Check1", true, "Check 1 passed");
            result.AddValidationCheck("Check2", false, "Check 2 failed"); // Non-critical failure
            result.AddValidationCheck("Check3", true, "Check 3 passed");

            // Act
            result.UpdateOverallStatus();

            // Assert
            Assert.AreEqual(SafetyStatus.Warning, result.Status, "Status should be Warning for non-critical failures");
            Assert.AreEqual(SafetyValidationAction.Warn, result.RecommendedAction, "Should Warn for non-critical failures");
            Assert.IsTrue(result.IsValid, "Should be valid for non-critical failures");
        }

        [TestMethod]
        public void UpdateOverallStatus_CriticalFailures_ShouldSetUnsafeStatus()
        {
            // Arrange
            var result = new SafetyValidationResult();
            result.AddValidationCheck("Check1", true, "Check 1 passed");
            result.AddValidationCheck("Check2", false, "Check 2 failed", isCritical: true); // Critical failure
            result.AddValidationCheck("Check3", true, "Check 3 passed");

            // Act
            result.UpdateOverallStatus();

            // Assert
            Assert.AreEqual(SafetyStatus.Unsafe, result.Status, "Status should be Unsafe for critical failures");
            Assert.AreEqual(SafetyValidationAction.Block, result.RecommendedAction, "Should Block for critical failures");
            Assert.IsFalse(result.IsValid, "Should be invalid for critical failures");
        }

        [TestMethod]
        public void UpdateOverallStatus_MixedFailures_ShouldSetUnsafeStatus()
        {
            // Arrange
            var result = new SafetyValidationResult();
            result.AddValidationCheck("Check1", false, "Check 1 failed", isCritical: true); // Critical failure
            result.AddValidationCheck("Check2", false, "Check 2 failed"); // Non-critical failure
            result.AddValidationCheck("Check3", true, "Check 3 passed");

            // Act
            result.UpdateOverallStatus();

            // Assert
            Assert.AreEqual(SafetyStatus.Unsafe, result.Status, "Status should be Unsafe when there are critical failures");
            Assert.AreEqual(SafetyValidationAction.Block, result.RecommendedAction, "Should Block when there are critical failures");
            Assert.IsFalse(result.IsValid, "Should be invalid when there are critical failures");
        }

        [TestMethod]
        public void UpdateOverallStatus_NoChecks_ShouldSetUnknownStatus()
        {
            // Arrange
            var result = new SafetyValidationResult();

            // Act
            result.UpdateOverallStatus();

            // Assert
            Assert.AreEqual(SafetyStatus.Unknown, result.Status, "Status should be Unknown when there are no checks");
            Assert.AreEqual(SafetyValidationAction.RequireConfirmation, result.RecommendedAction, "Should RequireConfirmation when there are no checks");
            Assert.IsTrue(result.IsValid, "Should be valid when there are no checks");
        }

        [TestMethod]
        public void CanProceed_ShouldReturnTrueForValidResults()
        {
            // Arrange
            var result = SafetyValidationResult.Success();

            // Assert
            Assert.IsTrue(result.CanProceed, "CanProceed should be true for valid results");
        }

        [TestMethod]
        public void CanProceed_ShouldReturnFalseForInvalidResults()
        {
            // Arrange
            var result = SafetyValidationResult.Failure("Test failure");

            // Assert
            Assert.IsFalse(result.CanProceed, "CanProceed should be false for invalid results");
        }

        [TestMethod]
        public void CanProceed_ShouldReturnTrueForWarningResults()
        {
            // Arrange
            var result = SafetyValidationResult.Warning("Test warning");

            // Assert
            Assert.IsTrue(result.CanProceed, "CanProceed should be true for warning results");
        }

        [TestMethod]
        public void RequiresUserConfirmation_ShouldReturnTrueForRequireConfirmationAction()
        {
            // Arrange
            var result = new SafetyValidationResult
            {
                RecommendedAction = SafetyValidationAction.RequireConfirmation
            };

            // Assert
            Assert.IsTrue(result.RequiresUserConfirmation, "RequiresUserConfirmation should be true for RequireConfirmation action");
        }

        [TestMethod]
        public void RequiresUserConfirmation_ShouldReturnFalseForOtherActions()
        {
            // Arrange
            var actions = new[] { SafetyValidationAction.Allow, SafetyValidationAction.Warn, SafetyValidationAction.Block };

            foreach (var action in actions)
            {
                var result = new SafetyValidationResult
                {
                    RecommendedAction = action
                };

                // Assert
                Assert.IsFalse(result.RequiresUserConfirmation, $"RequiresUserConfirmation should be false for {action} action");
            }
        }

        [TestMethod]
        public void SafetyCheckResult_DefaultValues_ShouldInitializeProperly()
        {
            // Arrange & Act
            var checkResult = new SafetyCheckResult
            {
                CheckName = "TestCheck",
                Passed = true,
                Message = "Test message"
            };

            // Assert
            Assert.AreEqual("TestCheck", checkResult.CheckName);
            Assert.IsTrue(checkResult.Passed);
            Assert.AreEqual("Test message", checkResult.Message);
            Assert.IsNull(checkResult.Details);
            Assert.IsFalse(checkResult.IsCritical);
            Assert.AreEqual(DateTime.UtcNow.Date, checkResult.Timestamp.Date);
        }

        [TestMethod]
        public void SafetyStatus_ShouldSupportAllRequiredStatuses()
        {
            // Assert - verify enum values exist
            Assert.AreEqual(0, (int)SafetyStatus.Safe);
            Assert.AreEqual(1, (int)SafetyStatus.Warning);
            Assert.AreEqual(2, (int)SafetyStatus.Unsafe);
            Assert.AreEqual(3, (int)SafetyStatus.Unknown);
        }
    }
}