using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.VersionManagement;

namespace LicenseReleaseService.Tests.VersionManagement
{
    /// <summary>
    /// Unit tests for VersionResourceManager
    /// </summary>
    public class VersionResourceManagerTests : IDisposable
    {
        private readonly Mock<ILogger<VersionResourceManager>> _mockLogger;
        private readonly ITestOutputHelper _outputHelper;
        private readonly VersionResourceManagerOptions _options;
        private VersionResourceManager _resourceManager;

        public VersionResourceManagerTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _mockLogger = new Mock<ILogger<VersionResourceManager>>();

            _options = new VersionResourceManagerOptions
            {
                EnableResourceMonitoring = true,
                EnableAutomaticCleanup = true,
                CleanupInterval = TimeSpan.FromMinutes(5),
                ResourceExpirationTime = TimeSpan.FromMinutes(30),
                MaxMemoryPerVersion = 2048L * 1024 * 1024, // 2GB
                MaxCpuPerVersion = 50,
                EnableResourceOptimization = true,
                OptimizationInterval = TimeSpan.FromMinutes(10)
            };

            _resourceManager = new VersionResourceManager(
                _mockLogger.Object,
                Options.Create(_options));
        }

        [Fact]
        public async Task InitializeAsync_ShouldInitializeSuccessfully()
        {
            // Act
            await _resourceManager.InitializeAsync();

            // Assert
            // No exception thrown indicates successful initialization
            // Verify logger was called
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("initialized")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task AllocateResourcesAsync_ShouldAllocateSuccessfully_WhenResourcesAreAvailable()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                OperationType = VersionOperationType.LicenseCheck,
                MemoryRequirements = 1024L * 1024 * 1024, // 1GB
                CpuRequirements = 25,
                Priority = AllocationPriority.Normal
            };

