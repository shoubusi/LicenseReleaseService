using System;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for LicenseReleaseResult class
    /// </summary>
    public class LicenseReleaseResultTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var result = new LicenseReleaseResult();

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.ErrorMessage);
            Assert.NotEmpty(result.Server);
            Assert.Equal(0, result.Port);
            Assert.NotEmpty(result.Feature);
            Assert.NotEmpty(result.User);
            Assert.Equal(TimeSpan.Zero, result.ExecutionTime);
            Assert.NotEqual(default, result.Timestamp);
            Assert.Equal(LicenseReleaseResultCode.UnknownError, result.ResultCode);
            Assert.Equal(0, result.LicensesReleased);
            Assert.Equal(0, result.RetryAttempt);
            Assert.Equal(0, result.ProcessId);
            Assert.NotEmpty(result.Command);
            Assert.NotEmpty(result.CommandOutput);
            Assert.NotEmpty(result.CommandError);
            Assert.Equal(0, result.ExitCode);
            Assert.False(result.TimedOut);
            Assert.False(result.Cancelled);
            Assert.False(result.WasRetried);
        }

        [Fact]
        public void CreateSuccess_WithValidParameters_ShouldCreateSuccessfulResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";
            var user = "test-user";
            var licensesReleased = 2;
            var executionTime = TimeSpan.FromMilliseconds(500);

            // Act
            var result = LicenseReleaseResult.CreateSuccess(server, port, feature, user, licensesReleased, executionTime);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.Equal(feature, result.Feature);
            Assert.Equal(user, result.User);
            Assert.Equal(licensesReleased, result.LicensesReleased);
            Assert.Equal(executionTime, result.ExecutionTime);
            Assert.Equal(LicenseReleaseResultCode.Success, result.ResultCode);
            Assert.Empty(result.ErrorMessage);
            Assert.Equal(0, result.RetryAttempt);
            Assert.False(result.TimedOut);
            Assert.False(result.Cancelled);
        }

        [Fact]
        public void CreateFailure_WithValidParameters_ShouldCreateFailedResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";
            var user = "test-user";
            var errorMessage = "License not found";
            var resultCode = LicenseReleaseResultCode.FeatureNotFound;
            var executionTime = TimeSpan.FromMilliseconds(200);

            // Act
            var result = LicenseReleaseResult.CreateFailure(server, port, feature, user, errorMessage, resultCode, executionTime);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.Equal(feature, result.Feature);
            Assert.Equal(user, result.User);
            Assert.Equal(errorMessage, result.ErrorMessage);
            Assert.Equal(resultCode, result.ResultCode);
            Assert.Equal(executionTime, result.ExecutionTime);
            Assert.Equal(0, result.LicensesReleased);
            Assert.Equal(0, result.RetryAttempt);
            Assert.False(result.TimedOut);
            Assert.False(result.Cancelled);
        }

        [Fact]
        public void CreateTimeout_WithValidParameters_ShouldCreateTimeoutResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";
            var user = "test-user";
            var executionTime = TimeSpan.FromSeconds(30);

            // Act
            var result = LicenseReleaseResult.CreateTimeout(server, port, feature, user, executionTime);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.Equal(feature, result.Feature);
            Assert.Equal(user, result.User);
            Assert.Equal("License release operation timed out", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.Timeout, result.ResultCode);
            Assert.Equal(executionTime, result.ExecutionTime);
            Assert.True(result.TimedOut);
            Assert.False(result.Cancelled);
        }

        [Fact]
        public void CreateCancelled_WithValidParameters_ShouldCreateCancelledResult()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";
            var user = "test-user";
            var executionTime = TimeSpan.FromMilliseconds(100);

            // Act
            var result = LicenseReleaseResult.CreateCancelled(server, port, feature, user, executionTime);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.Equal(feature, result.Feature);
            Assert.Equal(user, result.User);
            Assert.Equal("License release operation was cancelled", result.ErrorMessage);
            Assert.Equal(LicenseReleaseResultCode.Cancelled, result.ResultCode);
            Assert.Equal(executionTime, result.ExecutionTime);
            Assert.False(result.TimedOut);
            Assert.True(result.Cancelled);
        }

        [Fact]
        public void WasRetried_WithZeroRetryAttempts_ShouldReturnFalse()
        {
            // Arrange
            var result = new LicenseReleaseResult { RetryAttempt = 0 };

            // Act & Assert
            Assert.False(result.WasRetried);
        }

        [Fact]
        public void WasRetried_WithPositiveRetryAttempts_ShouldReturnTrue()
        {
            // Arrange
            var result = new LicenseReleaseResult { RetryAttempt = 2 };

            // Act & Assert
            Assert.True(result.WasRetried);
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var result = new LicenseReleaseResult
            {
                Success = true,
                Feature = "test-feature",
                User = "test-user",
                Server = "test-server",
                Port = 27000,
                ResultCode = LicenseReleaseResultCode.Success,
                ExecutionTime = TimeSpan.FromMilliseconds(500),
                RetryAttempt = 1,
                LicensesReleased = 2
            };

            // Act
            var stringResult = result.ToString();

            // Assert
            Assert.Contains("Success=True", stringResult);
            Assert.Contains("Feature=test-feature", stringResult);
            Assert.Contains("User=test-user", stringResult);
            Assert.Contains("test-server:27000", stringResult);
            Assert.Contains("Code=Success", stringResult);
            Assert.Contains("500.00ms", stringResult);
            Assert.Contains("Retries=1", stringResult);
            Assert.Contains("Released=2", stringResult);
        }

        [Fact]
        public void ToString_WithFailure_ShouldIncludeFailureInformation()
        {
            // Arrange
            var result = new LicenseReleaseResult
            {
                Success = false,
                Feature = "test-feature",
                User = "test-user",
                Server = "test-server",
                Port = 27000,
                ResultCode = LicenseReleaseResultCode.FeatureNotFound,
                ExecutionTime = TimeSpan.FromMilliseconds(200),
                ErrorMessage = "Feature not found"
            };

            // Act
            var stringResult = result.ToString();

            // Assert
            Assert.Contains("Success=False", stringResult);
            Assert.Contains("Code=FeatureNotFound", stringResult);
            Assert.Contains("200.00ms", stringResult);
        }
    }

    /// <summary>
    /// Unit tests for LicenseFeatureInfo class
    /// </summary>
    public class LicenseFeatureInfoTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var featureInfo = new LicenseFeatureInfo();

            // Assert
            Assert.NotEmpty(featureInfo.FeatureName);
            Assert.NotEmpty(featureInfo.Version);
            Assert.NotEmpty(featureInfo.Description);
            Assert.NotEmpty(featureInfo.Vendor);
            Assert.Equal(0, featureInfo.TotalLicenses);
            Assert.Equal(0, featureInfo.LicensesInUse);
            Assert.Equal(0, featureInfo.AvailableLicenses);
            Assert.Equal(0, featureInfo.UtilizationPercentage);
            Assert.False(featureInfo.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithActiveFeatureAndAvailableLicenses_ShouldReturnTrue()
        {
            // Arrange
            var featureInfo = new LicenseFeatureInfo
            {
                Status = "ACTIVE",
                AvailableLicenses = 5,
                ExpirationDate = DateTime.Now.AddDays(1)
            };

            // Act & Assert
            Assert.True(featureInfo.IsAvailable);
        }

        [Theory]
        [InlineData("INACTIVE")]
        [InlineData("EXPIRED")]
        [InlineData("DISABLED")]
        public void IsAvailable_WithInactiveStatus_ShouldReturnFalse(string status)
        {
            // Arrange
            var featureInfo = new LicenseFeatureInfo
            {
                Status = status,
                AvailableLicenses = 5,
                ExpirationDate = DateTime.Now.AddDays(1)
            };

            // Act & Assert
            Assert.False(featureInfo.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithNoAvailableLicenses_ShouldReturnFalse()
        {
            // Arrange
            var featureInfo = new LicenseFeatureInfo
            {
                Status = "ACTIVE",
                AvailableLicenses = 0,
                ExpirationDate = DateTime.Now.AddDays(1)
            };

            // Act & Assert
            Assert.False(featureInfo.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithExpiredFeature_ShouldReturnFalse()
        {
            // Arrange
            var featureInfo = new LicenseFeatureInfo
            {
                Status = "ACTIVE",
                AvailableLicenses = 5,
                ExpirationDate = DateTime.Now.AddDays(-1)
            };

            // Act & Assert
            Assert.False(featureInfo.IsAvailable);
        }

        [Fact]
        public void UtilizationPercentage_WithNoLicenses_ShouldReturnZero()
        {
            // Arrange
            var featureInfo = new LicenseFeatureInfo { TotalLicenses = 0, LicensesInUse = 0 };

            // Act & Assert
            Assert.Equal(0, featureInfo.UtilizationPercentage);
        }

        [Theory]
        [InlineData(10, 3, 30.0)]
        [InlineData(10, 5, 50.0)]
        [InlineData(10, 10, 100.0)]
        [InlineData(5, 0, 0.0)]
        public void UtilizationPercentage_WithValidInputs_ShouldCalculateCorrectly(int total, int inUse, double expected)
        {
            // Arrange
            var featureInfo = new LicenseFeatureInfo { TotalLicenses = total, LicensesInUse = inUse };

            // Act & Assert
            Assert.Equal(expected, featureInfo.UtilizationPercentage);
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var featureInfo = new LicenseFeatureInfo
            {
                FeatureName = "test-feature",
                Version = "1.0",
                TotalLicenses = 10,
                LicensesInUse = 3,
                AvailableLicenses = 7,
                Status = "ACTIVE",
                LicenseType = "FLOATING"
            };

            // Act
            var result = featureInfo.ToString();

            // Assert
            Assert.Contains("test-feature", result);
            Assert.Contains("Version=1.0", result);
            Assert.Contains("Total=10", result);
            Assert.Contains("InUse=3", result);
            Assert.Contains("Available=7", result);
            Assert.Contains("Status=ACTIVE", result);
            Assert.Contains("Type=FLOATING", result);
        }
    }

    /// <summary>
    /// Unit tests for LicenseUsageStatistics class
    /// </summary>
    public class LicenseUsageStatisticsTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var stats = new LicenseUsageStatistics();

            // Assert
            Assert.NotEmpty(stats.Server);
            Assert.Equal(0, stats.Port);
            Assert.Equal(0, stats.TotalLicenses);
            Assert.Equal(0, stats.TotalLicensesInUse);
            Assert.Equal(0, stats.TotalAvailableLicenses);
            Assert.Equal(0, stats.UniqueUsers);
            Assert.Equal(0, stats.ActiveFeatures);
            Assert.Equal(0, stats.OverallUtilization);
            Assert.NotEqual(default, stats.Timestamp);
            Assert.Equal(0, stats.PeakUsage);
            Assert.Equal(0, stats.AverageUsage24h);
        }

        [Theory]
        [InlineData(0, 0, 0.0)]
        [InlineData(10, 3, 30.0)]
        [InlineData(10, 5, 50.0)]
        [InlineData(10, 10, 100.0)]
        public void OverallUtilization_WithValidInputs_ShouldCalculateCorrectly(int total, int inUse, double expected)
        {
            // Arrange
            var stats = new LicenseUsageStatistics
            {
                TotalLicenses = total,
                TotalLicensesInUse = inUse
            };

            // Act & Assert
            Assert.Equal(expected, stats.OverallUtilization);
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var stats = new LicenseUsageStatistics
            {
                Server = "test-server",
                Port = 27000,
                TotalLicenses = 50,
                TotalLicensesInUse = 15,
                TotalAvailableLicenses = 35,
                UniqueUsers = 8,
                ActiveFeatures = 5,
                OverallUtilization = 30.0
            };

            // Act
            var result = stats.ToString();

            // Assert
            Assert.Contains("test-server:27000", result);
            Assert.Contains("Total=50", result);
            Assert.Contains("InUse=15", result);
            Assert.Contains("Available=35", result);
            Assert.Contains("Users=8", result);
            Assert.Contains("Features=5", result);
            Assert.Contains("30.0%", result);
        }
    }

    /// <summary>
    /// Unit tests for LicenseReleaseResultCode enum
    /// </summary>
    public class LicenseReleaseResultCodeTests
    {
        [Fact]
        public void Values_ShouldHaveExpectedNumericValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(0, (int)LicenseReleaseResultCode.Success);
            Assert.Equal(1, (int)LicenseReleaseResultCode.ServerUnavailable);
            Assert.Equal(2, (int)LicenseReleaseResultCode.FeatureNotFound);
            Assert.Equal(3, (int)LicenseReleaseResultCode.UserNotFound);
            Assert.Equal(4, (int)LicenseReleaseResultCode.PermissionDenied);
            Assert.Equal(5, (int)LicenseReleaseResultCode.ServerError);
            Assert.Equal(6, (int)LicenseReleaseResultCode.Timeout);
            Assert.Equal(7, (int)LicenseReleaseResultCode.Cancelled);
            Assert.Equal(8, (int)LicenseReleaseResultCode.InvalidParameters);
            Assert.Equal(9, (int)LicenseReleaseResultCode.LmutilNotFound);
            Assert.Equal(10, (int)LicenseReleaseResultCode.NetworkError);
            Assert.Equal(999, (int)LicenseReleaseResultCode.UnknownError);
        }
    }
}