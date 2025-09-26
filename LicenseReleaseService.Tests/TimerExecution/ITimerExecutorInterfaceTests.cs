using System;
using System.Threading.Tasks;
using LicenseReleaseService.TimerExecution;
using Xunit;

namespace LicenseReleaseService.Tests.TimerExecution
{
    /// <summary>
    /// Interface compliance tests for ITimerExecutor implementations
    /// </summary>
    public class ITimerExecutorInterfaceTests : IDisposable
    {
        private readonly ITimerExecutor _timer;

        public ITimerExecutorInterfaceTests()
        {
            _timer = new TimerExecutor();
        }

        [Fact]
        public void ITimerExecutor_ShouldHaveCorrectInterface()
        {
            // Assert
            Assert.IsAssignableFrom<ITimerExecutor>(_timer);
            Assert.IsAssignableFrom<IDisposable>(_timer);
        }

        [Fact]
        public void Status_ShouldReturnTimerStatus()
        {
            // Act
            var status = _timer.Status;

            // Assert
            Assert.IsType<TimerStatus>(status);
            Assert.True(Enum.IsDefined(typeof(TimerStatus), status));
        }

        [Fact]
        public void Options_ShouldReturnTimerExecutionOptions()
        {
            // Act
            var options = _timer.Options;

            // Assert
            Assert.IsType<TimerExecutionOptions>(options);
            Assert.True(options.DefaultInterval > TimeSpan.Zero);
            Assert.True(options.MaxConsecutiveErrors > 0);
        }

        [Fact]
        public void Interval_ShouldReturnTimeSpan()
        {
            // Act
            var interval = _timer.Interval;

            // Assert
            Assert.IsType<TimeSpan>(interval);
            Assert.True(interval > TimeSpan.Zero);
        }

        [Fact]
        public void IsRunning_ShouldReturnBoolean()
        {
            // Act
            var isRunning = _timer.IsRunning;

            // Assert
            Assert.IsType<bool>(isRunning);
        }

        [Fact]
        public void IsPaused_ShouldReturnBoolean()
        {
            // Act
            var isPaused = _timer.IsPaused;

            // Assert
            Assert.IsType<bool>(isPaused);
        }

        [Fact]
        public void IsDisposed_ShouldReturnBoolean()
        {
            // Act
            var isDisposed = _timer.IsDisposed;

            // Assert
            Assert.IsType<bool>(isDisposed);
        }

        [Fact]
        public void Metrics_ShouldReturnTimerPerformanceMetrics()
        {
            // Act
            var metrics = _timer.Metrics;

            // Assert
            Assert.IsType<TimerPerformanceMetrics>(metrics);
            Assert.True(metrics.TotalExecutions >= 0);
            Assert.True(metrics.SuccessfulExecutions >= 0);
            Assert.True(metrics.FailedExecutions >= 0);
        }

        [Fact]
        public void ConsecutiveErrors_ShouldReturnNonNegativeInteger()
        {
            // Act
            var consecutiveErrors = _timer.ConsecutiveErrors;

            // Assert
            Assert.True(consecutiveErrors >= 0);
        }

        [Fact]
        public void LastExecutionTime_ShouldReturnNullableDateTime()
        {
            // Act
            var lastExecutionTime = _timer.LastExecutionTime;

            // Assert
            Assert.True(lastExecutionTime == null || lastExecutionTime.Value <= DateTime.UtcNow);
        }

        [Fact]
        public void NextExecutionTime_ShouldReturnNullableDateTime()
        {
            // Act
            var nextExecutionTime = _timer.NextExecutionTime;

            // Assert
            Assert.True(nextExecutionTime == null || nextExecutionTime.Value >= DateTime.UtcNow);
        }

        [Fact]
        public async Task StartAsync_ShouldBeCallable()
        {
            // Act & Assert
            await _timer.StartAsync();
            Assert.Equal(TimerStatus.Running, _timer.Status);
        }

        [Fact]
        public async Task StartAsync_WithInterval_ShouldBeCallable()
        {
            // Arrange
            var interval = TimeSpan.FromSeconds(2);

            // Act & Assert
            await _timer.StartAsync(interval);
            Assert.Equal(TimerStatus.Running, _timer.Status);
            Assert.Equal(interval, _timer.Interval);
        }

        [Fact]
        public async Task StopAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();

            // Act & Assert
            await _timer.StopAsync();
            Assert.Equal(TimerStatus.Stopped, _timer.Status);
        }

        [Fact]
        public async Task PauseAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();

