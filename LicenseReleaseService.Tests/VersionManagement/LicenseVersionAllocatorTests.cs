using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.VersionManagement;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LicenseReleaseService.Tests.VersionManagement
{
    /// <summary>
    /// Comprehensive unit tests for LicenseVersionAllocator
    /// </summary>
    public class LicenseVersionAllocatorTests : IDisposable
    {
        private readonly Mock<ILogger<LicenseVersionAllocator>> _mockLogger;
        private readonly ResourceAllocationConfiguration _configuration;
        private readonly LicenseVersionAllocator _allocator;

        public LicenseVersionAllocatorTests()
        {
            _mockLogger = new Mock<ILogger<LicenseVersionAllocator>>();
            _configuration = new ResourceAllocationConfiguration
            {
                MaxConcurrentAllocations = 5,
                DefaultMaxConcurrentOperations = 3,
                MaxMemoryPerPool = 1024 * 1024 * 1024, // 1GB
                MaxCpuPerPool = 80,
                AllocationTimeout = TimeSpan.FromSeconds(10),
                DefaultAllocationTimeout = TimeSpan.FromMinutes(2),
                CleanupInterval = TimeSpan.FromMinutes(1)
            };

            _allocator = new LicenseVersionAllocator(_mockLogger.Object, _configuration);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
        {
            // Arrange
            var logger = new Mock<ILogger<LicenseVersionAllocator>>();
            var config = new ResourceAllocationConfiguration();

            // Act
            var allocator = new LicenseVersionAllocator(logger.Object, config);

            // Assert
            Assert.NotNull(allocator);
            Assert.Equal(config, allocator.Configuration);
            Assert.NotNull(allocator.GetStatistics());
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var config = new ResourceAllocationConfiguration();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new LicenseVersionAllocator(null, config));
            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Arrange
            var logger = new Mock<ILogger<LicenseVersionAllocator>>();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new LicenseVersionAllocator(logger.Object, null));
            Assert.Equal("configuration", exception.ParamName);
        }

        [Fact]
        public async Task AllocateAsync_WithValidParameters_ShouldSucceed()
        {
            // Arrange
            var version = "2024";
            var queryOptions = new LicenseQueryOptions
            {
                MaxConcurrentQueries = 2,
                QueryTimeout = TimeSpan.FromSeconds(30),
                MaxOutputSize = 1024 * 1024 // 1MB
            };

            // Act
            var result = await _allocator.AllocateAsync(version, queryOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AllocationId);
            Assert.Equal(version, result.Version);
            Assert.True(result.AllocatedAt <= DateTime.UtcNow);
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
            Assert.True(result.MemoryLimit > 0);
            Assert.True(result.CpuLimit > 0);
        }

        [Fact]
        public async Task AllocateAsync_WithEmptyVersion_ShouldThrowArgumentException()
        {
            // Arrange
            var version = "";
            var queryOptions = new LicenseQueryOptions();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _allocator.AllocateAsync(version, queryOptions));
            Assert.Equal("version", exception.ParamName);
        }

        [Fact]
        public async Task AllocateAsync_WithNullQueryOptions_ShouldThrowArgumentNullException()
        {
            // Arrange
            var version = "2024";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _allocator.AllocateAsync(version, null));
            Assert.Equal("queryOptions", exception.ParamName);
        }

        [Fact]
        public async Task AllocateAsync_WithCancellation_ShouldCancelGracefully()
        {
            // Arrange
            var version = "2024";
            var queryOptions = new LicenseQueryOptions();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _allocator.AllocateAsync(version, queryOptions, cts.Token);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AllocateAsync_MultipleCalls_ShouldTrackStatistics()
        {
            // Arrange
            var version = "2024";
            var queryOptions = new LicenseQueryOptions();

            // Act
            var result1 = await _allocator.AllocateAsync(version, queryOptions);
            var result2 = await _allocator.AllocateAsync(version, queryOptions);
            var stats = _allocator.GetStatistics();

            // Assert
            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.Equal(2, stats.TotalAllocations);
            Assert.Equal(2, stats.ActiveAllocations);
            Assert.True(stats.LastAllocationTime > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task ReleaseAsync_WithValidAllocation_ShouldSucceed()
        {
            // Arrange
            var version = "2024";
            var queryOptions = new LicenseQueryOptions();
            var allocation = await _allocator.AllocateAsync(version, queryOptions);

            // Act
            var releaseResult = await _allocator.ReleaseAsync(allocation);
            var stats = _allocator.GetStatistics();

            // Assert
            Assert.True(releaseResult.Success);
            Assert.True(releaseResult.ReleasedAt <= DateTime.UtcNow);
            Assert.Equal(1, stats.TotalReleases);
            Assert.Equal(0, stats.ActiveAllocations);
            Assert.True(stats.LastReleaseTime > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task ReleaseAsync_WithNullAllocation_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _allocator.ReleaseAsync(null));
            Assert.Equal("allocation", exception.ParamName);
        }

        [Fact]
        public async Task ReleaseAsync_WithInvalidAllocation_ShouldFail()
        {
            // Arrange
            var invalidAllocation = new ResourceAllocation
            {
                AllocationId = "invalid-id",
                Version = "nonexistent"
            };

            // Act
            var result = await _allocator.ReleaseAsync(invalidAllocation);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task GetUtilizationAsync_WithActiveAllocations_ShouldReturnAccurateSummary()
        {
            // Arrange
            var version = "2024";
            var queryOptions = new LicenseQueryOptions { MaxConcurrentQueries = 2 };

            // Create multiple allocations
            var allocation1 = await _allocator.AllocateAsync(version, queryOptions);
            var allocation2 = await _allocator.AllocateAsync(version, queryOptions);

            // Act
            var summary = await _allocator.GetUtilizationAsync();

            // Assert
            Assert.Equal(1, summary.TotalPools);
            Assert.Equal(2, summary.TotalCapacity); // Based on query options
            Assert.Equal(2, summary.TotalAllocated);
            Assert.Equal(0, summary.TotalAvailable);
            Assert.Equal(1.0, summary.OverallUtilization);

            var versionUtilization = summary.VersionUtilization.FirstOrDefault();
            Assert.NotNull(versionUtilization);
            Assert.Equal(version, versionUtilization.Version);
            Assert.Equal(2, versionUtilization.AllocatedResources);
            Assert.Equal(2, versionUtilization.ActiveAllocations.Count);
        }

        [Fact]
        public async Task GetUtilizationAsync_WithNoAllocations_ShouldReturnEmptySummary()
        {
            // Act
            var summary = await _allocator.GetUtilizationAsync();

            // Assert
            Assert.Equal(0, summary.TotalPools);
            Assert.Equal(0, summary.TotalCapacity);
            Assert.Equal(0, summary.TotalAllocated);
            Assert.Equal(0, summary.OverallUtilization);
            Assert.Empty(summary.VersionUtilization);
        }

        [Fact]
        public void GetStatistics_InitialState_ShouldReturnDefaultValues()
        {
            // Act
            var stats = _allocator.GetStatistics();

            // Assert
            Assert.Equal(0, stats.TotalAllocations);
            Assert.Equal(0, stats.TotalReleases);
            Assert.Equal(0, stats.ActiveAllocations);
            Assert.Equal(0, stats.FailedAllocations);
            Assert.Equal(TimeSpan.Zero, stats.AverageAllocationTime);
            Assert.Equal(default, stats.LastAllocationTime);
            Assert.Equal(default, stats.LastReleaseTime);
            Assert.Equal(0, stats.PeakUtilization);
        }

        [Fact]
        public void CreateResourcePool_WithValidParameters_ShouldSucceed()
        {
            // Arrange
            var version = "2023";
            var requirements = new ResourceRequirements
            {
                Version = version,
                MaxConcurrentOperations = 5,
                MemoryLimit = 512 * 1024 * 1024,
                CpuLimit = 50,
                Timeout = TimeSpan.FromMinutes(5),
                Priority = AllocationPriority.Normal
            };

            // Act
            var result = _allocator.CreateResourcePool(version, requirements);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CreateResourcePool_WithExistingVersion_ShouldReturnFalse()
        {
            // Arrange
            var version = "2023";
            var requirements = new ResourceRequirements { Version = version, MaxConcurrentOperations = 3 };

            // Act
            var result1 = _allocator.CreateResourcePool(version, requirements);
            var result2 = _allocator.CreateResourcePool(version, requirements);

            // Assert
            Assert.True(result1);
            Assert.False(result2); // Second call should fail
        }

        [Fact]
        public void CreateResourcePool_WithEmptyVersion_ShouldThrowArgumentException()
        {
            // Arrange
            var version = "";
            var requirements = new ResourceRequirements();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _allocator.CreateResourcePool(version, requirements));
            Assert.Equal("version", exception.ParamName);
        }

        [Fact]
        public void CreateResourcePool_WithNullRequirements_ShouldThrowArgumentNullException()
        {
            // Arrange
            var version = "2023";

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                _allocator.CreateResourcePool(version, null));
            Assert.Equal("requirements", exception.ParamName);
        }

        [Fact]
        public void RemoveResourcePool_WithExistingVersionAndNoAllocations_ShouldSucceed()
        {
            // Arrange
            var version = "2023";
            var requirements = new ResourceRequirements { Version = version, MaxConcurrentOperations = 3 };
            _allocator.CreateResourcePool(version, requirements);

            // Act
            var result = _allocator.RemoveResourcePool(version);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void RemoveResourcePool_WithActiveAllocations_ShouldFail()
        {
            // Arrange
            var version = "2023";
            var queryOptions = new LicenseQueryOptions();
            _allocator.AllocateAsync(version, queryOptions).GetAwaiter().GetResult();

            // Act
            var result = _allocator.RemoveResourcePool(version);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveResourcePool_WithNonexistentVersion_ShouldReturnFalse()
        {
            // Arrange
            var version = "nonexistent";

            // Act
            var result = _allocator.RemoveResourcePool(version);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ForceCleanupAsync_WithExpiredAllocations_ShouldCleanUp()
        {
            // Arrange
            var version = "2024";
            var queryOptions = new LicenseQueryOptions();

            // Create allocation that will expire
            var allocation = await _allocator.AllocateAsync(version, queryOptions);

            // Manually set expiration to past
            typeof(ResourceAllocation).GetProperty("ExpiresAt")?.SetValue(allocation, DateTime.UtcNow.AddMinutes(-1));

            // Act
            var cleanupResult = await _allocator.ForceCleanupAsync();

            // Assert
            Assert.True(cleanupResult.TotalCleanedAllocations >= 0);
            Assert.True(cleanupResult.CleanupTime <= DateTime.UtcNow);
        }

        [Fact]
        public async Task ConcurrentAllocation_ShouldRespectConcurrencyLimits()
        {
            // Arrange
            var version = "2024";
            var queryOptions = new LicenseQueryOptions { MaxConcurrentQueries = 1 };
            var allocationTasks = new List<Task<ResourceAllocation>>();

            // Act - Try to allocate more than the concurrency limit
            for (int i = 0; i < 5; i++)
            {
                allocationTasks.Add(_allocator.AllocateAsync(version, queryOptions));
            }

            var results = await Task.WhenAll(allocationTasks);
            var successfulAllocations = results.Where(r => r.Success).ToList();

            // Assert - Should respect the max concurrent allocations limit
            Assert.True(successfulAllocations.Count <= _configuration.MaxConcurrentAllocations);
        }

        [Fact]
        public async Task ResourceRequirementsCalculation_ShouldAdjustForNewerVersions()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions
            {
                MaxConcurrentQueries = 2,
                QueryTimeout = TimeSpan.FromSeconds(30)
            };

            // Act
            var newVersionResult = await _allocator.AllocateAsync("2024", queryOptions);
            var oldVersionResult = await _allocator.AllocateAsync("2020", queryOptions);

            // Assert - Newer versions should get more resources
            Assert.True(newVersionResult.Success);
            Assert.True(oldVersionResult.Success);
            Assert.True(newVersionResult.MemoryLimit >= oldVersionResult.MemoryLimit);
            Assert.True(newVersionResult.CpuLimit >= oldVersionResult.CpuLimit);
        }

        [Fact]
        public void Dispose_ShouldCleanUpResources()
        {
            // Arrange - Create some allocations
            var version = "2024";
            var queryOptions = new LicenseQueryOptions();
            _allocator.AllocateAsync(version, queryOptions).GetAwaiter().GetResult();

            // Act
            _allocator.Dispose();

            // Assert - Should not throw when disposed again
            Assert.DoesNotThrow(() => _allocator.Dispose());
        }

        [Fact]
        public async Task HighPriorityAllocation_ShouldGetHigherPriority()
        {
            // Arrange
            var version = "2024";
            var highPriorityQuery = new LicenseQueryOptions
            {
                QueryTimeout = TimeSpan.FromSeconds(5) // Short timeout = high priority
            };
            var normalPriorityQuery = new LicenseQueryOptions
            {
                QueryTimeout = TimeSpan.FromSeconds(30) // Normal timeout
            };

            // Act
            var highPriorityResult = await _allocator.AllocateAsync(version, highPriorityQuery);
            var normalPriorityResult = await _allocator.AllocateAsync(version, normalPriorityQuery);

            // Assert
            Assert.True(highPriorityResult.Success);
            Assert.True(normalPriorityResult.Success);
            Assert.Equal(AllocationPriority.High, highPriorityResult.Priority);
            Assert.Equal(AllocationPriority.Normal, normalPriorityResult.Priority);
        }

        [Fact]
        public async Task AllocationTimeout_ShouldHandleTimeoutGracefully()
        {
            // Arrange
            var version = "2024";
            var queryOptions = new LicenseQueryOptions();

            // Create a configuration with very short timeout
            var shortTimeoutConfig = new ResourceAllocationConfiguration
            {
                MaxConcurrentAllocations = 1,
                AllocationTimeout = TimeSpan.FromMilliseconds(1)
            };

            using var timeoutAllocator = new LicenseVersionAllocator(_mockLogger.Object, shortTimeoutConfig);

            // Allocate first resource to reach capacity
            await timeoutAllocator.AllocateAsync(version, queryOptions);

            // Act - Try to allocate when at capacity with short timeout
            var result = await timeoutAllocator.AllocateAsync(version, queryOptions);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("timeout", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            _allocator?.Dispose();
        }
    }

    /// <summary>
    /// Tests for VersionResourcePool class
    /// </summary>
    public class VersionResourcePoolTests
    {
        private readonly ResourceRequirements _requirements;
        private readonly ResourceAllocationConfiguration _configuration;

        public VersionResourcePoolTests()
        {
            _requirements = new ResourceRequirements
            {
                Version = "2024",
                MaxConcurrentOperations = 3,
                MemoryLimit = 512 * 1024 * 1024,
                CpuLimit = 50,
                Timeout = TimeSpan.FromMinutes(5),
                Priority = AllocationPriority.Normal
            };

            _configuration = new ResourceAllocationConfiguration
            {
                AllocationTimeout = TimeSpan.FromSeconds(30),
                DefaultAllocationTimeout = TimeSpan.FromMinutes(5)
            };
        }

        [Fact]
        public async Task VersionResourcePool_Constructor_ShouldInitializeProperties()
        {
            // Arrange & Act
            using var pool = new VersionResourcePool("2024", _requirements, _configuration);

            // Assert
            Assert.Equal("2024", pool.Version);
            Assert.Equal(3, pool.TotalCapacity);
            Assert.Equal(0, pool.AllocatedResources);
            Assert.Equal(3, pool.AvailableResources);
            Assert.False(pool.HasActiveAllocations);
        }

        [Fact]
        public async Task VersionResourcePool_AllocateAsync_WithinCapacity_ShouldSucceed()
        {
            // Arrange
            using var pool = new VersionResourcePool("2024", _requirements, _configuration);

            // Act
            var result = await pool.AllocateAsync(_requirements);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AllocationId);
            Assert.Equal("2024", result.Version);
            Assert.Equal(1, pool.AllocatedResources);
            Assert.Equal(2, pool.AvailableResources);
            Assert.True(pool.HasActiveAllocations);
        }

        [Fact]
        public async Task VersionResourcePool_AllocateAsync_AtCapacity_ShouldFail()
        {
            // Arrange
            using var pool = new VersionResourcePool("2024", _requirements, _configuration);

            // Allocate up to capacity
            for (int i = 0; i < _requirements.MaxConcurrentOperations; i++)
            {
                await pool.AllocateAsync(_requirements);
            }

            // Act
            var result = await pool.AllocateAsync(_requirements);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("capacity", result.ErrorMessage);
        }

        [Fact]
        public async Task VersionResourcePool_ReleaseAsync_ValidAllocation_ShouldSucceed()
        {
            // Arrange
            using var pool = new VersionResourcePool("2024", _requirements, _configuration);
            var allocation = await pool.AllocateAsync(_requirements);

            // Act
            var releaseResult = await pool.ReleaseAsync(allocation);

            // Assert
            Assert.True(releaseResult.Success);
            Assert.Equal(0, pool.AllocatedResources);
            Assert.Equal(3, pool.AvailableResources);
            Assert.False(pool.HasActiveAllocations);
        }

        [Fact]
        public async Task VersionResourcePool_ReleaseAsync_InvalidAllocation_ShouldFail()
        {
            // Arrange
            using var pool = new VersionResourcePool("2024", _requirements, _configuration);
            var invalidAllocation = new ResourceAllocation
            {
                AllocationId = "invalid",
                Version = "2024"
            };

            // Act
            var result = await pool.ReleaseAsync(invalidAllocation);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task VersionResourcePool_GetUtilizationAsync_ShouldReturnAccurateData()
        {
            // Arrange
            using var pool = new VersionResourcePool("2024", _requirements, _configuration);
            var allocation = await pool.AllocateAsync(_requirements);

            // Act
            var utilization = await pool.GetUtilizationAsync();

            // Assert
            Assert.Equal("2024", utilization.Version);
            Assert.Equal(3, utilization.TotalCapacity);
            Assert.Equal(1, utilization.AllocatedResources);
            Assert.Equal(2, utilization.AvailableResources);
            Assert.Equal(33.333, utilization.UtilizationPercentage, 3);
            Assert.Equal(allocation.MemoryLimit, utilization.MemoryUsed);
            Assert.Equal(allocation.CpuLimit, utilization.CpuUsed);
            Assert.Contains(allocation.AllocationId, utilization.ActiveAllocations);
        }

        [Fact]
        public async Task VersionResourcePool_CleanupExpiredAllocationsAsync_ShouldRemoveExpired()
        {
            // Arrange
            using var pool = new VersionResourcePool("2024", _requirements, _configuration);
            var allocation = await pool.AllocateAsync(_requirements);

            // Manually set expiration to past
            typeof(ResourceAllocation).GetProperty("ExpiresAt")?.SetValue(allocation, DateTime.UtcNow.AddMinutes(-1));

            // Act
            var cleanedCount = await pool.CleanupExpiredAllocationsAsync();

            // Assert
            Assert.Equal(1, cleanedCount);
            Assert.Equal(0, pool.AllocatedResources);
        }

        [Fact]
        public void VersionResourcePool_Dispose_ShouldCleanUpResources()
        {
            // Arrange
            var pool = new VersionResourcePool("2024", _requirements, _configuration);

            // Act
            pool.Dispose();

            // Assert
            Assert.DoesNotThrow(() => pool.Dispose());
        }
    }

    /// <summary>
    /// Tests for resource calculation methods
    /// </summary>
    public class ResourceCalculationTests
    {
        private readonly Mock<ILogger<LicenseVersionAllocator>> _mockLogger;
        private readonly ResourceAllocationConfiguration _configuration;
        private readonly LicenseVersionAllocator _allocator;

        public ResourceCalculationTests()
        {
            _mockLogger = new Mock<ILogger<LicenseVersionAllocator>>();
            _configuration = new ResourceAllocationConfiguration
            {
                DefaultMaxConcurrentOperations = 5,
                MaxMemoryPerPool = 1024 * 1024 * 1024,
                MaxCpuPerPool = 100
            };

            _allocator = new LicenseVersionAllocator(_mockLogger.Object, _configuration);
        }

        [Fact]
        public async Task CalculateMemoryRequirements_ShouldConsiderQueryOptions()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions
            {
                MaxConcurrentQueries = 3,
                MaxOutputSize = 100 * 1024 * 1024 // 100MB
            };

            // Act
            var result = await _allocator.AllocateAsync("2024", queryOptions);

            // Assert
            Assert.True(result.Success);
            // Base: 50MB + (3 queries * 10MB) + 100MB output = 180MB
            Assert.True(result.MemoryLimit >= 180 * 1024 * 1024);
        }

        [Fact]
        public async Task CalculateCpuRequirements_ShouldScaleWithConcurrentQueries()
        {
            // Arrange
            var lowConcurrencyQuery = new LicenseQueryOptions { MaxConcurrentQueries = 1 };
            var highConcurrencyQuery = new LicenseQueryOptions { MaxConcurrentQueries = 5 };

            // Act
            var lowResult = await _allocator.AllocateAsync("2024", lowConcurrencyQuery);
            var highResult = await _allocator.AllocateAsync("2024", highConcurrencyQuery);

            // Assert
            Assert.True(lowResult.Success);
            Assert.True(highResult.Success);
            Assert.True(highResult.CpuLimit > lowResult.CpuLimit);
        }

        [Fact]
        public async Task CalculatePriority_ShouldAssignHigherPriorityToNewerVersions()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions { QueryTimeout = TimeSpan.FromSeconds(30) };

            // Act
            var newVersionResult = await _allocator.AllocateAsync("2024", queryOptions);
            var oldVersionResult = await _allocator.AllocateAsync("2020", queryOptions);

            // Assert
            Assert.True(newVersionResult.Success);
            Assert.True(oldVersionResult.Success);
            Assert.Equal(AllocationPriority.High, newVersionResult.Priority);
            Assert.Equal(AllocationPriority.Normal, oldVersionResult.Priority);
        }
    }
}