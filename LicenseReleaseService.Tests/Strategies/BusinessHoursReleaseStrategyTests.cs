using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    /// Comprehensive tests for the BusinessHoursReleaseStrategy
    /// </summary>
    public class BusinessHoursReleaseStrategyTests : IDisposable
    {
        private readonly Mock<ILogger<BusinessHoursReleaseStrategy>> _mockLogger;
        private readonly Mock<Lazy<ILicenseManager>> _mockLicenseManager;
        private readonly Mock<ILicenseManager> _mockLicenseManagerInner;
        private readonly BusinessHoursReleaseStrategyConfig _config;
        private readonly BusinessHoursReleaseStrategy _strategy;
        private readonly ITestOutputHelper _output;

        public BusinessHoursReleaseStrategyTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<BusinessHoursReleaseStrategy>>();
            _mockLicenseManagerInner = new Mock<ILicenseManager>();
            _mockLicenseManager = new Mock<Lazy<ILicenseManager>>();
            _mockLicenseManager.Setup(x => x.Value).Returns(_mockLicenseManagerInner.Object);

            _config = new BusinessHoursReleaseStrategyConfig
            {
                IsEnabled = true,
                WorkingDays = new List<DayOfWeek>
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                },
                BusinessHours = new List<BusinessHours>
                {
                    new BusinessHours(TimeSpan.FromHours(9), TimeSpan.FromHours(17)) // 9 AM to 5 PM
                },
                GracePeriodMinutes = 15,
                MinimumTimeBeforeEndMinutes = 30,
                RespectHolidays = true,
                PreventReleaseDuringLunch = true,
                LunchBreakHours = new List<BusinessHours>
                {
                    new BusinessHours(TimeSpan.FromHours(12), TimeSpan.FromHours(13)) // 12 PM to 1 PM
                },
                TimeoutSeconds = 60
            };

            _strategy = new BusinessHoursReleaseStrategy(
                _mockLogger.Object,
                _mockLicenseManager.Object,
                _config);
        }

        public void Dispose()
        {
            _mockLogger.VerifyAll();
            _mockLicenseManager.VerifyAll();
            _mockLicenseManagerInner.VerifyAll();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeStrategy()
        {
            // Arrange & Act
            var strategy = new BusinessHoursReleaseStrategy(
                _mockLogger.Object,
                _mockLicenseManager.Object,
                _config);

            // Assert
            Assert.Equal("BusinessHoursReleaseStrategy", strategy.Name);
            Assert.Equal("Releases licenses during configured business hours and days", strategy.Description);
            Assert.Equal(5, strategy.Priority);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BusinessHoursReleaseStrategy(
                null,
                _mockLicenseManager.Object,
                _config));
        }

        [Fact]
        public void Constructor_WithNullLicenseManager_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BusinessHoursReleaseStrategy(
                _mockLogger.Object,
                null,
                _config));
        }

        [Fact]
        public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BusinessHoursReleaseStrategy(
                _mockLogger.Object,
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
            request.Strategy = "BusinessHoursReleaseStrategy";

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithEmptyStrategyNameAndBusinessHours_ShouldReturnTrue()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Strategy = "";
            // Set time to be within business hours
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 10, 0, 0); // Monday 10 AM

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.True(result);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public void CanHandle_WithWeekend_ShouldReturnFalse()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Strategy = "";
            // Set time to weekend
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 2, 10, 0, 0); // Saturday 10 AM

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.False(result);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public void CanHandle_WithOutsideBusinessHours_ShouldReturnFalse()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Strategy = "";
            // Set time to outside business hours
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 20, 0, 0); // Monday 8 PM

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.False(result);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public void CanHandle_WithDuringLunchBreak_ShouldReturnTrue()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Strategy = "";
            // Set time to during lunch break
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 12, 30, 0); // Monday 12:30 PM

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.True(result);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public void CanHandle_WithValidBusinessHours_ShouldReturnTrue()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            request.Strategy = "";
            // Set time to valid business hours
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 14, 0, 0); // Monday 2 PM

            // Act
            var result = _strategy.CanHandle(request);

            // Assert
            Assert.True(result);

            // Reset time provider
            TestTimeProvider.Reset();
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
            Assert.Equal("Business hours release strategy is disabled", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithNoBusinessHoursConfigured_ShouldReturnFailure()
        {
            // Arrange
            _config.BusinessHours.Clear();
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("No business hours configured", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithNoWorkingDaysConfigured_ShouldReturnFailure()
        {
            // Arrange
            _config.WorkingDays.Clear();
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("No working days configured", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithNegativeGracePeriod_ShouldReturnFailure()
        {
            // Arrange
            _config.GracePeriodMinutes = -1;
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Grace period cannot be negative", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithNegativeMinimumTimeBeforeEnd_ShouldReturnFailure()
        {
            // Arrange
            _config.MinimumTimeBeforeEndMinutes = -1;
            var request = CreateValidReleaseRequest();

            // Act
            var result = _strategy.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Minimum time before end cannot be negative", result.ErrorMessage);
        }

        [Fact]
        public void Validate_WithValidConfiguration_ShouldReturnSuccess()
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
        public void GetHealthStatus_WithHealthyConfiguration_ShouldReturnHealthy()
        {
            // Arrange & Act
            var result = _strategy.GetHealthStatus();

            // Assert
            Assert.True(result.IsHealthy);
            Assert.Equal("Strategy is healthy and ready to process requests", result.StatusMessage);
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
        public void GetHealthStatus_WithNoBusinessHoursConfigured_ShouldReturnUnhealthy()
        {
            // Arrange
            _config.BusinessHours.Clear();

            // Act
            var result = _strategy.GetHealthStatus();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("No business hours configured", result.StatusMessage);
        }

        [Fact]
        public void GetHealthStatus_WithNoWorkingDaysConfigured_ShouldReturnUnhealthy()
        {
            // Arrange
            _config.WorkingDays.Clear();

            // Act
            var result = _strategy.GetHealthStatus();

            // Assert
            Assert.False(result.IsHealthy);
            Assert.Equal("No working days configured", result.StatusMessage);
        }

        [Fact]
        public void GetHealthStatus_WithNullLicenseManager_ShouldReturnUnhealthy()
        {
            // Arrange
            var strategy = new BusinessHoursReleaseStrategy(
                _mockLogger.Object,
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
        public async Task ExecuteAsync_WithWeekend_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to weekend
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 2, 10, 0, 0); // Saturday 10 AM

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("outside business hours", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.PermissionDenied, result.ResultCode);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithOutsideBusinessHours_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to outside business hours
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 20, 0, 0); // Monday 8 PM

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("outside business hours", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.PermissionDenied, result.ResultCode);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithTooCloseToBusinessHoursEnd_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to 4:45 PM (30 minutes before 5:15 PM end time with grace period)
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 16, 45, 0); // Monday 4:45 PM

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Too close to business hours end", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.PermissionDenied, result.ResultCode);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithDuringLunchBreak_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to during lunch break
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 12, 30, 0); // Monday 12:30 PM

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("license release is not allowed during lunch break", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.PermissionDenied, result.ResultCode);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithHoliday_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to New Year's Day
            TestTimeProvider.CurrentTime = new DateTime(2023, 1, 1, 10, 0, 0); // New Year's Day 10 AM

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Today is a holiday", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.PermissionDenied, result.ResultCode);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithGracePeriod_ShouldAllowExecution()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to 8:50 AM (within 15-minute grace period before 9 AM)
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 8, 50, 0); // Monday 8:50 AM

            // Mock successful license release
            _mockLicenseManagerInner.Setup(x => x.ReleaseLicenseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LicenseReleaseResult.CreateSuccess("test-server", 27000, "test-feature", "test-user", 1, TimeSpan.FromSeconds(2)));

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.LicensesReleased);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithValidBusinessHours_ShouldReleaseLicense()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to valid business hours
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 14, 0, 0); // Monday 2 PM

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
            Assert.Equal("BusinessHoursReleaseStrategy", metadata.Strategy);
            Assert.False(metadata.IsWithinGracePeriod);
            Assert.Equal(DayOfWeek.Monday, metadata.DayOfWeek);
            Assert.False(metadata.IsWeekend);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithLicenseManagerError_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to valid business hours
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 14, 0, 0); // Monday 2 PM

            // Mock license manager failure
            _mockLicenseManagerInner.Setup(x => x.ReleaseLicenseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LicenseReleaseResult.CreateFailure("test-server", 27000, "test-feature", "test-user", "Network error", LicenseReleaseResultCode.NetworkError, TimeSpan.FromSeconds(1)));

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Network error", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.NetworkError, result.ResultCode);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithException_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to valid business hours
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 14, 0, 0); // Monday 2 PM

            // Mock license manager to throw exception
            _mockLicenseManagerInner.Setup(x => x.ReleaseLicenseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("License manager failed"));

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Strategy execution failed", result.ErrorMessage);
            Assert.Contains("License manager failed", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.UnknownError, result.ResultCode);

            // Reset time provider
            TestTimeProvider.Reset();
        }

        [Fact]
        public async Task ExecuteAsync_WithCompanySpecificRestrictions_ShouldReturnFailure()
        {
            // Arrange
            var request = CreateValidReleaseRequest();
            // Set time to valid business hours
            TestTimeProvider.CurrentTime = new DateTime(2023, 12, 4, 14, 0, 0); // Monday 2 PM

            // Add company restriction for current hour
            _config.CompanySpecificRestrictions.Add(new CompanyRestriction(14, 15, "Maintenance window"));

            // Act
            var result = await _strategy.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Company-specific restrictions", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.PermissionDenied, result.ResultCode);

            // Reset time provider
            TestTimeProvider.Reset();
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

    /// <summary>
    /// Helper class for controlling time during tests
    /// </summary>
    public static class TestTimeProvider
    {
        private static DateTime _currentTime = DateTime.Now;

        public static DateTime CurrentTime
        {
            get => _currentTime;
            set => _currentTime = value;
        }

        public static void Reset()
        {
            _currentTime = DateTime.Now;
        }

        public static DateTime Now => _currentTime;
    }
}