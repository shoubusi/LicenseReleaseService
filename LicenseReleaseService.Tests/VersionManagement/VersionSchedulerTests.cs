using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.VersionManagement;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.VersionManagement
{
    /// <summary>
    /// Unit tests for VersionScheduler
    /// </summary>
    public class VersionSchedulerTests : IDisposable
    {
        private readonly Mock<ILogger<VersionScheduler>> _mockLogger;
        private readonly Mock<IVersionRuntimeManager> _mockRuntimeManager;
        private readonly Mock<IVersionResourceManager> _mockResourceManager;
        private readonly Mock<ITimerExecutionService> _mockTimerExecutionService;
        private readonly ITestOutputHelper _outputHelper;
        private readonly VersionSchedulerOptions _options;
        private VersionScheduler _scheduler;

        public VersionSchedulerTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _mockLogger = new Mock<ILogger<VersionScheduler>>();
            _mockRuntimeManager = new Mock<IVersionRuntimeManager>();
            _mockResourceManager = new Mock<IVersionResourceManager>();
            _mockTimerExecutionService = new Mock<ITimerExecutionService>();

            _options = new VersionSchedulerOptions
            {
                MaxConcurrentSchedules = 5,
                MinVersionInterval = TimeSpan.FromSeconds(1),
                MaxVersionInterval = TimeSpan.FromHours(1),
                MaxConcurrentOperationsPerVersion = 2,
                EnableCircuitBreaker = true,
                MaxConsecutiveErrors = 3,
                CircuitBreakerCooldown = TimeSpan.FromMinutes(2),
                EnableAutoRestart = true,
                PreventExecutionOverlap = true,
                EnableMaintenanceOperations = true,
                MaintenanceIntervalMinutes = 15,
                DefaultOperationTimeoutMinutes = 3,
                ExpectedOperationDurationSeconds = 15,
                EnableDetailedLogging = false,
                MaxExecutionHistory = 50,
                DisposalGracePeriod = TimeSpan.FromSeconds(10),
                EnableTimerPerformanceOptimization = true
            };

            SetupMockDependencies();
            _scheduler = new VersionScheduler(
                _mockLogger.Object,
                Options.Create(_options),
                _mockRuntimeManager.Object,
                _mockResourceManager.Object,
                _mockTimerExecutionService.Object);
        }

        private void SetupMockDependencies()
        {
            // Setup runtime manager mock
            _mockRuntimeManager.Setup(x => x.ExecuteOperationAsync(
                It.IsAny<VersionOperationType>(),
                It.IsAny<string>(),
                It.IsAny<VersionOperationRequirements>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((VersionOperationType operationType, string version, VersionOperationRequirements requirements, Dictionary<string, object> parameters, CancellationToken token) =>
                {
                    // Simulate successful operation execution
                    return new VersionOperationResult
                    {
                        Success = true,
                        OperationId = Guid.NewGuid(),
                        OperationType = operationType,
                        Version = version,
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow.AddMilliseconds(100),
                        Duration = TimeSpan.FromMilliseconds(100)
                    };
                });

            // Setup timer execution service mock
            _mockTimerExecutionService.Setup(x => x.StartAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockTimerExecutionService.Setup(x => x.StartOneTimeAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockTimerExecutionService.Setup(x => x.StopAsync())
                .Returns(Task.CompletedTask);

            _mockTimerExecutionService.Setup(x => x.State)
                .Returns(TimerState.Stopped);

            _mockTimerExecutionService.Setup(x => x.GetMetrics())
                .Returns(new TimerPerformanceMetrics
                {
                    TotalExecutions = 0,
                    SuccessfulExecutions = 0,
                    FailedExecutions = 0,
                    AverageExecutionDuration = TimeSpan.Zero,
                    Uptime = TimeSpan.Zero
                });
        }

        [Fact]
        public async Task StartAsync_ShouldStartSuccessfully_WhenCalledFirstTime()
        {
            // Act
            await _scheduler.StartAsync();

            // Assert
            Assert.Equal(VersionSchedulerStatus.Running, _scheduler.Status);
            Assert.True(_scheduler.IsRunning);

            _mockTimerExecutionService.Verify(x => x.StartAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_ShouldThrowException_WhenAlreadyRunning()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _scheduler.StartAsync());
            Assert.Contains("must be stopped", exception.Message);
        }

        [Fact]
        public async Task StopAsync_ShouldStopSuccessfully_WhenRunning()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Act
            await _scheduler.StopAsync();

            // Assert
            Assert.Equal(VersionSchedulerStatus.Stopped, _scheduler.Status);
            Assert.False(_scheduler.IsRunning);

            _mockTimerExecutionService.Verify(x => x.StopAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StopAsync_ShouldStopGracefully_WhenAlreadyStopped()
        {
            // Arrange
            Assert.Equal(VersionSchedulerStatus.Stopped, _scheduler.Status);

            // Act
            await _scheduler.StopAsync();

            // Assert
            Assert.Equal(VersionSchedulerStatus.Stopped, _scheduler.Status);
        }

        [Fact]
        public async Task ScheduleOperationAsync_ShouldScheduleSuccessfully_WhenSchedulerIsRunning()
        {
            // Arrange
            await _scheduler.StartAsync();

            var interval = TimeSpan.FromMinutes(5);

            // Act
            var operationId = await _scheduler.ScheduleOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                interval,
                new Dictionary<string, object> { ["TestParam"] = "TestValue" });

            // Assert
            Assert.NotNull(operationId);
            Assert.True(operationId.StartsWith("op_"));

            // Verify operation is tracked
            var scheduledOperations = await _scheduler.GetScheduledOperationsAsync();
            Assert.Single(scheduledOperations);
            Assert.Equal(operationId, scheduledOperations.First().OperationId);
            Assert.Equal(VersionOperationType.LicenseCheck, scheduledOperations.First().OperationType);
            Assert.Equal("2024", scheduledOperations.First().Version);
            Assert.Equal(interval, scheduledOperations.First().Interval);
        }

        [Fact]
        public async Task ScheduleOperationAsync_ShouldThrowException_WhenSchedulerIsNotRunning()
        {
            // Arrange
            Assert.Equal(VersionSchedulerStatus.Stopped, _scheduler.Status);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _scheduler.ScheduleOperationAsync(
                    VersionOperationType.LicenseCheck,
                    "2024",
                    TimeSpan.FromMinutes(5)));

            Assert.Contains("must be running", exception.Message);
        }

        [Fact]
        public async Task ScheduleOperationAsync_ShouldThrowException_WhenVersionIsInvalid()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _scheduler.ScheduleOperationAsync(
                    VersionOperationType.LicenseCheck,
                    "", // Empty version
                    TimeSpan.FromMinutes(5)));

            Assert.Contains("cannot be null or whitespace", exception.Message);
        }

        [Fact]
        public async Task ScheduleOneTimeOperationAsync_ShouldScheduleSuccessfully_WhenSchedulerIsRunning()
        {
            // Arrange
            await _scheduler.StartAsync();

            var delay = TimeSpan.FromSeconds(10);

            // Act
            var operationId = await _scheduler.ScheduleOneTimeOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                delay);

            // Assert
            Assert.NotNull(operationId);
            Assert.True(operationId.StartsWith("op_"));

            // Verify operation is tracked and marked as one-time
            var scheduledOperations = await _scheduler.GetScheduledOperationsAsync();
            Assert.Single(scheduledOperations);
            Assert.Equal(operationId, scheduledOperations.First().OperationId);
            Assert.True(scheduledOperations.First().IsOneTime);
            Assert.Equal(delay, scheduledOperations.First().Interval);
        }

        [Fact]
        public async Task CancelScheduledOperationAsync_ShouldCancelSuccessfully_WhenOperationExists()
        {
            // Arrange
            await _scheduler.StartAsync();

            var operationId = await _scheduler.ScheduleOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                TimeSpan.FromMinutes(5));

            // Act
            var result = await _scheduler.CancelScheduledOperationAsync(operationId);

            // Assert
            Assert.True(result);

            // Verify operation is marked as cancelled
            var scheduledOperations = await _scheduler.GetScheduledOperationsAsync();
            var operation = scheduledOperations.First(o => o.OperationId == operationId);
            Assert.Equal(ScheduledOperationStatus.Cancelled, operation.Status);
            Assert.NotNull(operation.CancelledAt);
        }

        [Fact]
        public async Task CancelScheduledOperationAsync_ShouldReturnFalse_WhenOperationDoesNotExist()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Act
            var result = await _scheduler.CancelScheduledOperationAsync("nonexistent_operation_id");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetScheduledOperationsAsync_ShouldReturnAllOperations_WhenNoVersionFilter()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Schedule multiple operations
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseCheck, "2024", TimeSpan.FromMinutes(5));
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseQuery, "2023", TimeSpan.FromMinutes(10));
            await _scheduler.ScheduleOneTimeOperationAsync(VersionOperationType.LicenseRelease, "2024", TimeSpan.FromSeconds(30));

            // Act
            var operations = await _scheduler.GetScheduledOperationsAsync();

            // Assert
            Assert.Equal(3, operations.Count);
            Assert.True(operations.Any(o => o.Version == "2024"));
            Assert.True(operations.Any(o => o.Version == "2023"));
            Assert.True(operations.Any(o => o.IsOneTime));
        }

        [Fact]
        public async Task GetScheduledOperationsAsync_ShouldReturnFilteredOperations_WhenVersionFilterIsSpecified()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Schedule multiple operations
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseCheck, "2024", TimeSpan.FromMinutes(5));
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseQuery, "2023", TimeSpan.FromMinutes(10));
            await _scheduler.ScheduleOneTimeOperationAsync(VersionOperationType.LicenseRelease, "2024", TimeSpan.FromSeconds(30));

            // Act
            var operations = await _scheduler.GetScheduledOperationsAsync("2024");

            // Assert
            Assert.Equal(2, operations.Count);
            Assert.True(operations.All(o => o.Version == "2024"));
        }

        [Fact]
        public async Task GetMetricsAsync_ShouldReturnComprehensiveMetrics()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Schedule some operations
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseCheck, "2024", TimeSpan.FromMinutes(5));
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseQuery, "2023", TimeSpan.FromMinutes(10));

            // Act
            var metrics = await _scheduler.GetMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(VersionSchedulerStatus.Running, metrics.Status);
            Assert.Equal(2, metrics.TotalScheduledOperations);
            Assert.True(metrics.Timestamp > DateTime.UtcNow.AddMinutes(-1));
            Assert.True(metrics.VersionMetrics.ContainsKey("2024"));
            Assert.True(metrics.VersionMetrics.ContainsKey("2023"));
        }

        [Fact]
        public async Task ExecuteOperationAsync_ShouldExecuteSuccessfully_WhenSchedulerIsRunning()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Act
            await _scheduler.ExecuteOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                new Dictionary<string, object> { ["TestParam"] = "TestValue" });

            // Assert
            // Verify the operation was queued and executed
            _mockRuntimeManager.Verify(x => x.ExecuteOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                It.IsAny<VersionOperationRequirements>(),
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("TestParam")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteOperationAsync_ShouldThrowException_WhenSchedulerIsNotRunning()
        {
            // Arrange
            Assert.Equal(VersionSchedulerStatus.Stopped, _scheduler.Status);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                _scheduler.ExecuteOperationAsync(
                    VersionOperationType.LicenseCheck,
                    "2024"));

            // Note: This might be a different exception type based on the implementation
            // For now, we're just verifying it throws an exception
        }

        [Fact]
        public async Task ExecuteOperationAsync_ShouldHandleOperationFailure_Gracefully()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Setup runtime manager to fail
            _mockRuntimeManager.Setup(x => x.ExecuteOperationAsync(
                It.IsAny<VersionOperationType>(),
                It.IsAny<string>(),
                It.IsAny<VersionOperationRequirements>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VersionOperationResult
                {
                    Success = false,
                    ErrorMessage = "Operation failed due to timeout"
                });

            // Act
            await _scheduler.ExecuteOperationAsync(VersionOperationType.LicenseCheck, "2024");

            // Assert
            // Should not throw exception, but should handle the failure gracefully
            // The operation error event should be triggered
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Concurrency_ShouldHandleConcurrentSchedules()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Act - schedule multiple operations concurrently
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_scheduler.ScheduleOperationAsync(
                    VersionOperationType.LicenseCheck,
                    $"202{i}",
                    TimeSpan.FromMinutes(5 + i)));
            }

            var operationIds = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(5, operationIds.Length);
            Assert.True(operationIds.All(id => id != null && id.StartsWith("op_")));

            // Verify all operations are tracked
            var scheduledOperations = await _scheduler.GetScheduledOperationsAsync();
            Assert.Equal(5, scheduledOperations.Count);
        }

        [Fact]
        public async Task TimerIntegration_ShouldCreateVersionSpecificTimers()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Schedule operations for different versions
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseCheck, "2024", TimeSpan.FromMinutes(5));
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseCheck, "2023", TimeSpan.FromMinutes(10));

            // Act
            var metrics = await _scheduler.GetMetricsAsync();

            // Assert
            Assert.Equal(2, metrics.ActiveVersionTimers);
            Assert.True(metrics.VersionMetrics.ContainsKey("2024"));
            Assert.True(metrics.VersionMetrics.ContainsKey("2023"));
        }

        [Fact]
        public async Task TimerIntegration_ShouldReuseTimersForSameVersion()
        {
            // Arrange
            await _scheduler.StartAsync();

            // Schedule multiple operations for the same version
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseCheck, "2024", TimeSpan.FromMinutes(5));
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseQuery, "2024", TimeSpan.FromMinutes(5));

            // Act
            var metrics = await _scheduler.GetMetricsAsync();

            // Assert
            Assert.Equal(1, metrics.ActiveVersionTimers); // Should reuse timer for same version
            Assert.True(metrics.VersionMetrics.ContainsKey("2024"));
        }

        [Fact]
        public async Task EventHandling_ShouldRaiseEventsCorrectly()
        {
            // Arrange
            await _scheduler.StartAsync();

            var eventsRaised = new List<string>();
            _scheduler.OperationScheduled += (s, e) => eventsRaised.Add("Scheduled");
            _scheduler.OperationStarted += (s, e) => eventsRaised.Add("Started");
            _scheduler.OperationCompleted += (s, e) => eventsRaised.Add("Completed");

            // Act
            var operationId = await _scheduler.ScheduleOperationAsync(
                VersionOperationType.LicenseCheck,
                "2024",
                TimeSpan.FromMinutes(5));

            await _scheduler.ExecuteOperationAsync(VersionOperationType.LicenseCheck, "2024");

            // Wait a bit for async operations to complete
            await Task.Delay(100);

            // Assert
            Assert.Contains("Scheduled", eventsRaised);
            Assert.Contains("Started", eventsRaised);
            Assert.Contains("Completed", eventsRaised);
        }

        [Fact]
        public async Task ResourceManagement_ShouldRespectMaxConcurrentSchedules()
        {
            // Arrange
            _options.MaxConcurrentSchedules = 2; // Limit to 2 concurrent schedules
            _scheduler = new VersionScheduler(
                _mockLogger.Object,
                Options.Create(_options),
                _mockRuntimeManager.Object,
                _mockResourceManager.Object,
                _mockTimerExecutionService.Object);

            await _scheduler.StartAsync();

            // Act - try to schedule more than the limit
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 4; i++)
            {
                tasks.Add(_scheduler.ScheduleOperationAsync(
                    VersionOperationType.LicenseCheck,
                    $"202{i}",
                    TimeSpan.FromMinutes(5)));
            }

            var operationIds = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(4, operationIds.Length);
            Assert.True(operationIds.All(id => id != null));

            // All operations should be scheduled (semaphore should allow queuing)
            var scheduledOperations = await _scheduler.GetScheduledOperationsAsync();
            Assert.Equal(4, scheduledOperations.Count);
        }

        [Fact]
        public void Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            Assert.Equal(VersionSchedulerStatus.Stopped, _scheduler.Status);
            Assert.Equal(0, _scheduler.ScheduledOperationCount);
            Assert.Equal(0, _scheduler.QueuedOperationCount);
            Assert.Equal(0, _scheduler.ActiveTimerCount);
            Assert.False(_scheduler.IsRunning);

            // Act & Assert - properties are read-only, just verify they work
            var status = _scheduler.Status;
            var operationCount = _scheduler.ScheduledOperationCount;
            var queuedCount = _scheduler.QueuedOperationCount;
            var timerCount = _scheduler.ActiveTimerCount;
            var isRunning = _scheduler.IsRunning;

            Assert.Equal(VersionSchedulerStatus.Stopped, status);
            Assert.Equal(0, operationCount);
            Assert.Equal(0, queuedCount);
            Assert.Equal(0, timerCount);
            Assert.False(isRunning);
        }

        [Fact]
        public async Task Dispose_ShouldCleanupResources()
        {
            // Arrange
            await _scheduler.StartAsync();
            await _scheduler.ScheduleOperationAsync(VersionOperationType.LicenseCheck, "2024", TimeSpan.FromMinutes(5));

            // Act
            _scheduler.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => _scheduler.Status);
        }

        public void Dispose()
        {
            _scheduler?.Dispose();
        }
    }
}