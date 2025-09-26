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
using LicenseReleaseService.Licensing;

namespace LicenseReleaseService.Tests.VersionManagement
{
    /// <summary>
    /// Unit tests for VersionRuntimeManager
    /// </summary>
    public class VersionRuntimeManagerTests : IDisposable
    {
        private readonly Mock<ILogger<VersionRuntimeManager>> _mockLogger;
        private readonly Mock<IVersionResourceManager> _mockResourceManager;
        private readonly Mock<IVersionPerformanceOptimizer> _mockPerformanceOptimizer;
        private readonly Mock<ILicenseManager> _mockLicenseManager;
        private readonly Mock<IVersionConfigurationManager> _mockConfigurationManager;
        private readonly ITestOutputHelper _outputHelper;
        private readonly VersionRuntimeManagerOptions _options;
        private VersionRuntimeManager _runtimeManager;

        public VersionRuntimeManagerTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _mockLogger = new Mock<ILogger<VersionRuntimeManager>>();
            _mockResourceManager = new Mock<IVersionResourceManager>();
            _mockPerformanceOptimizer = new Mock<IVersionPerformanceOptimizer>();
            _mockLicenseManager = new Mock<ILicenseManager>();
            _mockConfigurationManager = new Mock<IVersionConfigurationManager>();

            _options = new VersionRuntimeManagerOptions
            {
                EnableDynamicVersionSelection = true,
                EnableHealthMonitoring = true,
                EnablePerformanceOptimization = true,
                DefaultHealthCheckInterval = TimeSpan.FromSeconds(30),
                MaxConcurrentOperations = 5,
                OperationTimeout = TimeSpan.FromMinutes(5)
            };

            SetupMockDependencies();
            _runtimeManager = new VersionRuntimeManager(
                _mockLogger.Object,
                Options.Create(_options),
                _mockResourceManager.Object,
                _mockPerformanceOptimizer.Object,
                _mockLicenseManager.Object,
                _mockConfigurationManager.Object);
        }

        private void SetupMockDependencies()
        {
            // Setup license manager mock
            _mockLicenseManager.Setup(x => x.GetAvailableVersionsAsync())
                .ReturnsAsync(new List<string> { "2024", "2023", "2022" });

            _mockLicenseManager.Setup(x => x.GetVersionInfoAsync(It.IsAny<string>()))
                .ReturnsAsync((string version) => new SolidWorksVersionInfo
                {
                    Version = version,
                    InstallPath = $@"C:\Program Files\SolidWorks {version}",
                    IsAvailable = true,
                    LastAccessTime = DateTime.UtcNow.AddHours(-1),
                    LicenseType = LicenseType.Network,
                    MaxConcurrentUsers = 10,
                    CurrentUsers = 2
                });

            // Setup resource manager mock
            _mockResourceManager.Setup(x => x.AllocateResourcesAsync(It.IsAny<string>(), It.IsAny<VersionOperationRequirements>()))
                .ReturnsAsync(new ResourceAllocationResult
                {
                    Success = true,
                    AllocationId = Guid.NewGuid().ToString(),
                    AllocatedResources = new Dictionary<string, object>
                    {
                        ["Memory"] = 1024L,
                        ["Cpu"] = 25
                    }
                });

            // Setup performance optimizer mock
            _mockPerformanceOptimizer.Setup(x => x.OptimizeForOperationAsync(It.IsAny<VersionOperationType>(), It.IsAny<string>()))
                .ReturnsAsync(new AppliedOptimization
                {
                    Success = true,
                    StrategyName = "TestOptimization",
                    Type = OptimizationType.PerformanceTuning
                });

            // Setup configuration manager mock
            _mockConfigurationManager.Setup(x => x.GetVersionConfigurationAsync(It.IsAny<string>()))
                .ReturnsAsync((string version) => new VersionConfiguration
                {
                    Version = version,
                    IsEnabled = true,
                    Priority = version == "2024" ? 1 : 2,
                    ResourceLimits = new Dictionary<string, long>
                    {
                        ["Memory"] = 2048,
                        ["Cpu"] = 50
                    }
                });
        }

