using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Configuration
{
    /// <summary>
    /// Unit tests for LicenseQueryElement
    /// </summary>
    [TestClass]
    public class LicenseQueryElementTests
    {
        private LicenseQueryElement _element;
        private string _testConfigPath;
        private string _originalConfigContent;

        [TestInitialize]
        public void TestInitialize()
        {
            _element = new LicenseQueryElement();
            _originalConfigContent = File.ReadAllText("App.config");
            _testConfigPath = Path.GetTempFileName();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        [TestMethod]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var element = new LicenseQueryElement();

            // Assert
            Assert.AreEqual(TimeSpan.FromMinutes(5), element.CacheExpiration);
            Assert.AreEqual(TimeSpan.FromSeconds(30), element.QueryTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(15), element.ParsingTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(20), element.ServerResponseTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(2), element.RetryDelay);
            Assert.AreEqual(3, element.MaxRetries);
            Assert.AreEqual(1000, element.MaxCacheSize);
            Assert.AreEqual(52428800, element.MaxMemoryUsage);
            Assert.AreEqual(10, element.MaxConcurrentQueries);
            Assert.AreEqual(10485760, element.MaxOutputSize);
            Assert.AreEqual(30, element.StatisticsRetentionDays);
            Assert.AreEqual(10000, element.MaxStatisticsEntries);
            Assert.IsTrue(element.EnableCaching);
            Assert.IsTrue(element.EnableStatistics);
            Assert.IsFalse(element.EnableVerboseOutput);
            Assert.IsTrue(element.EnableDetailedParsing);
            Assert.IsTrue(element.EnableErrorRecovery);
            Assert.IsTrue(element.EnableIncrementalUpdates);
            Assert.IsTrue(element.EnableFeatureBatching);
            Assert.IsTrue(element.EnableUserActivityTracking);
            Assert.IsTrue(element.EnableBorrowingTracking);
            Assert.IsTrue(element.EnableHealthMonitoring);
            Assert.IsTrue(element.EnablePerformanceMetrics);
            Assert.IsTrue(element.EnableAlerting);
            Assert.IsFalse(element.FailFastOnInvalidData);
            Assert.IsTrue(element.EnableAutoCleanup);
            Assert.IsFalse(element.EnableCompactOutput);
            Assert.AreEqual("Standard", element.DefaultOutputFormat);
            Assert.AreEqual("en-US", element.PreferredLanguage);
            Assert.AreEqual(90, element.AlertThreshold);
            Assert.AreEqual(3600, element.CleanupInterval);
            Assert.AreEqual(60, element.HealthCheckInterval);
            Assert.AreEqual(30, element.PerformanceMetricsInterval);
        }

        [TestMethod]
        public void MaxRetries_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 0, 1, 5, 10 };
            var invalidValues = new[] { -1, 11, 100 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxRetries = value;
                Assert.AreEqual(value, _element.MaxRetries);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxRetries = value);
            }
        }

        [TestMethod]
        public void MaxCacheSize_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 1000, 50000, 100000 };
            var invalidValues = new[] { 0, -1, 100001 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxCacheSize = value;
                Assert.AreEqual(value, _element.MaxCacheSize);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxCacheSize = value);
            }
        }

        [TestMethod]
        public void MaxMemoryUsage_ShouldValidateRange()
        {
            // Arrange
            var validValues = new long[] { 1048576, 52428800, 1073741824 };
            var invalidValues = new long[] { 1048575, -1, 1073741825 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxMemoryUsage = value;
                Assert.AreEqual(value, _element.MaxMemoryUsage);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxMemoryUsage = value);
            }
        }

        [TestMethod]
        public void MaxConcurrentQueries_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 10, 50, 100 };
            var invalidValues = new[] { 0, -1, 101 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxConcurrentQueries = value;
                Assert.AreEqual(value, _element.MaxConcurrentQueries);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxConcurrentQueries = value);
            }
        }

        [TestMethod]
        public void MaxOutputSize_ShouldValidateRange()
        {
            // Arrange
            var validValues = new long[] { 0, 10485760, 1073741824 };
            var invalidValues = new long[] { -1, 1073741825 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxOutputSize = value;
                Assert.AreEqual(value, _element.MaxOutputSize);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxOutputSize = value);
            }
        }

        [TestMethod]
        public void StatisticsRetentionDays_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 30, 180, 365 };
            var invalidValues = new[] { 0, -1, 366 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.StatisticsRetentionDays = value;
                Assert.AreEqual(value, _element.StatisticsRetentionDays);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.StatisticsRetentionDays = value);
            }
        }

        [TestMethod]
        public void MaxStatisticsEntries_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 10000, 50000, 100000 };
            var invalidValues = new[] { 0, -1, 100001 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.MaxStatisticsEntries = value;
                Assert.AreEqual(value, _element.MaxStatisticsEntries);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.MaxStatisticsEntries = value);
            }
        }

        [TestMethod]
        public void AlertThreshold_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 0, 50, 90, 100 };
            var invalidValues = new[] { -1, 101 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.AlertThreshold = value;
                Assert.AreEqual(value, _element.AlertThreshold);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.AlertThreshold = value);
            }
        }

        [TestMethod]
        public void CleanupInterval_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 3600, 43200, 86400 };
            var invalidValues = new[] { 0, -1, 86401 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.CleanupInterval = value;
                Assert.AreEqual(value, _element.CleanupInterval);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.CleanupInterval = value);
            }
        }

        [TestMethod]
        public void HealthCheckInterval_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 60, 1800, 3600 };
            var invalidValues = new[] { 0, -1, 3601 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.HealthCheckInterval = value;
                Assert.AreEqual(value, _element.HealthCheckInterval);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.HealthCheckInterval = value);
            }
        }

        [TestMethod]
        public void PerformanceMetricsInterval_ShouldValidateRange()
        {
            // Arrange
            var validValues = new[] { 1, 30, 300, 3600 };
            var invalidValues = new[] { 0, -1, 3601 };

            // Act & Assert - Valid values
            foreach (var value in validValues)
            {
                _element.PerformanceMetricsInterval = value;
                Assert.AreEqual(value, _element.PerformanceMetricsInterval);
            }

            // Act & Assert - Invalid values should throw
            foreach (var value in invalidValues)
            {
                Assert.ThrowsException<ConfigurationErrorsException>(() => _element.PerformanceMetricsInterval = value);
            }
        }

        [TestMethod]
        public void Validate_ShouldReturnEmptyListForValidConfiguration()
        {
            // Arrange
            var element = CreateValidLicenseQueryElement();

            // Act
            var errors = element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_ShouldDetectTimeoutRelationshipIssues()
        {
            // Arrange
            _element.QueryTimeout = TimeSpan.FromSeconds(10);
            _element.ParsingTimeout = TimeSpan.FromSeconds(15); // Greater than query timeout

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Query timeout should be greater than or equal to parsing timeout")));
        }

        [TestMethod]
        public void Validate_ShouldDetectServerResponseTimeoutIssues()
        {
            // Arrange
            _element.QueryTimeout = TimeSpan.FromSeconds(20);
            _element.ServerResponseTimeout = TimeSpan.FromSeconds(30); // Greater than query timeout

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Server response timeout should be less than or equal to query timeout")));
        }

        [TestMethod]
        public void Validate_ShouldDetectRetryLogicIssues()
        {
            // Arrange
            _element.MaxRetries = 3;
            _element.RetryDelay = TimeSpan.Zero;

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Retry delay should be greater than zero when max retries is greater than zero")));
        }

        [TestMethod]
        public void Validate_ShouldDetectMemoryLimitIssues()
        {
            // Arrange
            _element.MaxMemoryUsage = 2147483648; // 2GB, exceeds recommended limit

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max memory usage exceeds recommended limit of 1GB")));
        }

        [TestMethod]
        public void Validate_ShouldDetectConcurrentQueryLimitIssues()
        {
            // Arrange
            _element.MaxConcurrentQueries = 150; // Exceeds recommended limit

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max concurrent queries exceeds recommended limit of 100")));
        }

        [TestMethod]
        public void Validate_ShouldDetectOutputSizeLimitIssues()
        {
            // Arrange
            _element.MaxOutputSize = 209715200; // 200MB, exceeds recommended limit

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max output size exceeds recommended limit of 100MB")));
        }

        [TestMethod]
        public void Validate_ShouldDetectStatisticsRetentionIssues()
        {
            // Arrange
            _element.StatisticsRetentionDays = 400; // Exceeds recommended limit

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Statistics retention days exceeds recommended limit of 365")));
        }

        [TestMethod]
        public void Validate_ShouldDetectStatisticsEntriesLimitIssues()
        {
            // Arrange
            _element.MaxStatisticsEntries = 200000; // Exceeds recommended limit

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max statistics entries exceeds recommended limit of 100000")));
        }

        [TestMethod]
        public void Validate_ShouldDetectIntervalRelationshipIssues()
        {
            // Arrange
            _element.PerformanceMetricsInterval = 60;
            _element.HealthCheckInterval = 30; // Less than performance metrics interval

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Performance metrics interval should be less than or equal to health check interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectCleanupIntervalIssues()
        {
            // Arrange
            _element.CleanupInterval = 1800;
            _element.HealthCheckInterval = 3600; // Greater than cleanup interval

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Cleanup interval should be greater than or equal to health check interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectLowAlertThreshold()
        {
            // Arrange
            _element.AlertThreshold = 40;
            _element.EnableAlerting = true;

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Alert threshold less than 50% may generate excessive notifications")));
        }

        [TestMethod]
        public void Validate_ShouldDetectInvalidOutputFormat()
        {
            // Arrange
            _element.DefaultOutputFormat = "InvalidFormat";

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Invalid default output format")));
        }

        [TestMethod]
        public void Validate_ShouldDetectInvalidLanguage()
        {
            // Arrange
            _element.PreferredLanguage = "invalid-locale";

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Invalid preferred language")));
        }

        [TestMethod]
        public void Validate_ShouldDetectCacheSettingsIssues()
        {
            // Arrange
            _element.EnableCaching = true;
            _element.MaxCacheSize = 0; // Invalid when caching is enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max cache size must be greater than zero when caching is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectCacheExpirationIssues()
        {
            // Arrange
            _element.EnableCaching = true;
            _element.CacheExpiration = TimeSpan.Zero; // Invalid when caching is enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Cache expiration must be greater than zero when caching is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectStatisticsSettingsIssues()
        {
            // Arrange
            _element.EnableStatistics = true;
            _element.StatisticsRetentionDays = 0; // Invalid when statistics are enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Statistics retention days must be greater than zero when statistics are enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectStatisticsEntriesIssues()
        {
            // Arrange
            _element.EnableStatistics = true;
            _element.MaxStatisticsEntries = 0; // Invalid when statistics are enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max statistics entries must be greater than zero when statistics are enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectHealthMonitoringIssues()
        {
            // Arrange
            _element.EnableHealthMonitoring = true;
            _element.HealthCheckInterval = 0; // Invalid when health monitoring is enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Health check interval must be greater than zero when health monitoring is enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectPerformanceMetricsIssues()
        {
            // Arrange
            _element.EnablePerformanceMetrics = true;
            _element.PerformanceMetricsInterval = 0; // Invalid when performance metrics are enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Performance metrics interval must be greater than zero when performance metrics are enabled")));
        }

        [TestMethod]
        public void Validate_ShouldDetectAutoCleanupIssues()
        {
            // Arrange
            _element.EnableAutoCleanup = true;
            _element.CleanupInterval = 0; // Invalid when auto cleanup is enabled

            // Act
            var errors = _element.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Cleanup interval must be greater than zero when auto cleanup is enabled")));
        }

        [TestMethod]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            _element.CacheExpiration = TimeSpan.FromMinutes(10);
            _element.MaxRetries = 5;
            _element.EnableCaching = false;
            _element.EnableDetailedParsing = false;

            // Act
            var result = _element.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("LicenseQuery["));
            Assert.IsTrue(result.Contains("CacheExp=10.0m"));
            Assert.IsTrue(result.Contains("Timeout=30.0s"));
            Assert.IsTrue(result.Contains("MaxRetries=5"));
            Assert.IsTrue(result.Contains("Caching=False"));
            Assert.IsTrue(result.Contains("DetailedParsing=False"));
        }

        [TestMethod]
        public void ToLicenseQueryOptions_ShouldConvertCorrectly()
        {
            // Arrange
            _element.CacheExpiration = TimeSpan.FromMinutes(15);
            _element.QueryTimeout = TimeSpan.FromSeconds(45);
            _element.MaxRetries = 2;
            _element.EnableCaching = false;
            _element.DefaultOutputFormat = "JSON";
            _element.AlertThreshold = 85;

            // Act
            var options = _element.ToLicenseQueryOptions();

            // Assert
            Assert.AreEqual(_element.CacheExpiration, options.CacheExpiration);
            Assert.AreEqual(_element.QueryTimeout, options.QueryTimeout);
            Assert.AreEqual(_element.MaxRetries, options.MaxRetries);
            Assert.AreEqual(_element.EnableCaching, options.EnableCaching);
            Assert.AreEqual(_element.DefaultOutputFormat, options.DefaultOutputFormat);
            Assert.AreEqual(_element.AlertThreshold, options.AlertThreshold);
            Assert.AreEqual(_element.MaxCacheSize, options.MaxCacheSize);
            Assert.AreEqual(_element.MaxMemoryUsage, options.MaxMemoryUsage);
            Assert.AreEqual(_element.MaxConcurrentQueries, options.MaxConcurrentQueries);
            Assert.AreEqual(_element.MaxOutputSize, options.MaxOutputSize);
            Assert.AreEqual(_element.StatisticsRetentionDays, options.StatisticsRetentionDays);
            Assert.AreEqual(_element.MaxStatisticsEntries, options.MaxStatisticsEntries);
        }

        [TestMethod]
        public void ToLicenseQueryOptions_ShouldHandleAllProperties()
        {
            // Arrange
            var element = CreateValidLicenseQueryElement();

            // Act
            var options = element.ToLicenseQueryOptions();

            // Assert
            Assert.AreEqual(element.CacheExpiration, options.CacheExpiration);
            Assert.AreEqual(element.QueryTimeout, options.QueryTimeout);
            Assert.AreEqual(element.ParsingTimeout, options.ParsingTimeout);
            Assert.AreEqual(element.ServerResponseTimeout, options.ServerResponseTimeout);
            Assert.AreEqual(element.RetryDelay, options.RetryDelay);
            Assert.AreEqual(element.MaxRetries, options.MaxRetries);
            Assert.AreEqual(element.MaxCacheSize, options.MaxCacheSize);
            Assert.AreEqual((int)element.MaxMemoryUsage, options.MaxMemoryUsage);
            Assert.AreEqual(element.MaxConcurrentQueries, options.MaxConcurrentQueries);
            Assert.AreEqual((int)element.MaxOutputSize, options.MaxOutputSize);
            Assert.AreEqual(element.StatisticsRetentionDays, options.StatisticsRetentionDays);
            Assert.AreEqual(element.MaxStatisticsEntries, options.MaxStatisticsEntries);
            Assert.AreEqual(element.EnableCaching, options.EnableCaching);
            Assert.AreEqual(element.EnableStatistics, options.EnableStatistics);
            Assert.AreEqual(element.EnableVerboseOutput, options.EnableVerboseOutput);
            Assert.AreEqual(element.EnableDetailedParsing, options.EnableDetailedParsing);
            Assert.AreEqual(element.EnableErrorRecovery, options.EnableErrorRecovery);
            Assert.AreEqual(element.EnableIncrementalUpdates, options.EnableIncrementalUpdates);
            Assert.AreEqual(element.EnableFeatureBatching, options.EnableFeatureBatching);
            Assert.AreEqual(element.EnableUserActivityTracking, options.EnableUserActivityTracking);
            Assert.AreEqual(element.EnableBorrowingTracking, options.EnableBorrowingTracking);
            Assert.AreEqual(element.EnableHealthMonitoring, options.EnableHealthMonitoring);
            Assert.AreEqual(element.EnablePerformanceMetrics, options.EnablePerformanceMetrics);
            Assert.AreEqual(element.EnableAlerting, options.EnableAlerting);
            Assert.AreEqual(element.FailFastOnInvalidData, options.FailFastOnInvalidData);
            Assert.AreEqual(element.EnableAutoCleanup, options.EnableAutoCleanup);
            Assert.AreEqual(element.EnableCompactOutput, options.EnableCompactOutput);
            Assert.AreEqual(element.DefaultOutputFormat, options.DefaultOutputFormat);
            Assert.AreEqual(element.PreferredLanguage, options.PreferredLanguage);
            Assert.AreEqual(element.AlertThreshold, options.AlertThreshold);
            Assert.AreEqual(element.CleanupInterval, options.CleanupInterval);
            Assert.AreEqual(element.HealthCheckInterval, options.HealthCheckInterval);
            Assert.AreEqual(element.PerformanceMetricsInterval, options.PerformanceMetricsInterval);
        }

        [TestMethod]
        public void ConfigurationIntegration_ShouldLoadFromConfigFile()
        {
            // Arrange - Create a test config file with license query settings
            var testConfigContent = CreateTestConfigContent();
            File.WriteAllText(_testConfigPath, testConfigContent);

            // Act - Load configuration from test file
            var configMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = _testConfigPath
            };
            var config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            var section = config.GetSection("licenseReleaseService") as LicenseReleaseServiceSection;

            // Assert
            Assert.IsNotNull(section);
            Assert.IsNotNull(section.LicenseQuery);
            Assert.AreEqual(TimeSpan.FromMinutes(10), section.LicenseQuery.CacheExpiration);
            Assert.AreEqual(5, section.LicenseQuery.MaxRetries);
            Assert.AreEqual(false, section.LicenseQuery.EnableCaching);
            Assert.AreEqual("JSON", section.LicenseQuery.DefaultOutputFormat);
            Assert.AreEqual(85, section.LicenseQuery.AlertThreshold);
        }

        [TestMethod]
        public void Validation_ShouldWorkWithConfigurationManager()
        {
            // Arrange - Create a test config with invalid settings
            var invalidConfigContent = CreateInvalidTestConfigContent();
            File.WriteAllText(_testConfigPath, invalidConfigContent);

            // Act - Load and validate configuration
            var configMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = _testConfigPath
            };
            var config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            var section = config.GetSection("licenseReleaseService") as LicenseReleaseServiceSection;

            // Assert
            Assert.IsNotNull(section);
            var errors = section.Validate();
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("Query timeout should be greater than or equal to parsing timeout")));
        }

        private LicenseQueryElement CreateValidLicenseQueryElement()
        {
            return new LicenseQueryElement
            {
                CacheExpiration = TimeSpan.FromMinutes(5),
                QueryTimeout = TimeSpan.FromSeconds(30),
                ParsingTimeout = TimeSpan.FromSeconds(15),
                ServerResponseTimeout = TimeSpan.FromSeconds(20),
                RetryDelay = TimeSpan.FromSeconds(2),
                MaxRetries = 3,
                MaxCacheSize = 1000,
                MaxMemoryUsage = 52428800,
                MaxConcurrentQueries = 10,
                MaxOutputSize = 10485760,
                StatisticsRetentionDays = 30,
                MaxStatisticsEntries = 10000,
                EnableCaching = true,
                EnableStatistics = true,
                EnableVerboseOutput = false,
                EnableDetailedParsing = true,
                EnableErrorRecovery = true,
                EnableIncrementalUpdates = true,
                EnableFeatureBatching = true,
                EnableUserActivityTracking = true,
                EnableBorrowingTracking = true,
                EnableHealthMonitoring = true,
                EnablePerformanceMetrics = true,
                EnableAlerting = true,
                FailFastOnInvalidData = false,
                EnableAutoCleanup = true,
                EnableCompactOutput = false,
                DefaultOutputFormat = "Standard",
                PreferredLanguage = "en-US",
                AlertThreshold = 90,
                CleanupInterval = 3600,
                HealthCheckInterval = 60,
                PerformanceMetricsInterval = 30
            };
        }

        private string CreateTestConfigContent()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <section name=""licenseReleaseService"" type=""LicenseReleaseService.Configuration.LicenseReleaseServiceSection, LicenseReleaseService"" />
  </configSections>
  <licenseReleaseService>
    <licenseQuery
      cacheExpiration=""00:10:00""
      queryTimeout=""00:00:30""
      parsingTimeout=""00:00:15""
      serverResponseTimeout=""00:00:20""
      retryDelay=""00:00:02""
      maxRetries=""5""
      maxCacheSize=""1000""
      maxMemoryUsage=""52428800""
      maxConcurrentQueries=""10""
      maxOutputSize=""10485760""
      statisticsRetentionDays=""30""
      maxStatisticsEntries=""10000""
      enableCaching=""false""
      enableStatistics=""true""
      enableVerboseOutput=""false""
      enableDetailedParsing=""true""
      enableErrorRecovery=""true""
      enableIncrementalUpdates=""true""
      enableFeatureBatching=""true""
      enableUserActivityTracking=""true""
      enableBorrowingTracking=""true""
      enableHealthMonitoring=""true""
      enablePerformanceMetrics=""true""
      enableAlerting=""true""
      failFastOnInvalidData=""false""
      enableAutoCleanup=""true""
      enableCompactOutput=""false""
      defaultOutputFormat=""JSON""
      preferredLanguage=""en-US""
      alertThreshold=""85""
      cleanupInterval=""3600""
      healthCheckInterval=""60""
      performanceMetricsInterval=""30""
    />
  </licenseReleaseService>
</configuration>";
        }

        private string CreateInvalidTestConfigContent()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <section name=""licenseReleaseService"" type=""LicenseReleaseService.Configuration.LicenseReleaseServiceSection, LicenseReleaseService"" />
  </configSections>
  <licenseReleaseService>
    <licenseQuery
      cacheExpiration=""00:05:00""
      queryTimeout=""00:00:10""
      parsingTimeout=""00:00:15""
      serverResponseTimeout=""00:00:30""
      retryDelay=""00:00:00""
      maxRetries=""3""
      maxCacheSize=""0""
      maxMemoryUsage=""1073741825""
      maxConcurrentQueries=""150""
      maxOutputSize=""209715201""
      statisticsRetentionDays=""400""
      maxStatisticsEntries=""100001""
      enableCaching=""true""
      enableStatistics=""true""
      enableVerboseOutput=""false""
      enableDetailedParsing=""true""
      enableErrorRecovery=""true""
      enableIncrementalUpdates=""true""
      enableFeatureBatching=""true""
      enableUserActivityTracking=""true""
      enableBorrowingTracking=""true""
      enableHealthMonitoring=""true""
      enablePerformanceMetrics=""true""
      enableAlerting=""true""
      failFastOnInvalidData=""false""
      enableAutoCleanup=""true""
      enableCompactOutput=""false""
      defaultOutputFormat=""InvalidFormat""
      preferredLanguage=""invalid-locale""
      alertThreshold=""40""
      cleanupInterval=""0""
      healthCheckInterval=""0""
      performanceMetricsInterval=""0""
    />
  </licenseReleaseService>
</configuration>";
        }
    }
}