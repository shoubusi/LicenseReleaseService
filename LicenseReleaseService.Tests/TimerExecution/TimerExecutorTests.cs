using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.TimerExecution;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Comprehensive unit tests for TimerExecutor class
    /// </summary>
    public class TimerExecutorTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private TimerExecutor _timer;
        private readonly List<string> _testLog;
        private int _callbackExecutionCount;

        public TimerExecutorTests(ITestOutputHelper output)
        {
            _output = output;
            _testLog = new List<string>();
            _callbackExecutionCount = 0;
        }

        private void Log(string message)
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] {message}";
            _testLog.Add(logMessage);
            _output.WriteLine(logMessage);
        }

        private TimerExecutor CreateTimer(TimerExecutionOptions options = null)
        {
            _timer = new TimerExecutor(options ?? new TimerExecutionOptions());

            // Subscribe to events for testing
            _timer.ExecutionStarted += OnExecutionStarted;
            _timer.ExecutionCompleted += OnExecutionCompleted;
            _timer.ExecutionError += OnExecutionError;
            _timer.StateChanged += OnStateChanged;
            _timer.Paused += OnPaused;
            _timer.Resumed += OnResumed;
            _timer.Stopped += OnStopped;
            _timer.Disposed += OnDisposed;

            return _timer;
        }

        private async Task OnExecutionStarted(object sender, TimerExecutionEventArgs e)
        {
            Log($"Execution started: {e.ExecutionId}, Attempt: {e.Attempt}");
        }

        private async Task OnExecutionCompleted(object sender, TimerExecutionEventArgs e)
        {
            Log($"Execution completed: {e.ExecutionId}, Success: {e.Success}, Duration: {e.Duration.TotalMilliseconds:F2}ms");
        }

        private async Task OnExecutionError(object sender, TimerExecutionErrorEventArgs e)
        {
            Log($"Execution error: {e.Error.Message}, Consecutive errors: {e.ConsecutiveErrors}");
        }

        private async Task OnStateChanged(object sender, TimerStateChangedEventArgs e)
        {
            Log($"State changed: {e.PreviousState} -> {e.NewState}, Reason: {e.Reason ?? "None"}");
        }

        private async Task OnPaused(object sender, TimerExecutionEventArgs e)
        {
            Log($"Timer paused: {e.ExecutionId}");
        }

        private async Task OnResumed(object sender, TimerExecutionEventArgs e)
        {
            Log($"Timer resumed: {e.ExecutionId}");
        }

        private async Task OnStopped(object sender, TimerExecutionEventArgs e)
        {
            Log($"Timer stopped: {e.ExecutionId}");
        }

        private async Task OnDisposed(object sender, EventArgs e)
        {
            Log("Timer disposed");
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithDefaultOptions_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var timer = new TimerExecutor();

            // Assert
            Assert.NotNull(timer);
            Assert.Equal(TimerStatus.Created, timer.Status);
            Assert.Equal(TimerExecutionOptions.DefaultInterval, timer.Interval);
            Assert.False(timer.IsRunning);
            Assert.False(timer.IsPaused);
            Assert.False(timer.IsDisposed);
            Assert.Equal(0, timer.ConsecutiveErrors);
            Assert.Null(timer.LastExecutionTime);
            Assert.Null(timer.NextExecutionTime);
        }

        [Fact]
        public void Constructor_WithOptions_ShouldInitializeWithProvidedOptions()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(30),
                MaxConsecutiveErrors = 10,
                EnableMetrics = false
            };

            // Act
            var timer = new TimerExecutor(options);

            // Assert
            Assert.NotNull(timer);
            Assert.Equal(TimerStatus.Created, timer.Status);
            Assert.Equal(TimeSpan.FromSeconds(30), timer.Interval);
            Assert.Equal(10, timer.Options.MaxConsecutiveErrors);
            Assert.False(timer.Options.EnableMetrics);
        }

        [Fact]
        public void Constructor_WithInterval_ShouldInitializeWithProvidedInterval()
        {
            // Arrange
            var interval = TimeSpan.FromMinutes(2);

            // Act
            var timer = new TimerExecutor(interval);

            // Assert
            Assert.NotNull(timer);
            Assert.Equal(TimerStatus.Created, timer.Status);
            Assert.Equal(interval, timer.Interval);
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TimerExecutor(null));
        }

        #endregion

        #region Start Tests

        [Fact]
        public async Task StartAsync_WhenCreated_ShouldStartTimer()
        {
            // Arrange
            var timer = CreateTimer();
            var executionStartedEvent = new TaskCompletionSource<bool>();
            timer.ExecutionStarted += (s, e) => executionStartedEvent.SetResult(true);

            // Act
            await timer.StartAsync();

            // Assert
            Assert.Equal(TimerStatus.Running, timer.Status);
            Assert.True(timer.IsRunning);
            Assert.NotNull(timer.LastExecutionTime);
            Assert.NotNull(timer.NextExecutionTime);
            Assert.True(executionStartedEvent.Task.Wait(TimeSpan.FromSeconds(2)));
        }

        [Fact]
        public async Task StartAsync_WithCustomInterval_ShouldStartWithCustomInterval()
        {
            // Arrange
            var timer = CreateTimer();
            var customInterval = TimeSpan.FromSeconds(2);

            // Act
            await timer.StartAsync(customInterval);

            // Assert
            Assert.Equal(TimerStatus.Running, timer.Status);
            Assert.Equal(customInterval, timer.Interval);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyRunning_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => timer.StartAsync());
            Assert.Contains("Cannot start timer from Running state", exception.Message);
        }

        [Fact]
        public async Task StartAsync_WhenDisposed_ShouldThrowObjectDisposedException()
        {
            // Arrange
            var timer = CreateTimer();
            timer.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(() => timer.StartAsync());
        }

        [Fact]
        public async Task StartAsync_WithInvalidInterval_ShouldThrowArgumentException()
        {
            // Arrange
            var timer = CreateTimer();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => timer.StartAsync(TimeSpan.Zero));
            await Assert.ThrowsAsync<ArgumentException>(() => timer.StartAsync(TimeSpan.FromSeconds(-1)));
            await Assert.ThrowsAsync<ArgumentException>(() => timer.StartAsync(TimeSpan.FromMilliseconds(1)));
        }

        [Fact]
        public async Task StartAsync_WhenStopped_ShouldRestartTimer()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();
            await timer.StopAsync();

            // Act
            await timer.StartAsync();

            // Assert
            Assert.Equal(TimerStatus.Running, timer.Status);
            Assert.True(timer.IsRunning);
        }

        #endregion

        #region Stop Tests

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldStopTimer()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();

            // Act
            await timer.StopAsync();

            // Assert
            Assert.Equal(TimerStatus.Stopped, timer.Status);
            Assert.False(timer.IsRunning);
            Assert.False(timer.IsPaused);
        }

        [Fact]
        public async Task StopAsync_WhenPaused_ShouldStopTimer()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();
            await timer.PauseAsync();

            // Act
            await timer.StopAsync();

            // Assert
            Assert.Equal(TimerStatus.Stopped, timer.Status);
            Assert.False(timer.IsRunning);
            Assert.False(timer.IsPaused);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldDoNothing()
        {
            // Arrange
            var timer = CreateTimer();

            // Act
            await timer.StopAsync();

            // Assert
            Assert.Equal(TimerStatus.Stopped, timer.Status);
        }

        [Fact]
        public async Task StopAsync_WhenDisposed_ShouldThrowObjectDisposedException()
        {
            // Arrange
            var timer = CreateTimer();
            timer.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(() => timer.StopAsync());
        }

        #endregion

        #region Pause/Resume Tests

        [Fact]
        public async Task PauseAsync_WhenRunning_ShouldPauseTimer()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();

            // Act
            await timer.PauseAsync();

            // Assert
            Assert.Equal(TimerStatus.Paused, timer.Status);
            Assert.True(timer.IsPaused);
            Assert.False(timer.IsRunning);
        }

        [Fact]
        public async Task PauseAsync_WhenNotRunning_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var timer = CreateTimer();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => timer.PauseAsync());
        }

        [Fact]
        public async Task ResumeAsync_WhenPaused_ShouldResumeTimer()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();
            await timer.PauseAsync();

            // Act
            await timer.ResumeAsync();

            // Assert
            Assert.Equal(TimerStatus.Running, timer.Status);
            Assert.False(timer.IsPaused);
            Assert.True(timer.IsRunning);
        }

        [Fact]
        public async Task ResumeAsync_WhenNotPaused_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => timer.ResumeAsync());
        }

        [Fact]
        public async Task PauseResumeCycle_ShouldMaintainTimerState()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();

            // Act & Assert
            for (int i = 0; i < 3; i++)
            {
                await timer.PauseAsync();
                Assert.Equal(TimerStatus.Paused, timer.Status);

                await timer.ResumeAsync();
                Assert.Equal(TimerStatus.Running, timer.Status);
            }
        }

        #endregion

        #region Restart Tests

        [Fact]
        public async Task RestartAsync_WhenRunning_ShouldRestartTimer()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();
            var firstExecutionTime = timer.LastExecutionTime;

            // Act
            await timer.RestartAsync();

            // Assert
            Assert.Equal(TimerStatus.Running, timer.Status);
            Assert.True(timer.IsRunning);
            Assert.NotEqual(firstExecutionTime, timer.LastExecutionTime);
        }

        [Fact]
        public async Task RestartAsync_WhenPaused_ShouldRestartTimer()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();
            await timer.PauseAsync();

            // Act
            await timer.RestartAsync();

            // Assert
            Assert.Equal(TimerStatus.Running, timer.Status);
            Assert.True(timer.IsRunning);
            Assert.False(timer.IsPaused);
        }

        [Fact]
        public async Task RestartAsync_WhenInErrorState_ShouldRestartTimer()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();
            // Simulate error state
            await timer.StopAsync();

            // Act
            await timer.RestartAsync();

            // Assert
            Assert.Equal(TimerStatus.Running, timer.Status);
            Assert.True(timer.IsRunning);
        }

        #endregion

        #region Callback Tests

        [Fact]
        public async Task RegisterCallback_WhenTimerExecutes_ShouldExecuteCallback()
        {
            // Arrange
            var timer = CreateTimer();
            var callbackExecuted = new TaskCompletionSource<bool>();

            timer.RegisterCallback(async () =>
            {
                Interlocked.Increment(ref _callbackExecutionCount);
                callbackExecuted.SetResult(true);
            });

            // Act
            await timer.StartAsync();

            // Assert
            Assert.True(callbackExecuted.Task.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(_callbackExecutionCount > 0);
        }

        [Fact]
        public async Task RegisterCallbackWithEventArgs_WhenTimerExecutes_ShouldExecuteCallbackWithEventArgs()
        {
            // Arrange
            var timer = CreateTimer();
            var callbackExecuted = new TaskCompletionSource<TimerExecutionEventArgs>();

            timer.RegisterCallback(async (args) =>
            {
                callbackExecuted.SetResult(args);
            });

            // Act
            await timer.StartAsync();

            // Assert
            var result = await callbackExecuted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ExecutionId);
            Assert.True(result.StartTime <= DateTime.UtcNow);
        }

        [Fact]
        public async Task UnregisterCallback_WhenTimerExecutes_ShouldNotExecuteCallback()
        {
            // Arrange
            var timer = CreateTimer();
            var callbackExecuted = false;

            Func<Task> callback = async () =>
            {
                callbackExecuted = true;
            };

            timer.RegisterCallback(callback);
            timer.UnregisterCallback(callback);

            // Act
            await timer.StartAsync();
            await Task.Delay(2000); // Wait for potential execution

            // Assert
            Assert.False(callbackExecuted);
        }

        [Fact]
        public async Task MultipleCallbacks_WhenTimerExecutes_ShouldExecuteAllCallbacks()
        {
            // Arrange
            var timer = CreateTimer();
            var executionCount = 0;
            var callbacksCompleted = new TaskCompletionSource<bool>();

            for (int i = 0; i < 5; i++)
            {
                timer.RegisterCallback(async () =>
                {
                    Interlocked.Increment(ref executionCount);
                    if (executionCount == 5)
                    {
                        callbacksCompleted.SetResult(true);
                    }
                });
            }

            // Act
            await timer.StartAsync();

            // Assert
            Assert.True(callbacksCompleted.Task.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(5, executionCount);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CallbackThrowsException_ShouldHandleErrorAndContinue()
        {
            // Arrange
            var timer = CreateTimer();
            var errorHandled = new TaskCompletionSource<bool>();

            timer.RegisterCallback(async () =>
            {
                throw new InvalidOperationException("Test exception");
            });

            timer.ExecutionError += (s, e) =>
            {
                if (e.Error.Message == "Test exception")
                {
                    errorHandled.SetResult(true);
                }
            };

            // Act
            await timer.StartAsync();

            // Assert
            Assert.True(errorHandled.Task.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(TimerStatus.Running, timer.Status); // Timer should still be running
        }

        [Fact]
        public async Task ConsecutiveErrors_ShouldTriggerCircuitBreaker()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(1),
                MaxConsecutiveErrors = 3,
                CircuitBreakerCooldown = TimeSpan.FromSeconds(5),
                EnableCircuitBreaker = true
            };

            var timer = CreateTimer(options);
            var circuitBreakerTriggered = new TaskCompletionSource<bool>();

            timer.RegisterCallback(async () =>
            {
                throw new InvalidOperationException("Test exception");
            });

            timer.StateChanged += (s, e) =>
            {
                if (e.NewState == TimerState.CircuitBreaker)
                {
                    circuitBreakerTriggered.SetResult(true);
                }
            };

            // Act
            await timer.StartAsync();

            // Assert
            Assert.True(circuitBreakerTriggered.Task.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(TimerStatus.CircuitBreaker, timer.Status);
        }

        [Fact]
        public async Task FatalError_ShouldStopTimer()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(1),
                StopOnUnhandledException = true
            };

            var timer = CreateTimer(options);
            var timerStopped = new TaskCompletionSource<bool>();

            timer.RegisterCallback(async () =>
            {
                throw new OutOfMemoryException("Fatal error");
            });

            timer.Stopped += (s, e) =>
            {
                timerStopped.SetResult(true);
            };

            // Act
            await timer.StartAsync();

            // Assert
            Assert.True(timerStopped.Task.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(TimerStatus.Stopped, timer.Status);
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public async Task UpdateOptionsAsync_WithValidOptions_ShouldUpdateConfiguration()
        {
            // Arrange
            var timer = CreateTimer();
            var newOptions = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(30),
                MaxConsecutiveErrors = 10,
                EnableMetrics = false
            };

            // Act
            await timer.UpdateOptionsAsync(newOptions);

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(30), timer.Interval);
            Assert.Equal(10, timer.Options.MaxConsecutiveErrors);
            Assert.False(timer.Options.EnableMetrics);
        }

        [Fact]
        public async Task UpdateOptionsAsync_WithInvalidOptions_ShouldThrowArgumentException()
        {
            // Arrange
            var timer = CreateTimer();
            var invalidOptions = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.Zero
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => timer.UpdateOptionsAsync(invalidOptions));
        }

        [Fact]
        public async Task ChangeIntervalAsync_WithValidInterval_ShouldChangeInterval()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();
            var newInterval = TimeSpan.FromSeconds(30);

            // Act
            await timer.ChangeIntervalAsync(newInterval);

            // Assert
            Assert.Equal(newInterval, timer.Interval);
        }

        [Fact]
        public async Task ChangeIntervalAsync_WithInvalidInterval_ShouldThrowArgumentException()
        {
            // Arrange
            var timer = CreateTimer();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => timer.ChangeIntervalAsync(TimeSpan.Zero));
            await Assert.ThrowsAsync<ArgumentException>(() => timer.ChangeIntervalAsync(TimeSpan.FromMilliseconds(1)));
        }

        #endregion

        #region Information and Diagnostics Tests

        [Fact]
        public void GetStatusInfo_WhenCreated_ShouldReturnCorrectStatus()
        {
            // Arrange
            var timer = CreateTimer();

            // Act
            var statusInfo = timer.GetStatusInfo();

            // Assert
            Assert.Equal(TimerStatus.Created, statusInfo.Status);
            Assert.Equal(TimerExecutionOptions.DefaultInterval, statusInfo.Interval);
            Assert.Null(statusInfo.LastExecutionTime);
            Assert.Null(statusInfo.NextExecutionTime);
            Assert.Equal(0, statusInfo.ConsecutiveErrors);
            Assert.Equal(0, statusInfo.TotalExecutions);
        }

        [Fact]
        public async Task GetStatusInfo_WhenRunning_ShouldReturnRunningStatus()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();

            // Act
            var statusInfo = timer.GetStatusInfo();

            // Assert
            Assert.Equal(TimerStatus.Running, statusInfo.Status);
            Assert.NotNull(statusInfo.LastExecutionTime);
            Assert.NotNull(statusInfo.NextExecutionTime);
            Assert.True(statusInfo.Uptime.TotalMilliseconds > 0);
        }

        [Fact]
        public void GetDiagnostics_WhenCreated_ShouldReturnCompleteDiagnostics()
        {
            // Arrange
            var timer = CreateTimer();

            // Act
            var diagnostics = timer.GetDiagnostics();

            // Assert
            Assert.NotNull(diagnostics.Status);
            Assert.NotNull(diagnostics.Options);
            Assert.NotNull(diagnostics.Metrics);
            Assert.NotNull(diagnostics.ThreadInfo);
            Assert.NotNull(diagnostics.MemoryInfo);
            Assert.NotNull(diagnostics.SystemInfo);
        }

        [Fact]
        public async Task GetExecutionHistory_WhenExecuted_ShouldReturnExecutionHistory()
        {
            // Arrange
            var timer = CreateTimer();
            timer.RegisterCallback(async () => { /* Do nothing */ });
            await timer.StartAsync();
            await Task.Delay(2000); // Allow some executions

            // Act
            var history = timer.GetExecutionHistory();

            // Assert
            Assert.NotEmpty(history);
            Assert.True(history.Count > 0);
        }

        [Fact]
        public void GetExecutionHistory_WithMaxItems_ShouldReturnLimitedHistory()
        {
            // Arrange
            var timer = CreateTimer();

            // Act
            var history = timer.GetExecutionHistory(5);

            // Assert
            Assert.True(history.Count <= 5);
        }

        #endregion

        #region Metrics Tests

        [Fact]
        public async Task Metrics_WhenExecuted_ShouldTrackCorrectly()
        {
            // Arrange
            var timer = CreateTimer();
            timer.RegisterCallback(async () => { /* Do nothing */ });

            // Act
            await timer.StartAsync();
            await Task.Delay(2000); // Allow some executions
            var metrics = timer.Metrics;

            // Assert
            Assert.True(metrics.TotalExecutions > 0);
            Assert.True(metrics.SuccessfulExecutions > 0);
            Assert.Equal(0, metrics.FailedExecutions);
            Assert.True(metrics.Uptime.TotalMilliseconds > 0);
            Assert.NotNull(metrics.LastExecutionTime);
        }

        [Fact]
        public async Task Metrics_WhenErrorOccurs_ShouldTrackFailures()
        {
            // Arrange
            var timer = CreateTimer();
            timer.RegisterCallback(async () =>
            {
                throw new InvalidOperationException("Test error");
            });

            // Act
            await timer.StartAsync();
            await Task.Delay(3000); // Allow some executions and errors
            var metrics = timer.Metrics;

            // Assert
            Assert.True(metrics.TotalExecutions > 0);
            Assert.Equal(0, metrics.SuccessfulExecutions);
            Assert.True(metrics.FailedExecutions > 0);
            Assert.True(metrics.CurrentConsecutiveErrors > 0);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task ConcurrentOperations_ShouldBeThreadSafe()
        {
            // Arrange
            var timer = CreateTimer();
            var tasks = new List<Task>();

            // Act
            tasks.Add(Task.Run(async () =>
            {
                await timer.StartAsync();
                await Task.Delay(1000);
                await timer.PauseAsync();
                await Task.Delay(500);
                await timer.ResumeAsync();
            }));

            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(500);
                var status = timer.Status;
                var isRunning = timer.IsRunning;
                var metrics = timer.Metrics;
            }));

            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(750);
                await timer.ChangeIntervalAsync(TimeSpan.FromSeconds(2));
            }));

            // Assert
            await Task.WhenAll(tasks);
            Assert.Equal(TimerStatus.Running, timer.Status); // Final state should be running
        }

        [Fact]
        public async Task PreventExecutionOverlap_WhenEnabled_ShouldPreventConcurrentExecution()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMilliseconds(100),
                PreventExecutionOverlap = true
            };

            var timer = CreateTimer(options);
            var executionSemaphore = new SemaphoreSlim(0, 1);
            var overlapDetected = false;

            timer.RegisterCallback(async () =>
            {
                overlapDetected = executionSemaphore.CurrentCount == 0;
                await executionSemaphore.WaitAsync();
                await Task.Delay(1000); // Simulate long execution
                executionSemaphore.Release();
            });

            // Act
            await timer.StartAsync();
            await Task.Delay(2000); // Allow multiple execution attempts
            await timer.StopAsync();

            // Assert
            Assert.False(overlapDetected, "Execution overlap detected when it should have been prevented");
        }

        #endregion

        #region Reset Tests

        [Fact]
        public async Task ResetAsync_WhenCalled_ShouldResetAllState()
        {
            // Arrange
            var timer = CreateTimer();
            timer.RegisterCallback(async () =>
            {
                throw new InvalidOperationException("Test error");
            });

            await timer.StartAsync();
            await Task.Delay(2000); // Generate some errors

            // Act
            await timer.ResetAsync();

            // Assert
            Assert.Equal(0, timer.ConsecutiveErrors);
            Assert.Equal(0, timer.Metrics.TotalExecutions);
            Assert.Equal(0, timer.Metrics.CurrentConsecutiveErrors);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_WhenCalled_ShouldReleaseResources()
        {
            // Arrange
            var timer = CreateTimer();

            // Act
            timer.Dispose();

            // Assert
            Assert.True(timer.IsDisposed);
            Assert.Equal(TimerStatus.Disposed, timer.Status);
        }

        [Fact]
        public async Task Dispose_WhenTimerRunning_ShouldStopAndDispose()
        {
            // Arrange
            var timer = CreateTimer();
            await timer.StartAsync();

            // Act
            timer.Dispose();

            // Assert
            Assert.True(timer.IsDisposed);
            Assert.Equal(TimerStatus.Disposed, timer.Status);
            Assert.False(timer.IsRunning);
        }

        [Fact]
        public void OperationsAfterDispose_ShouldThrowObjectDisposedException()
        {
            // Arrange
            var timer = CreateTimer();
            timer.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => timer.StartAsync());
            Assert.Throws<ObjectDisposedException>(() => timer.StopAsync());
            Assert.Throws<ObjectDisposedException>(() => timer.PauseAsync());
            Assert.Throws<ObjectDisposedException>(() => timer.GetStatusInfo());
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task FullLifecycleTest_ShouldWorkCorrectly()
        {
            // Arrange
            var timer = CreateTimer();
            var callbackExecuted = 0;

            timer.RegisterCallback(async () =>
            {
                Interlocked.Increment(ref callbackExecuted);
            });

            // Act: Start -> Pause -> Resume -> Stop -> Restart -> Stop -> Dispose
            await timer.StartAsync();
            Assert.Equal(TimerStatus.Running, timer.Status);

            await Task.Delay(1000); // Let it run a bit
            await timer.PauseAsync();
            Assert.Equal(TimerStatus.Paused, timer.Status);

            await timer.ResumeAsync();
            Assert.Equal(TimerStatus.Running, timer.Status);

            await Task.Delay(1000); // Let it run a bit more
            await timer.StopAsync();
            Assert.Equal(TimerStatus.Stopped, timer.Status);

            await timer.RestartAsync();
            Assert.Equal(TimerStatus.Running, timer.Status);

            await Task.Delay(1000); // Final run
            await timer.StopAsync();
            Assert.Equal(TimerStatus.Stopped, timer.Status);

            // Assert
            Assert.True(callbackExecuted > 0);
            Assert.False(timer.IsRunning);
            Assert.False(timer.IsPaused);
            Assert.Equal(0, timer.ConsecutiveErrors);
        }

        [Fact]
        public async Task StressTest_ShouldHandleHighFrequencyExecution()
        {
            // Arrange
            var options = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromMilliseconds(50),
                MaxExecutionHistory = 1000
            };

            var timer = CreateTimer(options);
            var executionCount = 0;
            var stressTestDuration = TimeSpan.FromSeconds(5);
            var stopwatch = Stopwatch.StartNew();

            timer.RegisterCallback(async () =>
            {
                Interlocked.Increment(ref executionCount);
            });

            // Act
            await timer.StartAsync();
            await Task.Delay(stressTestDuration);
            await timer.StopAsync();

            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Executed {executionCount} times in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
            Assert.True(executionCount > 10, "Should have executed multiple times during stress test");
            Assert.True(timer.Metrics.SuccessRate > 90, "Should have high success rate");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task Performance_MultipleCallbacks_ShouldExecuteEfficiently()
        {
            // Arrange
            var timer = CreateTimer();
            var callbackCount = 100;
            var setupTime = Stopwatch.StartNew();

            for (int i = 0; i < callbackCount; i++)
            {
                timer.RegisterCallback(async () => { /* Do nothing */ });
            }

            setupTime.Stop();

            // Act
            var executionTime = Stopwatch.StartNew();
            await timer.StartAsync();
            await Task.Delay(1000);
            await timer.StopAsync();
            executionTime.Stop();

            // Assert
            _output.WriteLine($"Setup time: {setupTime.Elapsed.TotalMilliseconds:F2}ms for {callbackCount} callbacks");
            _output.WriteLine($"Execution time: {executionTime.Elapsed.TotalMilliseconds:F2}ms");
            Assert.True(setupTime.Elapsed.TotalMilliseconds < 1000, "Setup should be fast");
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            _timer?.Dispose();
        }

        #endregion
    }
}