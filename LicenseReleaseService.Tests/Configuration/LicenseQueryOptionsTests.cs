using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService.Tests.Configuration
{
    /// <summary>
    /// Unit tests for LicenseQueryOptions
    /// </summary>
    [TestClass]
    public class LicenseQueryOptionsTests
    {
        private LicenseQueryOptions _options;

        [TestInitialize]
        public void TestInitialize()
        {
            _options = new LicenseQueryOptions();
        }

        [TestMethod]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var options = new LicenseQueryOptions();

            // Assert
            Assert.AreEqual(TimeSpan.FromMinutes(5), options.CacheExpiration);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.QueryTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(15), options.ParsingTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(20), options.ServerResponseTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(2), options.RetryDelay);
            Assert.AreEqual(3, options.MaxRetries);
            Assert.AreEqual(1000, options.MaxCacheSize);
            Assert.AreEqual(52428800, options.MaxMemoryUsage);
            Assert.AreEqual(10, options.MaxConcurrentQueries);
            Assert.AreEqual(10485760, options.MaxOutputSize);
            Assert.AreEqual(30, options.StatisticsRetentionDays);
            Assert.AreEqual(10000, options.MaxStatisticsEntries);
            Assert.IsTrue(options.EnableCaching);
            Assert.IsTrue(options.EnableStatistics);
            Assert.IsFalse(options.EnableVerboseOutput);
            Assert.IsTrue(options.EnableDetailedParsing);
            Assert.IsTrue(options.EnableErrorRecovery);
            Assert.IsTrue(options.EnableIncrementalUpdates);
            Assert.IsTrue(options.EnableFeatureBatching);
            Assert.IsTrue(options.EnableUserActivityTracking);
            Assert.IsTrue(options.EnableBorrowingTracking);
            Assert.IsTrue(options.EnableHealthMonitoring);
            Assert.IsTrue(options.EnablePerformanceMetrics);
            Assert.IsTrue(options.EnableAlerting);
            Assert.IsFalse(options.FailFastOnInvalidData);
            Assert.IsTrue(options.EnableAutoCleanup);
            Assert.IsFalse(options.EnableCompactOutput);
            Assert.AreEqual("Standard", options.DefaultOutputFormat);
            Assert.AreEqual("en-US", options.PreferredLanguage);
            Assert.AreEqual(90, options.AlertThreshold);
            Assert.AreEqual(3600, options.CleanupInterval);
            Assert.AreEqual(60, options.HealthCheckInterval);
            Assert.AreEqual(30, options.PerformanceMetricsInterval);
            Assert.IsNotNull(options.RegexPatterns);
            Assert.IsTrue(options.RegexPatterns.Count > 0);
            Assert.IsNotNull(options.IncludedFeatures);
            Assert.IsNotNull(options.ExcludedFeatures);
            Assert.IsNotNull(options.IncludedUsers);
            Assert.IsNotNull(options.ExcludedUsers);
            Assert.IsNotNull(options.IncludedHosts);
            Assert.IsNotNull(options.ExcludedHosts);
        }

        [TestMethod]
        public void CacheExpiration_ShouldValidateValue()
        {
            // Arrange
            var validTime = TimeSpan.FromMinutes(10);
            var invalidTime = TimeSpan.FromSeconds(-1);

            // Act & Assert - Valid value
            _options.CacheExpiration = validTime;
            Assert.AreEqual(validTime, _options.CacheExpiration);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.CacheExpiration = invalidTime);
        }

        [TestMethod]
        public void QueryTimeout_ShouldValidateValue()
        {
            // Arrange
            var validTime = TimeSpan.FromSeconds(60);
            var invalidTime = TimeSpan.Zero;

            // Act & Assert - Valid value
            _options.QueryTimeout = validTime;
            Assert.AreEqual(validTime, _options.QueryTimeout);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.QueryTimeout = invalidTime);
        }

        [TestMethod]
        public void ParsingTimeout_ShouldValidateValue()
        {
            // Arrange
            var validTime = TimeSpan.FromSeconds(30);
            var invalidTime = TimeSpan.Zero;

            // Act & Assert - Valid value
            _options.ParsingTimeout = validTime;
            Assert.AreEqual(validTime, _options.ParsingTimeout);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.ParsingTimeout = invalidTime);
        }

        [TestMethod]
        public void ServerResponseTimeout_ShouldValidateValue()
        {
            // Arrange
            var validTime = TimeSpan.FromSeconds(25);
            var invalidTime = TimeSpan.Zero;

            // Act & Assert - Valid value
            _options.ServerResponseTimeout = validTime;
            Assert.AreEqual(validTime, _options.ServerResponseTimeout);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.ServerResponseTimeout = invalidTime);
        }

        [TestMethod]
        public void RetryDelay_ShouldValidateValue()
        {
            // Arrange
            var validTime = TimeSpan.FromSeconds(5);
            var invalidTime = TimeSpan.FromSeconds(-1);

            // Act & Assert - Valid value
            _options.RetryDelay = validTime;
            Assert.AreEqual(validTime, _options.RetryDelay);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.RetryDelay = invalidTime);
        }

        [TestMethod]
        public void MaxRetries_ShouldValidateValue()
        {
            // Arrange
            var validValue = 5;
            var invalidValue = -1;

            // Act & Assert - Valid value
            _options.MaxRetries = validValue;
            Assert.AreEqual(validValue, _options.MaxRetries);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.MaxRetries = invalidValue);
        }

        [TestMethod]
        public void MaxCacheSize_ShouldValidateValue()
        {
            // Arrange
            var validValue = 5000;
            var invalidValue = 0;

            // Act & Assert - Valid value
            _options.MaxCacheSize = validValue;
            Assert.AreEqual(validValue, _options.MaxCacheSize);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.MaxCacheSize = invalidValue);
        }

        [TestMethod]
        public void MaxMemoryUsage_ShouldValidateValue()
        {
            // Arrange
            var validValue = 104857600; // 100MB
            var invalidValue = 1024; // Less than 1MB

            // Act & Assert - Valid value
            _options.MaxMemoryUsage = validValue;
            Assert.AreEqual(validValue, _options.MaxMemoryUsage);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.MaxMemoryUsage = invalidValue);
        }

        [TestMethod]
        public void MaxConcurrentQueries_ShouldValidateValue()
        {
            // Arrange
            var validValue = 20;
            var invalidValue = 0;

            // Act & Assert - Valid value
            _options.MaxConcurrentQueries = validValue;
            Assert.AreEqual(validValue, _options.MaxConcurrentQueries);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.MaxConcurrentQueries = invalidValue);
        }

        [TestMethod]
        public void MaxOutputSize_ShouldValidateValue()
        {
            // Arrange
            var validValue = 20971520; // 20MB
            var invalidValue = -1;

            // Act & Assert - Valid value
            _options.MaxOutputSize = validValue;
            Assert.AreEqual(validValue, _options.MaxOutputSize);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.MaxOutputSize = invalidValue);
        }

        [TestMethod]
        public void StatisticsRetentionDays_ShouldValidateValue()
        {
            // Arrange
            var validValue = 90;
            var invalidValue = 0;

            // Act & Assert - Valid value
            _options.StatisticsRetentionDays = validValue;
            Assert.AreEqual(validValue, _options.StatisticsRetentionDays);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.StatisticsRetentionDays = invalidValue);
        }

        [TestMethod]
        public void MaxStatisticsEntries_ShouldValidateValue()
        {
            // Arrange
            var validValue = 50000;
            var invalidValue = 0;

            // Act & Assert - Valid value
            _options.MaxStatisticsEntries = validValue;
            Assert.AreEqual(validValue, _options.MaxStatisticsEntries);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.MaxStatisticsEntries = invalidValue);
        }

        [TestMethod]
        public void AlertThreshold_ShouldValidateValue()
        {
            // Arrange
            var validValue = 80;
            var invalidLowValue = -1;
            var invalidHighValue = 101;

            // Act & Assert - Valid value
            _options.AlertThreshold = validValue;
            Assert.AreEqual(validValue, _options.AlertThreshold);

            // Act & Assert - Invalid values
            Assert.ThrowsException<ArgumentException>(() => _options.AlertThreshold = invalidLowValue);
            Assert.ThrowsException<ArgumentException>(() => _options.AlertThreshold = invalidHighValue);
        }

        [TestMethod]
        public void CleanupInterval_ShouldValidateValue()
        {
            // Arrange
            var validValue = 1800;
            var invalidValue = 0;

            // Act & Assert - Valid value
            _options.CleanupInterval = validValue;
            Assert.AreEqual(validValue, _options.CleanupInterval);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.CleanupInterval = invalidValue);
        }

        [TestMethod]
        public void HealthCheckInterval_ShouldValidateValue()
        {
            // Arrange
            var validValue = 120;
            var invalidValue = 0;

            // Act & Assert - Valid value
            _options.HealthCheckInterval = validValue;
            Assert.AreEqual(validValue, _options.HealthCheckInterval);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.HealthCheckInterval = invalidValue);
        }

        [TestMethod]
        public void PerformanceMetricsInterval_ShouldValidateValue()
        {
            // Arrange
            var validValue = 60;
            var invalidValue = 0;

            // Act & Assert - Valid value
            _options.PerformanceMetricsInterval = validValue;
            Assert.AreEqual(validValue, _options.PerformanceMetricsInterval);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.PerformanceMetricsInterval = invalidValue);
        }

        [TestMethod]
        public void DefaultOutputFormat_ShouldValidateValue()
        {
            // Arrange
            var validValue = "JSON";
            var invalidValue = "";

            // Act & Assert - Valid value
            _options.DefaultOutputFormat = validValue;
            Assert.AreEqual(validValue, _options.DefaultOutputFormat);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.DefaultOutputFormat = invalidValue);
        }

        [TestMethod]
        public void PreferredLanguage_ShouldValidateValue()
        {
            // Arrange
            var validValue = "fr-FR";
            var invalidValue = "";

            // Act & Assert - Valid value
            _options.PreferredLanguage = validValue;
            Assert.AreEqual(validValue, _options.PreferredLanguage);

            // Act & Assert - Invalid value
            Assert.ThrowsException<ArgumentException>(() => _options.PreferredLanguage = invalidValue);
        }

        [TestMethod]
        public void Clone_ShouldCreateExactCopy()
        {
            // Arrange
            _options.CacheExpiration = TimeSpan.FromMinutes(10);
            _options.MaxRetries = 5;
            _options.EnableVerboseOutput = true;
            _options.DefaultOutputFormat = "JSON";
            _options.RegexPatterns["TestPattern"] = "test.*";
            _options.IncludedFeatures.Add("solidworks");
            _options.ExcludedUsers.Add("testuser");

            // Act
            var cloned = _options.Clone();

            // Assert
            Assert.AreEqual(_options.CacheExpiration, cloned.CacheExpiration);
            Assert.AreEqual(_options.MaxRetries, cloned.MaxRetries);
            Assert.AreEqual(_options.EnableVerboseOutput, cloned.EnableVerboseOutput);
            Assert.AreEqual(_options.DefaultOutputFormat, cloned.DefaultOutputFormat);
            Assert.AreEqual(_options.RegexPatterns.Count, cloned.RegexPatterns.Count);
            Assert.AreEqual(_options.RegexPatterns["TestPattern"], cloned.RegexPatterns["TestPattern"]);
            Assert.AreEqual(_options.IncludedFeatures.Count, cloned.IncludedFeatures.Count);
            Assert.AreEqual(_options.IncludedFeatures[0], cloned.IncludedFeatures[0]);
            Assert.AreEqual(_options.ExcludedUsers.Count, cloned.ExcludedUsers.Count);
            Assert.AreEqual(_options.ExcludedUsers[0], cloned.ExcludedUsers[0]);

            // Ensure it's a deep copy, not the same instance
            Assert.AreNotSame(_options, cloned);
            Assert.AreNotSame(_options.RegexPatterns, cloned.RegexPatterns);
            Assert.AreNotSame(_options.IncludedFeatures, cloned.IncludedFeatures);
            Assert.AreNotSame(_options.ExcludedUsers, cloned.ExcludedUsers);
        }

        [TestMethod]
        public void Validate_ShouldReturnEmptyListForValidConfiguration()
        {
            // Arrange
            var options = LicenseQueryOptions.DefaultSolidWorksOptions();

            // Act
            var errors = options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_ShouldDetectTimeoutRelationshipIssues()
        {
            // Arrange
            _options.QueryTimeout = TimeSpan.FromSeconds(10);
            _options.ParsingTimeout = TimeSpan.FromSeconds(15); // Greater than query timeout

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Query timeout should be greater than or equal to parsing timeout")));
        }

        [TestMethod]
        public void Validate_ShouldDetectServerResponseTimeoutIssues()
        {
            // Arrange
            _options.QueryTimeout = TimeSpan.FromSeconds(20);
            _options.ServerResponseTimeout = TimeSpan.FromSeconds(30); // Greater than query timeout

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Server response timeout should be less than or equal to query timeout")));
        }

        [TestMethod]
        public void Validate_ShouldDetectRetryLogicIssues()
        {
            // Arrange
            _options.MaxRetries = 3;
            _options.RetryDelay = TimeSpan.Zero;

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Retry delay should be greater than zero when max retries is greater than zero")));
        }

        [TestMethod]
        public void Validate_ShouldDetectMemoryLimitIssues()
        {
            // Arrange
            _options.MaxMemoryUsage = 2147483648; // 2GB, exceeds recommended limit

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max memory usage exceeds recommended limit of 1GB")));
        }

        [TestMethod]
        public void Validate_ShouldDetectConcurrentQueryLimitIssues()
        {
            // Arrange
            _options.MaxConcurrentQueries = 150; // Exceeds recommended limit

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max concurrent queries exceeds recommended limit of 100")));
        }

        [TestMethod]
        public void Validate_ShouldDetectOutputSizeLimitIssues()
        {
            // Arrange
            _options.MaxOutputSize = 209715200; // 200MB, exceeds recommended limit

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max output size exceeds recommended limit of 100MB")));
        }

        [TestMethod]
        public void Validate_ShouldDetectStatisticsRetentionIssues()
        {
            // Arrange
            _options.StatisticsRetentionDays = 400; // Exceeds recommended limit

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Statistics retention days exceeds recommended limit of 365")));
        }

        [TestMethod]
        public void Validate_ShouldDetectStatisticsEntriesLimitIssues()
        {
            // Arrange
            _options.MaxStatisticsEntries = 200000; // Exceeds recommended limit

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Max statistics entries exceeds recommended limit of 100000")));
        }

        [TestMethod]
        public void Validate_ShouldDetectIntervalRelationshipIssues()
        {
            // Arrange
            _options.PerformanceMetricsInterval = 60;
            _options.HealthCheckInterval = 30; // Less than performance metrics interval

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Performance metrics interval should be less than or equal to health check interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectCleanupIntervalIssues()
        {
            // Arrange
            _options.CleanupInterval = 1800;
            _options.HealthCheckInterval = 3600; // Greater than cleanup interval

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Cleanup interval should be greater than or equal to health check interval")));
        }

        [TestMethod]
        public void Validate_ShouldDetectLowAlertThreshold()
        {
            // Arrange
            _options.AlertThreshold = 40;
            _options.EnableAlerting = true;

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Alert threshold less than 50% may generate excessive notifications")));
        }

        [TestMethod]
        public void Validate_ShouldDetectInvalidOutputFormat()
        {
            // Arrange
            _options.DefaultOutputFormat = "InvalidFormat";

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Invalid default output format")));
        }

        [TestMethod]
        public void Validate_ShouldDetectInvalidLanguage()
        {
            // Arrange
            _options.PreferredLanguage = "invalid-locale";

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Invalid preferred language")));
        }

        [TestMethod]
        public void Validate_ShouldDetectEmptyRegexPatterns()
        {
            // Arrange
            _options.RegexPatterns.Clear();

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("At least one regex pattern must be specified")));
        }

        [TestMethod]
        public void Validate_ShouldDetectInvalidRegexPatterns()
        {
            // Arrange
            _options.RegexPatterns["InvalidPattern"] = "[invalid-regex";

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Invalid regex pattern for 'InvalidPattern'")));
        }

        [TestMethod]
        public void Validate_ShouldDetectEmptyRegexPatternValues()
        {
            // Arrange
            _options.RegexPatterns["EmptyPattern"] = "";

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Regex pattern for 'EmptyPattern' cannot be empty")));
        }

        [TestMethod]
        public void Validate_ShouldDetectFilterListSizeIssues()
        {
            // Arrange
            for (int i = 0; i < 150; i++)
            {
                _options.IncludedFeatures.Add($"feature{i}");
            }

            // Act
            var errors = _options.Validate();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Any(e => e.Contains("Included features list exceeds recommended limit of 100")));
        }

        [TestMethod]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            _options.CacheExpiration = TimeSpan.FromMinutes(10);
            _options.MaxRetries = 5;
            _options.EnableVerboseOutput = true;
            _options.DefaultOutputFormat = "JSON";

            // Act
            var result = _options.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("License Query Options:"));
            Assert.IsTrue(result.Contains("Cache Expiration: 10.0s"));
            Assert.IsTrue(result.Contains("Max Retries: 5"));
            Assert.IsTrue(result.Contains("Enable Verbose Output: True"));
            Assert.IsTrue(result.Contains("Default Output Format: JSON"));
        }

        [TestMethod]
        public void DefaultSolidWorksOptions_ShouldReturnAppropriateDefaults()
        {
            // Act
            var options = LicenseQueryOptions.DefaultSolidWorksOptions();

            // Assert
            Assert.AreEqual(TimeSpan.FromMinutes(5), options.CacheExpiration);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.QueryTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(15), options.ParsingTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(20), options.ServerResponseTimeout);
            Assert.AreEqual(3, options.MaxRetries);
            Assert.AreEqual(TimeSpan.FromSeconds(2), options.RetryDelay);
            Assert.AreEqual(1000, options.MaxCacheSize);
            Assert.AreEqual(52428800, options.MaxMemoryUsage);
            Assert.AreEqual(10, options.MaxConcurrentQueries);
            Assert.IsTrue(options.EnableCaching);
            Assert.IsTrue(options.EnableStatistics);
            Assert.IsFalse(options.EnableVerboseOutput);
            Assert.IsTrue(options.EnableDetailedParsing);
            Assert.AreEqual("Standard", options.DefaultOutputFormat);
        }

        [TestMethod]
        public void HighPerformanceOptions_ShouldReturnOptimizedSettings()
        {
            // Act
            var options = LicenseQueryOptions.HighPerformanceOptions();

            // Assert
            Assert.AreEqual(TimeSpan.FromMinutes(1), options.CacheExpiration);
            Assert.AreEqual(TimeSpan.FromSeconds(10), options.QueryTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(5), options.ParsingTimeout);
            Assert.AreEqual(1, options.MaxRetries);
            Assert.AreEqual(TimeSpan.FromSeconds(1), options.RetryDelay);
            Assert.AreEqual(100, options.MaxCacheSize);
            Assert.AreEqual(20971520, options.MaxMemoryUsage);
            Assert.AreEqual(50, options.MaxConcurrentQueries);
            Assert.IsFalse(options.EnableStatistics);
            Assert.IsFalse(options.EnableVerboseOutput);
            Assert.IsFalse(options.EnableDetailedParsing);
            Assert.IsTrue(options.EnableCompactOutput);
            Assert.AreEqual("Compact", options.DefaultOutputFormat);
        }

        [TestMethod]
        public void DebugOptions_ShouldReturnDebugSettings()
        {
            // Act
            var options = LicenseQueryOptions.DebugOptions();

            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.CacheExpiration);
            Assert.AreEqual(TimeSpan.FromMinutes(2), options.QueryTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(1), options.ParsingTimeout);
            Assert.AreEqual(5, options.MaxRetries);
            Assert.AreEqual(TimeSpan.FromSeconds(5), options.RetryDelay);
            Assert.AreEqual(50, options.MaxCacheSize);
            Assert.AreEqual(104857600, options.MaxMemoryUsage);
            Assert.AreEqual(1, options.MaxConcurrentQueries);
            Assert.IsFalse(options.EnableCaching);
            Assert.IsTrue(options.EnableStatistics);
            Assert.IsTrue(options.EnableVerboseOutput);
            Assert.IsTrue(options.EnableDetailedParsing);
            Assert.AreEqual("Verbose", options.DefaultOutputFormat);
        }

        [TestMethod]
        public void RegexPatterns_ShouldContainDefaultPatterns()
        {
            // Arrange & Act
            var options = new LicenseQueryOptions();

            // Assert
            Assert.IsTrue(options.RegexPatterns.ContainsKey("FeatureLine"));
            Assert.IsTrue(options.RegexPatterns.ContainsKey("UserLine"));
            Assert.IsTrue(options.RegexPatterns.ContainsKey("BorrowLine"));
            Assert.IsTrue(options.RegexPatterns.ContainsKey("ServerLine"));
            Assert.IsTrue(options.RegexPatterns.ContainsKey("Timestamp"));

            // Test that patterns are valid regex
            foreach (var pattern in options.RegexPatterns.Values)
            {
                try
                {
                    var regex = new System.Text.RegularExpressions.Regex(pattern);
                }
                catch (ArgumentException)
                {
                    Assert.Fail($"Invalid regex pattern: {pattern}");
                }
            }
        }

        [TestMethod]
        public void FilterLists_ShouldBeEmptyByDefault()
        {
            // Arrange & Act
            var options = new LicenseQueryOptions();

            // Assert
            Assert.IsNotNull(options.IncludedFeatures);
            Assert.AreEqual(0, options.IncludedFeatures.Count);
            Assert.IsNotNull(options.ExcludedFeatures);
            Assert.AreEqual(0, options.ExcludedFeatures.Count);
            Assert.IsNotNull(options.IncludedUsers);
            Assert.AreEqual(0, options.IncludedUsers.Count);
            Assert.IsNotNull(options.ExcludedUsers);
            Assert.AreEqual(0, options.ExcludedUsers.Count);
            Assert.IsNotNull(options.IncludedHosts);
            Assert.AreEqual(0, options.IncludedHosts.Count);
            Assert.IsNotNull(options.ExcludedHosts);
            Assert.AreEqual(0, options.ExcludedHosts.Count);
        }

        [TestMethod]
        public void FilterLists_ShouldAllowAddingItems()
        {
            // Arrange
            var options = new LicenseQueryOptions();

            // Act
            options.IncludedFeatures.Add("solidworks");
            options.ExcludedFeatures.Add("feature1");
            options.IncludedUsers.Add("user1");
            options.ExcludedUsers.Add("user2");
            options.IncludedHosts.Add("host1");
            options.ExcludedHosts.Add("host2");

            // Assert
            Assert.AreEqual(1, options.IncludedFeatures.Count);
            Assert.AreEqual("solidworks", options.IncludedFeatures[0]);
            Assert.AreEqual(1, options.ExcludedFeatures.Count);
            Assert.AreEqual("feature1", options.ExcludedFeatures[0]);
            Assert.AreEqual(1, options.IncludedUsers.Count);
            Assert.AreEqual("user1", options.IncludedUsers[0]);
            Assert.AreEqual(1, options.ExcludedUsers.Count);
            Assert.AreEqual("user2", options.ExcludedUsers[0]);
            Assert.AreEqual(1, options.IncludedHosts.Count);
            Assert.AreEqual("host1", options.IncludedHosts[0]);
            Assert.AreEqual(1, options.ExcludedHosts.Count);
            Assert.AreEqual("host2", options.ExcludedHosts[0]);
        }
    }
}