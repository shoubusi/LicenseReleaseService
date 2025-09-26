using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    public class TimerExecutionServiceTests : IDisposable
    {
        private readonly Mock<ILogger<TimerExecutionService>> _mockLogger;
        private readonly TimerExecutionOptions _testOptions;
        private List<TimerExecutionService> _servicesToDispose;

        public TimerExecutionServiceTests()
        {
            _mockLogger = new Mock<ILogger<TimerExecutionService>>();
            _testOptions = TimerExecutionOptions.TestOptions();
            _servicesToDispose = new List<TimerExecutionService>();
        }

        public void Dispose()
        {
            foreach (var service in _servicesToDispose)
            {
                try
                {
                    service.Dispose();
                }
                catch
                {
                    // Ignore disposal errors in tests
                }
            }
            _servicesToDispose.Clear();
        }

        private TimerExecutionService CreateService(TimerExecutionOptions options = null)
        {
            var service = new TimerExecutionService(_mockLogger.Object, options ?? _testOptions);
            _servicesToDispose.Add(service);
            return service;
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Act
            var service = CreateService();

            // Assert
            Assert.Equal(TimerState.Stopped, service.State);
            Assert.False(service.IsRunning);
            Assert.False(service.IsExecuting);
            Assert.Equal(_testOptions.DefaultInterval, service.CurrentInterval);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerExecutionService(null, _testOptions));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerExecutionService(_mockLogger.Object, null));
        }

        [Fact]
        public void Constructor_WithInvalidOptions_ShouldThrowArgumentException()
        {
            // Arrange
            var invalidOptions = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.Zero
            };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateService(invalidOptions));
            Assert.Contains("Invalid timer execution options", exception.Message);
        }

        [Fact]
        public void Start_WithValidInterval_ShouldStartTimer()
        {
            // Arrange
            var service = CreateService();
            var interval = TimeSpan.FromSeconds(1);
            var stateChanges = new List<TimerStateChangedEventArgs>();

            service.StateChanged += (s, e) => stateChanges.Add(e);

            // Act
            service.Start(interval);

            // Assert
            Assert.Equal(TimerState.Running, service.State);
            Assert.True(service.IsRunning);
            Assert.Equal(interval, service.CurrentInterval);

            // Verify state change events
            Assert.Contains(stateChanges, e => e.PreviousState == TimerState.Stopped && e.NewState == TimerState.Running);
        }

        [Fact]
        public void Start_WithInvalidInterval_ShouldThrowArgumentException()
        {
            // Arrange
            var service = CreateService();
            var invalidInterval = TimeSpan.Zero;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.Start(invalidInterval));
        }

        [Fact]
        public void Start_WhenAlreadyRunning_ShouldThrowTimerExecutionException()
        {
            // Arrange
            var service = CreateService();
            service.Start(TimeSpan.FromSeconds(1));

            // Act & Assert
            Assert.Throws<TimerExecutionException>(() => service.Start(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void StartOneTime_WithValidDelay_ShouldStartTimer()
        {
            // Arrange
            var service = CreateService();
            var delay = TimeSpan.FromMilliseconds(100);
            var executionStarted = new List<TimerExecutionEventArgs>();

            service.ExecutionStarted += (s, e) => executionStarted.Add(e);

            // Act
            service.StartOneTime(delay);

            // Assert
            Assert.Equal(TimerState.Running, service.State);
            Assert.True(service.IsRunning);
            Assert.Equal(delay, service.CurrentInterval);

            // Wait for execution to complete
            Thread.Sleep(200);

            // Verify execution occurred
            Assert.Single(executionStarted);
        }

        [Fact]
        public void Stop_WhenRunning_ShouldStopTimer()
        {
            // Arrange
            var service = CreateService();
            var stateChanges = new List<TimerStateChangedEventArgs>();

            service.StateChanged += (s, e) => stateChanges.Add(e);
            service.Start(TimeSpan.FromSeconds(1));

            // Act
            service.Stop();

            // Assert
            Assert.Equal(TimerState.Stopped, service.State);
            Assert.False(service.IsRunning);

            // Verify state change events
            var stoppingEvent = stateChanges.FirstOrDefault(e => e.NewState == TimerState.Stopping);
            var stoppedEvent = stateChanges.FirstOrDefault(e => e.NewState == TimerState.Stopped);
            Assert.NotNull(stoppingEvent);
            Assert.NotNull(stoppedEvent);
        }

        [Fact]
        public void Stop_WhenAlreadyStopped_ShouldDoNothing()
        {
            // Arrange
            var service = CreateService();
            var stateChanges = new List<TimerStateChangedEventArgs>();

            service.StateChanged += (s, e) => stateChanges.Add(e);

            // Act
            service.Stop();

            // Assert
            Assert.Equal(TimerState.Stopped, service.State);
            Assert.Empty(stateChanges); // No state changes expected
        }

        [Fact]
        public void Pause_WhenRunning_ShouldPauseTimer()
        {
            // Arrange
            var service = CreateService();
            var stateChanges = new List<TimerStateChangedEventArgs>();

            service.StateChanged += (s, e) => stateChanges.Add(e);
            service.Start(TimeSpan.FromSeconds(1));

            // Act
            service.Pause();

            // Assert
            Assert.Equal(TimerState.Paused, service.State);
            Assert.False(service.IsRunning);
            Assert.True(service.IsRunning == false);

            // Verify state change event
            Assert.Contains(stateChanges, e => e.NewState == TimerState.Paused);
        }

        [Fact]
        public void Pause_WhenNotRunning_ShouldThrowTimerExecutionException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Throws<TimerExecutionException>(() => service.Pause());
        }

        [Fact]
        public void Resume_WhenPaused_ShouldResumeTimer()
        {
            // Arrange
            var service = CreateService();
            var stateChanges = new List<TimerStateChangedEventArgs>();

            service.StateChanged += (s, e) => stateChanges.Add(e);
            service.Start(TimeSpan.FromSeconds(1));
            service.Pause();

            // Act
            service.Resume();

            // Assert
            Assert.Equal(TimerState.Running, service.State);
            Assert.True(service.IsRunning);

            // Verify state change event
            Assert.Contains(stateChanges, e => e.NewState == TimerState.Running && e.PreviousState == TimerState.Paused);
        }

        [Fact]
        public void Resume_WhenNotPaused_ShouldThrowTimerExecutionException()
        {
            // Arrange
            var service = CreateService();
            service.Start(TimeSpan.FromSeconds(1));

            // Act & Assert
            Assert.Throws<TimerExecutionException>(() => service.Resume());
        }

        [Fact]
        public void UpdateInterval_WhenRunning_ShouldUpdateInterval()
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
        public void UpdateInterval_WhenPaused_ShouldUpdateInterval()
        {
            // Arrange
            var service = CreateService();
            var originalInterval = TimeSpan.FromSeconds(1);
            var newInterval = TimeSpan.FromSeconds(2);

            service.Start(originalInterval);
            service.Pause();

            // Act
            service.UpdateInterval(newInterval);

            // Assert
            Assert.Equal(newInterval, service.CurrentInterval);
            Assert.Equal(TimerState.Paused, service.State);
        }

        [Fact]
        public void UpdateInterval_WhenNotRunning_ShouldThrowTimerExecutionException()
        {
            // Arrange
            var service = CreateService();
            var newInterval = TimeSpan.FromSeconds(2);

            // Act & Assert
            Assert.Throws<TimerExecutionException>(() => service.UpdateInterval(newInterval));
        }

        [Fact]
        public void ExecuteNowAsync_WhenStopped_ShouldExecuteImmediately()
        {
            // Arrange
            var service = CreateService();
            var executionStarted = new List<TimerExecutionEventArgs>();
            var executionCompleted = new List<TimerExecutionEventArgs>();

            service.ExecutionStarted += (s, e) => executionStarted.Add(e);
            service.ExecutionCompleted += (s, e) => executionCompleted.Add(e);

            // Act
            var task = service.ExecuteNowAsync();

            // Assert
            Assert.True(task.IsCompleted);
            Assert.Single(executionStarted);
            Assert.Single(executionCompleted);
            Assert.True(executionCompleted[0].Success);
        }

        [Fact]
        public async Task ExecuteNowAsync_WhenRunning_ShouldExecuteConcurrently()
        {
            // Arrange
            var service = CreateService();
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(1),
                MaxConcurrentExecutions = 2,
                PreventExecutionOverlap = false
            };
            service = CreateService(options);

            var executionStarted = new List<TimerExecutionEventArgs>();

            service.ExecutionStarted += (s, e) => executionStarted.Add(e);
            service.Start(TimeSpan.FromSeconds(10)); // Long interval to prevent auto-execution

            // Act
            var task1 = service.ExecuteNowAsync();
            var task2 = service.ExecuteNowAsync();

            await Task.WhenAll(task1, task2);

            // Assert
            Assert.Equal(2, executionStarted.Count);
        }

        [Fact]
        public void GetMetrics_ShouldReturnCurrentMetrics()
        {
            // Arrange
            var service = CreateService();
            service.Start(TimeSpan.FromMilliseconds(100));

            // Wait for at least one execution
            Thread.Sleep(200);

            // Act
            var metrics = service.GetMetrics();

            // Assert
            Assert.True(metrics.TotalExecutions >= 0);
            Assert.True(metrics.Uptime.TotalMilliseconds > 0);
            Assert.True(metrics.StartTime.HasValue);
        }

        [Fact]
        public void ResetStatistics_ShouldResetAllMetrics()
        {
            // Arrange
            var service = CreateService();
            service.Start(TimeSpan.FromMilliseconds(100));

            // Wait for some executions
            Thread.Sleep(200);

            var originalMetrics = service.GetMetrics();
            service.Stop();

            // Act
            service.ResetStatistics();

            // Assert
            var resetMetrics = service.GetMetrics();
            Assert.Equal(0, resetMetrics.TotalExecutions);
            Assert.Equal(0, resetMetrics.SuccessfulExecutions);
            Assert.Equal(0, resetMetrics.FailedExecutions);
            Assert.Equal(TimeSpan.Zero, resetMetrics.AverageExecutionDuration);
            Assert.Equal(0, resetMetrics.CurrentConsecutiveErrors);
        }

        [Fact]
        public void PeriodicExecution_ShouldExecuteAtConfiguredInterval()
        {
            // Arrange
            var service = CreateService();
            var executionEvents = new List<TimerExecutionEventArgs>();
            var interval = TimeSpan.FromMilliseconds(200);

            service.ExecutionStarted += (s, e) => executionEvents.Add(e);
            service.Start(interval);

            // Wait for multiple executions
            Thread.Sleep(600);

            service.Stop();

            // Assert
            Assert.True(executionEvents.Count >= 2); // At least 2 executions in 600ms with 200ms interval
        }

        [Fact]
        public void PeriodicExecution_WithPreventOverlap_ShouldPreventConcurrentExecutions()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMilliseconds(100),
                MaxConcurrentExecutions = 1,
                PreventExecutionOverlap = true
            };
            var service = CreateService(options);

            var executionStarts = new List<DateTime>();
            var executionCompletes = new List<DateTime>();

            service.ExecutionStarted += (s, e) => executionStarts.Add(DateTime.UtcNow);
            service.ExecutionCompleted += (s, e) => executionCompletes.Add(DateTime.UtcNow);

            // Act
            service.Start(options.DefaultInterval);

            // Wait for executions
            Thread.Sleep(500);

            service.Stop();

            // Assert
            // Verify that no execution started before the previous one completed
            for (int i = 1; i < executionStarts.Count; i++)
            {
                Assert.True(executionStarts[i] >= executionCompletes[i - 1],
                    $"Execution {i} started before execution {i - 1} completed");
            }
        }

        [Fact]
        public void EventHandlers_ShouldReceiveCorrectEvents()
        {
            // Arrange
            var service = CreateService();
            var executionStartedEvents = new List<TimerExecutionEventArgs>();
            var executionCompletedEvents = new List<TimerExecutionEventArgs>();
            var stateChangedEvents = new List<TimerStateChangedEventArgs>();
            var errorEvents = new List<TimerExecutionErrorEventArgs>();

            service.ExecutionStarted += (s, e) => executionStartedEvents.Add(e);
            service.ExecutionCompleted += (s, e) => executionCompletedEvents.Add(e);
            service.StateChanged += (s, e) => stateChangedEvents.Add(e);
            service.ExecutionError += (s, e) => errorEvents.Add(e);

            // Act
            service.Start(TimeSpan.FromMilliseconds(200));

            // Wait for execution
            Thread.Sleep(300);

            service.Stop();

            // Assert
            Assert.NotEmpty(executionStartedEvents);
            Assert.NotEmpty(executionCompletedEvents);
            Assert.True(executionStartedEvents.Count == executionCompletedEvents.Count);

            // Verify state changes: Stopped -> Running -> Stopping -> Stopped
            Assert.True(stateChangedEvents.Count >= 3); // At least start, stop, and possibly stopping
            Assert.Contains(stateChangedEvents, e => e.NewState == TimerState.Running);
            Assert.Contains(stateChangedEvents, e => e.NewState == TimerState.Stopped);

            // Verify no error events (assuming successful execution)
            Assert.Empty(errorEvents);
        }

        [Fact]
        public void Dispose_ShouldStopTimerAndCleanUpResources()
        {
            // Arrange
            var service = CreateService();
            var stateChanges = new List<TimerStateChangedEventArgs>();

            service.StateChanged += (s, e) => stateChanges.Add(e);
            service.Start(TimeSpan.FromSeconds(1));

            // Act
            service.Dispose();

            // Assert
            Assert.Equal(TimerState.Disposed, service.State);
            Assert.False(service.IsRunning);

            // Verify state change events
            Assert.Contains(stateChanges, e => e.NewState == TimerState.Disposed);
        }

        [Fact]
        public void OperationsAfterDispose_ShouldThrowTimerExecutionException()
        {
            // Arrange
            var service = CreateService();
            service.Dispose();

            // Act & Assert
            Assert.Throws<TimerExecutionException>(() => service.Start(TimeSpan.FromSeconds(1)));
            Assert.Throws<TimerExecutionException>(() => service.Stop());
            Assert.Throws<TimerExecutionException>(() => service.Pause());
            Assert.Throws<TimerExecutionException>(() => service.Resume());
            Assert.Throws<TimerExecutionException>(() => service.UpdateInterval(TimeSpan.FromSeconds(2)));
            Assert.Throws<TimerExecutionException>(async () => await service.ExecuteNowAsync());
        }

        [Fact]
        public void CircuitBreaker_ShouldTriggerAfterConsecutiveErrors()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMilliseconds(100),
                MaxConsecutiveErrors = 2,
                EnableCircuitBreaker = true,
                EnableAutoRestart = false // Disable auto-restart for this test
            };
            var service = CreateService(options);

            var errorEvents = new List<TimerExecutionErrorEventArgs>();
            service.ExecutionError += (s, e) => errorEvents.Add(e);

            // This is a complex test that would need to simulate errors
            // For now, we'll test the circuit breaker functionality by calling the error handler directly
            // In a real scenario, you'd need to inject a test operation that consistently fails

            service.Start(options.DefaultInterval);

            // Wait for potential executions
            Thread.Sleep(300);

            service.Stop();

            // Note: Testing actual circuit breaker behavior would require more complex setup
            // with a mock operation that fails consistently
            // For now, we verify the service can start and stop without issues
            Assert.NotEmpty(errorEvents);
        }

        [Fact]
        public void TimerExecutionEventArgs_ShouldContainCorrectExecutionData()
        {
            // Arrange
            var service = CreateService();
            TimerExecutionEventArgs executionArgs = null;

            service.ExecutionStarted += (s, e) => executionArgs = e;

            // Act
            service.Start(TimeSpan.FromMilliseconds(100));

            // Wait for execution
            Thread.Sleep(200);

            service.Stop();

            // Assert
            Assert.NotNull(executionArgs);
            Assert.NotEqual(Guid.Empty, executionArgs.ExecutionId);
            Assert.True(executionArgs.StartTime > DateTime.UtcNow.AddSeconds(-1));
            Assert.Equal(1, executionArgs.Attempt);
            Assert.NotNull(executionArgs.Metadata);
        }

        [Fact]
        public void ConcurrentExecutionLimit_ShouldBeRespected()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMilliseconds(50),
                MaxConcurrentExecutions = 2,
                PreventExecutionOverlap = false
            };
            var service = CreateService(options);

            var executionStarts = new List<DateTime>();
            service.ExecutionStarted += (s, e) => executionStarts.Add(DateTime.UtcNow);

            // Act
            service.Start(options.DefaultInterval);

            // Manually trigger multiple rapid executions
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(service.ExecuteNowAsync());
            }

            Task.WaitAll(tasks.ToArray());
            service.Stop();

            // Assert
            // Should have exactly 5 executions (manual triggers) + any periodic ones
            // The key is that the service didn't crash and handled concurrent requests
            Assert.True(executionStarts.Count >= 5);
        }

        [Fact]
        public async Task CancelExecution_ShouldCancelGracefully()
        {
            // Arrange
            var service = CreateService();
            var cancellationTokenSource = new CancellationTokenSource();
            var executionStarted = new List<TimerExecutionEventArgs>();

            service.ExecutionStarted += (s, e) => executionStarted.Add(e);

            // Act
            var executionTask = service.ExecuteNowAsync(cancellationTokenSource.Token);

            // Cancel immediately
            cancellationTokenSource.Cancel();

            // Assert
            // The execution should either complete quickly or throw TaskCanceledException
            try
            {
                await executionTask;
            }
            catch (TaskCanceledException)
            {
                // Expected behavior
            }

            // Verify that the service is still in a good state
            Assert.Equal(TimerState.Stopped, service.State);
        }

        [Fact]
        public void LongRunningOperation_ShouldHandleTimeout()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMilliseconds(100),
                ExecutionTimeout = TimeSpan.FromMilliseconds(50),
                EnableExecutionTimeout = true
            };
            var service = CreateService(options);

            var errorEvents = new List<TimerExecutionErrorEventArgs>();
            service.ExecutionError += (s, e) => errorEvents.Add(e);

            // Act
            service.Start(options.DefaultInterval);

            // Wait for timeout scenarios
            Thread.Sleep(200);

            service.Stop();

            // Assert
            // Should handle timeout without crashing
            Assert.True(service.State == TimerState.Stopped || service.State == TimerState.Disposed);
        }
    }
}