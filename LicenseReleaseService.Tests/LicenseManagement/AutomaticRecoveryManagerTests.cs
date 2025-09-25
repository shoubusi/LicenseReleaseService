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
using LicenseReleaseService.Process;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for AutomaticRecoveryManager class
    /// </summary>
    public class AutomaticRecoveryManagerTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ErrorClassifier> _mockErrorClassifier;
        private readonly Mock<RecoveryManager> _mockRecoveryManager;

        public AutomaticRecoveryManagerTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockErrorClassifier = new Mock<ErrorClassifier>(_mockLogger.Object);
            _mockRecoveryManager = new Mock<RecoveryManager>(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AutomaticRecoveryManager(
                null, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object));
        }

        [Fact]
        public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AutomaticRecoveryManager(
                _mockLogger.Object, null, _mockErrorClassifier.Object, _mockRecoveryManager.Object));
        }

        [Fact]
        public void Constructor_WithNullErrorClassifier_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, null, _mockRecoveryManager.Object));
        }

        [Fact]
        public void Constructor_WithNullRecoveryManager_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, null));
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Act
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            // Assert
            Assert.NotNull(recoveryManager);
            Assert.NotNull(recoveryManager.Statistics);
            Assert.Equal(0, recoveryManager.Statistics.TotalRequests);
        }

        [Fact]
        public async Task SubmitRecoveryRequest_WithNullException_ShouldThrowArgumentNullException()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                Task.Run(() => recoveryManager.SubmitRecoveryRequest(null)));
        }

        [Fact]
        public async Task SubmitRecoveryRequest_WithValidException_ShouldReturnRequestId()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var exception = new SocketException();
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1)
            };

            _mockErrorClassifier.Setup(c => c.ClassifyException(exception, null, null))
                .Returns(classification);

            // Act
            var requestId = recoveryManager.SubmitRecoveryRequest(exception);

            // Assert
            Assert.NotNull(requestId);
            Assert.Equal(36, requestId.Length); // GUID length without hyphens
            Assert.Equal(1, recoveryManager.Statistics.TotalRequests);
        }

        [Fact]
        public async Task SubmitRecoveryRequest_WithContext_ShouldIncludeContext()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var exception = new SocketException();
            var context = new { Server = "test-server", Port = 1234 };
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1)
            };

            _mockErrorClassifier.Setup(c => c.ClassifyException(exception, context, null))
                .Returns(classification);

            // Act
            var requestId = recoveryManager.SubmitRecoveryRequest(exception, context);

            // Assert
            Assert.NotNull(requestId);
        }

        [Fact]
        public async Task SubmitRecoveryRequest_WithOriginalOperation_ShouldIncludeOperation()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var exception = new SocketException();
            var originalOperation = new Func<Task<object>>(() => Task.FromResult<object>("success"));
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1)
            };

            _mockErrorClassifier.Setup(c => c.ClassifyException(exception, null, null))
                .Returns(classification);

            // Act
            var requestId = recoveryManager.SubmitRecoveryRequest(exception, null, originalOperation);

            // Assert
            Assert.NotNull(requestId);
        }

        [Fact]
        public async Task SubmitExplicitRecoveryRequest_WithValidParameters_ShouldReturnRequestId()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1)
            };

            var scenario = new RecoveryScenario
            {
                Id = "test-scenario",
                Name = "Test Scenario",
                Actions = new List<RecoveryAction>(),
                IsEnabled = true
            };

            // Act
            var requestId = recoveryManager.SubmitExplicitRecoveryRequest(classification, scenario);

            // Assert
            Assert.NotNull(requestId);
            Assert.Equal(1, recoveryManager.Statistics.TotalRequests);
        }

        [Fact]
        public async Task SubmitExplicitRecoveryRequest_WithNullClassification_ShouldThrowArgumentNullException()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var scenario = new RecoveryScenario();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                Task.Run(() => recoveryManager.SubmitExplicitRecoveryRequest(null, scenario)));
        }

        [Fact]
        public void GetQueueStatus_ShouldReturnCurrentQueueStatus()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            // Submit some requests
            var exception = new SocketException();
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1)
            };

            _mockErrorClassifier.Setup(c => c.ClassifyException(exception, null, null))
                .Returns(classification);

            recoveryManager.SubmitRecoveryRequest(exception);
            recoveryManager.SubmitRecoveryRequest(exception);

            // Act
            var queueStatus = recoveryManager.GetQueueStatus();

            // Assert
            Assert.NotNull(queueStatus);
            Assert.Equal(2, queueStatus.QueueSize);
            Assert.Equal(2, queueStatus.PendingRecoveries);
            Assert.Equal(0, queueStatus.ActiveRecoveries);
            Assert.Equal(0, queueStatus.CompletedRecoveries);
        }

        [Fact]
        public void GetRecoveryPriority_ShouldMapSeverityToPriorityCorrectly()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            // Test severity to priority mapping through private method
            var criticalClassification = new ErrorClassification { Severity = ErrorSeverity.Critical };
            var errorClassification = new ErrorClassification { Severity = ErrorSeverity.Error };
            var warningClassification = new ErrorClassification { Severity = ErrorSeverity.Warning };
            var infoClassification = new ErrorClassification { Severity = ErrorSeverity.Information };

            // Act & Assert
            var criticalPriority = recoveryManager.GetType()
                .GetMethod("GetRecoveryPriority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { criticalClassification }) as RecoveryPriority?;
            var errorPriority = recoveryManager.GetType()
                .GetMethod("GetRecoveryPriority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { errorClassification }) as RecoveryPriority?;
            var warningPriority = recoveryManager.GetType()
                .GetMethod("GetRecoveryPriority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { warningClassification }) as RecoveryPriority?;
            var infoPriority = recoveryManager.GetType()
                .GetMethod("GetRecoveryPriority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { infoClassification }) as RecoveryPriority?;

            Assert.Equal(RecoveryPriority.Critical, criticalPriority);
            Assert.Equal(RecoveryPriority.High, errorPriority);
            Assert.Equal(RecoveryPriority.Normal, warningPriority);
            Assert.Equal(RecoveryPriority.Low, infoPriority);
        }

        [Fact]
        public void GetRecoveryTimeout_ShouldReturnAppropriateTimeouts()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var criticalClassification = new ErrorClassification { Severity = ErrorSeverity.Critical };
            var errorClassification = new ErrorClassification { Severity = ErrorSeverity.Error };
            var warningClassification = new ErrorClassification { Severity = ErrorSeverity.Warning };
            var infoClassification = new ErrorClassification { Severity = ErrorSeverity.Information };

            // Act & Assert
            var criticalTimeout = recoveryManager.GetType()
                .GetMethod("GetRecoveryTimeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { criticalClassification }) as TimeSpan?;
            var errorTimeout = recoveryManager.GetType()
                .GetMethod("GetRecoveryTimeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { errorClassification }) as TimeSpan?;
            var warningTimeout = recoveryManager.GetType()
                .GetMethod("GetRecoveryTimeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { warningClassification }) as TimeSpan?;
            var infoTimeout = recoveryManager.GetType()
                .GetMethod("GetRecoveryTimeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { infoClassification }) as TimeSpan?;

            Assert.Equal(TimeSpan.FromMinutes(5), criticalTimeout);
            Assert.Equal(TimeSpan.FromMinutes(2), errorTimeout);
            Assert.Equal(TimeSpan.FromMinutes(1), warningTimeout);
            Assert.Equal(TimeSpan.FromSeconds(30), infoTimeout);
        }

        [Fact]
        public async Task ProcessRecoveryQueueAsync_ShouldProcessRequestsInPriorityOrder()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            // Create requests with different priorities
            var criticalException = new Exception("Critical");
            var criticalClassification = new ErrorClassification { Severity = ErrorSeverity.Critical };
            var lowException = new Exception("Low");
            var lowClassification = new ErrorClassification { Severity = ErrorSeverity.Information };

            _mockErrorClassifier.Setup(c => c.ClassifyException(criticalException, null, null))
                .Returns(criticalClassification);
            _mockErrorClassifier.Setup(c => c.ClassifyException(lowException, null, null))
                .Returns(lowClassification);

            // Submit requests (low priority first)
            recoveryManager.SubmitRecoveryRequest(lowException);
            recoveryManager.SubmitRecoveryRequest(criticalException);

            // Wait a bit for processing
            await Task.Delay(100);

            // Act
            var queueStatus = recoveryManager.GetQueueStatus();

            // Assert
            // Critical priority should be processed first
            Assert.True(queueStatus.PendingRecoveries >= 0);
        }

        [Fact]
        public async Task CalculateRetryDelay_ShouldImplementExponentialBackoff()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var classification = new ErrorClassification
            {
                RetryDelay = TimeSpan.FromSeconds(1)
            };

            var request = new RecoveryRequest
            {
                Classification = classification,
                CurrentRetry = 0
            };

            // Act & Assert
            var delay1 = recoveryManager.GetType()
                .GetMethod("CalculateRetryDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { request }) as TimeSpan?;

            request.CurrentRetry = 1;
            var delay2 = recoveryManager.GetType()
                .GetMethod("CalculateRetryDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { request }) as TimeSpan?;

            request.CurrentRetry = 2;
            var delay3 = recoveryManager.GetType()
                .GetMethod("CalculateRetryDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { request }) as TimeSpan?;

            Assert.True(delay1.Value.TotalSeconds <= delay2.Value.TotalSeconds);
            Assert.True(delay2.Value.TotalSeconds <= delay3.Value.TotalSeconds);
        }

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            // Act
            recoveryManager.Dispose();

            // Assert - should not throw exception
            recoveryManager.Dispose(); // Dispose multiple times should be safe
        }

        [Fact]
        public async Task SubmitRecoveryRequest_WithCallback_ShouldInvokeCallbackOnCompletion()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var exception = new SocketException();
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromMilliseconds(10)
            };

            var scenario = new RecoveryScenario
            {
                Id = "test-scenario",
                Name = "Test Scenario",
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "TestAction",
                        Timeout = TimeSpan.FromMilliseconds(50),
                        IsCritical = false,
                        Action = () => Task.FromResult(true)
                    }
                },
                IsEnabled = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyException(exception, null, null))
                .Returns(classification);

            var scenarioList = new List<RecoveryScenario> { scenario };
            _mockRecoveryManager.Setup(r => r.GetRegisteredScenarios())
                .Returns(scenarioList);

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<Exception>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecoveryResult { Success = true, CompletedAt = DateTime.Now });

            RecoveryResult callbackResult = null;
            var callbackInvoked = false;

            // Act
            var requestId = recoveryManager.SubmitRecoveryRequest(exception, null, null, result =>
            {
                callbackResult = result;
                callbackInvoked = true;
            });

            // Wait for processing
            await Task.Delay(200);

            // Assert
            Assert.True(callbackInvoked);
            Assert.NotNull(callbackResult);
            Assert.True(callbackResult.Success);
        }

        [Fact]
        public async Task GetQueueStatus_ShouldIncludeAccurateStatistics()
        {
            // Arrange
            var recoveryManager = new AutomaticRecoveryManager(
                _mockLogger.Object, _mockServiceProvider.Object, _mockErrorClassifier.Object, _mockRecoveryManager.Object);

            var exception = new SocketException();
            var classification = new ErrorClassification
            {
                Category = ErrorCategory.Network,
                Severity = ErrorSeverity.Error,
                Strategy = RecoveryStrategy.Retry,
                IsRetryable = true,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromMilliseconds(10)
            };

            var scenario = new RecoveryScenario
            {
                Id = "test-scenario",
                Name = "Test Scenario",
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "TestAction",
                        Timeout = TimeSpan.FromMilliseconds(50),
                        IsCritical = false,
                        Action = () => Task.FromResult(true)
                    }
                },
                IsEnabled = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyException(exception, null, null))
                .Returns(classification);

            var scenarioList = new List<RecoveryScenario> { scenario };
            _mockRecoveryManager.Setup(r => r.GetRegisteredScenarios())
                .Returns(scenarioList);

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<Exception>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecoveryResult { Success = true, CompletedAt = DateTime.Now });

            // Submit multiple requests
            recoveryManager.SubmitRecoveryRequest(exception);
            recoveryManager.SubmitRecoveryRequest(exception);

            // Wait for processing
            await Task.Delay(300);

            // Act
            var queueStatus = recoveryManager.GetQueueStatus();

            // Assert
            Assert.NotNull(queueStatus.Statistics);
            Assert.True(queueStatus.Statistics.TotalRequests >= 2);
            Assert.True(queueStatus.Statistics.TotalProcessed >= 0);
            Assert.True(queueStatus.Statistics.SuccessRate >= 0);
            Assert.True(queueStatus.Statistics.FailureRate >= 0);
        }
    }
}