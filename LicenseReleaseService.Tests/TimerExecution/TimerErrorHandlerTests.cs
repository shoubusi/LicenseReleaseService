using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Unit tests for TimerErrorHandler
    /// </summary>
    public class TimerErrorHandlerTests
    {
        private readonly Mock<ILogger<TimerErrorHandler>> _mockLogger;
        private readonly Mock<ITimerErrorClassifier> _mockErrorClassifier;
        private readonly Mock<ITimerRecoveryManager> _mockRecoveryManager;
        private readonly Mock<ITimerCircuitBreaker> _mockCircuitBreaker;
        private readonly Mock<ITimerHealthMonitor> _mockHealthMonitor;
        private readonly TimerErrorHandler _errorHandler;

        public TimerErrorHandlerTests()
        {
            _mockLogger = new Mock<ILogger<TimerErrorHandler>>();
            _mockErrorClassifier = new Mock<ITimerErrorClassifier>();
            _mockRecoveryManager = new Mock<ITimerRecoveryManager>();
            _mockCircuitBreaker = new Mock<ITimerCircuitBreaker>();
            _mockHealthMonitor = new Mock<ITimerHealthMonitor>();

            _errorHandler = new TimerErrorHandler(
                _mockLogger.Object,
                _mockErrorClassifier.Object,
                _mockRecoveryManager.Object,
                _mockCircuitBreaker.Object,
                _mockHealthMonitor.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerErrorHandler(
                null,
                _mockErrorClassifier.Object,
                _mockRecoveryManager.Object,
                _mockCircuitBreaker.Object,
                _mockHealthMonitor.Object));
        }

        [Fact]
        public void Constructor_WithNullErrorClassifier_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerErrorHandler(
                _mockLogger.Object,
                null,
                _mockRecoveryManager.Object,
                _mockCircuitBreaker.Object,
                _mockHealthMonitor.Object));
        }

        [Fact]
        public void Constructor_WithNullRecoveryManager_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerErrorHandler(
                _mockLogger.Object,
                _mockErrorClassifier.Object,
                null,
                _mockCircuitBreaker.Object,
                _mockHealthMonitor.Object));
        }

        [Fact]
        public void Constructor_WithNullCircuitBreaker_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerErrorHandler(
                _mockLogger.Object,
                _mockErrorClassifier.Object,
                _mockRecoveryManager.Object,
                null,
                _mockHealthMonitor.Object));
        }

        [Fact]
        public void Constructor_WithNullHealthMonitor_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerErrorHandler(
                _mockLogger.Object,
                _mockErrorClassifier.Object,
                _mockRecoveryManager.Object,
                _mockCircuitBreaker.Object,
                null));
        }

        [Fact]
        public void Constructor_WithValidDependencies_InitializesSuccessfully()
        {
            // Arrange & Act
            var errorHandler = new TimerErrorHandler(
                _mockLogger.Object,
                _mockErrorClassifier.Object,
                _mockRecoveryManager.Object,
                _mockCircuitBreaker.Object,
                _mockHealthMonitor.Object);

            // Assert
            Assert.NotNull(errorHandler);
        }

        #endregion

        #region HandleErrorAsync Tests

        [Fact]
        public async Task HandleErrorAsync_WithNullException_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _errorHandler.HandleErrorAsync(null));
        }

        [Fact]
        public async Task HandleErrorAsync_WithRecoverableError_AttemptsRecovery()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception);

            // Assert
            Assert.True(result.WasHandled);
            Assert.True(result.WasRecoveryAttempted);
            Assert.True(result.WasRecoverySuccessful);
            Assert.Equal(TimerErrorSeverity.Low, result.Severity);
            Assert.Equal(TimerErrorCategory.Timeout, result.Category);
            Assert.NotNull(result.ErrorId);
            Assert.NotNull(result.ExecutionId);

            _mockRecoveryManager.Verify(r => r.RecoverAsync(
                It.Is<TimerErrorEventArgs>(e => e.Exception == exception),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleErrorAsync_WithNonRecoverableError_LogsAndContinues()
        {
            // Arrange
            var exception = new OutOfMemoryException("Out of memory");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Resource,
                Severity = TimerErrorSeverity.Critical,
                RecoveryAction = TimerRecoveryAction.LogAndContinue,
                IsRecoverable = false,
                IsTransient = false
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception);

            // Assert
            Assert.True(result.WasHandled);
            Assert.False(result.WasRecoveryAttempted);
            Assert.False(result.WasRecoverySuccessful);
            Assert.Equal(TimerErrorSeverity.Critical, result.Severity);
            Assert.Equal(TimerErrorCategory.Resource, result.Category);

            _mockRecoveryManager.Verify(r => r.RecoverAsync(
                It.IsAny<TimerErrorEventArgs>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleErrorAsync_WithCriticalError_TriggersCircuitBreaker()
        {
            // Arrange
            var exception = new InvalidOperationException("Critical failure");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Configuration,
                Severity = TimerErrorSeverity.Critical,
                RecoveryAction = TimerRecoveryAction.EnableCircuitBreaker,
                IsRecoverable = false,
                IsTransient = false,
                ShouldTriggerCircuitBreaker = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception);

            // Assert
            Assert.True(result.WasHandled);
            Assert.False(result.WasRecoveryAttempted);
            Assert.Equal(TimerErrorSeverity.Critical, result.Severity);
            Assert.True(result.ShouldTriggerCircuitBreaker);

            _mockCircuitBreaker.Verify(c => c.RecordFailureAsync(exception, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleErrorAsync_WithExecutionId_PassesExecutionIdThrough()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var executionId = Guid.NewGuid();
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception, executionId);

            // Assert
            Assert.Equal(executionId, result.ExecutionId);
        }

        [Fact]
        public async Task HandleErrorAsync_WithContext_PassesContextToClassifier()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var context = new { TestProperty = "TestValue" };
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, context))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), context, It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception, null, context);

            // Assert
            Assert.True(result.WasHandled);
            _mockErrorClassifier.Verify(c => c.ClassifyError(exception, context), Times.Once);
        }

        [Fact]
        public async Task HandleErrorAsync_WithCancellation_CancelsOperation()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                _errorHandler.HandleErrorAsync(exception, null, null, cancellationTokenSource.Token));
        }

        #endregion

        #region GetErrorStatisticsAsync Tests

        [Fact]
        public async Task GetErrorStatisticsAsync_WithoutErrors_ReturnsEmptyStatistics()
        {
            // Act
            var stats = await _errorHandler.GetErrorStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalErrors);
            Assert.Equal(0, stats.HandledErrors);
            Assert.Equal(0, stats.UnhandledErrors);
            Assert.Equal(0, stats.RecoveryAttempts);
            Assert.Equal(0, stats.SuccessfulRecoveries);
            Assert.Equal(0, stats.FailedRecoveries);
        }

        [Fact]
        public async Task GetErrorStatisticsAsync_AfterHandlingErrors_ReturnsAccurateStatistics()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            await _errorHandler.HandleErrorAsync(exception);

            // Act
            var stats = await _errorHandler.GetErrorStatisticsAsync();

            // Assert
            Assert.Equal(1, stats.TotalErrors);
            Assert.Equal(1, stats.HandledErrors);
            Assert.Equal(0, stats.UnhandledErrors);
            Assert.Equal(1, stats.RecoveryAttempts);
            Assert.Equal(1, stats.SuccessfulRecoveries);
            Assert.Equal(0, stats.FailedRecoveries);
            Assert.Equal(100, stats.RecoverySuccessRate);
        }

        #endregion

        #region GetErrorHistoryAsync Tests

        [Fact]
        public async Task GetErrorHistoryAsync_WithoutHistory_ReturnsEmptyList()
        {
            // Act
            var history = await _errorHandler.GetErrorHistoryAsync();

            // Assert
            Assert.Empty(history);
        }

        [Fact]
        public async Task GetErrorHistoryAsync_AfterHandlingErrors_ReturnsHistoryEntries()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            await _errorHandler.HandleErrorAsync(exception);

            // Act
            var history = await _errorHandler.GetErrorHistoryAsync();

            // Assert
            Assert.Single(history);
            Assert.Equal(TimerErrorCategory.Timeout, history[0].Category);
            Assert.Equal(TimerErrorSeverity.Low, history[0].Severity);
            Assert.True(history[0].WasHandled);
            Assert.True(history[0].WasRecoverySuccessful);
        }

        [Fact]
        public async Task GetErrorHistoryAsync_WithCategoryFilter_ReturnsFilteredHistory()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            await _errorHandler.HandleErrorAsync(exception);

            // Act
            var timeoutHistory = await _errorHandler.GetErrorHistoryAsync(category: TimerErrorCategory.Timeout);

            // Assert
            Assert.Single(timeoutHistory);
            Assert.All(timeoutHistory, e => Assert.Equal(TimerErrorCategory.Timeout, e.Category));
        }

        #endregion

        #region ClearErrorHistoryAsync Tests

        [Fact]
        public async Task ClearErrorHistoryAsync_ClearsAllHistory()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            await _errorHandler.HandleErrorAsync(exception);

            // Act
            await _errorHandler.ClearErrorHistoryAsync();

            // Assert
            var history = await _errorHandler.GetErrorHistoryAsync();
            Assert.Empty(history);
        }

        #endregion

        #region Configure Tests

        [Fact]
        public void Configure_WithValidConfiguration_UpdatesSettings()
        {
            // Arrange
            var config = new TimerErrorHandlerConfiguration
            {
                EnableRecovery = true,
                EnableCircuitBreaker = true,
                EnableHealthMonitoring = true,
                MaxRecoveryAttempts = 5,
                RecoveryDelay = TimeSpan.FromSeconds(2),
                LogAllErrors = true,
                TrackErrorHistory = true,
                MaxErrorHistory = 1000
            };

            // Act
            _errorHandler.Configure(config);
            var currentConfig = _errorHandler.GetConfiguration();

            // Assert
            Assert.Equal(config.EnableRecovery, currentConfig.EnableRecovery);
            Assert.Equal(config.EnableCircuitBreaker, currentConfig.EnableCircuitBreaker);
            Assert.Equal(config.EnableHealthMonitoring, currentConfig.EnableHealthMonitoring);
            Assert.Equal(config.MaxRecoveryAttempts, currentConfig.MaxRecoveryAttempts);
            Assert.Equal(config.RecoveryDelay, currentConfig.RecoveryDelay);
            Assert.Equal(config.LogAllErrors, currentConfig.LogAllErrors);
            Assert.Equal(config.TrackErrorHistory, currentConfig.TrackErrorHistory);
            Assert.Equal(config.MaxErrorHistory, currentConfig.MaxErrorHistory);
        }

        [Fact]
        public void Configure_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _errorHandler.Configure(null));
        }

        [Fact]
        public void GetConfiguration_ReturnsCurrentConfiguration()
        {
            // Act
            var config = _errorHandler.GetConfiguration();

            // Assert
            Assert.NotNull(config);
            Assert.True(config.EnableRecovery);
            Assert.True(config.EnableCircuitBreaker);
            Assert.True(config.EnableHealthMonitoring);
            Assert.Equal(3, config.MaxRecoveryAttempts);
            Assert.Equal(TimeSpan.FromSeconds(1), config.RecoveryDelay);
            Assert.True(config.LogAllErrors);
            Assert.True(config.TrackErrorHistory);
            Assert.Equal(100, config.MaxErrorHistory);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task HandleErrorAsync_TriggersErrorHandledEvent()
        {
            // Arrange
            var eventTriggered = false;
            TimerErrorEventArgs eventArgs = null;

            _errorHandler.ErrorHandled += (sender, args) =>
            {
                eventTriggered = true;
                eventArgs = args;
            };

            var exception = new TimeoutException("Operation timed out");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            // Act
            await _errorHandler.HandleErrorAsync(exception);

            // Assert
            Assert.True(eventTriggered);
            Assert.NotNull(eventArgs);
            Assert.Equal(exception, eventArgs.Exception);
            Assert.Equal(TimerErrorCategory.Timeout, eventArgs.Category);
            Assert.Equal(TimerErrorSeverity.Low, eventArgs.Severity);
        }

        [Fact]
        public async Task HandleErrorAsync_WhenRecoveryAttempted_TriggersRecoveryAttemptedEvent()
        {
            // Arrange
            var eventTriggered = false;
            TimerRecoveryEventArgs eventArgs = null;

            _errorHandler.RecoveryAttempted += (sender, args) =>
            {
                eventTriggered = true;
                eventArgs = args;
            };

            var exception = new TimeoutException("Operation timed out");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            // Act
            await _errorHandler.HandleErrorAsync(exception);

            // Assert
            Assert.True(eventTriggered);
            Assert.NotNull(eventArgs);
            Assert.NotNull(eventArgs.RecoveryId);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task HandleErrorAsync_WithMultipleErrors_HandlesAllErrors()
        {
            // Arrange
            var exceptions = new Exception[]
            {
                new TimeoutException("Timeout 1"),
                new TimeoutException("Timeout 2"),
                new InvalidOperationException("Invalid operation")
            };

            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Timeout,
                Severity = TimerErrorSeverity.Low,
                RecoveryAction = TimerRecoveryAction.Retry,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(It.IsAny<Exception>(), It.IsAny<object>()))
                .ReturnsAsync(classification);

            var recoveryResult = new TimerRecoveryResult
            {
                RecoveryId = Guid.NewGuid(),
                WasSuccessful = true,
                Status = TimerRecoveryStatus.Success,
                ActionTaken = TimerRecoveryAction.Retry,
                Timestamp = DateTime.UtcNow
            };

            _mockRecoveryManager.Setup(r => r.RecoverAsync(It.IsAny<TimerErrorEventArgs>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recoveryResult);

            // Act
            var results = new List<TimerErrorHandlingResult>();
            foreach (var exception in exceptions)
            {
                var result = await _errorHandler.HandleErrorAsync(exception);
                results.Add(result);
            }

            // Assert
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.WasHandled));
            Assert.All(results, r => Assert.True(r.WasRecoverySuccessful));

            var stats = await _errorHandler.GetErrorStatisticsAsync();
            Assert.Equal(3, stats.TotalErrors);
            Assert.Equal(3, stats.HandledErrors);
        }

        [Fact]
        public async Task HandleErrorAsync_WithCriticalError_CoordinatesWithCircuitBreaker()
        {
            // Arrange
            var exception = new InvalidOperationException("Critical failure");
            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Configuration,
                Severity = TimerErrorSeverity.Critical,
                RecoveryAction = TimerRecoveryAction.EnableCircuitBreaker,
                IsRecoverable = false,
                IsTransient = false,
                ShouldTriggerCircuitBreaker = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception);

            // Assert
            Assert.True(result.WasHandled);
            Assert.False(result.WasRecoveryAttempted);
            Assert.True(result.ShouldTriggerCircuitBreaker);

            _mockCircuitBreaker.Verify(c => c.RecordFailureAsync(exception, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }

    /// <summary>
    /// Interface for mocking TimerErrorClassifier
    /// </summary>
    public interface ITimerErrorClassifier
    {
        Task<TimerErrorClassification> ClassifyError(Exception exception, object context = null);
    }

    /// <summary>
    /// Interface for mocking TimerRecoveryManager
    /// </summary>
    public interface ITimerRecoveryManager
    {
        Task<TimerRecoveryResult> RecoverAsync(TimerErrorEventArgs errorEventArgs, object context = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for mocking TimerCircuitBreaker
    /// </summary>
    public interface ITimerCircuitBreaker
    {
        Task RecordFailureAsync(Exception exception, Guid? operationId = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for mocking TimerHealthMonitor
    /// </summary>
    public interface ITimerHealthMonitor
    {
    }
}