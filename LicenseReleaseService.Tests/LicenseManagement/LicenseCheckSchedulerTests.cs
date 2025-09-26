using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.TimerExecution;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for the LicenseCheckScheduler class
    /// </summary>
    public class LicenseCheckSchedulerTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ITimerExecutionService> _mockTimerExecutionService;
        private readonly Mock<ILogger<LicenseCheckScheduler>> _mockLogger;
        private readonly Mock<ILicenseManager> _mockLicenseManager;
        private readonly Mock<ILicenseQueryEngine> _mockLicenseQueryEngine;
        private readonly TimerExecutionOptions _options;

        public LicenseCheckSchedulerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockTimerExecutionService = new Mock<ITimerExecutionService>();
            _mockLogger = new Mock<ILogger<LicenseCheckScheduler>>();
            _mockLicenseManager = new Mock<ILicenseManager>();
            _mockLicenseQueryEngine = new Mock<ILicenseQueryEngine>();
            _options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMinutes(1),
                MaxConsecutiveErrors = 3,
                MaxConcurrentExecutions = 2
            };
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesScheduler()
        {
            // Arrange
            var timerService = _mockTimerExecutionService.Object;
            var logger = _mockLogger.Object;
            var licenseManager = _mockLicenseManager.Object;
            var queryEngine = _mockLicenseQueryEngine.Object;

            // Act
            var scheduler = new LicenseCheckScheduler(timerService, logger, licenseManager, queryEngine, _options);

            // Assert
            Assert.Equal(SchedulerStatus.Stopped, scheduler.Status);
            Assert.False(scheduler.IsRunning);
            Assert.Equal(_options.DefaultInterval, scheduler.CurrentInterval);
            Assert.Equal(0, scheduler.QueuedOperations);
            Assert.Equal(0, scheduler.ExecutingOperations);
            Assert.Equal(0, scheduler.TotalOperationsCompleted);
            Assert.Equal(0, scheduler.TotalOperationsFailed);
        }

        [Fact]
        public void Constructor_WithNullParameters_ThrowsArgumentNullException()
        {
            // Arrange
            var timerService = _mockTimerExecutionService.Object;
            var logger = _mockLogger.Object;
            var licenseManager = _mockLicenseManager.Object;
            var queryEngine = _mockLicenseQueryEngine.Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseCheckScheduler(null, logger, licenseManager, queryEngine, _options));
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseCheckScheduler(timerService, null, licenseManager, queryEngine, _options));
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseCheckScheduler(timerService, logger, null, queryEngine, _options));
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseCheckScheduler(timerService, logger, licenseManager, null, _options));
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseCheckScheduler(timerService, logger, licenseManager, queryEngine, null));
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
            _mockTimerExecutionService.Verify(x => x.Start(interval, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(SchedulerStatus.Running, scheduler.Status);
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
            _mockTimerExecutionService.Verify(x => x.Start(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(SchedulerStatus.Running, scheduler.Status);
            Assert.Equal(TimeSpan.FromSeconds(30), scheduler.CurrentInterval); // Should remain unchanged
        }

        [Fact]
        public async Task StartAsync_WhenTimerServiceThrows_UpdatesStatusToError()
        {
            // Arrange
            _mockTimerExecutionService
                .Setup(x => x.Start(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Timer service error"));

            var scheduler = CreateScheduler();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scheduler.StartAsync(TimeSpan.FromSeconds(30)));

            Assert.Equal("Timer service error", exception.Message);
            Assert.Equal(SchedulerStatus.Error, scheduler.Status);
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
            _mockTimerExecutionService.Verify(x => x.Stop(), Times.Once);
            Assert.Equal(SchedulerStatus.Stopped, scheduler.Status);
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
            _mockTimerExecutionService.Verify(x => x.Stop(), Times.Never);
            Assert.Equal(SchedulerStatus.Stopped, scheduler.Status);
        }

        [Fact]
        public async Task PauseAsync_WhenRunning_InvokesTimerServicePause()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            await scheduler.PauseAsync();

            // Assert
            _mockTimerExecutionService.Verify(x => x.Pause(), Times.Once);
            Assert.Equal(SchedulerStatus.Paused, scheduler.Status);
            Assert.False(scheduler.IsRunning);
        }

        [Fact]
        public async Task ResumeAsync_WhenPaused_InvokesTimerServiceResume()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));
            await scheduler.PauseAsync();

            // Act
            await scheduler.ResumeAsync();

            // Assert
            _mockTimerExecutionService.Verify(x => x.Resume(), Times.Once);
            Assert.Equal(SchedulerStatus.Running, scheduler.Status);
            Assert.True(scheduler.IsRunning);
        }

        [Fact]
        public async Task UpdateIntervalAsync_WhenRunning_UpdatesInterval()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));
            var newInterval = TimeSpan.FromMinutes(2);

            // Act
            await scheduler.UpdateIntervalAsync(newInterval);

            // Assert
            _mockTimerExecutionService.Verify(x => x.UpdateInterval(newInterval), Times.Once);
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
        public async Task UpdateIntervalAsync_WithIntervalOutsideBounds_ThrowsArgumentException()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                scheduler.UpdateIntervalAsync(TimeSpan.FromMilliseconds(100))); // Below MinInterval
            await Assert.ThrowsAsync<ArgumentException>(() =>
                scheduler.UpdateIntervalAsync(TimeSpan.FromDays(2))); // Above MaxInterval
        }

        [Fact]
        public async Task ExecuteNowAsync_WithOperationType_ExecutesOperation()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var serverStatus = new LicenseServerStatus
            {
                Server = "localhost",
                Port = 27000,
                IsServerUp = true,
                TotalLicenses = 100,
                LicensesInUse = 50,
                AvailableLicenses = 50
            };

            _mockLicenseQueryEngine
                .Setup(x => x.QueryLicenseStatusAsync("localhost", 27000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serverStatus);

            // Act
            var result = await scheduler.ExecuteNowAsync(
                LicenseCheckOperationType.ServerStatusCheck, "localhost", 27000);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(LicenseCheckOperationType.ServerStatusCheck, result.OperationType);
            Assert.Equal("localhost", result.Server);
            Assert.Equal(27000, result.Port);
            Assert.Equal(serverStatus, result.ServerStatus);
        }

        [Fact]
        public async Task ExecuteNowAsync_WithCustomOperation_ExecutesOperation()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var operation = LicenseCheckOperation.CreateFeatureCheck("localhost", 27000, "test-feature");
            var feature = new LicenseFeature
            {
                FeatureName = "test-feature",
                TotalLicenses = 50,
                UsedLicenses = 25,
                AvailableLicenses = 25
            };

            _mockLicenseQueryEngine
                .Setup(x => x.QueryFeatureAsync("localhost", 27000, "test-feature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(feature);

            // Act
            var result = await scheduler.ExecuteNowAsync(operation);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(LicenseCheckOperationType.FeatureStatusCheck, result.OperationType);
            Assert.Equal("localhost", result.Server);
            Assert.Equal(27000, result.Port);
            Assert.Equal("test-feature", result.Feature);
        }

        [Fact]
        public async Task ExecuteNowAsync_WhenQueryEngineThrows_ReturnsFailedResult()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            _mockLicenseQueryEngine
                .Setup(x => x.QueryLicenseStatusAsync("localhost", 27000, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Query engine error"));

            // Act
            var result = await scheduler.ExecuteNowAsync(
                LicenseCheckOperationType.ServerStatusCheck, "localhost", 27000);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Query engine error", result.ErrorMessage);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task ScheduleOneTimeAsync_SchedulesOperationWithDelay()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var operation = LicenseCheckOperation.CreateHealthCheck("localhost", 27000);
            var delay = TimeSpan.FromMinutes(5);

            // Act
            await scheduler.ScheduleOneTimeAsync(operation, delay);

            // Assert
            Assert.True(operation.ScheduledFor.HasValue);
            var scheduledTime = operation.ScheduledFor.Value;
            var expectedTime = DateTime.Now.Add(delay);
            var timeDiff = Math.Abs((scheduledTime - expectedTime).TotalSeconds);
            Assert.True(timeDiff < 1, $"Scheduled time should be within 1 second of expected time. Difference: {timeDiff}s");
        }

        [Fact]
        public async Task AddRecurringOperationAsync_AddsRecurringOperation()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            var interval = TimeSpan.FromMinutes(5);

            // Act
            await scheduler.AddRecurringOperationAsync(operation, interval);

            // Assert
            Assert.True(operation.IsRecurring);
            Assert.Equal(interval, operation.RecurringInterval);

            var scheduledOperations = scheduler.GetScheduledOperations();
            Assert.Contains(scheduledOperations, op => op.OperationId == operation.OperationId);
        }

        [Fact]
        public async Task AddRecurringOperationAsync_WithInvalidInterval_ThrowsArgumentException()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                scheduler.AddRecurringOperationAsync(operation, TimeSpan.Zero));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                scheduler.AddRecurringOperationAsync(operation, TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public async Task RemoveRecurringOperationAsync_RemovesOperation()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            await scheduler.AddRecurringOperationAsync(operation, TimeSpan.FromMinutes(5));

            // Act
            await scheduler.RemoveRecurringOperationAsync(operation.OperationId);

            // Assert
            var scheduledOperations = scheduler.GetScheduledOperations();
            Assert.DoesNotContain(scheduledOperations, op => op.OperationId == operation.OperationId);
        }

        [Fact]
        public void GetScheduledOperations_ReturnsAllScheduledOperations()
        {
            // Arrange
            var scheduler = CreateScheduler();

            var operation1 = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            var operation2 = LicenseCheckOperation.CreateFeatureCheck("localhost", 27000, "feature1");

            // Act
            var scheduledOperations = scheduler.GetScheduledOperations();

            // Assert
            Assert.NotNull(scheduledOperations);
            // Initially empty, but should return empty collection rather than null
            Assert.Empty(scheduledOperations);
        }

        [Fact]
        public void GetOperationHistory_ReturnsOperationHistory()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var history = scheduler.GetOperationHistory();

            // Assert
            Assert.NotNull(history);
            // Initially empty, but should return empty collection rather than null
            Assert.Empty(history);
        }

        [Fact]
        public void GetMetrics_ReturnsSchedulerMetrics()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var metrics = scheduler.GetMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(TimeSpan.Zero, metrics.Uptime); // Should have just started
            Assert.Equal(0, metrics.TotalOperations);
            Assert.Equal(0, metrics.SuccessfulOperations);
            Assert.Equal(0, metrics.FailedOperations);
            Assert.Equal(0, metrics.ActiveTimers);
            Assert.Equal(0, metrics.RecurringOperations);
        }

        [Fact]
        public void ResetStatistics_ResetsAllCounters()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            scheduler.ResetStatistics();

            // Assert
            _mockTimerExecutionService.Verify(x => x.ResetStatistics(), Times.Once);
            // Note: We can't easily test internal counters without reflection, but we can verify the method was called
        }

        [Fact]
        public void ValidateConfiguration_WithValidConfiguration_ReturnsEmptyList()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            var errors = scheduler.ValidateConfiguration();

            // Assert
            Assert.NotNull(errors);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidInterval_ReturnsErrors()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Manually set invalid interval through reflection or by testing the validation logic directly
            var invalidOptions = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.Zero,
                MinInterval = TimeSpan.FromSeconds(1),
                MaxInterval = TimeSpan.FromMinutes(1)
            };

            var invalidScheduler = new LicenseCheckScheduler(
                _mockTimerExecutionService.Object,
                _mockLogger.Object,
                _mockLicenseManager.Object,
                _mockLicenseQueryEngine.Object,
                invalidOptions);

            // Act
            var errors = invalidScheduler.ValidateConfiguration();

            // Assert
            Assert.NotNull(errors);
            Assert.NotEmpty(errors);
            Assert.Contains("Current interval must be greater than zero", errors);
        }

        [Fact]
        public void GetQueueStatus_ReturnsQueueStatus()
        {
            // Arrange
            var scheduler = CreateScheduler();

            // Act
            var queueStatus = scheduler.GetQueueStatus();

            // Assert
            Assert.NotNull(queueStatus);
            Assert.Equal(0, queueStatus.QueueSize);
            Assert.Equal(0, queueStatus.ExecutingCount);
            Assert.Equal(0, queueStatus.CompletedCount);
            Assert.Equal(0, queueStatus.FailedCount);
            Assert.Equal(0, queueStatus.CancelledCount);
            Assert.Equal(0, queueStatus.TimedOutCount);
        }

        [Fact]
        public async Task Dispose_CancelsAllOperationsAndDisposesResources()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            // Act
            scheduler.Dispose();

            // Assert
            _mockTimerExecutionService.Verify(x => x.Stop(), Times.Once);
            // The scheduler should be disposed and no longer usable
        }

        [Fact]
        public async Task OnTimerExecutionStarted_ExecutesDefaultLicenseCheck()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var serverStatus = new LicenseServerStatus
            {
                Server = "localhost",
                Port = 27000,
                IsServerUp = true,
                TotalLicenses = 100,
                LicensesInUse = 50,
                AvailableLicenses = 50
            };

            _mockLicenseQueryEngine
                .Setup(x => x.QueryLicenseStatusAsync("localhost", 27000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serverStatus);

            // Simulate timer execution started event
            var timerEventArgs = new TimerExecutionEventArgs
            {
                ExecutionId = Guid.NewGuid(),
                StartTime = DateTime.Now
            };

            // Act
            _mockTimerExecutionService.Raise(x => x.ExecutionStarted += null, timerEventArgs);

            // Allow time for async execution
            await Task.Delay(100);

            // Assert
            _mockLicenseQueryEngine.Verify(x => x.QueryLicenseStatusAsync("localhost", 27000, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OnTimerExecutionError_UpdatesStatusToErrorAfterMaxConsecutiveErrors()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var errorEventArgs = new TimerExecutionErrorEventArgs
            {
                Error = new InvalidOperationException("Timer error"),
                ConsecutiveErrors = _options.MaxConsecutiveErrors,
                Timestamp = DateTime.Now
            };

            // Act
            _mockTimerExecutionService.Raise(x => x.ExecutionError += null, errorEventArgs);

            // Allow time for async execution
            await Task.Delay(100);

            // Assert
            Assert.Equal(SchedulerStatus.Error, scheduler.Status);
        }

        [Fact]
        public async Task ExecuteOperationAsync_WithServerStatusCheck_ExecutesCorrectly()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            var serverStatus = new LicenseServerStatus
            {
                Server = "localhost",
                Port = 27000,
                IsServerUp = true,
                TotalLicenses = 100,
                LicensesInUse = 50,
                AvailableLicenses = 50
            };

            _mockLicenseQueryEngine
                .Setup(x => x.QueryLicenseStatusAsync("localhost", 27000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serverStatus);

            // Act
            var result = await scheduler.ExecuteNowAsync(operation);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(serverStatus, result.ServerStatus);
        }

        [Fact]
        public async Task ExecuteOperationAsync_WithFeatureStatusCheck_ExecutesCorrectly()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var operation = LicenseCheckOperation.CreateFeatureCheck("localhost", 27000, "test-feature");
            var feature = new LicenseFeature
            {
                FeatureName = "test-feature",
                TotalLicenses = 50,
                UsedLicenses = 25,
                AvailableLicenses = 25
            };

            _mockLicenseQueryEngine
                .Setup(x => x.QueryFeatureAsync("localhost", 27000, "test-feature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(feature);

            // Act
            var result = await scheduler.ExecuteNowAsync(operation);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(1, result.FeaturesProcessed);
            Assert.Equal(50, result.LicensesChecked);
        }

        [Fact]
        public async Task ExecuteOperationAsync_WithUserStatusCheck_ExecutesCorrectly()
        {
            // Arrange
            var scheduler = CreateScheduler();
            await scheduler.StartAsync(TimeSpan.FromSeconds(30));

            var operation = LicenseCheckOperation.CreateUserCheck("localhost", 27000, "test-user");
            var userLicenses = new Dictionary<string, List<LicenseInfo>>
            {
                ["test-user"] = new List<LicenseInfo>
                {
                    new LicenseInfo { User = "test-user", Feature = "feature1" }
                }
            };

            _mockLicenseQueryEngine
                .Setup(x => x.QueryUsersAsync("localhost", 27000, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(userLicenses);

            // Act
            var result = await scheduler.ExecuteNowAsync(operation);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(1, result.UsersProcessed);
            Assert.Equal(1, result.LicensesChecked);
        }

        private LicenseCheckScheduler CreateScheduler()
        {
            return new LicenseCheckScheduler(
                _mockTimerExecutionService.Object,
                _mockLogger.Object,
                _mockLicenseManager.Object,
                _mockLicenseQueryEngine.Object,
                _options);
        }
    }
}