using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for ErrorAnalyticsManager class
    /// </summary>
    public class ErrorAnalyticsTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public ErrorAnalyticsTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ErrorAnalyticsManager(null, TimeSpan.FromHours(1), TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Act
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            // Assert
            Assert.NotNull(analyticsManager);
            Assert.NotNull(analyticsManager.CurrentAnalytics);
        }

        [Fact]
        public void Constructor_ShouldInitializeCurrentAnalytics()
        {
            // Arrange
            var retentionPeriod = TimeSpan.FromHours(24);
            var analyticsInterval = TimeSpan.FromMinutes(5);

            // Act
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                retentionPeriod,
                analyticsInterval);

            // Assert
            Assert.NotNull(analyticsManager.CurrentAnalytics);
            Assert.True(analyticsManager.CurrentAnalytics.PeriodStart <= DateTime.Now);
            Assert.True(analyticsManager.CurrentAnalytics.PeriodEnd >= DateTime.Now);
            Assert.NotNull(analyticsManager.CurrentAnalytics.ErrorsByCategory);
            Assert.NotNull(analyticsManager.CurrentAnalytics.ErrorsBySeverity);
        }

        [Fact]
        public void RecordError_WithNullException_ShouldNotRecord()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            var classification = new ErrorClassification();

            // Act
            analyticsManager.RecordError(null, classification);

            // Assert - should not throw exception
        }

        [Fact]
        public void RecordError_WithNullClassification_ShouldNotRecord()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            var exception = new SocketException();

            // Act
            analyticsManager.RecordError(exception, null);

            // Assert - should not throw exception
        }

        [Fact]
        public void RecordError_WithValidParameters_ShouldRecordErrorEvent()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            var exception = new SocketException();
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                ErrorCode = "NETWORK_ERROR",
                TechnicalMessage = "Network connection failed"
            };

            // Act
            analyticsManager.RecordError(exception, classification);

            // Assert - should not throw exception
        }

        [Fact]
        public void RecordError_WithRecoveryResult_ShouldIncludeRecoveryData()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            var exception = new SocketException();
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                ErrorCode = "NETWORK_ERROR"
            };

            var recoveryResult = new RecoveryResult
            {
                Success = true,
                RecoveryTime = TimeSpan.FromSeconds(2)
            };

            // Act
            analyticsManager.RecordError(exception, classification, null, recoveryResult);

            // Assert - should not throw exception
        }

        [Fact]
        public void GetAnalyticsForPeriod_WithValidRange_ShouldReturnAnalytics()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            var startTime = DateTime.Now.AddHours(-1);
            var endTime = DateTime.Now;

            // Act
            var analytics = analyticsManager.GetAnalyticsForPeriod(startTime, endTime);

            // Assert
            Assert.NotNull(analytics);
            Assert.Equal(startTime, analytics.PeriodStart);
            Assert.Equal(endTime, analytics.PeriodEnd);
        }

        [Fact]
        public void GetAnalyticsForPeriod_WithInvalidRange_ShouldHandleGracefully()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            var startTime = DateTime.Now;
            var endTime = DateTime.Now.AddHours(-1); // End before start

            // Act
            var analytics = analyticsManager.GetAnalyticsForPeriod(startTime, endTime);

            // Assert
            Assert.NotNull(analytics);
            Assert.Equal(0, analytics.TotalErrors);
        }

        [Fact]
        public void GetErrorTrends_WithTimeWindow_ShouldReturnTrends()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(24),
                TimeSpan.FromMinutes(5));

            var timeWindow = TimeSpan.FromHours(6);

            // Act
            var trends = analyticsManager.GetErrorTrends(timeWindow);

            // Assert
            Assert.NotNull(trends);
            // Should return empty trends for new instance
        }

        [Fact]
        public void GetErrorPatterns_WithNoData_ShouldReturnEmptyPatterns()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(24),
                TimeSpan.FromMinutes(5));

            // Act
            var patterns = analyticsManager.GetErrorPatterns();

            // Assert
            Assert.NotNull(patterns);
            Assert.Equal(0, patterns.RecentErrors);
            Assert.Equal(0, patterns.PreviousPeriodErrors);
            Assert.Equal(0, patterns.ErrorGrowthRate);
            Assert.NotNull(patterns.TopErrorTypes);
            Assert.NotNull(patterns.TopOperations);
        }

        [Fact]
        public void GetErrorPatterns_WithRecentErrors_ShouldCalculateGrowthRate()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(48),
                TimeSpan.FromMinutes(1));

            // Record some recent errors
            var exception = new SocketException();
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                ErrorCode = "NETWORK_ERROR"
            };

            for (int i = 0; i < 5; i++)
            {
                analyticsManager.RecordError(exception, classification);
            }

            // Wait for processing
            Thread.Sleep(100);

            // Act
            var patterns = analyticsManager.GetErrorPatterns();

            // Assert
            Assert.NotNull(patterns);
            Assert.Equal(5, patterns.RecentErrors);
            Assert.Equal(0, patterns.PreviousPeriodErrors); // No older errors
            // Growth rate should be calculated (might be infinity when previous is 0)
        }

        [Fact]
        public void ErrorEvent_ShouldHaveRequiredProperties()
        {
            // Arrange
            var errorEvent = new ErrorEvent
            {
                EventId = "test-event-id",
                Timestamp = DateTime.Now,
                ErrorType = "SocketException",
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Message = "Network error",
                ErrorCode = "NETWORK_ERROR",
                SourceOperation = "TestOperation"
            };

            // Assert
            Assert.Equal("test-event-id", errorEvent.EventId);
            Assert.Equal("SocketException", errorEvent.ErrorType);
            Assert.Equal(ErrorCategory.Network, errorEvent.Category);
            Assert.Equal(ErrorSeverity.Error, errorEvent.Severity);
            Assert.Equal("Network error", errorEvent.Message);
            Assert.Equal("NETWORK_ERROR", errorEvent.ErrorCode);
            Assert.Equal("TestOperation", errorEvent.SourceOperation);
        }

        [Fact]
        public void ErrorAnalytics_ShouldInitializeCollections()
        {
            // Arrange
            var analytics = new ErrorAnalytics
            {
                PeriodStart = DateTime.Now.AddHours(-1),
                PeriodEnd = DateTime.Now
            };

            // Assert
            Assert.NotNull(analytics.ErrorsByCategory);
            Assert.NotNull(analytics.ErrorsBySeverity);
            Assert.NotNull(analytics.ErrorsByType);
            Assert.NotNull(analytics.ErrorsByOperation);
            Assert.NotNull(analytics.TopErrorCodes);
            Assert.NotNull(analytics.RecoveryStats);
            Assert.NotNull(analytics.RetryStats);
        }

        [Fact]
        public void RecoveryStatistics_ShouldInitializeWithDefaultValues()
        {
            // Arrange
            var recoveryStats = new RecoveryStatistics();

            // Assert
            Assert.Equal(0, recoveryStats.TotalAttempts);
            Assert.Equal(0, recoveryStats.SuccessfulRecoveries);
            Assert.Equal(0, recoveryStats.FailedRecoveries);
            Assert.Equal(0, recoveryStats.SuccessRate);
            Assert.Equal(TimeSpan.Zero, recoveryStats.AverageRecoveryTime);
            Assert.NotNull(recoveryStats.SuccessByCategory);
        }

        [Fact]
        public void ErrorCodeCount_ShouldHavePercentage()
        {
            // Arrange
            var errorCodeCount = new ErrorCodeCount
            {
                ErrorCode = "NETWORK_ERROR",
                Count = 10
            };

            // Act
            errorCodeCount.Percentage = 25.5;

            // Assert
            Assert.Equal("NETWORK_ERROR", errorCodeCount.ErrorCode);
            Assert.Equal(10, errorCodeCount.Count);
            Assert.Equal(25.5, errorCodeCount.Percentage);
        }

        [Fact]
        public void RetryStatistics_ShouldInitializeWithDefaultValues()
        {
            // Arrange
            var retryStats = new RetryStatistics();

            // Assert
            Assert.Equal(0, retryStats.TotalAttempts);
            Assert.Equal(0, retryStats.SuccessfulRetries);
            Assert.Equal(0, retryStats.FailedRetries);
            Assert.Equal(0, retryStats.AverageRetryCount);
            Assert.Equal(0, retryStats.MaximumRetryCount);
        }

        [Fact]
        public void ErrorPatterns_ShouldInitializeCollections()
        {
            // Arrange
            var patterns = new ErrorPatterns();

            // Assert
            Assert.NotNull(patterns.TopErrorTypes);
            Assert.NotNull(patterns.TopOperations);
            Assert.Equal(0, patterns.RecentErrors);
            Assert.Equal(0, patterns.PreviousPeriodErrors);
            Assert.Equal(0, patterns.ErrorGrowthRate);
            Assert.Equal(0, patterns.CriticalErrors);
        }

        [Fact]
        public void ErrorTypeCount_ShouldHoldErrorTypeAndCount()
        {
            // Arrange
            var errorTypeCount = new ErrorTypeCount
            {
                ErrorType = "SocketException",
                Count = 5
            };

            // Assert
            Assert.Equal("SocketException", errorTypeCount.ErrorType);
            Assert.Equal(5, errorTypeCount.Count);
        }

        [Fact]
        public void OperationErrorCount_ShouldHoldOperationAndCount()
        {
            // Arrange
            var operationErrorCount = new OperationErrorCount
            {
                Operation = "GetLicenseStatus",
                Count = 3
            };

            // Assert
            Assert.Equal("GetLicenseStatus", operationErrorCount.Operation);
            Assert.Equal(3, operationErrorCount.Count);
        }

        [Fact]
        public async Task MultipleErrorEvents_ShouldBeProcessedCorrectly()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMilliseconds(100));

            var exception1 = new SocketException();
            var classification1 = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                ErrorCode = "NETWORK_ERROR",
                SourceOperation = "Operation1"
            };

            var exception2 = new TimeoutException();
            var classification2 = new ErrorClassification
            {
                Category = ErrorCategory.Timeout,
                Severity = ErrorSeverity.Warning,
                ErrorCode = "TIMEOUT_ERROR",
                SourceOperation = "Operation2"
            };

            // Act
            analyticsManager.RecordError(exception1, classification1);
            analyticsManager.RecordError(exception2, classification2);

            // Wait for processing
            await Task.Delay(200);

            // Assert
            var currentAnalytics = analyticsManager.CurrentAnalytics;
            Assert.True(currentAnalytics.TotalErrors >= 2);
            Assert.True(currentAnalytics.ErrorsByCategory.ContainsKey(ErrorCategory.Network));
            Assert.True(currentAnalytics.ErrorsByCategory.ContainsKey(ErrorCategory.Timeout));
        }

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var analyticsManager = new ErrorAnalyticsManager(
                _mockLogger.Object,
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            // Act
            analyticsManager.Dispose();

            // Assert - should not throw exception
            analyticsManager.Dispose(); // Dispose multiple times should be safe
        }
    }
}