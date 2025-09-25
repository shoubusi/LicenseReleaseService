using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for ErrorClassifier class
    /// </summary>
    public class ErrorClassifierTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public ErrorClassifierTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ErrorClassifier(null));
        }

        [Fact]
        public void Constructor_WithValidLogger_ShouldCreateInstance()
        {
            // Act
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);

            // Assert
            Assert.NotNull(errorClassifier);
        }

        [Fact]
        public void Constructor_ShouldInitializeDefaultMappings()
        {
            // Act
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);

            // Assert
            // The classifier should have default mappings for common exception types
            // We can verify this by testing classification of those types
        }

        [Fact]
        public async Task ClassifyException_WithNullException_ShouldThrowArgumentNullException()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() => errorClassifier.ClassifyException(null)));
        }

        [Fact]
        public void ClassifyException_WithSocketException_ShouldClassifyAsNetworkError()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new SocketException();

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Network, classification.Category);
            Assert.Equal(ErrorSeverity.Error, classification.Severity);
            Assert.Equal(RecoveryStrategy.Retry, classification.Strategy);
            Assert.True(classification.IsRetryable);
            Assert.True(classification.MaxRetries > 0);
            Assert.Equal("NETWORK_ERROR", classification.ErrorCode);
        }

        [Fact]
        public void ClassifyException_WithProcessExecutionException_ShouldClassifyAsProcessError()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new ProcessExecutionException("Process failed");

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Process, classification.Category);
            Assert.Equal(ErrorSeverity.Error, classification.Severity);
            Assert.Equal(RecoveryStrategy.Retry, classification.Strategy);
            Assert.True(classification.IsRetryable);
            Assert.Equal("PROCESS_ERROR", classification.ErrorCode);
        }

        [Fact]
        public void ClassifyException_WithTimeoutException_ShouldClassifyAsTimeoutError()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new TimeoutException("Operation timed out");

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Timeout, classification.Category);
            Assert.Equal(ErrorSeverity.Warning, classification.Severity);
            Assert.Equal(RecoveryStrategy.Retry, classification.Strategy);
            Assert.True(classification.IsRetryable);
            Assert.Equal("TIMEOUT_ERROR", classification.ErrorCode);
        }

        [Fact]
        public void ClassifyException_WithArgumentException_ShouldClassifyAsConfigurationError()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new ArgumentException("Invalid argument");

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Configuration, classification.Category);
            Assert.Equal(ErrorSeverity.Error, classification.Severity);
            Assert.Equal(RecoveryStrategy.None, classification.Strategy);
            Assert.False(classification.IsRetryable);
            Assert.Equal("CONFIGURATION_ERROR", classification.ErrorCode);
        }

        [Fact]
        public void ClassifyException_WithOperationCanceledException_ShouldClassifyAsUnknown()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new OperationCanceledException();

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Unknown, classification.Category);
            Assert.Equal(ErrorSeverity.Information, classification.Severity);
            Assert.Equal(RecoveryStrategy.None, classification.Strategy);
            Assert.False(classification.IsRetryable);
            Assert.Equal("OPERATION_CANCELLED", classification.ErrorCode);
        }

        [Fact]
        public void ClassifyException_WithLicenseManagerException_ShouldClassifyAsLicenseServerError()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new LicenseManagerException("License server error");

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.LicenseServer, classification.Category);
            Assert.Equal(ErrorSeverity.Error, classification.Severity);
            Assert.Equal(RecoveryStrategy.Retry, classification.Strategy);
            Assert.True(classification.IsRetryable);
            Assert.Equal("LICENSE_SERVER_ERROR", classification.ErrorCode);
        }

        [Fact]
        public void ClassifyException_WithUnknownException_ShouldCreateDefaultClassification()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new InvalidOperationException("Unknown error");

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Unknown, classification.Category);
            Assert.Equal(ErrorSeverity.Error, classification.Severity);
            Assert.Equal(RecoveryStrategy.None, classification.Strategy);
            Assert.False(classification.IsRetryable);
            Assert.Equal("UNKNOWN_ERROR", classification.ErrorCode);
        }

        [Fact]
        public void ClassifyException_WithProcessTimeout_ShouldHaveTimeoutSpecificProperties()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = ProcessExecutionException.TimeoutException("test.exe", "test", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Process, classification.Category);
            Assert.Equal(ErrorSeverity.Warning, classification.Severity);
            Assert.Equal(RecoveryStrategy.Retry, classification.Strategy);
            Assert.True(classification.IsRetryable);
            Assert.Contains("timeout", classification.UserMessage.ToLower());
        }

        [Fact]
        public void ClassifyException_WithProcessNotFound_ShouldHaveCriticalSeverity()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var result = new ProcessExecutionResult
            {
                ExitCode = 2,
                Output = "",
                Error = "File not found"
            };
            var exception = ProcessExecutionException.NonZeroExitCodeException("test.exe", "test", result);

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Process, classification.Category);
            Assert.Equal(ErrorSeverity.Critical, classification.Severity);
            Assert.Equal(RecoveryStrategy.None, classification.Strategy);
            Assert.False(classification.IsRetryable);
        }

        [Fact]
        public void ClassifyException_WithConnectionRefused_ShouldHaveSpecificMessage()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new SocketException((int)SocketError.ConnectionRefused);

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Network, classification.Category);
            Assert.Contains("connection was refused", classification.UserMessage.ToLower());
        }

        [Fact]
        public void ClassifyException_WithHostNotFound_ShouldHaveCriticalSeverity()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new SocketException((int)SocketError.HostNotFound);

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Network, classification.Category);
            Assert.Equal(ErrorSeverity.Critical, classification.Severity);
            Assert.Equal(RecoveryStrategy.None, classification.Strategy);
            Assert.False(classification.IsRetryable);
        }

        [Fact]
        public void ClassifyException_WithServerDownError_ShouldEscalate()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new LicenseManagerException("License server is down and not responding");

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.LicenseServer, classification.Category);
            Assert.Equal(ErrorSeverity.Critical, classification.Severity);
            Assert.Equal(RecoveryStrategy.Escalate, classification.Strategy);
            Assert.False(classification.IsRetryable);
        }

        [Fact]
        public void ClassifyException_WithInvalidLicenseError_ShouldNotRetry()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new LicenseManagerException("Invalid license configuration detected");

            // Act
            var classification = errorClassifier.ClassifyException(exception);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.LicenseServer, classification.Category);
            Assert.Equal(ErrorSeverity.Error, classification.Severity);
            Assert.Equal(RecoveryStrategy.None, classification.Strategy);
            Assert.False(classification.IsRetryable);
        }

        [Fact]
        public void ClassifyException_WithContext_ShouldIncludeContextInClassification()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new SocketException();
            var context = new { Server = "test-server", Port = 1234 };

            // Act
            var classification = errorClassifier.ClassifyException(exception, context, "TestOperation");

            // Assert
            Assert.NotNull(classification);
            Assert.Equal("TestOperation", classification.SourceOperation);
            Assert.NotNull(classification.Context);
            Assert.Equal(context, classification.Context["Context"]);
        }

        [Fact]
        public void ClassifyException_WithSourceOperation_ShouldSetSourceOperation()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var exception = new SocketException();

            // Act
            var classification = errorClassifier.ClassifyException(exception, null, "GetLicenseStatus");

            // Assert
            Assert.NotNull(classification);
            Assert.Equal("GetLicenseStatus", classification.SourceOperation);
        }

        [Fact]
        public void RegisterExceptionMapping_WithValidType_ShouldAddMapping()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var customException = new DivideByZeroException();
            var customClassification = new ErrorClassification
            {
                Category = ErrorCategory.Data,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                UserMessage = "Division by zero error",
                TechnicalMessage = "Division by zero occurred",
                ErrorCode = "DIVISION_BY_ZERO"
            };

            // Act
            errorClassifier.RegisterExceptionMapping(typeof(DivideByZeroException), customClassification);
            var classification = errorClassifier.ClassifyException(customException);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Data, classification.Category);
            Assert.Equal("DIVISION_BY_ZERO", classification.ErrorCode);
        }

        [Fact]
        public void RegisterExceptionMapping_WithNullType_ShouldThrowArgumentNullException()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var classification = new ErrorClassification();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => errorClassifier.RegisterExceptionMapping(null, classification));
        }

        [Fact]
        public void RegisterExceptionMapping_WithNullClassification_ShouldThrowArgumentNullException()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => errorClassifier.RegisterExceptionMapping(typeof(DivideByZeroException), null));
        }

        [Fact]
        public void RegisterCustomClassifier_WithValidClassifier_ShouldAddClassifier()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var customException = new CustomTestException("Test exception");
            var customClassification = new ErrorClassification
            {
                Category = ErrorCategory.Resource,
                Severity = ErrorSeverity.Warning,
                Strategy = RecoveryStrategy.Fallback,
                IsRetryable = true,
                MaxRetries = 2,
                RetryDelay = TimeSpan.FromSeconds(2),
                UserMessage = "Custom test error",
                TechnicalMessage = "Custom test exception occurred",
                ErrorCode = "CUSTOM_TEST_ERROR"
            };

            Func<Exception, ErrorClassification> classifier = ex =>
            {
                if (ex is CustomTestException)
                {
                    return customClassification;
                }
                return null;
            };

            // Act
            errorClassifier.RegisterCustomClassifier(classifier);
            var classification = errorClassifier.ClassifyException(customException);

            // Assert
            Assert.NotNull(classification);
            Assert.Equal(ErrorCategory.Resource, classification.Category);
            Assert.Equal("CUSTOM_TEST_ERROR", classification.ErrorCode);
        }

        [Fact]
        public void RegisterCustomClassifier_WithNullClassifier_ShouldThrowArgumentNullException()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => errorClassifier.RegisterCustomClassifier(null));
        }

        [Fact]
        public void GetRecommendedRetryPolicy_WithNonRetryableClassification_ShouldReturnZeroRetries()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var classification = new ErrorClassification
            {
                IsRetryable = false,
                MaxRetries = 0,
                RetryDelay = TimeSpan.Zero
            };

            // Act
            var policy = errorClassifier.GetRecommendedRetryPolicy(classification);

            // Assert
            Assert.NotNull(policy);
            Assert.Equal(0, policy.MaxRetries);
        }

        [Fact]
        public void GetRecommendedRetryPolicy_WithRetryableClassification_ShouldReturnConfiguredPolicy()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var classification = new ErrorClassification
            {
                IsRetryable = true,
                MaxRetries = 5,
                RetryDelay = TimeSpan.FromSeconds(2)
            };

            // Act
            var policy = errorClassifier.GetRecommendedRetryPolicy(classification);

            // Assert
            Assert.NotNull(policy);
            Assert.Equal(5, policy.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(2), policy.InitialDelay);
            Assert.Equal(RetryStrategyType.Exponential, policy.Strategy);
        }

        [Fact]
        public void ErrorClassification_ShouldHaveDefaultValues()
        {
            // Arrange
            var classification = new ErrorClassification();

            // Assert
            Assert.NotNull(classification.Context);
            Assert.Equal(default(DateTime), classification.Timestamp);
            Assert.Null(classification.SourceOperation);
            Assert.False(classification.RequiresImmediateNotification);
            Assert.True(classification.ShouldLog);
        }

        [Fact]
        public void ErrorClassification_Clone_ShouldCreateIndependentCopy()
        {
            // Arrange
            var original = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                UserMessage = "Test message",
                TechnicalMessage = "Technical message",
                ErrorCode = "TEST_CODE",
                Context = new Dictionary<string, object> { { "Key", "Value" } },
                SourceOperation = "TestOperation"
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.Category, clone.Category);
            Assert.Equal(original.Severity, clone.Severity);
            Assert.Equal(original.Strategy, clone.Strategy);
            Assert.Equal(original.IsRetryable, clone.IsRetryable);
            Assert.Equal(original.MaxRetries, clone.MaxRetries);
            Assert.Equal(original.RetryDelay, clone.RetryDelay);
            Assert.Equal(original.UserMessage, clone.UserMessage);
            Assert.Equal(original.TechnicalMessage, clone.TechnicalMessage);
            Assert.Equal(original.ErrorCode, clone.ErrorCode);
            Assert.Equal(original.SourceOperation, clone.SourceOperation);

            // Context should be a copy, not the same reference
            Assert.NotSame(original.Context, clone.Context);
            Assert.Equal(original.Context["Key"], clone.Context["Key"]);
        }

        [Fact]
        public void ClassifyException_WithInheritance_ShouldUseParentTypeMapping()
        {
            // Arrange
            var errorClassifier = new ErrorClassifier(_mockLogger.Object);
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Resource,
                Severity = ErrorSeverity.Warning,
                Strategy = RecoveryStrategy.Degrade,
                IsRetryable = false,
                UserMessage = "Custom exception message",
                TechnicalMessage = "Custom technical message",
                ErrorCode = "CUSTOM_EXCEPTION"
            };

            // Register mapping for parent type
            errorClassifier.RegisterExceptionMapping(typeof(Exception), classification);

            // Act - use child type
            var childException = new InvalidOperationException("Child exception");
            var result = errorClassifier.ClassifyException(childException);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ErrorCategory.Resource, result.Category);
            Assert.Equal("CUSTOM_EXCEPTION", result.ErrorCode);
        }

        private class CustomTestException : Exception
        {
            public CustomTestException(string message) : base(message)
            {
            }
        }
    }
}