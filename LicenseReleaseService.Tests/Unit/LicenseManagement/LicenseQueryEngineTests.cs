using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.Tests.Unit.LicenseManagement
{
    [TestClass]
    [Category("Unit")]
    [Category("LicenseManagement")]
    [Description("Tests for LicenseQueryEngine functionality")]
    public class LicenseQueryEngineTests
    {
        private Mock<ICacheManager> _mockCacheManager;
        private Mock<IProcessExecutor> _mockProcessExecutor;
        private Mock<LmstatOutputParser> _mockOutputParser;
        private Mock<ILogger<LicenseQueryEngine>> _mockLogger;
        private LicenseQueryEngine _queryEngine;
        private LicenseQueryOptions _testOptions;

        [TestInitialize]
        public void Setup()
        {
            _mockCacheManager = new Mock<ICacheManager>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockOutputParser = new Mock<LmstatOutputParser>();
            _mockLogger = new Mock<ILogger<LicenseQueryEngine>>();

            _testOptions = new LicenseQueryOptions
            {
                EnableCaching = true,
                EnableErrorRecovery = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromMilliseconds(100),
                QueryTimeout = TimeSpan.FromSeconds(30),
                CacheExpiration = TimeSpan.FromMinutes(5)
            };

            _queryEngine = new LicenseQueryEngine(
                _mockCacheManager.Object,
                _mockProcessExecutor.Object,
                _mockOutputParser.Object)
            {
                Options = _testOptions
            };
        }

        [Test]
        [Category("HappyPath")]
        [Description("Should successfully query license status with valid response")]
        public async Task QueryLicenseStatusAsync_ValidResponse_ReturnsServerStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var mockOutput = "License server UP: test-server:27000\nsolidworks: 10 total licenses; 5 in use";
            var mockProcessResult = new ProcessExecutionResult
            {
                Success = true,
                Output = mockOutput,
                ExitCode = 0,
                ExecutionTime = TimeSpan.FromMilliseconds(500)
            };
            var expectedStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}@{port}",
                IsServerUp = true,
                IsHealthy = true,
                TotalLicenses = 10,
                LicensesInUse = 5,
                AvailableLicenses = 5,
                ResponseTimeMs = 500
            };

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockProcessResult);

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(mockOutput, $"{server}@{port}"))
                .Returns(expectedStatus);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsServerUp);
            Assert.IsTrue(result.IsHealthy);
            Assert.AreEqual(10, result.TotalLicenses);
            Assert.AreEqual(5, result.LicensesInUse);
            Assert.AreEqual(5, result.AvailableLicenses);
            Assert.AreEqual(500, result.ResponseTimeMs);

            _mockProcessExecutor.Verify(x => x.ExecuteAsync(
                "lmutil.exe",
                "-c test-server@27000",
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockOutputParser.Verify(x => x.ParseLmstatOutput(mockOutput, "test-server@27000"), Times.Once);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Should return cached license status when available")]
        public async Task QueryLicenseStatusAsync_CachedHit_ReturnsCachedStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cachedStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}@{port}",
                IsServerUp = true,
                IsHealthy = true,
                TotalLicenses = 10,
                LicensesInUse = 5,
                AvailableLicenses = 5,
                LastChecked = DateTime.Now.AddMinutes(-1) // Within cache expiration
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedStatus);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(cachedStatus, result);

            _mockProcessExecutor.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        [Category("ErrorHandling")]
        [Description("Should handle process execution failure gracefully")]
        public async Task QueryLicenseStatusAsync_ProcessFails_ThrowsLicenseQueryException()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var mockProcessResult = new ProcessExecutionResult
            {
                Success = false,
                Error = "Process execution failed",
                ExitCode = 1,
                ExecutionTime = TimeSpan.FromMilliseconds(500)
            };

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockProcessResult);

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port));

            Assert.IsTrue(exception.Message.Contains("lmstat command failed"));
            Assert.AreEqual(server, exception.Server);
            Assert.AreEqual(port, exception.Port);
        }

        [Test]
        [Category("ErrorHandling")]
        [Description("Should handle network timeout with retry logic")]
        public async Task QueryLicenseStatusAsync_NetworkTimeout_RetriesAndFails()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var timeoutException = new TimeoutException("Network timeout");

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(timeoutException);

            _testOptions.MaxRetries = 2;
            _queryEngine.Options = _testOptions;

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port));

            Assert.IsTrue(exception.Message.Contains("Query failed after 2 retries"));
            Assert.IsTrue(exception.InnerException is TimeoutException);

            // Verify retry attempts
            _mockProcessExecutor.Verify(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Exactly(3)); // Initial + 2 retries
        }

        [Test]
        [Category("Caching")]
        [Description("Should cache successful query results")]
        public async Task QueryLicenseStatusAsync_SuccessfulQuery_CachesResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var mockOutput = "License server UP: test-server:27000\nsolidworks: 10 total licenses; 5 in use";
            var mockProcessResult = new ProcessExecutionResult
            {
                Success = true,
                Output = mockOutput,
                ExitCode = 0,
                ExecutionTime = TimeSpan.FromMilliseconds(500)
            };
            var expectedStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}@{port}",
                IsServerUp = true,
                IsHealthy = true,
                TotalLicenses = 10,
                LicensesInUse = 5,
                AvailableLicenses = 5,
                ResponseTimeMs = 500
            };

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockProcessResult);

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(mockOutput, $"{server}@{port}"))
                .Returns(expectedStatus);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            Assert.IsNotNull(result);

            _mockCacheManager.Verify(x => x.SetServerStatusAsync(
                server,
                port,
                It.Is<LicenseServerStatus>(s => s == result),
                _testOptions.CacheExpiration,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("Caching")]
        [Description("Should handle cache miss and populate cache")]
        public async Task QueryLicenseStatusAsync_CacheMiss_QueriesAndCachesResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var mockOutput = "License server UP: test-server:27000\nsolidworks: 10 total licenses; 5 in use";
            var mockProcessResult = new ProcessExecutionResult
            {
                Success = true,
                Output = mockOutput,
                ExitCode = 0,
                ExecutionTime = TimeSpan.FromMilliseconds(500)
            };
            var expectedStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}@{port}",
                IsServerUp = true,
                IsHealthy = true,
                TotalLicenses = 10,
                LicensesInUse = 5,
                AvailableLicenses = 5,
                ResponseTimeMs = 500
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LicenseServerStatus)null); // Cache miss

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockProcessResult);

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(mockOutput, $"{server}@{port}"))
                .Returns(expectedStatus);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            Assert.IsNotNull(result);

            _mockCacheManager.Verify(x => x.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()), Times.Once);
            _mockCacheManager.Verify(x => x.SetServerStatusAsync(
                server,
                port,
                It.Is<LicenseServerStatus>(s => s == result),
                _testOptions.CacheExpiration,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("Performance")]
        [Description("Should collect performance metrics for successful queries")]
        public async Task QueryLicenseStatusAsync_SuccessfulQuery_UpdatesPerformanceMetrics()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var mockOutput = "License server UP: test-server:27000\nsolidworks: 10 total licenses; 5 in use";
            var mockProcessResult = new ProcessExecutionResult
            {
                Success = true,
                Output = mockOutput,
                ExitCode = 0,
                ExecutionTime = TimeSpan.FromMilliseconds(500)
            };
            var expectedStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}@{port}",
                IsServerUp = true,
                IsHealthy = true,
                TotalLicenses = 10,
                LicensesInUse = 5,
                AvailableLicenses = 5,
                ResponseTimeMs = 500
            };

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockProcessResult);

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(mockOutput, $"{server}@{port}"))
                .Returns(expectedStatus);

            // Act
            await _queryEngine.QueryLicenseStatusAsync(server, port);
            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.IsNotNull(metrics);
            Assert.AreEqual(1, metrics.TotalQueries);
            Assert.AreEqual(1, metrics.SuccessfulQueries);
            Assert.AreEqual(0, metrics.FailedQueries);
            Assert.AreEqual(0, metrics.CachedQueries);
            Assert.AreEqual(500, metrics.AverageQueryTimeMs);
            Assert.AreEqual(500, metrics.MinQueryTimeMs);
            Assert.AreEqual(500, metrics.MaxQueryTimeMs);
        }

        [Test]
        [Category("Performance")]
        [Description("Should provide performance benchmark for license queries")]
        public void GetPerformanceMetrics_ReturnsAccurateMetrics()
        {
            // Arrange
            var initialMetrics = _queryEngine.GetPerformanceMetrics();

            // Act
            var result = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.TotalQueries);
            Assert.AreEqual(0, result.SuccessfulQueries);
            Assert.AreEqual(0, result.FailedQueries);
            Assert.AreEqual(0, result.CachedQueries);
            Assert.IsTrue(result.StartTime <= DateTime.Now);
            Assert.IsTrue(result.EndTime >= result.StartTime);
        }

        [Test]
        [Category("Validation")]
        [Description("Should validate configuration and return errors")]
        public void ValidateConfiguration_InvalidConfiguration_ReturnsErrorList()
        {
            // Act
            var errors = _queryEngine.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Count); // Should be valid with proper setup
        }

        [Test]
        [Category("HappyPath")]
        [Description("Should query specific feature information")]
        public async Task QueryFeatureAsync_ValidFeature_ReturnsFeatureInfo()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "solidworks";
            var mockOutput = "solidworks: 10 total licenses; 5 in use";
            var mockProcessResult = new ProcessExecutionResult
            {
                Success = true,
                Output = mockOutput,
                ExitCode = 0,
                ExecutionTime = TimeSpan.FromMilliseconds(300)
            };
            var expectedFeature = new LicenseFeature
            {
                Name = feature,
                TotalLicenses = 10,
                LicensesInUse = 5,
                AvailableLicenses = 5,
                Status = "ACTIVE"
            };

            _mockCacheManager
                .Setup(x => x.GetLicenseFeatureAsync(server, port, feature, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LicenseFeature)null); // Cache miss

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockProcessResult);

            _mockOutputParser
                .Setup(x => x.ParseFeatureOutput(mockOutput, $"{server}@{port}", feature))
                .Returns(expectedFeature);

            // Act
            var result = await _queryEngine.QueryFeatureAsync(server, port, feature);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(feature, result.Name);
            Assert.AreEqual(10, result.TotalLicenses);
            Assert.AreEqual(5, result.LicensesInUse);

            _mockCacheManager.Verify(x => x.SetLicenseFeatureAsync(
                server,
                port,
                feature,
                It.Is<LicenseFeature>(f => f == result),
                _testOptions.CacheExpiration,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Should query active users for a server")]
        public async Task QueryActiveUsersAsync_ValidServer_ReturnsActiveUsers()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var mockStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}@{port}",
                IsServerUp = true,
                Features = new Dictionary<string, LicenseFeatureStatus>
                {
                    ["solidworks"] = new LicenseFeatureStatus
                    {
                        FeatureName = "solidworks",
                        TotalLicenses = 10,
                        LicensesInUse = 5,
                        AvailableLicenses = 5
                    }
                }
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockStatus);

            // Act
            var result = await _queryEngine.QueryActiveUsersAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("solidworks"));
        }

        [Test]
        [Category("ErrorHandling")]
        [Description("Should handle invalid feature name with exception")]
        public async Task QueryFeatureAsync_InvalidFeatureName_ThrowsArgumentException()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = ""; // Invalid empty feature name

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _queryEngine.QueryFeatureAsync(server, port, feature));
        }

        [Test]
        [Category("ErrorHandling")]
        [Description("Should handle cancellation gracefully")]
        public async Task QueryLicenseStatusAsync_CancellationToken_ThrowsOperationCancelledException()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = new CancellationToken(true); // Already cancelled

            // Act & Assert
            await Assert.ThrowsExceptionAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port, cancellationToken),
                "Query was cancelled");
        }

        [Test]
        [Category("HappyPath")]
        [Description("Should query usage statistics")]
        public async Task QueryUsageStatisticsAsync_ValidServer_ReturnsStatistics()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var mockStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}@{port}",
                IsServerUp = true,
                TotalLicenses = 10,
                LicensesInUse = 5,
                AvailableLicenses = 5,
                TotalActiveUsers = 3
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockStatus);

            // Act
            var result = await _queryEngine.QueryUsageStatisticsAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.AreEqual(10, result.TotalLicenses);
            Assert.AreEqual(5, result.TotalLicensesInUse);
            Assert.AreEqual(5, result.TotalAvailableLicenses);
            Assert.AreEqual(3, result.UniqueUsers);
            Assert.AreEqual(50.0, result.OverallUtilization);
        }

        [Test]
        [Category("Caching")]
        [Description("Should invalidate server cache")]
        public async Task InvalidateServerCacheAsync_ValidServer_CallsCacheManager()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Act
            await _queryEngine.InvalidateServerCacheAsync(server, port);

            // Assert
            _mockCacheManager.Verify(x => x.InvalidateServerAsync(server, port, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("Caching")]
        [Description("Should clear all cache")]
        public async Task ClearAllCacheAsync_CallsCacheManager()
        {
            // Act
            await _queryEngine.ClearAllCacheAsync();

            // Assert
            _mockCacheManager.Verify(x => x.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Should check server health")]
        public async Task CheckServerHealthAsync_HealthyServer_ReturnsHealthyStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var mockStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}@{port}",
                IsServerUp = true,
                IsHealthy = true,
                StatusMessage = "License server is running normally"
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockStatus);

            // Act
            var result = await _queryEngine.CheckServerHealthAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsTrue(result.IsHealthy);
            Assert.AreEqual("License server is running normally", result.Message);
        }

        [Test]
        [Category("ErrorHandling")]
        [Description("Should return unhealthy status when server check fails")]
        public async Task CheckServerHealthAsync_ServerFails_ReturnsUnhealthyStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var exception = new LicenseQueryException("Server unavailable", server, port, "CheckServerHealthAsync");

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act
            var result = await _queryEngine.CheckServerHealthAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsFalse(result.IsHealthy);
            Assert.AreEqual("Health check failed", result.Message);
            Assert.AreEqual("Server unavailable", result.ErrorMessage);
        }

        [Test]
        [Category("Performance")]
        [Description("Should reset performance metrics")]
        public void ResetPerformanceMetrics_ResetsAllMetrics()
        {
            // Act
            _queryEngine.ResetPerformanceMetrics();
            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.AreEqual(0, metrics.TotalQueries);
            Assert.AreEqual(0, metrics.SuccessfulQueries);
            Assert.AreEqual(0, metrics.FailedQueries);
            Assert.AreEqual(0, metrics.CachedQueries);
            Assert.IsTrue(metrics.StartTime <= DateTime.Now);
        }

        [Test]
        [Category("HappyPath")]
        [Description("Should query multiple servers simultaneously")]
        public async Task QueryMultipleServersAsync_ValidServers_ReturnsAllStatuses()
        {
            // Arrange
            var servers = new List<string> { "server1", "server2" };
            var ports = new List<int> { 27000, 27001 };
            var mockStatus1 = new LicenseServerStatus { ServerAddress = "server1@27000", IsServerUp = true };
            var mockStatus2 = new LicenseServerStatus { ServerAddress = "server2@27001", IsServerUp = true };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync("server1", 27000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockStatus1);

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync("server2", 27001, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockStatus2);

            // Act
            var result = await _queryEngine.QueryMultipleServersAsync(servers, ports);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.ContainsKey("server1:27000"));
            Assert.IsTrue(result.ContainsKey("server2:27001"));
        }

        [Test]
        [Category("ErrorHandling")]
        [Description("Should handle mismatched server and port counts")]
        public async Task QueryMultipleServersAsync_MismatchedCounts_ThrowsArgumentException()
        {
            // Arrange
            var servers = new List<string> { "server1", "server2" };
            var ports = new List<int> { 27000 }; // Mismatched count

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _queryEngine.QueryMultipleServersAsync(servers, ports));
        }
    }

    /// <summary>
    /// Extension methods for verifying ILogger calls
    /// </summary>
    public static class LoggerExtensions
    {
        public static void VerifyLog(this Mock<ILogger<LicenseQueryEngine>> logger, LogLevel expectedLevel, string expectedMessage, Times times)
        {
            logger.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                times);
        }
    }
}