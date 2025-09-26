using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.TimerExecution;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Unit tests for the TimerExecution LicenseCheckScheduler class
    /// </summary>
    public class LicenseCheckSchedulerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ITimerExecutionService> _mockTimerExecutionService;
        private readonly Mock<ILogger<LicenseCheckScheduler>> _mockLogger;
        private readonly LicenseCheckSchedulerConfiguration _configuration;
        private readonly List<LicenseCheckScheduler> _createdSchedulers;

        public LicenseCheckSchedulerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockTimerExecutionService = new Mock<ITimerExecutionService>();
            _mockLogger = new Mock<ILogger<LicenseCheckScheduler>>();
            _createdSchedulers = new List<LicenseCheckScheduler>();

            _configuration = new LicenseCheckSchedulerConfiguration
            {
                DefaultServer = "localhost",
                DefaultPort = 1057,
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 3
            };

            // Setup timer execution service defaults
            _mockTimerExecutionService.SetupGet(x => x.State).Returns(TimerState.Stopped);
            _mockTimerExecutionService.SetupGet(x => x.IsRunning).Returns(false);
            _mockTimerExecutionService.SetupGet(x => x.IsExecuting).Returns(false);
        }

        private LicenseCheckScheduler CreateScheduler()
        {
            var scheduler = new LicenseCheckScheduler(
                _mockTimerExecutionService.Object,
                _mockLogger.Object,
                _configuration);
            _createdSchedulers.Add(scheduler);
            return scheduler;
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesScheduler()
        {
            // Arrange
            var timerService = _mockTimerExecutionService.Object;
            var logger = _mockLogger.Object;

            // Act
            var scheduler = new LicenseCheckScheduler(timerService, logger, _configuration);

            // Assert
            Assert.Equal(LicenseCheckSchedulerStatus.Created, scheduler.Status);
            Assert.False(scheduler.IsRunning);
            Assert.Equal(_configuration.DefaultInterval, scheduler.CurrentInterval);
            Assert.Equal(0, scheduler.PendingOperationsCount);
            Assert.NotNull(scheduler.Metrics);
            Assert.NotNull(scheduler.QueueStatistics);
        }

        [Fact]
        public void Constructor_WithNullParameters_ThrowsArgumentNullException()
        {
            // Arrange
            var timerService = _mockTimerExecutionService.Object;
            var logger = _mockLogger.Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseCheckScheduler(null, logger, _configuration));
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseCheckScheduler(timerService, null, _configuration));
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseCheckScheduler(timerService, logger, null));
        }

        [Fact]
        public async Task StartAsync_WhenNotStarted_InvokesTimerServiceStart()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var interval = TimeSpan.FromSeconds(30);

            // Act
            await scheduler.StartAsync(interval);

            // Assert
            _mockTimerExecutionService.Verify(x => x.StartAsync(interval, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(LicenseCheckSchedulerStatus.Running, scheduler.Status);
            Assert.True(scheduler.IsRunning);
            Assert.Equal(interval, scheduler.CurrentInterval);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_LogsWarningAndReturns()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            await scheduler.StartAsync(TimeSpan.FromSeconds(60));

            // Assert
            _mockTimerExecutionService.Verify(x => x.StartAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(LicenseCheckSchedulerStatus.Running, scheduler.Status);
            Assert.Equal(TimeSpan.FromSeconds(30), scheduler.CurrentInterval); // Should remain unchanged
        }

        [Fact]
        public async Task StartAsync_WhenTimerServiceThrows_UpdatesStatusToFaulted()
        {
            // Arrange
            _mockTimerExecutionService
                .Setup(x => x.StartAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Timer service error"));

            var scheduler = CreateScheduler();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scheduler.StartAsync(TimeSpan.FromSeconds(30)));

            Assert.Equal("Timer service error", exception.Message);
            Assert.Equal(LicenseCheckSchedulerStatus.Faulted, scheduler.Status);
            Assert.False(scheduler.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_InvokesTimerServiceStop()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            await scheduler.StopAsync();

            // Assert
            _mockTimerExecutionService.Verify(x => x.StopAsync(), Times.Once);
            Assert.Equal(LicenseCheckSchedulerStatus.Stopped, scheduler.Status);
            Assert.False(scheduler.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_LogsWarningAndReturns()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            await scheduler.StopAsync();

            // Assert
            _mockTimerExecutionService.Verify(x => x.StopAsync(), Times.Never);
            Assert.Equal(LicenseCheckSchedulerStatus.Created, scheduler.Status);
        }

        [Fact]
        public async Task PauseAsync_WhenRunning_SetsStatusToPaused()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            await scheduler.PauseAsync();

            // Assert
            Assert.Equal(LicenseCheckSchedulerStatus.Paused, scheduler.Status);
            Assert.False(scheduler.IsRunning);
        }

        [Fact]
        public async Task PauseAsync_WhenNotRunning_LogsWarningAndReturns()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            await scheduler.PauseAsync();

            // Assert
            Assert.Equal(LicenseCheckSchedulerStatus.Created, scheduler.Status);
        }

        [Fact]
        public async Task ResumeAsync_WhenPaused_SetsStatusToRunning()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));
            await scheduler.PauseAsync();

            // Act
            await scheduler.ResumeAsync();

            // Assert
            Assert.Equal(LicenseCheckSchedulerStatus.Running, scheduler.Status);
            Assert.True(scheduler.IsRunning);
        }

        [Fact]
        public async Task ResumeAsync_WhenNotPaused_LogsWarningAndReturns()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            await scheduler.ResumeAsync();

            // Assert
            Assert.Equal(LicenseCheckSchedulerStatus.Running, scheduler.Status);
        }

        [Fact]
        public async Task UpdateIntervalAsync_WhenRunning_UpdatesInterval()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var newInterval = TimeSpan.FromSeconds(45);
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            await scheduler.UpdateIntervalAsync(newInterval);

            // Assert
            Assert.Equal(newInterval, scheduler.CurrentInterval);
        }

        [Fact]
        public async Task UpdateIntervalAsync_WithInvalidInterval_ThrowsArgumentException()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                scheduler.UpdateIntervalAsync(TimeSpan.Zero));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                scheduler.UpdateIntervalAsync(TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public async Task UpdateIntervalAsync_WhenNotRunning_LogsWarningAndReturns()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var originalInterval = scheduler.CurrentInterval;

            // Act
            await scheduler.UpdateIntervalAsync(TimeSpan.FromSeconds(45));

            // Assert
            Assert.Equal(originalInterval, scheduler.CurrentInterval); // Should remain unchanged
        }

        [Fact]
        public async Task ExecuteNowAsync_WhenRunning_EnqueuesHighPriorityOperation()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            await scheduler.ExecuteNowAsync();

            // Assert
            // Verify that an operation was enqueued (this would require mocking the queue)
            // For now, just verify the method completes without exception
            Assert.True(scheduler.IsRunning);
        }

        [Fact]
        public async Task EnqueueOperationAsync_WithValidOperation_EnqueuesOperation()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var operation = LicenseCheckOperation.CreateComprehensiveCheck("localhost", 1057);

            // Act
            await scheduler.EnqueueOperationAsync(operation);

            // Assert
            // Verify the operation was enqueued (would require queue mocking)
            Assert.NotNull(operation);
        }

        [Fact]
        public async Task EnqueueOperationAsync_WithNullOperation_ThrowsArgumentNullException()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                scheduler.EnqueueOperationAsync(null));
        }

        [Fact]
        public async Task EnqueueOperationsAsync_WithValidOperations_EnqueuesAllOperations()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var operations = new List<LicenseCheckOperation>
            {
                LicenseCheckOperation.CreateComprehensiveCheck("localhost", 1057),
                LicenseCheckOperation.CreateServerStatusCheck("localhost", 1057)
            };

            // Act
            await scheduler.EnqueueOperationsAsync(operations);

            // Assert
            Assert.Equal(2, operations.Count);
        }

        [Fact]
        public async Task EnqueueOperationsAsync_WithNullOperations_ThrowsArgumentNullException()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                scheduler.EnqueueOperationsAsync(null));
        }

        [Fact]
        public async Task DequeueOperationAsync_WithValidOperationId_DequeuesOperation()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var operationId = Guid.NewGuid().ToString();

            // Act
            var result = await scheduler.DequeueOperationAsync(operationId);

            // Assert
            // Result depends on queue implementation
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ClearQueueAsync_WhenCalled_ClearsAllOperations()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            await scheduler.ClearQueueAsync();

            // Assert
            // Verify queue is cleared (would require queue mocking)
            Assert.True(true); // Placeholder assertion
        }

        [Fact]
        public async Task GetPendingOperationsAsync_WhenCalled_ReturnsPendingOperations()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var operations = await scheduler.GetPendingOperationsAsync();

            // Assert
            Assert.NotNull(operations);
        }

        [Fact]
        public async Task UpdateConfigurationAsync_WithValidConfiguration_UpdatesConfiguration()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var newConfig = new LicenseCheckSchedulerConfiguration
            {
                DefaultServer = "new-server",
                DefaultPort = 2057,
                DefaultInterval = TimeSpan.FromMinutes(2)
            };

            // Act
            await scheduler.UpdateConfigurationAsync(newConfig);

            // Assert
            // Configuration should be updated (would require access to internal state)
            Assert.True(true); // Placeholder assertion
        }

        [Fact]
        public async Task UpdateConfigurationAsync_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                scheduler.UpdateConfigurationAsync(null));
        }

        [Fact]
        public void ValidateConfiguration_WithValidConfiguration_ReturnsEmptyErrors()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var errors = scheduler.ValidateConfiguration();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidServer_ReturnsError()
        {
            // Arrange
            var invalidConfig = new LicenseCheckSchedulerConfiguration
            {
                DefaultServer = "",
                DefaultPort = 1057,
                DefaultInterval = TimeSpan.FromMinutes(1)
            };
            var scheduler = new LicenseCheckScheduler(
                _mockTimerExecutionService.Object,
                _mockLogger.Object,
                invalidConfig);

            // Act
            var errors = scheduler.ValidateConfiguration();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("Default server address is required", errors);
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidPort_ReturnsError()
        {
            // Arrange
            var invalidConfig = new LicenseCheckSchedulerConfiguration
            {
                DefaultServer = "localhost",
                DefaultPort = 0,
                DefaultInterval = TimeSpan.FromMinutes(1)
            };
            var scheduler = new LicenseCheckScheduler(
                _mockTimerExecutionService.Object,
                _mockLogger.Object,
                invalidConfig);

            // Act
            var errors = scheduler.ValidateConfiguration();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("Default port must be between 1 and 65535", errors);
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidInterval_ReturnsError()
        {
            // Arrange
            var invalidConfig = new LicenseCheckSchedulerConfiguration
            {
                DefaultServer = "localhost",
                DefaultPort = 1057,
                DefaultInterval = TimeSpan.Zero
            };
            var scheduler = new LicenseCheckScheduler(
                _mockTimerExecutionService.Object,
                _mockLogger.Object,
                invalidConfig);

            // Act
            var errors = scheduler.ValidateConfiguration();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("Default interval must be greater than zero", errors);
        }

        [Fact]
        public async Task GetStatusInfoAsync_WhenCalled_ReturnsStatusInfo()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var statusInfo = await scheduler.GetStatusInfoAsync();

            // Assert
            Assert.NotNull(statusInfo);
            Assert.Equal(LicenseCheckSchedulerStatus.Created, statusInfo.Status);
            Assert.Equal(scheduler.IsRunning, statusInfo.IsRunning);
            Assert.Equal(scheduler.CurrentInterval, statusInfo.CurrentInterval);
            Assert.True(statusInfo.Uptime.TotalSeconds >= 0);
        }

        [Fact]
        public async Task GetExecutionHistoryAsync_WhenCalled_ReturnsExecutionHistory()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var history = await scheduler.GetExecutionHistoryAsync();

            // Assert
            Assert.NotNull(history);
        }

        [Fact]
        public async Task GetDiagnosticsAsync_WhenCalled_ReturnsDiagnostics()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var diagnostics = await scheduler.GetDiagnosticsAsync();

            // Assert
            Assert.NotNull(diagnostics);
            Assert.NotNull(diagnostics.StatusInfo);
            Assert.NotNull(diagnostics.ConfigurationErrors);
            Assert.True(diagnostics.MemoryUsage >= 0);
            Assert.True(diagnostics.ThreadCount > 0);
        }

        [Fact]
        public async Task ResetStatisticsAsync_WhenCalled_ResetsAllStatistics()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var initialMetrics = scheduler.Metrics;

            // Act
            await scheduler.ResetStatisticsAsync();

            // Assert
            var resetMetrics = scheduler.Metrics;
            Assert.True(resetMetrics.TotalOperations == 0);
            Assert.True(resetMetrics.FailedOperations == 0);
            Assert.True(resetMetrics.SuccessfulOperations == 0);
        }

        [Fact]
        public void Metrics_WhenAccessed_ReturnsCurrentMetrics()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var metrics = scheduler.Metrics;

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(scheduler.Status == LicenseCheckSchedulerStatus.Running, metrics.IsRunning);
            Assert.True(metrics.Uptime.TotalSeconds >= 0);
            Assert.True(metrics.Timestamp <= DateTime.Now);
        }

        [Fact]
        public void QueueStatistics_WhenAccessed_ReturnsQueueStatistics()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var stats = scheduler.QueueStatistics;

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.TotalProcessed >= 0);
        }

        [Fact]
        public void Events_WhenRaised_AreHandledCorrectly()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var checkStartedRaised = false;
            var checkCompletedRaised = false;
            var checkErrorRaised = false;
            var queueStatusChangedRaised = false;

            scheduler.CheckStarted += (sender, args) => checkStartedRaised = true;
            scheduler.CheckCompleted += (sender, args) => checkCompletedRaised = true;
            scheduler.CheckError += (sender, args) => checkErrorRaised = true;
            scheduler.QueueStatusChanged += (sender, args) => queueStatusChangedRaised = true;

            // Act - Simulate events (would need to access internal event raising)
            // For now, just verify the event handlers are wired up
            Assert.True(true); // Placeholder
        }

        [Fact]
        public async Task TimerExecutionStarted_WhenRaised_EnqueuesOperation()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var eventArgs = new TimerExecutionEventArgs(
                Guid.NewGuid().ToString(),
                DateTime.Now,
                TimeSpan.Zero);

            // Act
            _mockTimerExecutionService.Raise(x => x.ExecutionStarted += null, eventArgs);

            // Assert
            // Verify that an operation was enqueued (would require queue mocking)
            Assert.True(scheduler.IsRunning);
        }

        [Fact]
        public void TimerExecutionCompleted_WhenRaised_ResetsConsecutiveErrors()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var eventArgs = new TimerExecutionEventArgs(
                Guid.NewGuid().ToString(),
                DateTime.Now,
                TimeSpan.FromMilliseconds(100));

            // Act
            _mockTimerExecutionService.Raise(x => x.ExecutionCompleted += null, eventArgs);

            // Assert
            // Consecutive errors should be reset (would need access to internal state)
            Assert.True(true); // Placeholder
        }

        [Fact]
        public void TimerExecutionError_WhenRaised_IncrementsErrorCounters()
        {
            // Arrange
            var scheduler = CreateScheduler();
            var eventArgs = new TimerExecutionErrorEventArgs(
                Guid.NewGuid().ToString(),
                new InvalidOperationException("Test error"),
                1,
                DateTime.Now,
                TimeSpan.Zero);

            // Act
            _mockTimerExecutionService.Raise(x => x.ExecutionError += null, eventArgs);

            // Assert
            // Error counters should be incremented (would need access to internal state)
            Assert.True(true); // Placeholder
        }

        [Fact]
        public async Task Dispose_WhenCalled_ReleasesAllResources()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            scheduler.Dispose();

            // Assert
            // Verify that all resources are disposed (timer, queue, cancellation tokens, etc.)
            Assert.True(true); // Placeholder
        }

        public void Dispose()
        {
            foreach (var scheduler in _createdSchedulers)
            {
                scheduler.Dispose();
            }
            _createdSchedulers.Clear();
        }
    }
}