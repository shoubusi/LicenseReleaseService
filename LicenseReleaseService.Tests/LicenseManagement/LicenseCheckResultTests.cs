using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for the LicenseCheckResult class
    /// </summary>
    public class LicenseCheckResultTests
    {
        private readonly ITestOutputHelper _output;

        public LicenseCheckResultTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Constructor_Default_InitializesWithDefaultValues()
        {
            // Arrange & Act
            var result = new LicenseCheckResult();

            // Assert
            Assert.NotEqual(Guid.Empty, result.ResultId);
            Assert.Equal(Guid.Empty, result.OperationId);
            Assert.Equal(LicenseCheckOperationType.ServerStatusCheck, result.OperationType);
            Assert.Equal(string.Empty, result.Server);
            Assert.Equal(0, result.Port);
            Assert.Equal(string.Empty, result.Feature);
            Assert.Equal(string.Empty, result.User);
            Assert.False(result.Success);
            Assert.Equal(string.Empty, result.ErrorMessage);
            Assert.Null(result.Exception);
            Assert.True(result.StartTime <= DateTime.Now);
            Assert.True(result.EndTime <= DateTime.Now);
            Assert.True(result.Duration >= TimeSpan.Zero);
            Assert.Equal(0, result.RetryAttempt);
            Assert.Equal(TimeSpan.Zero, result.Timeout);
            Assert.Equal(0, result.LicensesChecked);
            Assert.Equal(0, result.FeaturesProcessed);
            Assert.Equal(0, result.UsersProcessed);
            Assert.Equal(0, result.IdleLicensesFound);
            Assert.Equal(0, result.BorrowedLicensesFound);
            Assert.Null(result.ServerStatus);
            Assert.Null(result.UsageStatistics);
            Assert.NotNull(result.IdleLicenses);
            Assert.Empty(result.IdleLicenses);
            Assert.NotNull(result.BorrowedLicenses);
            Assert.Empty(result.BorrowedLicenses);
            Assert.Null(result.Metrics);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data);
            Assert.NotNull(result.Tags);
            Assert.Empty(result.Tags);
            Assert.True(result.Timestamp <= DateTime.Now);
            Assert.Null(result.ExecutionContext);
            Assert.False(result.WasCached);
            Assert.Null(result.CacheTimestamp);
            Assert.False(result.TimedOut);
            Assert.False(result.Cancelled);
            Assert.Equal(LicenseCheckOperationPriority.Normal, result.Priority);
        }

        [Fact]
        public void Constructor_WithOperation_SetsValuesFromOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                OperationId = Guid.NewGuid(),
                OperationType = LicenseCheckOperationType.FeatureStatusCheck,
                Server = "test-server",
                Port = 27000,
                Feature = "test-feature",
                User = "test-user",
                Priority = LicenseCheckOperationPriority.High,
                Timeout = TimeSpan.FromMinutes(5),
                RetryAttempt = 2
            };

            // Act
            var result = new LicenseCheckResult(operation);

            // Assert
            Assert.Equal(operation.OperationId, result.OperationId);
            Assert.Equal(operation.OperationType, result.OperationType);
            Assert.Equal(operation.Server, result.Server);
            Assert.Equal(operation.Port, result.Port);
            Assert.Equal(operation.Feature, result.Feature);
            Assert.Equal(operation.User, result.User);
            Assert.Equal(operation.Priority, result.Priority);
            Assert.Equal(operation.Timeout, result.Timeout);
            Assert.Equal(operation.RetryAttempt, result.RetryAttempt);
        }

        [Fact]
        public void CreateSuccess_CreatesSuccessfulResult()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000
            };
            var serverStatus = new LicenseServerStatus
            {
                Server = "test-server",
                Port = 27000,
                IsServerUp = true,
                TotalLicenses = 100,
                LicensesInUse = 50,
                AvailableLicenses = 50,
                Features = new Dictionary<string, LicenseFeatureStatus>
                {
                    ["feature1"] = new LicenseFeatureStatus
                    {
                        FeatureName = "feature1",
                        TotalLicenses = 100,
                        LicensesInUse = 50,
                        AvailableLicenses = 50
                    }
                }
            };

            // Act
            var result = LicenseCheckResult.CreateSuccess(operation, serverStatus);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(operation.OperationId, result.OperationId);
            Assert.Equal(serverStatus, result.ServerStatus);
            Assert.Equal(100, result.LicensesChecked);
            Assert.Equal(1, result.FeaturesProcessed);
            Assert.Equal(0, result.UsersProcessed);
            Assert.True(result.EndTime >= result.StartTime);
            Assert.True(result.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void CreateFailure_CreatesFailedResult()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000
            };
            var errorMessage = "Test error message";
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = LicenseCheckResult.CreateFailure(operation, errorMessage, exception);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(operation.OperationId, result.OperationId);
            Assert.Equal(errorMessage, result.ErrorMessage);
            Assert.Equal(exception, result.Exception);
            Assert.True(result.EndTime >= result.StartTime);
            Assert.True(result.Duration > TimeSpan.Zero);
        }

        [Fact]
        public void CreateTimeout_CreatesTimeoutResult()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000,
                Timeout = TimeSpan.FromMinutes(2)
            };

            // Act
            var result = LicenseCheckResult.CreateTimeout(operation);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Operation timed out", result.ErrorMessage);
            Assert.True(result.TimedOut);
            Assert.Equal(operation.Timeout, result.Duration);
        }

        [Fact]
        public void CreateCancelled_CreatesCancelledResult()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000
            };

            // Act
            var result = LicenseCheckResult.CreateCancelled(operation);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Operation was cancelled", result.ErrorMessage);
            Assert.True(result.Cancelled);
            Assert.True(result.EndTime >= result.StartTime);
        }

        [Fact]
        public void CompleteWithServerStatus_SetsValuesCorrectly()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var serverStatus = new LicenseServerStatus
            {
                Server = "test-server",
                Port = 27000,
                IsServerUp = true,
                TotalLicenses = 200,
                LicensesInUse = 150,
                AvailableLicenses = 50,
                Features = new Dictionary<string, LicenseFeatureStatus>
                {
                    ["feature1"] = new LicenseFeatureStatus
                    {
                        FeatureName = "feature1",
                        TotalLicenses = 100,
                        LicensesInUse = 75,
                        AvailableLicenses = 25
                    },
                    ["feature2"] = new LicenseFeatureStatus
                    {
                        FeatureName = "feature2",
                        TotalLicenses = 100,
                        LicensesInUse = 75,
                        AvailableLicenses = 25
                    }
                }
            };

            // Act
            result.CompleteWithServerStatus(serverStatus);

            // Assert
            Assert.Equal(serverStatus, result.ServerStatus);
            Assert.Equal(string.IsNullOrEmpty(serverStatus.ErrorMessage), result.Success);
            Assert.Equal(serverStatus.ErrorMessage, result.ErrorMessage);
            Assert.True(result.EndTime >= result.StartTime);
            Assert.True(result.Duration > TimeSpan.Zero);
            Assert.Equal(200, result.LicensesChecked);
            Assert.Equal(2, result.FeaturesProcessed);
            Assert.Equal(0, result.UsersProcessed);
        }

        [Fact]
        public void AddIdleLicenses_AddsLicensesToResult()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var idleLicenses = new List<LicenseInfo>
            {
                new LicenseInfo { User = "user1", Feature = "feature1" },
                new LicenseInfo { User = "user2", Feature = "feature2" }
            };

            // Act
            result.AddIdleLicenses(idleLicenses);

            // Assert
            Assert.Equal(2, result.IdleLicenses.Count);
            Assert.Contains(idleLicenses[0], result.IdleLicenses);
            Assert.Contains(idleLicenses[1], result.IdleLicenses);
            Assert.Equal(2, result.IdleLicensesFound);
        }

        [Fact]
        public void AddIdleLicenses_HandlesNullInput()
        {
            // Arrange
            var result = new LicenseCheckResult();

            // Act
            result.AddIdleLicenses(null);

            // Assert
            Assert.Empty(result.IdleLicenses);
            Assert.Equal(0, result.IdleLicensesFound);
        }

        [Fact]
        public void AddBorrowedLicenses_AddsLicensesToResult()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var borrowedLicenses = new List<LicenseInfo>
            {
                new LicenseInfo { User = "user1", Feature = "feature1" },
                new LicenseInfo { User = "user2", Feature = "feature2" }
            };

            // Act
            result.AddBorrowedLicenses(borrowedLicenses);

            // Assert
            Assert.Equal(2, result.BorrowedLicenses.Count);
            Assert.Contains(borrowedLicenses[0], result.BorrowedLicenses);
            Assert.Contains(borrowedLicenses[1], result.BorrowedLicenses);
            Assert.Equal(2, result.BorrowedLicensesFound);
        }

        [Fact]
        public void SetUsageStatistics_SetsStatisticsCorrectly()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var statistics = new LicenseUsageStatistics
            {
                TotalLicenses = 500,
                LicensesInUse = 300,
                AvailableLicenses = 200,
                ActiveUsers = 150,
                IdleUsers = 20,
                BorrowedUsers = 10
            };

            // Act
            result.SetUsageStatistics(statistics);

            // Assert
            Assert.Equal(statistics, result.UsageStatistics);
            Assert.Equal(500, result.LicensesChecked);
            Assert.Equal(150, result.UsersProcessed);
            Assert.Equal(20, result.IdleLicensesFound);
            Assert.Equal(10, result.BorrowedLicensesFound);
        }

        [Fact]
        public void AddTag_AddsTagToResult()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var tag = "test-tag";

            // Act
            result.AddTag(tag);

            // Assert
            Assert.Contains(tag, result.Tags);
            Assert.Single(result.Tags);
        }

        [Fact]
        public void AddTag_DoesNotAddDuplicateTag()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var tag = "test-tag";

            // Act
            result.AddTag(tag);
            result.AddTag(tag);

            // Assert
            Assert.Contains(tag, result.Tags);
            Assert.Single(result.Tags);
        }

        [Fact]
        public void AddData_AddsDataToResult()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var key = "test-key";
            var value = "test-value";

            // Act
            result.AddData(key, value);

            // Assert
            Assert.Contains(key, result.Data);
            Assert.Equal(value, result.Data[key]);
        }

        [Fact]
        public void GetData_ReturnsCorrectValue()
        {
            // Arrange
            var result = new LicenseCheckResult();
            result.AddData("string-key", "string-value");
            result.AddData("int-key", 42);
            result.AddData("bool-key", true);

            // Act & Assert
            Assert.Equal("string-value", result.GetData<string>("string-key"));
            Assert.Equal(42, result.GetData<int>("int-key"));
            Assert.True(result.GetData<bool>("bool-key"));
            Assert.Equal("default", result.GetData<string>("non-existing-key", "default"));
            Assert.Equal(0, result.GetData<int>("non-existing-key"));
        }

        [Fact]
        public void HasTag_ReturnsCorrectValue()
        {
            // Arrange
            var result = new LicenseCheckResult();
            result.AddTag("existing-tag");

            // Act & Assert
            Assert.True(result.HasTag("existing-tag"));
            Assert.False(result.HasTag("non-existing-tag"));
        }

        [Fact]
        public void SetMetrics_SetsMetricsCorrectly()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var metrics = new LicenseCheckOperationMetrics
            {
                ServerResponseTimeMs = 100,
                ProcessingTimeMs = 50,
                QueueWaitTimeMs = 10,
                DataReceivedBytes = 1024,
                CacheHits = 5,
                CacheMisses = 2
            };

            // Act
            result.SetMetrics(metrics);

            // Assert
            Assert.Equal(metrics, result.Metrics);
        }

        [Fact]
        public void MarkAsCached_SetsCachedProperties()
        {
            // Arrange
            var result = new LicenseCheckResult();
            var cacheTimestamp = DateTime.Now.AddMinutes(-5);

            // Act
            result.MarkAsCached(cacheTimestamp);

            // Assert
            Assert.True(result.WasCached);
            Assert.Equal(cacheTimestamp, result.CacheTimestamp);
        }

        [Fact]
        public void CompletedWithinTimeout_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result1 = new LicenseCheckResult
            {
                Duration = TimeSpan.FromMinutes(1),
                Timeout = TimeSpan.FromMinutes(2)
            };

            var result2 = new LicenseCheckResult
            {
                Duration = TimeSpan.FromMinutes(3),
                Timeout = TimeSpan.FromMinutes(2)
            };

            // Assert
            Assert.True(result1.CompletedWithinTimeout);
            Assert.False(result2.CompletedWithinTimeout);
        }

        [Fact]
        public void HasWarnings_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result1 = new LicenseCheckResult
            {
                Success = true,
                ErrorMessage = "Warning message"
            };

            var result2 = new LicenseCheckResult
            {
                Success = true,
                ErrorMessage = ""
            };

            var result3 = new LicenseCheckResult
            {
                Success = false,
                ErrorMessage = "Error message"
            };

            // Assert
            Assert.True(result1.HasWarnings);
            Assert.False(result2.HasWarnings);
            Assert.False(result3.HasWarnings); // This is an error, not a warning
        }

        [Fact]
        public void HasErrors_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result1 = new LicenseCheckResult { Success = false };
            var result2 = new LicenseCheckResult { Success = true, ErrorMessage = "Warning" };
            var result3 = new LicenseCheckResult { Success = true };

            // Assert
            Assert.True(result1.HasErrors);
            Assert.True(result2.HasErrors);
            Assert.False(result3.HasErrors);
        }

        [Fact]
        public void SuccessRate_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result1 = new LicenseCheckResult
            {
                LicensesChecked = 100,
                IdleLicensesFound = 20,
                BorrowedLicensesFound = 10
            };

            var result2 = new LicenseCheckResult
            {
                LicensesChecked = 0,
                IdleLicensesFound = 0,
                BorrowedLicensesFound = 0
            };

            // Assert
            Assert.Equal(0.7, result1.SuccessRate); // (100 - 20 - 10) / 100 = 0.7
            Assert.Equal(0.0, result2.SuccessRate);
        }

        [Fact]
        public void TotalLicensesFound_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result1 = new LicenseCheckResult
            {
                ServerStatus = new LicenseServerStatus { TotalLicenses = 500 }
            };

            var result2 = new LicenseCheckResult();

            // Assert
            Assert.Equal(500, result1.TotalLicensesFound);
            Assert.Equal(0, result2.TotalLicensesFound);
        }

        [Fact]
        public void TotalLicensesInUse_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result1 = new LicenseCheckResult
            {
                ServerStatus = new LicenseServerStatus { LicensesInUse = 300 }
            };

            var result2 = new LicenseCheckResult();

            // Assert
            Assert.Equal(300, result1.TotalLicensesInUse);
            Assert.Equal(0, result2.TotalLicensesInUse);
        }

        [Fact]
        public void TotalAvailableLicenses_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result1 = new LicenseCheckResult
            {
                ServerStatus = new LicenseServerStatus { AvailableLicenses = 200 }
            };

            var result2 = new LicenseCheckResult();

            // Assert
            Assert.Equal(200, result1.TotalAvailableLicenses);
            Assert.Equal(0, result2.TotalAvailableLicenses);
        }

        [Fact]
        public void UtilizationPercentage_ReturnsCorrectValue()
        {
            // Arrange & Act
            var result1 = new LicenseCheckResult
            {
                ServerStatus = new LicenseServerStatus { TotalLicenses = 400, LicensesInUse = 100 }
            };

            var result2 = new LicenseCheckResult
            {
                ServerStatus = new LicenseServerStatus { TotalLicenses = 0, LicensesInUse = 0 }
            };

            // Assert
            Assert.Equal(25.0, result1.UtilizationPercentage); // 100/400 * 100 = 25%
            Assert.Equal(0.0, result2.UtilizationPercentage);
        }

        [Fact]
        public void ToString_ReturnsCorrectStringRepresentation()
        {
            // Arrange
            var result = new LicenseCheckResult
            {
                ResultId = Guid.NewGuid(),
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(1500),
                LicensesChecked = 100,
                FeaturesProcessed = 5,
                UsersProcessed = 25,
                IdleLicensesFound = 10,
                BorrowedLicensesFound = 5
            };

            // Act
            var resultString = result.ToString();

            // Assert
            Assert.Contains(result.ResultId.ToString("N"), resultString);
            Assert.Contains(result.OperationType.ToString(), resultString);
            Assert.Contains(result.Server, resultString);
            Assert.Contains(result.Port.ToString(), resultString);
            Assert.Contains(result.Success.ToString(), resultString);
            Assert.Contains("1500.00ms", resultString);
            Assert.Contains("100", resultString);
            Assert.Contains("5", resultString);
            Assert.Contains("25", resultString);
            Assert.Contains("10", resultString);
            Assert.Contains("5", resultString);
        }

        [Fact]
        public void ToDetailedString_ReturnsDetailedInformation()
        {
            // Arrange
            var result = new LicenseCheckResult
            {
                ResultId = Guid.NewGuid(),
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000,
                Success = true,
                Duration = TimeSpan.FromMilliseconds(1500),
                Timeout = TimeSpan.FromMinutes(2),
                RetryAttempt = 1,
                LicensesChecked = 100,
                FeaturesProcessed = 5,
                UsersProcessed = 25,
                IdleLicensesFound = 10,
                BorrowedLicensesFound = 5,
                ErrorMessage = "Warning message",
                WasCached = true,
                CacheTimestamp = DateTime.Now.AddMinutes(-5)
            };
            result.AddTag("test-tag");
            result.AddData("custom-data", "value");

            // Act
            var detailedString = result.ToDetailedString();

            // Assert
            Assert.Contains("LicenseCheckResult Details:", detailedString);
            Assert.Contains(result.ResultId.ToString("N"), detailedString);
            Assert.Contains(result.OperationType.ToString(), detailedString);
            Assert.Contains(result.Server, detailedString);
            Assert.Contains(result.Port.ToString(), detailedString);
            Assert.Contains(result.Success.ToString(), detailedString);
            Assert.Contains("1500.00ms", detailedString);
            Assert.Contains("120000.00ms", detailedString); // 2 minutes in ms
            Assert.Contains("1", detailedString); // Retry attempt
            Assert.Contains("test-tag", detailedString);
            Assert.Contains("Warning message", detailedString);
            Assert.Contains("Cached: Yes", detailedString);
        }

        [Fact]
        public void ToDetailedString_HandlesTimedOutAndCancelled()
        {
            // Arrange
            var result = new LicenseCheckResult
            {
                ResultId = Guid.NewGuid(),
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000,
                Success = false,
                Duration = TimeSpan.FromMinutes(3),
                Timeout = TimeSpan.FromMinutes(2),
                TimedOut = true,
                Cancelled = false
            };

            // Act
            var detailedString = result.ToDetailedString();

            // Assert
            Assert.Contains("Status: Timed Out", detailedString);
            Assert.DoesNotContain("Status: Cancelled", detailedString);
        }

        [Fact]
        public void ToDetailedString_HandlesCancelled()
        {
            // Arrange
            var result = new LicenseCheckResult
            {
                ResultId = Guid.NewGuid(),
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000,
                Success = false,
                Cancelled = true,
                TimedOut = false
            };

            // Act
            var detailedString = result.ToDetailedString();

            // Assert
            Assert.Contains("Status: Cancelled", detailedString);
            Assert.DoesNotContain("Status: Timed Out", detailedString);
        }
    }
}