using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    [TestClass]
    public class TimerExecutionOptionsVersionTests
    {
        [TestMethod]
        public void Constructor_ShouldInitializeWithDefaultVersionSettings()
        {
            // Act
            var options = new TimerExecutionOptions();

            // Assert
            Assert.AreEqual(string.Empty, options.TargetVersion);
            Assert.AreEqual(string.Empty, options.DefaultVersion);
            Assert.AreEqual(false, options.EnableVersionSpecificConfig);
            Assert.AreEqual(TimeSpan.FromMinutes(30), options.VersionDetectionInterval);
            Assert.AreEqual(true, options.EnableVersionFallback);
            Assert.IsNotNull(options.SupportedVersions);
            Assert.AreEqual(0, options.SupportedVersions.Count);
        }

        [TestMethod]
        public void TargetVersion_Setter_ValidVersion_ShouldAcceptValue()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act
            options.TargetVersion = "2024";

            // Assert
            Assert.AreEqual("2024", options.TargetVersion);
        }

        [TestMethod]
        public void TargetVersion_Setter_InvalidVersionFormat_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => options.TargetVersion = "202a");
            Assert.ThrowsException<ArgumentException>(() => options.TargetVersion = "1999");
            Assert.ThrowsException<ArgumentException>(() => options.TargetVersion = "2031");
            Assert.ThrowsException<ArgumentException>(() => options.TargetVersion = "abcd");
        }

        [TestMethod]
        public void TargetVersion_Setter_NullValue_ShouldSetEmptyString()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act
            options.TargetVersion = null;

            // Assert
            Assert.AreEqual(string.Empty, options.TargetVersion);
        }

        [TestMethod]
        public void DefaultVersion_Setter_ValidVersion_ShouldAcceptValue()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act
            options.DefaultVersion = "2025";

            // Assert
            Assert.AreEqual("2025", options.DefaultVersion);
        }

        [TestMethod]
        public void DefaultVersion_Setter_InvalidVersionFormat_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => options.DefaultVersion = "202b");
            Assert.ThrowsException<ArgumentException>(() => options.DefaultVersion = "2019");
            Assert.ThrowsException<ArgumentException>(() => options.DefaultVersion = "2032");
        }

        [TestMethod]
        public void SupportedVersions_Setter_ValidVersions_ShouldAcceptList()
        {
            // Arrange
            var options = new TimerExecutionOptions();
            var versions = new List<string> { "2023", "2024", "2025" };

            // Act
            options.SupportedVersions = versions;

            // Assert
            CollectionAssert.AreEqual(versions, options.SupportedVersions);
        }

        [TestMethod]
        public void SupportedVersions_Setter_InvalidVersion_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new TimerExecutionOptions();
            var versions = new List<string> { "2023", "202c", "2025" };

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => options.SupportedVersions = versions);
        }

        [TestMethod]
        public void SupportedVersions_Setter_NullValue_ShouldSetEmptyList()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act
            options.SupportedVersions = null;

            // Assert
            Assert.IsNotNull(options.SupportedVersions);
            Assert.AreEqual(0, options.SupportedVersions.Count);
        }

        [TestMethod]
        public void VersionDetectionInterval_Setter_ValidRange_ShouldAcceptValue()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act
            options.VersionDetectionInterval = TimeSpan.FromMinutes(15);

            // Assert
            Assert.AreEqual(TimeSpan.FromMinutes(15), options.VersionDetectionInterval);
        }

        [TestMethod]
        public void VersionDetectionInterval_Setter_ZeroOrNegative_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new TimerExecutionOptions();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => options.VersionDetectionInterval = TimeSpan.Zero);
            Assert.ThrowsException<ArgumentException>(() => options.VersionDetectionInterval = TimeSpan.FromSeconds(-1));
        }

        [TestMethod]
        public void Validate_WithValidVersionSpecificConfig_ShouldReturnNoErrors()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromMinutes(15)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_WithEnabledVersionSpecificConfigButNoVersions_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = true,
                TargetVersion = "",
                DefaultVersion = "",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromMinutes(15)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Either TargetVersion or DefaultVersion must be specified")));
        }

        [TestMethod]
        public void Validate_WithTargetVersionNotInSupportedList_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = true,
                TargetVersion = "2026",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromMinutes(15)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Target version 2026 is not in the list of supported versions")));
        }

        [TestMethod]
        public void Validate_WithInvalidTargetVersionFormat_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = true,
                TargetVersion = "202a",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromMinutes(15)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Invalid target version")));
        }

        [TestMethod]
        public void Validate_WithInvalidDetectionInterval_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromSeconds(30)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Version detection interval must be at least 1 minute")));
        }

        [TestMethod]
        public void Validate_WithTooLongDetectionInterval_ShouldReturnError()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromHours(25)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Version detection interval should not exceed 24 hours")));
        }

        [TestMethod]
        public void Validate_WithDisabledVersionSpecificConfig_ShouldIgnoreVersionSettings()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = false,
                TargetVersion = "invalid",
                DefaultVersion = "also_invalid",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromMinutes(15)
            };

            // Act
            var errors = options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Count, "Should not validate version settings when disabled");
        }

        [TestMethod]
        public void Clone_ShouldCopyAllVersionSpecificProperties()
        {
            // Arrange
            var original = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromMinutes(15),
                EnableVersionFallback = false
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.AreEqual(original.EnableVersionSpecificConfig, clone.EnableVersionSpecificConfig);
            Assert.AreEqual(original.TargetVersion, clone.TargetVersion);
            Assert.AreEqual(original.DefaultVersion, clone.DefaultVersion);
            Assert.AreEqual(original.VersionDetectionInterval, clone.VersionDetectionInterval);
            Assert.AreEqual(original.EnableVersionFallback, clone.EnableVersionFallback);
            CollectionAssert.AreEqual(original.SupportedVersions, clone.SupportedVersions);
        }

        [TestMethod]
        public void ToString_ShouldIncludeVersionSpecificInformation()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                EnableVersionSpecificConfig = true,
                TargetVersion = "2024",
                DefaultVersion = "2023",
                SupportedVersions = new List<string> { "2023", "2024", "2025" },
                VersionDetectionInterval = TimeSpan.FromMinutes(15),
                EnableVersionFallback = false,
                DefaultInterval = TimeSpan.FromMinutes(2)
            };

            // Act
            var result = options.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Enable Version Specific Config: True"));
            Assert.IsTrue(result.Contains("Target Version: 2024"));
            Assert.IsTrue(result.Contains("Default Version: 2023"));
            Assert.IsTrue(result.Contains("Supported Versions: 2023, 2024, 2025"));
            Assert.IsTrue(result.Contains("Version Detection Interval: 15.0m"));
            Assert.IsTrue(result.Contains("Enable Version Fallback: False"));
        }

        [TestMethod]
        public void GetDefaultSupportedVersions_ShouldReturnExpectedVersions()
        {
            // Act
            var versions = TimerExecutionOptions.GetDefaultSupportedVersions();

            // Assert
            Assert.IsNotNull(versions);
            Assert.AreEqual(6, versions.Count);
            CollectionAssert.Contains(versions, "2020");
            CollectionAssert.Contains(versions, "2021");
            CollectionAssert.Contains(versions, "2022");
            CollectionAssert.Contains(versions, "2023");
            CollectionAssert.Contains(versions, "2024");
            CollectionAssert.Contains(versions, "2025");
        }

        [TestMethod]
        public void SolidWorks2025Options_ShouldReturnOptimizedConfiguration()
        {
            // Act
            var options = TimerExecutionOptions.SolidWorks2025Options();

            // Assert
            Assert.AreEqual("2025", options.TargetVersion);
            Assert.IsTrue(options.EnableVersionSpecificConfig);
            Assert.AreEqual(TimeSpan.FromMinutes(3), options.DefaultInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(4), options.ExecutionTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(8), options.CircuitBreakerCooldown);
            Assert.AreEqual(75, options.MaxExecutionHistory);
            Assert.AreEqual("2024", options.DefaultVersion);
            Assert.IsTrue(options.EnableVersionFallback);
        }

        [TestMethod]
        public void SolidWorks2024Options_ShouldReturnOptimizedConfiguration()
        {
            // Act
            var options = TimerExecutionOptions.SolidWorks2024Options();

            // Assert
            Assert.AreEqual("2024", options.TargetVersion);
            Assert.IsTrue(options.EnableVersionSpecificConfig);
            Assert.AreEqual(TimeSpan.FromMinutes(4), options.DefaultInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(5), options.ExecutionTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(10), options.CircuitBreakerCooldown);
            Assert.AreEqual(60, options.MaxExecutionHistory);
            Assert.AreEqual("2023", options.DefaultVersion);
        }

        [TestMethod]
        public void MultiVersionOptions_ShouldReturnOptimizedConfiguration()
        {
            // Act
            var options = TimerExecutionOptions.MultiVersionOptions();

            // Assert
            Assert.IsTrue(options.EnableVersionSpecificConfig);
            Assert.AreEqual(TimeSpan.FromMinutes(5), options.DefaultInterval);
            Assert.AreEqual(2, options.MaxConcurrentExecutions);
            Assert.AreEqual(TimeSpan.FromHours(2), options.MaxInterval);
            Assert.IsFalse(options.PreventExecutionOverlap);
            Assert.AreEqual("2024", options.DefaultVersion);
            Assert.IsTrue(options.EnableVersionFallback);
            Assert.AreEqual(150, options.MaxExecutionHistory);
        }

        [TestMethod]
        public void VersionSpecificOptions_ShouldPassValidation()
        {
            // Arrange
            var optionsList = new[]
            {
                TimerExecutionOptions.SolidWorks2025Options(),
                TimerExecutionOptions.SolidWorks2024Options(),
                TimerExecutionOptions.MultiVersionOptions()
            };

            // Act & Assert
            foreach (var options in optionsList)
            {
                var errors = options.Validate();
                Assert.AreEqual(0, errors.Count, $"Options should be valid: {options}");
            }
        }
    }
}