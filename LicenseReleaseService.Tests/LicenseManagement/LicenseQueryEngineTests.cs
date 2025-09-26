using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.LicenseManagement.Parsing;
using LicenseReleaseService.Process;
using Moq;
using Xunit;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for the LicenseQueryEngine class
    /// </summary>
    public class LicenseQueryEngineTests
    {
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<LmstatOutputParser> _mockOutputParser;
        private readonly LicenseQueryEngine _queryEngine;

        public LicenseQueryEngineTests()
        {
            _mockCacheManager = new Mock<ICacheManager>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockOutputParser = new Mock<LmstatOutputParser>();

            _queryEngine = new LicenseQueryEngine(
                _mockCacheManager.Object,
                _mockProcessExecutor.Object,
                _mockOutputParser.Object);
        }

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitialize()
        {
            // Arrange
            var cacheManager = new Mock<ICacheManager>().Object;
            var processExecutor = new Mock<IProcessExecutor>().Object;
            var outputParser = new Mock<LmstatOutputParser>().Object;

            // Act
            var engine = new LicenseQueryEngine(cacheManager, processExecutor, outputParser);

            // Assert
            Assert.NotNull(engine);
            Assert.NotNull(engine.Options);
            Assert.Same(cacheManager, engine.CacheManager);
            Assert.Same(processExecutor, engine.ProcessExecutor);
        }

        [Fact]
        public void Constructor_WithNullCacheManager_ShouldThrowArgumentNullException()
        {
            // Arrange
            var processExecutor = new Mock<IProcessExecutor>().Object;
            var outputParser = new Mock<LmstatOutputParser>().Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseQueryEngine(null, processExecutor, outputParser));
        }

        [Fact]
        public void Constructor_WithNullProcessExecutor_ShouldThrowArgumentNullException()
        {
            // Arrange
            var cacheManager = new Mock<ICacheManager>().Object;
            var outputParser = new Mock<LmstatOutputParser>().Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseQueryEngine(cacheManager, null, outputParser));
        }

        [Fact]
        public void Constructor_WithNullOutputParser_ShouldThrowArgumentNullException()
        {
            // Arrange
            var cacheManager = new Mock<ICacheManager>().Object;
            var processExecutor = new Mock<IProcessExecutor>().Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseQueryEngine(cacheManager, processExecutor, null));
        }

        [Fact]
        public async Task QueryLicenseStatusAsync_WithValidServer_ShouldReturnStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var processResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = "lmstat output",
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            };

            var expectedStatus = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                TotalLicenses = 100,
                LicensesInUse = 50,
                AvailableLicenses = 50
            };

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(processResult);

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(expectedStatus);

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.True(result.IsServerUp);
            Assert.True(result.IsHealthy);

            _mockProcessExecutor.Verify(
                x => x.ExecuteAsync("lmutil.exe", $"-c {server}@{port}", It.IsAny<TimeSpan>(), cancellationToken),
                Times.Once);

            _mockOutputParser.Verify(
                x => x.ParseLmstatOutput("lmstat output", $"{server}@{port}"),
                Times.Once);
        }

        [Fact]
        public async Task QueryLicenseStatusAsync_WithCachedData_ShouldReturnCachedStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var cachedStatus = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                LastChecked = DateTime.Now.AddMinutes(-1) // Recent cache entry
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync(cachedStatus);

            _queryEngine.Options.EnableCaching = true;
            _queryEngine.Options.CacheExpiration = TimeSpan.FromMinutes(5);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Same(cachedStatus, result);

            // Should not execute lmstat or parse output when cache is valid
            _mockProcessExecutor.Verify(
                x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _mockOutputParser.Verify(
                x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task QueryLicenseStatusAsync_WithExpiredCache_ShouldExecuteLmstat()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var expiredStatus = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                LastChecked = DateTime.Now.AddHours(-1) // Expired cache entry
            };

            var processResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = "lmstat output",
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            };

            var expectedStatus = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync(expiredStatus);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(processResult);

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(expectedStatus);

            _queryEngine.Options.EnableCaching = true;
            _queryEngine.Options.CacheExpiration = TimeSpan.FromMinutes(5);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.NotSame(expiredStatus, result);

            // Should execute lmstat when cache is expired
            _mockProcessExecutor.Verify(
                x => x.ExecuteAsync("lmutil.exe", $"-c {server}@{port}", It.IsAny<TimeSpan>(), cancellationToken),
                Times.Once);
        }

        [Fact]
        public async Task QueryLicenseStatusAsync_WithProcessFailure_ShouldThrowException()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var processResult = new ProcessExecutionResult
            {
                ExitCode = 1,
                Error = "lmstat failed",
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            };

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(processResult);

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port, cancellationToken));

            Assert.Equal("lmstat", exception.QueryType);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(LicenseQueryErrorCode.ProcessExecutionError, exception.ErrorCode);
            Assert.Contains("exit code 1", exception.Message);
            Assert.False(exception.IsTransient);
        }

        [Fact]
        public async Task QueryFeaturesAsync_WithValidServer_ShouldReturnFeatures()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var expectedFeatures = new Dictionary<string, LicenseFeature>
            {
                ["solidworks"] = new LicenseFeature
                {
                    Name = "solidworks",
                    TotalLicenses = 100,
                    UsedLicenses = 50,
                    AvailableLicenses = 50
                },
                ["simulation"] = new LicenseFeature
                {
                    Name = "simulation",
                    TotalLicenses = 50,
                    UsedLicenses = 25,
                    AvailableLicenses = 25
                }
            };

            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                FeatureDetails = expectedFeatures
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "output" });

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(status);

            // Act
            var result = await _queryEngine.QueryFeaturesAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("solidworks"));
            Assert.True(result.ContainsKey("simulation"));

            var solidworksFeature = result["solidworks"];
            Assert.Equal("solidworks", solidworksFeature.Name);
            Assert.Equal(100, solidworksFeature.TotalLicenses);
            Assert.Equal(50, solidworksFeature.UsedLicenses);
            Assert.Equal(50, solidworksFeature.AvailableLicenses);
        }

        [Fact]
        public async Task QueryActiveUsersAsync_WithActiveUsers_ShouldReturnActiveUsers()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var features = new Dictionary<string, LicenseFeature>
            {
                ["solidworks"] = new LicenseFeature
                {
                    Name = "solidworks",
                    TotalLicenses = 100,
                    UsedLicenses = 2,
                    AvailableLicenses = 98
                }
            };

            // Add users to the feature
            var activeUser1 = LicenseInfo.CreateActive("user1", "solidworks", "client1");
            var activeUser2 = LicenseInfo.CreateActive("user2", "solidworks", "client2");
            var idleUser = LicenseInfo.CreateIdle("user3", "solidworks", "Idle license", "client3");

            features["solidworks"].AddActiveUser(activeUser1);
            features["solidworks"].AddActiveUser(activeUser2);
            features["solidworks"].AddIdleUser(idleUser);

            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                FeatureDetails = features
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "output" });

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(status);

            // Act
            var result = await _queryEngine.QueryActiveUsersAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.True(result.ContainsKey("solidworks"));

            var solidworksUsers = result["solidworks"];
            Assert.Equal(2, solidworksUsers.Count);
            Assert.All(solidworksUsers, user => Assert.Equal(LicenseStatus.Active, user.Status));
        }

        [Fact]
        public async Task QueryFeatureAsync_WithValidFeature_ShouldReturnFeature()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "solidworks";
            var cancellationToken = CancellationToken.None;

            var expectedFeature = new LicenseFeature
            {
                Name = feature,
                TotalLicenses = 100,
                UsedLicenses = 50,
                AvailableLicenses = 50
            };

            _mockCacheManager
                .Setup(x => x.GetLicenseFeatureAsync(server, port, feature, cancellationToken))
                .ReturnsAsync((LicenseFeature)null);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync("lmutil.exe", $"-f {feature} -c {server}@{port}", It.IsAny<TimeSpan>(), cancellationToken))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "feature output" });

            _mockOutputParser
                .Setup(x => x.ParseFeatureOutput("feature output", $"{server}@{port}", feature))
                .Returns(expectedFeature);

            // Act
            var result = await _queryEngine.QueryFeatureAsync(server, port, feature, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(feature, result.Name);
            Assert.Equal(100, result.TotalLicenses);
            Assert.Equal(50, result.UsedLicenses);
            Assert.Equal(50, result.AvailableLicenses);

            _mockProcessExecutor.Verify(
                x => x.ExecuteAsync("lmutil.exe", $"-f {feature} -c {server}@{port}", It.IsAny<TimeSpan>(), cancellationToken),
                Times.Once);

            _mockOutputParser.Verify(
                x => x.ParseFeatureOutput("feature output", $"{server}@{port}", feature),
                Times.Once);
        }

        [Fact]
        public async Task QueryFeatureAsync_WithNullFeatureName_ShouldThrowArgumentException()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _queryEngine.QueryFeatureAsync(server, port, null, cancellationToken));

            await Assert.ThrowsAsync<ArgumentException>(
                () => _queryEngine.QueryFeatureAsync(server, port, "", cancellationToken));

            await Assert.ThrowsAsync<ArgumentException>(
                () => _queryEngine.QueryFeatureAsync(server, port, "   ", cancellationToken));
        }

        [Fact]
        public async Task QueryFeatureAsync_WithCachedFeature_ShouldReturnCachedFeature()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "solidworks";
            var cancellationToken = CancellationToken.None;

            var cachedFeature = new LicenseFeature
            {
                Name = feature,
                TotalLicenses = 100,
                UsedLicenses = 50,
                AvailableLicenses = 50,
                LastUpdated = DateTime.Now.AddMinutes(-1) // Recent cache entry
            };

            _mockCacheManager
                .Setup(x => x.GetLicenseFeatureAsync(server, port, feature, cancellationToken))
                .ReturnsAsync(cachedFeature);

            _queryEngine.Options.EnableCaching = true;
            _queryEngine.Options.CacheExpiration = TimeSpan.FromMinutes(5);

            // Act
            var result = await _queryEngine.QueryFeatureAsync(server, port, feature, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Same(cachedFeature, result);

            // Should not execute lmstat when cache is valid
            _mockProcessExecutor.Verify(
                x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task QueryLicenseStatusVerboseAsync_WithValidServer_ShouldReturnVerboseStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var processResult = new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = "verbose lmstat output",
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            };

            var expectedStatus = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                ServerMessages = new List<string> { "Verbose message 1", "Verbose message 2" }
            };

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync("lmutil.exe", $"-v -c {server}@{port}", It.IsAny<TimeSpan>(), cancellationToken))
                .ReturnsAsync(processResult);

            _mockOutputParser
                .Setup(x => x.ParseLmstatVerboseOutput("verbose lmstat output", $"{server}@{port}"))
                .Returns(expectedStatus);

            // Act
            var result = await _queryEngine.QueryLicenseStatusVerboseAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.Equal(2, result.ServerMessages.Count);

            _mockProcessExecutor.Verify(
                x => x.ExecuteAsync("lmutil.exe", $"-v -c {server}@{port}", It.IsAny<TimeSpan>(), cancellationToken),
                Times.Once);

            _mockOutputParser.Verify(
                x => x.ParseLmstatVerboseOutput("verbose lmstat output", $"{server}@{port}"),
                Times.Once);
        }

        [Fact]
        public async Task QueryUsersAsync_WithValidUsers_ShouldReturnUserLicenses()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var userNames = new[] { "user1", "user2" };
            var cancellationToken = CancellationToken.None;

            var features = new Dictionary<string, LicenseFeature>
            {
                ["solidworks"] = new LicenseFeature { Name = "solidworks" },
                ["simulation"] = new LicenseFeature { Name = "simulation" }
            };

            // Add users
            var user1License1 = LicenseInfo.CreateActive("user1", "solidworks", "client1");
            var user1License2 = LicenseInfo.CreateActive("user1", "simulation", "client1");
            var user2License = LicenseInfo.CreateActive("user2", "solidworks", "client2");
            var otherUserLicense = LicenseInfo.CreateActive("user3", "solidworks", "client3");

            features["solidworks"].AddActiveUser(user1License1);
            features["solidworks"].AddActiveUser(user2License);
            features["solidworks"].AddActiveUser(otherUserLicense);
            features["simulation"].AddActiveUser(user1License2);

            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                FeatureDetails = features
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "output" });

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(status);

            // Act
            var result = await _queryEngine.QueryUsersAsync(server, port, userNames, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("user1"));
            Assert.True(result.ContainsKey("user2"));

            var user1Licenses = result["user1"];
            Assert.Equal(2, user1Licenses.Count);
            Assert.Contains(user1Licenses, l => l.Feature == "solidworks");
            Assert.Contains(user1Licenses, l => l.Feature == "simulation");

            var user2Licenses = result["user2"];
            Assert.Single(user2Licenses);
            Assert.Equal("solidworks", user2Licenses[0].Feature);
        }

        [Fact]
        public async Task QueryBorrowedLicensesAsync_WithBorrowedLicenses_ShouldReturnBorrowedLicenses()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var features = new Dictionary<string, LicenseFeature>
            {
                ["solidworks"] = new LicenseFeature { Name = "solidworks" }
            };

            var borrowedLicense = LicenseInfo.CreateBorrowed("user1", "solidworks", DateTime.Now.AddHours(-1), "client1");
            var activeLicense = LicenseInfo.CreateActive("user2", "solidworks", "client2");

            features["solidworks"].AddBorrowedUser(borrowedLicense);
            features["solidworks"].AddActiveUser(activeLicense);

            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                FeatureDetails = features
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "output" });

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(status);

            // Act
            var result = await _queryEngine.QueryBorrowedLicensesAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(LicenseStatus.Borrowed, result[0].Status);
            Assert.Equal("user1", result[0].UserHost);
            Assert.Equal("solidworks", result[0].Feature);
        }

        [Fact]
        public async Task QueryIdleLicensesAsync_WithIdleLicenses_ShouldReturnIdleLicenses()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var features = new Dictionary<string, LicenseFeature>
            {
                ["solidworks"] = new LicenseFeature { Name = "solidworks" }
            };

            var idleLicense = LicenseInfo.CreateIdle("user1", "solidworks", "Idle license", "client1");
            var activeLicense = LicenseInfo.CreateActive("user2", "solidworks", "client2");

            features["solidworks"].AddIdleUser(idleLicense);
            features["solidworks"].AddActiveUser(activeLicense);

            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                FeatureDetails = features
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "output" });

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(status);

            // Act
            var result = await _queryEngine.QueryIdleLicensesAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.True(result[0].IsIdle);
            Assert.Equal("user1", result[0].UserHost);
            Assert.Equal("solidworks", result[0].Feature);
        }

        [Fact]
        public async Task QueryUsageStatisticsAsync_WithValidServer_ShouldReturnStatistics()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                TotalLicenses = 100,
                LicensesInUse = 50,
                AvailableLicenses = 50,
                TotalActiveUsers = 25,
                TotalIdleUsers = 10,
                TotalBorrowedUsers = 15
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync(status);

            // Act
            var result = await _queryEngine.QueryUsageStatisticsAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.Equal(100, result.TotalLicenses);
            Assert.Equal(50, result.LicensesInUse);
            Assert.Equal(50, result.AvailableLicenses);
            Assert.Equal(25, result.ActiveUsers);
            Assert.Equal(10, result.IdleUsers);
            Assert.Equal(15, result.BorrowedUsers);
            Assert.Equal(50.0, result.UtilizationPercentage);
            Assert.Equal(50.0, result.AvailabilityPercentage);
            Assert.Equal(20.0, result.IdlePercentage); // 10 idle out of 50 total users
        }

        [Fact]
        public async Task QueryMultipleServersAsync_WithValidServers_ShouldReturnAllStatuses()
        {
            // Arrange
            var servers = new[] { "server1", "server2" };
            var ports = new[] { 27000, 27001 };
            var cancellationToken = CancellationToken.None;

            var status1 = new LicenseServerStatus { Server = "server1", Port = 27000, IsServerUp = true };
            var status2 = new LicenseServerStatus { Server = "server2", Port = 27001, IsServerUp = true };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(It.IsAny<string>(), It.IsAny<int>(), cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), cancellationToken))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "output" });

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((s, a) => s.Contains("server1") ? status1 : status2);

            // Act
            var result = await _queryEngine.QueryMultipleServersAsync(servers, ports, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("server1:27000"));
            Assert.True(result.ContainsKey("server2:27001"));

            Assert.Equal(status1, result["server1:27000"]);
            Assert.Equal(status2, result["server2:27001"]);

            // Should have called lmstat twice
            _mockProcessExecutor.Verify(
                x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), cancellationToken),
                Times.Exactly(2));
        }

        [Fact]
        public async Task QueryMultipleServersAsync_WithMismatchedCounts_ShouldThrowArgumentException()
        {
            // Arrange
            var servers = new[] { "server1", "server2" };
            var ports = new[] { 27000 }; // Only one port
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _queryEngine.QueryMultipleServersAsync(servers, ports, cancellationToken));
        }

        [Fact]
        public async Task CheckServerHealthAsync_WithHealthyServer_ShouldReturnHealthyStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = true,
                IsHealthy = true,
                StatusMessage = "Server is healthy"
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync(status);

            // Act
            var result = await _queryEngine.CheckServerHealthAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.True(result.IsHealthy);
            Assert.Equal("Server is healthy", result.Message);
            Assert.True(result.ResponseTimeMs > 0);
        }

        [Fact]
        public async Task CheckServerHealthAsync_WithUnhealthyServer_ShouldReturnUnhealthyStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var status = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsServerUp = false,
                IsHealthy = false,
                StatusMessage = "Server is down",
                ErrorMessage = "Connection refused"
            };

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync(status);

            // Act
            var result = await _queryEngine.CheckServerHealthAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.False(result.IsHealthy);
            Assert.Equal("Health check failed", result.Message);
            Assert.Equal("Connection refused", result.ErrorMessage);
        }

        [Fact]
        public async Task InvalidateServerCacheAsync_WithValidServer_ShouldCallCacheManager()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            // Act
            await _queryEngine.InvalidateServerCacheAsync(server, port, cancellationToken);

            // Assert
            _mockCacheManager.Verify(
                x => x.InvalidateServerAsync(server, port, cancellationToken),
                Times.Once);
        }

        [Fact]
        public async Task InvalidateFeatureCacheAsync_WithValidFeature_ShouldCallCacheManager()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "solidworks";
            var cancellationToken = CancellationToken.None;

            // Act
            await _queryEngine.InvalidateFeatureCacheAsync(server, port, feature, cancellationToken);

            // Assert
            _mockCacheManager.Verify(
                x => x.InvalidateFeatureAsync(server, port, feature, cancellationToken),
                Times.Once);
        }

        [Fact]
        public async Task ClearAllCacheAsync_ShouldCallCacheManager()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            // Act
            await _queryEngine.ClearAllCacheAsync(cancellationToken);

            // Assert
            _mockCacheManager.Verify(
                x => x.ClearAsync(cancellationToken),
                Times.Once);
        }

        [Fact]
        public void GetPerformanceMetrics_ShouldReturnCurrentMetrics()
        {
            // Arrange
            _queryEngine.ResetPerformanceMetrics();

            // Act
            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(0, metrics.TotalQueries);
            Assert.Equal(0, metrics.SuccessfulQueries);
            Assert.Equal(0, metrics.FailedQueries);
            Assert.Equal(0, metrics.CachedQueries);
            Assert.Equal(0, metrics.AverageQueryTimeMs);
            Assert.Equal(0, metrics.MinQueryTimeMs);
            Assert.Equal(0, metrics.MaxQueryTimeMs);
            Assert.Equal(0, metrics.CacheHitRatio);
        }

        [Fact]
        public void ResetPerformanceMetrics_ShouldResetAllMetrics()
        {
            // Arrange
            // Execute a query to populate metrics
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), cancellationToken))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "output" });

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new LicenseServerStatus { Server = server, Port = port, IsServerUp = true });

            // Act
            // Execute query first to populate metrics
            _ = _queryEngine.QueryLicenseStatusAsync(server, port, cancellationToken).GetAwaiter().GetResult();

            // Reset metrics
            _queryEngine.ResetPerformanceMetrics();

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(0, metrics.TotalQueries);
            Assert.Equal(0, metrics.SuccessfulQueries);
            Assert.Equal(0, metrics.FailedQueries);
            Assert.Equal(0, metrics.CachedQueries);
            Assert.Equal(0, metrics.AverageQueryTimeMs);
            Assert.Equal(0, metrics.MinQueryTimeMs);
            Assert.Equal(0, metrics.MaxQueryTimeMs);
            Assert.Equal(0, metrics.CacheHitRatio);
        }

        [Fact]
        public void ValidateConfiguration_WithValidConfiguration_ShouldReturnEmptyList()
        {
            // Arrange - engine is already properly configured

            // Act
            var errors = _queryEngine.ValidateConfiguration();

            // Assert
            Assert.NotNull(errors);
            Assert.Empty(errors);
        }

        [Fact]
        public async Task QueryLicenseStatusAsync_WithTransientError_ShouldRetry()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var callCount = 0;
            var transientException = new LicenseQueryException(
                "Transient error",
                server,
                port,
                "QueryLicenseStatusAsync",
                LicenseQueryErrorCode.NetworkTimeout,
                true);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), cancellationToken))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw transientException;
                    }
                    return new ProcessExecutionResult { ExitCode = 0, Output = "success output" };
                });

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _mockOutputParser
                .Setup(x => x.ParseLmstatOutput(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new LicenseServerStatus { Server = server, Port = port, IsServerUp = true });

            _queryEngine.Options.MaxRetries = 2;
            _queryEngine.Options.RetryDelay = TimeSpan.FromMilliseconds(1);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, callCount); // First call failed, second succeeded
        }

        [Fact]
        public async Task QueryLicenseStatusAsync_WithCancellation_ShouldThrowOperationCancelledException()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cts = new CancellationTokenSource();

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, TimeSpan, CancellationToken>((f, a, t, ct) => cts.Cancel())
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, Output = "output" });

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cts.Token))
                .ReturnsAsync((LicenseServerStatus)null);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port, cts.Token));
        }

        [Fact]
        public async Task QueryLicenseStatusAsync_WithNonTransientError_ShouldNotRetry()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var cancellationToken = CancellationToken.None;

            var nonTransientException = new LicenseQueryException(
                "Non-transient error",
                server,
                port,
                "QueryLicenseStatusAsync",
                LicenseQueryErrorCode.ServerNotFound,
                false);

            _mockProcessExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), cancellationToken))
                .ThrowsAsync(nonTransientException);

            _mockCacheManager
                .Setup(x => x.GetServerStatusAsync(server, port, cancellationToken))
                .ReturnsAsync((LicenseServerStatus)null);

            _queryEngine.Options.MaxRetries = 2;
            _queryEngine.Options.RetryDelay = TimeSpan.FromMilliseconds(1);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port, cancellationToken));

            Assert.Equal("Non-transient error", exception.Message);
            Assert.Equal(LicenseQueryErrorCode.ServerNotFound, exception.ErrorCode);
            Assert.False(exception.IsTransient);
        }
    }
}