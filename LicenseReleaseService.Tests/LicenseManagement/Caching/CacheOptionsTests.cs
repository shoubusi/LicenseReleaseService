using System;
using System.Collections.Generic;
using LicenseReleaseService.LicenseManagement.Caching;
using Xunit;

namespace LicenseReleaseService.Tests.LicenseManagement.Caching
{
    /// <summary>
    /// Unit tests for CacheOptions class
    /// </summary>
    public class CacheOptionsTests
    {
        [Fact]
        public void Constructor_DefaultValues_AreCorrect()
        {
            var options = new CacheOptions();

            Assert.Equal(TimeSpan.FromMinutes(5), options.DefaultExpiration);
            Assert.Equal(TimeSpan.FromMinutes(2), options.ServerStatusExpiration);
            Assert.Equal(TimeSpan.FromMinutes(3), options.LicenseInfoExpiration);
            Assert.Equal(TimeSpan.FromMinutes(4), options.LicenseFeatureExpiration);
            Assert.Equal(1000, options.MaximumItems);
            Assert.True(options.EnableSlidingExpiration);
            Assert.Equal(TimeSpan.FromMinutes(1), options.SlidingExpirationWindow);
            Assert.False(options.EnableCompression);
            Assert.True(options.EnableStatistics);
            Assert.Equal(TimeSpan.FromMinutes(1), options.StatisticsUpdateInterval);
            Assert.Equal("LicenseCache_", options.KeyPrefix);
            Assert.True(options.EnableDetailedLogging);
            Assert.Equal(80, options.MemoryPressureThreshold);
            Assert.Equal(20, options.MemoryPressureCleanupPercentage);
            Assert.True(options.EnableMemoryPressureMonitoring);
            Assert.Equal("LicenseManagement", options.CacheRegion);
            Assert.Equal(CacheEvictionPriority.Normal, options.EvictionPriority);
            Assert.True(options.EnableBackgroundCleanup);
            Assert.Equal(TimeSpan.FromMinutes(5), options.BackgroundCleanupInterval);
        }

        [Fact]
        public void Constructor_CustomValues_AreSetCorrectly()
        {
            var defaultExpiration = TimeSpan.FromMinutes(10);
            var maximumItems = 2000;
            var enableSlidingExpiration = false;

            var options = new CacheOptions(defaultExpiration, maximumItems, enableSlidingExpiration);

            Assert.Equal(defaultExpiration, options.DefaultExpiration);
            Assert.Equal(maximumItems, options.MaximumItems);
            Assert.Equal(enableSlidingExpiration, options.EnableSlidingExpiration);
        }

        [Fact]
        public void DefaultExpiration_SetInvalidValue_ThrowsArgumentException()
        {
            var options = new CacheOptions();

            Assert.Throws<ArgumentException>(() => options.DefaultExpiration = TimeSpan.Zero);
            Assert.Throws<ArgumentException>(() => options.DefaultExpiration = TimeSpan.FromSeconds(-1));
        }

        [Fact]
        public void DefaultExpiration_SetValidValue_IsSetCorrectly()
        {
            var options = new CacheOptions();
            var value = TimeSpan.FromMinutes(15);

            options.DefaultExpiration = value;

            Assert.Equal(value, options.DefaultExpiration);
        }

        [Fact]
        public void MaximumItems_SetInvalidValue_ThrowsArgumentException()
        {
            var options = new CacheOptions();

            Assert.Throws<ArgumentException>(() => options.MaximumItems = 0);
            Assert.Throws<ArgumentException>(() => options.MaximumItems = -1);
        }

        [Fact]
        public void MaximumItems_SetValidValue_IsSetCorrectly()
        {
            var options = new CacheOptions();
            var value = 5000;

            options.MaximumItems = value;

            Assert.Equal(value, options.MaximumItems);
        }

        [Fact]
        public void MemoryPressureThreshold_SetInvalidValue_ThrowsArgumentException()
        {
            var options = new CacheOptions();

            Assert.Throws<ArgumentException>(() => options.MemoryPressureThreshold = -1);
            Assert.Throws<ArgumentException>(() => options.MemoryPressureThreshold = 101);
        }

