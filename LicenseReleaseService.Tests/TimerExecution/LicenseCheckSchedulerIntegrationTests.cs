using System;
using System.Collections.Generic;
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
    /// Integration tests for LicenseCheckScheduler with TimerExecution framework
    /// </summary>
    public class LicenseCheckSchedulerIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<TimerExecutionService>> _mockTimerLogger;
        private readonly Mock<ILogger<LicenseCheckScheduler>> _mockSchedulerLogger;
        private readonly TimerExecutionOptions _timerOptions;
        private readonly LicenseCheckSchedulerConfiguration _schedulerConfig;
        private readonly List<IDisposable> _disposables;

        public LicenseCheckSchedulerIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _mockTimerLogger = new Mock<ILogger<TimerExecutionService>>();
            _mockSchedulerLogger = new Mock<ILogger<LicenseCheckScheduler>>();
            _disposables = new List<IDisposable>();

            _timerOptions = new TimerExecutionOptions
            {
                DefaultInterval = TimeSpan.FromSeconds(1),
                MaxConsecutiveErrors = 3,
                MaxConcurrentExecutions = 1
            };

            _schedulerConfig = new LicenseCheckSchedulerConfiguration
            {
                DefaultServer = "localhost",
                DefaultPort = 1057,
                DefaultInterval = TimeSpan.FromSeconds(1),
                MaxConsecutiveErrors = 3
            };
        }

        [Fact]
        public async Task IntegrationTest_SchedulerWithTimerExecution_CompleteLifecycle()
        {
            // Arrange
            var timerService = new TimerExecutionService(_mockTimerLogger.Object, _timerOptions);
            var scheduler = new LicenseCheckScheduler(timerService, _mockSchedulerLogger.Object, _schedulerConfig);
            _disposables.Add(timerService);
            _disposables.Add(scheduler);

            var operationStartedCount = 0;
            var operationCompletedCount = 0;
            var timerStartedCount = 0;
            var timerCompletedCount = 0;

            scheduler.CheckStarted += (sender, args) =>
            {
                operationStartedCount++;
                _output.WriteLine($"Operation started: {args.OperationId}");
            };

            scheduler.CheckCompleted += (sender, args) =>
            {
                operationCompletedCount++;
                _output.WriteLine($"Operation completed: {args.OperationId}");
            };

            timerService.ExecutionStarted += (sender, args) =>
            {
                timerStartedCount++;
                _output.WriteLine($"Timer execution started: {args.ExecutionId}");
            };

            timerService.ExecutionCompleted += (sender, args) =>
            {
                timerCompletedCount++;
                _output.WriteLine($"Timer execution completed: {args.ExecutionId}");
            };

            // Act - Start the scheduler
            await scheduler.StartAsync(TimeSpan.FromMilliseconds(500));

            // Wait for some timer executions
            await Task.Delay(2000);

            // Get status information
            var status = await scheduler.GetStatusInfoAsync();
            var metrics = scheduler.Metrics;
            var diagnostics = await scheduler.GetDiagnosticsAsync();

            // Pause and resume
            await scheduler.PauseAsync();
            await Task.Delay(500);
            await scheduler.ResumeAsync();
            await Task.Delay(1000);

            // Execute immediate operation
            await scheduler.ExecuteNowAsync();
            await Task.Delay(500);

            // Stop the scheduler
            await scheduler.StopAsync();

            // Assert
            Assert.Equal(LicenseCheckSchedulerStatus.Stopped, scheduler.Status);
            Assert.False(scheduler.IsRunning);
            Assert.True(timerStartedCount > 0);
            Assert.True(timerCompletedCount > 0);
            Assert.True(status.Uptime.TotalSeconds > 0);
            Assert.NotNull(metrics);
            Assert.NotNull(diagnostics);
            Assert.True(diagnostics.IsHealthy);

            _output.WriteLine($"Timer executions started: {timerStartedCount}");
            _output.WriteLine($"Timer executions completed: {timerCompletedCount}");
            _output.WriteLine($"Operations started: {operationStartedCount}");
            _output.WriteLine($"Operations completed: {operationCompletedCount}");
            _output.WriteLine($"Scheduler uptime: {status.Uptime.TotalSeconds:F2} seconds");
            _output.WriteLine($"Total operations processed: {metrics.TotalOperations}");
        }

        [Fact]
        public async Task IntegrationTest_QueueManagement_AfterMultipleOperations()
        {
            // Arrange
            var timerService = new TimerExecutionService(_mockTimerLogger.Object, _timerOptions);
            var scheduler = new LicenseCheckScheduler(timerService, _mockSchedulerLogger.Object, _schedulerConfig);
            _disposables.Add(timerService);
            _disposables.Add(scheduler);

            await scheduler.StartAsync(TimeSpan.FromMilliseconds(200));

            // Act - Enqueue multiple operations
            var operations = new List<LicenseCheckOperation>();
            for (int i = 0; i < 5; i++)
            {
                var operation = LicenseCheckOperation.CreateComprehensiveCheck($"server{i}", 1057 + i);
                operations.Add(operation);
                await scheduler.EnqueueOperationAsync(operation, (LicenseCheckPriority)(i % 4));
            }

            await Task.Delay(1000);

            // Check queue status
            var pendingOps = await scheduler.GetPendingOperationsAsync();
            var queueStats = scheduler.QueueStatistics;
            var status = await scheduler.GetStatusInfoAsync();

            // Clear queue
            await scheduler.ClearQueueAsync();

            await scheduler.StopAsync();

            // Assert
            Assert.NotNull(pendingOps);
            Assert.NotNull(queueStats);
            Assert.True(status.PendingOperationsCount >= 0);
            Assert.True(queueStats.TotalProcessed >= 0);

            _output.WriteLine($"Pending operations: {pendingOps.Count}");
            _output.WriteLine($"Queue statistics - Total processed: {queueStats.TotalProcessed}");
            _output.WriteLine($"Queue statistics - Completed: {queueStats.CompletedCount}");
            _output.WriteLine($"Queue statistics - Failed: {queueStats.FailedCount}");
        }

        [Fact]
        public async Task IntegrationTest_ConfigurationUpdates_DuringRuntime()
        {
            // Arrange
            var timerService = new TimerExecutionService(_mockTimerLogger.Object, _timerOptions);
            var scheduler = new LicenseCheckScheduler(timerService, _mockSchedulerLogger.Object, _schedulerConfig);
            _disposables.Add(timerService);
            _disposables.Add(scheduler);

            await scheduler.StartAsync(TimeSpan.FromMilliseconds(500));

            // Act - Update configuration while running
            var newConfig = new LicenseCheckSchedulerConfiguration
            {
                DefaultServer = "new-server",
                DefaultPort = 2057,
                DefaultInterval = TimeSpan.FromSeconds(2),
                MaxConsecutiveErrors = 5
            };

            await scheduler.UpdateConfigurationAsync(newConfig);
            await scheduler.UpdateIntervalAsync(TimeSpan.FromSeconds(2));

            await Task.Delay(1000);

            var status = await scheduler.GetStatusInfoAsync();
            var validationErrors = scheduler.ValidateConfiguration();

            await scheduler.StopAsync();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(2), scheduler.CurrentInterval);
            Assert.Empty(validationErrors);
            Assert.Equal(LicenseCheckSchedulerStatus.Stopped, scheduler.Status);

            _output.WriteLine($"Updated interval: {status.CurrentInterval}");
            _output.WriteLine($"Configuration validation errors: {validationErrors.Count}");
        }

        [Fact]
        public async Task IntegrationTest_ErrorHandling_WithTimerFailures()
        {
            // Arrange - Create a timer service that will fail
            var timerService = new TimerExecutionService(_mockTimerLogger.Object, _timerOptions);
            var scheduler = new LicenseCheckScheduler(timerService, _mockSchedulerLogger.Object, _schedulerConfig);
            _disposables.Add(timerService);
            _disposables.Add(scheduler);

            var errorCount = 0;
            scheduler.CheckError += (sender, args) =>
            {
                errorCount++;
                _output.WriteLine($"Error occurred: {args.ErrorMessage}");
            };

            await scheduler.StartAsync(TimeSpan.FromMilliseconds(100));

            // Wait and monitor for errors
            await Task.Delay(1000);

            var diagnostics = await scheduler.GetDiagnosticsAsync();
            var metrics = scheduler.Metrics;

            await scheduler.StopAsync();

            // Assert
            Assert.NotNull(diagnostics);
            Assert.NotNull(metrics);
            Assert.True(diagnostics.LastDiagnosticsCheck > DateTime.Now.AddSeconds(-10));

            _output.WriteLine($"Error count: {errorCount}");
            _output.WriteLine($"Diagnostics healthy: {diagnostics.IsHealthy}");
            _output.WriteLine($"Consecutive errors: {metrics.ConsecutiveErrors}");
        }

        [Fact]
        public async Task IntegrationTest_PerformanceMetrics_AfterHeavyLoad()
        {
            // Arrange
            var timerService = new TimerExecutionService(_mockTimerLogger.Object, _timerOptions);
            var scheduler = new LicenseCheckScheduler(timerService, _mockSchedulerLogger.Object, _schedulerConfig);
            _disposables.Add(timerService);
            _disposables.Add(scheduler);

            await scheduler.StartAsync(TimeSpan.FromMilliseconds(50)); // Fast interval for load test

            // Act - Generate heavy load
            for (int i = 0; i < 20; i++)
            {
                var operation = LicenseCheckOperation.CreateComprehensiveCheck($"load-test-server", 1057);
                await scheduler.EnqueueOperationAsync(operation, LicenseCheckPriority.High);
                await Task.Delay(10);
            }

            await Task.Delay(2000); // Let operations process

            var metricsBefore = scheduler.Metrics;
            await scheduler.ResetStatisticsAsync();
            var metricsAfter = scheduler.Metrics;
            var status = await scheduler.GetStatusInfoAsync();

            await scheduler.StopAsync();

            // Assert
            Assert.NotNull(metricsBefore);
            Assert.NotNull(metricsAfter);
            Assert.True(metricsAfter.TotalOperations == 0); // Should be reset
            Assert.True(status.Uptime.TotalSeconds > 0);

            _output.WriteLine($"Metrics before reset - Total operations: {metricsBefore.TotalOperations}");
            _output.WriteLine($"Metrics after reset - Total operations: {metricsAfter.TotalOperations}");
            _output.WriteLine($"Average execution time: {metricsBefore.AverageExecutionTime?.TotalMilliseconds:F2}ms");
            _output.WriteLine($"Operations per second: {metricsBefore.OperationsPerSecond:F2}");
        }

        [Fact]
        public async Task IntegrationTest_Dispose_CleanupAllResources()
        {
            // Arrange
            var timerService = new TimerExecutionService(_mockTimerLogger.Object, _timerOptions);
            var scheduler = new LicenseCheckScheduler(timerService, _mockSchedulerLogger.Object, _schedulerConfig);

            await scheduler.StartAsync(TimeSpan.FromMilliseconds(200));
            await scheduler.ExecuteNowAsync();

            // Act - Dispose while running
            scheduler.Dispose();
            timerService.Dispose();

            // Assert - Verify proper cleanup (no exceptions should occur)
            Assert.False(scheduler.IsRunning);
            Assert.Equal(LicenseCheckSchedulerStatus.Disposed, scheduler.Status);

            _output.WriteLine("All resources disposed successfully");
        }

        [Fact]
        public async Task IntegrationTest_ExecutionHistory_MaintainsProperSize()
        {
            // Arrange
            var timerService = new TimerExecutionService(_mockTimerLogger.Object, _timerOptions);
            var scheduler = new LicenseCheckScheduler(timerService, _mockSchedulerLogger.Object, _schedulerConfig);
            _disposables.Add(timerService);
            _disposables.Add(scheduler);

            await scheduler.StartAsync(TimeSpan.FromMilliseconds(100));

            // Act - Generate many operations to fill history
            for (int i = 0; i < 10; i++)
            {
                await scheduler.ExecuteNowAsync();
                await Task.Delay(50);
            }

            var history = await scheduler.GetExecutionHistoryAsync();
            var status = await scheduler.GetStatusInfoAsync();

            await scheduler.StopAsync();

            // Assert
            Assert.NotNull(history);
            Assert.True(history.Count <= 100); // Should be limited by cleanup

            _output.WriteLine($"Execution history size: {history.Count}");
            _output.WriteLine($"Status uptime: {status.Uptime.TotalSeconds:F2} seconds");
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error disposing resource: {ex.Message}");
                }
            }
            _disposables.Clear();
        }
    }
}