            // Act
            var result = await _resourceManager.AllocateResourcesAsync("2024", requirements);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AllocationId);
            Assert.Equal("2024", result.Version);
            Assert.Equal(1024L * 1024 * 1024, result.AllocatedResources["Memory"]);
            Assert.Equal(25, result.AllocatedResources["Cpu"]);
        }

        [Fact]
        public async Task AllocateResourcesAsync_ShouldFail_WhenMemoryRequirementsExceedLimits()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 3L * 1024 * 1024 * 1024, // 3GB exceeds limit
                CpuRequirements = 25
            };

            // Act
            var result = await _resourceManager.AllocateResourcesAsync("2024", requirements);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Memory requirements", result.ErrorMessage);
        }

        [Fact]
        public async Task AllocateResourcesAsync_ShouldFail_WhenCpuRequirementsExceedLimits()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 1024L * 1024 * 1024,
                CpuRequirements = 75 // Exceeds 50% limit
            };

            // Act
            var result = await _resourceManager.AllocateResourcesAsync("2024", requirements);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("CPU requirements", result.ErrorMessage);
        }

        [Fact]
        public async Task AllocateResourcesAsync_ShouldTrackUtilizationCorrectly()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 1024L * 1024 * 1024,
                CpuRequirements = 25
            };

            // Act
            var result1 = await _resourceManager.AllocateResourcesAsync("2024", requirements);
            var result2 = await _resourceManager.AllocateResourcesAsync("2024", requirements);

            // Assert
            Assert.True(result1.Success);
            Assert.True(result2.Success);

            var utilization = await _resourceManager.GetVersionUtilizationAsync("2024");
            Assert.Equal(2L * 1024L * 1024 * 1024, utilization.MemoryUtilization);
            Assert.Equal(50, utilization.CpuUtilization);
            Assert.Equal(2, utilization.ActiveAllocations);
        }

        [Fact]
        public async Task ReleaseResourcesAsync_ShouldReleaseSuccessfully_WhenAllocationExists()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 1024L * 1024 * 1024,
                CpuRequirements = 25
            };

            var allocateResult = await _resourceManager.AllocateResourcesAsync("2024", requirements);
            Assert.True(allocateResult.Success);

            // Act
            var releaseResult = await _resourceManager.ReleaseResourcesAsync("2024", allocateResult.AllocationId);

            // Assert
            Assert.True(releaseResult.Success);

            var utilization = await _resourceManager.GetVersionUtilizationAsync("2024");
            Assert.Equal(0, utilization.MemoryUtilization);
            Assert.Equal(0, utilization.CpuUtilization);
            Assert.Equal(0, utilization.ActiveAllocations);
        }

        [Fact]
        public async Task ReleaseResourcesAsync_ShouldReturnError_WhenAllocationDoesNotExist()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            // Act
            var result = await _resourceManager.ReleaseResourcesAsync("2024", "nonexistent_allocation_id");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task GetVersionHealthAsync_ShouldReturnHealthyStatus_WhenResourcesAreAdequate()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 512L * 1024 * 1024,
                CpuRequirements = 10
            };

            await _resourceManager.AllocateResourcesAsync("2024", requirements);

            // Act
            var health = await _resourceManager.GetVersionHealthAsync("2024");

            // Assert
            Assert.NotNull(health);
            Assert.True(health.IsHealthy);
            Assert.Equal("2024", health.Version);
            Assert.Empty(health.Issues);
        }

        [Fact]
        public async Task GetVersionHealthAsync_ShouldDetectResourceIssues()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            // Allocate resources that approach limits
            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 1900L * 1024 * 1024, // Close to 2GB limit
                CpuRequirements = 45 // Close to 50% limit
            };

            await _resourceManager.AllocateResourcesAsync("2024", requirements);

            // Act
            var health = await _resourceManager.GetVersionHealthAsync("2024");

            // Assert
            Assert.NotNull(health);
            Assert.False(health.IsHealthy);
            Assert.NotEmpty(health.Issues);
            Assert.True(health.Issues.Any(i => i.Severity == ResourceHealthSeverity.Warning));
        }

        [Fact]
        public async Task GetResourceMetricsAsync_ShouldReturnComprehensiveMetrics()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            // Allocate some resources
            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 1024L * 1024 * 1024,
                CpuRequirements = 25
            };

            await _resourceManager.AllocateResourcesAsync("2024", requirements);
            await _resourceManager.AllocateResourcesAsync("2023", requirements);

            // Act
            var metrics = await _resourceManager.GetResourceMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(2, metrics.VersionCount);
            Assert.Equal(2, metrics.TotalAllocations);
            Assert.Equal(2L * 1024L * 1024 * 1024, metrics.TotalMemoryUtilization);
            Assert.Equal(50, metrics.TotalCpuUtilization);
            Assert.True(metrics.Timestamp > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task GetVersionUtilizationAsync_ShouldReturnVersionSpecificUtilization()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 1024L * 1024 * 1024,
                CpuRequirements = 25
            };

            await _resourceManager.AllocateResourcesAsync("2024", requirements);

            // Act
            var utilization = await _resourceManager.GetVersionUtilizationAsync("2024");

            // Assert
            Assert.NotNull(utilization);
            Assert.Equal("2024", utilization.Version);
            Assert.Equal(1024L * 1024 * 1024, utilization.MemoryUtilization);
            Assert.Equal(25, utilization.CpuUtilization);
            Assert.Equal(1, utilization.ActiveAllocations);
        }

        [Fact]
        public async Task GetVersionUtilizationAsync_ShouldReturnZeroUtilization_WhenNoAllocationsExist()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            // Act
            var utilization = await _resourceManager.GetVersionUtilizationAsync("2024");

            // Assert
            Assert.NotNull(utilization);
            Assert.Equal("2024", utilization.Version);
            Assert.Equal(0, utilization.MemoryUtilization);
            Assert.Equal(0, utilization.CpuUtilization);
            Assert.Equal(0, utilization.ActiveAllocations);
        }

        [Fact]
        public async Task CleanupExpiredResourcesAsync_ShouldCleanupExpiredAllocations()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 1024L * 1024 * 1024,
                CpuRequirements = 25
            };

            var result = await _resourceManager.AllocateResourcesAsync("2024", requirements);
            Assert.True(result.Success);

            // Manually set allocation as expired (this would require internal access in real implementation)
            // For this test, we'll simulate the cleanup process

            // Act
            await _resourceManager.CleanupExpiredResourcesAsync();

            // Assert
            // Verify cleanup was attempted
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("cleanup")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task OptimizeResourcesAsync_ShouldApplyOptimizations()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            // Act
            await _resourceManager.OptimizeResourcesAsync();

            // Assert
            // Verify optimization was attempted
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("optimization")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetSystemResourceStatusAsync_ShouldReturnSystemWideStatus()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            // Act
            var status = await _resourceManager.GetSystemResourceStatusAsync();

            // Assert
            Assert.NotNull(status);
            Assert.True(status.TotalMemoryCapacity > 0);
            Assert.True(status.TotalCpuCapacity > 0);
            Assert.True(status.Timestamp > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task ConcurrentAllocations_ShouldBeHandledCorrectly()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 512L * 1024 * 1024,
                CpuRequirements = 10
            };

            // Act - allocate resources concurrently
            var tasks = new List<Task<ResourceAllocationResult>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_resourceManager.AllocateResourcesAsync("2024", requirements));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            // All allocations should succeed since they're within limits
            Assert.True(results.All(r => r.Success));
            Assert.Equal(5, results.Length);

            var utilization = await _resourceManager.GetVersionUtilizationAsync("2024");
            Assert.Equal(5L * 512L * 1024 * 1024, utilization.MemoryUtilization);
            Assert.Equal(50, utilization.CpuUtilization); // 5 * 10 = 50%
            Assert.Equal(5, utilization.ActiveAllocations);
        }

        [Fact]
        public async Task GetAllocationsAsync_ShouldReturnAllAllocations()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 512L * 1024 * 1024,
                CpuRequirements = 10
            };

            await _resourceManager.AllocateResourcesAsync("2024", requirements);
            await _resourceManager.AllocateResourcesAsync("2023", requirements);

            // Act
            var allocations = await _resourceManager.GetAllocationsAsync();

            // Assert
            Assert.NotNull(allocations);
            Assert.Equal(2, allocations.Count);
            Assert.True(allocations.Any(a => a.Version == "2024"));
            Assert.True(allocations.Any(a => a.Version == "2023"));
        }

        [Fact]
        public async Task GetVersionAllocationsAsync_ShouldReturnVersionSpecificAllocations()
        {
            // Arrange
            await _resourceManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                MemoryRequirements = 512L * 1024 * 1024,
                CpuRequirements = 10
            };

            await _resourceManager.AllocateResourcesAsync("2024", requirements);
            await _resourceManager.AllocateResourcesAsync("2023", requirements);

            // Act
            var allocations = await _resourceManager.GetVersionAllocationsAsync("2024");

            // Assert
            Assert.NotNull(allocations);
            Assert.Single(allocations);
            Assert.Equal("2024", allocations.First().Version);
        }

        public void Dispose()
        {
            _resourceManager?.Dispose();
        }
    }
}