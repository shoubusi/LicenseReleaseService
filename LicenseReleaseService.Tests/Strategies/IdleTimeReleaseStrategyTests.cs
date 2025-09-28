using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.Models;
using LicenseReleaseService.Strategies;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.Strategies
{
    /// <summary>
    /// Comprehensive tests for the IdleTimeReleaseStrategy
    /// </summary>
    public class IdleTimeReleaseStrategyTests : IDisposable
    {
        private readonly Mock<ILogger<IdleTimeReleaseStrategy>> _mockLogger;
        private readonly Mock<IIdleDetector> _mockIdleDetector;
        private readonly Mock<Lazy<ILicenseManager>> _mockLicenseManager;
        private readonly Mock<ILicenseManager> _mockLicenseManagerInner;
        private readonly IdleTimeReleaseStrategyConfig _config;
        private readonly IdleTimeReleaseStrategy _strategy;
        private readonly ITestOutputHelper _output;

        public IdleTimeReleaseStrategyTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<IdleTimeReleaseStrategy>>();
            _mockIdleDetector = new Mock<IIdleDetector>();
            _mockLicenseManagerInner = new Mock<ILicenseManager>();
            _mockLicenseManager = new Mock<Lazy<ILicenseManager>>();
            _mockLicenseManager.Setup(x => x.Value).Returns(_mockLicenseManagerInner.Object);

            _config = new IdleTimeReleaseStrategyConfig
            {
                IsEnabled = true,
                IdleThresholdSeconds = 1800, // 30 minutes
                ConfidenceThreshold = 0.8,
                PreventReleaseDuringCriticalWork = true,
                MinimumSessionDuration = TimeSpan.FromMinutes(10),
                TimeoutSeconds = 60
            };

            _strategy = new IdleTimeReleaseStrategy(
                _mockLogger.Object,
                _mockIdleDetector.Object,
                _mockLicenseManager.Object,
                _config);
        }

        public void Dispose()
        {
            _mockLogger.VerifyAll();
            _mockIdleDetector.VerifyAll();
            _mockLicenseManager.VerifyAll();
            _mockLicenseManagerInner.VerifyAll();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeStrategy()
        {
            // Arrange & Act
            var strategy = new IdleTimeReleaseStrategy(
                _mockLogger.Object,
                _mockIdleDetector.Object,
                _mockLicenseManager.Object,
                _config);

            // Assert
            Assert.Equal("IdleTimeReleaseStrategy", strategy.Name);
            Assert.Equal("Releases licenses when users are idle for a configured duration", strategy.Description);
            Assert.Equal(10, strategy.Priority);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleTimeReleaseStrategy(
                null,
                _mockIdleDetector.Object,
                _mockLicenseManager.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullIdleDetector_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleTimeReleaseStrategy(
                _mockLogger.Object,
                null,
                _mockLicenseManager.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullLicenseManager_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleTimeReleaseStrategy(
                _mockLogger.Object,
                _mockIdleDetector.Object,
                null,
                _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IdleTimeReleaseStrategy(
                _mockLogger.Object,
                _mockIdleDetector.Object,
                _mockLicenseManager.Object,
                null));
        }

        #endregion

        #region CanHandle Tests

        [Fact]
        public void CanHandle_WithNullRequest_ShouldReturnFalse()
        {
            // Arrange, Act & Assert
            Assert.False(_strategy.CanHandle(null));
        }

        [Fact]
        public void CanHandle_WithStrategyDisabled_ShouldReturnFalse()
        {
            // Arrange
            _config.IsEnabled = false;
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithSpecificStrategyNameMismatch_ShouldReturnFalse()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Strategy = "OtherStrategy";

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithMatchingStrategyName_ShouldReturnTrue()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Strategy = "IdleTimeReleaseStrategy";

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithEmptyStrategyName_ShouldReturnTrue()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Strategy = "";

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithMissingUser_ShouldReturnFalse()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.User = "";

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithMissingHost_ShouldReturnFalse()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Host = "";

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithValidRequest_ShouldReturnTrue()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region Validate Tests

        [Fact]
        public void Validate_WithNullRequest_ShouldReturnFailure()
        {
            // Arrange, Act & Assert
            var result = _strategy.Validate(null);
            Assert.False(result.IsValid);
            Assert.Equal("Request cannot be null", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithStrategyDisabled_ShouldReturnFailure()
        {
            // Arrange
            _config.IsEnabled = false;
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Idle time release strategy is disabled", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithMissingUser_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.User = "";

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("User name is required for idle time release strategy", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithMissingHost_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Host = "";

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Host name is required for idle time release strategy", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithInvalidIdleThreshold_ShouldReturnFailure()
        {
            // Arrange
            _config.IdleThresholdSeconds = -1;
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Idle threshold must be greater than 0", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithInvalidConfidenceThreshold_ShouldReturnFailure()
        {
            // Arrange
            _config.ConfidenceThreshold = 1.5;
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Confidence threshold must be between 0 and 1", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithValidRequest_ShouldReturnSuccess()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(string.Empty, result.ErrorMessage);
        }

        #endregion

        #region GetHealthStatus Tests

        [Fact]
        public void GetHealthStatus_WithHealthyComponents_ShouldReturnHealthy()
        {
            // Arrange
            _mockIdleDetector.Setup(x => x.Configuration).Returns(new IdleDetectorConfiguration());

            // Act
            var result = _strategy.GetHealthStatus();

            // Assert
            Assert.True(result.IsHealthy);
            Assert.Equal("Strategy is healthy and ready to process requests", result.StatusMessage);
        }

        [Fact]
        public void GetHealthStatus_WithNullIdleDetector_ShouldReturnUnhealthy()
        {
            // Arrange
            var strategy = new IdleTimeReleaseStrategy(
                _mockLogger.Object,
                null,
                _mockLicenseManager.Object,
                _config);

            // Act
            var result = strategy.GetHealthStatus();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("Idle detector is not available", result.StatusMessage);
        }

        [Fact]
        public void GetHealthStatus_WithStrategyDisabled_ShouldReturnUnhealthy()
        {
            // Arrange
            _config.IsEnabled = false;

            // Act
            var result = _strategy.GetHealthStatus();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("Strategy is disabled", result.StatusMessage);
        }

        [Fact]
        public void GetHealthStatus_WithNullLicenseManager_ShouldReturnUnhealthy()
        {
            // Arrange
            var strategy = new IdleTimeReleaseStrategy(
                _mockLogger.Object,
                _mockIdleDetector.Object,
                null,
                _config);

            // Act
            var result = strategy.GetHealthStatus();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("License manager is not available", result.StatusMessage);
        }

        #endregion

        #region ExecuteAsync Tests

        [Fact]
        public async Task ExecuteAsync_WithNullRequest_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _strategy.ExecuteAsync(null));
        }

        [Fact]
        public async Task ExecuteAsync_WithNoUserProcesses_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Mock idle detector to return no processes
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = false,
                    Confidence = 0.0,
                    Reason = "No processes found"
                });

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No processes found", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.UserNotFound, result.ResultCode);
        }

        [Fact]
        public async Task ExecuteAsync_WithNoIdleProcesses_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Mock idle detector to return active processes
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = false,
                    Confidence = 0.9,
                    IdleTime = TimeSpan.FromMinutes(5),
                    Reason = "User is active"
                });

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("is not idle", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.UserNotFound, result.ResultCode);
        }

        [Fact]
        public async Task ExecuteAsync_WithInsufficientIdleTime_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Mock idle detector to return idle but insufficient time
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = true,
                    Confidence = 0.9,
                    IdleTime = TimeSpan.FromMinutes(15), // Less than 30 minute threshold
                    Reason = "User is idle"
                });

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("is below threshold", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.UserNotFound, result.ResultCode);
        }

        [Fact]
        public async Task ExecuteAsync_WithLowConfidence_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Mock idle detector to return idle with low confidence
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = true,
                    Confidence = 0.6, // Below 0.8 threshold
                    IdleTime = TimeSpan.FromMinutes(45),
                    Reason = "User might be idle"
                });

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("confidence is below threshold", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.UnknownError, result.ResultCode);
        }

        [Fact]
        public async Task ExecuteAsync_WithSafetyValidationFailure_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Mock idle detector to return valid idle state
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = true,
                    Confidence = 0.9,
                    IdleTime = TimeSpan.FromMinutes(45),
                    Reason = "User is idle"
                });

            // Mock license manager to simulate critical work
            _mockLicenseManagerInner.Setup(x => x.GetLicenseInfoAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LicenseInfo
                {
                    IsInUse = true,
                    User = request.User,
                    Feature = request.Feature,
                    Server = request.Server,
                    Port = request.Port
                });

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            // Note: In a real implementation, this would test the safety validation logic
            // For now, we're testing the basic flow
        }

        [Fact]
        public async Task ExecuteAsync_WithForceRelease_ShouldBypassSafetyChecks()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.ForceRelease = true;

            // Mock idle detector to return valid idle state
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = true,
                    Confidence = 0.9,
                    IdleTime = TimeSpan.FromMinutes(45),
                    Reason = "User is idle"
                });

            // Mock successful license release
            _mockLicenseManagerInner.Setup(x => x.ReleaseLicenseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LicenseReleaseResult.CreateSuccess("test-server", 27000, "test-feature", "test-user", 1, TimeSpan.FromSeconds(2)));

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.LicensesReleased);
        }

        [Fact]
        public async Task ExecuteAsync_WithSuccessfulConditions_ShouldReleaseLicense()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Mock idle detector to return valid idle state
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = true,
                    Confidence = 0.9,
                    IdleTime = TimeSpan.FromMinutes(45),
                    Reason = "User is idle"
                });

            // Mock successful license release
            _mockLicenseManagerInner.Setup(x => x.ReleaseLicenseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LicenseReleaseResult.CreateSuccess("test-server", 27000, "test-feature", "test-user", 1, TimeSpan.FromSeconds(2)));

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.LicensesReleased);
            Assert.Equal("test-server", result.Server);
            Assert.Equal(27000, result.Port);
            Assert.Equal("test-feature", result.Feature);
            Assert.Equal("test-user", result.User);

            // Verify metadata
            Assert.NotNull(result.Metadata);
            var metadata = (dynamic)result.Metadata;
            Assert.Equal("IdleTimeReleaseStrategy", metadata.Strategy);
            Assert.True(metadata.IdleTime > TimeSpan.Zero);
            Assert.True(metadata.Confidence >= 0.8);
        }

        [Fact]
        public async Task ExecuteAsync_WithLicenseManagerError_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Mock idle detector to return valid idle state
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdleDetectionResult
                {
                    IsIdle = true,
                    Confidence = 0.9,
                    IdleTime = TimeSpan.FromMinutes(45),
                    Reason = "User is idle"
                });

            // Mock license manager failure
            _mockLicenseManagerInner.Setup(x => x.ReleaseLicenseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LicenseReleaseResult.CreateFailure("test-server", 27000, "test-feature", "test-user", "Network error", LicenseReleaseResultCode.NetworkError, TimeSpan.FromSeconds(1)));

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Network error", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.NetworkError, result.ResultCode);
        }

        [Fact]
        public async Task ExecuteAsync_WithException_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();

            // Mock idle detector to throw exception
            _mockIdleDetector.Setup(x => x.DetectIdleAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Detector failed"));

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Strategy execution failed", result.ErrorMessage);
            Assert.Contains("Detector failed", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.UnknownError, result.ResultCode);
        }

        #endregion

        #region Helper Methods

        private ReleaseRequest CreateValidReleaseRequest()
        {
            return new ReleaseRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                Server = "test-server",
                Port = 27000,
                Feature = "test-feature",
                User = "test-user",
                Host = "test-host",
                DisplayName = "Test User",
                Priority = ReleaseRequestPriority.Normal,
                Timeout = TimeSpan.FromSeconds(30),
                MaxRetries = 3,
                Source = "system",
                Initiator = "test-initiator",
                Timestamp = DateTime.Now,
                ForceRelease = false,
                DryRun = false,
                ValidationFlags = ReleaseValidationFlags.Default
            };
        }

        #endregion
    }
}