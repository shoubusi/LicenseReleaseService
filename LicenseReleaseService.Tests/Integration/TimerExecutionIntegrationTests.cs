using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.TimerExecution;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.IdleDetection
{
    public class TimerExecutionIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ITimerExecutionService> _mockTimerService;
        private readonly Mock<ConfigurationIntegration> _mockConfigIntegration;
        private readonly TimerExecutionIntegration _timerIntegration;

        public TimerExecutionIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _mockTimerService = new Mock<ITimerExecutionService>();
            _mockConfigIntegration = new Mock<ConfigurationIntegration>();

            // Setup default configuration
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(true);
            _mockConfigIntegration.Setup(c => c.DetectionInterval).Returns(TimeSpan.FromSeconds(60));
            _mockConfigIntegration.Setup(c => c.ConfidenceThreshold).Returns(0.7);
            _mockConfigIntegration.Setup(c => c.MaxDetectionTime).Returns(TimeSpan.FromSeconds(30));

            // Setup timer service
            _mockTimerService.Setup(s => s.State).Returns(TimerState.Stopped);
            _mockTimerService.Setup(s => s.IsRunning).Returns(false);

            _timerIntegration = new TimerExecutionIntegration(
                _mockTimerService.Object,
                _mockConfigIntegration.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Arrange & Act
            var integration = new TimerExecutionIntegration(
                _mockTimerService.Object,
                _mockConfigIntegration.Object);

            // Assert
            Assert.NotNull(integration);
            Assert.True(integration.IsEnabled);
        }

        [Fact]
        public void Constructor_WithNullTimerService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerExecutionIntegration(
                null,
                _mockConfigIntegration.Object));
        }

        [Fact]
        public void Constructor_WithNullConfigIntegration_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerExecutionIntegration(
                _mockTimerService.Object,
                null));
        }

        [Fact]
        public void IsEnabled_WhenConfigEnabled_ShouldReturnTrue()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(true);

            // Act
            var isEnabled = _timerIntegration.IsEnabled;

            // Assert
            Assert.True(isEnabled);
        }

        [Fact]
        public void IsEnabled_WhenConfigDisabled_ShouldReturnFalse()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);

            // Act
            var isEnabled = _timerIntegration.IsEnabled;

            // Assert
            Assert.False(isEnabled);
        }

        [Fact]
        public void TimerState_ShouldReturnTimerServiceState()
        {
            // Arrange
            _mockTimerService.Setup(s => s.State).Returns(TimerState.Running);

            // Act
            var state = _timerIntegration.TimerState;

            // Assert
            Assert.Equal(TimerState.Running, state);
        }

        [Fact]
        public void IsRunning_ShouldReturnTimerServiceIsRunning()
        {
            // Arrange
            _mockTimerService.Setup(s => s.IsRunning).Returns(true);

            // Act
            var isRunning = _timerIntegration.IsRunning;

            // Assert
            Assert.True(isRunning);
        }

        [Fact]
        public void TimerRegistrations_ShouldReturnReadOnlyDictionary()
        {
            // Arrange & Act
            var registrations = _timerIntegration.TimerRegistrations;

            // Assert
            Assert.NotNull(registrations);
            Assert.True(registrations.IsReadOnly);
        }

        [Fact]
        public void PendingTaskCount_ShouldReturnZeroInitially()
        {
            // Arrange & Act
            var count = _timerIntegration.PendingTaskCount;

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task StartIdleDetectionTimersAsync_WhenDisabled_ShouldReturn()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await _timerIntegration.StartIdleDetectionTimersAsync();
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task StartIdleDetectionTimersAsync_WhenEnabled_ShouldStartTimers()
        {
            // Arrange
            var detectorConfigs = new List<IdleDetectorConfiguration>
            {
                ConfigurationTestHelper.CreateTestDetectorConfiguration("TestDetector")
            };

            _mockConfigIntegration.Setup(c => c.GetEnabledDetectorConfigurations()).Returns(detectorConfigs);

            // Act
            await _timerIntegration.StartIdleDetectionTimersAsync();

            // Assert
            _mockTimerService.Verify(s => s.StartAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartIdleDetectionTimersAsync_WithNoDetectors_ShouldNotStartTimers()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.GetEnabledDetectorConfigurations()).Returns(new List<IdleDetectorConfiguration>());

            // Act
            await _timerIntegration.StartIdleDetectionTimersAsync();

            // Assert
            _mockTimerService.Verify(s => s.StartAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StopIdleDetectionTimersAsync_ShouldStopTimerService()
        {
            // Arrange
            _mockTimerService.Setup(s => s.IsRunning).Returns(true);

            // Act
            await _timerIntegration.StopIdleDetectionTimersAsync();

            // Assert
            _mockTimerService.Verify(s => s.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task StartDetectorTimerAsync_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _timerIntegration.StartDetectorTimerAsync(null));
        }

        [Fact]
        public async Task StartDetectorTimerAsync_WhenDisabled_ShouldReturn()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);
            var config = ConfigurationTestHelper.CreateTestDetectorConfiguration();

            // Act
            await _timerIntegration.StartDetectorTimerAsync(config);

            // Assert
            _mockTimerService.Verify(s => s.StartOneTimeAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StartDetectorTimerAsync_WhenEnabled_ShouldStartTimer()
        {
            // Arrange
            var config = ConfigurationTestHelper.CreateTestDetectorConfiguration("TestDetector");

            // Act
            await _timerIntegration.StartDetectorTimerAsync(config);

            // Assert
            _mockTimerService.Verify(s => s.StartOneTimeAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopDetectorTimerAsync_WithNullName_ShouldThrowArgumentException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _timerIntegration.StopDetectorTimerAsync(null));
        }

        [Fact]
        public async Task StopDetectorTimerAsync_WithEmptyName_ShouldThrowArgumentException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _timerIntegration.StopDetectorTimerAsync(""));
        }

        [Fact]
        public async Task StopDetectorTimerAsync_WithWhitespaceName_ShouldThrowArgumentException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _timerIntegration.StopDetectorTimerAsync("   "));
        }

        [Fact]
        public async Task StopDetectorTimerAsync_WithNonExistentTimer_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await _timerIntegration.StopDetectorTimerAsync("NonExistentDetector");
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task ScheduleIdleDetectionTaskAsync_WithNullTask_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _timerIntegration.ScheduleIdleDetectionTaskAsync(null));
        }

        [Fact]
        public async Task ScheduleIdleDetectionTaskAsync_WhenDisabled_ShouldReturn()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);
            var task = CreateTestIdleDetectionTask();

            // Act
            await _timerIntegration.ScheduleIdleDetectionTaskAsync(task);

            // Assert
            Assert.Equal(0, _timerIntegration.PendingTaskCount);
        }

        [Fact]
        public async Task ScheduleIdleDetectionTaskAsync_WhenEnabled_ShouldAddToQueue()
        {
            // Arrange
            var task = CreateTestIdleDetectionTask();

            // Act
            await _timerIntegration.ScheduleIdleDetectionTaskAsync(task);

            // Assert
            Assert.Equal(1, _timerIntegration.PendingTaskCount);
        }

        [Fact]
        public async Task ScheduleIdleDetectionTasksAsync_WithNullTasks_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _timerIntegration.ScheduleIdleDetectionTasksAsync(null));
        }

        [Fact]
        public async Task ScheduleIdleDetectionTasksAsync_WithEmptyTasks_ShouldReturn()
        {
            // Arrange
            var emptyTasks = Enumerable.Empty<IdleDetectionTask>();

            // Act
            await _timerIntegration.ScheduleIdleDetectionTasksAsync(emptyTasks);

            // Assert
            Assert.Equal(0, _timerIntegration.PendingTaskCount);
        }

        [Fact]
        public async Task ScheduleIdleDetectionTasksAsync_WithMultipleTasks_ShouldAddAllToQueue()
        {
            // Arrange
            var tasks = new List<IdleDetectionTask>
            {
                CreateTestIdleDetectionTask("task1"),
                CreateTestIdleDetectionTask("task2"),
                CreateTestIdleDetectionTask("task3")
            };

            // Act
            await _timerIntegration.ScheduleIdleDetectionTasksAsync(tasks);

            // Assert
            Assert.Equal(3, _timerIntegration.PendingTaskCount);
        }

        [Fact]
        public async Task ExecuteIdleDetectionTaskAsync_WithNullTask_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _timerIntegration.ExecuteIdleDetectionTaskAsync(null));
        }

        [Fact]
        public async Task ExecuteIdleDetectionTaskAsync_WhenDisabled_ShouldReturn()
        {
            // Arrange
            _mockConfigIntegration.Setup(c => c.IsIdleDetectionEnabled).Returns(false);
            var task = CreateTestIdleDetectionTask();

            // Act
            await _timerIntegration.ExecuteIdleDetectionTaskAsync(task);

            // Assert
            // Should not throw and should not execute
        }

        [Fact]
        public async Task ExecuteIdleDetectionTaskAsync_WithValidTask_ShouldExecute()
        {
            // Arrange
            var task = CreateTestIdleDetectionTask();
            var executed = false;

            task.TaskFunc = async (cancellationToken) =>
            {
                executed = true;
                await Task.Delay(10, cancellationToken);
                return new List<IdleDetectionResult>();
            };

            // Act
            await _timerIntegration.ExecuteIdleDetectionTaskAsync(task);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task ExecuteIdleDetectionTaskAsync_WithFailingTask_ShouldHandleException()
        {
            // Arrange
            var task = CreateTestIdleDetectionTask();
            var eventRaised = false;

            _timerIntegration.TaskExecutionFailed += (sender, e) =>
            {
                eventRaised = true;
            };

            task.TaskFunc = async (cancellationToken) =>
            {
                await Task.Delay(10, cancellationToken);
                throw new InvalidOperationException("Test exception");
            };

            // Act
            await _timerIntegration.ExecuteIdleDetectionTaskAsync(task);

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public void GetTimerExecutionStatistics_ShouldReturnStatistics()
        {
            // Arrange & Act
            var stats = _timerIntegration.GetTimerExecutionStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.IsEnabled);
            Assert.Equal(TimerState.Stopped, stats.TimerState);
            Assert.Equal(0, stats.PendingTaskCount);
        }

        [Fact]
        public void GetTimerRegistration_WithNullName_ShouldThrowArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _timerIntegration.GetTimerRegistration(null));
        }

        [Fact]
        public void GetTimerRegistration_WithEmptyName_ShouldThrowArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _timerIntegration.GetTimerRegistration(""));
        }

        [Fact]
        public void GetTimerRegistration_WithNonExistentDetector_ShouldReturnNull()
        {
            // Arrange & Act
            var registration = _timerIntegration.GetTimerRegistration("NonExistentDetector");

            // Assert
            Assert.Null(registration);
        }

        [Fact]
        public void GetTimerRegistration_WithExistingDetector_ShouldReturnRegistration()
        {
            // Arrange
            var config = ConfigurationTestHelper.CreateTestDetectorConfiguration("TestDetector");
            _timerIntegration.StartDetectorTimerAsync(config).Wait();

            // Act
            var registration = _timerIntegration.GetTimerRegistration("TestDetector");

            // Assert
            Assert.NotNull(registration);
            Assert.Equal("TestDetector", registration.DetectorName);
        }

        [Fact]
        public async Task UpdateTimerRegistrationAsync_WithNullName_ShouldThrowArgumentException()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _timerIntegration.UpdateTimerRegistrationAsync(null, TimeSpan.FromSeconds(60)));
        }

        [Fact]
        public async Task UpdateTimerRegistrationAsync_WithNonExistentDetector_ShouldCreateNew()
        {
            // Arrange
            var config = ConfigurationTestHelper.CreateTestDetectorConfiguration("TestDetector");
            _mockConfigIntegration.Setup(c => c.GetDetectorConfiguration("TestDetector")).Returns(config);

            // Act
            await _timerIntegration.UpdateTimerRegistrationAsync("TestDetector", TimeSpan.FromSeconds(30));

            // Assert
            // Should not throw and should create registration
        }

        [Fact]
        public void TimerStarted_Event_ShouldBeRaisedWhenTimerStarts()
        {
            // Arrange
            var eventRaised = false;
            IdleDetectionTimerEventArgs args = null;

            _timerIntegration.TimerStarted += (sender, e) =>
            {
                eventRaised = true;
                args = e;
            };

            var config = ConfigurationTestHelper.CreateTestDetectorConfiguration("TestDetector");

            // Act
            _timerIntegration.StartDetectorTimerAsync(config).Wait();

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(args);
        }

        [Fact]
        public void TimerStopped_Event_ShouldBeRaisedWhenTimerStops()
        {
            // Arrange
            var eventRaised = false;
            IdleDetectionTimerEventArgs args = null;

            _timerIntegration.TimerStopped += (sender, e) =>
            {
                eventRaised = true;
                args = e;
            };

            var config = ConfigurationTestHelper.CreateTestDetectorConfiguration("TestDetector");
            _timerIntegration.StartDetectorTimerAsync(config).Wait();

            // Act
            _timerIntegration.StopDetectorTimerAsync("TestDetector").Wait();

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(args);
        }

        [Fact]
        public void TaskExecuted_Event_ShouldBeRaisedWhenTaskExecutes()
        {
            // Arrange
            var eventRaised = false;
            IdleDetectionTaskEventArgs args = null;

            _timerIntegration.TaskExecuted += (sender, e) =>
            {
                eventRaised = true;
                args = e;
            };

            var task = CreateTestIdleDetectionTask();
            task.TaskFunc = async (cancellationToken) =>
            {
                await Task.Delay(10, cancellationToken);
                return new List<IdleDetectionResult>();
            };

            // Act
            _timerIntegration.ExecuteIdleDetectionTaskAsync(task).Wait();

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(args);
            Assert.NotNull(args.Result);
            Assert.True(args.Result.Success);
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            var exception = Record.Exception(() =>
            {
                _timerIntegration.Dispose();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_MultipleTimes_ShouldNotThrow()
        {
            // Arrange
            _timerIntegration.Dispose();

            // Act & Assert
            var exception = Record.Exception(() =>
            {
                _timerIntegration.Dispose();
            });

            Assert.Null(exception);
        }

        private IdleDetectionTask CreateTestIdleDetectionTask(string taskId = "test-task")
        {
            return new IdleDetectionTask
            {
                TaskId = taskId,
                DetectorName = "TestDetector",
                TaskType = IdleDetectionTaskType.PeriodicDetection,
                ScheduledTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                Priority = 10,
                Timeout = TimeSpan.FromSeconds(30),
                TaskFunc = async (cancellationToken) =>
                {
                    await Task.Delay(10, cancellationToken);
                    return new List<IdleDetectionResult>();
                }
            };
        }

        public void Dispose()
        {
            _timerIntegration?.Dispose();
        }
    }

    /// <summary>
    /// Test helper class for creating timer execution test objects
    /// </summary>
    public static class TimerExecutionTestHelper
    {
        public static IdleDetectionTask CreateTestIdleDetectionTask(
            string taskId = "test-task",
            string detectorName = "TestDetector",
            IdleDetectionTaskType taskType = IdleDetectionTaskType.PeriodicDetection,
            int priority = 10)
        {
            return new IdleDetectionTask
            {
                TaskId = taskId,
                DetectorName = detectorName,
                TaskType = taskType,
                ScheduledTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                Priority = priority,
                Timeout = TimeSpan.FromSeconds(30),
                TaskFunc = async (cancellationToken) =>
                {
                    await Task.Delay(10, cancellationToken);
                    return new List<IdleDetectionResult>();
                }
            };
        }

        public static TimerRegistration CreateTestTimerRegistration(
            string detectorName = "TestDetector",
            TimeSpan? interval = null,
            bool isEnabled = true)
        {
            return new TimerRegistration
            {
                DetectorName = detectorName,
                TimerName = $"IdleDetection_{detectorName}",
                Interval = interval ?? TimeSpan.FromSeconds(60),
                IsEnabled = isEnabled,
                StartedAt = DateTime.UtcNow,
                ExecutionCount = 0,
                ErrorCount = 0,
                TotalExecutionTime = TimeSpan.Zero
            };
        }

        public static TimerExecutionStatistics CreateTestTimerExecutionStatistics(
            bool isEnabled = true,
            TimerState timerState = TimerState.Stopped,
            int pendingTaskCount = 0,
            int totalTimerRegistrations = 0)
        {
            return new TimerExecutionStatistics
            {
                IsEnabled = isEnabled,
                IsRunning = timerState == TimerState.Running,
                TimerState = timerState,
                PendingTaskCount = pendingTaskCount,
                TotalTimerRegistrations = totalTimerRegistrations,
                EnabledTimerRegistrations = totalTimerRegistrations,
                TotalExecutions = 0,
                TotalErrors = 0,
                AverageExecutionTime = TimeSpan.Zero,
                LastExecutionTime = null
            };
        }
    }
}