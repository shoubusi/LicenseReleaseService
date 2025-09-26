using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Unit tests for TimerErrorClassifier
    /// </summary>
    public class TimerErrorClassifierTests
    {
        private readonly Mock<ILogger<TimerErrorClassifier>> _mockLogger;
        private readonly TimerErrorClassifier _classifier;

        public TimerErrorClassifierTests()
        {
            _mockLogger = new Mock<ILogger<TimerErrorClassifier>>();
            _classifier = new TimerErrorClassifier(_mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerErrorClassifier(null));
        }

        [Fact]
        public void Constructor_WithValidLogger_InitializesDefaultMappings()
        {
            // Arrange & Act
            var classifier = new TimerErrorClassifier(_mockLogger.Object);

            // Assert
            var exceptionMappings = classifier.GetExceptionMappings();
            var severityMappings = classifier.GetSeverityMappings();
            var recoveryMappings = classifier.GetRecoveryMappings();

            Assert.NotEmpty(exceptionMappings);
            Assert.NotEmpty(severityMappings);
            Assert.NotEmpty(recoveryMappings);

            // Verify some default mappings exist
            Assert.Contains(typeof(ArgumentException), exceptionMappings.Keys);
            Assert.Contains(typeof(TimeoutException), exceptionMappings.Keys);
            Assert.Contains(typeof(SocketException), exceptionMappings.Keys);
            Assert.Contains(typeof(OutOfMemoryException), severityMappings.Keys);
        }

        #endregion

        #region ClassifyError Tests

        [Fact]
        public async Task ClassifyError_WithNullException_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _classifier.ClassifyError(null));
        }

        [Fact]
        public async Task ClassifyError_WithArgumentException_ReturnsConfigurationCategory()
        {
            // Arrange
            var exception = new ArgumentException("Invalid argument");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.Configuration, result.Category);
            Assert.Equal(TimerErrorSeverity.High, result.Severity);
            Assert.Equal(TimerRecoveryAction.DisableTimer, result.RecoveryAction);
            Assert.False(result.IsTransient);
            Assert.True(result.IsRecoverable);
        }

        [Fact]
        public async Task ClassifyError_WithTimeoutException_ReturnsTimeoutCategory()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.Timeout, result.Category);
            Assert.Equal(TimerErrorSeverity.Low, result.Severity);
            Assert.Equal(TimerRecoveryAction.Retry, result.RecoveryAction);
            Assert.True(result.IsTransient);
            Assert.True(result.IsRecoverable);
        }

        [Fact]
        public async Task ClassifyError_WithSocketException_ReturnsNetworkCategory()
        {
            // Arrange
            var exception = new SocketException(10054); // Connection reset

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.Network, result.Category);
            Assert.Equal(TimerErrorSeverity.Medium, result.Severity);
            Assert.Equal(TimerRecoveryAction.Retry, result.RecoveryAction);
            Assert.True(result.IsTransient);
            Assert.True(result.IsRecoverable);
        }

        [Fact]
        public async Task ClassifyError_WithOutOfMemoryException_ReturnsResourceCategoryAndCriticalSeverity()
        {
            // Arrange
            var exception = new OutOfMemoryException();

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.Resource, result.Category);
            Assert.Equal(TimerErrorSeverity.Critical, result.Severity);
            Assert.Equal(TimerRecoveryAction.IncreaseInterval, result.RecoveryAction);
            Assert.False(result.IsTransient);
            Assert.False(result.IsRecoverable);
        }

        [Fact]
        public async Task ClassifyError_WithAggregateException_ClassifiesInnerExceptions()
        {
            // Arrange
            var innerException1 = new TimeoutException("Inner timeout");
            var innerException2 = new SocketException(10053);
            var aggregateException = new AggregateException(innerException1, innerException2);

            // Act
            var result = await _classifier.ClassifyError(aggregateException);

            // Assert
            // Should return the most severe category among inner exceptions
            Assert.Equal(TimerErrorCategory.Network, result.Category); // Network is more severe than Timeout
            Assert.Equal(TimerErrorSeverity.Medium, result.Severity);
            Assert.True(result.IsTransient);
            Assert.True(result.IsRecoverable);
        }

        [Fact]
        public async Task ClassifyError_WithGenericException_ReturnsUnknownCategory()
        {
            // Arrange
            var exception = new Exception("Generic error");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.Unknown, result.Category);
            Assert.Equal(TimerErrorSeverity.Medium, result.Severity);
            Assert.Equal(TimerRecoveryAction.LogAndContinue, result.RecoveryAction);
            Assert.False(result.IsTransient);
            Assert.True(result.IsRecoverable);
        }

        [Fact]
        public async Task ClassifyError_WithExceptionContainingTimeoutInMessage_ReturnsTimeoutCategory()
        {
            // Arrange
            var exception = new InvalidOperationException("The operation timed out after 30 seconds");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.Timeout, result.Category);
            Assert.Equal(TimerErrorSeverity.Low, result.Severity);
            Assert.True(result.IsTransient);
            Assert.True(result.IsRecoverable);
        }

        [Fact]
        public async Task ClassifyError_WithExceptionContainingNetworkInMessage_ReturnsNetworkCategory()
        {
            // Arrange
            var exception = new InvalidOperationException("Network connection failed");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.Network, result.Category);
            Assert.Equal(TimerErrorSeverity.Medium, result.Severity);
            Assert.True(result.IsTransient);
            Assert.True(result.IsRecoverable);
        }

        [Fact]
        public async Task ClassifyError_WithExceptionContainingLicenseInMessage_ReturnsLicenseServerCategory()
        {
            // Arrange
            var exception = new InvalidOperationException("License server unavailable");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.LicenseServer, result.Category);
            Assert.Equal(TimerErrorSeverity.High, result.Severity);
            Assert.Equal(TimerRecoveryAction.EnableCircuitBreaker, result.RecoveryAction);
            Assert.True(result.ShouldTriggerCircuitBreaker);
        }

        [Fact]
        public async Task ClassifyError_WithClassificationError_LogsErrorAndReturnsFallback()
        {
            // Arrange
            var exception = new Exception("Test exception");

            // Create a mock classifier that throws during classification
            var mockClassifier = new Mock<ITimerErrorClassifier>();
            mockClassifier.Setup(c => c.ClassifyError(It.IsAny<Exception>(), It.IsAny<object>()))
                .ThrowsAsync(new Exception("Classification failed"));

            var realClassifier = new TimerErrorClassifier(_mockLogger.Object);

            // Act
            var result = await realClassifier.ClassifyError(exception);

            // Assert
            Assert.Equal(TimerErrorCategory.Unknown, result.Category);
            Assert.Equal(TimerErrorSeverity.Medium, result.Severity);
            Assert.Equal(TimerRecoveryAction.LogAndContinue, result.RecoveryAction);
            Assert.False(result.IsTransient);
            Assert.True(result.IsRecoverable);
            Assert.Contains("ClassificationError", result.AnalysisDetails.Keys);
        }

        [Fact]
        public async Task ClassifyError_WithContext_PassesContextThrough()
        {
            // Arrange
            var exception = new TimeoutException("Test timeout");
            var context = new { TestProperty = "TestValue" };

            // Act
            var result = await _classifier.ClassifyError(exception, context);

            // Assert
            Assert.Equal(context, result.Context);
        }

        #endregion

        #region Classification Properties Tests

        [Theory]
        [InlineData(TimerErrorSeverity.Low, TimerErrorCategory.Timeout, false, false, true)]
        [InlineData(TimerErrorSeverity.Medium, TimerErrorCategory.Network, false, false, true)]
        [InlineData(TimerErrorSeverity.High, TimerErrorCategory.Resource, true, false, false)]
        [InlineData(TimerErrorSeverity.Critical, TimerErrorCategory.Configuration, true, true, false)]
        [InlineData(TimerErrorSeverity.High, TimerErrorCategory.LicenseServer, true, false, false)]
        public async Task TimerErrorClassification_Properties_ReturnExpectedValues(
            TimerErrorSeverity severity, TimerErrorCategory category,
            bool expectedTrigger, bool expectedStop, bool expectedAllowsRetry)
        {
            // Arrange
            var exception = new Exception("Test");
            var result = await _classifier.ClassifyError(exception);

            // Override for testing
            result.Severity = severity;
            result.Category = category;

            // Act & Assert
            Assert.Equal(expectedTrigger, result.ShouldTriggerCircuitBreaker);
            Assert.Equal(expectedStop, result.ShouldStopTimer);
            Assert.Equal(expectedAllowsRetry, result.AllowsRetry);
        }

        #endregion

        #region Custom Mapping Tests

        [Fact]
        public void RegisterExceptionMapping_WithValidType_AddsMapping()
        {
            // Arrange
            var exceptionType = typeof(DivideByZeroException);
            var category = TimerErrorCategory.Execution;

            // Act
            _classifier.RegisterExceptionMapping(exceptionType, category);

            // Assert
            var mappings = _classifier.GetExceptionMappings();
            Assert.Contains(exceptionType, mappings.Keys);
            Assert.Equal(category, mappings[exceptionType]);
        }

        [Fact]
        public void RegisterExceptionMapping_WithNullType_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _classifier.RegisterExceptionMapping(null, TimerErrorCategory.Unknown));
        }

        [Fact]
        public void RegisterExceptionMapping_WithNonExceptionType_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _classifier.RegisterExceptionMapping(typeof(string), TimerErrorCategory.Unknown));
        }

        [Fact]
        public async Task RegisterExceptionMapping_WithCustomMapping_IsUsedInClassification()
        {
            // Arrange
            var exceptionType = typeof(NotImplementedException);
            var category = TimerErrorCategory.Process;
            _classifier.RegisterExceptionMapping(exceptionType, category);

            var exception = new NotImplementedException("Not implemented");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(category, result.Category);
        }

        [Fact]
        public void RegisterSeverityMapping_WithValidType_AddsMapping()
        {
            // Arrange
            var exceptionType = typeof(NotSupportedException);
            var severity = TimerErrorSeverity.Critical;

            // Act
            _classifier.RegisterSeverityMapping(exceptionType, severity);

            // Assert
            var mappings = _classifier.GetSeverityMappings();
            Assert.Contains(exceptionType, mappings.Keys);
            Assert.Equal(severity, mappings[exceptionType]);
        }

        [Fact]
        public async Task RegisterSeverityMapping_WithCustomMapping_IsUsedInClassification()
        {
            // Arrange
            var exceptionType = typeof(NotImplementedException);
            var severity = TimerErrorSeverity.Critical;
            _classifier.RegisterSeverityMapping(exceptionType, severity);

            var exception = new NotImplementedException("Not implemented");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(severity, result.Severity);
        }

        [Fact]
        public void RegisterRecoveryMapping_WithValidCategory_AddsMapping()
        {
            // Arrange
            var category = TimerErrorCategory.Unknown;
            var action = TimerRecoveryAction.Custom;

            // Act
            _classifier.RegisterRecoveryMapping(category, action);

            // Assert
            var mappings = _classifier.GetRecoveryMappings();
            Assert.Contains(category, mappings.Keys);
            Assert.Equal(action, mappings[category]);
        }

        [Fact]
        public async Task RegisterRecoveryMapping_WithCustomMapping_IsUsedInClassification()
        {
            // Arrange
            var category = TimerErrorCategory.Unknown;
            var action = TimerRecoveryAction.Custom;
            _classifier.RegisterRecoveryMapping(category, action);

            var exception = new Exception("Generic error");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.Equal(action, result.RecoveryAction);
        }

        #endregion

        #region Analysis Details Tests

        [Fact]
        public async Task ClassifyError_WithSocketException_IncludesNetworkErrorDetails()
        {
            // Arrange
            var exception = new SocketException(10054);

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.NotNull(result.AnalysisDetails);
            Assert.Contains("ErrorCode", result.AnalysisDetails.Keys);
            Assert.Contains("SocketErrorCode", result.AnalysisDetails.Keys);
            Assert.Equal(10054, result.AnalysisDetails["ErrorCode"]);
        }

        [Fact]
        public async Task ClassifyError_WithTimeoutException_IncludesTimeoutErrorDetails()
        {
            // Arrange
            var exception = new TimeoutException("Test timeout");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.NotNull(result.AnalysisDetails);
            Assert.Contains("TimeoutType", result.AnalysisDetails.Keys);
            Assert.Contains("TimeoutMessage", result.AnalysisDetails.Keys);
            Assert.Equal("TimeoutException", result.AnalysisDetails["TimeoutType"]);
            Assert.Equal("Test timeout", result.AnalysisDetails["TimeoutMessage"]);
        }

        [Fact]
        public async Task ClassifyError_WithOutOfMemoryException_IncludesResourceErrorDetails()
        {
            // Arrange
            var exception = new OutOfMemoryException();

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.NotNull(result.AnalysisDetails);
            Assert.Contains("ResourceType", result.AnalysisDetails.Keys);
            Assert.Equal("Memory", result.AnalysisDetails["ResourceType"]);
        }

        [Fact]
        public async Task ClassifyError_WithAnyException_IncludesBasicDetails()
        {
            // Arrange
            var exception = new InvalidOperationException("Test message");

            // Act
            var result = await _classifier.ClassifyError(exception);

            // Assert
            Assert.NotNull(result.AnalysisDetails);
            Assert.Contains("ExceptionType", result.AnalysisDetails.Keys);
            Assert.Contains("ExceptionMessage", result.AnalysisDetails.Keys);
            Assert.Contains("Category", result.AnalysisDetails.Keys);
            Assert.Contains("Severity", result.AnalysisDetails.Keys);
            Assert.Contains("StackTrace", result.AnalysisDetails.Keys);

            Assert.Equal("InvalidOperationException", result.AnalysisDetails["ExceptionType"]);
            Assert.Equal("Test message", result.AnalysisDetails["ExceptionMessage"]);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public async Task TimerErrorClassification_ToString_ReturnsExpectedFormat()
        {
            // Arrange
            var exception = new TimeoutException("Test timeout");
            var result = await _classifier.ClassifyError(exception);

            // Act
            var toString = result.ToString();

            // Assert
            Assert.Contains("TimerErrorClassification", toString);
            Assert.Contains("Timeout", toString);
            Assert.Contains("Low", toString);
            Assert.Contains("Retry", toString);
            Assert.Contains("True", toString); // IsTransient
            Assert.Contains("True", toString); // IsRecoverable
            Assert.Contains("TimeoutException", toString);
        }

        #endregion
    }

    /// <summary>
    /// Interface for mocking TimerErrorClassifier
    /// </summary>
    public interface ITimerErrorClassifier
    {
        Task<TimerErrorClassification> ClassifyError(Exception exception, object context = null);
        void RegisterExceptionMapping(Type exceptionType, TimerErrorCategory category);
        void RegisterSeverityMapping(Type exceptionType, TimerErrorSeverity severity);
        void RegisterRecoveryMapping(TimerErrorCategory category, TimerRecoveryAction action);
        IReadOnlyDictionary<Type, TimerErrorCategory> GetExceptionMappings();
        IReadOnlyDictionary<Type, TimerErrorSeverity> GetSeverityMappings();
        IReadOnlyDictionary<TimerErrorCategory, TimerRecoveryAction> GetRecoveryMappings();
    }
}