        [Fact]
        public async Task InitializeAsync_ShouldInitializeSuccessfully_WhenAllDependenciesAreAvailable()
        {
            // Act
            var result = await _runtimeManager.InitializeAsync();

            // Assert
            Assert.True(result.LicenseManagerInitialized);
            Assert.True(result.InitializedVersions.Count > 0);
            Assert.Equal(3, result.TotalVersionCount);
            Assert.Empty(result.Errors);

            _mockLicenseManager.Verify(x => x.GetAvailableVersionsAsync(), Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_ShouldHandleInitializationErrors_Gracefully()
        {
            // Arrange
            _mockLicenseManager.Setup(x => x.GetAvailableVersionsAsync())
                .ThrowsAsync(new Exception("License service unavailable"));

            // Act
            var result = await _runtimeManager.InitializeAsync();

            // Assert
            Assert.False(result.LicenseManagerInitialized);
            Assert.Single(result.Errors);
            Assert.Contains("License service unavailable", result.Errors.First());
        }

        [Fact]
        public async Task SelectBestVersionAsync_ShouldReturnOptimalVersion_WhenMultipleVersionsAvailable()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                OperationType = VersionOperationType.LicenseCheck,
                Priority = AllocationPriority.High,
                MemoryRequirements = 1024,
                CpuRequirements = 25
            };

            // Act
            var result = await _runtimeManager.SelectBestVersionAsync(VersionOperationType.LicenseCheck, requirements);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.SelectedVersion);
            Assert.Equal("2024", result.SelectedVersion.Version); // Should select highest priority version
            Assert.NotNull(result.RuntimeContext);
        }

        [Fact]
        public async Task SelectBestVersionAsync_ShouldReturnError_WhenNoVersionsAvailable()
        {
            // Arrange
            _mockLicenseManager.Setup(x => x.GetAvailableVersionsAsync())
                .ReturnsAsync(new List<string>());

            await _runtimeManager.InitializeAsync();

            var requirements = new VersionOperationRequirements();

            // Act
            var result = await _runtimeManager.SelectBestVersionAsync(VersionOperationType.LicenseCheck, requirements);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("No available versions", result.ErrorMessage);
        }

        [Fact]
        public async Task SelectBestVersionAsync_ShouldConsiderHealthStatus_WhenSelectingVersion()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                OperationType = VersionOperationType.LicenseCheck,
                Priority = AllocationPriority.Normal
            };

            // Act
            var result = await _runtimeManager.SelectBestVersionAsync(VersionOperationType.LicenseCheck, requirements);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.RuntimeContext);
            // Health status should be checked
            _mockResourceManager.Verify(x => x.GetVersionHealthAsync(It.IsAny<string>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task ExecuteOperationAsync_ShouldExecuteSuccessfully_WhenResourcesAreAvailable()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                OperationType = VersionOperationType.LicenseCheck,
                Priority = AllocationPriority.Normal
            };

            var parameters = new Dictionary<string, object>
            {
                ["TestParam"] = "TestValue"
            };

            // Act
            var result = await _runtimeManager.ExecuteOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                requirements,
                parameters);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(VersionOperationType.LicenseCheck, result.OperationType);
            Assert.Equal("2024", result.Version);
            Assert.True(result.Duration.HasValue);
            Assert.True(result.Duration.Value.TotalMilliseconds > 0);

            // Verify resource allocation and cleanup
            _mockResourceManager.Verify(x => x.AllocateResourcesAsync("2024", requirements), Times.Once);
            _mockResourceManager.Verify(x => x.ReleaseResourcesAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteOperationAsync_ShouldHandleResourceAllocationFailure_Gracefully()
        {
            // Arrange
            _mockResourceManager.Setup(x => x.AllocateResourcesAsync(It.IsAny<string>(), It.IsAny<VersionOperationRequirements>()))
                .ReturnsAsync(new ResourceAllocationResult
                {
                    Success = false,
                    ErrorMessage = "Insufficient resources"
                });

            await _runtimeManager.InitializeAsync();

            var requirements = new VersionOperationRequirements();

            // Act
            var result = await _runtimeManager.ExecuteOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                requirements);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Insufficient resources", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteOperationAsync_ShouldApplyPerformanceOptimization_WhenEnabled()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            var requirements = new VersionOperationRequirements();

            // Act
            var result = await _runtimeManager.ExecuteOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                requirements);

            // Assert
            Assert.True(result.Success);
            _mockPerformanceOptimizer.Verify(x => x.OptimizeForOperationAsync(VersionOperationType.LicenseCheck, "2024"), Times.Once);
        }

        [Fact]
        public async Task GetHealthStatusAsync_ShouldReturnComprehensiveHealthInformation()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            // Act
            var health = await _runtimeManager.GetHealthStatusAsync();

            // Assert
            Assert.NotNull(health);
            Assert.Equal(3, health.TotalVersionCount);
            Assert.True(health.HealthyVersionCount > 0);
            Assert.Equal(RuntimeHealthLevel.Healthy, health.OverallHealth);
            Assert.Empty(health.SystemWideIssues);
        }

        [Fact]
        public async Task GetHealthStatusAsync_ShouldDetectUnhealthyVersions()
        {
            // Arrange
            _mockResourceManager.Setup(x => x.GetVersionHealthAsync("2023"))
                .ReturnsAsync(new VersionResourceHealth
                {
                    Version = "2023",
                    IsHealthy = false,
                    Issues = new List<ResourceHealthIssue>
                    {
                        new ResourceHealthIssue
                        {
                            ResourceType = "Memory",
                            Severity = ResourceHealthSeverity.Critical,
                            Message = "Memory usage exceeded limits"
                        }
                    }
                });

            await _runtimeManager.InitializeAsync();

            // Act
            var health = await _runtimeManager.GetHealthStatusAsync();

            // Assert
            Assert.NotNull(health);
            Assert.True(health.TotalVersionCount > health.HealthyVersionCount);
            Assert.NotEqual(RuntimeHealthLevel.Healthy, health.OverallHealth);
        }

        [Fact]
        public async Task RefreshVersionsAsync_ShouldUpdateVersionList_WhenNewVersionsAreAvailable()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            // Setup new versions
            _mockLicenseManager.Setup(x => x.GetAvailableVersionsAsync())
                .ReturnsAsync(new List<string> { "2024", "2023", "2022", "2025" });

            // Act
            var result = await _runtimeManager.RefreshVersionsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Single(result.AddedVersions);
            Assert.Equal("2025", result.AddedVersions.First().Version);
            Assert.Empty(result.RemovedVersions);
        }

        [Fact]
        public async Task GetRuntimeMetricsAsync_ShouldReturnComprehensiveMetrics()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            // Execute some operations to generate metrics
            await _runtimeManager.ExecuteOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                new VersionOperationRequirements());

            // Act
            var metrics = await _runtimeManager.GetRuntimeMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.TotalOperations > 0);
            Assert.True(metrics.SuccessfulOperations > 0);
            Assert.Equal(1.0, metrics.SuccessRate); // Should be 100% for successful test
            Assert.True(metrics.AverageOperationTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task GetVersionContextAsync_ShouldReturnVersionSpecificContext()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            // Act
            var context = await _runtimeManager.GetVersionContextAsync("2024");

            // Assert
            Assert.NotNull(context);
            Assert.Equal("2024", context.Version);
            Assert.NotNull(context.VersionInfo);
            Assert.True(context.IsActive);
        }

        [Fact]
        public async Task GetVersionContextAsync_ShouldReturnNull_WhenVersionDoesNotExist()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            // Act
            var context = await _runtimeManager.GetVersionContextAsync("NonExistent");

            // Assert
            Assert.Null(context);
        }

        [Fact]
        public async Task CleanupResourcesAsync_ShouldCleanupExpiredResources()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            // Act
            await _runtimeManager.CleanupResourcesAsync();

            // Assert
            _mockResourceManager.Verify(x => x.CleanupExpiredResourcesAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteOperationAsync_ShouldRespectOperationTimeout()
        {
            // Arrange
            await _runtimeManager.InitializeAsync();

            var requirements = new VersionOperationRequirements
            {
                OperationType = VersionOperationType.LicenseCheck,
                Timeout = TimeSpan.FromMilliseconds(100) // Very short timeout
            };

            // Simulate a long-running operation
            _mockResourceManager.Setup(x => x.AllocateResourcesAsync(It.IsAny<string>(), It.IsAny<VersionOperationRequirements>()))
                .ReturnsAsync(new ResourceAllocationResult
                {
                    Success = true,
                    AllocationId = Guid.NewGuid().ToString()
                });

            // Add delay to simulate long operation
            _mockPerformanceOptimizer.Setup(x => x.OptimizeForOperationAsync(It.IsAny<VersionOperationType>(), It.IsAny<string>()))
                .Returns(async () =>
                {
                    await Task.Delay(200); // Exceeds timeout
                    return new AppliedOptimization { Success = true };
                });

            // Act
            var result = await _runtimeManager.ExecuteOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                requirements);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("timeout", result.ErrorMessage.ToLower());
        }

        public void Dispose()
        {
            _runtimeManager?.Dispose();
        }
    }
}