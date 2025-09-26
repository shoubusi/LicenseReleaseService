using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.VersionManagement.Deployment;
using LicenseReleaseService.VersionManagement.Models;

namespace LicenseReleaseService.Tests.VersionManagement.Deployment
{
    public class VersionDeploymentValidatorTests : IDisposable
    {
        private readonly Mock<ILogger<VersionDeploymentValidator>> _mockLogger;
        private readonly Mock<IOptions<VersionDeploymentValidationOptions>> _mockOptions;
        private readonly VersionDeploymentValidationOptions _validationOptions;
        private readonly VersionDeploymentValidator _deploymentValidator;
        private readonly ITestOutputHelper _output;

        public VersionDeploymentValidatorTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<VersionDeploymentValidator>>();
            _validationOptions = new VersionDeploymentValidationOptions
            {
                Enabled = true,
                ValidatePrerequisites = true,
                ValidateEnvironment = true,
                ValidateDependencies = true,
                ValidatePermissions = true,
                ValidateResources = true,
                ValidateNetwork = true,
                ValidateConfiguration = true,
                ValidateSecurity = true,
                ValidatePerformance = true,
                FailFast = true,
                EnableCustomValidators = true,
                ParallelValidation = true,
                Timeout = TimeSpan.FromMinutes(5),
                CustomValidationRules = new List<CustomValidationRule>()
            };

            _mockOptions = new Mock<IOptions<VersionDeploymentValidationOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(_validationOptions);

            _deploymentValidator = new VersionDeploymentValidator(_mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithValidInputs_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true,
                LicenseCount = 10,
                MaxLicenseCount = 10,
                LastHealthCheck = DateTime.UtcNow
            };

            var deploymentOptions = new VersionDeploymentOptions
            {
                TargetEnvironment = "Production",
                SkipValidation = false,
                ForceDeployment = false,
                RollbackOnFailure = true
            };

            // Act
            var result = await _deploymentValidator.ValidateDeploymentAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            Assert.True(result.ValidationResults.Count > 0);
            _output.WriteLine($"Validation completed successfully with {result.ValidationResults.Count} checks");

            // Verify that all validations passed
            foreach (var validationResult in result.ValidationResults)
            {
                _output.WriteLine($"  {validationResult.Category}.{validationResult.Name}: {validationResult.Status} - {validationResult.Message}");
            }
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithNullVersionInfo_ShouldThrowArgumentNullException()
        {
            // Arrange
            var deploymentOptions = new VersionDeploymentOptions();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _deploymentValidator.ValidateDeploymentAsync(null!, deploymentOptions));

            Assert.Equal("Value cannot be null. (Parameter 'versionInfo')", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithNullDeploymentOptions_ShouldThrowArgumentNullException()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _deploymentValidator.ValidateDeploymentAsync(versionInfo, null!));

            Assert.Equal("Value cannot be null. (Parameter 'deploymentOptions')", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithUnlicensedVersion_ShouldFailValidation()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = false, // This should cause validation to fail
                LicenseCount = 0,
                MaxLicenseCount = 10
            };

            var deploymentOptions = new VersionDeploymentOptions
            {
                TargetEnvironment = "Production"
            };

            // Act
            var result = await _deploymentValidator.ValidateDeploymentAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ValidationResults);
            Assert.Contains(result.ValidationResults, r => r.Status == ValidationStatus.Failed);
            _output.WriteLine($"Validation failed as expected for unlicensed version");

