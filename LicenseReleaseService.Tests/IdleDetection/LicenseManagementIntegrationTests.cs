using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.LicenseManagement;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class LicenseManagementIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILicenseManager> _mockLicenseManager;
        private readonly Mock<ConfigurationIntegration> _mockConfigIntegration;
        private readonly LicenseManagementIntegration _licenseIntegration;

        public LicenseManagementIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLicenseManager = new Mock<ILicenseManager>();
            _mockConfigIntegration = new Mock<ConfigurationIntegration>();

            // Setup default configuration
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(true);
            _mockConfigIntegration.Setup(c => c.DetectionInterval).Returns(TimeSpan.FromSeconds(60));
            _mockConfigIntegration.Setup(c => c.DefaultIdleThreshold).Returns(TimeSpan.FromMinutes(15));
            _mockConfigIntegration.Setup(c => c.ConfidenceThreshold).Returns(0.7);

            _licenseIntegration = new LicenseManagementIntegration(
                _mockLicenseManager.Object,
                _mockConfigIntegration.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Arrange & Act
            var integration = new LicenseManagementIntegration(
                _mockLicenseManager.Object,
                _mockConfigIntegration.Object);

            // Assert
            Assert.NotNull(integration);
            Assert.True(integration.IsEnabled);
        }

        [Fact]
        public void Constructor_WithNullLicenseManager_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LicenseManagementIntegration(
                null,
                _mockConfigIntegration.Object));
        }

        [Fact]
        public void Constructor_WithNullConfigIntegration_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LicenseManagementIntegration(
                _mockLicenseManager.Object,
                null));
        }

        [Fact]
        public void IsEnabled_WhenConfigEnabled_ShouldReturnTrue()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(true);

            // Act
            var isEnabled = _licenseIntegration.IsEnabled;

            // Assert
            Assert.True(isEnabled);
        }

        [Fact]
        public void IsEnabled_WhenConfigDisabled_ShouldReturnFalse()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);

            // Act
            var isEnabled = _licenseIntegration.IsEnabled;

            // Assert
            Assert.False(isEnabled);
        }

        [Fact]
        public void ActiveSessions_ShouldReturnReadOnlyDictionary()
        {
            // Arrange & Act
            var sessions = _licenseIntegration.ActiveSessions;

            // Assert
            Assert.NotNull(sessions);
            Assert.True(sessions.IsReadOnly);
        }

        [Fact]
        public void DetectionResults_ShouldReturnReadOnlyDictionary()
        {
            // Arrange & Act
            var results = _licenseIntegration.DetectionResults;

            // Assert
            Assert.NotNull(results);
            Assert.True(results.IsReadOnly);
        }

        [Fact]
        public async Task ProcessIdleDetectionResultsAsync_WithNullResults_ShouldReturn()
        {
            // Arrange, Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await _licenseIntegration.ProcessIdleDetectionResultsAsync(null);
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task ProcessIdleDetectionResultsAsync_WithEmptyResults_ShouldReturn()
        {
            // Arrange
            var emptyResults = Enumerable.Empty<IdleDetectionResult>();

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await _licenseIntegration.ProcessIdleDetectionResultsAsync(emptyResults);
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task ProcessIdleDetectionResultsAsync_WhenDisabled_ShouldReturn()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);
            var results = new List<IdleDetectionResult> { CreateTestDetectionResult() };

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await _licenseIntegration.ProcessIdleDetectionResultsAsync(results);
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task GetReleaseCandidatesAsync_WhenDisabled_ShouldReturnEmpty()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);

            // Act
            var candidates = await _licenseIntegration.GetReleaseCandidatesAsync();

            // Assert
            Assert.NotNull(candidates);
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task GetReleaseCandidatesAsync_WithNoSessions_ShouldReturnEmpty()
        {
            // Arrange
            _mockLicenseManager.Setup(m => m.GetUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, LicenseUserUsage>());

            // Act
            var candidates = await _licenseIntegration.GetReleaseCandidatesAsync();

            // Assert
            Assert.NotNull(candidates);
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task GetReleaseCandidatesAsync_WithIdleSessions_ShouldReturnCandidates()
        {
            // Arrange
            var users = new Dictionary<string, LicenseUserUsage>
            {
                ["testuser"] = CreateTestUserUsage("testuser", "solidworks", true, TimeSpan.FromMinutes(20))
            };

            _mockLicenseManager.Setup(m => m.GetUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Add detection result
            var detectionResult = CreateTestDetectionResult("testuser_solidworks_20240101120000", TimeSpan.FromMinutes(20));
            await _licenseIntegration.ProcessIdleDetectionResultsAsync(new[] { detectionResult });

            // Act
            var candidates = await _licenseIntegration.GetReleaseCandidatesAsync();

            // Assert
            Assert.NotNull(candidates);
            Assert.Single(candidates);
            Assert.Equal("testuser", candidates.First().UserName);
        }

        [Fact]
        public async Task ReleaseIdleLicensesAsync_WhenDisabled_ShouldReturnFailure()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);

            // Act
            var result = await _licenseIntegration.ReleaseIdleLicensesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("not enabled", result.ErrorMessage);
        }

        [Fact]
        public async Task ReleaseIdleLicensesAsync_WithNoCandidates_ShouldReturnSuccess()
        {
            // Arrange
            _mockLicenseManager.Setup(m => m.GetUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, LicenseUserUsage>());

            // Act
            var result = await _licenseIntegration.ReleaseIdleLicensesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(0, result.TotalCandidates);
        }

        [Fact]
        public async Task ReleaseIdleLicensesAsync_WithCandidates_ShouldReleaseLicenses()
        {
            // Arrange
            var users = new Dictionary<string, LicenseUserUsage>
            {
                ["testuser"] = CreateTestUserUsage("testuser", "solidworks", true, TimeSpan.FromMinutes(20))
            };

            _mockLicenseManager.Setup(m => m.GetUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            _mockLicenseManager.Setup(m => m.ReleaseLicenseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LicenseReleaseResult { Success = true });

            // Add detection result
            var detectionResult = CreateTestDetectionResult("testuser_solidworks_20240101120000", TimeSpan.FromMinutes(20));
            await _licenseIntegration.ProcessIdleDetectionResultsAsync(new[] { detectionResult });

            // Act
            var result = await _licenseIntegration.ReleaseIdleLicensesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(1, result.TotalCandidates);
            Assert.Equal(1, result.TotalReleased);
            Assert.Equal(0, result.TotalFailed);
        }

        [Fact]
        public async Task GetSessionStatisticsAsync_WhenDisabled_ShouldReturnDefaultStats()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);

            // Act
            var stats = await _licenseIntegration.GetSessionStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalSessions);
        }

        [Fact]
        public async Task GetSessionStatisticsAsync_WithSessions_ShouldReturnCorrectStats()
        {
            // Arrange
            var users = new Dictionary<string, LicenseUserUsage>
            {
                ["testuser"] = CreateTestUserUsage("testuser", "solidworks", true, TimeSpan.FromMinutes(10)),
                ["activeuser"] = CreateTestUserUsage("activeuser", "solidworks", false, TimeSpan.Zero)
            };

            _mockLicenseManager.Setup(m => m.GetUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Add detection result
            var detectionResult = CreateTestDetectionResult("testuser_solidworks_20240101120000", TimeSpan.FromMinutes(10));
            await _licenseIntegration.ProcessIdleDetectionResultsAsync(new[] { detectionResult });

            // Act
            var stats = await _licenseIntegration.GetSessionStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(2, stats.TotalSessions);
            Assert.Equal(1, stats.ActiveSessions);
            Assert.Equal(1, stats.IdleSessions);
            Assert.Equal(1, stats.MonitoredSessions);
            Assert.Equal(0.7, stats.ConfidenceThreshold);
        }

        [Fact]
        public async Task ValidateSessionCompatibilityAsync_WithNullSession_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _licenseIntegration.ValidateSessionCompatibilityAsync(null));
        }

        [Fact]
        public async Task ValidateSessionCompatibilityAsync_WithValidSession_ShouldReturnValid()
        {
            // Arrange
            var session = CreateTestLicenseUserSession();
            _mockLicenseManager.Setup(m => m.IsServerAvailableAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _licenseIntegration.ValidateSessionCompatibilityAsync(session);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Empty(result.CompatibilityIssues);
        }

        [Fact]
        public async Task ValidateSessionCompatibilityAsync_WithInactiveSession_ShouldReturnInvalid()
        {
            // Arrange
            var session = CreateTestLicenseUserSession();
            session.IsActive = false;

            // Act
            var result = await _licenseIntegration.ValidateSessionCompatibilityAsync(session);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Contains("not active", result.CompatibilityIssues.First(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateSessionCompatibilityAsync_WithServerUnavailable_ShouldReturnCompatibilityIssues()
        {
            // Arrange
            var session = CreateTestLicenseUserSession();
            _mockLicenseManager.Setup(m => m.IsServerAvailableAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _licenseIntegration.ValidateSessionCompatibilityAsync(session);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid); // Should still be valid but with compatibility issues
            Assert.Contains("not available", result.CompatibilityIssues.First(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void LicenseReleaseTriggered_Event_ShouldBeRaisedWhenLicenseReleased()
        {
            // Arrange
            var eventRaised = false;
            LicenseReleaseEventArgs args = null;

            _licenseIntegration.LicenseReleaseTriggered += (sender, e) =>
            {
                eventRaised = true;
                args = e;
            };

            var users = new Dictionary<string, LicenseUserUsage>
            {
                ["testuser"] = CreateTestUserUsage("testuser", "solidworks", true, TimeSpan.FromMinutes(20))
            };

            _mockLicenseManager.Setup(m => m.GetUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            _mockLicenseManager.Setup(m => m.ReleaseLicenseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LicenseReleaseResult { Success = true });

            // Add detection result
            var detectionResult = CreateTestDetectionResult("testuser_solidworks_20240101120000", TimeSpan.FromMinutes(20));

            // Act
            _licenseIntegration.ProcessIdleDetectionResultsAsync(new[] { detectionResult }).Wait();
            _licenseIntegration.ReleaseIdleLicensesAsync().Wait();

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(args);
            Assert.NotNull(args.Candidate);
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            var exception = Record.Exception(() =>
            {
                _licenseIntegration.Dispose();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_MultipleTimes_ShouldNotThrow()
        {
            // Arrange
            _licenseIntegration.Dispose();

            // Act & Assert
            var exception = Record.Exception(() =>
            {
                _licenseIntegration.Dispose();
            });

            Assert.Null(exception);
        }

        private IdleDetectionResult CreateTestDetectionResult(string sessionId = "test-session", TimeSpan? idleDuration = null)
        {
            return new IdleDetectionResult
            {
                SessionId = sessionId,
                IsIdle = true,
                IdleDuration = idleDuration ?? TimeSpan.FromMinutes(10),
                ConfidenceScore = 0.8,
                DetectionMethods = new[] { "TimeBased", "ActivityMonitor" },
                DetectionTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow.AddMinutes(-10)
            };
        }

        private LicenseUserUsage CreateTestUserUsage(string user, string feature, bool isIdle, TimeSpan idleTime)
        {
            return new LicenseUserUsage
            {
                User = user,
                FeatureUsages = new List<LicenseFeatureUsage>
                {
                    new LicenseFeatureUsage
                    {
                        Feature = feature,
                        IsIdle = isIdle,
                        LastActivity = DateTime.UtcNow.Add(-idleTime),
                        LoginTime = DateTime.UtcNow.AddHours(-1),
                        IsActive = !isIdle,
                        CanBeReleased = true
                    }
                }
            };
        }

        private LicenseUserSession CreateTestLicenseUserSession()
        {
            return new LicenseUserSession
            {
                SessionId = "test-session-id",
                UserName = "testuser",
                Feature = "solidworks",
                Server = "test-server",
                Port = 27000,
                LoginTime = DateTime.UtcNow.AddHours(-1),
                LastActivity = DateTime.UtcNow.AddMinutes(-5),
                IsActive = true,
                IsIdle = false,
                FeatureUsage = new LicenseFeatureUsage
                {
                    Feature = "solidworks",
                    IsActive = true,
                    IsIdle = false,
                    CanBeReleased = true
                }
            };
        }

        public void Dispose()
        {
            _licenseIntegration?.Dispose();
        }
    }

    /// <summary>
    /// Test helper classes for license management tests
    /// </summary>
    public static class LicenseManagementTestHelper
    {
        public static LicenseReleaseCandidate CreateTestReleaseCandidate(
            string sessionId = "test-session",
            string userName = "testuser",
            string feature = "solidworks",
            TimeSpan? idleTime = null)
        {
            return new LicenseReleaseCandidate
            {
                SessionId = sessionId,
                UserName = userName,
                Feature = feature,
                Server = "test-server",
                Port = 27000,
                IdleTime = idleTime ?? TimeSpan.FromMinutes(15),
                ConfidenceScore = 0.8,
                DetectionMethods = new[] { "TimeBased" },
                LastActivity = DateTime.UtcNow.AddMinutes(-15),
                Reason = "Idle detection"
            };
        }

        public static LicenseUserSession CreateTestLicenseUserSession(
            string sessionId = "test-session",
            string userName = "testuser",
            string feature = "solidworks",
            bool isActive = true,
            bool isIdle = false)
        {
            return new LicenseUserSession
            {
                SessionId = sessionId,
                UserName = userName,
                Feature = feature,
                Server = "test-server",
                Port = 27000,
                LoginTime = DateTime.UtcNow.AddHours(-1),
                LastActivity = DateTime.UtcNow.AddMinutes(-5),
                IsActive = isActive,
                IsIdle = isIdle,
                FeatureUsage = new LicenseFeatureUsage
                {
                    Feature = feature,
                    IsActive = isActive,
                    IsIdle = isIdle,
                    CanBeReleased = true
                }
            };
        }

        public static IdleDetectionResult CreateTestIdleDetectionResult(
            string sessionId = "test-session",
            bool isIdle = true,
            TimeSpan? idleDuration = null,
            double confidenceScore = 0.8)
        {
            return new IdleDetectionResult
            {
                SessionId = sessionId,
                IsIdle = isIdle,
                IdleDuration = idleDuration ?? TimeSpan.FromMinutes(10),
                ConfidenceScore = confidenceScore,
                DetectionMethods = new[] { "TimeBased" },
                DetectionTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow.AddMinutes(-10)
            };
        }
    }
}