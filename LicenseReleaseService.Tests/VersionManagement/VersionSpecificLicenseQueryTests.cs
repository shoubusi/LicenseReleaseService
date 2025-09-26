using System;
using System.Collections.Generic;
using System.IO;
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
    /// Comprehensive unit tests for VersionSpecificLicenseQuery
    /// </summary>
    public class VersionSpecificLicenseQueryTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<VersionSpecificLicenseQuery>> _loggerMock;
        private readonly Mock<IProcessExecutor> _processExecutorMock;
        private readonly VersionQueryConfiguration _configuration;
        private readonly SolidWorksVersionInfo _versionInfo;
        private VersionSpecificLicenseQuery _query;

        public VersionSpecificLicenseQueryTests(ITestOutputHelper output)
        {
            _output = output;
            _loggerMock = new Mock<ILogger<VersionSpecificLicenseQuery>>();
            _processExecutorMock = new Mock<IProcessExecutor>();

            _configuration = new VersionQueryConfiguration
            {
                QueryTimeout = TimeSpan.FromSeconds(30),
                RetryCount = 3,
                RetryDelay = TimeSpan.FromSeconds(2),
                CacheExpiration = TimeSpan.FromMinutes(5),
                MaxCacheSize = 100,
                EnableCaching = true
            };

            _versionInfo = new SolidWorksVersionInfo("2025", @"C:\Program Files\SolidWorks Corp\SolidWorks 2025")
            {
                IsAvailable = true,
                IsSupported = true,
                ExecutablePath = @"C:\Program Files\SolidWorks Corp\SolidWorks 2025\SLDWORKS.exe",
                LmutilPath = @"C:\Program Files\SolidWorks Corp\SolidWorks 2025\lmutil.exe"
            };

            CreateQuery();
        }

        private void CreateQuery()
        {
            _query = new VersionSpecificLicenseQuery(
                _loggerMock.Object,
                _versionInfo,
                _processExecutorMock.Object,
                _configuration);
        }

        [Fact]
        public async Task Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new VersionSpecificLicenseQuery(
                    null,
                    _versionInfo,
                    _processExecutorMock.Object,
                    _configuration);
            }));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_NullVersionInfo_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new VersionSpecificLicenseQuery(
                    _loggerMock.Object,
                    null,
                    _processExecutorMock.Object,
                    _configuration);
            }));

            Assert.Equal("versionInfo", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_NullProcessExecutor_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new VersionSpecificLicenseQuery(
                    _loggerMock.Object,
                    _versionInfo,
                    null,
                    _configuration);
            }));

            Assert.Equal("processExecutor", exception.ParamName);
        }

        [Fact]
        public async Task Constructor_NullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() =>
            {
                return new VersionSpecificLicenseQuery(
                    _loggerMock.Object,
                    _versionInfo,
                    _processExecutorMock.Object,
                    null);
            }));

            Assert.Equal("configuration", exception.ParamName);
        }

        [Fact]
        public async Task QueryLicensesAsync_NullQueryOptions_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _query.QueryLicensesAsync(null));

            Assert.Equal("queryOptions", exception.ParamName);
        }

        [Fact]
        public async Task QueryLicensesAsync_LmutilExecutionFails_ReturnsError()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions();

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 1,
                StandardError = "lmutil: command not found",
                ExecutionTime = TimeSpan.FromSeconds(1)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // Act
            var result = await _query.QueryLicensesAsync(queryOptions);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("exited with code 1", result.Errors[0]);
        }

        [Fact]
        public async Task QueryLicensesAsync_ValidLmutilOutput_ReturnsSuccess()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions();

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = @"Users of sldworks: (Total of 10 licenses issued; Total of 3 licenses in use)
sldworks user1 host1 (display1) (v1.0)
sldworks user2 host2 (display2) (v1.0)",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(2)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // Act
            var result = await _query.QueryLicensesAsync(queryOptions);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.LicenseCount);
            Assert.Equal(10, result.TotalLicenses);
            Assert.Equal(3, result.UsedLicenses);
            Assert.Equal(7, result.AvailableLicenses);
            Assert.Equal(2, result.Users.Count);
            Assert.Contains("user1", result.Users);
            Assert.Contains("user2", result.Users);
        }

        [Fact]
        public async Task QueryLicensesAsync_CacheEnabled_ReturnsCachedResult()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions();

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = @"Users of sldworks: (Total of 10 licenses issued; Total of 3 licenses in use)",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(2)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // First call should execute lmutil
            var result1 = await _query.QueryLicensesAsync(queryOptions);
            Assert.True(result1.Success);
            Assert.False(result1.Cached);

            // Second call should use cache
            var result2 = await _query.QueryLicensesAsync(queryOptions);
            Assert.True(result2.Success);
            Assert.True(result2.Cached);

            // Verify lmutil was called only once
            _processExecutorMock.Verify(
                x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task QueryLicensesAsync_CacheDisabled_AlwaysExecutesLmutil()
        {
            // Arrange
            _configuration.EnableCaching = false;
            CreateQuery(); // Recreate with new configuration

            var queryOptions = new LicenseQueryOptions();

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = @"Users of sldworks: (Total of 10 licenses issued; Total of 3 licenses in use)",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(2)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // Both calls should execute lmutil
            var result1 = await _query.QueryLicensesAsync(queryOptions);
            var result2 = await _query.QueryLicensesAsync(queryOptions);

            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.False(result1.Cached);
            Assert.False(result2.Cached);

            // Verify lmutil was called twice
            _processExecutorMock.Verify(
                x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task TryAllocateAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _query.TryAllocateAsync(null));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task TryAllocateAsync_QueryFails_ReturnsFailure()
        {
            // Arrange
            var request = new MultiVersionAllocationRequest
            {
                FeatureName = "sldworks",
                RequiredLicenses = 1
            };

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 1,
                StandardError = "Query failed",
                ExecutionTime = TimeSpan.FromSeconds(1)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // Act
            var result = await _query.TryAllocateAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("License query failed", result.ErrorMessage);
        }

        [Fact]
        public async Task TryAllocateAsync_InsufficientLicenses_ReturnsFailure()
        {
            // Arrange
            var request = new MultiVersionAllocationRequest
            {
                FeatureName = "sldworks",
                RequiredLicenses = 5
            };

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = @"Users of sldworks: (Total of 10 licenses issued; Total of 8 licenses in use)",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(2)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // Act
            var result = await _query.TryAllocateAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Insufficient licenses", result.ErrorMessage);
            Assert.Contains("2 available, 5 required", result.ErrorMessage);
        }

        [Fact]
        public async Task TryAllocateAsync_SufficientLicenses_ReturnsSuccess()
        {
            // Arrange
            var request = new MultiVersionAllocationRequest
            {
                FeatureName = "sldworks",
                RequiredLicenses = 2
            };

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = @"Users of sldworks: (Total of 10 licenses issued; Total of 3 licenses in use)",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(2)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // Act
            var result = await _query.TryAllocateAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.AllocatedLicenses);
            Assert.NotNull(result.AllocationId);
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task ReleaseLicensesAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _query.ReleaseLicensesAsync(null));

            Assert.Equal("request", exception.ParamName);
        }

        [Fact]
        public async Task ReleaseLicensesAsync_InvalidRequest_ReturnsFailure()
        {
            // Arrange
            var request = new MultiVersionReleaseRequest
            {
                Version = "2025",
                FeatureName = "", // Invalid - empty feature name
                User = "testuser",
                LicenseCount = 1
            };

            // Act
            var result = await _query.ReleaseLicensesAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Feature name is required", result.Errors);
        }

        [Fact]
        public async Task ReleaseLicensesAsync_ValidRequest_ReturnsSuccess()
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
            var result = await _query.ReleaseLicensesAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Single(result.ReleasedLicenses);
            Assert.Equal("sldworks", result.ReleasedLicenses[0].FeatureName);
            Assert.Equal("testuser", result.ReleasedLicenses[0].User);
            Assert.Equal(1, result.ReleasedLicenses[0].ReleasedCount);
        }

        [Fact]
        public async Task GetHealthStatusAsync_UnavailableVersion_ReturnsUnhealthy()
        {
            // Arrange
            _versionInfo.IsAvailable = false;
            CreateQuery();

            // Act
            var result = await _query.GetHealthStatusAsync();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("Version not available", result.Status);
        }

        [Fact]
        public async Task GetHealthStatusAsync_MissingExecutable_ReturnsUnhealthy()
        {
            // Arrange
            _versionInfo.ExecutablePath = @"C:\nonexistent\SLDWORKS.exe";
            CreateQuery();

            // Act
            var result = await _query.GetHealthStatusAsync();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("Executable not found", result.Status);
        }

        [Fact]
        public async Task GetHealthStatusAsync_MissingLmutil_ReturnsUnhealthy()
        {
            // Arrange
            _versionInfo.LmutilPath = @"";
            CreateQuery();

            // Act
            var result = await _query.GetHealthStatusAsync();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("License manager not accessible", result.Status);
        }

        [Fact]
        public async Task GetHealthStatusAsync_LmutilTestFails_ReturnsUnhealthy()
        {
            // Arrange
            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 1,
                StandardError = "lmutil: invalid command",
                ExecutionTime = TimeSpan.FromSeconds(1)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    "-help",
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // Act
            var result = await _query.GetHealthStatusAsync();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("License manager test failed", result.Status);
        }

        [Fact]
        public async Task GetHealthStatusAsync_ServerNotConnected_ReturnsUnhealthy()
        {
            // Arrange
            var testResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "lmutil help",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(1)
            };

            var connectivityResult = new ProcessExecutionResult
            {
                ExitCode = 1,
                StandardError = "Cannot connect to license server",
                ExecutionTime = TimeSpan.FromSeconds(5)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    "-help",
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(testResult);

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    "lmstat -c",
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(connectivityResult);

            // Act
            var result = await _query.GetHealthStatusAsync();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Contains("License server not reachable", result.Status);
        }

        [Fact]
        public async Task GetHealthStatusAsync_AllChecksPass_ReturnsHealthy()
        {
            // Arrange
            var testResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "lmutil help",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(1)
            };

            var connectivityResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "License server status: UP",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(2)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    "-help",
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(testResult);

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    "lmstat -c",
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(connectivityResult);

            // Act
            var result = await _query.GetHealthStatusAsync();

            // Assert
            Assert.True(result.IsHealthy);
            Assert.Equal("Healthy", result.Status);
            Assert.NotNull(result.Metrics);
            Assert.True(result.Metrics.ContainsKey("ResponseTime"));
            Assert.True(result.Metrics.ContainsKey("ServerStatus"));
        }

        [Fact]
        public async Task UpdateVersionInfo_ValidUpdate_UpdatesProperties()
        {
            // Arrange
            var newVersionInfo = new SolidWorksVersionInfo("2025", @"C:\Updated\Path")
            {
                ExecutablePath = @"C:\Updated\Path\SLDWORKS.exe",
                LmutilPath = @"C:\Updated\Path\lmutil.exe",
                FullVersion = "SolidWorks 2025 SP2",
                ServicePack = "SP2",
                BuildNumber = "2.0.0.0",
                Health = VersionHealth.Healthy,
                LastDetected = DateTime.UtcNow
            };

            // Act
            _query.UpdateVersionInfo(newVersionInfo);

            // Assert
            Assert.Equal(@"C:\Updated\Path", _versionInfo.InstallationPath);
            Assert.Equal(@"C:\Updated\Path\SLDWORKS.exe", _versionInfo.ExecutablePath);
            Assert.Equal(@"C:\Updated\Path\lmutil.exe", _versionInfo.LmutilPath);
            Assert.Equal("SolidWorks 2025 SP2", _versionInfo.FullVersion);
            Assert.Equal("SP2", _versionInfo.ServicePack);
            Assert.Equal("2.0.0.0", _versionInfo.BuildNumber);
            Assert.Equal(VersionHealth.Healthy, _versionInfo.Health);
        }

        [Fact]
        public async Task UpdateVersionInfo_VersionMismatch_ThrowsArgumentException()
        {
            // Arrange
            var newVersionInfo = new SolidWorksVersionInfo("2024", @"C:\Different\Path");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => Task.Run(() =>
            {
                _query.UpdateVersionInfo(newVersionInfo);
            }));

            Assert.Equal("Version mismatch during update", exception.ParamName);
        }

        [Fact]
        public async Task ParseLmutilOutput_EmptyOutput_ReturnsError()
        {
            // Arrange
            var emptyOutput = "";
            var errorOutput = "";

            // Act
            var result = await Task.Run(() => _query.TestParseLmutilOutput(emptyOutput, errorOutput));

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No output from lmutil command", result.Errors);
        }

        [Fact]
        public async Task ParseLmutilOutput_ValidOutput_ReturnsParsedData()
        {
            // Arrange
            var output = @"Users of sldworks: (Total of 10 licenses issued; Total of 3 licenses in use)
sldworks user1 host1 (display1) (v1.0)
Users of sldworks_drawing: (Total of 5 licenses issued; Total of 1 licenses in use)
sldworks_drawing user2 host2 (display2) (v1.0)
user3 host3 (display3) (borrowed: 2024/01/15)";

            var errorOutput = "";

            // Act
            var result = await Task.Run(() => _query.TestParseLmutilOutput(output, errorOutput));

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.Licenses.Count);
            Assert.Equal(15, result.TotalLicenses);
            Assert.Equal(4, result.UsedLicenses);
            Assert.Equal(11, result.AvailableLicenses);
            Assert.Equal(3, result.Users.Count);
            Assert.Single(result.BorrowedLicenses);
            Assert.Equal("user3", result.BorrowedLicenses[0].Username);
        }

        [Fact]
        public async Task Cache_ExpiresAfterTimeout_ReturnsFreshResult()
        {
            // Arrange
            _configuration.CacheExpiration = TimeSpan.FromMilliseconds(100);
            CreateQuery();

            var queryOptions = new LicenseQueryOptions();

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = @"Users of sldworks: (Total of 10 licenses issued; Total of 3 licenses in use)",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(2)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // First call should execute lmutil
            var result1 = await _query.QueryLicensesAsync(queryOptions);
            Assert.True(result1.Success);
            Assert.False(result1.Cached);

            // Wait for cache to expire
            await Task.Delay(200);

            // Second call should execute lmutil again
            var result2 = await _query.QueryLicensesAsync(queryOptions);
            Assert.True(result2.Success);
            Assert.False(result2.Cached);

            // Verify lmutil was called twice
            _processExecutorMock.Verify(
                x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task ConcurrentQueries_RespectsConcurrencyLimits()
        {
            // Arrange
            var queryOptions = new LicenseQueryOptions();

            var executionResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = @"Users of sldworks: (Total of 10 licenses issued; Total of 3 licenses in use)",
                StandardError = "",
                ExecutionTime = TimeSpan.FromSeconds(2)
            };

            _processExecutorMock
                .Setup(x => x.ExecuteAsync(
                    _versionInfo.LmutilPath,
                    It.IsAny<string>(),
                    _configuration.QueryTimeout,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);

            // Create concurrent tasks
            var tasks = new List<Task<VersionSpecificQueryResult>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_query.QueryLicensesAsync(queryOptions));
            }

            // Act
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(5, results.Length);
            Assert.All(results, r => Assert.True(r.Success));
        }

        public void Dispose()
        {
            _query?.Dispose();
        }
    }

    /// <summary>
    /// Extension methods for testing
    /// </summary>
    public static class VersionSpecificLicenseQueryTestExtensions
    {
        public static LicenseQueryResult TestParseLmutilOutput(
            this VersionSpecificLicenseQuery query,
            string output,
            string errorOutput)
        {
            // Use reflection to access private method
            var methodInfo = typeof(VersionSpecificLicenseQuery).GetMethod(
                "ParseLmutilOutput",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (LicenseQueryResult)methodInfo.Invoke(query, new object[] { output, errorOutput });
        }
    }
}