            // Find and log the failed validations
            var failedValidations = result.ValidationResults.Where(r => r.Status == ValidationStatus.Failed);
            foreach (var failedValidation in failedValidations)
            {
                _output.WriteLine($"  Failed: {failedValidation.Category}.{failedValidation.Name}: {failedValidation.Message}");
            }
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithSkipValidation_ShouldSkipAllChecks()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = false, // This would normally fail validation
                IsLicensed = false    // This would normally fail validation
            };

            var deploymentOptions = new VersionDeploymentOptions
            {
                TargetEnvironment = "Production",
                SkipValidation = true // This should skip all validation
            };

            // Act
            var result = await _deploymentValidator.ValidateDeploymentAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Validation skipped", result.Message);
            _output.WriteLine("Validation was skipped as expected");
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithForceDeployment_ShouldSucceedDespiteFailures()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = false, // This would normally fail validation
                IsLicensed = false    // This would normally fail validation
            };

            var deploymentOptions = new VersionDeploymentOptions
            {
                TargetEnvironment = "Production",
                ForceDeployment = true // This should force deployment despite validation failures
            };

            // Act
            var result = await _deploymentValidator.ValidateDeploymentAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Force deployment enabled", result.Message);
            _output.WriteLine("Validation passed due to force deployment flag");

            // There should still be validation results showing failures
            Assert.NotNull(result.ValidationResults);
            Assert.Contains(result.ValidationResults, r => r.Status == ValidationStatus.Failed);
        }

        [Fact]
        public async Task ValidateDeploymentPrerequisitesAsync_WithValidVersion_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true,
                LicenseCount = 5,
                MaxLicenseCount = 10
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentPrerequisitesAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Prerequisites validation completed with {result.ValidationResults.Count} checks");

            // Verify all prerequisite checks passed
            foreach (var validationResult in result.ValidationResults)
            {
                Assert.True(validationResult.Status == ValidationStatus.Passed ||
                           validationResult.Status == ValidationStatus.Warning,
                           $"Prerequisite check {validationResult.Name} should pass");
            }
        }

        [Fact]
        public async Task ValidateDeploymentPrerequisitesAsync_WithInsufficientLicenses_ShouldFail()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true,
                LicenseCount = 0, // Insufficient licenses
                MaxLicenseCount = 10
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentPrerequisitesAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ValidationResults);
            Assert.Contains(result.ValidationResults, r =>
                r.Status == ValidationStatus.Failed &&
                r.Name.Contains("License"));
            _output.WriteLine("Prerequisites validation failed as expected due to insufficient licenses");
        }

        [Fact]
        public async Task ValidateDeploymentEnvironmentAsync_WithValidEnvironment_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions
            {
                TargetEnvironment = "Production"
            };

            // Act
            var result = await _deploymentValidator.ValidateDeploymentEnvironmentAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Environment validation completed with {result.ValidationResults.Count} checks");
        }

        [Fact]
        public async Task ValidateDeploymentDependenciesAsync_WithValidDependencies_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentDependenciesAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Dependencies validation completed with {result.ValidationResults.Count} checks");
        }

        [Fact]
        public async Task ValidateDeploymentPermissionsAsync_WithValidPermissions_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentPermissionsAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Permissions validation completed with {result.ValidationResults.Count} checks");
        }

        [Fact]
        public async Task ValidateDeploymentResourcesAsync_WithValidResources_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentResourcesAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Resources validation completed with {result.ValidationResults.Count} checks");
        }

        [Fact]
        public async Task ValidateDeploymentNetworkAsync_WithValidNetwork_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentNetworkAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Network validation completed with {result.ValidationResults.Count} checks");
        }

        [Fact]
        public async Task ValidateDeploymentConfigurationAsync_WithValidConfiguration_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentConfigurationAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Configuration validation completed with {result.ValidationResults.Count} checks");
        }

        [Fact]
        public async Task ValidateDeploymentSecurityAsync_WithValidSecurity_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentSecurityAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Security validation completed with {result.ValidationResults.Count} checks");
        }

        [Fact]
        public async Task ValidateDeploymentPerformanceAsync_WithValidPerformance_ShouldSucceed()
        {
            // Arrange
            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Act
            var result = await _deploymentValidator.ValidateDeploymentPerformanceAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            _output.WriteLine($"Performance validation completed with {result.ValidationResults.Count} checks");
        }

        [Fact]
        public async Task AddCustomValidationRuleAsync_WithValidRule_ShouldSucceed()
        {
            // Arrange
            var customRule = new CustomValidationRule
            {
                Name = "CustomTestRule",
                Description = "Test custom validation rule",
                Category = "Custom",
                Severity = ValidationSeverity.Warning,
                Condition = "true",
                Enabled = true
            };

            // Act
            var result = await _deploymentValidator.AddCustomValidationRuleAsync(customRule);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Rule);
            Assert.Equal(customRule.Name, result.Rule.Name);
            _output.WriteLine($"Custom validation rule added successfully: {customRule.Name}");
        }

        [Fact]
        public async Task AddCustomValidationRuleAsync_WithNullRule_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _deploymentValidator.AddCustomValidationRuleAsync(null!));

            Assert.Equal("Value cannot be null. (Parameter 'rule')", exception.Message);
            _output.WriteLine($"Expected exception thrown: {exception.Message}");
        }

        [Fact]
        public async Task AddCustomValidationRuleAsync_WithDuplicateName_ShouldFail()
        {
            // Arrange
            var customRule = new CustomValidationRule
            {
                Name = "DuplicateRule",
                Description = "Test duplicate validation rule",
                Category = "Custom",
                Severity = ValidationSeverity.Warning,
                Condition = "true",
                Enabled = true
            };

            // Add the rule first time
            await _deploymentValidator.AddCustomValidationRuleAsync(customRule);

            // Act - try to add the same rule again
            var result = await _deploymentValidator.AddCustomValidationRuleAsync(customRule);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message);
            _output.WriteLine($"Duplicate rule addition failed as expected: {result.Message}");
        }

        [Fact]
        public async Task RemoveCustomValidationRuleAsync_WithValidRuleId_ShouldSucceed()
        {
            // Arrange
            var customRule = new CustomValidationRule
            {
                Name = "RuleToRemove",
                Description = "Test rule to remove",
                Category = "Custom",
                Severity = ValidationSeverity.Warning,
                Condition = "true",
                Enabled = true
            };

            // Add the rule first
            var addResult = await _deploymentValidator.AddCustomValidationRuleAsync(customRule);
            Assert.True(addResult.Success);

            // Act
            var result = await _deploymentValidator.RemoveCustomValidationRuleAsync(addResult.Rule!.Id);

            // Assert
            Assert.True(result.Success);
            _output.WriteLine($"Custom validation rule removed successfully: {customRule.Name}");
        }

        [Fact]
        public async Task RemoveCustomValidationRuleAsync_WithInvalidRuleId_ShouldFail()
        {
            // Arrange
            var invalidRuleId = "invalid-rule-id";

            // Act
            var result = await _deploymentValidator.RemoveCustomValidationRuleAsync(invalidRuleId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message);
            _output.WriteLine($"Invalid rule removal failed as expected: {result.Message}");
        }

        [Fact]
        public async Task GetCustomValidationRulesAsync_ShouldReturnAllRules()
        {
            // Arrange
            var rule1 = new CustomValidationRule
            {
                Name = "TestRule1",
                Description = "Test rule 1",
                Category = "Custom",
                Severity = ValidationSeverity.Warning,
                Condition = "true",
                Enabled = true
            };

            var rule2 = new CustomValidationRule
            {
                Name = "TestRule2",
                Description = "Test rule 2",
                Category = "Custom",
                Severity = ValidationSeverity.Error,
                Condition = "false",
                Enabled = true
            };

            // Add rules
            await _deploymentValidator.AddCustomValidationRuleAsync(rule1);
            await _deploymentValidator.AddCustomValidationRuleAsync(rule2);

            // Act
            var result = await _deploymentValidator.GetCustomValidationRulesAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Rules);
            Assert.True(result.Rules.Count >= 2);
            _output.WriteLine($"Retrieved {result.Rules.Count} custom validation rules");

            // Verify our test rules are included
            Assert.Contains(result.Rules, r => r.Name == "TestRule1");
            Assert.Contains(result.Rules, r => r.Name == "TestRule2");
        }

        [Fact]
        public async Task ExecuteCustomValidationRulesAsync_WithValidRules_ShouldExecuteSuccessfully()
        {
            // Arrange
            var customRule = new CustomValidationRule
            {
                Name = "ExecutionTestRule",
                Description = "Test rule execution",
                Category = "Custom",
                Severity = ValidationSeverity.Warning,
                Condition = "true", // This should always pass
                Enabled = true
            };

            var versionInfo = new SolidWorksVersionInfo
            {
                Version = "2023",
                InstallPath = @"C:\SolidWorks\2023",
                IsInstalled = true,
                IsLicensed = true
            };

            var deploymentOptions = new VersionDeploymentOptions();

            // Add the rule
            await _deploymentValidator.AddCustomValidationRuleAsync(customRule);

            // Act
            var result = await _deploymentValidator.ExecuteCustomValidationRulesAsync(versionInfo, deploymentOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ValidationResults);
            Assert.Contains(result.ValidationResults, r => r.Name == "ExecutionTestRule");
            _output.WriteLine($"Custom validation rules executed successfully with {result.ValidationResults.Count} results");
        }

        [Fact]
        public void Dispose_WhenCalled_ShouldCleanUpResources()
        {
            // Arrange
            var customRule = new CustomValidationRule
            {
                Name = "CleanupTestRule",
                Description = "Test cleanup",
                Category = "Custom",
                Severity = ValidationSeverity.Warning,
                Condition = "true",
                Enabled = true
            };

            // Add a rule
            _deploymentValidator.AddCustomValidationRuleAsync(customRule).Wait();

            // Act
            _deploymentValidator.Dispose();

            // Assert - verify that the validator is disposed and can't be used
            var exception = Assert.Throws<ObjectDisposedException>(() =>
                _deploymentValidator.GetCustomValidationRulesAsync());

            Assert.Equal("Cannot access a disposed object.\r\nObject name: 'VersionDeploymentValidator'.", exception.Message);
            _output.WriteLine("Deployment validator disposed successfully");
        }

        public void Dispose()
        {
            _deploymentValidator?.Dispose();
        }
    }
}