            // Act & Assert
            await _timer.PauseAsync();
            Assert.Equal(TimerStatus.Paused, _timer.Status);
        }

        [Fact]
        public async Task ResumeAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();
            await _timer.PauseAsync();

            // Act & Assert
            await _timer.ResumeAsync();
            Assert.Equal(TimerStatus.Running, _timer.Status);
        }

        [Fact]
        public async Task RestartAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();

            // Act & Assert
            await _timer.RestartAsync();
            Assert.Equal(TimerStatus.Running, _timer.Status);
        }

        [Fact]
        public async Task TriggerExecutionAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();

            // Act & Assert
            await _timer.TriggerExecutionAsync();
            // No exception should be thrown
        }

        [Fact]
        public async Task ChangeIntervalAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();
            var newInterval = TimeSpan.FromSeconds(5);

            // Act & Assert
            await _timer.ChangeIntervalAsync(newInterval);
            Assert.Equal(newInterval, _timer.Interval);
        }

        [Fact]
        public async Task ResetAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();

            // Act & Assert
            await _timer.ResetAsync();
            Assert.Equal(0, _timer.ConsecutiveErrors);
        }

        [Fact]
        public async Task WaitForCurrentExecutionAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();

            // Act & Assert
            var result = await _timer.WaitForCurrentExecutionAsync(TimeSpan.FromSeconds(1));
            Assert.IsType<bool>(result);
        }

        [Fact]
        public async Task CancelCurrentExecutionAsync_ShouldBeCallable()
        {
            // Arrange
            await _timer.StartAsync();

            // Act & Assert
            await _timer.CancelCurrentExecutionAsync();
            // No exception should be thrown
        }

        [Fact]
        public async Task UpdateOptionsAsync_ShouldBeCallable()
        {
            // Arrange
            var newOptions = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(10),
                MaxConsecutiveErrors = 5
            };

            // Act & Assert
            await _timer.UpdateOptionsAsync(newOptions);
            Assert.Equal(TimeSpan.FromSeconds(10), _timer.Interval);
        }

        [Fact]
        public void ValidateConfiguration_ShouldReturnListOfStrings()
        {
            // Act
            var validationErrors = _timer.ValidateConfiguration();

            // Assert
            Assert.IsType<System.Collections.Generic.List<string>>(validationErrors);
            // Should be empty for default valid configuration
        }

        [Fact]
        public void GetStatusInfo_ShouldReturnTimerStatusInfo()
        {
            // Act
            var statusInfo = _timer.GetStatusInfo();

            // Assert
            Assert.IsType<TimerStatusInfo>(statusInfo);
            Assert.True(Enum.IsDefined(typeof(TimerStatus), statusInfo.Status));
            Assert.True(statusInfo.Interval > TimeSpan.Zero);
        }

        [Fact]
        public void GetExecutionHistory_ShouldReturnListOfTimerExecutionEventArgs()
        {
            // Act
            var executionHistory = _timer.GetExecutionHistory();

            // Assert
            Assert.IsType<System.Collections.Generic.List<TimerExecutionEventArgs>>(executionHistory);
        }

        [Fact]
        public void GetExecutionHistory_WithMaxItems_ShouldRespectLimit()
        {
            // Act
            var executionHistory = _timer.GetExecutionHistory(5);

            // Assert
            Assert.IsType<System.Collections.Generic.List<TimerExecutionEventArgs>>(executionHistory);
            Assert.True(executionHistory.Count <= 5);
        }

        [Fact]
        public void GetDiagnostics_ShouldReturnTimerDiagnosticsInfo()
        {
            // Act
            var diagnostics = _timer.GetDiagnostics();

            // Assert
            Assert.IsType<TimerDiagnosticsInfo>(diagnostics);
            Assert.NotNull(diagnostics.Status);
            Assert.NotNull(diagnostics.Options);
            Assert.NotNull(diagnostics.Metrics);
            Assert.NotNull(diagnostics.ThreadInfo);
            Assert.NotNull(diagnostics.MemoryInfo);
            Assert.NotNull(diagnostics.SystemInfo);
        }

        [Fact]
        public void Dispose_ShouldBeCallable()
        {
            // Act & Assert
            _timer.Dispose();
            Assert.True(_timer.IsDisposed);
        }

        [Fact]
        public async Task FullLifecycle_ShouldWorkThroughInterface()
        {
            // This test verifies that the complete lifecycle works through the ITimerExecutor interface

            // Start
            await _timer.StartAsync();
            Assert.Equal(TimerStatus.Running, _timer.Status);
            Assert.True(_timer.IsRunning);

            // Pause
            await _timer.PauseAsync();
            Assert.Equal(TimerStatus.Paused, _timer.Status);
            Assert.True(_timer.IsPaused);

            // Resume
            await _timer.ResumeAsync();
            Assert.Equal(TimerStatus.Running, _timer.Status);
            Assert.True(_timer.IsRunning);

            // Change interval
            await _timer.ChangeIntervalAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(TimeSpan.FromSeconds(3), _timer.Interval);

            // Get status info
            var statusInfo = _timer.GetStatusInfo();
            Assert.NotNull(statusInfo);
            Assert.Equal(TimerStatus.Running, statusInfo.Status);

            // Get diagnostics
            var diagnostics = _timer.GetDiagnostics();
            Assert.NotNull(diagnostics);

            // Stop
            await _timer.StopAsync();
            Assert.Equal(TimerStatus.Stopped, _timer.Status);
            Assert.False(_timer.IsRunning);

            // Restart
            await _timer.RestartAsync();
            Assert.Equal(TimerStatus.Running, _timer.Status);

            // Reset
            await _timer.ResetAsync();
            Assert.Equal(0, _timer.ConsecutiveErrors);

            // Final stop
            await _timer.StopAsync();
            Assert.Equal(TimerStatus.Stopped, _timer.Status);
        }

        [Fact]
        public async Task Events_ShouldBeAccessibleThroughInterface()
        {
            // Arrange
            var eventTriggered = new TaskCompletionSource<bool>();

            // Subscribe to events through the interface
            _timer.ExecutionStarted += (s, e) => eventTriggered.SetResult(true);
            _timer.ExecutionCompleted += (s, e) => { };
            _timer.ExecutionError += (s, e) => { };
            _timer.StateChanged += (s, e) => { };
            _timer.Paused += (s, e) => { };
            _timer.Resumed += (s, e) => { };
            _timer.Stopped += (s, e) => { };
            _timer.Disposed += (s, e) => { };

            // Act
            await _timer.StartAsync();

            // Assert
            Assert.True(eventTriggered.Task.Wait(TimeSpan.FromSeconds(2)));
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

    /// <summary>
    /// Tests for TimerStatusInfo class
    /// </summary>
    public class TimerStatusInfoTests
    {
        [Fact]
        public void TimerStatusInfo_ShouldHaveDefaultValues()
        {
            // Act
            var statusInfo = new TimerStatusInfo();

            // Assert
            Assert.Equal(default(TimerStatus), statusInfo.Status);
            Assert.Equal(default(TimeSpan), statusInfo.Interval);
            Assert.Null(statusInfo.LastExecutionTime);
            Assert.Null(statusInfo.NextExecutionTime);
            Assert.Equal(0, statusInfo.ConsecutiveErrors);
            Assert.Equal(0, statusInfo.TotalExecutions);
            Assert.Equal(default(TimeSpan), statusInfo.Uptime);
            Assert.Equal(default(DateTime), statusInfo.StateTimestamp);
            Assert.Null(statusInfo.StartTime);
            Assert.Null(statusInfo.StopTime);
        }

        [Fact]
        public void TimerStatusInfo_ShouldAllowSettingAllProperties()
        {
            // Arrange
            var statusInfo = new TimerStatusInfo();

            // Act
            statusInfo.Status = TimerStatus.Running;
            statusInfo.Interval = TimeSpan.FromSeconds(30);
            statusInfo.LastExecutionTime = DateTime.UtcNow.AddSeconds(-10);
            statusInfo.NextExecutionTime = DateTime.UtcNow.AddSeconds(20);
            statusInfo.ConsecutiveErrors = 5;
            statusInfo.TotalExecutions = 100;
            statusInfo.Uptime = TimeSpan.FromMinutes(5);
            statusInfo.StateTimestamp = DateTime.UtcNow;
            statusInfo.StartTime = DateTime.UtcNow.AddMinutes(-5);
            statusInfo.StopTime = DateTime.UtcNow;

            // Assert
            Assert.Equal(TimerStatus.Running, statusInfo.Status);
            Assert.Equal(TimeSpan.FromSeconds(30), statusInfo.Interval);
            Assert.NotNull(statusInfo.LastExecutionTime);
            Assert.NotNull(statusInfo.NextExecutionTime);
            Assert.Equal(5, statusInfo.ConsecutiveErrors);
            Assert.Equal(100, statusInfo.TotalExecutions);
            Assert.Equal(TimeSpan.FromMinutes(5), statusInfo.Uptime);
            Assert.NotEqual(default(DateTime), statusInfo.StateTimestamp);
            Assert.NotNull(statusInfo.StartTime);
            Assert.NotNull(statusInfo.StopTime);
        }
    }

    /// <summary>
    /// Tests for TimerDiagnosticsInfo class
    /// </summary>
    public class TimerDiagnosticsInfoTests
    {
        [Fact]
        public void TimerDiagnosticsInfo_ShouldHaveAllRequiredProperties()
        {
            // Arrange
            var timer = new TimerExecutor();
            var diagnostics = timer.GetDiagnostics();

            // Assert
            Assert.NotNull(diagnostics.Status);
            Assert.NotNull(diagnostics.Options);
            Assert.NotNull(diagnostics.Metrics);
            Assert.NotNull(diagnostics.ThreadInfo);
            Assert.NotNull(diagnostics.MemoryInfo);
            Assert.NotNull(diagnostics.SystemInfo);
            Assert.NotNull(diagnostics.RecentErrors);
        }

        [Fact]
        public void TimerDiagnosticsInfo_ShouldContainValidThreadInfo()
        {
            // Arrange
            var timer = new TimerExecutor();
            var diagnostics = timer.GetDiagnostics();

            // Assert
            Assert.True(diagnostics.ThreadInfo.ManagedThreadId > 0);
            Assert.NotNull(diagnostics.ThreadInfo.ThreadState);
            Assert.NotNull(diagnostics.ThreadInfo.ThreadPriority);
        }

        [Fact]
        public void TimerDiagnosticsInfo_ShouldContainValidMemoryInfo()
        {
            // Arrange
            var timer = new TimerExecutor();
            var diagnostics = timer.GetDiagnostics();

            // Assert
            Assert.True(diagnostics.MemoryInfo.CurrentMemoryUsage >= 0);
            Assert.True(diagnostics.MemoryInfo.PeakMemoryUsage >= 0);
            Assert.True(diagnostics.MemoryInfo.TrackedObjects >= 0);
            Assert.NotNull(diagnostics.MemoryInfo.GarbageCollectionInfo);
        }

        [Fact]
        public void TimerDiagnosticsInfo_ShouldContainValidSystemInfo()
        {
            // Arrange
            var timer = new TimerExecutor();
            var diagnostics = timer.GetDiagnostics();

            // Assert
            Assert.NotNull(diagnostics.SystemInfo.OSVersion);
            Assert.NotNull(diagnostics.SystemInfo.RuntimeVersion);
            Assert.True(diagnostics.SystemInfo.ProcessorCount > 0);
            Assert.True(diagnostics.SystemInfo.WorkingSet >= 0);
            Assert.True(diagnostics.SystemInfo.SystemUptime.TotalMilliseconds >= 0);
        }
    }

    /// <summary>
    /// Tests for TimerThreadInfo class
    /// </summary>
    public class TimerThreadInfoTests
    {
        [Fact]
        public void TimerThreadInfo_ShouldHaveDefaultValues()
        {
            // Act
            var threadInfo = new TimerThreadInfo();

            // Assert
            Assert.Equal(0, threadInfo.ManagedThreadId);
            Assert.Equal(default(System.Threading.ThreadState), threadInfo.ThreadState);
            Assert.Equal(default(System.Threading.ThreadPriority), threadInfo.ThreadPriority);
            Assert.False(threadInfo.IsBackground);
            Assert.False(threadInfo.IsThreadPoolThread);
        }

        [Fact]
        public void TimerThreadInfo_ShouldAllowSettingAllProperties()
        {
            // Arrange
            var threadInfo = new TimerThreadInfo();

            // Act
            threadInfo.ManagedThreadId = 42;
            threadInfo.ThreadState = System.Threading.ThreadState.Running;
            threadInfo.ThreadPriority = System.Threading.ThreadPriority.AboveNormal;
            threadInfo.IsBackground = true;
            threadInfo.IsThreadPoolThread = true;

            // Assert
            Assert.Equal(42, threadInfo.ManagedThreadId);
            Assert.Equal(System.Threading.ThreadState.Running, threadInfo.ThreadState);
            Assert.Equal(System.Threading.ThreadPriority.AboveNormal, threadInfo.ThreadPriority);
            Assert.True(threadInfo.IsBackground);
            Assert.True(threadInfo.IsThreadPoolThread);
        }
    }

    /// <summary>
    /// Tests for TimerErrorInfo class
    /// </summary>
    public class TimerErrorInfoTests
    {
        [Fact]
        public void TimerErrorInfo_ShouldHaveDefaultValues()
        {
            // Act
            var errorInfo = new TimerErrorInfo();

            // Assert
            Assert.Equal(default(DateTime), errorInfo.Timestamp);
            Assert.Null(errorInfo.Message);
            Assert.Null(errorInfo.ErrorType);
            Assert.Null(errorInfo.StackTrace);
            Assert.Null(errorInfo.ExecutionId);
            Assert.Equal(0, errorInfo.ConsecutiveErrors);
        }

        [Fact]
        public void TimerErrorInfo_ShouldAllowSettingAllProperties()
        {
            // Arrange
            var errorInfo = new TimerErrorInfo();
            var executionId = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;

            // Act
            errorInfo.Timestamp = timestamp;
            errorInfo.Message = "Test error message";
            errorInfo.ErrorType = "InvalidOperationException";
            errorInfo.StackTrace = "at TestMethod() in TestFile.cs:line 42";
            errorInfo.ExecutionId = executionId;
            errorInfo.ConsecutiveErrors = 5;

            // Assert
            Assert.Equal(timestamp, errorInfo.Timestamp);
            Assert.Equal("Test error message", errorInfo.Message);
            Assert.Equal("InvalidOperationException", errorInfo.ErrorType);
            Assert.Equal("at TestMethod() in TestFile.cs:line 42", errorInfo.StackTrace);
            Assert.Equal(executionId, errorInfo.ExecutionId);
            Assert.Equal(5, errorInfo.ConsecutiveErrors);
        }
    }

    /// <summary>
    /// Tests for TimerMemoryInfo class
    /// </summary>
    public class TimerMemoryInfoTests
    {
        [Fact]
        public void TimerMemoryInfo_ShouldHaveDefaultValues()
        {
            // Act
            var memoryInfo = new TimerMemoryInfo();

            // Assert
            Assert.Equal(0, memoryInfo.CurrentMemoryUsage);
            Assert.Equal(0, memoryInfo.PeakMemoryUsage);
            Assert.Equal(0, memoryInfo.TrackedObjects);
            Assert.Null(memoryInfo.GarbageCollectionInfo);
        }

        [Fact]
        public void TimerMemoryInfo_ShouldAllowSettingAllProperties()
        {
            // Arrange
            var memoryInfo = new TimerMemoryInfo();

            // Act
            memoryInfo.CurrentMemoryUsage = 1024 * 1024; // 1MB
            memoryInfo.PeakMemoryUsage = 2 * 1024 * 1024; // 2MB
            memoryInfo.TrackedObjects = 42;
            memoryInfo.GarbageCollectionInfo = "Gen0: 10, Gen1: 2, Gen2: 0";

            // Assert
            Assert.Equal(1024 * 1024, memoryInfo.CurrentMemoryUsage);
            Assert.Equal(2 * 1024 * 1024, memoryInfo.PeakMemoryUsage);
            Assert.Equal(42, memoryInfo.TrackedObjects);
            Assert.Equal("Gen0: 10, Gen1: 2, Gen2: 0", memoryInfo.GarbageCollectionInfo);
        }
    }

    /// <summary>
    /// Tests for TimerSystemInfo class
    /// </summary>
    public class TimerSystemInfoTests
    {
        [Fact]
        public void TimerSystemInfo_ShouldHaveDefaultValues()
        {
            // Act
            var systemInfo = new TimerSystemInfo();

            // Assert
            Assert.Null(systemInfo.OSVersion);
            Assert.Null(systemInfo.RuntimeVersion);
            Assert.Equal(0, systemInfo.ProcessorCount);
            Assert.Equal(0, systemInfo.WorkingSet);
            Assert.Equal(default(TimeSpan), systemInfo.SystemUptime);
        }

        [Fact]
        public void TimerSystemInfo_ShouldAllowSettingAllProperties()
        {
            // Arrange
            var systemInfo = new TimerSystemInfo();

            // Act
            systemInfo.OSVersion = "Windows 10.0.19042";
            systemInfo.RuntimeVersion = "5.0.0";
            systemInfo.ProcessorCount = 8;
            systemInfo.WorkingSet = 1024 * 1024 * 1024; // 1GB
            systemInfo.SystemUptime = TimeSpan.FromDays(7);

            // Assert
            Assert.Equal("Windows 10.0.19042", systemInfo.OSVersion);
            Assert.Equal("5.0.0", systemInfo.RuntimeVersion);
            Assert.Equal(8, systemInfo.ProcessorCount);
            Assert.Equal(1024 * 1024 * 1024, systemInfo.WorkingSet);
            Assert.Equal(TimeSpan.FromDays(7), systemInfo.SystemUptime);
        }
    }
}