        [Fact]
        public void MemoryPressureThreshold_SetValidValue_IsSetCorrectly()
        {
            var options = new CacheOptions();
            var value = 90;

            options.MemoryPressureThreshold = value;

            Assert.Equal(value, options.MemoryPressureThreshold);
        }

        [Fact]
        public void Validate_DefaultOptions_ReturnsEmptyList()
        {
            var options = new CacheOptions();

            var errors = options.Validate();

            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidDefaultExpiration_ReturnsError()
        {
            var options = new CacheOptions();
            options.DefaultExpiration = TimeSpan.Zero;

            var errors = options.Validate();

            Assert.Contains("Default expiration must be positive", errors);
        }

        [Fact]
        public void Validate_InvalidMaximumItems_ReturnsError()
        {
            var options = new CacheOptions();
            options.MaximumItems = 0;

            var errors = options.Validate();

            Assert.Contains("Maximum items must be positive", errors);
        }

        [Fact]
        public void Validate_InvalidKeyPrefix_ReturnsError()
        {
            var options = new CacheOptions();
            options.KeyPrefix = "";

            var errors = options.Validate();

            Assert.Contains("Key prefix cannot be empty", errors);
        }

        [Fact]
        public void Validate_MultipleErrors_ReturnsAllErrors()
        {
            var options = new CacheOptions();
            options.DefaultExpiration = TimeSpan.Zero;
            options.MaximumItems = -1;
            options.KeyPrefix = "";

            var errors = options.Validate();

            Assert.Contains("Default expiration must be positive", errors);
            Assert.Contains("Maximum items must be positive", errors);
            Assert.Contains("Key prefix cannot be empty", errors);
        }

        [Fact]
        public void Clone_CreatesExactCopy()
        {
            var options = new CacheOptions
            {
                DefaultExpiration = TimeSpan.FromMinutes(30),
                MaximumItems = 5000,
                EnableSlidingExpiration = false,
                KeyPrefix = "TestPrefix_",
                EnableDetailedLogging = false,
                MemoryPressureThreshold = 95
            };

            var cloned = options.Clone();

            Assert.Equal(options.DefaultExpiration, cloned.DefaultExpiration);
            Assert.Equal(options.MaximumItems, cloned.MaximumItems);
            Assert.Equal(options.EnableSlidingExpiration, cloned.EnableSlidingExpiration);
            Assert.Equal(options.KeyPrefix, cloned.KeyPrefix);
            Assert.Equal(options.EnableDetailedLogging, cloned.EnableDetailedLogging);
            Assert.Equal(options.MemoryPressureThreshold, cloned.MemoryPressureThreshold);
        }

        [Fact]
        public void Clone_ModifyingClone_DoesNotAffectOriginal()
        {
            var options = new CacheOptions();
            var cloned = options.Clone();

            cloned.DefaultExpiration = TimeSpan.FromHours(1);
            cloned.MaximumItems = 10000;
            cloned.KeyPrefix = "ModifiedPrefix";

            Assert.NotEqual(options.DefaultExpiration, cloned.DefaultExpiration);
            Assert.NotEqual(options.MaximumItems, cloned.MaximumItems);
            Assert.NotEqual(options.KeyPrefix, cloned.KeyPrefix);
        }

        [Fact]
        public void ToString_ReturnsCorrectStringRepresentation()
        {
            var options = new CacheOptions
            {
                DefaultExpiration = TimeSpan.FromMinutes(10),
                MaximumItems = 2000,
                EnableSlidingExpiration = false,
                KeyPrefix = "Test_"
            };

            var result = options.ToString();

            Assert.Contains("DefaultExpiration=00:10:00", result);
            Assert.Contains("MaxItems=2000", result);
            Assert.Contains("SlidingExpiration=False", result);
            Assert.Contains("KeyPrefix=Test_", result);
        }
    }
}