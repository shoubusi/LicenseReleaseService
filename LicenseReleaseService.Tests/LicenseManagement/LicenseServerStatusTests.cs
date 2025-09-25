using System;
using System.Collections.Generic;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for LicenseServerStatus class
    /// </summary>
    public class LicenseServerStatusTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var status = new LicenseServerStatus();

            // Assert
            Assert.NotNull(status.Features);
            Assert.Empty(status.Features);
            Assert.NotNull(status.FeatureDetails);
            Assert.Empty(status.FeatureDetails);
            Assert.NotNull(status.ServerMessages);
            Assert.Empty(status.ServerMessages);
            Assert.Empty(status.ServerName);
            Assert.Equal(0, status.TotalLicenses);
            Assert.Equal(0, status.LicensesInUse);
            Assert.Equal(0, status.AvailableLicenses);
            Assert.False(status.IsServerUp);
            Assert.False(status.IsHealthy);
            Assert.NotEqual(default, status.LastChecked);
            Assert.Equal(0, status.ResponseTimeMs);
            Assert.Equal(0, status.UtilizationPercentage);
            Assert.Equal(0, status.AvailabilityPercentage);
            Assert.False(status.IsAvailable);
            Assert.Equal(0, status.TotalUsers);
            Assert.Equal(0, status.TotalActiveUsers);
            Assert.Equal(0, status.TotalIdleUsers);
            Assert.Equal(0, status.TotalBorrowedUsers);
        }

        [Fact]
        public void CreateSuccess_WithValidParameters_ShouldCreateSuccessfulStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var responseTime = 150L;

            // Act
            var status = LicenseServerStatus.CreateSuccess(server, port, responseTime);

            // Assert
            Assert.Equal(server, status.Server);
            Assert.Equal(port, status.Port);
            Assert.True(status.IsServerUp);
            Assert.True(status.IsHealthy);
            Assert.True(status.IsAvailable);
            Assert.Equal(responseTime, status.ResponseTimeMs);
            Assert.Equal("License server is running normally", status.StatusMessage);
            Assert.Empty(status.ErrorMessage);
        }

        [Fact]
        public void CreateFailure_WithValidParameters_ShouldCreateFailedStatus()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var errorMessage = "Connection refused";

            // Act
            var status = LicenseServerStatus.CreateFailure(server, port, errorMessage);

            // Assert
            Assert.Equal(server, status.Server);
            Assert.Equal(port, status.Port);
            Assert.False(status.IsServerUp);
            Assert.False(status.IsHealthy);
            Assert.False(status.IsAvailable);
            Assert.Equal(errorMessage, status.ErrorMessage);
            Assert.Equal("License server is not responding", status.StatusMessage);
        }

        [Fact]
        public void AddOrUpdateFeature_WithNewFeature_ShouldAddFeature()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var featureName = "test-feature";
            var featureStatus = new LicenseFeatureStatus
            {
                FeatureName = featureName,
                TotalLicenses = 10,
                LicensesInUse = 3,
                AvailableLicenses = 7
            };

            // Act
            status.AddOrUpdateFeature(featureName, featureStatus);

            // Assert
            Assert.Contains(featureName, status.Features.Keys);
            Assert.Equal(10, status.TotalLicenses);
            Assert.Equal(3, status.LicensesInUse);
            Assert.Equal(7, status.AvailableLicenses);
        }

        [Fact]
        public void AddOrUpdateFeature_WithExistingFeature_ShouldUpdateFeature()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var featureName = "test-feature";
            var initialFeature = new LicenseFeatureStatus
            {
                FeatureName = featureName,
                TotalLicenses = 10,
                LicensesInUse = 3,
                AvailableLicenses = 7
            };
            var updatedFeature = new LicenseFeatureStatus
            {
                FeatureName = featureName,
                TotalLicenses = 15,
                LicensesInUse = 5,
                AvailableLicenses = 10
            };

            // Act
            status.AddOrUpdateFeature(featureName, initialFeature);
            status.AddOrUpdateFeature(featureName, updatedFeature);

            // Assert
            Assert.Contains(featureName, status.Features.Keys);
            Assert.Equal(15, status.TotalLicenses);
            Assert.Equal(5, status.LicensesInUse);
            Assert.Equal(10, status.AvailableLicenses);
        }

        [Fact]
        public void RemoveFeature_WithExistingFeature_ShouldRemoveFeature()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var featureName = "test-feature";
            var featureStatus = new LicenseFeatureStatus
            {
                FeatureName = featureName,
                TotalLicenses = 10,
                LicensesInUse = 3,
                AvailableLicenses = 7
            };

            status.AddOrUpdateFeature(featureName, featureStatus);

            // Act
            var result = status.RemoveFeature(featureName);

            // Assert
            Assert.True(result);
            Assert.DoesNotContain(featureName, status.Features.Keys);
            Assert.Equal(0, status.TotalLicenses);
            Assert.Equal(0, status.LicensesInUse);
            Assert.Equal(0, status.AvailableLicenses);
        }

        [Fact]
        public void RemoveFeature_WithNonExistingFeature_ShouldReturnFalse()
        {
            // Arrange
            var status = new LicenseServerStatus();

            // Act
            var result = status.RemoveFeature("non-existing-feature");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void UtilizationPercentage_WithNoLicenses_ShouldReturnZero()
        {
            // Arrange
            var status = new LicenseServerStatus { TotalLicenses = 0, LicensesInUse = 0 };

            // Act & Assert
            Assert.Equal(0, status.UtilizationPercentage);
        }

        [Fact]
        public void UtilizationPercentage_WithLicensesInUse_ShouldCalculateCorrectly()
        {
            // Arrange
            var status = new LicenseServerStatus { TotalLicenses = 10, LicensesInUse = 3 };

            // Act & Assert
            Assert.Equal(30.0, status.UtilizationPercentage);
        }

        [Fact]
        public void AvailabilityPercentage_WithNoLicenses_ShouldReturnZero()
        {
            // Arrange
            var status = new LicenseServerStatus { TotalLicenses = 0, AvailableLicenses = 0 };

            // Act & Assert
            Assert.Equal(0, status.AvailabilityPercentage);
        }

        [Fact]
        public void AvailabilityPercentage_WithAvailableLicenses_ShouldCalculateCorrectly()
        {
            // Arrange
            var status = new LicenseServerStatus { TotalLicenses = 10, AvailableLicenses = 7 };

            // Act & Assert
            Assert.Equal(70.0, status.AvailabilityPercentage);
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var status = new LicenseServerStatus
            {
                Server = "test-server",
                Port = 27000,
                IsServerUp = true,
                IsHealthy = true,
                TotalLicenses = 10,
                LicensesInUse = 3,
                AvailableLicenses = 7
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("test-server:27000", result);
            Assert.Contains("Up=True", result);
            Assert.Contains("Healthy=True", result);
            Assert.Contains("Total=10", result);
            Assert.Contains("InUse=3", result);
            Assert.Contains("Available=7", result);
            Assert.Contains("30.0%", result); // Utilization percentage
        }

        [Fact]
        public void IsAvailable_WithServerUpAndHealthy_ShouldReturnTrue()
        {
            // Arrange
            var status = new LicenseServerStatus
            {
                IsServerUp = true,
                IsHealthy = true,
                ErrorMessage = ""
            };

            // Act & Assert
            Assert.True(status.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithServerDown_ShouldReturnFalse()
        {
            // Arrange
            var status = new LicenseServerStatus
            {
                IsServerUp = false,
                IsHealthy = true,
                ErrorMessage = ""
            };

            // Act & Assert
            Assert.False(status.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithErrorMessage_ShouldReturnFalse()
        {
            // Arrange
            var status = new LicenseServerStatus
            {
                IsServerUp = true,
                IsHealthy = true,
                ErrorMessage = "Connection failed"
            };

            // Act & Assert
            Assert.False(status.IsAvailable);
        }

        [Fact]
        public void AddOrUpdateFeatureDetail_WithNewFeature_ShouldAddFeature()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var featureName = "test-feature";
            var featureDetail = new LicenseFeature(featureName, 10, 3, 7);

            // Act
            status.AddOrUpdateFeatureDetail(featureName, featureDetail);

            // Assert
            Assert.Contains(featureName, status.FeatureDetails.Keys);
            Assert.Equal(10, status.TotalLicenses);
            Assert.Equal(3, status.LicensesInUse);
            Assert.Equal(7, status.AvailableLicenses);
        }

        [Fact]
        public void AddOrUpdateFeatureDetail_WithExistingFeature_ShouldUpdateFeature()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var featureName = "test-feature";
            var initialFeature = new LicenseFeature(featureName, 10, 3, 7);
            var updatedFeature = new LicenseFeature(featureName, 15, 5, 10);

            status.AddOrUpdateFeatureDetail(featureName, initialFeature);

            // Act
            status.AddOrUpdateFeatureDetail(featureName, updatedFeature);

            // Assert
            Assert.Contains(featureName, status.FeatureDetails.Keys);
            Assert.Equal(15, status.TotalLicenses);
            Assert.Equal(5, status.LicensesInUse);
            Assert.Equal(10, status.AvailableLicenses);
        }

        [Fact]
        public void RemoveFeatureDetail_WithExistingFeature_ShouldRemoveFeature()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var featureName = "test-feature";
            var featureDetail = new LicenseFeature(featureName, 10, 3, 7);

            status.AddOrUpdateFeatureDetail(featureName, featureDetail);

            // Act
            var result = status.RemoveFeatureDetail(featureName);

            // Assert
            Assert.True(result);
            Assert.DoesNotContain(featureName, status.FeatureDetails.Keys);
            Assert.Equal(0, status.TotalLicenses);
            Assert.Equal(0, status.LicensesInUse);
            Assert.Equal(0, status.AvailableLicenses);
        }

        [Fact]
        public void RemoveFeatureDetail_WithNonExistingFeature_ShouldReturnFalse()
        {
            // Arrange
            var status = new LicenseServerStatus();

            // Act
            var result = status.RemoveFeatureDetail("non-existing-feature");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddServerMessage_WithValidMessage_ShouldAddMessage()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var message = "Server started successfully";

            // Act
            status.AddServerMessage(message);

            // Assert
            Assert.Single(status.ServerMessages);
            Assert.Contains(message, status.ServerMessages);
        }

        [Fact]
        public void AddServerMessage_WithEmptyMessage_ShouldNotAddMessage()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var message = "";

            // Act
            status.AddServerMessage(message);

            // Assert
            Assert.Empty(status.ServerMessages);
        }

        [Fact]
        public void AddServerMessage_WithWhiteSpaceMessage_ShouldNotAddMessage()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var message = "   ";

            // Act
            status.AddServerMessage(message);

            // Assert
            Assert.Empty(status.ServerMessages);
        }

        [Fact]
        public void ClearServerMessages_ShouldRemoveAllMessages()
        {
            // Arrange
            var status = new LicenseServerStatus();
            status.AddServerMessage("Message 1");
            status.AddServerMessage("Message 2");
            status.AddServerMessage("Message 3");

            // Act
            status.ClearServerMessages();

            // Assert
            Assert.Empty(status.ServerMessages);
        }

        [Fact]
        public void TotalUsers_ShouldCalculateCorrectly()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var feature1 = new LicenseFeature("feature1", 10, 2, 8);
            var feature2 = new LicenseFeature("feature2", 5, 1, 4);

            feature1.AddActiveUser(LicenseInfo.CreateActive("user1", "feature1"));
            feature1.AddActiveUser(LicenseInfo.CreateActive("user2", "feature1"));
            feature2.AddActiveUser(LicenseInfo.CreateActive("user3", "feature2"));

            status.AddOrUpdateFeatureDetail("feature1", feature1);
            status.AddOrUpdateFeatureDetail("feature2", feature2);

            // Act & Assert
            Assert.Equal(3, status.TotalUsers);
        }

        [Fact]
        public void TotalActiveUsers_ShouldCalculateCorrectly()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var feature1 = new LicenseFeature("feature1", 10, 2, 8);
            var feature2 = new LicenseFeature("feature2", 5, 1, 4);

            feature1.AddActiveUser(LicenseInfo.CreateActive("user1", "feature1"));
            feature1.AddIdleUser(LicenseInfo.CreateIdle("user2", "feature1", "Idle"));
            feature2.AddActiveUser(LicenseInfo.CreateActive("user3", "feature2"));

            status.AddOrUpdateFeatureDetail("feature1", feature1);
            status.AddOrUpdateFeatureDetail("feature2", feature2);

            // Act & Assert
            Assert.Equal(2, status.TotalActiveUsers);
        }

        [Fact]
        public void TotalIdleUsers_ShouldCalculateCorrectly()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var feature1 = new LicenseFeature("feature1", 10, 2, 8);
            var feature2 = new LicenseFeature("feature2", 5, 1, 4);

            feature1.AddIdleUser(LicenseInfo.CreateIdle("user1", "feature1", "Idle"));
            feature1.AddIdleUser(LicenseInfo.CreateIdle("user2", "feature1", "Idle"));
            feature2.AddActiveUser(LicenseInfo.CreateActive("user3", "feature2"));

            status.AddOrUpdateFeatureDetail("feature1", feature1);
            status.AddOrUpdateFeatureDetail("feature2", feature2);

            // Act & Assert
            Assert.Equal(2, status.TotalIdleUsers);
        }

        [Fact]
        public void TotalBorrowedUsers_ShouldCalculateCorrectly()
        {
            // Arrange
            var status = new LicenseServerStatus();
            var feature1 = new LicenseFeature("feature1", 10, 2, 8);
            var feature2 = new LicenseFeature("feature2", 5, 1, 4);

            feature1.AddBorrowedUser(LicenseInfo.CreateBorrowed("user1", "feature1", DateTime.Now.AddHours(-1)));
            feature1.AddBorrowedUser(LicenseInfo.CreateBorrowed("user2", "feature1", DateTime.Now.AddHours(-2)));
            feature2.AddActiveUser(LicenseInfo.CreateActive("user3", "feature2"));

            status.AddOrUpdateFeatureDetail("feature1", feature1);
            status.AddOrUpdateFeatureDetail("feature2", feature2);

            // Act & Assert
            Assert.Equal(2, status.TotalBorrowedUsers);
        }

        [Fact]
        public void TotalUsers_WithNoFeatures_ShouldReturnZero()
        {
            // Arrange
            var status = new LicenseServerStatus();

            // Act & Assert
            Assert.Equal(0, status.TotalUsers);
            Assert.Equal(0, status.TotalActiveUsers);
            Assert.Equal(0, status.TotalIdleUsers);
            Assert.Equal(0, status.TotalBorrowedUsers);
        }
    }

    /// <summary>
    /// Unit tests for LicenseFeatureStatus class
    /// </summary>
    public class LicenseFeatureStatusTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var featureStatus = new LicenseFeatureStatus();

            // Assert
            Assert.NotNull(featureStatus.Users);
            Assert.Empty(featureStatus.Users);
            Assert.Equal(0, featureStatus.TotalLicenses);
            Assert.Equal(0, featureStatus.LicensesInUse);
            Assert.Equal(0, featureStatus.AvailableLicenses);
            Assert.Equal(0, featureStatus.UtilizationPercentage);
            Assert.False(featureStatus.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithActiveFeatureAndAvailableLicenses_ShouldReturnTrue()
        {
            // Arrange
            var featureStatus = new LicenseFeatureStatus
            {
                Status = "ACTIVE",
                AvailableLicenses = 5,
                ExpirationDate = DateTime.Now.AddDays(1)
            };

            // Act & Assert
            Assert.True(featureStatus.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithInactiveFeature_ShouldReturnFalse()
        {
            // Arrange
            var featureStatus = new LicenseFeatureStatus
            {
                Status = "INACTIVE",
                AvailableLicenses = 5,
                ExpirationDate = DateTime.Now.AddDays(1)
            };

            // Act & Assert
            Assert.False(featureStatus.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithNoAvailableLicenses_ShouldReturnFalse()
        {
            // Arrange
            var featureStatus = new LicenseFeatureStatus
            {
                Status = "ACTIVE",
                AvailableLicenses = 0,
                ExpirationDate = DateTime.Now.AddDays(1)
            };

            // Act & Assert
            Assert.False(featureStatus.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithExpiredFeature_ShouldReturnFalse()
        {
            // Arrange
            var featureStatus = new LicenseFeatureStatus
            {
                Status = "ACTIVE",
                AvailableLicenses = 5,
                ExpirationDate = DateTime.Now.AddDays(-1)
            };

            // Act & Assert
            Assert.False(featureStatus.IsAvailable);
        }

        [Fact]
        public void UtilizationPercentage_WithNoLicenses_ShouldReturnZero()
        {
            // Arrange
            var featureStatus = new LicenseFeatureStatus { TotalLicenses = 0, LicensesInUse = 0 };

            // Act & Assert
            Assert.Equal(0, featureStatus.UtilizationPercentage);
        }

        [Fact]
        public void UtilizationPercentage_WithLicensesInUse_ShouldCalculateCorrectly()
        {
            // Arrange
            var featureStatus = new LicenseFeatureStatus { TotalLicenses = 10, LicensesInUse = 3 };

            // Act & Assert
            Assert.Equal(30.0, featureStatus.UtilizationPercentage);
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var featureStatus = new LicenseFeatureStatus
            {
                FeatureName = "test-feature",
                Version = "1.0",
                TotalLicenses = 10,
                LicensesInUse = 3,
                AvailableLicenses = 7,
                Status = "ACTIVE"
            };

            // Act
            var result = featureStatus.ToString();

            // Assert
            Assert.Contains("test-feature", result);
            Assert.Contains("Version=1.0", result);
            Assert.Contains("Total=10", result);
            Assert.Contains("InUse=3", result);
            Assert.Contains("Available=7", result);
            Assert.Contains("Status=ACTIVE", result);
            Assert.Contains("30.0%", result); // Utilization percentage
        }
    }

    /// <summary>
    /// Unit tests for LicenseUserUsage class
    /// </summary>
    public class LicenseUserUsageTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var userUsage = new LicenseUserUsage();

            // Assert
            Assert.NotEmpty(userUsage.UserName);
            Assert.NotEmpty(userUsage.HostName);
            Assert.Equal(0, userUsage.LicensesCheckedOut);
            Assert.Equal(0, userUsage.ProcessId);
            Assert.NotEqual(default, userUsage.CheckoutTime);
        }

        [Fact]
        public void CheckoutDuration_ShouldCalculateCorrectly()
        {
            // Arrange
            var checkoutTime = DateTime.Now.AddMinutes(-30);
            var userUsage = new LicenseUserUsage { CheckoutTime = checkoutTime };

            // Act
            var duration = userUsage.CheckoutDuration;

            // Assert
            Assert.True(duration.TotalMinutes >= 29 && duration.TotalMinutes <= 31); // Allow some tolerance
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var userUsage = new LicenseUserUsage
            {
                UserName = "testuser",
                HostName = "test-host",
                LicensesCheckedOut = 2,
                ProcessName = "test-app",
                ProcessId = 1234
            };

            // Act
            var result = userUsage.ToString();

            // Assert
            Assert.Contains("testuser", result);
            Assert.Contains("test-host", result);
            Assert.Contains("Licenses=2", result);
            Assert.Contains("test-app", result);
            Assert.Contains("1234", result);
        }
    }
}