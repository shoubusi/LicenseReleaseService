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
    /// Unit tests for TimerRecoveryManager
    /// </summary>
    public class TimerRecoveryManagerTests
    {
        private readonly Mock<ILogger<TimerRecoveryManager>> _mockLogger;
        private readonly Mock<ITimerErrorClassifier> _mockErrorClassifier;
        private readonly Mock<ITimerExecutionService> _mockTimerService;
        private readonly TimerRecoveryManager _recoveryManager;

        public TimerRecoveryManagerTests()
        {
            _mockLogger = new Mock<ILogger<TimerRecoveryManager>>();
            _mockErrorClassifier = new Mock<ITimerErrorClassifier>();
            _mockTimerService = new Mock<ITimerExecutionService>();

            _recoveryManager = new TimerRecoveryManager(
                _mockLogger.Object,
                _mockErrorClassifier.Object,
                _mockTimerService.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerRecoveryManager(
                null,
                _mockErrorClassifier.Object,
                _mockTimerService.Object));
        }

        [Fact]
        public void Constructor_WithNullErrorClassifier_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerRecoveryManager(
                _mockLogger.Object,
                null,
                _mockTimerService.Object));
        }

        [Fact]
        public void Constructor_WithNullTimerService_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerRecoveryManager(
                _mockLogger.Object,
                _mockErrorClassifier.Object,
                null));
        }

        [Fact]
        public void Constructor_WithValidDependencies_InitializesSuccessfully()
        {
            // Arrange & Act
            var recoveryManager = new TimerRecoveryManager(
                _mockLogger.Object,
                _mockErrorClassifier.Object,
                _mockTimerService.Object);

            // Assert
            Assert.NotNull(recoveryManager);
        }

        #endregion

        #region RecoverAsync Tests

        [Fact]
        public async Task RecoverAsync_WithNullErrorEventArgs_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _recoveryManager.RecoverAsync(null));
        }

        [Fact]
        public async Task RecoverAsync_WithRecoverableError_ReturnsSuccessfulResult()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

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

            // Act
            var result = await _recoveryManager.RecoverAsync(errorEventArgs);

            // Assert
            Assert.True(result.WasSuccessful);
            Assert.Equal(TimerRecoveryStatus.Success, result.Status);
            Assert.Equal(classification.RecoveryAction, result.ActionTaken);
            Assert.NotNull(result.RecoveryId);
        }

        [Fact]
        public async Task RecoverAsync_WithNonRecoverableError_ReturnsFailedResult()
        {
            // Arrange
            var exception = new OutOfMemoryException("Out of memory");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Resource,
                Severity = TimerErrorSeverity.Critical,
                RecoveryAction = TimerRecoveryAction.IncreaseInterval,
                IsRecoverable = false,
                IsTransient = false
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            // Act
            var result = await _recoveryManager.RecoverAsync(errorEventArgs);

            // Assert
            Assert.False(result.WasSuccessful);
            Assert.Equal(TimerRecoveryStatus.Failed, result.Status);
            Assert.Equal(classification.RecoveryAction, result.ActionTaken);
            Assert.NotNull(result.RecoveryId);
        }

        [Fact]
        public async Task RecoverAsync_WithRetryAction_ExecutesRetry()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

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

            // Act
            var result = await _recoveryManager.RecoverAsync(errorEventArgs);

            // Assert
            Assert.True(result.WasSuccessful);
            Assert.Equal(TimerRecoveryAction.Retry, result.ActionTaken);
            Assert.Contains("Retry", result.Details);
        }

        [Fact]
        public async Task RecoverAsync_WithIncreaseIntervalAction_ExecutesIntervalAdjustment()
        {
            // Arrange
            var exception = new InvalidOperationException("Resource constraint");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Resource,
                Severity = TimerErrorSeverity.Medium,
                RecoveryAction = TimerRecoveryAction.IncreaseInterval,
                IsRecoverable = true,
                IsTransient = true
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            // Act
            var result = await _recoveryManager.RecoverAsync(errorEventArgs);

            // Assert
            Assert.True(result.WasSuccessful);
            Assert.Equal(TimerRecoveryAction.IncreaseInterval, result.ActionTaken);
            Assert.Contains("Interval", result.Details);
        }

        [Fact]
        public async Task RecoverAsync_WithDisableTimerAction_DisablesTimer()
        {
            // Arrange
            var exception = new ArgumentException("Invalid configuration");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

            var classification = new TimerErrorClassification
            {
                Category = TimerErrorCategory.Configuration,
                Severity = TimerErrorSeverity.High,
                RecoveryAction = TimerRecoveryAction.DisableTimer,
                IsRecoverable = true,
                IsTransient = false
            };

            _mockErrorClassifier.Setup(c => c.ClassifyError(exception, It.IsAny<object>()))
                .ReturnsAsync(classification);

            // Act
            var result = await _recoveryManager.RecoverAsync(errorEventArgs);

            // Assert
            Assert.True(result.WasSuccessful);
            Assert.Equal(TimerRecoveryAction.DisableTimer, result.ActionTaken);
            Assert.Contains("Disable", result.Details);
        }

        [Fact]
        public async Task RecoverAsync_WithContext_PassesContextToClassifier()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);
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

            // Act
            var result = await _recoveryManager.RecoverAsync(errorEventArgs, context);

            // Assert
            Assert.True(result.WasSuccessful);
            _mockErrorClassifier.Verify(c => c.ClassifyError(exception, context), Times.Once);
        }

        [Fact]
        public async Task RecoverAsync_WithCancellation_CancelsOperation()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                _recoveryManager.RecoverAsync(errorEventArgs, null, cancellationTokenSource.Token));
        }

        #endregion

        #region GetRecoveryScenariosAsync Tests

        [Fact]
        public async Task GetRecoveryScenariosAsync_ReturnsDefaultScenarios()
        {
            // Act
            var scenarios = await _recoveryManager.GetRecoveryScenariosAsync();

            // Assert
            Assert.NotEmpty(scenarios);
            Assert.Contains(scenarios, s => s.Category == TimerErrorCategory.Timeout);
            Assert.Contains(scenarios, s => s.Category == TimerErrorCategory.Network);
            Assert.Contains(scenarios, s => s.Category == TimerErrorCategory.Resource);
        }

        [Fact]
        public async Task GetRecoveryScenariosAsync_WithCategoryFilter_ReturnsFilteredScenarios()
        {
            // Act
            var scenarios = await _recoveryManager.GetRecoveryScenariosAsync(TimerErrorCategory.Timeout);

            // Assert
            Assert.NotEmpty(scenarios);
            Assert.All(scenarios, s => Assert.Equal(TimerErrorCategory.Timeout, s.Category));
        }

        #endregion

        #region AddRecoveryScenarioAsync Tests

        [Fact]
        public async Task AddRecoveryScenarioAsync_WithValidScenario_AddsScenario()
        {
            // Arrange
            var scenario = new TimerRecoveryScenario
            {
                Id = Guid.NewGuid(),
                Name = "Test Scenario",
                Category = TimerErrorCategory.Process,
                Severity = TimerErrorSeverity.Medium,
                Action = TimerRecoveryAction.Custom,
                MaxAttempts = 3,
                DelayBetweenAttempts = TimeSpan.FromSeconds(5),
                Enabled = true
            };

            // Act
            await _recoveryManager.AddRecoveryScenarioAsync(scenario);

            // Assert
            var scenarios = await _recoveryManager.GetRecoveryScenariosAsync(TimerErrorCategory.Process);
            Assert.Contains(scenarios, s => s.Id == scenario.Id);
        }

        [Fact]
        public async Task AddRecoveryScenarioAsync_WithNullScenario_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _recoveryManager.AddRecoveryScenarioAsync(null));
        }

        [Fact]
        public async Task AddRecoveryScenarioAsync_WithDuplicateId_ThrowsArgumentException()
        {
            // Arrange
            var scenario = new TimerRecoveryScenario
            {
                Id = Guid.NewGuid(),
                Name = "Test Scenario",
                Category = TimerErrorCategory.Process,
                Severity = TimerErrorSeverity.Medium,
                Action = TimerRecoveryAction.Custom,
                MaxAttempts = 3,
                DelayBetweenAttempts = TimeSpan.FromSeconds(5),
                Enabled = true
            };

            await _recoveryManager.AddRecoveryScenarioAsync(scenario);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _recoveryManager.AddRecoveryScenarioAsync(scenario));
        }

        #endregion

        #region RemoveRecoveryScenarioAsync Tests

        [Fact]
        public async Task RemoveRecoveryScenarioAsync_WithExistingScenario_RemovesScenario()
        {
            // Arrange
            var scenario = new TimerRecoveryScenario
            {
                Id = Guid.NewGuid(),
                Name = "Test Scenario",
                Category = TimerErrorCategory.Process,
                Severity = TimerErrorSeverity.Medium,
                Action = TimerRecoveryAction.Custom,
                MaxAttempts = 3,
                DelayBetweenAttempts = TimeSpan.FromSeconds(5),
                Enabled = true
            };

            await _recoveryManager.AddRecoveryScenarioAsync(scenario);

            // Act
            await _recoveryManager.RemoveRecoveryScenarioAsync(scenario.Id);

            // Assert
            var scenarios = await _recoveryManager.GetRecoveryScenariosAsync(TimerErrorCategory.Process);
            Assert.DoesNotContain(scenarios, s => s.Id == scenario.Id);
        }

        [Fact]
        public async Task RemoveRecoveryScenarioAsync_WithNonexistentScenario_ThrowsKeyNotFoundException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _recoveryManager.RemoveRecoveryScenarioAsync(Guid.NewGuid()));
        }

        #endregion

        #region GetRecoveryHistoryAsync Tests

        [Fact]
        public async Task GetRecoveryHistoryAsync_WithoutHistory_ReturnsEmptyList()
        {
            // Act
            var history = await _recoveryManager.GetRecoveryHistoryAsync();

            // Assert
            Assert.Empty(history);
        }

        [Fact]
        public async Task GetRecoveryHistoryAsync_AfterRecovery_ReturnsHistoryEntries()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

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

            await _recoveryManager.RecoverAsync(errorEventArgs);

            // Act
            var history = await _recoveryManager.GetRecoveryHistoryAsync();

            // Assert
            Assert.Single(history);
            Assert.Equal(TimerRecoveryStatus.Success, history[0].Status);
            Assert.Equal(TimerRecoveryAction.Retry, history[0].ActionTaken);
        }

        [Fact]
        public async Task GetRecoveryHistoryAsync_WithTimeFilter_ReturnsFilteredHistory()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

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

            await _recoveryManager.RecoverAsync(errorEventArgs);

            // Act
            var recentHistory = await _recoveryManager.GetRecoveryHistoryAsync(since: DateTime.UtcNow.AddMinutes(-1));

            // Assert
            Assert.Single(recentHistory);
        }

        #endregion

        #region GetRecoveryStatisticsAsync Tests

        [Fact]
        public async Task GetRecoveryStatisticsAsync_WithoutHistory_ReturnsEmptyStatistics()
        {
            // Act
            var stats = await _recoveryManager.GetRecoveryStatisticsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalRecoveryAttempts);
            Assert.Equal(0, stats.SuccessfulRecoveries);
            Assert.Equal(0, stats.FailedRecoveries);
            Assert.Equal(0, stats.SuccessRate);
        }

        [Fact]
        public async Task GetRecoveryStatisticsAsync_AfterRecovery_ReturnsAccurateStatistics()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

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

            await _recoveryManager.RecoverAsync(errorEventArgs);

            // Act
            var stats = await _recoveryManager.GetRecoveryStatisticsAsync();

            // Assert
            Assert.Equal(1, stats.TotalRecoveryAttempts);
            Assert.Equal(1, stats.SuccessfulRecoveries);
            Assert.Equal(0, stats.FailedRecoveries);
            Assert.Equal(100, stats.SuccessRate);
        }

        #endregion

        #region ClearRecoveryHistoryAsync Tests

        [Fact]
        public async Task ClearRecoveryHistoryAsync_ClearsAllHistory()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

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

            await _recoveryManager.RecoverAsync(errorEventArgs);

            // Act
            await _recoveryManager.ClearRecoveryHistoryAsync();

            // Assert
            var history = await _recoveryManager.GetRecoveryHistoryAsync();
            Assert.Empty(history);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task RecoverAsync_TriggersRecoveryStartedEvent()
        {
            // Arrange
            var eventTriggered = false;
            TimerRecoveryEventArgs eventArgs = null;

            _recoveryManager.RecoveryStarted += (sender, args) =>
            {
                eventTriggered = true;
                eventArgs = args;
            };

            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

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

            // Act
            await _recoveryManager.RecoverAsync(errorEventArgs);

            // Assert
            Assert.True(eventTriggered);
            Assert.NotNull(eventArgs);
            Assert.NotNull(eventArgs.RecoveryId);
        }

        [Fact]
        public async Task RecoverAsync_TriggersRecoveryCompletedEvent()
        {
            // Arrange
            var eventTriggered = false;
            TimerRecoveryEventArgs eventArgs = null;

            _recoveryManager.RecoveryCompleted += (sender, args) =>
            {
                eventTriggered = true;
                eventArgs = args;
            };

            var exception = new TimeoutException("Operation timed out");
            var errorEventArgs = new TimerErrorEventArgs(exception, Guid.NewGuid(), TimerExecutionState.Running, 1);

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

            // Act
            await _recoveryManager.RecoverAsync(errorEventArgs);

            // Assert
            Assert.True(eventTriggered);
            Assert.NotNull(eventArgs);
            Assert.NotNull(eventArgs.RecoveryId);
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
    /// Interface for mocking TimerExecutionService
    /// </summary>
    public interface ITimerExecutionService
    {
        TimerExecutionState State { get; }
    }
}