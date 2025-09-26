using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class ITimerExecutionServiceTests
    {
        private readonly Mock<ILogger<TimerExecutionService>> _mockLogger;
        private readonly TimerExecutionOptions _testOptions;

        public ITimerExecutionServiceTests()
        {
            _mockLogger = new Mock<ILogger<TimerExecutionService>>();
            _testOptions = TimerExecutionOptions.TestOptions();
        }

        private ITimerExecutionService CreateService()
        {
            return new TimerExecutionService(_mockLogger.Object, _testOptions);
        }

        [Fact]
        public void State_ShouldInitiallyBeStopped()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Equal(TimerState.Stopped, service.State);
        }

        [Fact]
        public void IsRunning_ShouldInitiallyBeFalse()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.False(service.IsRunning);
        }

        [Fact]
        public void IsExecuting_ShouldInitiallyBeFalse()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.False(service.IsExecuting);
        }

        [Fact]
        public void CurrentInterval_ShouldReturnDefaultInterval()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Equal(_testOptions.DefaultInterval, service.CurrentInterval);
        }

        [Fact]
        public void Start_WithValidInterval_ShouldChangeStateToRunning()
        {
            // Arrange
            var service = CreateService();
            var interval = TimeSpan.FromSeconds(1);

            // Act
            service.Start(interval);

            // Assert
            Assert.Equal(TimerState.Running, service.State);
            Assert.True(service.IsRunning);
            Assert.Equal(interval, service.CurrentInterval);
        }

        [Fact]
        public void StartOneTime_WithValidDelay_ShouldChangeStateToRunning()
        {
            // Arrange
            var service = CreateService();
            var delay = TimeSpan.FromMilliseconds(100);

            // Act
            service.StartOneTime(delay);

            // Assert
            Assert.Equal(TimerState.Running, service.State);
            Assert.True(service.IsRunning);
            Assert.Equal(delay, service.CurrentInterval);
        }

        [Fact]
        public void Stop_WhenRunning_ShouldChangeStateToStopped()
        {
            // Arrange
            var service = CreateService();
            service.Start(TimeSpan.FromSeconds(1));

            // Act
            service.Stop();

            // Assert
            Assert.Equal(TimerState.Stopped, service.State);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public void Pause_WhenRunning_ShouldChangeStateToPaused()
        {
            // Arrange
            var service = CreateService();
            service.Start(TimeSpan.FromSeconds(1));

            // Act
            service.Pause();

            // Assert
            Assert.Equal(TimerState.Paused, service.State);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public void Resume_WhenPaused_ShouldChangeStateToRunning()
        {
            // Arrange
            var service = CreateService();
            service.Start(TimeSpan.FromSeconds(1));
            service.Pause();

            // Act
            service.Resume();

            // Assert
            Assert.Equal(TimerState.Running, service.State);
            Assert.True(service.IsRunning);
        }

        [Fact]
        public void UpdateInterval_WhenRunning_ShouldUpdateCurrentInterval()
        {
            // Arrange
            var service = CreateService();
            var originalInterval = TimeSpan.FromSeconds(1);
            var newInterval = TimeSpan.FromSeconds(2);

            service.Start(originalInterval);

            // Act
            service.UpdateInterval(newInterval);

            // Assert
            Assert.Equal(newInterval, service.CurrentInterval);
            Assert.Equal(TimerState.Running, service.State);
        }

        [Fact]
        public async Task ExecuteNowAsync_ShouldCompleteExecution()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.ExecuteNowAsync();

            // Assert
            // Should complete without throwing exceptions
            // The service should remain in stopped state
            Assert.Equal(TimerState.Stopped, service.State);
        }

        [Fact]
        public void GetMetrics_ShouldReturnValidMetrics()
        {
            // Arrange
            var service = CreateService();

            // Act
            var metrics = service.GetMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.TotalExecutions >= 0);
            Assert.True(metrics.SuccessRate >= 0);
            Assert.True(metrics.SuccessRate <= 100);
        }

        [Fact]
        public void ResetStatistics_ShouldResetMetrics()
        {
            // Arrange
            var service = CreateService();
            service.Start(TimeSpan.FromMilliseconds(100));

            // Wait for some activity
            Thread.Sleep(200);

            service.Stop();
            var originalMetrics = service.GetMetrics();

            // Act
            service.ResetStatistics();

            // Assert
            var resetMetrics = service.GetMetrics();
            Assert.Equal(0, resetMetrics.TotalExecutions);
            Assert.Equal(0, resetMetrics.SuccessfulExecutions);
            Assert.Equal(0, resetMetrics.FailedExecutions);
            Assert.Equal(0, resetMetrics.CurrentConsecutiveErrors);
        }

        [Fact]
        public void Events_ShouldBeCallableWithoutThrowing()
        {
            // Arrange
            var service = CreateService();
            var eventHandlerCalled = false;

            // Attach event handlers
            service.ExecutionStarted += (s, e) => eventHandlerCalled = true;
            service.ExecutionCompleted += (s, e) => eventHandlerCalled = true;
            service.ExecutionError += (s, e) => eventHandlerCalled = true;
            service.StateChanged += (s, e) => eventHandlerCalled = true;

            // Act - this shouldn't throw any exceptions
            service.Start(TimeSpan.FromMilliseconds(100));

            // Wait for potential events
            Thread.Sleep(200);

            service.Stop();

            // Assert
            // The fact that we reach this point without exceptions means the events work
            // Note: eventHandlerCalled may or may not be true depending on timing
        }

        [Fact]
        public void MultipleOperations_ShouldWorkTogether()
        {
            // Arrange
            var service = CreateService();

            // Act - perform a sequence of operations
            service.Start(TimeSpan.FromSeconds(1));
            service.Pause();
            service.Resume();
            service.UpdateInterval(TimeSpan.FromSeconds(2));
            service.Stop();

            // Assert
            Assert.Equal(TimerState.Stopped, service.State);
            Assert.False(service.IsRunning);
            Assert.Equal(TimeSpan.FromSeconds(2), service.CurrentInterval);
        }

        [Fact]
        public async Task ConcurrentOperations_ShouldBeHandledGracefully()
        {
            // Arrange
            var service = CreateService();

            // Act - start multiple operations concurrently
            var startTask = Task.Run(() => service.Start(TimeSpan.FromSeconds(1)));
            var executeTask = service.ExecuteNowAsync();
            var metricsTask = Task.Run(() => service.GetMetrics());

            // Wait for all operations to complete
            await Task.WhenAll(startTask, executeTask, metricsTask);

            // Assert
            // Service should be in a consistent state
            Assert.True(service.State == TimerState.Running || service.State == TimerState.Stopped);
        }

        [Fact]
        public void EdgeCases_ShouldBeHandled()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert - these operations should not throw exceptions
            service.Stop(); // Stop when already stopped
            service.ResetStatistics(); // Reset when nothing has happened
            var metrics = service.GetMetrics(); // Get metrics immediately

            Assert.NotNull(metrics);
            Assert.Equal(TimerState.Stopped, service.State);
        }

        [Fact]
        public void TimerLifecycle_ShouldBeComplete()
        {
            // Arrange
            var service = CreateService();

            // Act - complete lifecycle
            service.Start(TimeSpan.FromMilliseconds(100));
            Thread.Sleep(50); // Let it run briefly
            service.Pause();
            service.Resume();
            service.Stop();

            // Assert
            Assert.Equal(TimerState.Stopped, service.State);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task Cancellation_ShouldBeSupported()
        {
            // Arrange
            var service = CreateService();
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var executionTask = service.ExecuteNowAsync(cancellationTokenSource.Token);

            // Cancel after a short delay
            cancellationTokenSource.CancelAfter(50);

            // Assert
            // Should either complete or cancel gracefully
            try
            {
                await executionTask;
            }
            catch (TaskCanceledException)
            {
                // Expected behavior
            }

            // Service should still be functional
            Assert.Equal(TimerState.Stopped, service.State);
        }

        [Fact]
        public void InterfaceConsistency_ShouldBeMaintained()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert - verify all interface methods work
            Assert.NotNull(service.State);
            Assert.False(service.IsRunning);
            Assert.False(service.IsExecuting);
            Assert.NotNull(service.CurrentInterval);

            // Events should be null before subscribers are added
            // (we can't test event handler invocation directly without reflection)

            // Methods should execute without throwing
            service.Start(TimeSpan.FromSeconds(1));
            Assert.True(service.IsRunning);

            service.Stop();
            Assert.False(service.IsRunning);

            var metrics = service.GetMetrics();
            Assert.NotNull(metrics);

            service.ResetStatistics(); // Should not throw

            // One-time execution
            service.StartOneTime(TimeSpan.FromMilliseconds(100));
            Assert.True(service.IsRunning);
            service.Stop();

            // Pause/Resume cycle
            service.Start(TimeSpan.FromSeconds(1));
            service.Pause();
            Assert.Equal(TimerState.Paused, service.State);
            service.Resume();
            Assert.Equal(TimerState.Running, service.State);
            service.Stop();
        }
    }
}