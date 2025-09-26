using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Process;
using LicenseReleaseService.VersionManagement;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.VersionManagement
{
    /// <summary>
    /// Comprehensive unit tests for MultiVersionLicenseManager
    /// </summary>
    public class MultiVersionLicenseManagerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<MultiVersionLicenseManager>> _loggerMock;
        private readonly Mock<SolidWorksVersionDetector> _versionDetectorMock;
        private readonly Mock<IProcessExecutor> _processExecutorMock;
        private readonly Mock<LicenseVersionAllocator> _versionAllocatorMock;
        private readonly Mock<VersionConflictResolver> _conflictResolverMock;
        private readonly MultiVersionManagementConfiguration _configuration;
        private MultiVersionLicenseManager _manager;

        public MultiVersionLicenseManagerTests(ITestOutputHelper output)
        {
            _output = output;
            _loggerMock = new Mock<ILogger<MultiVersionLicenseManager>>();
            _versionDetectorMock = new Mock<SolidWorksVersionDetector>(_loggerMock.Object);
            _processExecutorMock = new Mock<IProcessExecutor>();
            _versionAllocatorMock = new Mock<LicenseVersionAllocator>(_loggerMock.Object, new ResourceAllocationConfiguration());
            _conflictResolverMock = new Mock<VersionConflictResolver>(_loggerMock.Object, new ConflictResolutionConfiguration());

            _configuration = new MultiVersionManagementConfiguration
            {
                MaxConcurrentOperations = 3,
                MaxConcurrentVersionQueries = 2,
                MaximumVersionSpan = 5,
                AllocationStrategy = ResourceAllocationStrategy.NewestFirst
            };

            CreateManager();
        }

        private void CreateManager()
        {
            _manager = new MultiVersionLicenseManager(
                _loggerMock.Object,
                _versionDetectorMock.Object,
                _processExecutorMock.Object,
                _versionAllocatorMock.Object,
                _conflictResolverMock.Object,
                _configuration);
        }

        [Fact]
        public async Task Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new MultiVersionLicenseManager(
                    null,
                    _versionDetectorMock.Object,
                    _processExecutorMock.Object,
                    _versionAllocatorMock.Object,
                    _conflictResolverMock.Object,
                    _configuration);
            }));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_NullVersionDetector_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new MultiVersionLicenseManager(
                    _loggerMock.Object,
                    null,
                    _processExecutorMock.Object,
                    _versionAllocatorMock.Object,
                    _conflictResolverMock.Object,
                    _configuration);
            }));

            Assert.Equal("versionDetector", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_NullProcessExecutor_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new MultiVersionLicenseManager(
                    _loggerMock.Object,
                    _versionDetectorMock.Object,
                    null,
                    _versionAllocatorMock.Object,
                    _conflictResolverMock.Object,
                    _configuration);
            }));

            Assert.Equal("processExecutor", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_NullVersionAllocator_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new MultiVersionLicenseManager(
                    _loggerMock.Object,
                    _versionDetectorMock.Object,
                    _processExecutorMock.Object,
                    null,
                    _conflictResolverMock.Object,
                    _configuration);
            }));

            Assert.Equal("versionAllocator", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_NullConflictResolver_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new MultiVersionLicenseManager(
                    _loggerMock.Object,
                    _versionDetectorMock.Object,
                    _processExecutorMock.Object,
                    _versionAllocatorMock.Object,
                    null,
                    _configuration);
            }));

            Assert.Equal("conflictResolver", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_NullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new MultiVersionLicenseManager(
                    _loggerMock.Object,
                    _versionDetectorMock.Object,
                    _processExecutorMock.Object,
                    _versionAllocatorMock.Object,
                    _conflictResolverMock.Object,
                    null);
            }));

            Assert.Equal("configuration", exception.ParamName);
        }

        [Fact]
        public async Task InitializeAsync_VersionDetectionFails_ReturnsError()
        {
            // Arrange
            var detectionResult = new VersionDetectionResult
            {
                Success = false,
                Error = "Detection failed"
            };

            _versionDetectorMock
                .Setup(x => x.DetectInstalledVersionsAsync())
                .ReturnsAsync(detectionResult);

            // Act
            var result = await _manager.InitializeAsync();

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Detection failed", result.Errors);
            Assert.Equal(0, result.ManagedVersionCount);
        }

        [Fact]
        public async Task InitializeAsync_NoVersionsDetected_ReturnsWarning()
        {
            // Arrange
            var detectionResult = new VersionDetectionResult
            {
                Success = true,
                TotalVersions = 0
            };

            _versionDetectorMock
                .Setup(x => x.DetectInstalledVersionsAsync())
                .ReturnsAsync(detectionResult);

            _versionDetectorMock
                .Setup(x => x.GetAvailableVersions())
                .Returns(new List<SolidWorksVersionInfo>());

            // Act
            var result = await _manager.InitializeAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.ManagedVersionCount);
            Assert.Contains("No SolidWorks versions detected", result.Errors);
        }

        [Fact]
        public async Task InitializeAsync_ValidVersions_ReturnsSuccess()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo("2025", @"C:\SW2025") { IsAvailable = true, IsSupported = true },
                new SolidWorksVersionInfo("2024", @"C:\SW2024") { IsAvailable = true, IsSupported = true }
            };

            var detectionResult = new VersionDetectionResult
            {
                Success = true,
                TotalVersions = 2,
                AvailableVersions = 2,
                SupportedVersions = 2
            };

            _versionDetectorMock
                .Setup(x => x.DetectInstalledVersionsAsync())
                .ReturnsAsync(detectionResult);

            _versionDetectorMock
                .Setup(x => x.GetAvailableVersions())
                .Returns(versions);

            _conflictResolverMock
                .Setup(x => x.DetectVersionConflictsAsync(It.IsAny<List<SolidWorksVersionInfo>>()))
                .ReturnsAsync(new List<VersionConflict>());

            // Act
            var result = await _manager.InitializeAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.ManagedVersionCount);
            Assert.Equal(2, result.InitializedVersions.Count);
            Assert.Equal(2, result.TotalVersionCount);
        }

        [Fact]
        public async Task QueryAllVersionsAsync_NoManagedVersions_ReturnsWarning()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions();

            // Act
            var result = await _manager.QueryAllVersionsAsync(queryOptions);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.SuccessfulVersionCount);
            Assert.Contains("No managed versions available", result.Warnings);
        }

        [Fact]
        public async Task QueryAllVersionsAsync_ValidVersions_ReturnsResults()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo("2025", @"C:\SW2025") { IsAvailable = true, IsSupported = true },
                new SolidWorksVersionInfo("2024", @"C:\SW2024") { IsAvailable = true, IsSupported = true }
            };

            var detectionResult = new VersionDetectionResult
            {
                Success = true,
                TotalVersions = 2,
                AvailableVersions = 2,
                SupportedVersions = 2
            };

            _versionDetectorMock
                .Setup(x => x.DetectInstalledVersionsAsync())
                .ReturnsAsync(detectionResult);

            _versionDetectorMock
                .Setup(x => x.GetAvailableVersions())
                .Returns(versions);

            _conflictResolverMock
                .Setup(x => x.DetectVersionConflictsAsync(It.IsAny<List<SolidWorksVersionInfo>>()))
                .ReturnsAsync(new List<VersionConflict>());

            // Initialize the manager first
            await _manager.InitializeAsync();

            var queryOptions = new LicenseQueryOptions();

            // Act
            var result = await _manager.QueryAllVersionsAsync(queryOptions);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.SuccessfulVersionCount);
            Assert.Equal(2, result.VersionResults.Count);
        }

        [Fact]
        public async Task QueryVersionAsync_NullVersion_ThrowsArgumentNullException()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _manager.QueryVersionAsync(null, queryOptions));

            Assert.Equal("version", exception.ParamName);
        }

        [Fact]
        public async Task QueryVersionAsync_NullQueryOptions_ThrowsArgumentNullException()
        {
            // Arrange
            var version = new SolidWorksVersionInfo("2025", @"C:\SW2025");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _manager.QueryVersionAsync(version, null));

            Assert.Equal("queryOptions", exception.ParamName);
        }

        [Fact]
        public async Task QueryVersionAsync_UnmanagedVersion_ReturnsError()
        {
            // Arrange
            var version = new SolidWorksVersionInfo("2025", @"C:\SW2025");
            var queryOptions = new LicenseQueryOptions();

            // Act
            var result = await _manager.QueryVersionAsync(version, queryOptions);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not managed", result.Errors[0]);
        }

        [Fact]
        public async Task AllocateLicensesAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _manager.AllocateLicensesAsync(null));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task AllocateLicensesAsync_NoManagedVersions_ReturnsFailure()
        {
            // Arrange
            var request = new MultiVersionAllocationRequest
            {
                FeatureName = "sldworks",
                RequiredLicenses = 1
            };

            // Act
            var result = await _manager.AllocateLicensesAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No managed versions available", result.ErrorMessage);
        }

        [Fact]
        public async Task ReleaseLicensesAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _manager.ReleaseLicensesAsync(null));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task ReleaseLicensesAsync_UnmanagedVersion_ReturnsFailure()
        {
            // Arrange
            var request = new MultiVersionReleaseRequest
            {
                Version = "2025",
                FeatureName = "sldworks",
                User = "testuser",
                LicenseCount = 1
            };

            // Act
            var result = await _manager.ReleaseLicensesAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not managed", result.ErrorMessage);
        }

        [Fact]
        public async Task GetHealthStatusAsync_NoManagedVersions_ReturnsUnhealthy()
        {
            // Act
            var result = await _manager.GetHealthStatusAsync();

            // Assert
            Assert.Equal(MultiVersionHealthLevel.Unhealthy, result.OverallHealth);
            Assert.Equal(0, result.TotalVersionCount);
            Assert.Equal(0, result.HealthyVersionCount);
        }

        [Fact]
        public async Task RefreshVersionsAsync_DetectionFails_ReturnsFailure()
        {
            // Arrange
            var detectionResult = new VersionDetectionResult
            {
                Success = false,
                Error = "Refresh failed"
            };

            _versionDetectorMock
                .Setup(x => x.RefreshDetectionAsync())
                .ReturnsAsync(detectionResult);

            // Act
            var result = await _manager.RefreshVersionsAsync();

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Refresh failed", result.ErrorMessage);
        }

        [Fact]
        public async Task RefreshVersionsAsync_ValidRefresh_ReturnsSuccess()
        {
            // Arrange
            var oldVersions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo("2024", @"C:\SW2024") { IsAvailable = true, IsSupported = true }
            };

            var newVersions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo("2024", @"C:\SW2024") { IsAvailable = true, IsSupported = true },
                new SolidWorksVersionInfo("2025", @"C:\SW2025") { IsAvailable = true, IsSupported = true }
            };

            var detectionResult = new VersionDetectionResult
            {
                Success = true,
                TotalVersions = 2,
                AvailableVersions = 2,
                SupportedVersions = 2
            };

            _versionDetectorMock
                .Setup(x => x.RefreshDetectionAsync())
                .ReturnsAsync(detectionResult);

            _versionDetectorMock
                .Setup(x => x.GetAvailableVersions())
                .Returns(newVersions);

            // Initialize with old versions first
            await _manager.InitializeAsync();

            // Mock the current state to have old versions
            _versionDetectorMock
                .Setup(x => x.GetAvailableVersions())
                .Returns(newVersions);

            _conflictResolverMock
                .Setup(x => x.DetectVersionConflictsAsync(It.IsAny<List<SolidWorksVersionInfo>>()))
                .ReturnsAsync(new List<VersionConflict>());

            // Act
            var result = await _manager.RefreshVersionsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.AddedVersions.Count);
            Assert.Equal("2025", result.AddedVersions[0].Version);
            Assert.Equal(0, result.RemovedVersions.Count);
        }

        [Fact]
        public async Task ManagedVersions_ReturnsAvailableAndSupportedVersions()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo("2025", @"C:\SW2025") { IsAvailable = true, IsSupported = true },
                new SolidWorksVersionInfo("2024", @"C:\SW2024") { IsAvailable = false, IsSupported = true }, // Not available
                new SolidWorksVersionInfo("2023", @"C:\SW2023") { IsAvailable = true, IsSupported = false }  // Not supported
            };

            var detectionResult = new VersionDetectionResult
            {
                Success = true,
                TotalVersions = 3,
                AvailableVersions = 2,
                SupportedVersions = 2
            };

            _versionDetectorMock
                .Setup(x => x.DetectInstalledVersionsAsync())
                .ReturnsAsync(detectionResult);

            _versionDetectorMock
                .Setup(x => x.GetAvailableVersions())
                .Returns(versions.Where(v => v.IsAvailable).ToList());

            _conflictResolverMock
                .Setup(x => x.DetectVersionConflictsAsync(It.IsAny<List<SolidWorksVersionInfo>>()))
                .ReturnsAsync(new List<VersionConflict>());

            await _manager.InitializeAsync();

            // Act
            var managedVersions = _manager.ManagedVersions;

            // Assert
            Assert.Single(managedVersions);
            Assert.Equal("2025", managedVersions[0].Version);
            Assert.True(managedVersions[0].IsAvailable);
            Assert.True(managedVersions[0].IsSupported);
        }

        [Fact]
        public async Task ConcurrentOperations_RespectsConcurrencyLimit()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo("2025", @"C:\SW2025") { IsAvailable = true, IsSupported = true }
            };

            var detectionResult = new VersionDetectionResult
            {
                Success = true,
                TotalVersions = 1,
                AvailableVersions = 1,
                SupportedVersions = 1
            };

            _versionDetectorMock
                .Setup(x => x.DetectInstalledVersionsAsync())
                .ReturnsAsync(detectionResult);

            _versionDetectorMock
                .Setup(x => x.GetAvailableVersions())
                .Returns(versions);

            _conflictResolverMock
                .Setup(x => x.DetectVersionConflictsAsync(It.IsAny<List<SolidWorksVersionInfo>>()))
                .ReturnsAsync(new List<VersionConflict>());

            await _manager.InitializeAsync();

            var queryOptions = new LicenseQueryOptions();
            var version = versions[0];

            // Create tasks that will run concurrently
            var tasks = new List<Task<VersionSpecificQueryResult>>();
            var taskCount = 5; // More than the concurrency limit

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(_manager.QueryVersionAsync(version, queryOptions));
            }

            // Act - all tasks should complete without deadlock
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(taskCount, results.Length);
            Assert.All(results, r => Assert.NotNull(r));
        }

        [Fact]
        public async Task Dispose_CleansUpResources()
        {
            // Arrange
            var versions = new List<SolidWorksVersionInfo>
            {
                new SolidWorksVersionInfo("2025", @"C:\SW2025") { IsAvailable = true, IsSupported = true }
            };

            var detectionResult = new VersionDetectionResult
            {
                Success = true,
                TotalVersions = 1,
                AvailableVersions = 1,
                SupportedVersions = 1
            };

            _versionDetectorMock
                .Setup(x => x.DetectInstalledVersionsAsync())
                .ReturnsAsync(detectionResult);

            _versionDetectorMock
                .Setup(x => x.GetAvailableVersions())
                .Returns(versions);

            _conflictResolverMock
                .Setup(x => x.DetectVersionConflictsAsync(It.IsAny<List<SolidWorksVersionInfo>>()))
                .ReturnsAsync(new List<VersionConflict>());

            await _manager.InitializeAsync();

            // Act
            _manager.Dispose();

            // Assert - manager should be disposed and throw ObjectDisposedException on further operations
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                _manager.QueryAllVersionsAsync(new LicenseQueryOptions()));

            Assert.Contains("MultiVersionLicenseManager", exception.Message);
        }

        public void Dispose()
        {
            _manager?.Dispose();
        }
    }

    /// <summary>
    /// Test classes for MultiVersionLicenseManager
    /// </summary>
    public class MultiVersionInitializationResult
    {
        public bool Success { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<VersionConflict> Conflicts { get; set; } = new List<VersionConflict>();
        public List<SolidWorksVersionInfo> InitializedVersions { get; set; } = new List<SolidWorksVersionInfo>();
        public TimeSpan InitializationTime { get; set; }
        public int ManagedVersionCount { get; set; }
        public int TotalVersionCount { get; set; }
    }

    public class MultiVersionQueryResult
    {
        public bool Success { get; set; } = true;
        public List<VersionSpecificQueryResult> VersionResults { get; set; } = new List<VersionSpecificQueryResult>();
        public int SuccessfulVersionCount { get; set; }
        public int FailedVersionCount { get; set; }
        public int TotalLicenseCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<VersionConflict> Conflicts { get; set; } = new List<VersionConflict>();
        public List<VersionConflict> ResolvedConflicts { get; set; } = new List<VersionConflict>();
        public TimeSpan QueryTime { get; set; }
    }

    public class VersionSpecificQueryResult
    {
        public string Version { get; set; }
        public bool Success { get; set; }
        public int LicenseCount { get; set; }
        public int AvailableLicenses { get; set; }
        public int UsedLicenses { get; set; }
        public int TotalLicenses { get; set; }
        public List<string> Users { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public bool Cached { get; set; }
        public DateTime QueryTimestamp { get; set; }
    }

    public class MultiVersionAllocationRequest
    {
        public string FeatureName { get; set; }
        public int RequiredLicenses { get; set; }
        public string User { get; set; }
        public string Host { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class MultiVersionAllocationResult
    {
        public bool Success { get; set; }
        public string AllocatedVersion { get; set; }
        public string ErrorMessage { get; set; }
        public VersionAllocationResult AllocationDetails { get; set; }
        public List<VersionAllocationAttempt> FailedAttempts { get; set; } = new List<VersionAllocationAttempt>();
        public TimeSpan AllocationTime { get; set; }
    }

    public class VersionAllocationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int AllocatedLicenses { get; set; }
        public int RemainingLicenses { get; set; }
        public string AllocationId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class VersionAllocationAttempt
    {
        public string Version { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class MultiVersionReleaseRequest
    {
        public string Version { get; set; }
        public string FeatureName { get; set; }
        public string User { get; set; }
        public int LicenseCount { get; set; }
        public string AllocationId { get; set; }
    }

    public class MultiVersionReleaseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<ReleasedLicense> ReleasedLicenses { get; set; } = new List<ReleasedLicense>();
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public TimeSpan ReleaseTime { get; set; }
    }

    public class MultiVersionHealthResult
    {
        public List<VersionHealthResult> VersionHealth { get; set; } = new List<VersionHealthResult>();
        public int HealthyVersionCount { get; set; }
        public int TotalVersionCount { get; set; }
        public MultiVersionHealthLevel OverallHealth { get; set; }
        public List<SystemWideIssue> SystemWideIssues { get; set; } = new List<SystemWideIssue>();
    }

    public enum MultiVersionHealthLevel
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    public class SystemWideIssue
    {
        public string IssueType { get; set; }
        public int AffectedVersionCount { get; set; }
        public SystemWideIssueSeverity Severity { get; set; }
        public string Description { get; set; }
    }

    public enum SystemWideIssueSeverity
    {
        Warning,
        Critical
    }

    public class MultiVersionRefreshResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<SolidWorksVersionInfo> AddedVersions { get; set; } = new List<SolidWorksVersionInfo>();
        public List<SolidWorksVersionInfo> RemovedVersions { get; set; } = new List<SolidWorksVersionInfo>();
        public TimeSpan RefreshTime { get; set; }
    }

    public class VersionHealthResult
    {
        public string Version { get; set; }
        public bool IsHealthy { get; set; }
        public string Status { get; set; }
        public DateTime LastCheck { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
    }

    public class ResourceAllocation
    {
        public string AllocationId { get; set; }
        public string Version { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ReleasedLicense
    {
        public string FeatureName { get; set; }
        public string User { get; set; }
        public int ReleasedCount { get; set; }
        public DateTime ReleasedAt { get; set; }
    }
}