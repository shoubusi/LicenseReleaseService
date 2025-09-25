using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.Tests.LicenseManagement.Models
{
    /// <summary>
    /// Unit tests for LicenseInfo class
    /// </summary>
    public class LicenseInfoTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var licenseInfo = new LicenseInfo();

            // Assert
            Assert.NotEmpty(licenseInfo.UserHost);
            Assert.NotEmpty(licenseInfo.Feature);
            Assert.NotEqual(default, licenseInfo.BorrowTime);
            Assert.NotEqual(default, licenseInfo.Status);
            Assert.Equal(TimeSpan.Zero, licenseInfo.UsageDuration);
            Assert.False(licenseInfo.IsIdle);
            Assert.Empty(licenseInfo.IdleReason);
            Assert.Empty(licenseInfo.ClientHost);
            Assert.Empty(licenseInfo.LicenseVersion);
            Assert.Equal(LicenseStatus.Unknown, licenseInfo.Status);
        }

        [Fact]
        public void Constructor_WithParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var userHost = "test-user";
            var feature = "solidworks";
            var borrowTime = DateTime.Now.AddMinutes(-30);
            var status = LicenseStatus.Active;

            // Act
            var licenseInfo = new LicenseInfo(userHost, feature, borrowTime, status);

            // Assert
            Assert.Equal(userHost, licenseInfo.UserHost);
            Assert.Equal(feature, licenseInfo.Feature);
            Assert.Equal(borrowTime, licenseInfo.BorrowTime);
            Assert.Equal(status, licenseInfo.Status);
            Assert.True(licenseInfo.UsageDuration.TotalSeconds > 0);
        }

        [Fact]
        public void UserHost_WithValidValue_ShouldSetCorrectly()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            var validUserHost = "test-user-host";

            // Act
            licenseInfo.UserHost = validUserHost;

            // Assert
            Assert.Equal(validUserHost, licenseInfo.UserHost);
        }

        [Fact]
        public void UserHost_WithNullValue_ShouldThrowException()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => licenseInfo.UserHost = null);
        }

        [Fact]
        public void UserHost_WithWhiteSpaceValue_ShouldThrowException()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => licenseInfo.UserHost = "   ");
        }

        [Fact]
        public void UserHost_WithLongValue_ShouldThrowException()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            var longUserHost = new string('a', 256);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => licenseInfo.UserHost = longUserHost);
        }

        [Fact]
        public void Feature_WithValidValue_ShouldSetCorrectly()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            var validFeature = "solidworks-premium";

            // Act
            licenseInfo.Feature = validFeature;

            // Assert
            Assert.Equal(validFeature, licenseInfo.Feature);
        }

        [Fact]
        public void BorrowTime_WithValidValue_ShouldSetCorrectly()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            var validBorrowTime = DateTime.Now.AddMinutes(-10);

            // Act
            licenseInfo.BorrowTime = validBorrowTime;

            // Assert
            Assert.Equal(validBorrowTime, licenseInfo.BorrowTime);
        }

        [Fact]
        public void BorrowTime_WithDefaultValue_ShouldThrowException()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => licenseInfo.BorrowTime = default);
        }

        [Fact]
        public void BorrowTime_WithFutureValue_ShouldThrowException()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            var futureTime = DateTime.Now.AddMinutes(10);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => licenseInfo.BorrowTime = futureTime);
        }

        [Fact]
        public void UsageDuration_WithValidValue_ShouldSetCorrectly()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            var validDuration = TimeSpan.FromMinutes(30);

            // Act
            licenseInfo.UsageDuration = validDuration;

            // Assert
            Assert.Equal(validDuration, licenseInfo.UsageDuration);
        }

        [Fact]
        public void UsageDuration_WithNegativeValue_ShouldThrowException()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            var negativeDuration = TimeSpan.FromMinutes(-1);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => licenseInfo.UsageDuration = negativeDuration);
        }

        [Fact]
        public void CreateActive_WithValidParameters_ShouldCreateActiveLicense()
        {
            // Arrange
            var userHost = "test-user";
            var feature = "solidworks";
            var clientHost = "client-host";
            var licenseVersion = "2024";

            // Act
            var licenseInfo = LicenseInfo.CreateActive(userHost, feature, clientHost, licenseVersion);

            // Assert
            Assert.Equal(userHost, licenseInfo.UserHost);
            Assert.Equal(feature, licenseInfo.Feature);
            Assert.Equal(clientHost, licenseInfo.ClientHost);
            Assert.Equal(licenseVersion, licenseInfo.LicenseVersion);
            Assert.Equal(LicenseStatus.Active, licenseInfo.Status);
            Assert.False(licenseInfo.IsIdle);
            Assert.True(licenseInfo.IsActive);
            Assert.False(licenseInfo.IsBorrowed);
            Assert.False(licenseInfo.IsExpired);
        }

        [Fact]
        public void CreateIdle_WithValidParameters_ShouldCreateIdleLicense()
        {
            // Arrange
            var userHost = "test-user";
            var feature = "solidworks";
            var idleReason = "User away from desk";
            var clientHost = "client-host";

            // Act
            var licenseInfo = LicenseInfo.CreateIdle(userHost, feature, idleReason, clientHost);

            // Assert
            Assert.Equal(userHost, licenseInfo.UserHost);
            Assert.Equal(feature, licenseInfo.Feature);
            Assert.Equal(clientHost, licenseInfo.ClientHost);
            Assert.Equal(LicenseStatus.Idle, licenseInfo.Status);
            Assert.True(licenseInfo.IsIdle);
            Assert.Equal(idleReason, licenseInfo.IdleReason);
            Assert.False(licenseInfo.IsActive);
            Assert.False(licenseInfo.IsBorrowed);
            Assert.False(licenseInfo.IsExpired);
        }

        [Fact]
        public void CreateBorrowed_WithValidParameters_ShouldCreateBorrowedLicense()
        {
            // Arrange
            var userHost = "test-user";
            var feature = "solidworks";
            var borrowTime = DateTime.Now.AddHours(-2);
            var clientHost = "client-host";

            // Act
            var licenseInfo = LicenseInfo.CreateBorrowed(userHost, feature, borrowTime, clientHost);

            // Assert
            Assert.Equal(userHost, licenseInfo.UserHost);
            Assert.Equal(feature, licenseInfo.Feature);
            Assert.Equal(clientHost, licenseInfo.ClientHost);
            Assert.Equal(borrowTime, licenseInfo.BorrowTime);
            Assert.Equal(LicenseStatus.Borrowed, licenseInfo.Status);
            Assert.False(licenseInfo.IsIdle);
            Assert.False(licenseInfo.IsActive);
            Assert.True(licenseInfo.IsBorrowed);
            Assert.False(licenseInfo.IsExpired);
        }

        [Fact]
        public void UpdateUsageDuration_ShouldCalculateCorrectly()
        {
            // Arrange
            var borrowTime = DateTime.Now.AddMinutes(-10);
            var licenseInfo = new LicenseInfo("user", "feature", borrowTime, LicenseStatus.Active);

            // Act
            licenseInfo.UpdateUsageDuration();

            // Assert
            Assert.True(licenseInfo.UsageDuration.TotalMinutes >= 9 && licenseInfo.UsageDuration.TotalMinutes <= 11);
        }

        [Fact]
        public void MarkAsIdle_ShouldUpdateStatusCorrectly()
        {
            // Arrange
            var licenseInfo = LicenseInfo.CreateActive("user", "feature");

            // Act
            licenseInfo.MarkAsIdle("Testing idle state");

            // Assert
            Assert.True(licenseInfo.IsIdle);
            Assert.Equal(LicenseStatus.Idle, licenseInfo.Status);
            Assert.Equal("Testing idle state", licenseInfo.IdleReason);
        }

        [Fact]
        public void MarkAsActive_ShouldUpdateStatusCorrectly()
        {
            // Arrange
            var licenseInfo = LicenseInfo.CreateIdle("user", "feature", "Testing");

            // Act
            licenseInfo.MarkAsActive();

            // Assert
            Assert.False(licenseInfo.IsIdle);
            Assert.Equal(LicenseStatus.Active, licenseInfo.Status);
            Assert.Empty(licenseInfo.IdleReason);
        }

        [Fact]
        public void TotalHoldTime_ShouldCalculateCorrectly()
        {
            // Arrange
            var borrowTime = DateTime.Now.AddMinutes(-30);
            var licenseInfo = new LicenseInfo("user", "feature", borrowTime, LicenseStatus.Active);

            // Act
            var holdTime = licenseInfo.TotalHoldTime;

            // Assert
            Assert.True(holdTime.TotalMinutes >= 29 && holdTime.TotalMinutes <= 31);
        }

        [Fact]
        public void IdlePercentage_ShouldCalculateCorrectly()
        {
            // Arrange
            var borrowTime = DateTime.Now.AddMinutes(-10);
            var licenseInfo = new LicenseInfo("user", "feature", borrowTime, LicenseStatus.Active);
            licenseInfo.UsageDuration = TimeSpan.FromMinutes(3);

            // Act
            var idlePercentage = licenseInfo.IdlePercentage;

            // Assert
            Assert.Equal(30.0, idlePercentage); // 3 minutes out of 10 minutes
        }

        [Fact]
        public void IdlePercentage_WithZeroHoldTime_ShouldReturnZero()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            licenseInfo.BorrowTime = DateTime.Now;
            licenseInfo.UsageDuration = TimeSpan.Zero;

            // Act
            var idlePercentage = licenseInfo.IdlePercentage;

            // Assert
            Assert.Equal(0, idlePercentage);
        }

        [Fact]
        public void Validate_WithValidLicenseInfo_ShouldReturnEmptyList()
        {
            // Arrange
            var licenseInfo = LicenseInfo.CreateActive("user", "feature");

            // Act
            var errors = licenseInfo.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_WithInvalidLicenseInfo_ShouldReturnErrors()
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            licenseInfo.UserHost = ""; // Invalid
            licenseInfo.Feature = ""; // Invalid
            licenseInfo.BorrowTime = default; // Invalid
            licenseInfo.UsageDuration = TimeSpan.FromMinutes(-1); // Invalid

            // Act
            var errors = licenseInfo.Validate();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("User host is required", errors);
            Assert.Contains("Feature name is required", errors);
            Assert.Contains("Borrow time is required", errors);
            Assert.Contains("Usage duration cannot be negative", errors);
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var borrowTime = DateTime.Now.AddMinutes(-30);
            var licenseInfo = new LicenseInfo("user-host", "solidworks", borrowTime, LicenseStatus.Active)
            {
                ClientHost = "client-host",
                LicenseVersion = "2024",
                IsIdle = false,
                UsageDuration = TimeSpan.FromMinutes(30)
            };

            // Act
            var result = licenseInfo.ToString();

            // Assert
            Assert.Contains("user-host", result);
            Assert.Contains("solidworks", result);
            Assert.Contains("Active", result);
            Assert.Contains("client-host", result);
            Assert.Contains("2024", result);
            Assert.Contains("30.0min", result);
        }

        [Theory]
        [InlineData(LicenseStatus.Active, true, false, false)]
        [InlineData(LicenseStatus.Idle, false, false, false)]
        [InlineData(LicenseStatus.Borrowed, false, true, false)]
        [InlineData(LicenseStatus.Expired, false, false, true)]
        [InlineData(LicenseStatus.Unknown, false, false, false)]
        public void StatusProperties_ShouldReflectCorrectStatus(LicenseStatus status, bool expectedActive, bool expectedBorrowed, bool expectedExpired)
        {
            // Arrange
            var licenseInfo = new LicenseInfo();
            licenseInfo.Status = status;

            // Act & Assert
            Assert.Equal(expectedActive, licenseInfo.IsActive);
            Assert.Equal(expectedBorrowed, licenseInfo.IsBorrowed);
            Assert.Equal(expectedExpired, licenseInfo.IsExpired);
        }
    }
}